using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

public class ExpenseRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly ExpenseRepository _repository;
    private readonly int _categoryId;
    private readonly int _selfId;
    private readonly int _otherId;

    public ExpenseRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new ExpenseRepository(_connection);

        var categories = new CategoryRepository(_connection);
        var people = new PersonRepository(_connection);

        _categoryId = categories.Create("Wohnen", null).Id;
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Create_legt_Ausgabe_mit_eigenem_Zahler_an()
    {
        var expense = _repository.Create(
            _categoryId, 4200, new DateOnly(2026, 3, 5), _selfId, note: "Miete");

        Assert.True(expense.Id > 0);
        Assert.Equal(4200, expense.AmountCents);
        Assert.Equal(new DateOnly(2026, 3, 5), expense.ExpenseDate);
        Assert.Equal("Miete", expense.Note);
        Assert.Equal(_selfId, expense.PayerId);
        Assert.Null(expense.SettledDate);
    }

    [Fact]
    public void Create_mit_negativem_Betrag_erlaubt_Erstattung()
    {
        var expense = _repository.Create(
            _categoryId, -1500, new DateOnly(2026, 3, 5), _selfId);

        Assert.Equal(-1500, expense.AmountCents);
    }

    [Fact]
    public void Create_mit_SettledDate_speichert_das_Datum()
    {
        var expense = _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 5), _otherId,
            settledDate: new DateOnly(2026, 3, 10));

        Assert.Equal(new DateOnly(2026, 3, 10), expense.SettledDate);
    }

    [Fact]
    public void GetById_laedt_die_gespeicherte_Ausgabe_unveraendert()
    {
        var created = _repository.Create(
            _categoryId, 999, new DateOnly(2026, 1, 31), _otherId, note: "Strom");

        var loaded = _repository.GetById(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(created.CategoryId, loaded!.CategoryId);
        Assert.Equal(created.AmountCents, loaded.AmountCents);
        Assert.Equal(created.ExpenseDate, loaded.ExpenseDate);
        Assert.Equal(created.Note, loaded.Note);
        Assert.Equal(created.PayerId, loaded.PayerId);
    }

    [Fact]
    public void GetById_liefert_null_fuer_unbekannte_Id()
    {
        Assert.Null(_repository.GetById(99999));
    }

    [Fact]
    public void Update_aendert_die_Felder_einer_Ausgabe()
    {
        var expense = _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 5), _otherId);

        _repository.Update(
            expense.Id, _categoryId, 2000, new DateOnly(2026, 3, 6), _selfId,
            note: "korrigiert", settledDate: new DateOnly(2026, 3, 7));

        var loaded = _repository.GetById(expense.Id)!;
        Assert.Equal(2000, loaded.AmountCents);
        Assert.Equal(new DateOnly(2026, 3, 6), loaded.ExpenseDate);
        Assert.Equal(_selfId, loaded.PayerId);
        Assert.Equal("korrigiert", loaded.Note);
        Assert.Equal(new DateOnly(2026, 3, 7), loaded.SettledDate);
    }

    [Fact]
    public void Delete_entfernt_die_Ausgabe()
    {
        var expense = _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 5), _selfId);

        _repository.Delete(expense.Id);

        Assert.Null(_repository.GetById(expense.Id));
    }

    [Fact]
    public void GetRecent_ist_nach_Erfassungsreihenfolge_sortiert_nicht_nach_ExpenseDate()
    {
        // Absichtlich rueckdatiert erfasst, damit ein spaeteres
        // ExpenseDate die Erfassungsreihenfolge nicht verfaelscht.
        var zuerstErfasst = _repository.Create(
            _categoryId, 100, new DateOnly(2026, 5, 1), _selfId);
        var zuletztErfasst = _repository.Create(
            _categoryId, 100, new DateOnly(2026, 1, 1), _selfId);

        var recent = _repository.GetRecent(10);

        Assert.Equal(new[] { zuletztErfasst.Id, zuerstErfasst.Id }, recent.Select(e => e.Id));
    }

    [Fact]
    public void GetRecent_begrenzt_auf_die_angegebene_Anzahl()
    {
        for (var i = 1; i <= 15; i++)
        {
            _repository.Create(_categoryId, 100, new DateOnly(2026, 1, i), _selfId);
        }

        var recent = _repository.GetRecent(10);

        Assert.Equal(10, recent.Count);
    }

    [Fact]
    public void GetRecent_liefert_Kategorie_und_Zahlername()
    {
        _repository.Create(
            _categoryId, 4200, new DateOnly(2026, 3, 5), _otherId, note: "Miete");

        var recent = _repository.GetRecent(10);

        var item = Assert.Single(recent);
        Assert.Equal("Wohnen", item.CategoryName);
        Assert.Equal("Mitbewohner", item.PayerName);
        Assert.Equal("Miete", item.Note);
        Assert.Equal(4200, item.AmountCents);
    }

    [Fact]
    public void Create_ohne_IsIncome_ist_eine_gewoehnliche_Ausgabe()
    {
        var expense = _repository.Create(
            _categoryId, 4200, new DateOnly(2026, 3, 5), _selfId);

        Assert.False(expense.IsIncome);
        Assert.False(_repository.GetById(expense.Id)!.IsIncome);
    }

    [Fact]
    public void Create_mit_IsIncome_speichert_die_Einnahme()
    {
        var expense = _repository.Create(
            _categoryId, 300000, new DateOnly(2026, 3, 5), _selfId,
            isIncome: true);

        Assert.True(expense.IsIncome);
        Assert.True(_repository.GetById(expense.Id)!.IsIncome);
    }

    [Fact]
    public void Update_kann_eine_Ausgabe_zur_Einnahme_machen_und_zurueck()
    {
        var expense = _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 5), _selfId);

        _repository.Update(
            expense.Id, _categoryId, 1000, new DateOnly(2026, 3, 5), _selfId,
            note: null, settledDate: null, isIncome: true);
        Assert.True(_repository.GetById(expense.Id)!.IsIncome);

        _repository.Update(
            expense.Id, _categoryId, 1000, new DateOnly(2026, 3, 5), _selfId,
            note: null, settledDate: null, isIncome: false);
        Assert.False(_repository.GetById(expense.Id)!.IsIncome);
    }

    [Fact]
    public void Summarize_verrechnet_eine_beglichene_Einnahme_positiv_gegen_die_Ausgabe()
    {
        // 500,00 € Ausgabe (negativ), 200,00 € beglichene (tatsaechlich
        // eingegangene) Einnahme (positiv) -> Summe -300,00 €, nicht -700,00 €.
        _repository.Create(_categoryId, 50000, new DateOnly(2026, 3, 1), _selfId);
        _repository.Create(
            _categoryId, 20000, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));

        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 12, 31),
        };

        var summary = _repository.Summarize(filter);

        Assert.Equal(2, summary.Count);
        Assert.Equal(-30000, summary.SumCents);
    }

    [Fact]
    public void Summarize_zaehlt_eine_noch_offene_Einnahme_weder_erhoehend_noch_mindernd()
    {
        // Eine noch nicht eingegangene Einnahme ist noch nicht real
        // geflossenes Geld - sie darf die Summe nicht veraendern, bis sie
        // ueber die Offene-Posten-Liste als erhalten markiert wurde.
        _repository.Create(_categoryId, 50000, new DateOnly(2026, 3, 1), _selfId);
        _repository.Create(
            _categoryId, 20000, new DateOnly(2026, 3, 2), _otherId, isIncome: true);

        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 12, 31),
        };

        var summary = _repository.Summarize(filter);

        Assert.Equal(2, summary.Count);
        Assert.Equal(-50000, summary.SumCents);
    }
}
