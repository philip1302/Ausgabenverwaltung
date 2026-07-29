using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Orchestriert den Programmstart: Datenbank anlegen/pruefen, beim
/// allerersten Start Grunddaten anlegen, faellige wiederkehrende
/// Buchungen erzeugen. Zeigt selbst nichts an und kennt kein UI - die
/// Oberflaeche ruft nur <see cref="Run"/> auf und wertet das Ergebnis aus.
/// Nebenlaeufiger Start mehrerer Instanzen wird bewusst nicht gesondert
/// behandelt (Single-User-Desktop-Anwendung, SQLite serialisiert
/// Dateizugriffe ohnehin auf DB-Ebene).
/// </summary>
public static class StartupService
{
    private const string SelfPersonName = "Ich";
    private const string StarterCategoryName = "Sonstiges";

    public static StartupResult Run(string databaseFilePath)
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
        };
    }
}
