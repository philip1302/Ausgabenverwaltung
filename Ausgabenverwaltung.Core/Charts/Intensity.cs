namespace Ausgabenverwaltung.Core.Charts;

/// <summary>
/// Teilt Betraege in wenige Stufen ein, damit eine Tabelle ihre
/// Groessenverhaeltnisse zeigen kann, ohne dass eine Zahl weichen muss.
///
/// <b>Eingeteilt wird nach Rang, nicht linear nach Betrag.</b> Das ist die
/// ganze Kunst daran: in einer Haushaltsauswertung steht neben dreissig
/// Betraegen zwischen 5 und 80 Euro eine Jahresmiete von 12.000. Linear
/// geteilt bekaeme die Miete die hoechste Stufe und ALLES andere die
/// niedrigste - die Einfaerbung saehe dann aus wie ein Fehler und sagte
/// nichts mehr. Ueber den Rang verteilt sich die Skala dagegen auf die
/// Werte, die tatsaechlich vorkommen.
///
/// Der Preis dafuer ist, dass die Stufen keinen festen Betrag bedeuten:
/// Stufe 4 heisst "gehoert in dieser Auswertung zum oberen Viertel", nicht
/// "ueber 500 Euro". Genau das ist beim Ueberfliegen einer Tabelle aber
/// die nuetzlichere Aussage - und der genaue Betrag steht ohnehin in der
/// Zelle.
///
/// Reine Arithmetik, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class Intensity
{
    /// <summary>
    /// Ordnet jedem uebergebenen Wert eine Stufe von 1 bis
    /// <paramref name="stepCount"/> zu.
    ///
    /// Die Stufe 0 kommt im Ergebnis NICHT vor: sie ist dem Aufrufer
    /// vorbehalten und bedeutet "hier steht gar kein Wert" (eine leere
    /// Zelle, eine Summenzeile). Was eingefaerbt werden soll, entscheidet
    /// also der Aufrufer dadurch, was er hereingibt.
    ///
    /// Verglichen wird ueber den BETRAG (<see cref="Math.Abs(long)"/>).
    /// Ob eine Zelle im Plus oder im Minus steht, sagt bereits ihr
    /// Vorzeichen; die Stufe sagt, wie schwer sie wiegt.
    ///
    /// Gleiche Betraege bekommen immer dieselbe Stufe - sonst stuenden
    /// zwei identische Zahlen nebeneinander in verschiedenen Toenen, und
    /// der Anwender suchte nach einem Unterschied, den es nicht gibt.
    /// </summary>
    /// <param name="values">
    /// Die zu bewertenden Werte. Leer ergibt ein leeres Ergebnis.
    /// </param>
    /// <param name="stepCount">
    /// Wie viele Stufen es geben soll. Werte unter 1 werden auf 1
    /// angehoben.
    /// </param>
    public static IReadOnlyDictionary<TKey, int> Steps<TKey>(
        IReadOnlyDictionary<TKey, long> values, int stepCount = 4)
        where TKey : notnull
    {
        if (stepCount < 1)
        {
            stepCount = 1;
        }

        var ergebnis = new Dictionary<TKey, int>(values.Count);
        if (values.Count == 0)
        {
            return ergebnis;
        }

        var betraege = values.Values.Select(Math.Abs).ToList();
        betraege.Sort();

        foreach (var (schluessel, wert) in values)
        {
            ergebnis[schluessel] = StufeVon(Math.Abs(wert), betraege, stepCount);
        }

        return ergebnis;
    }

    /// <summary>
    /// Die Stufe eines einzelnen Betrags: wie viele der vorhandenen Werte
    /// liegen echt darunter?
    ///
    /// "Echt darunter" und nicht "hoechstens gleich" ist der Punkt, an dem
    /// gleiche Betraege dieselbe Stufe bekommen - sie starten alle vom
    /// selben Rang aus.
    /// </summary>
    private static int StufeVon(long betrag, List<long> sortiert, int stepCount)
    {
        // Gespreizt wird ueber die AbSTAENDE zwischen den Raengen, nicht
        // ueber ihre Anzahl: der kleinste Betrag traegt dadurch immer
        // Stufe 1 und der groesste immer die hoechste. Ueber die Anzahl
        // gerechnet erreichte eine Tabelle mit zwei Werten die oberste
        // Stufe nie - die Skala haette dann Stufen, die nichts bedeuten.
        var nenner = sortiert.Count - 1;

        // Ein einziger Wert, oder lauter gleiche: nichts wiegt schwerer
        // als etwas anderes, also bleibt alles auf der untersten Stufe.
        // Eine durchgehend dunkle Tabelle behauptete sonst eine
        // Dringlichkeit, die sich aus den Zahlen nicht ergibt.
        if (nenner == 0)
        {
            return 1;
        }

        var rang = UntereGrenze(sortiert, betrag);

        // Ganzzahlig gerechnet: rang liegt zwischen 0 und nenner, das
        // Ergebnis damit zwischen 0 und stepCount-1.
        var stufe = 1 + (int)((long)rang * (stepCount - 1) / nenner);

        // Die Klemmung greift nur bei ungluecklicher Teilung - sicherer,
        // als sich darauf zu verlassen.
        return Math.Clamp(stufe, 1, stepCount);
    }

    /// <summary>
    /// Die Stelle, an der <paramref name="betrag"/> in die sortierte Liste
    /// gehoerte - also die Anzahl der echt kleineren Werte.
    ///
    /// <see cref="List{T}.BinarySearch(T)"/> taugt dafuer nicht: bei
    /// mehreren gleichen Werten gibt er IRGENDEINEN ihrer Plaetze zurueck,
    /// nicht den ersten. Genau das wuerde gleiche Betraege auf
    /// verschiedene Stufen werfen.
    /// </summary>
    private static int UntereGrenze(List<long> sortiert, long betrag)
    {
        var links = 0;
        var rechts = sortiert.Count;

        while (links < rechts)
        {
            var mitte = links + (rechts - links) / 2;
            if (sortiert[mitte] < betrag)
            {
                links = mitte + 1;
            }
            else
            {
                rechts = mitte;
            }
        }

        return links;
    }
}
