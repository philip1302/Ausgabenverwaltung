using Ausgabenverwaltung.Core.Entities;

namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Baumknoten des geladenen Kategorie-Baums. Haelt Category und Kinder
/// getrennt, damit Category selbst ein reines 1:1-Abbild der Tabelle bleibt.
/// </summary>
public sealed class CategoryNode
{
    public CategoryNode(Category category)
    {
        Category = category;
    }

    public Category Category { get; }
    public List<CategoryNode> Children { get; } = new();
}
