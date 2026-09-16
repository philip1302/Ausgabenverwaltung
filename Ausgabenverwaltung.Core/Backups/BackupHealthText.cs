using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Die Saetze zum Gesamtzustand der Datensicherung.
///
/// Jeder Erklaerungstext sagt dieselben drei Dinge wie jede andere
/// Meldung der Anwendung (Regel 12): was ist der Fall, was bedeutet das
/// fuer die Daten, was kann der Anwender tun. Die Ueberschrift benennt
/// den Fall nur - erklaert wird er darunter.
///
/// Reine Textbildung ohne Dateizugriff, damit sie pruefbar bleibt
/// (Regel 7); <c>MeldungsGrundsaetzeTests</c> haelt sie gegen dieselben
/// Grundsaetze wie alle uebrigen Texte.
/// </summary>
public static class BackupHealthText
{

    public static string Ueberschrift(BackupHealthLevel level) => level switch
    {
        BackupHealthLevel.Gesichert =>
            "Die Daten sind gesichert.",

        BackupHealthLevel.Eingeschraenkt =>
            "Die Sicherung liegt nur auf diesem Rechner.",

        _ =>
            "Es gibt keine gültige Sicherung.",
    };

    public static string Erklaerung(BackupHealthLevel level) => level switch
    {
        BackupHealthLevel.Gesichert =>
            "Es liegt eine Sicherung auf diesem Rechner und eine zweite im "
            + "zusätzlich gewählten Ordner. Die erfassten Daten sind damit "
            + "vollständig gesichert; geht die aktive Datei verloren, lässt sich "
            + "der letzte gesicherte Stand zurückholen. Zu tun ist nichts.",

        BackupHealthLevel.Eingeschraenkt =>
            "Gesichert wird, aber nur auf diesem Rechner — die zweite Kopie fehlt, "
            + "ist nicht angekommen oder ist zu alt. Die erfassten Daten sind "
            + "unverändert und gesichert; geht jedoch dieser Rechner verloren, sind "
            + "die Sicherungen mit weg. Am besten unten einen zweiten Ordner wählen, "
            + "etwa auf einem USB-Stick oder in einem Cloud-Ordner.",

        _ =>
            "Es konnte keine Sicherung angelegt werden, oder es ist bislang keine "
            + "vorhanden. Die erfassten Daten sind davon nicht betroffen und "
            + "unverändert — es fehlt aber der Stand, auf den sich im Schadensfall "
            + "zurückgehen ließe. Bitte „Jetzt sichern“ auslösen; scheitert das "
            + "erneut, nennt die Zeile darunter den Grund.",
    };

    /// <summary>
    /// Die Zeile unter der Ueberschrift: wann zuletzt wohin gesichert
    /// wurde. Bewusst EIN Satz ueber beide Ziele - die Aufteilung in zwei
    /// Teilaussagen war der Grund, warum der Zustand vorher nirgends
    /// vollstaendig dastand.
    /// </summary>
    /// <param name="health">Der ausgewertete Zustand.</param>
    /// <param name="nowLocal">Jetzt in lokaler Zeit - der Zeitstempel von
    /// Ziel 1 stammt aus dem Dateinamen und ist lokal.</param>
    /// <param name="nowUtc">Jetzt in UTC - fuer das Alter von Ziel 2.</param>
    public static string Zeitangabe(BackupHealth health, DateTime nowLocal, DateTime nowUtc)
    {
        var ziel1 = health.LastLocalBackup is DateTime letzte
            ? $"Zuletzt {Tagesangabe(letzte, nowLocal)} {letzte.ToString("HH:mm", Kultur.DeDe)} Uhr "
              + "auf diesem Rechner"
            : "Auf diesem Rechner liegt noch keine Sicherung";

        var ziel2 = !health.ExternalConfigured
            ? ", ein zweiter Ordner ist nicht eingerichtet."
            : $" und {ExternalBackupAge.ToText(health.LastExternalBackupUtc, nowUtc)} "
              + "im zusätzlichen Ordner.";

        return ziel1 + ziel2;
    }

    /// <summary>
    /// "heute"/"gestern"/Datum - dieselbe Staffelung wie in
    /// <see cref="ExternalBackupAge"/>, nur auf einem lokalen Zeitpunkt.
    /// </summary>
    private static string Tagesangabe(DateTime zeitpunkt, DateTime nowLocal)
    {
        return (nowLocal.Date - zeitpunkt.Date).Days switch
        {
            <= 0 => "heute",
            1 => "gestern",
            _ => "am " + zeitpunkt.ToString("dd.MM.yyyy", Kultur.DeDe),
        };
    }
}
