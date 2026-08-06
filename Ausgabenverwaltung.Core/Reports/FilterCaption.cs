using Ausgabenverwaltung.Core.Categories;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die Beschriftung, die eine zugeklappte Filterauswahl traegt ("Alle
/// Kategorien", "Haushalt ohne Restaurant", "3 Zahler").
///
/// Steht hier und nicht in den ViewModels, weil beide Filterleisten -
/// Report und Ausgabenliste - dieselbe Auswahl gleich benennen muessen
/// und die Faelle pruefbar sind (Regel 7). Die Texte sind deutsch wie
/// alles, was der Anwender liest; dasselbe Muster wie bei
/// <see cref="Formatting.EuroText"/>.
///
/// Durchgehendes Prinzip: <b>eine leere Auswahl heisst "alle"</b>, nicht
/// "nichts" - der Text muss das aussprechen, sonst raet der Anwender, ob
/// gerade gefiltert wird oder nicht.
/// </summary>
public static class FilterCaption
{
    /// <summary>
    /// Beschriftung der Kategorie-Auswahl.
    /// <paramref name="names"/> liefert den Namen zu einer Id; fehlt eine
    /// Id darin (inzwischen geloescht), wird sie mitgezaehlt, aber nicht
    /// benannt.
    /// </summary>
    public static string Categories(
        CategoryFilterChoice choice, IReadOnlyDictionary<int, string> names)
    {
        var aeste = choice.RootIds;
        var ohne = choice.ExcludedIds;

        if (aeste.Count == 0 && ohne.Count == 0)
        {
            return "Alle Kategorien";
        }

        // Kein gewaehlter Ast, aber Ausschluesse: "alles ausser X". Das
        // ist ein sinnvoller Wunsch und darf nicht als "alle" erscheinen.
        if (aeste.Count == 0)
        {
            return "Alle außer " + Aufzaehlung(ohne, names, "Kategorien");
        }

        var basis = Aufzaehlung(aeste, names, "Äste");

        return ohne.Count == 0
            ? basis
            : basis + " ohne " + Aufzaehlung(ohne, names, "Unterkategorien");
    }

    /// <summary>
    /// Beschriftung der Zahler-Auswahl. Uebergeben werden die Namen der
    /// angehakten Zahler - eine leere Liste heisst "alle".
    /// </summary>
    public static string Payers(IReadOnlyList<string> selectedNames) => selectedNames.Count switch
    {
        0 => "Alle Zahler",
        1 => selectedNames[0],
        _ => $"{selectedNames.Count} Zahler",
    };

    /// <summary>
    /// Beschriftung der Status-Haekchen. Beide an ist ausdruecklich NICHT
    /// "alle" (Regel 4: eigene Ausgaben haben keinen Status und fallen
    /// dann heraus) - der Text sagt das, statt beides gleich zu benennen.
    /// </summary>
    public static string Settlement(SettlementStatus status) => status switch
    {
        SettlementStatus.Alle => "Jeder Status",
        SettlementStatus.Offene => "Nur offene",
        SettlementStatus.Beglichene => "Nur beglichene",
        _ => "Offene und beglichene",
    };

    // Ein Name bei einem Eintrag, sonst die Anzahl mit Sammelbegriff -
    // drei Namen nebeneinander sprengen die Schaltflaeche, und der volle
    // Inhalt steht ohnehin aufgeklappt darunter.
    private static string Aufzaehlung(
        IReadOnlyList<int> ids, IReadOnlyDictionary<int, string> names, string mehrzahl)
    {
        if (ids.Count == 1 && names.TryGetValue(ids[0], out var name))
        {
            return name;
        }

        return $"{ids.Count} {mehrzahl}";
    }
}
