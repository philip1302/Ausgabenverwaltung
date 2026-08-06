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
/// groesseres Y heisst weiter unten. Deshalb dreht <see cref="ToY"/> die
/// Werteachse um.
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

    /// <summary>
    /// Der Bildpunkt zu einem Betrag. Oben ist klein, unten ist gross -
    /// deshalb wird der Anteil von der Hoehe abgezogen.
    /// </summary>
    public static double ToY(long cents, AxisScale scale, double height)
    {
        var share = (cents - scale.MinCents) / (double)scale.SpanCents;
        return height - share * height;
    }

    /// <summary>Die Laenge, die ein Betrag in Bildpunkten einnimmt.</summary>
    private static double LengthOf(long cents, AxisScale scale, double height)
        => Math.Abs(cents) / (double)scale.SpanCents * height;

    private static List<ChartLine> GridLines(AxisScale scale, double height)
    {
        var lines = new List<ChartLine>();

        foreach (var tick in scale.Ticks)
        {
            // Die Null bekommt eine eigene Art: sie ist die Bezugslinie,
            // von der aus jeder Balken gelesen wird, und muss sich vom
            // uebrigen Gitternetz abheben.
            lines.Add(new ChartLine(
                ToY(tick, scale, height),
                tick,
                tick == 0 ? ChartLineKind.Zero : ChartLineKind.Grid));
        }

        return lines;
    }

    private static void AddAverage(
        List<ChartLine> lines,
        IReadOnlyList<long> values,
        AxisScale scale,
        double height,
        ChartLineKind kind)
    {
        if (values.Count == 0)
        {
            return;
        }

        // Ganzzahlig gemittelt, weil Geld ganzzahlig ist (Regel 1). Der
        // halbe Cent, der dabei verloren geht, ist auf einer Achse von
        // mehreren hundert Euro nicht darstellbar.
        var average = (long)(values.Sum() / (double)values.Count);

        // Ein Durchschnitt von null saehe wie die Nulllinie aus und
        // verwirrte mehr, als er hilft.
        if (average == 0)
        {
            return;
        }

        // Die Linie liegt wie jeder Balken oberhalb der Nulllinie
        // (Betrag, nicht Vorzeichen) - angezeigt wird trotzdem der
        // vorzeichenbehaftete Durchschnitt.
        lines.Add(new ChartLine(ToY(Math.Abs(average), scale, height), average, kind));
    }

    private static List<AxisTick> TicksOf(AxisScale scale, double height)
        => scale.Ticks
            .Select(value => new AxisTick(ToY(value, scale, height), value))
            .ToList();

    private static ChartLayout Empty()
        => new([], [], [], NiceScale.Compute([]));
}
