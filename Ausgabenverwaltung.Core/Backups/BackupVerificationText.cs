using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Das Merkmal, das nach einer Pruefung an der Sicherungszeile stehen
/// bleibt - kurz genug fuer eine Tabellenzeile.
///
/// Der ausfuehrliche Satz zu einem Fehlschlag kommt aus
/// <see cref="Errors.FileErrorText.ForBackupVerification"/> und erscheint
/// im Meldungsband; hier steht nur, was die Zeile selbst tragen kann.
/// </summary>
public static class BackupVerificationText
{

    public static string Merkmal(BackupVerificationResult ergebnis)
    {
        if (!ergebnis.IsReadable)
        {
            return "nicht lesbar";
        }

        var buchungen = ergebnis.ExpenseCount == 1
            ? "1 Buchung"
            : $"{Kultur.Anzahl(ergebnis.ExpenseCount ?? 0)} Buchungen";

        var kern = $"geprüft ✓ · Schema {ergebnis.SchemaVersion} · {buchungen}";

        // Lesbar, aber nicht zurueckspielbar - das muss an der Zeile
        // stehen und nicht nur im Band, sonst sieht sie aus wie jede
        // andere gepruefte Sicherung.
        return ergebnis.IsSchemaTooNew
            ? kern + " · neuere Programmversion"
            : kern;
    }
}
