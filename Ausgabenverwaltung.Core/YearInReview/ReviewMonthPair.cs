namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Ein Monat mit den Ausgaben BEIDER Zeitraeume - die Datengrundlage des
/// Monatsverlaufs. Ausgaben stehen positiv, wie ueberall im Rueckblick.
///
/// Die Folge ist lueckenlos: ein Monat, in dem in keinem der beiden Jahre
/// etwas gebucht wurde, steht mit zweimal 0 mit drin. Nur so behaelt jeder
/// Monat seinen Platz auf der Zeitachse; fiele er heraus, ruecken die
/// uebrigen zusammen und der Jahresverlauf stimmte nicht mehr.
/// </summary>
/// <param name="Key">
/// Der Monatsschluessel des LAUFENDEN Jahres, z. B. "2026-03". Er traegt
/// den Sprung in die Buchungen dieses Monats.
/// </param>
/// <param name="Label">Achsentext, z. B. "Mär".</param>
/// <param name="CurrentCount">
/// Buchungen im laufenden Jahr. Unterscheidet "nichts gebucht" von
/// "Ausgabe und Erstattung heben sich auf" - dieselbe Unterscheidung wie
/// bei <see cref="ReviewComparison"/>.
/// </param>
public sealed record ReviewMonthPair(
    string Key,
    string Label,
    long PreviousCents,
    long CurrentCents,
    int CurrentCount)
{
    public long DeltaCents => CurrentCents - PreviousCents;
}
