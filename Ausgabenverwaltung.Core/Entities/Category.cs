namespace Ausgabenverwaltung.Core.Entities;

/// <summary>
/// Eine Kategorie im selbstreferenzierenden Baum. Flaches 1:1-Abbild
/// der Tabelle Category, ohne Kindknoten (siehe dazu CategoryNode).
/// </summary>
public sealed class Category
{
    public int Id { get; set; }

    /// <summary>NULL = Oberkategorie.</summary>
    public int? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>
    /// Eigene Farbe als '#RRGGBB', NULL = keine. NULL heisst nicht
    /// "farblos": die Kategorie erbt dann die Farbe des naechsten
    /// Vorfahren, der eine hat (siehe
    /// <see cref="Categories.CategoryColors"/>).
    /// </summary>
    public string? Color { get; set; }

    public DateTime CreatedUtc { get; set; }
}
