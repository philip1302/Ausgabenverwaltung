namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Die Einteilung einer Wertachse: Wo liegen die Striche, und welche
/// Zahlen stehen daran?
///
/// Die naheliegende Loesung - Kleinstwert bis Groesstwert durch die
/// Anzahl der Striche teilen - ergibt Beschriftungen wie 1.337 €,
/// 2.674 €, 4.011 €. Rechnerisch richtig, aber niemand liest daraus
/// etwas ab. Menschen rechnen in 1, 2 und 5 und deren Zehnerpotenzen;
/// genau diese Schrittweiten waehlt das hier verwendete, seit langem
/// gebraeuchliche Verfahren ("nice numbers", Heckbert, Graphics Gems).
///
/// Die Grenzen werden dabei nach aussen gerundet ("loose labeling"): die
/// Achse beginnt und endet auf einem runden Wert, auch wenn dadurch
/// etwas Luft ueber dem hoechsten Balken bleibt. Das ist erwuenscht - ein
/// Balken, der genau am oberen Rand endet, sieht abgeschnitten aus.
///
/// Gerechnet wird in Cent (Regel 1), weil die Werte so hereinkommen.
///
/// Reine Arithmetik, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class NiceScale
{
    /// <summary>
    /// Teilt die Achse so ein, dass alle Werte hineinpassen.
    ///
    /// Die Null ist IMMER enthalten, auch wenn alle Werte darueber oder
    /// darunter liegen: bei einem Balkendiagramm entspricht die Laenge
    /// des Balkens dem Wert, und das gilt nur, wenn er bei null beginnt.
    /// Eine bei 95 beginnende Achse macht aus zwei Prozent Unterschied
    /// optisch einen Sprung - der haeufigste Weg, mit einem korrekten
    /// Diagramm etwas Falsches zu behaupten.
    /// </summary>
    /// <param name="values">
    /// Alle darzustellenden Werte. Leer oder ausschliesslich null ergibt
    /// eine Ersatzachse, damit die Anzeige nicht auf einer Spanne von
    /// null rechnen muss.
    /// </param>
    /// <param name="targetTickCount">
    /// Wie viele Striche angestrebt werden. Das Verfahren haelt sich
    /// nicht sklavisch daran - es weicht ab, wenn dadurch die
    /// Schrittweite runder wird. Werte unter 2 werden auf 2 angehoben.
    /// </param>
    public static AxisScale Compute(IReadOnlyList<long> values, int targetTickCount = 5)
    {
        if (targetTickCount < 2)
        {
            targetTickCount = 2;
        }

        var smallest = 0L;
        var largest = 0L;

        foreach (var value in values)
        {
            smallest = Math.Min(smallest, value);
            largest = Math.Max(largest, value);
        }

        // Nichts da oder alles null: eine Ersatzachse von 0 bis 1 €. Ohne
        // sie waere die Spanne 0, und jede Umrechnung Wert -> Bildpunkt
        // eine Division durch null. Beschriftet wird sie trotzdem
        // sauber - das Diagramm zeigt dann eine leere, aber gueltige
        // Flaeche.
        if (smallest == 0 && largest == 0)
        {
            return new AxisScale(0, 100, 100, new long[] { 0, 100 });
        }

        var rawSpan = (double)(largest - smallest);
        var step = (long)Math.Max(1, Math.Round(
            NiceNumber(rawSpan / (targetTickCount - 1), roundUp: true)));

        // Nach aussen auf ein Vielfaches der Schrittweite runden.
        var min = (long)Math.Floor(smallest / (double)step) * step;
        var max = (long)Math.Ceiling(largest / (double)step) * step;

        // Beruehrt der hoechste Balken genau die obere Kante, wirkt er
        // abgeschnitten - dann eine Stufe zulegen. Dasselbe unten.
        if (max == largest && largest > 0)
        {
            max += step;
        }

        if (min == smallest && smallest < 0)
        {
            min -= step;
        }

        // Beides null kann nach dem Runden nicht mehr vorkommen (der
        // Fall ist oben abgefangen), aber eine Spanne von null waere
        // trotzdem toedlich - deshalb die Sicherung.
        if (max == min)
        {
            max = min + step;
        }

        var ticks = new List<long>();
        for (var value = min; value <= max; value += step)
        {
            ticks.Add(value);
        }

        // Rundungsreste: der letzte Strich muss auf der Kante sitzen,
        // sonst endet das Gitternetz vor dem Rand.
        if (ticks.Count == 0 || ticks[^1] != max)
        {
            ticks.Add(max);
        }

        return new AxisScale(min, max, step, ticks);
    }

    /// <summary>
    /// Die naechstgelegene "runde" Zahl zu <paramref name="value"/>:
    /// 1, 2, 5 oder 10 mal die passende Zehnerpotenz.
    ///
    /// <paramref name="roundUp"/> waehlt zwischen der naechsthoeheren
    /// runden Zahl (fuer Schrittweiten - lieber zu wenige Striche als zu
    /// viele) und der jeweils naechstgelegenen.
    /// </summary>
    private static double NiceNumber(double value, bool roundUp)
    {
        if (value <= 0)
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var powerOfTen = Math.Pow(10, exponent);

        // Der Wert, heruntergebrochen auf den Bereich 1 bis 10.
        var fraction = value / powerOfTen;

        double rounded;
        if (roundUp)
        {
            rounded = fraction switch
            {
                <= 1 => 1,
                <= 2 => 2,
                <= 5 => 5,
                _ => 10,
            };
        }
        else
        {
            rounded = fraction switch
            {
                < 1.5 => 1,
                < 3 => 2,
                < 7 => 5,
                _ => 10,
            };
        }

        return rounded * powerOfTen;
    }
}

/// <summary>
/// Die fertige Achseneinteilung.
/// </summary>
/// <param name="MinCents">Untere Achsengrenze, gerundet.</param>
/// <param name="MaxCents">Obere Achsengrenze, gerundet.</param>
/// <param name="StepCents">Abstand zwischen zwei Strichen.</param>
/// <param name="Ticks">
/// Die Werte, an denen ein Strich mit Beschriftung steht - von unten
/// nach oben, Anfang und Ende eingeschlossen.
/// </param>
public sealed record AxisScale(
    long MinCents,
    long MaxCents,
    long StepCents,
    IReadOnlyList<long> Ticks)
{
    /// <summary>
    /// Der Abstand zwischen unterer und oberer Grenze. Nie 0 - sonst
    /// waere jede Umrechnung von Wert auf Bildpunkt eine Division
    /// durch null.
    /// </summary>
    public long SpanCents => MaxCents - MinCents;

    /// <summary>Ob die Null innerhalb der Achse liegt.</summary>
    public bool IncludesZero => MinCents <= 0 && MaxCents >= 0;
}
