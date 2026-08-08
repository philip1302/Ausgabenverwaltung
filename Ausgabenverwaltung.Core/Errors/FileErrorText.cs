namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Verstaendliche Saetze zu Schreibfehlern im Dateisystem: CSV-Export,
/// Sicherung, zweites Sicherungsziel.
///
/// Jeder Text folgt derselben Ordnung: WAS ist passiert, WAS bedeutet das
/// fuer die Daten, WAS kann der Anwender tun. Keine Ausnahmenamen, keine
/// Fehlernummern - die gehoeren in den aufklappbaren Bereich des Dialogs
/// und ins Protokoll, nicht in den Haupttext.
///
/// Reine Textbildung, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class FileErrorText
{
    /// <summary>
    /// Der CSV-Export. Der haeufigste Fall ist mit Abstand die noch in
    /// Excel geoeffnete Zieldatei - er bekommt deshalb einen eigenen
    /// Hinweis statt einer allgemeinen Meldung ueber gesperrte Dateien.
    /// </summary>
    public static string ForCsvExport(StorageProblem problem, string fileName)
    {
        var kopf = $"Die Datei „{fileName}“ konnte nicht geschrieben werden.";

        var erklaerung = problem switch
        {
            StorageProblem.FileInUse =>
                "Sie ist gerade in einem anderen Programm geöffnet — meistens in Excel. "
                + "Bitte dort schließen und den Export noch einmal starten. "
                + "Alternativ lässt sich ein anderer Dateiname wählen.",

            StorageProblem.AccessDenied =>
                "In diesen Ordner darf nicht geschrieben werden. "
                + "Bitte einen anderen Speicherort wählen, zum Beispiel den eigenen "
                + "Dokumentenordner.",

            StorageProblem.DiskFull =>
                "Auf dem Datenträger ist kein Platz mehr frei. "
                + "Bitte Platz schaffen oder einen anderen Speicherort wählen.",

            StorageProblem.PathNotFound =>
                "Der Ordner ist nicht erreichbar. Möglicherweise wurde ein "
                + "Wechseldatenträger abgezogen oder ein Netzlaufwerk getrennt. "
                + "Bitte einen anderen Speicherort wählen.",

            StorageProblem.ReadOnly =>
                "Der Datenträger ist schreibgeschützt. "
                + "Bitte einen anderen Speicherort wählen.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen. "
                + "Bitte einen anderen Speicherort wählen und es noch einmal versuchen.",
        };

        // Die Auswertung selbst ist unberuehrt - das ist die Frage, die
        // sich der Anwender in diesem Moment stellt.
        return $"{kopf} {erklaerung}\n\n"
             + "Es wurde nichts verändert; die Auswertung steht unverändert am Bildschirm.";
    }

    /// <summary>
    /// Das feste erste Sicherungsziel. Hier steht mehr auf dem Spiel als
    /// beim Export: schlaegt es fehl, gibt es fuer diesen Tag ueberhaupt
    /// keine Sicherung, und das muss deutlich werden.
    /// </summary>
    public static string ForBackup(StorageProblem problem)
    {
        var erklaerung = problem switch
        {
            StorageProblem.DiskFull =>
                "Auf dem Datenträger ist kein Platz mehr frei. "
                + "Bitte Platz schaffen und die Sicherung erneut starten.",

            StorageProblem.AccessDenied =>
                "In den Sicherungsordner darf nicht geschrieben werden. "
                + "Möglicherweise greift ein Virenschutz ein oder die Berechtigungen "
                + "des Ordners haben sich geändert.",

            StorageProblem.PathNotFound =>
                "Der Sicherungsordner ist nicht erreichbar.",

            StorageProblem.ReadOnly =>
                "Der Datenträger ist schreibgeschützt.",

            StorageProblem.FileInUse =>
                "Eine Datei im Sicherungsordner ist von einem anderen Programm "
                + "geöffnet. Bitte dieses schließen und die Sicherung erneut starten.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return "Die Datensicherung konnte nicht angelegt werden. " + erklaerung + "\n\n"
             + "Die erfassten Daten sind davon nicht betroffen und unverändert. "
             + "Es fehlt aber die Sicherung — bis sie gelingt, gibt es für heute "
             + "keinen Stand, auf den sich zurückgehen ließe.";
    }

    /// <summary>
    /// Das zweite, selbst gewaehlte Sicherungsziel. Bewusst ruhiger im Ton
    /// als <see cref="ForBackup"/>: Ziel 1 hat funktioniert, es fehlt nur
    /// die zweite Kopie.
    /// </summary>
    public static string ForExternalBackup(StorageProblem problem, string folderPath)
    {
        var erklaerung = problem switch
        {
            StorageProblem.PathNotFound =>
                "Der Ordner ist nicht erreichbar. Vermutlich ist der Datenträger "
                + "abgezogen oder das Netzlaufwerk gerade nicht verbunden.",

            StorageProblem.AccessDenied =>
                "In den Ordner darf nicht geschrieben werden.",

            StorageProblem.DiskFull =>
                "Auf dem Datenträger ist kein Platz mehr frei.",

            StorageProblem.ReadOnly =>
                "Der Datenträger ist schreibgeschützt.",

            StorageProblem.FileInUse =>
                "Eine Datei im Zielordner ist von einem anderen Programm geöffnet.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return $"Die zusätzliche Kopie nach „{folderPath}“ ist nicht angekommen. "
             + erklaerung + "\n\n"
             + "Die Sicherung auf diesem Rechner ist trotzdem angelegt worden — "
             + "es fehlt nur die zweite Kopie. Sobald das Ziel wieder erreichbar ist, "
             + "wird beim nächsten Lauf von selbst wieder dorthin kopiert.";
    }

    /// <summary>
    /// Die Pruefung einer vorhandenen Sicherungsdatei. Sie sagt nichts
    /// ueber die aktiven Daten aus - das muss der Text ausdruecklich
    /// klarstellen, sonst liest sich "nicht lesbar" wie ein Schaden an
    /// der laufenden Datenbank.
    /// </summary>
    public static string ForBackupVerification(StorageProblem problem, string fileName)
    {
        var erklaerung = problem switch
        {
            StorageProblem.DatabaseCorrupt =>
                "Die Datei lässt sich zwar öffnen, enthält aber keine vollständige, "
                + "unbeschädigte Datenbank. Zum Zurückspielen taugt sie nicht.",

            StorageProblem.PathNotFound =>
                "Die Datei ist nicht mehr erreichbar. Möglicherweise wurde ein "
                + "Wechseldatenträger abgezogen oder ein Netzlaufwerk getrennt.",

            StorageProblem.AccessDenied =>
                "Die Datei darf nicht gelesen werden.",

            StorageProblem.FileInUse =>
                "Die Datei ist von einem anderen Programm geöffnet. Bitte dieses "
                + "schließen und die Prüfung noch einmal starten.",

            StorageProblem.DiskFull =>
                "Zum Prüfen wird die Sicherung kurz entpackt, und dafür ist auf dem "
                + "Datenträger kein Platz mehr frei. Bitte Platz schaffen und die "
                + "Prüfung noch einmal starten.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return $"Die Sicherung „{fileName}“ konnte nicht geprüft werden. " + erklaerung + "\n\n"
             + "Die erfassten Daten sind davon nicht betroffen und unverändert — geprüft "
             + "wurde nur eine Kopie. Bitte prüfen Sie eine ältere Sicherung; findet sich "
             + "keine brauchbare, hilft „Jetzt sichern“ zu einem frischen Stand.";
    }

    /// <summary>
    /// Eine Sicherung, die zwar lesbar ist, aber aus einer neueren
    /// Programmversion stammt. Kein Fehler an der Datei - sie taugt nur
    /// nicht zum Zurueckspielen mit dieser Fassung, und das sieht man ihr
    /// von aussen nicht an.
    /// </summary>
    public static string ForBackupFromNewerVersion(
        string fileName, int schemaVersion, int expectedVersion)
    {
        return $"Die Sicherung „{fileName}“ ist lesbar, stammt aber aus einer neueren "
             + $"Programmversion (Stand {schemaVersion} statt {expectedVersion}).\n\n"
             + "Die erfassten Daten sind davon nicht betroffen und unverändert. "
             + "Zurückspielen ließe sich diese Sicherung mit der laufenden Fassung "
             + "aber nicht — dafür müsste erst die neuere Programmversion installiert "
             + "werden.";
    }

    /// <summary>
    /// Die Schreibprobe beim Auswaehlen des zweiten Ziels. Der Anwender
    /// steht hier gerade im Ordnerdialog und kann sofort etwas anderes
    /// waehlen - der Text bleibt deshalb kurz.
    /// </summary>
    public static string ForBackupTargetChoice(StorageProblem problem)
    {
        var erklaerung = problem switch
        {
            StorageProblem.AccessDenied =>
                "In diesen Ordner darf nicht geschrieben werden.",

            StorageProblem.PathNotFound =>
                "Dieser Ordner ist nicht erreichbar.",

            StorageProblem.DiskFull =>
                "Auf diesem Datenträger ist kein Platz mehr frei.",

            StorageProblem.ReadOnly =>
                "Dieser Datenträger ist schreibgeschützt.",

            _ =>
                "In diesen Ordner ließ sich nicht testweise schreiben.",
        };

        return erklaerung + "\n\n"
             + "Der Ordner wurde deshalb nicht als zweites Ziel übernommen. "
             + "Das bisherige Ziel gilt unverändert weiter.";
    }

    /// <summary>
    /// Die Sicherheitskopie vor einer Wiederherstellung ist nicht
    /// entstanden. Der ernstere der beiden Fehlschlaege, weil er den
    /// Rueckweg betrifft - deshalb wird auch gar nichts vorbereitet, und
    /// der Text muss sagen, dass das die richtige Entscheidung war.
    /// </summary>
    public static string ForRestoreSafetyCopy(StorageProblem problem)
    {
        var erklaerung = problem switch
        {
            StorageProblem.DiskFull =>
                "Für die Kopie ist auf dem Datenträger kein Platz mehr frei. "
                + "Bitte Platz schaffen und es noch einmal versuchen.",

            StorageProblem.AccessDenied or StorageProblem.ReadOnly =>
                "In den Sicherungsordner darf nicht geschrieben werden.",

            StorageProblem.PathNotFound =>
                "Der Sicherungsordner ist nicht erreichbar.",

            StorageProblem.FileInUse =>
                "Eine Datei im Sicherungsordner ist von einem anderen Programm "
                + "geöffnet. Bitte dieses schließen und es noch einmal versuchen.",

            StorageProblem.DatabaseLocked =>
                "Die laufende Datenbank ließ sich für die Kopie nicht lesen, weil "
                + "gerade etwas anderes darauf schreibt. Bitte einen Augenblick "
                + "warten und es noch einmal versuchen.",

            StorageProblem.DatabaseCorrupt =>
                "Beim Lesen der laufenden Datenbank sind Schäden aufgefallen, "
                + "deshalb ließ sich keine Kopie anlegen.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return "Vor dem Einspielen wird die bisherige Datenbank gesichert, und "
             + "genau das ist nicht gelungen. " + erklaerung + "\n\n"
             + "Es wurde deshalb nichts vorbereitet und nichts ersetzt: die "
             + "erfassten Daten sind unverändert. Ohne Weg zurück wird keine "
             + "Sicherung eingespielt.";
    }

    /// <summary>
    /// Das Bereitlegen der gewaehlten Sicherung ist gescheitert. Die
    /// Sicherheitskopie stand zu diesem Zeitpunkt schon - das darf im Text
    /// nicht fehlen, sonst klingt es nach einem halb vollzogenen Austausch.
    /// </summary>
    public static string ForRestore(StorageProblem problem, string fileName)
    {
        var erklaerung = problem switch
        {
            StorageProblem.DiskFull =>
                "Die Sicherung muss zum Einspielen entpackt werden, und dafür ist "
                + "auf dem Datenträger kein Platz mehr frei. Bitte Platz schaffen "
                + "und es noch einmal versuchen.",

            StorageProblem.AccessDenied or StorageProblem.ReadOnly =>
                "In den Ordner der Datenbank darf nicht geschrieben werden.",

            StorageProblem.PathNotFound =>
                "Die Sicherung ist nicht mehr erreichbar. Möglicherweise wurde ein "
                + "Wechseldatenträger abgezogen oder ein Netzlaufwerk getrennt.",

            StorageProblem.FileInUse =>
                "Die Sicherung ist von einem anderen Programm geöffnet. Bitte "
                + "dieses schließen und es noch einmal versuchen.",

            StorageProblem.DatabaseCorrupt =>
                "Die Sicherung enthält keine brauchbare Datenbankdatei. Bitte eine "
                + "andere Sicherung wählen und diese vorher prüfen.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return $"Die Sicherung „{fileName}“ ließ sich nicht zum Einspielen "
             + "bereitlegen. " + erklaerung + "\n\n"
             + "Ersetzt wurde nichts: die erfassten Daten sind unverändert, und die "
             + "Anwendung startet auch nicht neu. Die Sicherheitskopie der bisherigen "
             + "Datenbank ist trotzdem angelegt worden und liegt im Sicherungsordner.";
    }
}
