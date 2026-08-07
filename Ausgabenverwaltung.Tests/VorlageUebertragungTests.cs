using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Dapper;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die ausdrueckliche Uebertragung einer Vorlagenaenderung auf die bereits
/// erzeugten Buchungen (RecurringExpenseRepository.ApplyToGeneratedExpenses).
///
/// Das ist die eine Ausnahme von Regel 6, und diese Tests halten fest, wie
/// eng sie ist: uebertragen wird, WAS gebucht wurde (Betrag, Kategorie,
/// Zahler, Bemerkung, Art) - nie, WANN es gebucht wurde und nie, ob es
/// beglichen ist. Und nur, wenn jemand die Uebertragung aufruft: das blosse
/// Speichern einer Vorlage laesst die Historie unangetastet.
/// </summary>
public class VorlageUebertragungTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly RecurringExpenseRepository _repository;
    private readonly ExpenseRepository _expenseRepository;
    private readonly int _categoryId;
    private readonly int _andereKategorieId;
    private readonly int _payerId;
    private readonly int _andererPayerId;

    public VorlageUebertragungTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new RecurringExpenseRepository(_connection);
        _expenseRepository = new ExpenseRepository(_connection);

        var categories = new CategoryRepository(_connection);
        var people = new PersonRepository(_connection);

        _categoryId = categories.Create("Abos", null).Id;
        _andereKategorieId = categories.Create("Wohnen", null).Id;
        _payerId = people.Create("Ich", isSelf: true).Id;
        _andererPayerId = people.Create("Anna", isSelf: false).Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Uebertragung_aendert_Betrag_Kategorie_Zahler_Bemerkung_und_Art()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "alt");

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(3, erzeugt.Count);

        _repository.Update(
            template.Id, _andereKategorieId, _andererPayerId, 2500, "Miete",
            "month", 1, 1, new DateOnly(2026, 1, 1), null, note: "neu", isIncome: true);

        var anzahl = _repository.ApplyToGeneratedExpenses(template.Id);

        Assert.Equal(3, anzahl);

        foreach (var buchung in erzeugt)
        {
            var geladen = _expenseRepository.GetById(buchung.Id);

            Assert.NotNull(geladen);
            Assert.Equal(2500, geladen!.AmountCents);
            Assert.Equal(_andereKategorieId, geladen.CategoryId);
            Assert.Equal(_andererPayerId, geladen.PayerId);
            Assert.Equal("neu", geladen.Note);
            Assert.True(geladen.IsIncome);
        }
    }

    [Fact]
    public void Uebertragung_laesst_Datum_und_Beglichen_Status_unberuehrt()
    {
        var template = _repository.Create(
            _categoryId, _andererPayerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        // Eine der Buchungen ist bereits beglichen. Der Zahler ist ein
        // fremder, nur dann hat SettledDate ueberhaupt eine Bedeutung
        // (Regel 4).
        var beglichen = erzeugt[0];
        var beglichenAm = new DateOnly(2026, 1, 20);
        _expenseRepository.Update(
            beglichen.Id, _categoryId, beglichen.AmountCents, beglichen.ExpenseDate,
            _andererPayerId, note: null, settledDate: beglichenAm);

        _repository.Update(
            template.Id, _andereKategorieId, _payerId, 9999, "Miete",
            "month", 1, 1, new DateOnly(2026, 1, 1), null, note: "neu");

        _repository.ApplyToGeneratedExpenses(template.Id);

        foreach (var buchung in erzeugt)
        {
            var geladen = _expenseRepository.GetById(buchung.Id)!;

            Assert.Equal(buchung.ExpenseDate, geladen.ExpenseDate);
            Assert.Equal(9999, geladen.AmountCents);
        }

        // Das Beglichen-Datum gehoert der einzelnen Buchung und nicht der
        // Vorlage - es darf die Uebertragung unveraendert ueberstehen.
        Assert.Equal(beglichenAm, _expenseRepository.GetById(beglichen.Id)!.SettledDate);
        Assert.Null(_expenseRepository.GetById(erzeugt[1].Id)!.SettledDate);
    }

    [Fact]
    public void Uebertragung_laesst_handerfasste_und_fremde_Buchungen_unberuehrt()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var andereVorlage = _repository.Create(
            _categoryId, _payerId, 500, "Zeitung", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        var handerfasst = _expenseRepository.Create(
            _categoryId, 700, new DateOnly(2026, 2, 2), _payerId, note: "von Hand");
        var fremde = LiesBuchungsIds(andereVorlage.Id);

        _repository.Update(
            template.Id, _andereKategorieId, _andererPayerId, 2500, "Miete",
            "month", 1, 1, new DateOnly(2026, 1, 1), null, note: "neu");

        var anzahl = _repository.ApplyToGeneratedExpenses(template.Id);

        // Nur die drei eigenen Buchungen - nicht die der anderen Vorlage
        // und nicht die handerfasste.
        Assert.Equal(3, anzahl);

        var geladenHanderfasst = _expenseRepository.GetById(handerfasst.Id)!;
        Assert.Equal(700, geladenHanderfasst.AmountCents);
        Assert.Equal(_categoryId, geladenHanderfasst.CategoryId);
        Assert.Equal("von Hand", geladenHanderfasst.Note);

        Assert.Equal(3, fremde.Count);
        foreach (var id in fremde)
        {
            Assert.Equal(500, _expenseRepository.GetById(id)!.AmountCents);
        }
    }

    [Fact]
    public void Uebertragung_zieht_ModifiedUtc_mit_im_Format_aus_Regel_3()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        var vorher = LiesModifiedUtc(erzeugt[0].Id);

        // Die Zeitstempel haben Sekundenaufloesung; ohne diese Pause
        // koennten vorher und nachher denselben Wert tragen.
        Thread.Sleep(1100);

        _repository.Update(
            template.Id, _categoryId, _payerId, 2500, "Miete",
            "month", 1, 1, new DateOnly(2026, 1, 1), null, note: null);

        _repository.ApplyToGeneratedExpenses(template.Id);

        var nachher = LiesModifiedUtc(erzeugt[0].Id);

        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", nachher);
        Assert.True(
            IsoDateTime.ParseUtc(nachher) > IsoDateTime.ParseUtc(vorher),
            $"ModifiedUtc muss neuer sein: vorher {vorher}, nachher {nachher}.");
    }

    [Fact]
    public void Ohne_Uebertragung_bleibt_die_Historie_unveraendert()
    {
        // Regel 6 im Normalfall: das blosse Speichern einer Vorlage
        // veraendert bereits erzeugte Buchungen nie.
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "alt");

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        _repository.Update(
            template.Id, _andereKategorieId, _andererPayerId, 2500, "Miete",
            "month", 1, 1, new DateOnly(2026, 1, 1), null, note: "neu", isIncome: true);

        foreach (var buchung in erzeugt)
        {
            var geladen = _expenseRepository.GetById(buchung.Id)!;

            Assert.Equal(1000, geladen.AmountCents);
            Assert.Equal(_categoryId, geladen.CategoryId);
            Assert.Equal(_payerId, geladen.PayerId);
            Assert.Equal("alt", geladen.Note);
            Assert.False(geladen.IsIncome);
        }
    }

    [Fact]
    public void Uebertragung_ohne_erzeugte_Buchungen_aendert_nichts_und_meldet_null()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Kuenftig", "month", 1, 1, new DateOnly(2027, 1, 1), null);

        Assert.Equal(0, _repository.ApplyToGeneratedExpenses(template.Id));
    }

    private List<int> LiesBuchungsIds(int recurringExpenseId) =>
        _connection.Query<int>(
            "SELECT Id FROM Expense WHERE RecurringExpenseId = @Id ORDER BY Id",
            new { Id = recurringExpenseId }).ToList();

    private string LiesModifiedUtc(int expenseId) =>
        _connection.ExecuteScalar<string>(
            "SELECT ModifiedUtc FROM Expense WHERE Id = @Id", new { Id = expenseId })!;
}
