using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Weg vom Loesch-Knopf bis zum Ergebnis - ohne Fenster, aber mit
/// demselben ViewModel, das die Ansicht bindet. Geprueft wird hier, was
/// nur in der Oberflaeche entsteht: die Erklaerung mit den konkreten
/// Zahlen und die Sicherung vor dem Zusammenfuehren. Die Fachlogik selbst
/// steht in CategoryRepositoryTests.
/// </summary>
public class KategorienLoeschenTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-loeschen-");

    private readonly IDbConnection _connection;
    private readonly CategoryRepository _repository;
    private readonly BackupService _backupService;
    private readonly int _personId;

    public KategorienLoeschenTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _repository = new CategoryRepository(_connection);
        _backupService = new BackupService(
            _connection,
            SicherungsOrdner,
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json")));

        _personId = new PersonRepository(_connection).Create("Ich", isSelf: true).Id;
    }

    private string SicherungsOrdner => Path.Combine(_tempDir.FullName, "Backups");

    public void Dispose()
    {
        _connection.Dispose();

        // Siehe BackupServiceTests: das native Verbindungspooling haelt
        // Dateihandles sonst ueber Dispose() hinaus offen.
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    private KategorienViewModel NeuesViewModel() => new(_repository, _backupService, new WeakReferenceMessenger());

    private static KategorieKnoten Knoten(KategorienViewModel viewModel, string name) =>
        viewModel.Wurzelknoten.Single(k => k.Name == name);

    private void LegeAusgabeAn(int categoryId, long amountCents, int tag) =>
        new ExpenseRepository(_connection)
            .Create(categoryId, amountCents, new DateOnly(2026, 1, tag), _personId);

    private void LegeVorlageAn(int categoryId) =>
        new RecurringExpenseRepository(_connection).Create(
            categoryId, _personId, 5000, "Stallmiete",
            intervalUnit: "month", intervalCount: 1, anchorDay: 1,
            startDate: new DateOnly(2026, 1, 1), endDate: null);

    [Fact]
    public void Eine_unbenutzte_Kategorie_fuehrt_zur_Sicherheitsabfrage_mit_Namen()
    {
        _repository.Create("Hufschmied", null);
        var viewModel = NeuesViewModel();

        viewModel.LoeschenCommand.Execute(Knoten(viewModel, "Hufschmied"));

        Assert.True(viewModel.LoeschAnfrageAktiv);
        Assert.False(viewModel.LoeschHindernisAktiv);
        Assert.Contains("Hufschmied", viewModel.LoeschAnfrageText);
        Assert.Contains("nicht rückgängig", viewModel.LoeschAnfrageText);

        viewModel.LoeschenBestaetigenCommand.Execute(null);

        Assert.Empty(_repository.GetTree());
        Assert.False(viewModel.LoeschAnfrageAktiv);
    }

    [Fact]
    public void Eine_benutzte_Kategorie_wird_nicht_geloescht_sondern_erklaert()
    {
        var hufschmied = _repository.Create("Hufschmied", null);
        LegeAusgabeAn(hufschmied.Id, 1000, 1);
        LegeAusgabeAn(hufschmied.Id, 2000, 2);
        LegeVorlageAn(hufschmied.Id);

        var viewModel = NeuesViewModel();
        viewModel.LoeschenCommand.Execute(Knoten(viewModel, "Hufschmied"));

        Assert.False(viewModel.LoeschAnfrageAktiv);
        Assert.True(viewModel.LoeschHindernisAktiv);

        // Konkrete Zahlen, keine Posten mit der Zahl Null.
        Assert.Contains("2 Ausgaben", viewModel.LoeschHindernisText);
        Assert.Contains("1 Vorlage", viewModel.LoeschHindernisText);
        Assert.DoesNotContain("Unterkategorie", viewModel.LoeschHindernisText);

        // Ohne Unterkategorien steht der Weg ueber das Zusammenfuehren offen.
        Assert.True(viewModel.ZusammenfuehrenMoeglich);
        Assert.Single(_repository.GetTree());
    }

    [Fact]
    public void Mit_Unterkategorien_wird_Zusammenfuehren_nicht_angeboten()
    {
        var pferde = _repository.Create("Pferde", null);
        _repository.Create("Hufschmied", pferde.Id);

        var viewModel = NeuesViewModel();
        viewModel.LoeschenCommand.Execute(Knoten(viewModel, "Pferde"));

        Assert.True(viewModel.LoeschHindernisAktiv);
        Assert.Contains("1 Unterkategorie", viewModel.LoeschHindernisText);
        Assert.False(viewModel.ZusammenfuehrenMoeglich);

        // Und selbst wenn der Befehl doch ausgeloest wird, beginnt kein
        // Zusammenfuehren.
        viewModel.ZusammenfuehrenStartenCommand.Execute(null);
        Assert.False(viewModel.ZusammenfuehrenAktiv);
    }

    [Fact]
    public void Zusammenfuehren_zeigt_die_Vorschau_sichert_und_haengt_dann_um()
    {
        var hufschmied = _repository.Create("Hufschmied", null);
        var tierarzt = _repository.Create("Tierarzt", null);
        LegeAusgabeAn(hufschmied.Id, 1000, 1);
        LegeAusgabeAn(hufschmied.Id, 2500, 2);
        LegeVorlageAn(hufschmied.Id);

        var viewModel = NeuesViewModel();
        viewModel.LoeschenCommand.Execute(Knoten(viewModel, "Hufschmied"));
        viewModel.ZusammenfuehrenStartenCommand.Execute(null);

        Assert.True(viewModel.ZusammenfuehrenAktiv);
        Assert.False(viewModel.ZusammenfuehrenBereit);

        // Nur Blattknoten sind waehlbar, und die Quelle selbst nie.
        var ziel = viewModel.ZielWurzeln.Single(k => k.Id == tierarzt.Id);
        Assert.True(ziel.IstWaehlbar);
        Assert.False(viewModel.ZielWurzeln.Single(k => k.Id == hufschmied.Id).IstWaehlbar);

        viewModel.ZielWaehlenCommand.Execute(ziel);

        Assert.True(viewModel.ZusammenfuehrenBereit);
        Assert.Contains("2 Ausgaben", viewModel.ZusammenfuehrenVorschauText);
        Assert.Contains("1 Vorlage", viewModel.ZusammenfuehrenVorschauText);
        Assert.Contains("35,00", viewModel.ZusammenfuehrenVorschauText);
        Assert.Contains("Tierarzt", viewModel.ZusammenfuehrenVorschauText);
        Assert.Contains("nicht rückgängig", viewModel.ZusammenfuehrenVorschauText);

        // Bis hierher ist nichts geschehen.
        Assert.Equal(2, _repository.GetTree().Count);

        viewModel.ZusammenfuehrenBestaetigenCommand.Execute(null);

        // Vorher wurde automatisch gesichert ...
        Assert.NotEmpty(Directory.GetFiles(SicherungsOrdner, "*.zip"));

        // ... und danach steht alles beim Ziel.
        var uebrig = Assert.Single(_repository.GetTree());
        Assert.Equal(tierarzt.Id, uebrig.Category.Id);
        Assert.Equal(2, _repository.GetUsage(tierarzt.Id).ExpenseCount);
        Assert.Equal(1, _repository.GetUsage(tierarzt.Id).RecurringExpenseCount);
        Assert.False(viewModel.ZusammenfuehrenAktiv);
    }

    [Fact]
    public void Abbrechen_raeumt_alle_Baender_weg()
    {
        var hufschmied = _repository.Create("Hufschmied", null);
        _repository.Create("Tierarzt", null);
        LegeAusgabeAn(hufschmied.Id, 1000, 1);

        var viewModel = NeuesViewModel();
        viewModel.LoeschenCommand.Execute(Knoten(viewModel, "Hufschmied"));
        viewModel.ZusammenfuehrenStartenCommand.Execute(null);
        viewModel.ZusammenfuehrenAbbrechenCommand.Execute(null);

        Assert.False(viewModel.ZusammenfuehrenAktiv);
        Assert.False(viewModel.LoeschHindernisAktiv);
        Assert.False(viewModel.LoeschAnfrageAktiv);
        Assert.Empty(viewModel.ZielWurzeln);
        Assert.Equal(2, _repository.GetTree().Count);
    }
}
