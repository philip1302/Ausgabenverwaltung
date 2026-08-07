using System.Globalization;
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
///
/// Die Listenfilter (Kategorien, Ausschluesse, Zahler) kommen als
/// JSON-Array in EINEM Parameter und werden per <c>json_each</c> wieder
/// aufgeblaettert. Bewusst nicht ueber Dappers <c>IN @Liste</c>-Ersetzung:
/// die schriebe den Text zur Laufzeit um, und dann stimmte das, was hier
/// steht, nicht mehr mit dem ueberein, was die Datenbank sieht. So bleibt
/// der Block eine echte Konstante - und <c>json_array_length(...) = 0</c>
/// sagt an Ort und Stelle, dass eine leere Liste nicht einschraenkt.
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

        -- Einzeln gewaehlte Zahler, ODER-verknuepft und unabhaengig vom
        -- PayerScope waehlbar. Leere Liste => keine Einschraenkung.
        AND  (json_array_length(@PayerIdsJson) = 0
              OR e.PayerId IN (SELECT value FROM json_each(@PayerIdsJson)))

        -- @StatusOffen / @StatusBeglichen: 0 oder 1, kombinierbar.
        -- Regel 4: "offen" gibt es nur bei fremdem Zahler. Eigene
        -- Ausgaben haben keinen Status und fallen aus BEIDEN
        -- Einschraenkungen heraus - sie erscheinen nur, wenn gar nichts
        -- angehakt ist.
        AND  ((@StatusOffen = 0 AND @StatusBeglichen = 0)
              OR (@StatusOffen = 1 AND e.SettledDate IS NULL AND p.IsSelf = 0)
              OR (@StatusBeglichen = 1 AND e.SettledDate IS NOT NULL))

        -- Gewaehlte Kategorie-Aeste per rekursiver CTE (siehe
        -- docs/schema_v1.sql, Abfrage 2). Ein Ast umfasst immer alle
        -- Unterkategorien; mehrere Aeste sind ODER-verknuepft, weil die
        -- CTE alle Wurzeln gemeinsam aufspannt. Leere Liste => keine
        -- Einschraenkung. Archivierte Kategorien werden bewusst NICHT
        -- ausgeschlossen, ihre Ausgaben bleiben Teil der Historie.
        AND  (json_array_length(@CategoryRootIdsJson) = 0
              OR e.CategoryId IN (
                  WITH RECURSIVE Subtree(Id) AS (
                      SELECT c.Id FROM Category c
                      WHERE  c.Id IN (SELECT value FROM json_each(@CategoryRootIdsJson))
                      UNION ALL
                      SELECT c.Id FROM Category c
                      JOIN   Subtree s ON c.ParentId = s.Id
                  )
                  SELECT Id FROM Subtree
              ))

        -- Ausgenommene Aeste. Sticht die Auswahl darueber: was hier
        -- drinsteht, faellt heraus, auch wenn es unter einem gewaehlten
        -- Ast haengt ("Haushalt, aber ohne Restaurant"). Wirkt ebenfalls
        -- auf den ganzen Unter-Ast. Leere Liste => nichts ausgenommen.
        AND  (json_array_length(@ExcludedCategoryIdsJson) = 0
              OR e.CategoryId NOT IN (
                  WITH RECURSIVE Ausgenommen(Id) AS (
                      SELECT c.Id FROM Category c
                      WHERE  c.Id IN (SELECT value FROM json_each(@ExcludedCategoryIdsJson))
                      UNION ALL
                      SELECT c.Id FROM Category c
                      JOIN   Ausgenommen a ON c.ParentId = a.Id
                  )
                  SELECT Id FROM Ausgenommen
              ))

        AND  (@SearchText IS NULL OR e.Note LIKE '%' || @SearchText || '%')

        -- Nur Einnahmen (1) bzw. nur Ausgaben (0). Ein leerer Wert
        -- schraenkt nicht ein - dieselbe Regel wie bei allen uebrigen
        -- Filtern hier.
        AND  (@IsIncome IS NULL OR e.IsIncome = @IsIncome)

        -- Nur die aus einer bestimmten Vorlage erzeugten Buchungen.
        -- NULL => keine Einschraenkung.
        AND  (@RecurringExpenseId IS NULL OR e.RecurringExpenseId = @RecurringExpenseId)

        -- Nur genau eine Buchung - der Sprung aus den Uebersichtslisten.
        -- NULL => keine Einschraenkung.
        AND  (@ExpenseId IS NULL OR e.Id = @ExpenseId)
        """;

    /// <summary>
    /// Belegung der in <see cref="Where"/> verwendeten Parameter. Datums-
    /// angaben werden ueber <see cref="IsoDate"/> in das
    /// 'YYYY-MM-DD'-Speicherformat gebracht (Regel 3), die Enums in die
    /// im SQL verglichenen Textwerte, die Id-Listen in JSON-Arrays.
    /// </summary>
    public static object ToParameters(ReportFilter filter) => new
    {
        FromText = IsoDate.ToDateText(filter.From),
        ToText = IsoDate.ToDateText(filter.To),
        PayerScope = PayerScopeText(filter.PayerScope),
        PayerIdsJson = ToJsonArray(filter.PayerIds),
        StatusOffen = filter.Status.HasFlag(SettlementStatus.Offene) ? 1 : 0,
        StatusBeglichen = filter.Status.HasFlag(SettlementStatus.Beglichene) ? 1 : 0,
        CategoryRootIdsJson = ToJsonArray(filter.CategoryRootIds),
        ExcludedCategoryIdsJson = ToJsonArray(filter.ExcludedCategoryIds),
        filter.SearchText,

        // Bewusst als 0/1 statt als bool: die Spalte ist INTEGER, und ein
        // Vergleich zwischen einem bool-Parameter und einer INTEGER-Spalte
        // haengt sonst daran, wie der Treiber den Wert gerade abbildet.
        IsIncome = filter.IsIncome is bool einnahme ? (einnahme ? 1 : 0) : (int?)null,

        filter.RecurringExpenseId,
        filter.ExpenseId,
    };

    /// <summary>
    /// Ganze Zahlen als JSON-Array ("[1,2,3]", leer: "[]"). Von Hand
    /// zusammengesetzt statt ueber einen Serialisierer: es geht
    /// ausschliesslich um <c>int</c>, damit ist die Schreibweise eindeutig
    /// und kulturunabhaengig - <see cref="CultureInfo.InvariantCulture"/>
    /// steht trotzdem dabei, damit das nicht von der Voreinstellung des
    /// Systems abhaengt.
    /// </summary>
    private static string ToJsonArray(IReadOnlyList<int> ids)
        => "[" + string.Join(
            ",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + "]";

    private static string PayerScopeText(PayerScope scope) => scope switch
    {
        PayerScope.Self => "self",
        PayerScope.Others => "others",
        PayerScope.All => "all",
        PayerScope.SelfAndOpen => "self_or_open",
        _ => throw new ArgumentOutOfRangeException(nameof(scope)),
    };
}
