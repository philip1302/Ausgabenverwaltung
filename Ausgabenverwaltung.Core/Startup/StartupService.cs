using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Orchestriert den Programmstart: Datenbank anlegen/pruefen, beim
/// allerersten Start Grunddaten anlegen, sichern, faellige wiederkehrende
/// Buchungen erzeugen. Zeigt selbst nichts an und kennt kein UI - die
/// Oberflaeche ruft nur <see cref="Run(string)"/> auf und wertet das
/// Ergebnis aus. Nebenlaeufiger Start mehrerer Instanzen wird bewusst
/// nicht gesondert behandelt (Single-User-Desktop-Anwendung, SQLite
/// serialisiert Dateizugriffe ohnehin auf DB-Ebene).
/// </summary>
public static class StartupService
{
    private const string SelfPersonName = "Ich";
    private const string StarterCategoryName = "Sonstiges";

    public static StartupResult Run(string databaseFilePath)
        => Run(databaseFilePath, AppPaths.GetBackupFolderPath(), AppPaths.GetSettingsFilePath());

    // Sicherungsordner und Einstellungsdatei kommen als Parameter herein,
    // damit Tests in ein Temp-Verzeichnis statt in das echte %APPDATA%
    // schreiben - dieselbe Ueberlegung wie bei AppPaths.
    public static StartupResult Run(
        string databaseFilePath, string backupFolderPath, string settingsFilePath)
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={databaseFilePath}");

        DatabaseInitializer.Initialize(connection);

        var actualVersion = DatabaseInitializer.GetSchemaVersion(connection);
        if (actualVersion > DatabaseInitializer.ExpectedSchemaVersion)
        {
            throw new SchemaVersionTooNewException(actualVersion, DatabaseInitializer.ExpectedSchemaVersion);
        }

        var personRepository = new PersonRepository(connection);
        var isFirstStart = !personRepository.GetAll().Any(p => p.IsSelf);

        if (isFirstStart)
        {
            personRepository.Create(SelfPersonName, isSelf: true);
            new CategoryRepository(connection).Create(StarterCategoryName, parentId: null);
        }

        // Sichern VOR der Erzeugung wiederkehrender Buchungen: die
        // Sicherung soll den Stand festhalten, mit dem der Anwender die
        // Sitzung begonnen hat. Erzeugt der Lauf gleich darauf etwas
        // Unerwuenschtes, liegt der Zustand davor bereits im Sicherungs-
        // ordner. Beim allerersten Start gibt es nichts zu sichern - eine
        // Sicherung der leeren Datenbank waere nur ein belegter Platz.
        // BackupService wirft nicht; ein Fehlschlag steht im Ergebnis und
        // haelt den Start nicht auf.
        var backup = isFirstStart
            ? null
            : new BackupService(connection, backupFolderPath, new BackupSettingsStore(settingsFilePath))
                .RunIfDue(DateTime.Now);

        // Lokales Kalenderdatum bewusst statt UTC: "heute faellig" bezieht
        // sich auf den Tag des Anwenders, nicht auf UTC-Mitternacht (Regel 3
        // betrifft nur gespeicherte Zeitstempel, nicht diesen Eingabewert).
        var asOf = DateOnly.FromDateTime(DateTime.Now);
        var generatedExpenses = new RecurringExpenseRepository(connection).GenerateDueOccurrences(asOf);

        return new StartupResult
        {
            DatabaseFilePath = databaseFilePath,
            IsFirstStart = isFirstStart,
            GeneratedExpenses = generatedExpenses,
            Backup = backup,
        };
    }
}
