using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;

namespace Ausgabenverwaltung.Tests;

public class OpenItemsRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly OpenItemsRepository _repository;
    private readonly ExpenseRepository _expenses;
    private readonly int _categoryId;
    private readonly int _selfId;
    private readonly int _otherId;

    // heute in UTC, weil OpenItemsRepository "Tage offen" ueber SQLites
    // julianday('now') berechnet, das auf UTC laeuft.
    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.UtcNow);

    public OpenItemsRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new OpenItemsRepository(_connection);
        _expenses = new ExpenseRepository(_connection);

        var categories = new CategoryRepository(_connection);
        var people = new PersonRepository(_connection);

        _categoryId = categories.Create("Pferde", null).Id;
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void GetOpen_enthaelt_nur_unbeglichene_Ausgaben_mit_fremdem_Zahler()
    {
        var offen = _expenses.Create(_categoryId, 1000, Heute.AddDays(-1), _otherId);

        // Beglichen, fremder Zahler -> nicht mehr offen.
        _expenses.Create(_categoryId, 1000, Heute.AddDays(-2), _otherId, settledDate: Heute);

        // SettledDate NULL, aber eigener Zahler -> wird nie ausgewertet (Regel 4).
        _expenses.Create(_categoryId, 1000, Heute.AddDays(-3), _selfId);

        var offeneListe = _repository.GetOpen();

        var item = Assert.Single(offeneListe);
        Assert.Equal(offen.Id, item.Id);
    }

    [Fact]
    public void GetOpen_ist_nach_ExpenseDate_sortiert()
    {
        var spaeter = _expenses.Create(_categoryId, 100, Heute.AddDays(-1), _otherId);
        var frueher = _expenses.Create(_categoryId, 100, Heute.AddDays(-20), _otherId);

        var offeneListe = _repository.GetOpen();

        Assert.Equal(new[] { frueher.Id, spaeter.Id }, offeneListe.Select(i => i.Id));
    }

    [Fact]
    public void GetOpen_liefert_Personenname_und_vollen_Kategoriepfad()
    {
        var hufschmied = new CategoryRepository(_connection).Create("Hufschmied", _categoryId);
        _expenses.Create(hufschmied.Id, 4200, Heute, _otherId, note: "Beschlag");

        var item = Assert.Single(_repository.GetOpen());

        Assert.Equal("Mitbewohner", item.PayerName);
        Assert.Equal("Pferde › Hufschmied", item.CategoryFullPath);
        Assert.Equal("Beschlag", item.Note);
        Assert.Equal(4200, item.AmountCents);
        Assert.Null(item.SettledDate);
    }

    [Fact]
    public void GetOpen_berechnet_TageOffen_ab_ExpenseDate()
    {
        _expenses.Create(_categoryId, 100, Heute.AddDays(-5), _otherId);

        var item = Assert.Single(_repository.GetOpen());

        Assert.Equal(5, item.TageOffen);
    }

    [Fact]
    public void GetRecentlySettled_enthaelt_nur_im_Zeitraum_beglichene_Posten_mit_fremdem_Zahler()
    {
        var kuerzlich = _expenses.Create(
            _categoryId, 1000, Heute.AddDays(-10), _otherId, settledDate: Heute.AddDays(-2));

        // Ausserhalb des Zeitraums -> nicht in der Liste.
        _expenses.Create(
            _categoryId, 1000, Heute.AddDays(-60), _otherId, settledDate: Heute.AddDays(-40));

        // Noch offen -> nicht in der Liste.
        _expenses.Create(_categoryId, 1000, Heute.AddDays(-1), _otherId);

        var beglichene = _repository.GetRecentlySettled(since: Heute.AddDays(-30));

        var item = Assert.Single(beglichene);
        Assert.Equal(kuerzlich.Id, item.Id);
    }

    [Fact]
    public void GetRecentlySettled_berechnet_TageOffen_bis_zum_SettledDate()
    {
        _expenses.Create(
            _categoryId, 100, Heute.AddDays(-10), _otherId, settledDate: Heute.AddDays(-3));

        var item = Assert.Single(_repository.GetRecentlySettled(since: Heute.AddDays(-30)));

        Assert.Equal(7, item.TageOffen);
    }

    [Fact]
    public void SetSettledDate_verschiebt_einen_Posten_von_offen_zu_beglichen()
    {
        var expense = _expenses.Create(_categoryId, 100, Heute.AddDays(-1), _otherId);

        _repository.SetSettledDate(expense.Id, Heute);

        Assert.Empty(_repository.GetOpen());
        var beglichen = Assert.Single(_repository.GetRecentlySettled(since: Heute.AddDays(-1)));
        Assert.Equal(expense.Id, beglichen.Id);
        Assert.Equal(Heute, beglichen.SettledDate);
    }

    [Fact]
    public void SetSettledDate_mit_null_macht_das_Begleichen_rueckgaengig()
    {
        var expense = _expenses.Create(
            _categoryId, 100, Heute.AddDays(-1), _otherId, settledDate: Heute);

        _repository.SetSettledDate(expense.Id, null);

        var offen = Assert.Single(_repository.GetOpen());
        Assert.Equal(expense.Id, offen.Id);
        Assert.Null(offen.SettledDate);
    }

    [Fact]
    public void GetOpenSumsByPayer_summiert_nur_offene_Posten_mit_fremdem_Zahler()
    {
        _expenses.Create(_categoryId, 1000, Heute.AddDays(-1), _otherId);
        _expenses.Create(_categoryId, 500, Heute.AddDays(-2), _otherId);

        // Beglichen -> zaehlt nicht mehr mit.
        _expenses.Create(_categoryId, 1000, Heute.AddDays(-3), _otherId, settledDate: Heute);

        // SettledDate NULL, aber eigener Zahler -> wird nie ausgewertet (Regel 4).
        _expenses.Create(_categoryId, 5000, Heute.AddDays(-4), _selfId);

        var summen = _repository.GetOpenSumsByPayer();

        Assert.Equal(1500, summen[_otherId]);
        Assert.False(summen.ContainsKey(_selfId));
    }
}
