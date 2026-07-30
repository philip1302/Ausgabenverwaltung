using Ausgabenverwaltung.Core.Categories;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Setzt die Kreuztabelle aus dem Kategoriebaum und dem Ergebnis von
/// <see cref="ReportRepository.EvaluateMatrix"/> zusammen.
///
/// Hier wird bewusst NICHT mehr summiert: die Zellen kommen bereits
/// astweise aufaddiert aus SQL. Einsortiert wird nur noch - das sind
/// hoechstens "Anzahl Kategorien x Anzahl Zeitabschnitte" Werte und
/// bleibt auch bei mehreren tausend Buchungen belanglos.
/// </summary>
public static class ReportMatrixBuilder
{
    private static readonly IReadOnlyDictionary<string, ReportAmount> NoCells =
        new Dictionary<string, ReportAmount>();

    /// <param name="roots">
    /// Die obersten anzuzeigenden Kategorien. Ist in der Filterleiste ein
    /// Kategorie-Ast gewaehlt, ist das dessen Wurzelknoten - sonst der
    /// ganze Baum.
    /// </param>
    /// <param name="fullPaths">
    /// Volle Pfade ALLER Kategorien (siehe
    /// <see cref="CategoryPaths.BuildFullPaths"/>). Getrennt uebergeben,
    /// damit der Pfad auch dann ab der echten Wurzel zaehlt, wenn nur ein
    /// Ast angezeigt wird.
    /// </param>
    public static ReportMatrix Build(
        IReadOnlyList<CategoryNode> roots,
        IReadOnlyDictionary<int, string> fullPaths,
        IReadOnlyList<ReportMatrixCell> cells,
        ReportGrouping grouping)
    {
        if (cells.Count == 0)
        {
            return ReportMatrix.Empty(grouping);
        }

        var byCategory = IndexCells(cells);
        var periodKeys = BuildPeriodKeys(cells, grouping);
        var rows = BuildRows(roots, byCategory, fullPaths, depth: 0);

        // Spalten- und Gesamtsumme entstehen aus den OBERSTEN Zeilen: jede
        // Buchung gehoert zu genau einem Ast, doppelt gezaehlt wird also
        // nichts. Ueber alle Zeilen zu summieren waere falsch - die Werte
        // einer Oberkategorie enthalten ihre Unterkategorien schon.
        var columnTotals = new Dictionary<string, ReportAmount>();
        foreach (var key in periodKeys)
        {
            var spalte = ReportAmount.Empty;
            foreach (var row in rows)
            {
                spalte = spalte.Add(row.Cell(key));
            }

            columnTotals[key] = spalte;
        }

        var total = ReportAmount.Empty;
        foreach (var row in rows)
        {
            total = total.Add(row.Total);
        }

        return new ReportMatrix
        {
            Grouping = grouping,
            PeriodKeys = periodKeys,
            Rows = rows,
            ColumnTotals = columnTotals,
            Total = total,
        };
    }

    private static Dictionary<int, IReadOnlyDictionary<string, ReportAmount>> IndexCells(
        IReadOnlyList<ReportMatrixCell> cells)
    {
        var byCategory = new Dictionary<int, Dictionary<string, ReportAmount>>();

        foreach (var cell in cells)
        {
            if (!byCategory.TryGetValue(cell.CategoryId, out var perPeriod))
            {
                perPeriod = new Dictionary<string, ReportAmount>();
                byCategory[cell.CategoryId] = perPeriod;
            }

            perPeriod[cell.GroupKey] = new ReportAmount(cell.SumCents, cell.Count);
        }

        return byCategory.ToDictionary(
            eintrag => eintrag.Key,
            eintrag => (IReadOnlyDictionary<string, ReportAmount>)eintrag.Value);
    }

    // Die Spalten spannen sich vom fruehesten bis zum spaetesten belegten
    // Zeitabschnitt - Luecken dazwischen bleiben als leere Spalte stehen.
    // Bewusst nicht ueber den vollen Filterzeitraum: bei "alles" waeren
    // das monatlich rund 96.000 Spalten, und auch ein versehentlich weit
    // gefasster Zeitraum soll die Tabelle nicht unbrauchbar machen.
    private static IReadOnlyList<string> BuildPeriodKeys(
        IReadOnlyList<ReportMatrixCell> cells, ReportGrouping grouping)
    {
        var schluessel = cells.Select(cell => cell.GroupKey).ToList();
        var erster = schluessel.Min(StringComparer.Ordinal)!;
        var letzter = schluessel.Max(StringComparer.Ordinal)!;

        return ReportPeriods.Enumerate(erster, letzter, grouping);
    }

    private static IReadOnlyList<ReportMatrixRow> BuildRows(
        IReadOnlyList<CategoryNode> nodes,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, ReportAmount>> byCategory,
        IReadOnlyDictionary<int, string> fullPaths,
        int depth)
    {
        var rows = new List<ReportMatrixRow>();

        foreach (var node in nodes)
        {
            var id = node.Category.Id;
            var cells = byCategory.TryGetValue(id, out var vorhanden) ? vorhanden : NoCells;

            // Die Zeilensumme ergibt sich aus den eigenen Zellen - die
            // enthalten den ganzen Ast bereits.
            var total = ReportAmount.Empty;
            foreach (var betrag in cells.Values)
            {
                total = total.Add(betrag);
            }

            rows.Add(new ReportMatrixRow
            {
                CategoryId = id,
                Name = node.Category.Name,
                FullPath = fullPaths.TryGetValue(id, out var pfad) ? pfad : node.Category.Name,
                Depth = depth,
                IsArchived = node.Category.IsArchived,
                Cells = cells,
                Total = total,
                Children = BuildRows(node.Children, byCategory, fullPaths, depth + 1),
            });
        }

        return rows;
    }
}
