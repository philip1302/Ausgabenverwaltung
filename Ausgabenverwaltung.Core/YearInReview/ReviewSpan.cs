namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Wie weit die beiden verglichenen Jahre reichen sollen.
///
/// Die Unterscheidung gibt es nur wegen des LAUFENDEN Jahres: wer im
/// September ein angefangenes Jahr gegen ein volles Vorjahr stellt, sieht
/// ueberall einen Rueckgang, der keiner ist, sondern nur das fehlende
/// Quartal. Bei einem abgeschlossenen Jahr fallen beide Faelle zusammen;
/// <see cref="ReviewPeriods"/> liefert dort unabhaengig von der Wahl das
/// ganze Kalenderjahr.
/// </summary>
public enum ReviewSpan
{
    /// <summary>
    /// Beide Jahre nur bis zum heutigen Tag-und-Monat - also etwa
    /// 1. Januar bis 13. September gegen 1. Januar bis 13. September.
    /// Die Voreinstellung, weil sie die einzige ist, deren Unterschied
    /// ausschliesslich aus dem Ausgabeverhalten stammt.
    /// </summary>
    GleicherZeitraum,

    /// <summary>Beide Jahre vollstaendig, vom 1. Januar bis zum 31. Dezember.</summary>
    GanzeKalenderjahre,
}
