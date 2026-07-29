using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Anlegen, Aendern, Loeschen und Laden einzelner Ausgaben. Die
/// Offene-Posten-Liste (Regel 4) steckt in
/// Ausgabenverwaltung.Core.OpenItems.OpenItemsRepository, weil sie
/// zusaetzlich Personenname und vollen Kategoriepfad braucht. Betraege
/// sind immer long-Cent (Regel 1), Datumsangaben werden ueber
/// IsoDate/IsoDateTime als 'YYYY-MM-DD' bzw. 'YYYY-MM-DDTHH:MM:SSZ'-TEXT
/// gespeichert (Regel 3).
/// </summary>
public sealed class ExpenseRepository
{
    private readonly IDbConnection _connection;

    public ExpenseRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public Expense Create(
        int categoryId,
        long amountCents,
        DateOnly expenseDate,
        int payerId,
        string? note = null,
        DateOnly? settledDate = null,
        int? recurringExpenseId = null)
    {
        var nowUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @AmountCents, @ExpenseDateText, @Note, @PayerId,
                 @SettledDateText, @RecurringExpenseId, @NowUtcText, @NowUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDateText = IsoDate.ToDateText(expenseDate),
            Note = note,
            PayerId = payerId,
            SettledDateText = settledDate is DateOnly settled ? IsoDate.ToDateText(settled) : null,
            RecurringExpenseId = recurringExpenseId,
            NowUtcText = IsoDateTime.ToUtcText(nowUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        return new Expense
        {
            Id = (int)id,
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDate = expenseDate,
            Note = note,
            PayerId = payerId,
            SettledDate = settledDate,
            RecurringExpenseId = recurringExpenseId,
            CreatedUtc = nowUtc,
            ModifiedUtc = nowUtc,
        };
    }

    public void Update(
        int id,
        int categoryId,
        long amountCents,
        DateOnly expenseDate,
        int payerId,
        string? note,
        DateOnly? settledDate)
    {
        const string sql = """
            UPDATE Expense
            SET CategoryId = @CategoryId,
                AmountCents = @AmountCents,
                ExpenseDate = @ExpenseDateText,
                Note = @Note,
                PayerId = @PayerId,
                SettledDate = @SettledDateText,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new
        {
            Id = id,
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDateText = IsoDate.ToDateText(expenseDate),
            Note = note,
            PayerId = payerId,
            SettledDateText = settledDate is DateOnly settled ? IsoDate.ToDateText(settled) : null,
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        });
    }

    public void Delete(int id)
    {
        const string sql = "DELETE FROM Expense WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
    }

    public Expense? GetById(int id)
    {
        const string sql = """
            SELECT Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                   SettledDate, RecurringExpenseId, CreatedUtc, ModifiedUtc
            FROM Expense
            WHERE Id = @Id
            """;

        var row = _connection.QueryFirstOrDefault<ExpenseRow>(sql, new { Id = id });
        return row is null ? null : ToExpense(row);
    }

    /// <summary>
    /// Die zuletzt ERFASSTEN Ausgaben (nach CreatedUtc, nicht nach
    /// ExpenseDate - ein nachtraeglich datiertes Buchungsdatum soll den
    /// gerade eingegebenen Eintrag nicht nach unten schieben), mit
    /// Kategorie- und Zahlername fuer die Anzeige.
    /// </summary>
    public IReadOnlyList<ExpenseOverview> GetRecent(int count)
    {
        const string sql = """
            SELECT e.Id, e.ExpenseDate, e.AmountCents, c.Name AS CategoryName,
                   p.Name AS PayerName, e.Note
            FROM   Expense e
            JOIN   Category c ON c.Id = e.CategoryId
            JOIN   Person   p ON p.Id = e.PayerId
            ORDER BY e.CreatedUtc DESC, e.Id DESC
            LIMIT @Count
            """;

        var rows = _connection.Query<ExpenseOverviewRow>(sql, new { Count = count });

        return rows.Select(row => new ExpenseOverview
        {
            Id = row.Id,
            ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
            AmountCents = row.AmountCents,
            CategoryName = row.CategoryName,
            PayerName = row.PayerName,
            Note = row.Note,
        }).ToList();
    }

    private static Expense ToExpense(ExpenseRow row) => new()
    {
        Id = row.Id,
        CategoryId = row.CategoryId,
        AmountCents = row.AmountCents,
        ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
        Note = row.Note,
        PayerId = row.PayerId,
        SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
        RecurringExpenseId = row.RecurringExpenseId,
        CreatedUtc = IsoDateTime.ParseUtc(row.CreatedUtc),
        ModifiedUtc = IsoDateTime.ParseUtc(row.ModifiedUtc),
    };

    // Datums- und Zeitstempelspalten werden als reiner TEXT gelesen statt
    // ueber automatische Dapper-Konvertierung, damit das Parsen zentral
    // ueber IsoDate/IsoDateTime laeuft (siehe Regel 3).
    private sealed class ExpenseRow
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public long AmountCents { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public string? Note { get; set; }
        public int PayerId { get; set; }
        public string? SettledDate { get; set; }
        public int? RecurringExpenseId { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string ModifiedUtc { get; set; } = string.Empty;
    }

    private sealed class ExpenseOverviewRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string PayerName { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}
