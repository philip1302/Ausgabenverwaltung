namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Ermittelt den Pfad zur Datenbankdatei. Liegt bewusst NICHT neben der
/// EXE, sondern im rollenden Anwendungsdatenordner des Nutzers, damit
/// Installation/Update das Verzeichnis nicht anfassen muessen.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "Ausgabenverwaltung";
    private const string DatabaseFileName = "ausgaben.db";

    public static string GetDatabaseFilePath()
        => GetDatabaseFilePath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    // Nimmt den App-Daten-Wurzelordner als Parameter entgegen, damit Tests
    // ein Temp-Verzeichnis statt des echten %APPDATA% verwenden koennen.
    public static string GetDatabaseFilePath(string appDataRoot)
    {
        var appFolder = Path.Combine(appDataRoot, AppFolderName);
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, DatabaseFileName);
    }
}
