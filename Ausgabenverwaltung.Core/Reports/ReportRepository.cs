using System.Data;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Wertet Ausgaben gemaess einem ReportFilter gruppiert aus. Eine
/// einzige, statische SQL-Abfrage (keine versteckte Query-Generierung) -
/// die optionalen Filter (Kategorie-Ast, Volltextsuche) sind ueber
/// "@Parameter IS NULL OR ..."-Bedingungen abgebildet, der Zahler-Filter
/// und die Gruppierung ueber CASE-Ausdruecke mit Text-Parametern.
/// </summary>
public sealed class ReportRepository
{
    private readonly IDbConnection _connection;

    public ReportRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public IReadOnlyList<ReportGroupResult> Evaluate(ReportFilter filter)
    {
        const string sql = """
            SELECT
                CASE @GroupUnit
                    WHEN 'year'  THEN strftime('%Y', e.ExpenseDate)
                    WHEN 'month' THEN strftime('%Y-%m', e.ExpenseDate)
                    WHEN 'quarter' THEN
                        strftime('%Y', e.ExpenseDate) || '-Q' ||
                        ((CAST(strftime('%m', e.ExpenseDate) AS INTEGER) - 1) / 3 + 1)
                END                AS GroupKey,
                SUM(e.AmountCents) AS SumCents,
                COUNT(*)           AS Count
            FROM   Expense e
            JOIN   Person  p ON p.Id = e.PayerId
            WHERE  e.ExpenseDate >= @FromText
              AND  e.ExpenseDate <  @ToText

              -- @PayerScope: 'self' | 'others' | 'all'
              AND  (@PayerScope = 'all'
                    OR (@PayerScope = 'self'   AND p.IsSelf = 1)
                    OR (@PayerScope = 'others' AND p.IsSelf = 0))

              -- Kategorie-Ast per rekursiver CTE (siehe docs/schema_v1.sql,
              -- Abfrage 2). @CategoryRootId IS NULL => keine Einschraenkung.
              -- Archivierte Kategorien werden bewusst NICHT ausgeschlossen,
              -- ihre Ausgaben bleiben Teil der Historie.
              AND  (@CategoryRootId IS NULL OR e.CategoryId IN (
                        WITH RECURSIVE Subtree(Id) AS (
                            SELECT Id FROM Category WHERE Id = @CategoryRootId
                            UNION ALL
                            SELECT c.Id FROM Category c
                            JOIN   Subtree s ON c.ParentId = s.Id
                        )
                        SELECT Id FROM Subtree
                    ))

              AND  (@SearchText IS NULL OR e.Note LIKE '%' || @SearchText || '%')

            GROUP  BY GroupKey
            ORDER  BY GroupKey
            """;

        var rows = _connection.Query<ReportRow>(sql, new
        {
            FromText = IsoDate.ToDateText(filter.From),
            ToText = IsoDate.ToDateText(filter.To),
            PayerScope = PayerScopeText(filter.PayerScope),
            GroupUnit = GroupUnitText(filter.Grouping),
            filter.CategoryRootId,
            filter.SearchText,
        });

        return rows
            .Select(row => new ReportGroupResult
            {
                GroupKey = row.GroupKey,
                SumCents = row.SumCents,
                Count = row.Count,
            })
            .ToList();
    }

    private static string PayerScopeText(PayerScope scope) => scope switch
    {
        PayerScope.Self => "self",
        PayerScope.Others => "others",
        PayerScope.All => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };

    private static string GroupUnitText(ReportGrouping grouping) => grouping switch
    {
        ReportGrouping.Year => "year",
        ReportGrouping.Quarter => "quarter",
        ReportGrouping.Month => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
    };

    private sealed class ReportRow
    {
        public string GroupKey { get; set; } = string.Empty;
        public long SumCents { get; set; }
        public int Count { get; set; }
    }
}
