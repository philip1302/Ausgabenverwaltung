namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Loest die Farbe jeder Kategorie auf.
///
/// Regel: eine Kategorie ohne eigene Farbe erbt die des naechsten
/// Vorfahren, der eine hat. Dadurch genuegt es, den Oberkategorien
/// Farben zu geben - jede neue Unterkategorie ordnet sich von selbst
/// ein. Eine eigene Farbe ueberschreibt die geerbte fuer den Knoten UND
/// seinen ganzen Ast, bis wieder jemand ueberschreibt.
///
/// Hat im ganzen Ast niemand eine Farbe, gilt
/// <see cref="CategoryColorPalette.DefaultHex"/>. Das Ergebnis ist also
/// nie leer: die Anzeige bekommt fuer jede Kategorie einen Wert und
/// braucht keinen Sonderfall.
///
/// Werte ausserhalb der Palette werden behandelt, als staende dort
/// nichts - siehe <see cref="CategoryColorPalette.IsKnown"/>.
/// </summary>
public static class CategoryColors
{
    /// <summary>
    /// Die aufgeloeste Farbe je Kategorie-Id, ueber den ganzen Baum.
    /// </summary>
    public static IReadOnlyDictionary<int, string> Resolve(IReadOnlyList<CategoryNode> roots)
    {
        var result = new Dictionary<int, string>();
        Collect(roots, CategoryColorPalette.DefaultHex, result);
        return result;
    }

    /// <summary>
    /// Nachschlagen mit Rueckfallwert. Eine Buchung kann auf eine
    /// Kategorie zeigen, die im gerade geladenen Baum fehlt (etwa weil
    /// zwischen zwei Abfragen umsortiert wurde) - das darf die Anzeige
    /// nicht aufhalten.
    /// </summary>
    public static string Of(IReadOnlyDictionary<int, string> resolved, int categoryId) =>
        resolved.TryGetValue(categoryId, out var color) ? color : CategoryColorPalette.DefaultHex;

    private static void Collect(
        IReadOnlyList<CategoryNode> nodes, string inherited, Dictionary<int, string> result)
    {
        foreach (var node in nodes)
        {
            var own = node.Category.Color;
            var effective = CategoryColorPalette.IsKnown(own) ? own! : inherited;

            result[node.Category.Id] = effective;

            // Die eigene (oder geerbte) Farbe ist ab hier die geerbte des
            // Astes - deshalb effective und nicht inherited weitergeben.
            Collect(node.Children, effective, result);
        }
    }
}
