using Avalonia;
using System;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Startup;

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

    // Vor AppMain darf nichts aus Avalonia benutzt werden - es ist noch
    // nichts eingerichtet. Protokoll, Pfade und die Doppelstart-Pruefung
    // kommen alle ohne aus, und genau deshalb stehen sie hier: sie
    // entscheiden, OB ueberhaupt ein Fenster aufgeht.
    [STAThread]
    public static void Main(string[] args)
    {
        StarteProtokoll();
        ErmittleDatenbankPfad();

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
