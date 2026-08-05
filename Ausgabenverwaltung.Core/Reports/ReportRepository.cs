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
        // Zusammengesetzt aus Konstanten - der SQL-Text steht damit
        // weiterhin vollstaendig zur Uebersetzungszeit fest.
        const string sql = """
            SELECT
            """ + GroupKeySql + """
                                   AS GroupKey,
            """ + SumCentsSql + """
                                   AS SumCents,
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

    /// <summary>
    /// Die Kreuztabelle: Summe und Anzahl je Kategorie UND Zeitabschnitt.
    ///
    /// Die Werte einer Kategorie enthalten dabei bereits alle
    /// Unterkategorien. Dafuer sorgt die Ahnen-CTE: sie ordnet jede
    /// Buchung ihrer eigenen Kategorie UND jeder darueber liegenden zu, so
    /// dass eine Oberkategorie ihren ganzen Ast in einem Rutsch mit
    /// aufsummiert. Aggregiert wird damit vollstaendig in SQL - im
    /// Speicher werden die Zellen nur noch einsortiert (siehe
    /// <see cref="ReportMatrixBuilder"/>).
    ///
    /// Geliefert werden nur BELEGTE Zellen. Welche Zeitabschnitte
    /// dazwischen leer bleiben, ergibt sich aus den Schluesseln
    /// (<see cref="ReportPeriods.Enumerate"/>).
    /// </summary>
    public IReadOnlyList<ReportMatrixCell> EvaluateMatrix(ReportFilter filter)
    {
        const string sql = """
            WITH RECURSIVE Ancestor(CategoryId, AncestorId) AS (
                -- Ankerteil: jede Kategorie ist ihr eigener Vorfahre
                SELECT Id, Id FROM Category
                UNION ALL
                -- Rekursionsteil: eine Stufe hoeher, bis zur Oberkategorie
                SELECT   a.CategoryId, c.ParentId
                FROM     Ancestor a
                JOIN     Category c ON c.Id = a.AncestorId
                WHERE    c.ParentId IS NOT NULL
            )
            SELECT
                a.AncestorId       AS CategoryId,
            """ + GroupKeySql + """
                                   AS GroupKey,
            """ + SumCentsSql + """
                                   AS SumCents,
                COUNT(*)           AS Count
            FROM   Expense  e
            JOIN   Person   p ON p.Id = e.PayerId
            JOIN   Ancestor a ON a.CategoryId = e.CategoryId
            WHERE
            """ + ReportFilterSql.Where + """

            GROUP  BY a.AncestorId, GroupKey
            """;

        var parameters = new DynamicParameters(ReportFilterSql.ToParameters(filter));
        parameters.Add("GroupUnit", GroupUnitText(filter.Grouping));

        var rows = _connection.Query<ReportMatrixRowData>(sql, parameters);

        return rows
            .Select(row => new ReportMatrixCell
            {
                CategoryId = row.CategoryId,
                GroupKey = row.GroupKey,
                SumCents = row.SumCents,
                Count = row.Count,
            })
            .ToList();
    }

    // Der Schluessel des Zeitabschnitts. Eine gemeinsame Konstante, weil
    // Evaluate und EvaluateMatrix zwingend dieselben Schluessel liefern
    // muessen - und weil Reports.ReportPeriods genau diese drei Formate in
    // C# nachbildet, um die Spalten der Kreuztabelle zu erzeugen.
    private const string GroupKeySql = """
                CASE @GroupUnit
                    WHEN 'year'  THEN strftime('%Y', e.ExpenseDate)
                    WHEN 'month' THEN strftime('%Y-%m', e.ExpenseDate)
                    WHEN 'quarter' THEN
                        strftime('%Y', e.ExpenseDate) || '-Q' ||
                        ((CAST(strftime('%m', e.ExpenseDate) AS INTEGER) - 1) / 3 + 1)
                END
        """;

    // Eine Einnahme mindert die Summe statt sie zu erhoehen - deshalb hier
    // per CASE das Vorzeichen kippen statt einfach zu addieren (siehe
    // Entities.Expense.IsIncome). Eigene Konstante wie GroupKeySql, weil
    // Evaluate und EvaluateMatrix dieselbe Rechenregel brauchen.
    private const string SumCentsSql = """
                SUM(CASE WHEN e.IsIncome = 1
                         THEN -e.AmountCents
                         ELSE  e.AmountCents
                    END)
        """;

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

    private sealed class ReportMatrixRowData
    {
        public int CategoryId { get; set; }
        public string GroupKey { get; set; } = string.Empty;
        public long SumCents { get; set; }
        public int Count { get; set; }
    }
}
