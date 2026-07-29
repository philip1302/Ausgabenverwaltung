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
    public DateTime CreatedUtc { get; set; }
}
