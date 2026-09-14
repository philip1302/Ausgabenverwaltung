namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Arten von Befunden, die der Rueckblick selbst herausrechnet.
///
/// Jede Art kommt hoechstens EINMAL vor - "die staerkste Steigerung" gibt
/// es nur in der Einzahl. Bewusst NICHT dabei ist eine eigene Art
/// "groesste relative Veraenderung": nach der Entrauschung
/// (<see cref="ReviewThresholds"/>) waere sie entweder dasselbe wie die
/// staerkste Steigerung oder sie liesse genau das Rauschen wieder herein,
/// das die Schwellen gerade aussperren. Die Prozentzahl steht stattdessen
/// in jedem Befund mit drin.
/// </summary>
public enum ReviewFindingKind
{
    /// <summary>Die groesste gedaempfte Zunahme einer Kategorie.</summary>
    StaerksteSteigerung,

    /// <summary>Die groesste gedaempfte Abnahme einer Kategorie.</summary>
    StaerksterRueckgang,

    /// <summary>Im Vorjahr keine Buchung, jetzt eine Ausgabenposition.</summary>
    NeuHinzugekommen,

    /// <summary>Im Vorjahr eine Ausgabenposition, jetzt keine Buchung mehr.</summary>
    Weggefallen,

    /// <summary>Die Oberkategorie mit dem groessten Anteil an den Ausgaben.</summary>
    GroessterKostenblock,

    /// <summary>
    /// Eine Kategorie, in der viele kleine Betraege zusammenkommen - die
    /// Frage, die eine reine Summenbetrachtung nie beantwortet.
    /// </summary>
    VieleKleineBuchungen,

    /// <summary>Die Kategorie mit den meisten Buchungen.</summary>
    HaeufigsteKategorie,

    /// <summary>Der Monat mit den hoechsten Ausgaben.</summary>
    TeuersterMonat,

    /// <summary>
    /// Der Monat, der am deutlichsten UNTER dem Monatsschnitt liegt -
    /// das Gegenstueck zum teuersten und damit nie derselbe.
    /// </summary>
    GuenstigsterMonat,
}

/// <summary>
/// Ein einzelner Befund des Jahresrueckblicks - eine Karte auf der Seite.
///
/// Betraege stehen positiv (siehe <see cref="ReviewCategoryChange"/>),
/// <see cref="DeltaCents"/> groesser null heisst "mehr ausgegeben". Der
/// Satz dazu entsteht in <see cref="ReviewText"/>, nicht hier: dieser Typ
/// traegt Zahlen, keine Sprache.
/// </summary>
public sealed record ReviewFinding
{
    public required ReviewFindingKind Kind { get; init; }

    /// <summary>NULL bei den Monatsbefunden - sie gehoeren zu keiner Kategorie.</summary>
    public required int? CategoryId { get; init; }

    /// <summary>
    /// Worueber der Befund spricht: der volle Kategoriepfad oder ein Monat
    /// ("September 2026").
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>Monatsschluessel ("2026-09") bei Monatsbefunden, sonst NULL.</summary>
    public required string? PeriodKey { get; init; }

    public required long PreviousCents { get; init; }
    public required long CurrentCents { get; init; }

    /// <summary>Groesser null = mehr ausgegeben als im Vorjahr.</summary>
    public required long DeltaCents { get; init; }

    /// <summary>NULL, wenn es keinen Vorjahreswert gibt - siehe ReviewMath.</summary>
    public required decimal? RelativeChange { get; init; }

    /// <summary>Anteil an den Ausgaben des Jahres, 0 bis 1.</summary>
    public required decimal Share { get; init; }

    public required int PreviousCount { get; init; }
    public required int CurrentCount { get; init; }

    /// <summary>
    /// Die Rangzahl, nach der die Befunde sortiert werden. KEIN Geldbetrag
    /// und niemals anzuzeigen (siehe <see cref="ReviewMath.Score"/>).
    /// </summary>
    public required long Score { get; init; }
}
