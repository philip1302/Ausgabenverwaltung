namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Die Eingabebestaetigung vor dem Wiederherstellen: der Anwender tippt den
/// Dateinamen der Sicherung ab.
///
/// Warum so viel Reibung, wo sonst ein Undo-Band genuegt: Reibung wird nach
/// Schadensausmass bemessen, und dies ist die einzige Aktion der Anwendung,
/// die den GANZEN Datenbestand austauscht. Ein falsch getroffener Knopf
/// wuerde hier nicht eine Zeile kosten, sondern alles, was seit der
/// gewaehlten Sicherung erfasst wurde. Abtippen ist keine Huerde gegen
/// Unwissen, sondern gegen Unachtsamkeit: es erzwingt einen Blick auf
/// GENAU die Datei, die gleich eingespielt wird.
///
/// Steht in Core und nicht im ViewModel, weil es pruefbar ist (Regel 7).
/// </summary>
public static class RestoreConfirmation
{
    /// <summary>
    /// Passt das Getippte zum Dateinamen? Grosz-/Kleinschreibung ist egal
    /// und umgebende Leerzeichen fallen weg - beides bestraft nur das
    /// Abtippen selbst und schuetzt niemanden. Alles andere muss stimmen,
    /// insbesondere der Zeitstempel im Namen: er ist der Unterschied
    /// zwischen der Sicherung von heute und der von vor drei Wochen.
    /// </summary>
    public static bool Matches(string? typed, string fileName)
        => typed is not null
           && string.Equals(
               typed.Trim(), fileName.Trim(), StringComparison.OrdinalIgnoreCase);
}
