namespace Ausgabenverwaltung.Core.Entities;

/// <summary>
/// Eine Person: entweder der Anwender selbst (IsSelf = true, genau einmal)
/// oder ein moeglicher Zahlungsverantwortlicher.
/// </summary>
public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSelf { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>
    /// Anzeigereihenfolge, von Hand ueber Pfeil-nach-oben/-unten
    /// verschiebbar (siehe <see cref="People.PersonRepository.MoveUp"/>).
    /// Keine alphabetische Sortierung: die eigene Person soll z. B. oben
    /// stehen koennen, unabhaengig vom Namen.
    /// </summary>
    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; }
}
