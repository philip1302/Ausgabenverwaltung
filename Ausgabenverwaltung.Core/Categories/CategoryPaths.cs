namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Bildet die vollen Kategoriepfade ab der Wurzel ("Pferde › Hufschmied").
/// Im Unterschied zu <see cref="CategoryRepository.GetSelectableLeaves"/>
/// liefert <see cref="BuildFullPaths"/> den Pfad JEDES Knotens, also auch
/// den von Ober- und Zwischenkategorien - gebraucht fuer die
/// Kategorie-Auswahl der Ausgabenliste, die auch auf einen ganzen Ast
/// filtern kann.
/// </summary>
public static class CategoryPaths
{
    /// <summary>
    /// Trennzeichen zwischen den Pfadstufen. Dasselbe Zeichen wird in den
    /// SQL-CTEs verwendet, die den Pfad direkt in der Datenbank bilden
    /// (siehe OpenItems.OpenItemsRepository und
    /// Expenses.ExpenseRepository) - wird es hier geaendert, muessen die
    /// Abfragen mitgeaendert werden.
    /// </summary>
    public const string Separator = " › ";

    public static IReadOnlyDictionary<int, string> BuildFullPaths(IReadOnlyList<CategoryNode> roots)
    {
        var paths = new Dictionary<int, string>();
        Collect(roots, parentPath: null, paths);
        return paths;
    }

    /// <summary>
    /// Haengt einen Kategorienamen an einen bestehenden Pfad an. Bei
    /// <paramref name="parentPath"/> = NULL entsteht ein Wurzelpfad.
    /// </summary>
    public static string Append(string? parentPath, string name) =>
        parentPath is null ? name : parentPath + Separator + name;

    private static void Collect(
        IReadOnlyList<CategoryNode> nodes, string? parentPath, Dictionary<int, string> paths)
    {
        foreach (var node in nodes)
        {
            var path = Append(parentPath, node.Category.Name);
            paths[node.Category.Id] = path;
            Collect(node.Children, path, paths);
        }
    }
}
