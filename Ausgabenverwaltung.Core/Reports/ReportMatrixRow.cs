namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Eine Zeile der Kreuztabelle - eine Kategorie mit ihren Werten je
/// Zeitabschnitt. Die Zeilen bilden denselben Baum wie die Kategorien;
/// <see cref="Children"/> haengt immer vollstaendig darunter, unabhaengig
/// davon, was die Oberflaeche gerade aufgeklappt zeigt.
///
/// Alle Werte enthalten die Unterkategorien bereits (siehe
/// <see cref="ReportMatrixCell"/>). Die Zeile einer Oberkategorie ist also
/// KEINE Zwischenueberschrift, sondern die Summe ihres ganzen Astes.
/// </summary>
public sealed class ReportMatrixRow
{
    public required int CategoryId { get; init; }

    /// <summary>Nur der Name der Kategorie, fuer die eingerueckte Anzeige.</summary>
    public required string Name { get; init; }

    /// <summary>Voller Pfad ab der Wurzel, fuer Beschriftungen ausserhalb des Baums.</summary>
    public required string FullPath { get; init; }

    /// <summary>Tiefe unterhalb der obersten angezeigten Zeile (0 = oben).</summary>
    public required int Depth { get; init; }

    public required bool IsArchived { get; init; }

    /// <summary>
    /// Nur die BELEGTEN Zeitabschnitte. Ein fehlender Schluessel bedeutet
    /// "keine Buchung" - siehe <see cref="Cell"/>.
    /// </summary>
    public required IReadOnlyDictionary<string, ReportAmount> Cells { get; init; }

    /// <summary>Summe ueber den gesamten Zeitraum, inklusive Unterkategorien.</summary>
    public required ReportAmount Total { get; init; }

    public required IReadOnlyList<ReportMatrixRow> Children { get; init; }

    public ReportAmount Cell(string periodKey) =>
        Cells.TryGetValue(periodKey, out var amount) ? amount : ReportAmount.Empty;
}
