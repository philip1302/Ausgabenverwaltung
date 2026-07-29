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
    public DateTime CreatedUtc { get; set; }
}
