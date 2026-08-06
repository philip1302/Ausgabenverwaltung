using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Ausgabenverwaltung;

/// <summary>
/// Das Auffangnetz fuer alles, was sonst niemand faengt.
///
/// Ohne Terminal gibt es keine Konsole, auf der eine nicht behandelte
/// Ausnahme noch erschiene. Sie wuerde entweder still verschluckt (die
/// Anwendung reagiert dann einfach nicht mehr auf einen Knopf) oder sie
/// nimmt den Prozess mit - beides sieht fuer den Anwender gleich aus: das
/// Fenster ist weg oder tut nichts, und niemand hat ihm etwas gesagt.
/// Genau das soll hier nicht mehr vorkommen.
///
/// Drei Ebenen, weil Ausnahmen aus drei Richtungen kommen:
///
/// 1. <b>Oberflaechen-Thread.</b> Alles, was in einem Ereignishandler oder
///    einem Command passiert. Die haeufigste Richtung. Die Ausnahme wird
///    als behandelt markiert, die Anwendung laeuft weiter.
///
/// 2. <b>Nicht abgewartete Tasks.</b> Ein <c>async void</c> oder ein
///    vergessenes <c>await</c>. Diese Ausnahmen tauchen erst beim
///    Aufraeumen durch die Speicherbereinigung auf - unter Umstaenden
///    Minuten spaeter und ohne jeden Zusammenhang zur Bedienung. Ins
///    Protokoll gehoeren sie trotzdem.
///
/// 3. <b>Alles Uebrige.</b> Ein eigener Thread, ein Fehler beim
///    Herunterfahren. Hier ist der Prozess nicht mehr zu retten - das
///    Einzige, was bleibt, ist es zu sagen, bevor er geht.
/// </summary>
public static class GlobaleFehlerbehandlung
{
    // Verhindert, dass ein Fehler IM Fehlerdialog einen zweiten
    // Fehlerdialog oeffnet und so weiter. Ein Anwender, der sich durch
    // dreissig Fenster klickt, ist niemandem geholfen.
    private static int _zeigtGerade;

    // Wie lange der sterbende Prozess auf den Anwender wartet. Danach geht
    // er ohnehin - besser ein geschlossener Dialog als ein Fenster, das
    // fuer immer stehen bleibt.
    private static readonly TimeSpan MaximaleWartezeit = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Haengt die drei Auffanglinien ein. Muss vor dem ersten Fenster
    /// laufen.
    /// </summary>
    public static void Einrichten()
    {
        // Ohne diesen Filter faengt der Dispatcher die Ausnahme je nach
        // Umstand gar nicht erst ab, und das Ereignis darunter kaeme nie.
        Dispatcher.UIThread.UnhandledExceptionFilter += (_, e) => e.RequestCatch = true;

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            // Als behandelt markieren, BEVOR der Dialog kommt: sonst
            // reisst dieselbe Ausnahme die Anwendung mit, waehrend der
            // Dialog noch aufgeht.
            e.Handled = true;

            Behandle("beim Bedienen der Anwendung", e.Exception, fortfahrenMoeglich: true);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            // Als beobachtet markieren, damit die Ausnahme den Prozess
            // nicht beim naechsten Aufraeumlauf mitnimmt.
            e.SetObserved();

            var ausnahme = (Exception?)e.Exception?.InnerException ?? e.Exception;
            if (ausnahme is null)
            {
                return;
            }

            AppLog.Current.Exception("In einer nicht abgewarteten Hintergrundaufgabe", ausnahme);

            if (IstDBusPlattformEinstellungenFehler(ausnahme))
            {
                // Avalonia fragt beim Start unaufgefordert per D-Bus das
                // System-Theme ab. Fehlt der Session-Bus komplett (z.B. in
                // einem Container ohne Desktop-Umgebung), wirft Avalonia
                // dabei eine NullReferenceException statt sauber zu
                // scheitern - ein Fehler in Avalonia selbst, nicht in
                // dieser Anwendung. Ein echter Linux-Desktop hat immer
                // einen laufenden Session-Bus, dort greift dieser Fall
                // nie. Geloggt ist die Ausnahme oben schon; ein Dialog
                // dafuer haette dem Anwender nichts zu sagen.
                return;
            }

            // Der Dialog kommt auf den Oberflaechen-Thread: dieses
            // Ereignis wird vom Finalizer-Thread ausgeloest, und von dort
            // laesst sich kein Fenster oeffnen.
            Dispatcher.UIThread.Post(() =>
                Behandle("in einer Hintergrundaufgabe", ausnahme, fortfahrenMoeglich: true));
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception ausnahme)
            {
                return;
            }

            // Ab hier ist der Prozess verloren - die Laufzeitumgebung
            // beendet ihn, sobald dieser Handler zurueckkehrt. "Fortfahren"
            // waere deshalb eine Zusage, die niemand einhalten kann.
            BehandleToedlich(ausnahme);
        };
    }

    /// <summary>
    /// Meldet eine Ausnahme, die ein Aufrufer selbst gefangen hat, auf
    /// demselben Weg wie eine nicht behandelte. Fuer Stellen, die
    /// weiterlaufen koennen, den Fehler aber nicht verschweigen duerfen.
    /// </summary>
    public static void Melde(string zusammenhang, Exception ausnahme)
        => Behandle(zusammenhang, ausnahme, fortfahrenMoeglich: true);

    /// <summary>Die Version der Anwendung - fuer Protokoll und Fehlerdialog.</summary>
    public static string Version { get; } = LiesVersion();

    private static void Behandle(string zusammenhang, Exception ausnahme, bool fortfahrenMoeglich)
    {
        AppLog.Current.Exception($"Nicht behandelte Ausnahme {zusammenhang}", ausnahme);

        // Interlocked statt eines einfachen bool: dieser Weg kann von
        // mehreren Threads zugleich betreten werden.
        if (Interlocked.Exchange(ref _zeigtGerade, 1) == 1)
        {
            return;
        }

        try
        {
            ZeigeDialog(zusammenhang, ausnahme, fortfahrenMoeglich);
        }
        catch (Exception dialogFehler)
        {
            // Selbst der Fehlerdialog laesst sich nicht mehr oeffnen.
            // Mehr als das Protokoll bleibt dann nicht.
            AppLog.Current.Exception("Beim Anzeigen des Fehlerdialogs", dialogFehler);
            Interlocked.Exchange(ref _zeigtGerade, 0);
        }
    }

    private static void ZeigeDialog(string zusammenhang, Exception ausnahme, bool fortfahrenMoeglich)
    {
        var bericht = UnexpectedErrorText.Describe(
            zusammenhang, ausnahme, AppLog.Current.FolderPath, Version);

        var dialog = new FehlerDialog(bericht, fortfahrenMoeglich);

        dialog.Closed += (_, _) =>
        {
            Interlocked.Exchange(ref _zeigtGerade, 0);

            if (dialog.Antwort == FehlerAntwort.Beenden)
            {
                Beende();
            }
        };

        var besitzer = HauptfensterOderNull();

        if (besitzer is not null)
        {
            // Bewusst nicht abgewartet: dieser Aufruf kommt aus einem
            // Ereignishandler, der zurueckkehren muss, damit der
            // Oberflaechen-Thread den Dialog ueberhaupt zeichnen kann.
            _ = dialog.ShowDialog(besitzer);
        }
        else
        {
            dialog.Show();
        }
    }

    // Der Prozess geht ohnehin zu Ende. Der Dialog wird deshalb auf dem
    // Oberflaechen-Thread geoeffnet, und der ausloesende Thread wartet, bis
    // der Anwender ihn gelesen hat - sonst waere das Fenster weg, bevor es
    // jemand sieht.
    private static void BehandleToedlich(Exception ausnahme)
    {
        AppLog.Current.Exception("Nicht behandelte Ausnahme, die Anwendung wird beendet", ausnahme);

        if (Interlocked.Exchange(ref _zeigtGerade, 1) == 1)
        {
            return;
        }

        try
        {
            // Auf dem Oberflaechen-Thread selbst kann nicht gewartet
            // werden - das waere ein Stillstand, bei dem der Dialog nie
            // gezeichnet wuerde.
            if (Dispatcher.UIThread.CheckAccess())
            {
                ZeigeDialog("beim Ausführen der Anwendung", ausnahme, fortfahrenMoeglich: false);
                return;
            }

            using var fertig = new ManualResetEventSlim(false);

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var bericht = UnexpectedErrorText.Describe(
                        "beim Ausführen der Anwendung", ausnahme, AppLog.Current.FolderPath, Version);

                    var dialog = new FehlerDialog(bericht, fortfahrenMoeglich: false);
                    dialog.Closed += (_, _) => fertig.Set();
                    dialog.Show();
                }
                catch (Exception dialogFehler)
                {
                    AppLog.Current.Exception("Beim Anzeigen des letzten Fehlerdialogs", dialogFehler);
                    fertig.Set();
                }
            });

            fertig.Wait(MaximaleWartezeit);
        }
        catch (Exception dialogFehler)
        {
            AppLog.Current.Exception("Beim Anzeigen des letzten Fehlerdialogs", dialogFehler);
        }
    }

    // Erkennt den Avalonia-Fehler beim D-Bus-Zugriff auf das System-Theme
    // (siehe Kommentar oben bei TaskScheduler.UnobservedTaskException)
    // anhand seines Ursprungs im Aufrufstapel - unabhaengig vom Text, der
    // sich mit Avalonia-Versionen aendern kann.
    private static bool IstDBusPlattformEinstellungenFehler(Exception ausnahme)
        => ausnahme.StackTrace?.Contains(
            "Avalonia.FreeDesktop.DBusPlatformSettings", StringComparison.Ordinal) == true;

    private static Window? HauptfensterOderNull()
    {
        if (Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        // Ein noch nicht geoeffnetes oder bereits geschlossenes Fenster
        // taugt nicht als Besitzer - ShowDialog wuerde damit werfen.
        return desktop.MainWindow is { IsVisible: true } fenster ? fenster : null;
    }

    private static void Beende()
    {
        AppLog.Current.Info(LogEvents.ProgramEnd());

        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private static string LiesVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            // Die Informationsversion ist die, die auch im Explorer steht;
            // fehlt sie, tut es die Baugruppenversion.
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            return informational
                ?? assembly.GetName().Version?.ToString()
                ?? "unbekannt";
        }
        catch (Exception)
        {
            return "unbekannt";
        }
    }
}
