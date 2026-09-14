// Die Umrechnungen Betrag -> Bildpunkt liegen in ChartGeometry, weil
// inzwischen mehrere Diagrammarten dieselben brauchen. Als "using static"
// eingebunden, damit die Aufrufe hier so kurz bleiben wie zu der Zeit, als
// die Methoden noch in dieser Datei standen.
using static Ausgabenverwaltung.Core.Charts.ChartGeometry;

namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Ein Wert je Zeitabschnitt, aufgeschluesselt nach dem, was das
/// Diagramm getrennt darstellt.
///
/// Was in welchen der drei Betraege faellt, entscheidet die Abfrage in
/// <see cref="Reports.ReportRepository.EvaluateTrend"/> - hier sind es
/// nur noch Zahlen.
/// </summary>
/// <param name="Key">Abschnittsschluessel, z. B. "2026-03".</param>
/// <param name="Label">Achsentext, z. B. "Mär 2026".</param>
public sealed record PeriodValue(
    string Key,
    string Label,
    long OwnExpenseCents,
    long ForeignOpenExpenseCents,
    long IncomeCents)
{
    /// <summary>Alles, was der Anwender in diesem Abschnitt traegt.</summary>
    public long BorneExpenseCents => OwnExpenseCents + ForeignOpenExpenseCents;

    /// <summary>
    /// Einnahmen minus getragene Ausgaben. Positiv = es blieb etwas
    /// uebrig.
    /// </summary>
    public long NetCents => IncomeCents - BorneExpenseCents;
}

/// <summary>Ein gezeichnetes Rechteck in Bildpunkten.</summary>
public sealed record ChartBar(
    string Key,
    double X,
    double Y,
    double Width,
    double Height,
    BarKind Kind,
    long ValueCents);

/// <summary>
/// Wofuer ein Rechteck steht. Die Ansicht waehlt daran die Farbe - der
/// Text dazu steht in der Legende und im Kurzhinweis, denn Farbe ist
/// immer nur Zusatz (Regel 10).
/// </summary>
public enum BarKind
{
    OwnExpenses,
    ForeignOpenExpenses,
    Income,
    NetPositive,
    NetNegative,
}

/// <summary>Eine waagerechte Linie: Gitternetz, Nulllinie, Durchschnitt.</summary>
public sealed record ChartLine(double Y, long ValueCents, ChartLineKind Kind);

public enum ChartLineKind
{
    Grid,
    Zero,
    AverageExpenses,
    AverageIncome,
    AverageNet,
}

/// <summary>Ein Beschriftungstext an der Wertachse.</summary>
public sealed record AxisTick(double Y, long ValueCents);

/// <summary>Die fertige Zeichenanweisung fuer eine Zeichenflaeche.</summary>
public sealed record ChartLayout(
    IReadOnlyList<ChartBar> Bars,
    IReadOnlyList<ChartLine> Lines,
    IReadOnlyList<AxisTick> Ticks,
    AxisScale Scale)
{
    public bool IsEmpty => Bars.Count == 0;
}

/// <summary>
/// Rechnet Betraege in Bildpunkte um: wo ein Balken steht, wie hoch er
/// ist, wo Nulllinie, Gitternetz und Durchschnittslinien liegen.
///
/// Breite und Hoehe kommen als schlichte Zahlen herein, nicht als
/// Avalonia-Typen - so wie sonst Pfade und Stichtage hereingereicht
/// werden. Damit laesst sich die gesamte Geometrie ohne laufendes
/// Fenster pruefen (Regel 7), und die Ansicht setzt nur noch
/// Rechtecke an die genannten Stellen.
///
/// Der Ursprung liegt wie bei Bildschirmkoordinaten ueblich OBEN links;
/// groesseres Y heisst weiter unten. Deshalb dreht
/// <see cref="ChartGeometry.ToY"/> die Werteachse um.
/// </summary>
public static class BarChart
{
    /// <summary>
    /// Anteil der Abschnittsbreite, der zwischen zwei Abschnitten frei
    /// bleibt. Ohne Luft verschmelzen benachbarte Monate optisch zu
    /// einer Flaeche.
    /// </summary>
    private const double GapShare = 0.28;

    /// <summary>Anteil, der innerhalb eines Abschnitts zwischen den
    /// beiden Balken der Detailansicht frei bleibt.</summary>
    private const double InnerGapShare = 0.12;

    /// <summary>
    /// Die Detailansicht: je Abschnitt ein gestapelter Ausgabenbalken
    /// und daneben ein Einnahmenbalken.
    /// </summary>
    public static ChartLayout Detailed(
        IReadOnlyList<PeriodValue> values, double width, double height)
    {
        if (values.Count == 0 || width <= 0 || height <= 0)
        {
            return Empty();
        }

        // Beide Saeulen muessen auf dieselbe Achse passen, sonst waeren
        // Ausgaben und Einnahmen nicht vergleichbar - und genau das
        // Vergleichen ist der Zweck dieser Ansicht.
        var forScale = new List<long>(values.Count * 2);
        foreach (var value in values)
        {
            forScale.Add(value.BorneExpenseCents);
            forScale.Add(value.IncomeCents);
        }

        var scale = NiceScale.Compute(forScale);
        var bars = new List<ChartBar>();

        var slotWidth = width / values.Count;
        var usable = slotWidth * (1 - GapShare);
        var innerGap = usable * InnerGapShare;
        var barWidth = (usable - innerGap) / 2;
        var zeroY = ToY(0, scale, height);

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var left = slotWidth * i + (slotWidth - usable) / 2;

            // Saeule 1: eigene Ausgaben unten, fremde offene darauf
            // gestapelt. Die Reihenfolge ist nicht beliebig - unten steht,
            // was sicher der Anwender traegt, darueber das noch Offene.
            var ownHeight = LengthOf(value.OwnExpenseCents, scale, height);
            if (value.OwnExpenseCents > 0)
            {
                bars.Add(new ChartBar(
                    value.Key, left, zeroY - ownHeight, barWidth, ownHeight,
                    BarKind.OwnExpenses, value.OwnExpenseCents));
            }

            if (value.ForeignOpenExpenseCents > 0)
            {
                var openHeight = LengthOf(value.ForeignOpenExpenseCents, scale, height);
                bars.Add(new ChartBar(
                    value.Key, left, zeroY - ownHeight - openHeight, barWidth, openHeight,
                    BarKind.ForeignOpenExpenses, value.ForeignOpenExpenseCents));
            }

            // Saeule 2: Einnahmen.
            if (value.IncomeCents > 0)
            {
                var incomeHeight = LengthOf(value.IncomeCents, scale, height);
                bars.Add(new ChartBar(
                    value.Key, left + barWidth + innerGap, zeroY - incomeHeight,
                    barWidth, incomeHeight, BarKind.Income, value.IncomeCents));
            }
        }

        var lines = GridLines(scale, height);

        // Zwei Durchschnitte, farblich zu den Saeulen passend. Sie
        // beantworten die Frage, die ein Balkendiagramm sonst offen
        // laesst: war dieser Monat viel oder wenig?
        AddAverage(
            lines, values.Select(v => v.BorneExpenseCents).ToList(),
            scale, height, ChartLineKind.AverageExpenses);

        AddAverage(
            lines, values.Select(v => v.IncomeCents).ToList(),
            scale, height, ChartLineKind.AverageIncome);

        return new ChartLayout(bars, lines, TicksOf(scale, height), scale);
    }

    /// <summary>
    /// Die Netto-Ansicht: ein Balken je Abschnitt, immer nach oben von
    /// der Nulllinie aus gezeichnet - wie die Detailansicht. Ob es sich
    /// um Ueberschuss oder Ausgabenueberhang handelt, zeigt allein die
    /// Farbkodierung (<see cref="BarKind.NetPositive"/> /
    /// <see cref="BarKind.NetNegative"/>), nicht die Richtung des
    /// Balkens (Regel 10: Farbe ist Zusatz, hier aber bewusst der
    /// einzige Unterschied in der Geometrie).
    /// </summary>
    public static ChartLayout Net(
        IReadOnlyList<PeriodValue> values, double width, double height)
    {
        if (values.Count == 0 || width <= 0 || height <= 0)
        {
            return Empty();
        }

        // Die Achse spannt sich ueber den Betrag, nicht das Vorzeichen -
        // sonst braeuchte ein negativer Monat Platz unterhalb der
        // Nulllinie, den es beim Zeichnen nach oben nicht mehr gibt.
        var scale = NiceScale.Compute(values.Select(v => Math.Abs(v.NetCents)).ToList());
        var bars = new List<ChartBar>();

        var slotWidth = width / values.Count;
        var barWidth = slotWidth * (1 - GapShare);
        var zeroY = ToY(0, scale, height);

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (value.NetCents == 0)
            {
                continue;
            }

            var left = slotWidth * i + (slotWidth - barWidth) / 2;
            var barHeight = LengthOf(value.NetCents, scale, height);

            bars.Add(new ChartBar(
                value.Key, left, zeroY - barHeight, barWidth, barHeight,
                value.NetCents >= 0 ? BarKind.NetPositive : BarKind.NetNegative,
                value.NetCents));
        }

        var lines = GridLines(scale, height);

        AddAverage(
            lines, values.Select(v => v.NetCents).ToList(),
            scale, height, ChartLineKind.AverageNet);

        return new ChartLayout(bars, lines, TicksOf(scale, height), scale);
    }

    private static ChartLayout Empty()
        => new([], [], [], NiceScale.Compute([]));
}
