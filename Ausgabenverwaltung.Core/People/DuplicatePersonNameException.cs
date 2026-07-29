namespace Ausgabenverwaltung.Core.People;

/// <summary>
/// Wird geworfen, wenn bereits eine Person mit diesem Namen existiert.
/// Ersetzt die rohe SqliteException des UNIQUE-Constraints (UNIQUE(Name))
/// durch eine fuer die Oberflaeche verstaendliche Meldung.
/// </summary>
public sealed class DuplicatePersonNameException : Exception
{
    public DuplicatePersonNameException(string name)
        : base($"Es gibt bereits eine Person namens \"{name}\".")
    {
    }
}
