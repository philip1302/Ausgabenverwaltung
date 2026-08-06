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

    // ================= Schreibende Datenbankzugriffe =================
    //
    // Jede erfolgreiche Erstellung/Aenderung/Loeschung bekommt hier einen
    // Eintrag - ausschliesslich Ids und Anzahlen, nie Betraege, Namen oder
    // Bemerkungen (Regel 11). Lesende Zugriffe (Listen laden, Reports,
    // Filter) werden bewusst NICHT protokolliert: die passieren bei jedem
    // Bildschirmwechsel und wuerden das Protokoll unlesbar aufblaehen -
    // das widerspraeche "das Protokoll ist teilbar" (Regel 11).

    // ---------------- Kategorien ----------------
    public static string CategoryCreated(int id, int? parentId)
        => parentId is int p
            ? $"Kategorie angelegt (Id {id}, Unterkategorie von Id {p})."
            : $"Kategorie angelegt (Id {id}, Oberkategorie).";

    public static string CategoryRenamed(int id)
        => $"Kategorie umbenannt (Id {id}).";

    public static string CategoryArchived(int id, int descendantCount)
        => descendantCount > 0
            ? $"Kategorie archiviert (Id {id}, inkl. {descendantCount} Unterkategorien)."
            : $"Kategorie archiviert (Id {id}).";

    public static string CategoryRestored(int id)
        => $"Kategorie wiederhergestellt (Id {id}).";

    public static string CategoryDeleted(int id)
        => $"Kategorie geloescht (Id {id}).";

    public static string CategoryMerged(int sourceId, int targetId, int expenseCount, int recurringExpenseCount)
        => $"Kategorien zusammengefuehrt: Id {sourceId} -> Id {targetId} "
           + $"({expenseCount} Ausgabe(n), {recurringExpenseCount} Vorlage(n) umgehaengt).";

    public static string CategoryColorChanged(int id)
        => $"Kategoriefarbe geaendert (Id {id}).";

    public static string CategoryMoved(int id, int delta)
        => $"Kategorie verschoben (Id {id}, {(delta < 0 ? "nach oben" : "nach unten")}).";

    // ---------------- Ausgaben ----------------
    public static string ExpenseCreated(int id, bool isIncome)
        => $"{(isIncome ? "Einnahme" : "Ausgabe")} erfasst (Id {id}).";

    public static string ExpenseUpdated(int id)
        => $"Ausgabe geaendert (Id {id}).";

    public static string ExpenseDeleted(int id)
        => $"Ausgabe geloescht (Id {id}).";

    public static string ExpensesDeleted(int count)
        => count == 1 ? "1 Ausgabe geloescht." : $"{count} Ausgaben geloescht.";

    // ---------------- Offene Posten ----------------
    public static string OpenItemSettled(int id, bool settled)
        => settled
            ? $"Posten als beglichen markiert (Id {id})."
            : $"Begleichung eines Postens zurueckgenommen (Id {id}).";

    // ---------------- Personen ----------------
    public static string PersonCreated(int id)
        => $"Person angelegt (Id {id}).";

    public static string PersonRenamed(int id)
        => $"Person umbenannt (Id {id}).";

    public static string PersonArchived(int id)
        => $"Person archiviert (Id {id}).";

    public static string PersonRestored(int id)
        => $"Person wiederhergestellt (Id {id}).";

    public static string PersonMoved(int id, int delta)
        => $"Person verschoben (Id {id}, {(delta < 0 ? "nach oben" : "nach unten")}).";

    // ---------------- Wiederkehrende Ausgaben ----------------
    public static string RecurringExpenseCreated(int id)
        => $"Wiederkehrende Ausgabe angelegt (Id {id}).";

    public static string RecurringExpenseUpdated(int id)
        => $"Wiederkehrende Ausgabe geaendert (Id {id}).";

    public static string RecurringExpenseDeactivated(int id)
        => $"Wiederkehrende Ausgabe deaktiviert (Id {id}).";

    public static string RecurringExpenseActivated(int id)
        => $"Wiederkehrende Ausgabe aktiviert (Id {id}).";

    public static string RecurringExpenseDeleted(int id)
        => $"Wiederkehrende Ausgabe geloescht (Id {id}).";

    public static string RecurringExpenseGeneratedThroughSet(int id, DateOnly through)
        => $"Wiederkehrende Ausgabe (Id {id}): Stand ohne Erzeugung auf "
           + $"{through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} gesetzt.";

    /// <summary>Wie <see cref="RecurringGenerated"/>, aber fuer den Lauf EINER
    /// einzelnen Vorlage (Anlegen, Aendern, "Jetzt erzeugen" je Zeile).</summary>
    public static string RecurringGeneratedForTemplate(int templateId, int count, DateOnly asOf)
    {
        var tag = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return count == 1
            ? $"1 Buchung aus wiederkehrender Ausgabe Id {templateId} erzeugt (Stichtag {tag})."
            : $"{count} Buchungen aus wiederkehrender Ausgabe Id {templateId} erzeugt (Stichtag {tag}).";
    }

    // ================= Selbstaktualisierung =================
    //
    // Hier stehen Versionsangaben, Groessen und Dateinamen - also
    // dasselbe, was Regel 11 ohnehin erlaubt. Ein Update sagt nichts
    // ueber die Daten des Anwenders aus, deshalb ist an diesen Eintraegen
    // nichts zu schwaerzen.

    public static string UpdateAbgeschaltet()
        => "Suche nach neuer Fassung uebersprungen - in den Einstellungen abgeschaltet.";

    public static string UpdateOrdnerSchreibgeschuetzt(string ordnerPfad)
        => $"Suche nach neuer Fassung uebersprungen - der Programmordner ist "
           + $"schreibgeschuetzt: {ordnerPfad}";

    public static string UpdateAktuell(string version)
        => $"Nach neuer Fassung gesehen: {version} ist der neueste Stand.";

    public static string UpdateGefunden(string version)
        => $"Neue Fassung gefunden: {version}.";

    public static string UpdateNichtGefunden(UpdateGrundText grund)
        => $"Nach neuer Fassung gesehen, ohne Ergebnis: {Beschreibe(grund)}";

    public static string UpdateBereitgelegt(string version, long groesseBytes)
        => $"Fassung {version} geladen und bereitgelegt "
           + $"({groesseBytes / (1024 * 1024)} MB) - wird beim naechsten Start uebernommen.";

    public static string UpdatePruefsummeAbweichend(string version)
        => $"Fassung {version} verworfen: die Pruefsumme der geladenen Datei weicht "
           + "von der angegebenen ab.";

    public static string UpdateVerworfen(string version)
        => $"Vorbereitete Fassung {version} verworfen - sie hielt der Pruefung "
           + "unmittelbar vor dem Austausch nicht stand.";

    public static string UpdateUebernommen(string version)
        => $"Fassung {version} uebernommen, die Anwendung startet neu.";

    /// <summary>
    /// Warum nichts gefunden wurde - als eigener Aufzaehlungstyp, damit
    /// der Protokolltext nicht aus dem Aufrufer hereingereicht wird und
    /// hier an einer Stelle nachlesbar bleibt, was das Protokoll
    /// preisgibt (Regel 11).
    /// </summary>
    public enum UpdateGrundText
    {
        NichtsGefunden,
        KeinPassendesAsset,
        OhnePruefsumme,
        PlattformOhneVeroeffentlichung,
    }

    private static string Beschreibe(UpdateGrundText grund) => grund switch
    {
        UpdateGrundText.NichtsGefunden =>
            "die Liste der Veroeffentlichungen war nicht abzurufen oder leer.",
        UpdateGrundText.KeinPassendesAsset =>
            "die neuere Veroeffentlichung bringt keine Datei fuer diese Plattform mit.",
        UpdateGrundText.OhnePruefsumme =>
            "die Datei traegt keine Pruefsumme; ohne sie wird nicht ausgetauscht.",
        UpdateGrundText.PlattformOhneVeroeffentlichung =>
            "fuer diese Plattform wird nichts veroeffentlicht.",
        _ => "Grund unbekannt.",
    };
}
