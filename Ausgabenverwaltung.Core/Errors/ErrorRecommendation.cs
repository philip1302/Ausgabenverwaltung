namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Was nach einem unerwarteten Fehler zu raten ist.
///
/// Die Unterscheidung gibt es, weil nicht jeder Fehler gleich schwer
/// wiegt: an einer Ansicht, die sich nicht aufbauen laesst, kann man
/// vorbeinavigieren und in Ruhe weiterarbeiten. Ein Fehler beim Schreiben
/// dagegen laesst offen, ob die naechste Aenderung ankommt - da waere
/// "einfach weitermachen" ein schlechter Rat.
/// </summary>
public enum ErrorRecommendation
{
    /// <summary>
    /// Weiterarbeiten ist vertretbar. Der Fehler betraf die Anzeige oder
    /// einen einzelnen Vorgang, nicht die Daten.
    /// </summary>
    Continue,

    /// <summary>
    /// Besser beenden und neu starten. Der Fehler hing am Speichern oder
    /// am Datentraeger.
    /// </summary>
    Restart,
}
