using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Reports;
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
        int? recurringExpenseId = null,
        bool isIncome = false)
    {
        var nowUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @AmountCents, @ExpenseDateText, @Note, @PayerId,
                 @SettledDateText, @RecurringExpenseId, @IsIncome, @NowUtcText, @NowUtcText)
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
            IsIncome = isIncome,
            NowUtcText = IsoDateTime.ToUtcText(nowUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        AppLog.Current.Info(LogEvents.ExpenseCreated((int)id, isIncome));

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
            IsIncome = isIncome,
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
        DateOnly? settledDate,
        bool isIncome = false)
    {
        const string sql = """
            UPDATE Expense
            SET CategoryId = @CategoryId,
                AmountCents = @AmountCents,
                ExpenseDate = @ExpenseDateText,
                Note = @Note,
                PayerId = @PayerId,
                SettledDate = @SettledDateText,
                IsIncome = @IsIncome,
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
            IsIncome = isIncome,
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        });

        AppLog.Current.Info(LogEvents.ExpenseUpdated(id));
    }

    public void Delete(int id)
    {
        const string sql = "DELETE FROM Expense WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });

        AppLog.Current.Info(LogEvents.ExpenseDeleted(id));
    }

    /// <summary>
    /// Loescht mehrere Ausgaben in einer Anweisung - fuer das
    /// Sammel-Loeschen in der Ausgabenliste. Eine leere Liste ist
    /// zulaessig und veraendert nichts.
    /// </summary>
    public void DeleteMany(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        const string sql = "DELETE FROM Expense WHERE Id IN @Ids";
        _connection.Execute(sql, new { Ids = ids });

        AppLog.Current.Info(LogEvents.ExpensesDeleted(ids.Count));
    }

    public Expense? GetById(int id)
    {
        const string sql = """
            SELECT Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                   SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc
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
                   p.Name AS PayerName, e.Note, e.IsIncome, p.IsSelf AS PayerIsSelf,
                   e.SettledDate
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
            IsIncome = row.IsIncome,
            PayerIsSelf = row.PayerIsSelf,
            SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
        }).ToList();
    }

    /// <summary>
    /// Die Ausgabenliste: alle Buchungen, die auf den Filter passen, mit
    /// Zahlername, vollem Kategoriepfad und Vorlagentitel. Gefiltert UND
    /// sortiert wird in SQL, nicht im Speicher - die Liste kann ueber
    /// Jahre tausende Zeilen umfassen.
    /// </summary>
    public IReadOnlyList<ExpenseListItem> Query(
        ReportFilter filter, ExpenseSortColumn sort, bool ascending)
    {
        // Kopf und Filter stehen als Konstante fest, nur die ORDER-BY-
        // Klausel kommt hinzu - und die stammt aus einer Weissliste ueber
        // ExpenseSortColumn, nie aus Anwendereingabe.
        var sql = QuerySql + "\n" + OrderBySql(sort, ascending);

        var rows = _connection.Query<ExpenseListRow>(sql, ReportFilterSql.ToParameters(filter));

        return rows.Select(row => new ExpenseListItem
        {
            Id = row.Id,
            ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
            CategoryId = row.CategoryId,
            CategoryFullPath = row.CategoryFullPath,
            AmountCents = row.AmountCents,
            IsIncome = row.IsIncome,
            PayerId = row.PayerId,
            PayerName = row.PayerName,
            PayerIsSelf = row.PayerIsSelf,
            SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
            Note = row.Note,
            RecurringExpenseId = row.RecurringExpenseId,
            RecurringExpenseTitle = row.RecurringExpenseTitle,
        }).ToList();
    }

    /// <summary>
    /// Anzahl und Summe der Treffer DESSELBEN Filters wie
    /// <see cref="Query"/> - eigene Aggregatabfrage statt einer Summe ueber
    /// die geladenen Zeilen, damit die Fusszeile der Liste unabhaengig von
    /// der Anzeige stimmt.
    /// </summary>
    public ExpenseListSummary Summarize(ReportFilter filter)
    {
        // Das Vorzeichen kommt vom Buchungstyp: eine Ausgabe mindert die
        // Summe (negativ), eine Einnahme erhoeht sie (positiv) - aber
        // erst, sobald sie tatsaechlich eingegangen ist (SettledDate
        // gesetzt). Eine noch offene Einnahme ist noch nicht real
        // geflossenes Geld und geht deshalb weder erhoehend noch
        // mindernd ein (siehe Entities.Expense.IsIncome). ABS() macht die
        // Rechnung robust gegenueber etwaigen Alt-Datensaetzen, die noch
        // aus der Zeit vor dieser Regel einen negativen Betrag tragen
        // (frueher: Erstattung).
        const string sql = """
            SELECT COUNT(*)                                       AS Anzahl,
                   COALESCE(SUM(
                       CASE WHEN e.IsIncome = 1 AND e.SettledDate IS NOT NULL THEN  ABS(e.AmountCents)
                            WHEN e.IsIncome = 1                              THEN  0
                            ELSE                                                  -ABS(e.AmountCents)
                       END), 0)                                    AS SummeCents
            FROM   Expense e
            JOIN   Person  p ON p.Id = e.PayerId
            WHERE
            """ + ReportFilterSql.Where;

        var row = _connection.QueryFirst<SummaryRow>(sql, ReportFilterSql.ToParameters(filter));
        return new ExpenseListSummary(row.Anzahl, row.SummeCents);
    }

    // Der Kategoriepfad wird per rekursiver CTE gebildet - dasselbe Muster
    // wie in OpenItems.OpenItemsRepository (siehe docs/schema_v1.sql,
    // Abfrage 2). Das Trennzeichen entspricht
    // Categories.CategoryPaths.Separator.
    // Der LEFT JOIN auf RecurringExpense holt den Vorlagentitel; er bleibt
    // NULL bei handerfassten Buchungen und bei Buchungen, deren Vorlage
    // inzwischen geloescht wurde (ON DELETE SET NULL).
    private const string QuerySql = """
        WITH RECURSIVE Pfad(Id, ParentId, FullPath) AS (
            SELECT Id, ParentId, Name FROM Category WHERE ParentId IS NULL
            UNION ALL
            SELECT c.Id, c.ParentId, Pfad.FullPath || ' › ' || c.Name
            FROM   Category c
            JOIN   Pfad ON c.ParentId = Pfad.Id
        )
        SELECT
            e.Id, e.ExpenseDate, e.CategoryId,
            pfad.FullPath      AS CategoryFullPath,
            e.AmountCents, e.IsIncome, e.PayerId,
            p.Name             AS PayerName,
            p.IsSelf           AS PayerIsSelf,
            e.SettledDate, e.Note, e.RecurringExpenseId,
            r.Title            AS RecurringExpenseTitle
        FROM      Expense e
        JOIN      Person  p    ON p.Id = e.PayerId
        JOIN      Pfad    pfad ON pfad.Id = e.CategoryId
        LEFT JOIN RecurringExpense r ON r.Id = e.RecurringExpenseId
        WHERE
        """ + ReportFilterSql.Where;

    // Rangfolge der Statusspalte beim Sortieren. Eigene Ausgaben haben
    // keinen Status (Regel 4) und landen deshalb hinter offen und
    // beglichen, statt mit einem der beiden vermischt zu werden.
    private const string StatusRankSql = """
        CASE WHEN p.IsSelf = 1          THEN 2
             WHEN e.SettledDate IS NULL THEN 0
             ELSE                            1
        END
        """;

    /// <summary>
    /// ORDER-BY-Klausel zur gewaehlten Spalte. Textspalten werden
    /// gross-/kleinschreibungsunabhaengig sortiert (COLLATE NOCASE), sonst
    /// stuenden alle Kleinbuchstaben hinter allen Grossbuchstaben. Die Id
    /// als zweites Kriterium haelt die Reihenfolge bei gleichen Werten
    /// stabil - ohne sie waere sie in SQLite unbestimmt.
    /// </summary>
    private static string OrderBySql(ExpenseSortColumn sort, bool ascending)
    {
        var spalte = sort switch
        {
            ExpenseSortColumn.Datum => "e.ExpenseDate",
            ExpenseSortColumn.Kategorie => "pfad.FullPath COLLATE NOCASE",
            ExpenseSortColumn.Betrag => "e.AmountCents",
            ExpenseSortColumn.Zahler => "p.Name COLLATE NOCASE",
            ExpenseSortColumn.Status => StatusRankSql,
            ExpenseSortColumn.Bemerkung => "e.Note COLLATE NOCASE",
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };

        var richtung = ascending ? "ASC" : "DESC";
        return $"ORDER BY {spalte} {richtung}, e.Id DESC";
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
        IsIncome = row.IsIncome,
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
        public bool IsIncome { get; set; }
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
        public bool IsIncome { get; set; }
        public bool PayerIsSelf { get; set; }
        public string? SettledDate { get; set; }
    }

    private sealed class ExpenseListRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryFullPath { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public bool IsIncome { get; set; }
        public int PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public bool PayerIsSelf { get; set; }
        public string? SettledDate { get; set; }
        public string? Note { get; set; }
        public int? RecurringExpenseId { get; set; }
        public string? RecurringExpenseTitle { get; set; }
    }

    private sealed class SummaryRow
    {
        public int Anzahl { get; set; }
        public long SummeCents { get; set; }
    }
}
