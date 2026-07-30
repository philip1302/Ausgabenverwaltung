namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Wird geworfen, wenn eine Kategorie MIT Unterkategorien zusammengefuehrt
/// werden soll. Zusammengefuehrt wird immer nur ein einzelner Knoten,
/// niemals ein ganzer Ast: was mit den Unterkategorien geschehen soll,
/// ist bei jeder einzelnen eine eigene Entscheidung (archivieren,
/// loeschen, woandershin zusammenfuehren). Ein stilles rekursives
/// Verschieben wuerde diese Entscheidung vorwegnehmen und liesse sich
/// nicht rueckgaengig machen.
/// </summary>
public sealed class CategoryHasChildrenException : Exception
{
    public CategoryHasChildrenException(string name, int childCount)
        : base($"Die Kategorie \"{name}\" hat {childCount} " +
               (childCount == 1 ? "Unterkategorie" : "Unterkategorien") +
               " und kann deshalb nicht zusammengefuehrt werden.")
    {
        Name = name;
        ChildCount = childCount;
    }

    public string Name { get; }

    public int ChildCount { get; }
}
