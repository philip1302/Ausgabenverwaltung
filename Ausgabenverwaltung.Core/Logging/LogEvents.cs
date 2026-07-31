using System.Globalization;
using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Die Texte der protokollierten Ereignisse - an einer Stelle und ohne
/// Dateizugriff, damit sich pruefen laesst, WAS im Protokoll landet
/// (Regel 7).
///
/// Das ist der eigentliche Zweck dieser Klasse. Ein Protokoll soll sich
/// weitergeben lassen, ohne dass die Haushaltsfuehrung mitgeht: es
/// enthaelt Anzahlen, IDs, Versionen und Dateinamen, aber KEINE Betraege,
/// KEINE Bemerkungen und KEINE Personennamen. Wer ein Ereignis ergaenzt,
/// ergaenzt es hier - dann faellt beim Lesen dieser Datei sofort auf, wenn
/// sich Inhalt einschleicht, statt verstreut ueber ein Dutzend
/// Aufrufstellen.
///
/// Der Datenbankpfad ist die eine bewusste Ausnahme: er enthaelt unter
/// Windows den Anmeldenamen. Ohne ihn laesst sich aber nicht beantworten,
/// welche Datei die Anwendung ueberhaupt geoeffnet hat - und genau das ist
/// bei einem Fehlerbericht die erste Frage.
/// </summary>
public static class LogEvents
{
    public static string ProgramStart(string version, string databaseFilePath)
        => $"Programmstart. Version {version}, Datenbank: {databaseFilePath}";

    public static string ProgramEnd()
        => "Programm wird beendet.";

    public static string MigrationStarted(int fromVersion, int toVersion)
        => $"Schema-Migration beginnt: Version {fromVersion} -> {toVersion}.";

    public static string MigrationFinished(int fromVersion, int toVersion)
        => $"Schema-Migration abgeschlossen: Version {fromVersion} -> {toVersion}.";

    public static string MigrationRolledBack(int fromVersion, int toVersion)
        => $"Schema-Migration von Version {fromVersion} auf {toVersion} fehlgeschlagen "
           + "und zurueckgerollt. Die Datenbank ist unveraendert.";

    /// <summary>
    /// Ergebnis eines Sicherungslaufs. Der Dateiname ist ein reiner
    /// Zeitstempel (siehe <see cref="BackupFileName"/>) und verraet nichts
    /// ueber den Inhalt.
    /// </summary>
    public static string Backup(BackupResult result)
    {
        var ziel1 = result.Primary switch
        {
            BackupOutcome.Succeeded => $"erstellt ({result.FileName})",
            BackupOutcome.Skipped => "uebersprungen (heute bereits gesichert)",
            BackupOutcome.Failed => $"FEHLGESCHLAGEN: {result.PrimaryError}",
            _ => result.Primary.ToString(),
        };

        var ziel2 = result.External switch
        {
            BackupOutcome.Succeeded => "kopiert",
            BackupOutcome.Skipped => "uebersprungen",
            BackupOutcome.NotConfigured => "nicht eingerichtet",
            BackupOutcome.Failed => $"FEHLGESCHLAGEN: {result.ExternalError}",
            _ => result.External.ToString(),
        };

        return $"Sicherung. Ziel 1: {ziel1}. Ziel 2: {ziel2}.";
    }

    /// <summary>
    /// Erzeugung wiederkehrender Buchungen. Nur die Anzahl - Titel,
    /// Betrag und Bemerkung der erzeugten Buchungen bleiben draussen.
    /// </summary>
    public static string RecurringGenerated(int count, DateOnly asOf)
    {
        var tag = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return count == 1
            ? $"1 wiederkehrende Buchung erzeugt (Stichtag {tag})."
            : $"{count} wiederkehrende Buchungen erzeugt (Stichtag {tag}).";
    }

    public static string DatabaseChecked(int schemaVersion)
        => $"Datenbank geprueft, Schema-Version {schemaVersion}, Integritaetspruefung bestanden.";

    public static string DatabaseCorrupt(string finding)
        => $"Integritaetspruefung der Datenbank NICHT bestanden: {finding}";

    public static string FirstStart()
        => "Erster Start: Grunddaten (eigene Person, Startkategorie) wurden angelegt.";

    public static string SecondInstanceRejected()
        => "Zweiter Programmstart bei laufender Instanz - die vorhandene wurde "
           + "in den Vordergrund geholt, dieser Start endet hier.";
}
