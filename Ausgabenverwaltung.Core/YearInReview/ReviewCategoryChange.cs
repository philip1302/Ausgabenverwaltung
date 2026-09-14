namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Eine Kategorie mit ihren Ausgaben in beiden verglichenen Jahren.
///
/// <b>Ausgaben stehen hier POSITIV.</b> Die Auswertung liefert sie negativ
/// (siehe ReportRepository.SumCentsSql); gedreht wird genau EINMAL, beim
/// Bau dieser Zeilen. Ab hier gilt durchgaengig: ein groesserer Wert heisst
/// mehr ausgegeben, und <see cref="DeltaCents"/> groesser null heisst "mehr
/// als im Vorjahr". Damit kann kein Anzeigetext mehr versehentlich ein
/// Minuszeichen vor einen Betrag setzen.
///
/// Die Werte enthalten die Unterkategorien bereits - wie bei
/// ReportMatrixRow, aus der diese Zeilen entstehen. Die Zeile einer
/// Oberkategorie ist also keine Zwischenueberschrift, sondern die Summe
/// ihres ganzen Astes.
/// </summary>
public sealed record ReviewCategoryChange
{
    public required int CategoryId { get; init; }

    /// <summary>Nur der Name, fuer die eingerueckte Anzeige im Baum.</summary>
    public required string Name { get; init; }

    /// <summary>Voller Pfad ab der Wurzel - fuer Befundsaetze ausserhalb des Baums.</summary>
    public required string FullPath { get; init; }

    /// <summary>Tiefe unterhalb der obersten Zeile (0 = Oberkategorie).</summary>
    public required int Depth { get; init; }

    public required bool IsArchived { get; init; }

    /// <summary>Ausgaben des Vorjahreszeitraums, nicht negativ.</summary>
    public required long PreviousCents { get; init; }

    /// <summary>Ausgaben des betrachteten Zeitraums, nicht negativ.</summary>
    public required long CurrentCents { get; init; }

    public required int PreviousCount { get; init; }
    public required int CurrentCount { get; init; }

    public required IReadOnlyList<ReviewCategoryChange> Children { get; init; }

    /// <summary>Groesser null = mehr ausgegeben als im Vorjahr.</summary>
    public long DeltaCents => CurrentCents - PreviousCents;

    /// <summary>Ob in einem der beiden Jahre ueberhaupt gebucht wurde.</summary>
    public bool HasValues => PreviousCount > 0 || CurrentCount > 0;

    /// <summary>Ein Blatt - eine Kategorie, in die unmittelbar gebucht wird.</summary>
    public bool IstBlatt => Children.Count == 0;
}
