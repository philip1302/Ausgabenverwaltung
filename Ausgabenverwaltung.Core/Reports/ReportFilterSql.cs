using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die WHERE-Bedingungen eines <see cref="ReportFilter"/> als EIN
/// sichtbarer SQL-Textblock, dazu die passende Parameterbelegung.
/// Bewusst hier zentral und nicht in jeder Abfrage erneut: die Auswertung
/// (<see cref="ReportRepository"/>) und die Ausgabenliste
/// (Expenses.ExpenseRepository) muessen bei gleichem Filter zwingend
/// dieselbe Treffermenge liefern - sonst passen Liste, Trefferzahl und
/// Summe nicht zueinander.
///
/// Das ist keine versteckte Query-Generierung (siehe CLAUDE.md/Stil):
/// der Text ist konstant und vollstaendig lesbar, es wird nichts
/// zusammengesetzt. Aufrufer muessen die Tabellen nur wie folgt
/// benennen: <c>Expense e</c> und <c>Person p</c> (ueber
/// <c>p.Id = e.PayerId</c> verbunden).
/// </summary>
public static class ReportFilterSql
{
    public const string Where = """
              e.ExpenseDate >= @FromText
        AND   e.ExpenseDate <  @ToText

        -- @PayerScope: 'self' | 'others' | 'all' | 'self_or_open'
        -- Der letzte Wert mischt bewusst Zahler, Status und Buchungstyp:
        -- eigene Buchungen zaehlen immer, fremde Buchungen zaehlen dazu,
        -- solange sie noch offen sind (jeder Typ - eine offene Einnahme
        -- traegt ohnehin schon 0 zur Summe bei, siehe SumCentsSql), UND
        -- zusaetzlich eine fremde Einnahme, sobald sie beglichen ist -
        -- sonst faellt eine schon beglichene Einnahme hier komplett aus
        -- dem Filter, statt (wie eine beglichene fremde Ausgabe) einfach
        -- nicht mehr zu zaehlen.
        AND  (@PayerScope = 'all'
              OR (@PayerScope = 'self'   AND p.IsSelf = 1)
              OR (@PayerScope = 'others' AND p.IsSelf = 0)
              OR (@PayerScope = 'self_or_open' AND (
                      p.IsSelf = 1
                      OR (p.IsSelf = 0 AND e.SettledDate IS NULL)
                      OR (p.IsSelf = 0 AND e.IsIncome = 1 AND e.SettledDate IS NOT NULL)
                  )))

        -- Einzelner Zahler, unabhaengig vom PayerScope waehlbar.
        AND  (@PayerId IS NULL OR e.PayerId = @PayerId)

        -- @Status: 'all' | 'open' | 'settled'
        -- Regel 4: "offen" gibt es nur bei fremdem Zahler. Eigene
        -- Ausgaben haben keinen Status und fallen aus BEIDEN
        -- Einschraenkungen heraus - sie erscheinen nur unter 'all'.
        AND  (@Status = 'all'
              OR (@Status = 'open'    AND e.SettledDate IS NULL AND p.IsSelf = 0)
              OR (@Status = 'settled' AND e.SettledDate IS NOT NULL))

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

        -- Nur die aus einer bestimmten Vorlage erzeugten Buchungen.
        -- NULL => keine Einschraenkung.
        AND  (@RecurringExpenseId IS NULL OR e.RecurringExpenseId = @RecurringExpenseId)
        """;

    /// <summary>
    /// Belegung der in <see cref="Where"/> verwendeten Parameter. Datums-
    /// angaben werden ueber <see cref="IsoDate"/> in das
    /// 'YYYY-MM-DD'-Speicherformat gebracht (Regel 3), die Enums in die
    /// im SQL verglichenen Textwerte.
    /// </summary>
    public static object ToParameters(ReportFilter filter) => new
    {
        FromText = IsoDate.ToDateText(filter.From),
        ToText = IsoDate.ToDateText(filter.To),
        PayerScope = PayerScopeText(filter.PayerScope),
        filter.PayerId,
        Status = StatusText(filter.Status),
        filter.CategoryRootId,
        filter.SearchText,
        filter.RecurringExpenseId,
    };

    private static string PayerScopeText(PayerScope scope) => scope switch
    {
        PayerScope.Self => "self",
        PayerScope.Others => "others",
        PayerScope.All => "all",
        PayerScope.SelfAndOpen => "self_or_open",
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };

    private static string StatusText(SettlementStatus status) => status switch
    {
        SettlementStatus.NurOffene => "open",
        SettlementStatus.NurBeglichene => "settled",
        SettlementStatus.Alle => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
