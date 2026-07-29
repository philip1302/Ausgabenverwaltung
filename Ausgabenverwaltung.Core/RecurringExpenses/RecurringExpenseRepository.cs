using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Datenzugriff fuer Vorlagen wiederkehrender Buchungen: Anlegen, Aendern,
/// Deaktivieren und Laden der Vorlage selbst, sowie das eigentliche
/// Erzeugen faelliger <see cref="Expense"/>-Zeilen daraus. Die reine
/// Datumsberechnung dafuer steckt in <see cref="RecurrenceGenerator"/> und
/// ist bewusst von diesem Datenzugriff getrennt. Betraege werden beim
/// Erzeugen aus der Vorlage kopiert, nicht referenziert (Regel 6) -
/// nachtraegliche Vorlagenaenderungen wirken sich also nie auf bereits
/// erzeugte Buchungen aus.
/// </summary>
public sealed class RecurringExpenseRepository
{
    private readonly IDbConnection _connection;

    public RecurringExpenseRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public RecurringExpense Create(
        int categoryId,
        int payerId,
        long amountCents,
        string title,
        string intervalUnit,
        int intervalCount,
        int? anchorDay,
        DateOnly startDate,
        DateOnly? endDate,
        string? note = null)
    {
        var nowUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO RecurringExpense
                (CategoryId, PayerId, AmountCents, Note, Title,
                 IntervalUnit, IntervalCount, AnchorDay,
                 StartDate, EndDate, GeneratedThrough,
                 IsActive, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @PayerId, @AmountCents, @Note, @Title,
                 @IntervalUnit, @IntervalCount, @AnchorDay,
                 @StartDateText, @EndDateText, NULL,
                 1, @NowUtcText, @NowUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            CategoryId = categoryId,
            PayerId = payerId,
            AmountCents = amountCents,
            Note = note,
            Title = title,
            IntervalUnit = intervalUnit,
            IntervalCount = intervalCount,
            AnchorDay = anchorDay,
            StartDateText = IsoDate.ToDateText(startDate),
            EndDateText = endDate is DateOnly end ? IsoDate.ToDateText(end) : null,
            NowUtcText = IsoDateTime.ToUtcText(nowUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        return new RecurringExpense
        {
            Id = (int)id,
            CategoryId = categoryId,
            PayerId = payerId,
            AmountCents = amountCents,
            Note = note,
            Title = title,
            IntervalUnit = intervalUnit,
            IntervalCount = intervalCount,
            AnchorDay = anchorDay,
            StartDate = startDate,
            EndDate = endDate,
            GeneratedThrough = null,
            IsActive = true,
            CreatedUtc = nowUtc,
            ModifiedUtc = nowUtc,
        };
    }

    public void Update(
        int id,
        int categoryId,
        int payerId,
        long amountCents,
        string title,
        string intervalUnit,
        int intervalCount,
        int? anchorDay,
        DateOnly startDate,
        DateOnly? endDate,
        string? note)
    {
        const string sql = """
            UPDATE RecurringExpense
            SET CategoryId = @CategoryId,
                PayerId = @PayerId,
                AmountCents = @AmountCents,
                Note = @Note,
                Title = @Title,
                IntervalUnit = @IntervalUnit,
                IntervalCount = @IntervalCount,
                AnchorDay = @AnchorDay,
                StartDate = @StartDateText,
                EndDate = @EndDateText,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new
        {
            Id = id,
            CategoryId = categoryId,
            PayerId = payerId,
            AmountCents = amountCents,
            Note = note,
            Title = title,
            IntervalUnit = intervalUnit,
            IntervalCount = intervalCount,
            AnchorDay = anchorDay,
            StartDateText = IsoDate.ToDateText(startDate),
            EndDateText = endDate is DateOnly end ? IsoDate.ToDateText(end) : null,
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        });
    }

    /// <summary>
    /// Deaktiviert die Vorlage, ohne sie zu loeschen: bereits erzeugte
    /// Buchungen bleiben unveraendert, es werden nur keine weiteren mehr
    /// erzeugt (GetAllActive liefert die Vorlage danach nicht mehr).
    /// </summary>
    public void Deactivate(int id)
    {
        const string sql = """
            UPDATE RecurringExpense
            SET IsActive = 0,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new { Id = id, NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow) });
    }

    public RecurringExpense? GetById(int id)
    {
        const string sql = """
            SELECT Id, CategoryId, PayerId, AmountCents, Note, Title,
                   IntervalUnit, IntervalCount, AnchorDay,
                   StartDate, EndDate, GeneratedThrough,
                   IsActive, CreatedUtc, ModifiedUtc
            FROM RecurringExpense
            WHERE Id = @Id
            """;

        var row = _connection.QueryFirstOrDefault<RecurringExpenseRow>(sql, new { Id = id });
        return row is null ? null : ToRecurringExpense(row);
    }

    public IReadOnlyList<RecurringExpense> GetAllActive()
    {
        const string sql = """
            SELECT Id, CategoryId, PayerId, AmountCents, Note, Title,
                   IntervalUnit, IntervalCount, AnchorDay,
                   StartDate, EndDate, GeneratedThrough,
                   IsActive, CreatedUtc, ModifiedUtc
            FROM RecurringExpense
            WHERE IsActive = 1
            ORDER BY Title
            """;

        var rows = _connection.Query<RecurringExpenseRow>(sql);
        return rows.Select(ToRecurringExpense).ToList();
    }

    /// <summary>
    /// Erzeugt fuer alle aktiven Vorlagen die bis <paramref name="asOf"/>
    /// faelligen, aber noch nicht erzeugten Buchungen (Datumsberechnung
    /// siehe <see cref="RecurrenceGenerator"/>) und schreibt je Vorlage
    /// GeneratedThrough auf <paramref name="asOf"/> fort. Laeuft
    /// vollstaendig in einer Transaktion: schlaegt eine einzelne Buchung
    /// fehl, wird fuer KEINE Vorlage etwas uebernommen. Gibt die neu
    /// erzeugten Buchungen zurueck.
    /// </summary>
    public IReadOnlyList<Expense> GenerateDueOccurrences(DateOnly asOf)
    {
        var templates = GetAllActive();

        using var transaction = _connection.BeginTransaction();
        try
        {
            var created = new List<Expense>();

            foreach (var template in templates)
            {
                var occurrences = RecurrenceGenerator.GetDueOccurrences(
                    template.StartDate,
                    template.EndDate,
                    template.IntervalUnit,
                    template.IntervalCount,
                    template.AnchorDay,
                    template.GeneratedThrough,
                    asOf);

                foreach (var occurrenceDate in occurrences)
                {
                    created.Add(InsertGeneratedExpense(template, occurrenceDate, transaction));
                }

                UpdateGeneratedThrough(template.Id, asOf, transaction);
            }

            transaction.Commit();
            return created;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private Expense InsertGeneratedExpense(RecurringExpense template, DateOnly occurrenceDate, IDbTransaction transaction)
    {
        var nowUtc = DateTime.UtcNow;

        const string sql = """
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @AmountCents, @ExpenseDateText, @Note, @PayerId,
                 NULL, @RecurringExpenseId, @NowUtcText, @NowUtcText)
            """;

        _connection.Execute(sql, new
        {
            template.CategoryId,
            template.AmountCents,
            ExpenseDateText = IsoDate.ToDateText(occurrenceDate),
            template.Note,
            template.PayerId,
            RecurringExpenseId = template.Id,
            NowUtcText = IsoDateTime.ToUtcText(nowUtc),
        }, transaction);

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()", transaction: transaction);

        return new Expense
        {
            Id = (int)id,
            CategoryId = template.CategoryId,
            AmountCents = template.AmountCents,
            ExpenseDate = occurrenceDate,
            Note = template.Note,
            PayerId = template.PayerId,
            SettledDate = null,
            RecurringExpenseId = template.Id,
            CreatedUtc = nowUtc,
            ModifiedUtc = nowUtc,
        };
    }

    private void UpdateGeneratedThrough(int id, DateOnly asOf, IDbTransaction transaction)
    {
        const string sql = """
            UPDATE RecurringExpense
            SET GeneratedThrough = @GeneratedThroughText,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new
        {
            Id = id,
            GeneratedThroughText = IsoDate.ToDateText(asOf),
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        }, transaction);
    }

    private static RecurringExpense ToRecurringExpense(RecurringExpenseRow row) => new()
    {
        Id = row.Id,
        CategoryId = row.CategoryId,
        PayerId = row.PayerId,
        AmountCents = row.AmountCents,
        Note = row.Note,
        Title = row.Title,
        IntervalUnit = row.IntervalUnit,
        IntervalCount = row.IntervalCount,
        AnchorDay = row.AnchorDay,
        StartDate = IsoDate.ParseDate(row.StartDate),
        EndDate = row.EndDate is null ? null : IsoDate.ParseDate(row.EndDate),
        GeneratedThrough = row.GeneratedThrough is null ? null : IsoDate.ParseDate(row.GeneratedThrough),
        IsActive = row.IsActive,
        CreatedUtc = IsoDateTime.ParseUtc(row.CreatedUtc),
        ModifiedUtc = IsoDateTime.ParseUtc(row.ModifiedUtc),
    };

    // Datums- und Zeitstempelspalten werden als reiner TEXT gelesen statt
    // ueber automatische Dapper-Konvertierung, damit das Parsen zentral
    // ueber IsoDate/IsoDateTime laeuft (siehe Regel 3).
    private sealed class RecurringExpenseRow
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int PayerId { get; set; }
        public long AmountCents { get; set; }
        public string? Note { get; set; }
        public string Title { get; set; } = string.Empty;
        public string IntervalUnit { get; set; } = string.Empty;
        public int IntervalCount { get; set; }
        public int? AnchorDay { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string? EndDate { get; set; }
        public string? GeneratedThrough { get; set; }
        public bool IsActive { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string ModifiedUtc { get; set; } = string.Empty;
    }
}
