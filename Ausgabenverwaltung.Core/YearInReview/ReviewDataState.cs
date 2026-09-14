namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Lage, in der sich der Rueckblick befindet. Die Unterscheidung
/// gehoert nach Core und nicht in die Oberflaeche (Regel 7) - sie
/// entscheidet, ob dort ein Leerzustand mit Handlungsangebot steht oder
/// ein Ergebnis.
///
/// Es sind bewusst DREI Faelle und nicht die sonst ueblichen zwei: dass
/// sich zwischen zwei Jahren wenig verschoben hat, ist ein Befund und kein
/// Mangel. Wer dort "Noch nichts erfasst" liest, sucht einen Fehler, den
/// es nicht gibt.
/// </summary>
public enum ReviewDataState
{
    /// <summary>Die Datenbank ist leer - es gibt nichts zurueckzublicken.</summary>
    NochNichtsErfasst,

    /// <summary>
    /// Fuer das Vorjahr liegt keine Buchung vor. Kennzahlen und Tabelle
    /// gelten fuer sich und bleiben stehen, nur der Vergleich fehlt.
    /// </summary>
    VorjahrOhneBuchung,

    /// <summary>
    /// Beide Jahre sind belegt, aber keine Veraenderung ueberschreitet die
    /// Schwellen. Ein Ergebnis, kein Leerzustand - es gibt nichts
    /// aufzuloesen und deshalb auch keinen Knopf.
    /// </summary>
    KeineAuffaelligkeiten,

    /// <summary>Es gibt etwas zu erzaehlen.</summary>
    Vollstaendig,
}
