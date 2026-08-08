namespace Ausgabenverwaltung.Core.OpenItems;

/// <summary>
/// Sortierspalte der Offene-Posten-Liste. Vorgabe ist Datum aufsteigend
/// (aelteste zuerst) - anders als in der Ausgabenliste, wo das Neueste
/// oben steht: ein offener Posten wird mit dem Alter dringender.
///
/// Steht in Core und nicht bei den ViewModels, obwohl hier - anders als
/// bei <see cref="Expenses.ExpenseSortColumn"/> - nicht in SQL sortiert
/// wird, sondern im Speicher je Personengruppe. Der Grund ist das Merken
/// der Sortierung (Settings.AppSettings): eine Einstellung, deren Typ in
/// der Oberflaeche liegt, liesse sich dort nicht ablegen, und die
/// nachsichtige Umwandlung aus der Datei gehoert an die eine Stelle, die
/// das fuer alle Einstellungen tut (Settings.AppSettingsStore).
///
/// Die Namen der Werte bleiben deutsch, wie bei
/// <see cref="Expenses.ExpenseSortColumn"/> auch: sie stehen so in der
/// Einstellungsdatei und in den Kommandoparametern der Ansicht.
/// </summary>
public enum OpenItemsSortColumn
{
    Datum,
    Kategorie,
    Betrag,
    Bemerkung,
    TageOffen,
}
