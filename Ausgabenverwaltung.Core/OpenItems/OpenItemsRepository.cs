using System.Data;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.OpenItems;

/// <summary>
/// Offene-Posten-Liste (Regel 4): nicht beglichene Ausgaben, fuer die eine
/// fremde Person (IsSelf = 0) zustaendig ist, bereits mit Personenname und
/// vollem Kategoriepfad ab der Wurzel fuer die Anzeige verbunden (der Pfad
/// wird ueber eine rekursive CTE gebildet, siehe docs/schema_v1.sql,
/// Abfrage 2, fuer das gleiche Muster bei Kategorie-Aesten). "Tage offen"
/// wird direkt in SQL ueber julianday() berechnet (docs/schema_v1.sql,
/// Abfrage 1) statt in C# mit einem injizierten Referenzdatum, weil die
/// Liste ohnehin bei jedem Laden frisch aus der DB kommt.
/// </summary>
public sealed class OpenItemsRepository
{
    private readonly IDbConnection _connection;

    public OpenItemsRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public IReadOnlyList<OpenItem> GetOpen()
    {
        const string sql = """
            WITH RECURSIVE Pfad(Id, ParentId, FullPath) AS (
                SELECT Id, ParentId, Name FROM Category WHERE ParentId IS NULL
                UNION ALL
                SELECT c.Id, c.ParentId, Pfad.FullPath || ' › ' || c.Name
                FROM   Category c
                JOIN   Pfad ON c.ParentId = Pfad.Id
            )
            SELECT
                e.Id, e.ExpenseDate, e.AmountCents, e.IsIncome, e.Note, e.SettledDate,
                e.PayerId, p.Name AS PayerName,
                pfad.FullPath     AS CategoryFullPath,
                CAST(julianday('now') - julianday(e.ExpenseDate) AS INTEGER) AS TageOffen
            FROM   Expense e
            JOIN   Person  p    ON p.Id = e.PayerId
            JOIN   Pfad    pfad ON pfad.Id = e.CategoryId
            WHERE  e.SettledDate IS NULL
              AND  p.IsSelf = 0
            ORDER  BY e.ExpenseDate
            """;

        var rows = _connection.Query<OpenItemRow>(sql);
        return rows.Select(ToOpenItem).ToList();
    }

    /// <summary>
    /// Beglichene Posten mit fremdem Zahler, deren SettledDate nicht vor
    /// <paramref name="since"/> liegt - fuer den Schalter "Beglichene der
    /// letzten 30 Tage anzeigen". "Tage offen" bezieht sich hier auf die
    /// Zeit bis zur Begleichung, nicht bis heute.
    /// </summary>
    public IReadOnlyList<OpenItem> GetRecentlySettled(DateOnly since)
    {
        const string sql = """
            WITH RECURSIVE Pfad(Id, ParentId, FullPath) AS (
                SELECT Id, ParentId, Name FROM Category WHERE ParentId IS NULL
                UNION ALL
                SELECT c.Id, c.ParentId, Pfad.FullPath || ' › ' || c.Name
                FROM   Category c
                JOIN   Pfad ON c.ParentId = Pfad.Id
            )
            SELECT
                e.Id, e.ExpenseDate, e.AmountCents, e.IsIncome, e.Note, e.SettledDate,
                e.PayerId, p.Name AS PayerName,
                pfad.FullPath     AS CategoryFullPath,
                CAST(julianday(e.SettledDate) - julianday(e.ExpenseDate) AS INTEGER) AS TageOffen
            FROM   Expense e
            JOIN   Person  p    ON p.Id = e.PayerId
            JOIN   Pfad    pfad ON pfad.Id = e.CategoryId
            WHERE  e.SettledDate IS NOT NULL
              AND  e.SettledDate >= @SinceText
              AND  p.IsSelf = 0
            ORDER  BY e.SettledDate DESC
            """;

        var rows = _connection.Query<OpenItemRow>(sql, new { SinceText = IsoDate.ToDateText(since) });
        return rows.Select(ToOpenItem).ToList();
    }

    /// <summary>
    /// Setzt oder loescht (NULL) das SettledDate einer Ausgabe - Letzteres
    /// fuer den Rueckgaengig-Hinweis nach versehentlichem Abhaken.
    /// </summary>
    public void SetSettledDate(int id, DateOnly? settledDate)
    {
        const string sql = """
            UPDATE Expense
            SET SettledDate = @SettledDateText,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new
        {
            Id = id,
            SettledDateText = settledDate is DateOnly settled ? IsoDate.ToDateText(settled) : null,
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        });
    }

    /// <summary>
    /// Summe der offenen Posten je Person (Regel 4: nur relevant, wenn die
    /// Person nicht die IsSelf-Person ist), fuer die Anzeige in der
    /// Personenverwaltung. Personen ohne offenen Posten fehlen im Ergebnis.
    /// </summary>
    public IReadOnlyDictionary<int, long> GetOpenSumsByPayer()
    {
        // Bewusst OHNE Fallunterscheidung nach IsIncome: eine offene
        // Ausgabe ("die Person schuldet mir das noch zurueck") und eine
        // offene Einnahme ("die Person schuldet mir das noch") zeigen in
        // dieselbe Richtung - beides ist Geld, das mir die Person noch
        // schuldet. Anders als bei den Ergebnis-Summen (siehe
        // Expenses.ExpenseRepository.Summarize) gibt es hier also kein
        // Vorzeichen umzukehren.
        const string sql = """
            SELECT e.PayerId, SUM(e.AmountCents) AS SummeCents
            FROM   Expense e
            JOIN   Person  p ON p.Id = e.PayerId
            WHERE  e.SettledDate IS NULL
              AND  p.IsSelf = 0
            GROUP  BY e.PayerId
            """;

        return _connection.Query<PayerOpenSumRow>(sql)
            .ToDictionary(row => row.PayerId, row => row.SummeCents);
    }

    private static OpenItem ToOpenItem(OpenItemRow row) => new()
    {
        Id = row.Id,
        ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
        AmountCents = row.AmountCents,
        IsIncome = row.IsIncome,
        Note = row.Note,
        SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
        PayerId = row.PayerId,
        PayerName = row.PayerName,
        CategoryFullPath = row.CategoryFullPath,
        TageOffen = row.TageOffen,
    };

    // Datumsspalten werden als reiner TEXT gelesen statt ueber
    // automatische Dapper-Konvertierung, damit das Parsen zentral ueber
    // IsoDate laeuft (siehe Regel 3).
    private sealed class OpenItemRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public bool IsIncome { get; set; }
        public string? Note { get; set; }
        public string? SettledDate { get; set; }
        public int PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public string CategoryFullPath { get; set; } = string.Empty;
        public int TageOffen { get; set; }
    }

    private sealed class PayerOpenSumRow
    {
        public int PayerId { get; set; }
        public long SummeCents { get; set; }
    }
}
