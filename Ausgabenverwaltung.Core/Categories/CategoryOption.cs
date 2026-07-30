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

    /// <summary>
    /// Die bereits aufgeloeste Farbe ('#RRGGBB', siehe
    /// <see cref="CategoryColors"/>) - schon hier und nicht erst in der
    /// Anzeige, damit die Vorschlagsliste den Baum nicht ein zweites Mal
    /// laden muss.
    /// </summary>
    public required string Color { get; init; }
}
