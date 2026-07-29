using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;

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
    public void GetOpenItems_enthaelt_nur_unbeglichene_Ausgaben_mit_fremdem_Zahler()
    {
        // Offen, fremder Zahler -> gehoert in die Liste.
        var offen = _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 1), _otherId);

        // Beglichen, fremder Zahler -> nicht mehr offen.
        _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 2), _otherId,
            settledDate: new DateOnly(2026, 3, 3));

        // SettledDate NULL, aber eigener Zahler -> wird nie ausgewertet (Regel 4).
        _repository.Create(
            _categoryId, 1000, new DateOnly(2026, 3, 4), _selfId);

        var openItems = _repository.GetOpenItems();

        var item = Assert.Single(openItems);
        Assert.Equal(offen.Id, item.Id);
    }

    [Fact]
    public void GetOpenItems_ist_nach_ExpenseDate_sortiert()
    {
        var spaeter = _repository.Create(
            _categoryId, 100, new DateOnly(2026, 3, 20), _otherId);
        var frueher = _repository.Create(
            _categoryId, 100, new DateOnly(2026, 3, 1), _otherId);

        var openItems = _repository.GetOpenItems();

        Assert.Equal(new[] { frueher.Id, spaeter.Id }, openItems.Select(e => e.Id));
    }
}
