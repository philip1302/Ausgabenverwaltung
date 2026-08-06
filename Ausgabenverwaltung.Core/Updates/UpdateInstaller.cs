using System.Globalization;
using System.Runtime.InteropServices;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Der Austausch selbst - und zwar GANZ FRUEH beim Programmstart, bevor
/// Protokollordner, Datenbank, Einzelinstanz-Sperre oder Fenster
/// existieren.
///
/// Der Zeitpunkt ist der eigentliche Kunstgriff. Mitten im Betrieb waere
/// der Austausch heikel: die Datenbankverbindung steht, ein halb
/// getauschter Zustand bei einem Stromausfall waere schwer zu ordnen, und
/// unter Windows laesst sich eine laufende Programmdatei ohnehin nicht
/// ueberschreiben. Beim Start dagegen haengt nichts daran - schlaegt der
/// Austausch fehl, wird er verworfen und die Anwendung startet ganz
/// normal in der alten Fassung weiter.
///
/// Windows kann eine LAUFENDE Programmdatei zwar nicht ueberschreiben,
/// aber sehr wohl umbenennen. Genau darauf beruht der Ablauf: die alte
/// Datei wird zur Seite benannt, die neue nimmt ihren Platz ein, und der
/// neue Prozess startet. Unter macOS gilt dasselbe fuer das Bundle -
/// Unix benennt ueber den Inode um, das laufende Programm stoert das
/// nicht.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>
    /// Aufrufmerkmal fuer den neu gestarteten Prozess: warte, bis der
    /// Vorgaenger wirklich beendet ist. Ohne dieses Warten wuerde
    /// <see cref="Startup.SingleInstance"/> den Neustart als Doppelstart
    /// abweisen, und die Anwendung waere nach dem Update schlicht weg.
    /// </summary>
    public const string WarteMerkmal = "--warte-auf-prozess";

    /// <summary>
    /// Wie lange der neue Prozess auf das Ende des alten wartet. Grosszuegig
    /// bemessen: laenger zu warten kostet nur Sekunden, zu frueh
    /// aufzugeben kostet den Start.
    /// </summary>
    private static readonly TimeSpan WarteHoechstdauer = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Das Ergebnis eines Uebernahmeversuchs.
    /// </summary>
    public enum Ergebnis
    {
        /// <summary>Nichts lag bereit - der Normalfall.</summary>
        NichtsVorbereitet,

        /// <summary>Getauscht; der Aufrufer muss den neuen Prozess starten
        /// und sich sofort beenden.</summary>
        Uebernommen,

        /// <summary>Es lag etwas bereit, liess sich aber nicht uebernehmen.
        /// Es wurde verworfen, der Start laeuft normal weiter.</summary>
        Gescheitert,
    }

    /// <summary>
    /// Was ausgetauscht wird: unter Windows die Programmdatei selbst,
    /// unter macOS das ganze .app-Bundle, in dem sie steckt.
    ///
    /// <c>null</c>, wenn sich das nicht bestimmen laesst (etwa weil die
    /// Anwendung nicht als eigenstaendige Datei laeuft) - dann findet
    /// keine Aktualisierung statt.
    /// </summary>
    public static string? ZielPfad()
    {
        try
        {
            var eigenerPfad = Environment.ProcessPath;
            if (string.IsNullOrEmpty(eigenerPfad))
            {
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return eigenerPfad;
            }

            // Unter macOS liegt die ausfuehrbare Datei in
            // <Name>.app/Contents/MacOS/<Name>. Ausgetauscht wird nicht
            // sie allein, sondern das Bundle darum - sonst blieben
            // Info.plist und Symbol auf dem alten Stand.
            var bundle = SucheBundle(eigenerPfad);
            return bundle ?? eigenerPfad;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? SucheBundle(string ausfuehrbareDatei)
    {
        var ordner = Directory.GetParent(ausfuehrbareDatei);

        // Hoechstens drei Ebenen hoch: MacOS -> Contents -> <Name>.app
        for (var i = 0; i < 3 && ordner is not null; i++)
        {
            if (ordner.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                return ordner.FullName;
            }

            ordner = ordner.Parent;
        }

        return null;
    }

    /// <summary>
    /// Entfernt die beiseitegelegte alte Fassung. Beim ersten Start nach
    /// einem Austausch kann sie noch gesperrt sein, weil der Vorgaenger
    /// gerade erst endet - dann bleibt sie liegen und der naechste Start
    /// versucht es erneut. Deshalb still und ohne Aufhebens.
    /// </summary>
    public static void RaeumeAlteAuf(string zielPfad)
        => UpdateStaging.LoescheStill(UpdateStaging.AltPfad(zielPfad));

    /// <summary>
    /// Uebernimmt eine vorbereitete Fassung, falls eine bereitliegt und
    /// sie der Pruefung standhaelt.
    ///
    /// Wirft nie. Jeder Fehlschlag endet mit
    /// <see cref="Ergebnis.Gescheitert"/>, verworfener Vorbereitung und
    /// einem Protokolleintrag - die Anwendung startet dann in der alten
    /// Fassung weiter. Das ist immer die bessere von beiden
    /// Moeglichkeiten: eine veraltete Anwendung ist ein Aergernis, eine
    /// nicht mehr startende ein Schaden.
    /// </summary>
    public static Ergebnis TryUebernehmen(string zielPfad)
    {
        var neu = UpdateStaging.NeuPfad(zielPfad);

        try
        {
            var zettel = UpdateStaging.LiesZettel(zielPfad);

            // Ohne Begleitzettel gilt eine daliegende Datei als
            // unvollstaendig - etwa als Rest eines abgebrochenen
            // Downloads.
            if (zettel is null)
            {
                if (File.Exists(neu) || Directory.Exists(neu))
                {
                    UpdateStaging.Verwirf(zielPfad);
                }

                return Ergebnis.NichtsVorbereitet;
            }

            if (!File.Exists(neu) && !Directory.Exists(neu))
            {
                UpdateStaging.Verwirf(zielPfad);
                return Ergebnis.NichtsVorbereitet;
            }

            // Die Pruefsumme wird hier ein ZWEITES Mal geprueft, obwohl
            // sie beim Herunterladen schon stimmte. Zwischen damals und
            // jetzt liegt mindestens ein Programmende, womoeglich ein
            // Absturz oder ein Virenscanner - und was gleich ausgefuehrt
            // wird, prueft man unmittelbar davor.
            if (!UpdateStaging.PasstZumZettel(neu, zettel))
            {
                AppLog.Current.Warning(LogEvents.UpdateVerworfen(zettel.Version));
                UpdateStaging.Verwirf(zielPfad);
                return Ergebnis.Gescheitert;
            }

            var alt = UpdateStaging.AltPfad(zielPfad);

            // Ein Rest vom vorletzten Mal wuerde das Umbenennen scheitern
            // lassen.
            UpdateStaging.LoescheStill(alt);

            // Der eigentliche Austausch: zwei Umbenennungen innerhalb
            // desselben Ordners. Zwischen ihnen liegt der einzige
            // heikle Augenblick - faellt hier der Strom aus, fehlt die
            // Programmdatei. Deshalb der Umweg ueber ".alt" statt eines
            // Loeschens: die alte Fassung ist bis zuletzt vorhanden und
            // liesse sich von Hand zurueckbenennen.
            Bewege(zielPfad, alt);

            try
            {
                Bewege(neu, zielPfad);
            }
            catch (Exception)
            {
                // Der zweite Schritt ging schief - die alte Fassung
                // zurueckholen, sonst startet gar nichts mehr.
                Bewege(alt, zielPfad);
                throw;
            }

            UpdateStaging.LoescheStill(UpdateStaging.ZettelPfad(zielPfad));

            AppLog.Current.Info(LogEvents.UpdateUebernommen(zettel.Version));

            return Ergebnis.Uebernommen;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Uebernehmen einer Aktualisierung", ex);
            UpdateStaging.Verwirf(zielPfad);

            return Ergebnis.Gescheitert;
        }
    }

    // File.Move kann keine Verzeichnisse, Directory.Move keine Dateien -
    // und unter macOS ist das Ziel ein Verzeichnis (.app-Bundle).
    private static void Bewege(string von, string nach)
    {
        if (Directory.Exists(von))
        {
            Directory.Move(von, nach);
        }
        else
        {
            File.Move(von, nach);
        }
    }

    /// <summary>
    /// Startet die soeben uebernommene Fassung. Der eigene Prozess muss
    /// sich unmittelbar danach beenden - der neue wartet darauf.
    /// </summary>
    public static bool StarteNeuenProzess(string zielPfad)
    {
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = AusfuehrbareDatei(zielPfad),
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(zielPfad) ?? string.Empty,
            };

            start.ArgumentList.Add(WarteMerkmal);
            start.ArgumentList.Add(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            return System.Diagnostics.Process.Start(start) is not null;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Starten der aktualisierten Fassung", ex);
            return false;
        }
    }

    /// <summary>
    /// Unter macOS ist das Ziel das Bundle, gestartet werden muss aber
    /// die Datei darin.
    /// </summary>
    private static string AusfuehrbareDatei(string zielPfad)
    {
        if (!zielPfad.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            return zielPfad;
        }

        var name = Path.GetFileNameWithoutExtension(zielPfad);
        return Path.Combine(zielPfad, "Contents", "MacOS", name);
    }

    /// <summary>
    /// Wartet auf das Ende des Vorgaengerprozesses, dessen Kennung als
    /// <see cref="WarteMerkmal"/>-Argument hereinkam. Ohne dieses Warten
    /// stolperte der Neustart ueber die Einzelinstanz-Sperre des noch
    /// endenden Vorgaengers.
    ///
    /// Kehrt auch zurueck, wenn der Prozess laengst weg ist oder die
    /// Kennung unbrauchbar war - gewartet wird auf ein Ende, nicht auf
    /// eine Bestaetigung.
    /// </summary>
    public static void WarteAufVorgaenger(string[] argumente)
    {
        try
        {
            var index = Array.IndexOf(argumente, WarteMerkmal);
            if (index < 0 || index + 1 >= argumente.Length)
            {
                return;
            }

            if (!int.TryParse(
                    argumente[index + 1], CultureInfo.InvariantCulture, out var pid))
            {
                return;
            }

            using var vorgaenger = System.Diagnostics.Process.GetProcessById(pid);
            vorgaenger.WaitForExit((int)WarteHoechstdauer.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // GetProcessById wirft, wenn es den Prozess nicht mehr gibt -
            // genau der Zustand, auf den gewartet wurde.
        }
        catch (Exception)
        {
            // Auch sonst gilt: nicht warten zu koennen ist kein Grund,
            // den Start abzubrechen.
        }
    }
}
