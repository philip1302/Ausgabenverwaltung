namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Die Art eines Speicherproblems - so weit eingeteilt, wie sich daraus
/// ein UNTERSCHIEDLICHER Rat an den Anwender ableiten laesst, und keinen
/// Schritt weiter.
///
/// Das ist der Grund fuer diesen Zwischenschritt: "Der Datentraeger ist
/// voll" und "Die Datei ist von einem anderen Programm geoeffnet" fuehren
/// zu voellig verschiedenen naechsten Handgriffen, waehrend die
/// darunterliegenden Fehlernummern (ERROR_DISK_FULL, SQLITE_FULL, ...) den
/// Anwender nichts angehen. Die Einteilung passiert einmal in
/// <see cref="StorageProblems"/>, die Formulierung je nach Zusammenhang in
/// <see cref="FileErrorText"/> bzw. <see cref="DatabaseErrorText"/>.
/// </summary>
public enum StorageProblem
{
    /// <summary>Nicht einzuordnen - der Text bleibt dann allgemeiner, nennt
    /// aber trotzdem Folge und naechsten Schritt.</summary>
    Unknown,

    /// <summary>Kein Platz mehr auf dem Datentraeger.</summary>
    DiskFull,

    /// <summary>Keine Berechtigung zum Schreiben an dieser Stelle.</summary>
    AccessDenied,

    /// <summary>Die Zieldatei ist von einem anderen Programm geoeffnet -
    /// beim CSV-Export der mit Abstand haeufigste Fall (Excel).</summary>
    FileInUse,

    /// <summary>Ordner oder Laufwerk gibt es nicht (mehr): abgezogener
    /// USB-Stick, getrenntes Netzlaufwerk, umbenannter Ordner.</summary>
    PathNotFound,

    /// <summary>Datentraeger oder Datei ist schreibgeschuetzt.</summary>
    ReadOnly,

    /// <summary>Die Datenbankdatei ist gesperrt - in aller Regel, weil die
    /// Anwendung bereits laeuft.</summary>
    DatabaseLocked,

    /// <summary>Die Datenbankdatei ist beschaedigt oder gar keine
    /// Datenbank.</summary>
    DatabaseCorrupt,
}
