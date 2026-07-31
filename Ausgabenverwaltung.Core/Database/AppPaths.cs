namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Ermittelt die Pfade der Anwendung. Sie liegen bewusst NICHT neben der
/// EXE, sondern im rollenden Anwendungsdatenordner des Nutzers, damit
/// Installation/Update das Verzeichnis nicht anfassen muessen.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "Ausgabenverwaltung";
    private const string BackupFolderName = "Backups";
    private const string LogFolderName = "Logs";
    private const string SettingsFileName = "settings.json";

    /// <summary>
    /// Dateiname der Datenbank. Oeffentlich, weil die Datensicherung die
    /// Kopie im ZIP genauso benennt - damit ist Wiederherstellen ein
    /// Entpacken und Ersetzen, ohne Umbenennen.
    /// </summary>
    public const string DatabaseFileName = "ausgaben.db";

    public static string GetDatabaseFilePath()
        => GetDatabaseFilePath(DefaultAppDataRoot);

    // Nimmt den App-Daten-Wurzelordner als Parameter entgegen, damit Tests
    // ein Temp-Verzeichnis statt des echten %APPDATA% verwenden koennen.
    public static string GetDatabaseFilePath(string appDataRoot)
    {
        return Path.Combine(EnsureAppFolder(appDataRoot), DatabaseFileName);
    }

    /// <summary>
    /// Festes erstes Sicherungsziel: %APPDATA%\Ausgabenverwaltung\Backups.
    /// Wird bei Bedarf angelegt - eine Einrichtung durch den Anwender ist
    /// nicht noetig und nicht vorgesehen.
    /// </summary>
    public static string GetBackupFolderPath()
        => GetBackupFolderPath(DefaultAppDataRoot);

    public static string GetBackupFolderPath(string appDataRoot)
    {
        var backupFolder = Path.Combine(EnsureAppFolder(appDataRoot), BackupFolderName);
        Directory.CreateDirectory(backupFolder);
        return backupFolder;
    }

    /// <summary>
    /// Protokollordner: %APPDATA%\Ausgabenverwaltung\Logs. Bewusst NEBEN
    /// dem Sicherungsordner und nicht darin: Sicherungen sind Daten, die
    /// man aufhebt, Protokolle sind Notizen, die man weitergibt. Wer den
    /// Sicherungsordner auf einen USB-Stick kopiert, soll nicht
    /// unbeabsichtigt sein Protokoll mitkopieren - und umgekehrt soll ein
    /// weitergegebenes Protokoll keine Sicherung enthalten.
    ///
    /// Wird bei Bedarf angelegt, wie der Sicherungsordner auch.
    /// </summary>
    public static string GetLogFolderPath()
        => GetLogFolderPath(DefaultAppDataRoot);

    public static string GetLogFolderPath(string appDataRoot)
    {
        var logFolder = Path.Combine(EnsureAppFolder(appDataRoot), LogFolderName);
        Directory.CreateDirectory(logFolder);
        return logFolder;
    }

    /// <summary>
    /// Einstellungsdatei. Bewusst eine Datei neben der Datenbank und keine
    /// Tabelle DARIN: sie haelt unter anderem fest, wann zuletzt extern
    /// gesichert wurde, und dieser Wert gehoert nicht in den Stand, den
    /// eine Sicherung einfriert. Ausserdem bleibt sie lesbar, wenn die
    /// Datenbank gerade nicht zu oeffnen ist.
    /// </summary>
    public static string GetSettingsFilePath()
        => GetSettingsFilePath(DefaultAppDataRoot);

    public static string GetSettingsFilePath(string appDataRoot)
    {
        return Path.Combine(EnsureAppFolder(appDataRoot), SettingsFileName);
    }

    private static string DefaultAppDataRoot
        => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string EnsureAppFolder(string appDataRoot)
    {
        var appFolder = Path.Combine(appDataRoot, AppFolderName);
        Directory.CreateDirectory(appFolder);
        return appFolder;
    }
}
