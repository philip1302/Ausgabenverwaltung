namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Wird geworfen, wenn unter demselben Elternknoten (oder auf oberster
/// Ebene, falls ParentId NULL ist) bereits eine Kategorie mit diesem
/// Namen existiert. Ersetzt die rohe SqliteException der UNIQUE-
/// Constraints (UNIQUE(ParentId, Name) bzw. UX_Category_RootName) durch
/// eine fuer die Oberflaeche verstaendliche Meldung.
/// </summary>
public sealed class DuplicateCategoryNameException : Exception
{
    public DuplicateCategoryNameException(string name)
        : base($"Es gibt an dieser Stelle im Kategoriebaum bereits eine Kategorie namens \"{name}\".")
    {
    }
}
