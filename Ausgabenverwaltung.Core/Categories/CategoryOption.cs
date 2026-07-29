namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Eine fuer die Erfassung waehlbare Kategorie: ein Blattknoten mit
/// vollem Pfad ("Pferde › Hufschmied") fuer die Anzeige in der
/// Vorschlagsliste.
/// </summary>
public sealed class CategoryOption
{
    public required int Id { get; init; }
    public required string FullPath { get; init; }
}
