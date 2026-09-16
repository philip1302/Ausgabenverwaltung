namespace Ausgabenverwaltung.Core.Reports;

/// <summary>Eine Kategorie mit dem, was in einem Zeitraum auf sie entfiel.</summary>
public sealed record CategorySum(int CategoryId, string Name, long SumCents);

/// <summary>
/// Eine Zeile der Aufstellung "wofuer": Name, Betrag und der Anteil am
/// Ganzen (0 bis 1).
///
/// <see cref="CategoryId"/> ist NULL beim Sammeleintrag am Ende. Er steht
/// fuer mehrere Kategorien zugleich und fuehrt deshalb nirgendwohin - die
/// Ansicht macht aus ihm keinen Knopf.
/// </summary>
public sealed record CategoryShare(int? CategoryId, string Name, long SumCents, double Share);

/// <summary>
/// Macht aus Kategoriesummen die kurze Aufstellung, die auf eine Karte
/// passt: die groessten zuerst, der Rest zu einer Zeile zusammengefasst.
///
/// Reine Arithmetik, damit pruefbar (Regel 7). Woher die Summen kommen -
/// welcher Zeitraum, welcher Zahlerbereich, Ausgaben oder Einnahmen -
/// entscheidet der Aufrufer; hier wird nur noch sortiert und geteilt.
/// </summary>
public static class CategoryShares
{
    /// <summary>
    /// So heisst die letzte Zeile, wenn mehrere Kategorien in ihr
    /// zusammengefasst sind. Die Anzahl gehoert dazu: "Übrige" allein
    /// laesst offen, ob dahinter zwei Kategorien stehen oder zwanzig.
    /// </summary>
    public static string RestName(int anzahl) => $"Übrige ({anzahl})";

    /// <param name="sums">
    /// Die Summen je Kategorie, Vorzeichen wie gebucht. Eintraege ohne
    /// Betrag fallen heraus - eine Kategorie mit 0 € beantwortet die
    /// Frage "wofuer" nicht.
    /// </param>
    /// <param name="count">
    /// Wieviele Kategorien einzeln genannt werden. Bliebe danach genau
    /// EINE uebrig, wird auch sie noch einzeln genannt: "Übrige (1)"
    /// verschweigt einen Namen, ohne dafuer Platz zu sparen.
    /// </param>
    public static IReadOnlyList<CategoryShare> Top(
        IReadOnlyList<CategorySum> sums, int count)
    {
        if (count < 1)
        {
            return [];
        }

        var sortiert = sums
            .Where(eintrag => eintrag.SumCents > 0)
            // Bei gleichem Betrag nach Namen: sonst haengt die Reihenfolge
            // an der Reihenfolge der Datenbankzeilen und die Karte
            // vertauscht sich zwischen zwei Aufrufen ohne Grund.
            .OrderByDescending(eintrag => eintrag.SumCents)
            .ThenBy(eintrag => eintrag.Name, StringComparer.CurrentCulture)
            .ToList();

        if (sortiert.Count == 0)
        {
            return [];
        }

        var gesamt = sortiert.Sum(eintrag => eintrag.SumCents);
        var einzeln = sortiert.Count == count + 1 ? count + 1 : count;

        var zeilen = sortiert
            .Take(einzeln)
            .Select(eintrag => new CategoryShare(
                eintrag.CategoryId, eintrag.Name, eintrag.SumCents,
                Anteil(eintrag.SumCents, gesamt)))
            .ToList();

        var rest = sortiert.Skip(einzeln).ToList();
        if (rest.Count > 0)
        {
            var restSumme = rest.Sum(eintrag => eintrag.SumCents);
            zeilen.Add(new CategoryShare(
                null, RestName(rest.Count), restSumme, Anteil(restSumme, gesamt)));
        }

        return zeilen;
    }

    private static double Anteil(long teil, long gesamt)
        => gesamt <= 0 ? 0 : (double)teil / gesamt;
}
