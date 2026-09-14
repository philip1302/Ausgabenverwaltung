namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Eine benannte Filtereinstellung, wie sie hinter dem Knopf
/// "Gespeicherte Filter" in Ausgabenliste und Auswertung steht.
///
/// Gespeichert wird der ZUSTAND DER LEISTE und nicht der fertige
/// <see cref="ReportFilter"/>: der ist bereits umgerechnet (Aeste und
/// Ausschluesse, ausschliessendes Ende) und liesse sich nicht wieder in
/// Haekchen zurueckverwandeln. Was hier steht, laesst sich beim Anwenden
/// eins zu eins in die Felder der Leiste zuruecklegen.
///
/// Liegt bei den Einstellungen und nicht in der Datenbank
/// (<see cref="Settings.AppSettings.SavedFilters"/>): ein gespeicherter
/// Filter ist eine Gewohnheit des Anwenders und kein Datenbestand.
/// </summary>
public sealed record SavedFilter
{
    /// <summary>
    /// Der vom Anwender vergebene Name, zugleich der Schluessel: ein
    /// zweites Speichern unter demselben Namen ersetzt den Eintrag
    /// (siehe <see cref="SavedFilters.Save"/>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der Schluessel der Zeitraum-Schnellwahl ("DieserMonat",
    /// "DiesesJahr", "Letzte12Monate", "Alles"), wenn der Zeitraum ueber
    /// sie eingestellt war - sonst NULL.
    ///
    /// Der Grund, warum hier nicht einfach zwei Datumsangaben stehen:
    /// "dieses Jahr" ist im naechsten Jahr etwas anderes. Wer einen
    /// Filter "Auto, dieses Jahr" ablegt, meint das laufende Jahr und
    /// nicht 2026 - festgehaltene Grenzen wuerden den Filter mit jedem
    /// Jahreswechsel stiller unbrauchbar machen.
    /// </summary>
    public string? PeriodKey { get; init; }

    /// <summary>
    /// Untergrenze, EINschliessend, wie sie im Feld steht. Nur ohne
    /// <see cref="PeriodKey"/> von Bedeutung. NULL = offene Grenze.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>Obergrenze, EINschliessend. NULL = offene Grenze.</summary>
    public DateOnly? ToInclusive { get; init; }

    /// <summary>
    /// Die angehakten Kategorien, genau wie im Baum angeklickt - also
    /// roh und nicht als Aeste und Ausschluesse (siehe
    /// <see cref="Categories.CategoryFilterSelection"/>). Beim Anwenden
    /// werden die Haekchen daraus wiederhergestellt; eine inzwischen
    /// geloeschte Kategorie faellt dabei weg.
    /// </summary>
    public IReadOnlyList<int> CategoryIds { get; init; } = [];

    /// <summary>Die angehakten Zahler. Leer = alle.</summary>
    public IReadOnlyList<int> PayerIds { get; init; } = [];

    public bool StatusOpen { get; init; }

    public bool StatusSettled { get; init; }

    public bool IncomeOnly { get; init; }

    public bool ExpensesOnly { get; init; }

    public bool MyCosts { get; init; }

    /// <summary>Suchtext, NULL oder leer wenn nicht gesucht wird.</summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Die Gruppierung der Auswertung. NULL bei einem Filter, der in der
    /// Ausgabenliste gespeichert wurde - die kennt keine Gruppierung.
    /// Angewendet in der Auswertung bleibt die eingestellte Gruppierung
    /// dann stehen, statt auf eine erfundene zurueckzufallen.
    /// </summary>
    public ReportGrouping? Grouping { get; init; }
}
