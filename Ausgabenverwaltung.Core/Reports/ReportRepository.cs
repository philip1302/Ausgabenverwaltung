using System.Data;
using Dapper;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Wertet Ausgaben gemaess einem ReportFilter gruppiert aus. Eine einzige,
/// statische SQL-Abfrage (keine versteckte Query-Generierung) - die
/// Filterbedingungen kommen aus <see cref="ReportFilterSql.Where"/>, damit
/// derselbe Filter hier und in der Ausgabenliste dieselbe Treffermenge
/// bedeutet; die Gruppierung ueber einen CASE-Ausdruck mit Text-Parameter.
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
        // Zusammengesetzt aus zwei Konstanten - der SQL-Text steht damit
        // weiterhin vollstaendig zur Uebersetzungszeit fest.
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
            WHERE
            """ + ReportFilterSql.Where + """

            GROUP  BY GroupKey
            ORDER  BY GroupKey
            """;

        var parameters = new DynamicParameters(ReportFilterSql.ToParameters(filter));
        parameters.Add("GroupUnit", GroupUnitText(filter.Grouping));

        var rows = _connection.Query<ReportRow>(sql, parameters);

        return rows
            .Select(row => new ReportGroupResult
            {
                GroupKey = row.GroupKey,
                SumCents = row.SumCents,
                Count = row.Count,
            })
            .ToList();
    }

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
