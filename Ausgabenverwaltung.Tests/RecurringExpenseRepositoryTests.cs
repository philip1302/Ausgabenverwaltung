using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Dapper;

namespace Ausgabenverwaltung.Tests;

public class RecurringExpenseRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly RecurringExpenseRepository _repository;
    private readonly ExpenseRepository _expenseRepository;
    private readonly int _categoryId;
    private readonly int _payerId;

    public RecurringExpenseRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new RecurringExpenseRepository(_connection);
        _expenseRepository = new ExpenseRepository(_connection);

        var categories = new CategoryRepository(_connection);
        var people = new PersonRepository(_connection);

        _categoryId = categories.Create("Abos", null).Id;
        _payerId = people.Create("Ich", isSelf: true).Id;
    }

    public void Dispose() => _connection.Dispose();

    // ---------------------------------------------------------------
    // CRUD
    // ---------------------------------------------------------------

    [Fact]
    public void Create_legt_Vorlage_mit_allen_Feldern_an()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 5000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "Fixkosten");

        Assert.True(template.Id > 0);
        Assert.Equal(5000, template.AmountCents);
        Assert.Equal("Miete", template.Title);
        Assert.Equal("month", template.IntervalUnit);
        Assert.Equal(1, template.IntervalCount);
        Assert.Equal(1, template.AnchorDay);
        Assert.Equal(new DateOnly(2026, 1, 1), template.StartDate);
        Assert.Null(template.EndDate);
        Assert.Null(template.GeneratedThrough);
        Assert.True(template.IsActive);
        Assert.Equal("Fixkosten", template.Note);
    }

    [Fact]
    public void GetById_liefert_null_fuer_unbekannte_Id()
    {
        Assert.Null(_repository.GetById(99999));
    }

    [Fact]
    public void GetById_laedt_die_gespeicherte_Vorlage_unveraendert()
    {
        var created = _repository.Create(
            _categoryId, _payerId, 1234, "Zeitung", "week", 1, null,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 12, 31));

        var loaded = _repository.GetById(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(created.Title, loaded!.Title);
        Assert.Equal(created.AmountCents, loaded.AmountCents);
        Assert.Equal(created.IntervalUnit, loaded.IntervalUnit);
        Assert.Equal(created.StartDate, loaded.StartDate);
        Assert.Equal(created.EndDate, loaded.EndDate);
    }

    [Fact]
    public void Update_aendert_die_Felder_der_Vorlage()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Alt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.Update(
            template.Id, _categoryId, _payerId, 2000, "Neu", "year", 2, 15,
            new DateOnly(2026, 2, 1), new DateOnly(2027, 2, 1), note: "geaendert");

        var loaded = _repository.GetById(template.Id)!;
        Assert.Equal(2000, loaded.AmountCents);
        Assert.Equal("Neu", loaded.Title);
        Assert.Equal("year", loaded.IntervalUnit);
        Assert.Equal(2, loaded.IntervalCount);
        Assert.Equal(15, loaded.AnchorDay);
        Assert.Equal(new DateOnly(2026, 2, 1), loaded.StartDate);
        Assert.Equal(new DateOnly(2027, 2, 1), loaded.EndDate);
        Assert.Equal("geaendert", loaded.Note);
    }

    [Fact]
    public void Deactivate_entfernt_die_Vorlage_aus_GetAllActive_ohne_sie_zu_loeschen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Wird deaktiviert", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.Deactivate(template.Id);

        Assert.DoesNotContain(_repository.GetAllActive(), t => t.Id == template.Id);
        var loaded = _repository.GetById(template.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded!.IsActive);
    }

    [Fact]
    public void GetAllActive_liefert_nur_aktive_Vorlagen_alphabetisch_sortiert()
    {
        _repository.Create(_categoryId, _payerId, 1000, "Bernd-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Create(_categoryId, _payerId, 1000, "Anna-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var inaktiv = _repository.Create(_categoryId, _payerId, 1000, "Zzz-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Deactivate(inaktiv.Id);

        var titel = _repository.GetAllActive().Select(t => t.Title).ToList();

        Assert.Equal(new[] { "Anna-Abo", "Bernd-Abo" }, titel);
    }

    // ---------------------------------------------------------------
    // Erzeugung
    // ---------------------------------------------------------------

    [Fact]
    public void GenerateDueOccurrences_erzeugt_faellige_Buchungen_mit_kopierten_Vorlagenwerten()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1200, "Streamingdienst", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "Familientarif");

        var created = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        Assert.Equal(3, created); // Januar, Februar, Maerz
        var buchungen = GetGeneratedExpenses(template.Id);
        Assert.Equal(
            new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1) },
            buchungen.Select(b => b.ExpenseDate));

        var erste = _expenseRepository.GetById(buchungen[0].Id)!;
        Assert.Equal(1200, erste.AmountCents);
        Assert.Equal("Familientarif", erste.Note);
        Assert.Equal(_payerId, erste.PayerId);
        Assert.Equal(template.Id, erste.RecurringExpenseId);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_bei_zweitem_Aufruf_am_selben_Tag_nichts_doppelt()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1500, "Fitnessstudio", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        var ersterLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 15));
        var zweiterLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 15));

        Assert.Equal(3, ersterLauf);
        Assert.Equal(0, zweiterLauf);
        Assert.Equal(3, GetGeneratedExpenses(template.Id).Count);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_nach_Ablauf_des_EndDate_keine_weiteren_Buchungen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 800, "Zeitschriftenabo", "month", 1, 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1));

        var vollstaendig = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(3, vollstaendig);

        // EndDate liegt jetzt in der Vergangenheit - ein spaeterer Lauf
        // (das Abo ist laengst ausgelaufen) darf nichts mehr erzeugen.
        var weitererLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 12, 1));

        Assert.Equal(0, weitererLauf);
        Assert.Equal(3, GetGeneratedExpenses(template.Id).Count);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_eine_geloeschte_Buchung_nicht_erneut()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 3000, "Vereinsbeitrag", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1)); // Jan, Feb, Maerz

        var februarBuchung = GetGeneratedExpenses(template.Id).Single(e => e.ExpenseDate == new DateOnly(2026, 2, 1));
        _expenseRepository.Delete(februarBuchung.Id);

        // Weiterer Lauf mit spaeterem Cutoff - Februar darf nicht
        // zurueckkommen, GeneratedThrough steht bereits dahinter.
        var weitererLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 5, 1));

        Assert.Equal(2, weitererLauf); // nur April, Mai
        var verbleibendeDaten = GetGeneratedExpenses(template.Id).Select(e => e.ExpenseDate).ToList();
        Assert.DoesNotContain(new DateOnly(2026, 2, 1), verbleibendeDaten);
    }

    [Fact]
    public void GenerateDueOccurrences_laeuft_in_einer_Transaktion_und_rollt_bei_Fehler_alles_zurueck()
    {
        var vorlageA = _repository.Create(_categoryId, _payerId, 1000, "A - Erste Vorlage", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);
        var vorlageB = _repository.Create(_categoryId, _payerId, 2000, "B - Zweite Vorlage", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        // Sabotage: fuer Vorlage B existiert das erste faellige Vorkommen
        // bereits als Buchung, ohne dass GeneratedThrough davon weiss.
        // Der Generator versucht es trotzdem anzulegen und verletzt damit
        // UX_Expense_Occurrence.
        InsertRawExpense(vorlageB.Id, new DateOnly(2026, 1, 1));

        Assert.ThrowsAny<Exception>(() => _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1)));

        // Vorlage A wurde alphabetisch zuerst verarbeitet und haette ohne
        // Transaktion bereits eine Buchung erhalten - das muss
        // zurueckgerollt worden sein.
        Assert.Empty(GetGeneratedExpenses(vorlageA.Id));
        Assert.Null(_repository.GetById(vorlageA.Id)!.GeneratedThrough);
    }

    [Fact]
    public void Update_der_Vorlage_veraendert_bereits_erzeugte_Buchungen_nicht_rueckwirkend()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Internet", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "alt");

        _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1));

        _repository.Update(
            template.Id, _categoryId, _payerId, 5000, "Internet", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "neu, teurer Tarif");

        var januarBuchung = GetGeneratedExpenses(template.Id).Single();
        var geladeneBuchung = _expenseRepository.GetById(januarBuchung.Id)!;

        Assert.Equal(1000, geladeneBuchung.AmountCents);
        Assert.Equal("alt", geladeneBuchung.Note);
    }

    private void InsertRawExpense(int recurringExpenseId, DateOnly date)
    {
        var nowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow);

        _connection.Execute("""
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, PayerId, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, 1, @ExpenseDateText, @PayerId, @RecurringExpenseId, @NowUtcText, @NowUtcText)
            """, new
        {
            CategoryId = _categoryId,
            ExpenseDateText = IsoDate.ToDateText(date),
            PayerId = _payerId,
            RecurringExpenseId = recurringExpenseId,
            NowUtcText = nowUtcText,
        });
    }

    private List<GeneratedExpenseRow> GetGeneratedExpenses(int recurringExpenseId)
    {
        var rows = _connection.Query<ExpenseDateRow>("""
            SELECT Id, ExpenseDate
            FROM Expense
            WHERE RecurringExpenseId = @Id
            ORDER BY ExpenseDate
            """, new { Id = recurringExpenseId });

        return rows.Select(r => new GeneratedExpenseRow(r.Id, IsoDate.ParseDate(r.ExpenseDate))).ToList();
    }

    private sealed record GeneratedExpenseRow(int Id, DateOnly ExpenseDate);

    private sealed class ExpenseDateRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
    }
}
