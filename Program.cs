using Avalonia;
using System;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Startup;
using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung;

sealed class Program
{
    /// <summary>
    /// Der belegte Platz dieser Ausfuehrung. <see cref="App"/> haengt den
    /// Horcher daran und gibt ihn beim Beenden frei.
    /// </summary>
    internal static SingleInstance? Einzelinstanz { get; private set; }

    /// <summary>
    /// Es laeuft bereits eine Ausfuehrung, sie hat sich aber nicht
    /// gemeldet. Dann wird Avalonia doch noch gestartet - nur um das zu
    /// sagen. Ein zweiter Start, der einfach nichts tut, waere genau das
    /// wortlose Verschwinden, das es nicht geben soll.
    /// </summary>
    internal static bool LaeuftBereitsOhneAntwort { get; private set; }

    /// <summary>
    /// Der Pfad der Datenbank, einmal ermittelt. NULL, wenn schon das
    /// nicht ging - dann ist der Anwendungsdatenordner nicht erreichbar,
    /// und <see cref="App"/> meldet das.
    /// </summary>
    internal static string? DatenbankPfad { get; private set; }

    /// <summary>Was beim Ermitteln der Pfade schiefging, sonst NULL.</summary>
    internal static Exception? Pfadfehler { get; private set; }

    /// <summary>
    /// Was die Uebernahme einer bereitliegenden Wiederherstellung ergab.
    /// NULL im Normalfall - dann lag nichts bereit. <see cref="App"/> gibt
    /// den Text an den Bereich "Datensicherung" weiter, von dem aus der
    /// Vorgang angestossen wurde.
    ///
    /// Steht hier und nicht in StartupResult, weil die Uebernahme VOR dem
    /// Start der Datenbank laeuft - sie entscheidet ja, welche Datenbank
    /// ueberhaupt gestartet wird.
    /// </summary>
    internal static string? Wiederherstellungsmeldung { get; private set; }

    /// <summary>
    /// Ob die Meldung ein Fehlschlag ist. Nur zusammen mit
    /// <see cref="Wiederherstellungsmeldung"/> von Belang.
    /// </summary>
    internal static bool WiederherstellungGescheitert { get; private set; }

    // Vor AppMain darf nichts aus Avalonia benutzt werden - es ist noch
    // nichts eingerichtet. Protokoll, Pfade und die Doppelstart-Pruefung
    // kommen alle ohne aus, und genau deshalb stehen sie hier: sie
    // entscheiden, OB ueberhaupt ein Fenster aufgeht.
    [STAThread]
    public static void Main(string[] args)
    {
        StarteProtokoll();

        // Ein Neustart nach einem Austausch: der Vorgaenger endet gerade
        // erst und haelt die Einzelinstanz-Sperre noch. Ohne dieses
        // Warten wuerde der Start gleich unten als Doppelstart abgewiesen
        // und die Anwendung waere nach dem Update schlicht weg.
        Neustart.WarteAufVorgaenger(args);

        // Vor allem anderen: liegt eine geladene Fassung bereit? Der
        // Austausch gehoert genau hierher - vor Datenbank, Fenster und
        // Einzelinstanz-Sperre haengt nichts daran, was mitten im
        // Wechsel Schaden nehmen koennte.
        if (UebernehmeAktualisierungFalls())
        {
            return;
        }

        ErmittleDatenbankPfad();

        // Und danach, aber immer noch vor allem Weiteren: liegt eine
        // Sicherung zum Einspielen bereit? Derselbe Zeitpunkt aus demselben
        // Grund wie beim Programmaustausch - hier haengt nichts an der
        // Datenbankdatei, gleich haengt die ganze Anwendung daran.
        UebernehmeWiederherstellungFalls();

        if (DatenbankPfad is not null && !BelegePlatz(DatenbankPfad))
        {
            // Die laufende Ausfuehrung hat ihr Fenster nach vorn geholt.
            // Hier ist nichts mehr zu tun - und ausdruecklich KEIN
            // Fenster zu oeffnen, sonst staenden zwei da.
            Einzelinstanz?.Dispose();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    // Das Protokoll ist das Erste, was laeuft: alles danach soll bereits
    // hineinschreiben koennen. AppLog.Start schluckt seine eigenen Fehler,
    // das Ermitteln des Ordners davor nicht - deshalb dieser Rahmen.
    private static void StarteProtokoll()
    {
        try
        {
            AppLog.Start(AppPaths.GetLogFolderPath(), DateOnly.FromDateTime(DateTime.Now));
        }
        catch (Exception)
        {
            // Ohne Protokollordner laeuft die Anwendung ohne Protokoll
            // weiter. AppLog.Current bleibt dann die stille Ausfuehrung.
        }
    }

    /// <summary>
    /// Uebernimmt eine bereitliegende Fassung. Liefert <c>true</c>, wenn
    /// dieser Prozess sich daraufhin sofort beenden soll - der Nachfolger
    /// laeuft dann bereits.
    ///
    /// Scheitert irgendetwas, laeuft der Start ganz normal in der
    /// bisherigen Fassung weiter. Das ist immer die bessere von beiden
    /// Moeglichkeiten (siehe <see cref="UpdateInstaller"/>).
    /// </summary>
    private static bool UebernehmeAktualisierungFalls()
    {
        try
        {
            var ziel = UpdateInstaller.ZielPfad();
            if (ziel is null)
            {
                return false;
            }

            // Der Rest des letzten Austauschs. Beim allerersten Start
            // danach kann er noch gesperrt sein, weil der Vorgaenger eben
            // erst endete - dann bleibt er liegen und der naechste Start
            // versucht es erneut.
            UpdateInstaller.RaeumeAlteAuf(ziel);

            if (UpdateInstaller.TryUebernehmen(ziel) != UpdateInstaller.Ergebnis.Uebernommen)
            {
                return false;
            }

            // Getauscht: den Nachfolger starten und selbst Platz machen.
            // Laesst er sich nicht starten, laeuft dieser Prozess mit der
            // BEREITS AUSGETAUSCHTEN Datei weiter - im Speicher steht noch
            // die alte Fassung, was fuer diese eine Sitzung folgenlos
            // bleibt; ab dem naechsten Start gilt die neue.
            return UpdateInstaller.StarteNeuenProzess(ziel);
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Uebernehmen einer Aktualisierung", ex);
            return false;
        }
    }

    /// <summary>
    /// Spielt eine bereitliegende Sicherung ein, falls eine bereitliegt.
    /// Anders als beim Programmaustausch endet dieser Prozess danach NICHT:
    /// die Datei ist ersetzt, bevor sie irgendwer geoeffnet hat, und der
    /// Start laeuft ganz normal weiter - nur eben auf dem eingespielten
    /// Stand.
    ///
    /// Scheitert etwas, wird die Vorbereitung verworfen und mit der
    /// bisherigen Datenbank gestartet (siehe
    /// <see cref="BackupRestore"/>). Beides bekommt der Anwender zu lesen,
    /// sonst bliebe offen, auf welchem Stand er gerade arbeitet.
    /// </summary>
    private static void UebernehmeWiederherstellungFalls()
    {
        if (DatenbankPfad is not string datenbankPfad)
        {
            return;
        }

        var (ergebnis, zettel) = BackupRestore.TryUebernehmen(datenbankPfad);

        switch (ergebnis)
        {
            case BackupRestore.Ergebnis.Uebernommen when zettel is not null:
                Wiederherstellungsmeldung = RestoreText.Uebernommen(
                    zettel.Quelldatei, zettel.Sicherheitskopie);
                break;

            case BackupRestore.Ergebnis.Gescheitert:
                Wiederherstellungsmeldung = RestoreText.Verworfen();
                WiederherstellungGescheitert = true;
                break;
        }
    }

    private static void ErmittleDatenbankPfad()
    {
        try
        {
            DatenbankPfad = AppPaths.GetDatabaseFilePath();
        }
        catch (Exception ex)
        {
            // Der Anwendungsdatenordner laesst sich nicht anlegen. Das
            // Fenster geht trotzdem auf und sagt es.
            Pfadfehler = ex;
            AppLog.Current.Exception("Beim Ermitteln des Datenbankpfads", ex);
        }
    }

    /// <summary>
    /// Liefert <c>true</c>, wenn diese Ausfuehrung weiterlaufen darf.
    /// </summary>
    private static bool BelegePlatz(string datenbankPfad)
    {
        Einzelinstanz = SingleInstance.Acquire(datenbankPfad);

        if (Einzelinstanz.IsFirstInstance)
        {
            AppLog.Current.Info(
                LogEvents.ProgramStart(GlobaleFehlerbehandlung.Version, datenbankPfad));
            return true;
        }

        if (Einzelinstanz.SignalExistingInstance())
        {
            return false;
        }

        // Die andere Ausfuehrung antwortet nicht - sie haengt, ist gerade
        // am Beenden oder stammt aus einer aelteren Programmversion. Das
        // Fenster geht auf und erklaert es.
        LaeuftBereitsOhneAntwort = true;
        return true;
    }
}
