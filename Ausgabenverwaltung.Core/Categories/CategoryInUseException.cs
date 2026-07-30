namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Wird geworfen, wenn eine Kategorie geloescht werden soll, die noch
/// benutzt wird. Traegt die Zaehlung mit, damit die Oberflaeche
/// benennen kann, was im Weg steht, statt nur "geht nicht" zu melden.
///
/// Ersetzt die rohe SqliteException der Fremdschluessel
/// (ON DELETE RESTRICT): geprueft wird vorher, damit die Meldung
/// verstaendlich ist - die Fremdschluessel bleiben die Absicherung
/// dahinter.
/// </summary>
public sealed class CategoryInUseException : Exception
{
    public CategoryInUseException(string name, CategoryUsage usage)
        : base($"Die Kategorie \"{name}\" wird noch verwendet und kann nicht geloescht werden.")
    {
        Name = name;
        Usage = usage;
    }

    public string Name { get; }

    public CategoryUsage Usage { get; }
}
