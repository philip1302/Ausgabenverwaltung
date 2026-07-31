using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Settings;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Orchestriert den Programmstart: Datenbank anlegen/pruefen, faellige
/// Schema-Migration ausfuehren, beim allerersten Start Grunddaten anlegen,
/// sichern, faellige wiederkehrende Buchungen erzeugen. Zeigt selbst
/// nichts an und kennt kein UI - die Oberflaeche ruft nur
/// <see cref="Run(string)"/> auf und wertet das Ergebnis aus.
///
/// Dass die Anwendung nur einmal laeuft, stellt <see cref="SingleInstance"/>
/// sicher, und zwar VOR diesem Dienst: hier wird bereits geschrieben
/// (Grunddaten, Migration, wiederkehrende Buchungen), und zwei
/// Ausfuehrungen, die das gleichzeitig tun, sind nichts, was sich
/// nachtraeglich noch sortieren liesse.
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

        // Integritaetspruefung als ALLERERSTES - noch vor dem Anlegen des
        // Schemas. Auf einer beschaedigten Datei duerfen weder Schreib-
        // noch Umstellungsversuche stattfinden; jeder davon koennte die
        // Seiten ueberschreiben, aus denen sich sonst noch etwas retten
        // liesse. Bei einer neuen oder leeren Datei legt SQLite hier eine
        // gueltige leere Datenbank an und meldet erwartungsgemaess "ok".
        var findings = DatabaseHealth.QuickCheck(connection);
        if (findings is not null)
        {
            AppLog.Current.Warning(LogEvents.DatabaseCorrupt(findings));
            throw new DatabaseCorruptException(databaseFilePath, findings);
        }

        DatabaseInitializer.Initialize(connection);

        // Wirft SchemaVersionUnreadableException, wenn in SchemaVersion
        // kein brauchbarer Stand steht - eine halb angelegte Datei aus
        // einem abgebrochenen ersten Start faellt hier auf und nicht erst
        // beim ersten Zugriff auf eine fehlende Spalte.
        var actualVersion = DatabaseInitializer.GetSchemaVersion(connection);
        AppLog.Current.Info(LogEvents.DatabaseChecked(actualVersion));

        if (actualVersion > DatabaseInitializer.ExpectedSchemaVersion)
        {
            throw new SchemaVersionTooNewException(actualVersion, DatabaseInitializer.ExpectedSchemaVersion);
        }

        var backupService = new BackupService(
            connection, backupFolderPath, new AppSettingsStore(settingsFilePath));

        // Die Migration laeuft vor allem anderen: kein Repository darf
        // vorher lesen oder schreiben, denn bis hierher entspricht der
        // Aufbau der Datenbank noch nicht dem, was der Code erwartet.
        var migration = MigriereFalls(connection, backupService, actualVersion);

        var personRepository = new PersonRepository(connection);
        var isFirstStart = !personRepository.GetAll().Any(p => p.IsSelf);

        if (isFirstStart)
        {
            personRepository.Create(SelfPersonName, isSelf: true);
            new CategoryRepository(connection).Create(StarterCategoryName, parentId: null);
            AppLog.Current.Info(LogEvents.FirstStart());
        }

        // Sichern VOR der Erzeugung wiederkehrender Buchungen: die
        // Sicherung soll den Stand festhalten, mit dem der Anwender die
        // Sitzung begonnen hat. Erzeugt der Lauf gleich darauf etwas
        // Unerwuenschtes, liegt der Zustand davor bereits im Sicherungs-
        // ordner. Beim allerersten Start gibt es nichts zu sichern - eine
        // Sicherung der leeren Datenbank waere nur ein belegter Platz.
        // Lief gerade eine Migration, ist ihre Sicherung die Sicherung
        // dieses Starts; ein zweiter Lauf wuerde nur denselben Stand ein
        // zweites Mal ablegen.
        var backup = migration?.Backup
            ?? (isFirstStart ? null : backupService.RunIfDue(DateTime.Now));

        if (backup is not null)
        {
            AppLog.Current.Info(LogEvents.Backup(backup));
        }

        // Lokales Kalenderdatum bewusst statt UTC: "heute faellig" bezieht
        // sich auf den Tag des Anwenders, nicht auf UTC-Mitternacht (Regel 3
        // betrifft nur gespeicherte Zeitstempel, nicht diesen Eingabewert).
        var asOf = DateOnly.FromDateTime(DateTime.Now);
        var generatedExpenses = new RecurringExpenseRepository(connection).GenerateDueOccurrences(asOf);

        if (generatedExpenses.Count > 0)
        {
            AppLog.Current.Info(LogEvents.RecurringGenerated(generatedExpenses.Count, asOf));
        }

        return new StartupResult
        {
            DatabaseFilePath = databaseFilePath,
            IsFirstStart = isFirstStart,
            GeneratedExpenses = generatedExpenses,
            Backup = backup,
            Migration = migration,
        };
    }

    /// <summary>
    /// Zieht die Datenbank hoch, falls sie hinter dem erwarteten Stand
    /// liegt - und zwar erst, nachdem eine Sicherung geschrieben wurde.
    /// Scheitert diese Sicherung, wird NICHT migriert, sondern abgebrochen
    /// (<see cref="MigrationBackupFailedException"/>).
    /// </summary>
    private static MigrationResult? MigriereFalls(
        IDbConnection connection, BackupService backupService, int actualVersion)
    {
        if (actualVersion >= DatabaseInitializer.ExpectedSchemaVersion)
        {
            return null;
        }

        // RunNow statt RunIfDue: die heutige Sicherung kann laengst
        // geschrieben sein, sie zeigte dann einen Stand von vorhin und
        // nicht den unmittelbar vor der Umstellung.
        var backup = backupService.RunNow(DateTime.Now);
        AppLog.Current.Info(LogEvents.Backup(backup));

        if (backup.Primary == BackupOutcome.Failed)
        {
            throw new MigrationBackupFailedException(
                actualVersion, DatabaseInitializer.ExpectedSchemaVersion, backup.PrimaryError);
        }

        int newVersion;
        try
        {
            newVersion = DatabaseMigrator.MigrateToLatest(connection);
        }
        catch (Exception ex)
        {
            // Der Migrator hat seine Transaktion bereits zurueckgerollt.
            // Hier kommt nur noch dazu, was er nicht weiss: welche
            // Sicherung unmittelbar davor entstanden ist. Ohne diese
            // Angabe koennte die Meldung nicht sagen, wo der Rueckweg
            // liegt.
            throw new MigrationFailedException(
                actualVersion, DatabaseInitializer.ExpectedSchemaVersion, backup, ex);
        }

        // Der Migrator laeuft auch dann durch, wenn zu einem Stand
        // schlicht kein Schritt hinterlegt ist - er ueberspringt ihn dann
        // stillschweigend. Das faellt sonst nirgends auf und wuerde die
        // Anwendung auf einem Aufbau weiterlaufen lassen, den sie nicht
        // kennt.
        if (newVersion != DatabaseInitializer.ExpectedSchemaVersion)
        {
            throw new MigrationFailedException(
                actualVersion,
                DatabaseInitializer.ExpectedSchemaVersion,
                backup,
                new InvalidOperationException(
                    $"Nach der Umstellung steht die Datenbank auf Version {newVersion}, "
                    + $"erwartet wurde Version {DatabaseInitializer.ExpectedSchemaVersion}. "
                    + "Fuer diesen Stand ist kein Umstellungsschritt hinterlegt."));
        }

        return new MigrationResult
        {
            FromVersion = actualVersion,
            ToVersion = newVersion,
            Backup = backup,
        };
    }
}
