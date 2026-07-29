namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Sortierspalte der Offene-Posten-Liste. Vorgabe ist Datum aufsteigend
/// (aelteste zuerst).
/// </summary>
public enum OffenePostenSortSpalte
{
    Datum,
    Kategorie,
    Betrag,
    Bemerkung,
    TageOffen,
}
