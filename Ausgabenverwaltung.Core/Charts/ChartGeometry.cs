namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Ein Punkt in Bildpunkten. Bewusst ein eigener Record und nicht der
/// Punkttyp der Oberflaeche: Core kennt keine UI-Typen, sonst liesse sich
/// die Geometrie nicht ohne laufendes Fenster pruefen (Regel 7). Die
/// Umwandlung macht das ViewModel beim Uebersetzen.
/// </summary>
public sealed record ChartPoint(double X, double Y);

/// <summary>
/// Die Umrechnungen, die JEDES Diagramm braucht: wo liegt ein Betrag auf
/// der Werteachse, wie lang ist er, wo sitzen Gitternetz und
/// Achsenbeschriftung.
///
/// Stand frueher privat in <see cref="BarChart"/>. Seit es mehr als eine
/// Diagrammart gibt, liegt es hier - dieselbe Rechnung zweimal zu haben
/// hiesse, sie zweimal richtig halten zu muessen.
///
/// Der Ursprung liegt wie bei Bildschirmkoordinaten ueblich OBEN links;
/// groesseres Y heisst weiter unten. Deshalb dreht <see cref="ToY"/> die
/// Werteachse um.
/// </summary>
public static class ChartGeometry
{
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
    public static double LengthOf(long cents, AxisScale scale, double height)
        => Math.Abs(cents) / (double)scale.SpanCents * height;

    /// <summary>
    /// Das Gitternetz zu einer Achseneinteilung. Die Null bekommt eine
    /// eigene Art: sie ist die Bezugslinie, von der aus jeder Balken
    /// gelesen wird, und muss sich vom uebrigen Gitternetz abheben.
    /// </summary>
    public static List<ChartLine> GridLines(AxisScale scale, double height)
    {
        var lines = new List<ChartLine>();

        foreach (var tick in scale.Ticks)
        {
            lines.Add(new ChartLine(
                ToY(tick, scale, height),
                tick,
                tick == 0 ? ChartLineKind.Zero : ChartLineKind.Grid));
        }

        return lines;
    }

    /// <summary>Die Beschriftungen der Wertachse.</summary>
    public static List<AxisTick> TicksOf(AxisScale scale, double height)
        => scale.Ticks
            .Select(value => new AxisTick(ToY(value, scale, height), value))
            .ToList();

    /// <summary>
    /// Haengt eine Durchschnittslinie an, sofern sie etwas aussagt.
    ///
    /// Ganzzahlig gemittelt, weil Geld ganzzahlig ist (Regel 1). Der halbe
    /// Cent, der dabei verloren geht, ist auf einer Achse von mehreren
    /// hundert Euro nicht darstellbar.
    /// </summary>
    public static void AddAverage(
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
}
