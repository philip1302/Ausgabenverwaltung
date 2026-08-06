namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Eltern-Kind-Beziehung einer Kategorie, so knapp wie diese Rechnung sie
/// braucht. Bewusst nicht <see cref="CategoryNode"/>: die Ableitung
/// interessiert nur, wer wessen Kind ist - Name, Farbe und
/// Archivierungszustand spielen keine Rolle, und ein flacher Aufbau laesst
/// sich aus jeder Quelle herstellen.
/// </summary>
public readonly record struct CategoryParentLink(int Id, int? ParentId);

/// <summary>
/// Das Ergebnis: die gewaehlten Aeste und die davon wieder ausgenommenen
/// Unter-Aeste, fertig fuer <c>ReportFilter.CategoryRootIds</c> und
/// <c>ReportFilter.ExcludedCategoryIds</c>.
/// </summary>
public sealed record CategoryFilterChoice(
    IReadOnlyList<int> RootIds,
    IReadOnlyList<int> ExcludedIds);

/// <summary>
/// Uebersetzt die Haekchen eines Kategorie-Baums in das Filtermodell.
///
/// Die Filterleiste zeigt einen Baum, in dem jeder Knoten ein Haekchen
/// traegt. Daraus muessen zwei Listen werden, denn die Abfrage kennt nur
/// "diese Aeste" und "diese Aeste nicht" (siehe ReportFilterSql):
///
/// <list type="bullet">
///   <item><b>Ast</b> wird ein angehakter Knoten, dessen Elternteil NICHT
///   angehakt ist. Nur der oberste angehakte Knoten eines Zweiges kommt in
///   die Liste - seine angehakten Kinder sind ueber ihn schon erfasst,
///   weil ein Ast immer den ganzen Unterbaum umfasst.</item>
///   <item><b>Ausgenommen</b> wird ein NICHT angehakter Knoten, dessen
///   Elternteil angehakt ist. Auch hier nur der oberste: was unter einem
///   ausgenommenen Knoten haengt, faellt ohnehin mit weg.</item>
/// </list>
///
/// Beispiel - Haushalt angehakt, darin Restaurant abgewaehlt, Auto
/// ebenfalls angehakt: Aeste = [Haushalt, Auto], Ausgenommen =
/// [Restaurant]. Was unter Restaurant haengt, muss nicht aufgezaehlt
/// werden.
///
/// <b>Gar nichts angehakt heisst "keine Einschraenkung"</b>, nicht "keine
/// Treffer" - dieselbe Regel wie bei allen uebrigen Listenfiltern (siehe
/// <c>ReportFilter</c>). Wer nichts auswaehlt, will die Kategorie nicht
/// als Filter benutzen; "zeig mir nichts" ist kein sinnvoller Wunsch.
/// </summary>
public static class CategoryFilterSelection
{
    /// <summary>
    /// Rechnet die Haekchen in Aeste und Ausschluesse um. Die Reihenfolge
    /// der Ergebnislisten folgt der Reihenfolge von
    /// <paramref name="categories"/>, damit das Ergebnis vorhersagbar und
    /// pruefbar bleibt.
    ///
    /// Kategorien in <paramref name="checkedIds"/>, die in
    /// <paramref name="categories"/> gar nicht vorkommen, werden
    /// uebergangen - eine inzwischen geloeschte Kategorie soll die
    /// Auswertung nicht zum Stehen bringen.
    /// </summary>
    public static CategoryFilterChoice Derive(
        IReadOnlyList<CategoryParentLink> categories,
        IReadOnlySet<int> checkedIds)
    {
        var roots = new List<int>();
        var excluded = new List<int>();

        // Nichts angehakt: kein Filter. Ohne diese Abkuerzung waeren beide
        // Listen ohnehin leer - sie steht hier, weil die Absicht sonst nur
        // aus dem Ausbleiben von Treffern hervorginge.
        if (checkedIds.Count == 0)
        {
            return new CategoryFilterChoice(roots, excluded);
        }

        foreach (var (id, parentId) in categories)
        {
            var selbstAngehakt = checkedIds.Contains(id);

            // Ein Knoten ohne Elternteil hat keinen, der ihn abdecken
            // koennte: angehakt wird er Ast, nicht angehakt ist er
            // schlicht nicht dabei und muss auch nicht ausgenommen werden.
            var elternAngehakt = parentId is int eltern && checkedIds.Contains(eltern);

            if (selbstAngehakt && !elternAngehakt)
            {
                roots.Add(id);
            }
            else if (!selbstAngehakt && elternAngehakt)
            {
                excluded.Add(id);
            }
        }

        return new CategoryFilterChoice(roots, excluded);
    }
}
