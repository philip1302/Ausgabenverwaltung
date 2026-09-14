namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Ein Zeitabschnitt mit den Werten beider Jahre.
/// </summary>
/// <param name="Key">
/// Schluessel des Abschnitts im LAUFENDEN Jahr, z. B. "2026-03" - er
/// dient dem Sprung in die Buchungen.
/// </param>
/// <param name="Label">Achsentext, z. B. "Mär".</param>
public sealed record ComparePeriod(
    string Key,
    string Label,
    long PreviousCents,
    long CurrentCents);

/// <summary>Ein gezeichnetes Rechteck des Vergleichs.</summary>
public sealed record CompareBar(
    string Key,
    string Label,
    double X,
    double Y,
    double Width,
    double Height,
    long ValueCents,
    bool IstVorjahr);

/// <summary>Die fertige Zeichenanweisung fuer den Vergleich.</summary>
public sealed record CompareLayout(
    IReadOnlyList<CompareBar> Bars,
    IReadOnlyList<ChartLine> Lines,
    IReadOnlyList<AxisTick> Ticks,
    AxisScale Scale)
{
    public bool IsEmpty => Bars.Count == 0;
}

/// <summary>
/// Zwei Zeitreihen im selben Bild: das Vorjahr als Bezug, das laufende
/// Jahr als Gegenstand.
///
/// <b>Ueberlagert, nicht nebeneinander.</b> Zwoelf Monatspaare nebeneinander
/// ergaeben vierundzwanzig duenne Streifen, an denen sich weder der
/// Jahresverlauf noch der Vergleich ablesen laesst. Stattdessen steht der
/// Vorjahresbalken breit im Hintergrund und der des laufenden Jahres
/// schmaler davor: der Vergleich ist dann eine einzige Kante, die man
/// entlangsieht, statt zwoelfmal zweier Hoehen, die man einzeln vergleicht.
///
/// <b>Eine gemeinsame Achse.</b> Zwei Werteachsen in einem Bild waeren
/// hier besonders verfuehrerisch und besonders falsch - mit der Wahl der
/// beiden Skalen liesse sich jedes beliebige Ergebnis herbeifuehren.
///
/// Wie <see cref="BarChart"/> reine Arithmetik, Ursprung oben links
/// (Regel 7).
/// </summary>
public static class SeriesBars
{
    /// <summary>
    /// Anteil der Abschnittsbreite, der zwischen zwei Abschnitten frei
    /// bleibt - derselbe Wert wie in <see cref="BarChart"/>, damit die
    /// beiden Diagramme der Anwendung gleich aussehen.
    /// </summary>
    private const double GapShare = 0.28;

    /// <summary>
    /// Wie breit der vordere Balken im Verhaeltnis zum hinteren ist. Zu
    /// schmal wirkt er wie ein Strich, zu breit verdeckt er den Bezug.
    /// </summary>
    private const double FrontShare = 0.52;

    /// <summary>
    /// Rechnet beide Reihen auf eine gemeinsame Flaeche.
    ///
    /// Abschnitte, in denen BEIDE Jahre leer sind, erzeugen keinen
    /// Balken - aber sie behalten ihren Platz auf der Zeitachse, sonst
    /// ruecken die uebrigen Monate zusammen und der Jahresverlauf stimmt
    /// nicht mehr.
    /// </summary>
    public static CompareLayout Compare(
        IReadOnlyList<ComparePeriod> values, double width, double height)
    {
        if (values.Count == 0 || width <= 0 || height <= 0)
        {
            return Empty();
        }

        // Beide Reihen spannen dieselbe Achse auf - das ist der Kern des
        // Vergleichs.
        var forScale = new List<long>(values.Count * 2);
        foreach (var value in values)
        {
            forScale.Add(value.PreviousCents);
            forScale.Add(value.CurrentCents);
        }

        var scale = NiceScale.Compute(forScale);
        var bars = new List<CompareBar>();

        var slotWidth = width / values.Count;
        var hinten = slotWidth * (1 - GapShare);
        var vorne = hinten * FrontShare;
        var zeroY = ChartGeometry.ToY(0, scale, height);

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var mitte = slotWidth * i + slotWidth / 2;

            // Das Vorjahr zuerst in die Liste: die Ansicht zeichnet in
            // dieser Reihenfolge, der hintere Balken muss also zuerst
            // kommen.
            if (value.PreviousCents != 0)
            {
                var hoehe = ChartGeometry.LengthOf(value.PreviousCents, scale, height);
                bars.Add(new CompareBar(
                    value.Key, value.Label,
                    mitte - hinten / 2, zeroY - hoehe, hinten, hoehe,
                    value.PreviousCents, IstVorjahr: true));
            }

            if (value.CurrentCents != 0)
            {
                var hoehe = ChartGeometry.LengthOf(value.CurrentCents, scale, height);
                bars.Add(new CompareBar(
                    value.Key, value.Label,
                    mitte - vorne / 2, zeroY - hoehe, vorne, hoehe,
                    value.CurrentCents, IstVorjahr: false));
            }
        }

        var lines = ChartGeometry.GridLines(scale, height);

        return new CompareLayout(
            bars, lines, ChartGeometry.TicksOf(scale, height), scale);
    }

    private static CompareLayout Empty()
        => new([], [], [], NiceScale.Compute([]));
}
