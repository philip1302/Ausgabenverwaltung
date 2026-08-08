namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Welcher Filter hinter einem Chip steckt - zugleich der Auftrag, den
/// das Wegklicken ausfuehrt.
/// </summary>
public enum FilterArt
{
    Zeitraum,
    Kategorien,
    Zahler,
    Status,
    Buchungsart,
    MeineKosten,
    Suche,
}

/// <summary>
/// Ein aktiver Filter, wie er unter der Leiste steht: eine Beschriftung
/// und das Kreuz, das genau ihn aufhebt.
/// </summary>
public sealed record FilterChip(FilterArt Art, string Beschriftung);

/// <summary>
/// Der Zustand der Filterleiste, so weit er fuer die Chips zaehlt.
/// Bewusst schon aufbereitet: die Beschriftungen kommen aus
/// <see cref="FilterCaption"/>, die Ansicht traegt hier nur zusammen.
/// </summary>
public sealed record FilterZustand
{
    /// <summary>Der Zeitraum, wenn er VOM VORGABEZEITRAUM ABWEICHT -
    /// sonst NULL. Der Vorgabezeitraum ist kein Filter, sondern der
    /// Ausgangspunkt; ihn als Chip zu zeigen hiesse, jede Ansicht
    /// dauerhaft als "gefiltert" zu bezeichnen.</summary>
    public string? Zeitraum { get; init; }

    /// <summary>Beschriftung der Kategorieauswahl, NULL wenn "alle".</summary>
    public string? Kategorien { get; init; }

    /// <summary>Beschriftung der Zahlerauswahl, NULL wenn "alle".</summary>
    public string? Zahler { get; init; }

    public bool StatusOffen { get; init; }

    public bool StatusBeglichen { get; init; }

    public bool NurEinnahmen { get; init; }

    public bool NurAusgaben { get; init; }

    public bool MeineKosten { get; init; }

    /// <summary>Suchtext, NULL oder leer wenn nicht gesucht wird.</summary>
    public string? Suche { get; init; }
}

/// <summary>
/// Baut aus dem Filterzustand die Liste der Chips, die unter der
/// Filterleiste stehen.
///
/// <b>Warum es diese Liste ueberhaupt gibt.</b> Die Filterleiste laesst
/// sich einklappen, damit sie bei schmalem Fenster nicht die halbe Seite
/// einnimmt. Eingeklappt sieht man aber nicht mehr, WORAUF gefiltert wird
/// - und ein Filter, der wirkt, ohne sich zu zeigen, ist der haeufigste
/// Grund fuer "meine Buchungen sind weg" (siehe die gleichlautende
/// Begruendung beim Einzelbuchungsfilter). Die Chips sind der Preis
/// dafuer, dass die Leiste verschwinden darf: sie bleiben immer stehen,
/// auch eingeklappt, und jeder laesst sich einzeln wegklicken.
///
/// Steht in Core und nicht in den ViewModels, weil beide Filterleisten -
/// Ausgabenliste und Auswertung - dieselben Zustaende gleich benennen
/// muessen; dieselbe Begruendung wie bei <see cref="FilterCaption"/>, und
/// pruefbar ist es obendrein (Regel 7).
/// </summary>
public static class FilterChips
{
    public static IReadOnlyList<FilterChip> Bestimme(FilterZustand zustand)
    {
        var chips = new List<FilterChip>();

        if (!string.IsNullOrWhiteSpace(zustand.Zeitraum))
        {
            chips.Add(new FilterChip(FilterArt.Zeitraum, zustand.Zeitraum));
        }

        if (!string.IsNullOrWhiteSpace(zustand.Kategorien))
        {
            chips.Add(new FilterChip(FilterArt.Kategorien, zustand.Kategorien));
        }

        if (!string.IsNullOrWhiteSpace(zustand.Zahler))
        {
            chips.Add(new FilterChip(FilterArt.Zahler, zustand.Zahler));
        }

        // Status und Buchungsart sind je zwei Haekchen, ergeben aber EINEN
        // Chip: "offen und beglichen" nebeneinander waere zwar richtig,
        // aber niemand liest zwei Chips als ein Filterfeld. Sind BEIDE
        // Haekchen gesetzt, schraenkt das ohnehin nicht ein - dann gibt es
        // auch keinen Chip.
        if (zustand.StatusOffen != zustand.StatusBeglichen)
        {
            chips.Add(new FilterChip(
                FilterArt.Status, zustand.StatusOffen ? "Nur offene" : "Nur beglichene"));
        }

        if (zustand.NurEinnahmen != zustand.NurAusgaben)
        {
            chips.Add(new FilterChip(
                FilterArt.Buchungsart, zustand.NurEinnahmen ? "Nur Einnahmen" : "Nur Ausgaben"));
        }

        if (zustand.MeineKosten)
        {
            chips.Add(new FilterChip(FilterArt.MeineKosten, "Meine Kosten"));
        }

        if (!string.IsNullOrWhiteSpace(zustand.Suche))
        {
            // In Anfuehrungszeichen, weil ein Suchtext alles Moegliche sein
            // kann - ohne sie liesse sich "Nur offene" nicht davon
            // unterscheiden, dass jemand genau das gesucht hat.
            chips.Add(new FilterChip(FilterArt.Suche, $"Suche: „{zustand.Suche.Trim()}“"));
        }

        return chips;
    }
}
