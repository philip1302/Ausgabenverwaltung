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
}
