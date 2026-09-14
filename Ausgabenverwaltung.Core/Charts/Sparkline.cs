namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Die fertige Zeichenanweisung fuer eine Sparkline: ein Linienzug und der
/// letzte Punkt gesondert, damit die Ansicht das aktuelle Ende
/// hervorheben kann.
/// </summary>
public sealed record SparklineLayout(
    IReadOnlyList<ChartPoint> Points,
    ChartPoint? Last,
    AxisScale Scale)
{
    /// <summary>
    /// Zu wenig Werte fuer eine Aussage. Die Ansicht laesst die Sparkline
    /// dann ganz weg - eine waagerechte Linie aus einem einzigen Wert
    /// behauptet eine Ruhe, die gar nicht gemessen wurde.
    /// </summary>
    public bool IsEmpty => Points.Count < 2;

    public static SparklineLayout Empty { get; } =
        new([], null, NiceScale.Compute([]));
}

/// <summary>
/// Der kleine Verlauf neben einer Kennzahl: keine Achse, keine
/// Beschriftung, kein Gitternetz - nur die Form der letzten Monate.
///
/// Die Sparkline beantwortet nicht "wie viel", das steht als Zahl
/// daneben, sondern "geht es herauf oder herunter". Deshalb kommt sie
/// ohne alles aus, was ein grosses Diagramm braucht.
///
/// Reine Arithmetik wie <see cref="BarChart"/>, deshalb vollstaendig
/// pruefbar (Regel 7).
/// </summary>
public static class Sparkline
{
    /// <summary>
    /// Verteilt die Werte gleichmaessig ueber die Breite und rechnet sie
    /// auf der Hoehe in Bildpunkte um.
    ///
    /// Die Achse kommt aus <see cref="NiceScale"/> und enthaelt damit
    /// IMMER die Null. Das ist hier wichtiger als bei einem grossen
    /// Diagramm: eine Sparkline hat keine beschriftete Achse, an der sich
    /// ablesen liesse, wo sie beginnt. Ohne Nullbezug wuerde aus einer
    /// Schwankung von zwei Prozent optisch ein Gebirge, und niemand
    /// koennte es bemerken.
    /// </summary>
    /// <param name="values">
    /// Die Werte in zeitlicher Reihenfolge, lueckenlos - ein Monat ohne
    /// Buchung steht mit 0 mit drin. Weniger als zwei Werte ergeben ein
    /// leeres Ergebnis.
    /// </param>
    public static SparklineLayout Compute(
        IReadOnlyList<long> values, double width, double height)
    {
        if (values.Count < 2 || width <= 0 || height <= 0)
        {
            return SparklineLayout.Empty;
        }

        var scale = NiceScale.Compute(values);

        // Der erste Punkt sitzt auf 0, der letzte auf der vollen Breite -
        // die Linie nutzt die Flaeche also ganz aus. Bei n Werten liegen
        // dazwischen n-1 Abstaende.
        var schritt = width / (values.Count - 1);

        var points = new List<ChartPoint>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            points.Add(new ChartPoint(
                schritt * i,
                ChartGeometry.ToY(values[i], scale, height)));
        }

        return new SparklineLayout(points, points[^1], scale);
    }
}
