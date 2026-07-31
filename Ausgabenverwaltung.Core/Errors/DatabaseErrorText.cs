namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Verstaendliche Saetze zu Problemen mit der Datenbank - beim Start und
/// im laufenden Betrieb.
///
/// Der wichtigste Satz in jedem dieser Texte ist der ueber den Zustand der
/// Daten. Wer eine Fehlermeldung ueber "die Datenbank" liest, denkt an
/// verlorene Eingaben; dass nichts verloren ist, muss deshalb dastehen und
/// darf nicht zwischen den Zeilen vermutet werden.
///
/// Reine Textbildung, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class DatabaseErrorText
{
    /// <summary>
    /// Die Datei ist gesperrt. Weitaus haeufigster Grund: die Anwendung
    /// laeuft bereits - deshalb steht der Verdacht vorn und nicht am Ende
    /// einer Aufzaehlung von Moeglichkeiten.
    /// </summary>
    public static string Locked(string databaseFilePath)
        => "Die Datei mit den erfassten Ausgaben lässt sich nicht öffnen, weil sie "
         + "gerade von einem anderen Programm benutzt wird.\n\n"
         + "Am wahrscheinlichsten läuft die Ausgabenverwaltung bereits — sehen Sie in "
         + "der Taskleiste nach, ob dort schon ein Fenster offen ist. Möglich ist auch, "
         + "dass ein Sicherungsprogramm die Datei gerade liest oder dass ein früherer "
         + "Programmlauf noch nicht vollständig beendet ist.\n\n"
         + "Ihre Daten sind unverändert. Bitte schließen Sie das andere Fenster oder "
         + "warten Sie einen Moment, und starten Sie die Anwendung dann erneut.\n\n"
         + $"Betroffene Datei:\n{databaseFilePath}";

    /// <summary>
    /// Die Integritaetspruefung ist durchgefallen. Hier wird bewusst NICHT
    /// weitergearbeitet: mit einer beschaedigten Datei weiterzuschreiben
    /// vergroessert den Schaden und ueberschreibt womoeglich noch das,
    /// was zu retten waere.
    /// </summary>
    public static string Corrupt(string databaseFilePath, string backupFolderPath)
        => "Die Datei mit den erfassten Ausgaben ist beschädigt. Die Anwendung startet "
         + "deshalb nicht weiter — mit beschädigten Daten weiterzuarbeiten würde den "
         + "Schaden nur vergrößern.\n\n"
         + "Das kommt vor, wenn der Rechner während des Schreibens ausgeschaltet wurde, "
         + "bei einem Defekt des Datenträgers oder wenn die Datei von außen verändert "
         + "wurde.\n\n"
         + "Was das für Ihre Daten bedeutet: Die zuletzt angelegte Sicherung ist davon "
         + "nicht betroffen — sie liegt unverändert im Sicherungsordner und lässt sich "
         + "zurückholen. Verloren ist höchstens das, was seit dieser Sicherung erfasst "
         + "wurde.\n\n"
         + "So kommen Sie weiter:\n\n"
         + "1. Öffnen Sie den Sicherungsordner (Schaltfläche unten).\n"
         + "2. Wählen Sie die neueste ZIP-Datei und entpacken Sie sie. "
         + "Darin liegt eine Datei namens „ausgaben.db“.\n"
         + "3. Benennen Sie die beschädigte Datei um, statt sie zu löschen — "
         + "vielleicht lässt sich später doch noch etwas daraus lesen.\n"
         + "4. Kopieren Sie die entpackte Datei an deren Stelle.\n"
         + "5. Starten Sie die Anwendung erneut.\n\n"
         + $"Beschädigte Datei:\n{databaseFilePath}\n\n"
         + $"Sicherungsordner:\n{backupFolderPath}";

    /// <summary>
    /// Die Umstellung des Datenbankaufbaus ist gescheitert und wurde
    /// zurueckgerollt. Der Verweis auf die Sicherung steht trotzdem dabei:
    /// er kostet nichts und beantwortet die naechste Frage, die sich
    /// jemand in dieser Lage stellt.
    /// </summary>
    public static string MigrationFailed(
        int fromVersion, int toVersion, string backupFolderPath, string? backupFileName)
    {
        var sicherung = backupFileName is null
            ? $"Im Sicherungsordner liegt der Stand von vor dem Versuch:\n{backupFolderPath}"
            : $"Der Stand von unmittelbar vor dem Versuch liegt als „{backupFileName}“ "
              + $"im Sicherungsordner:\n{backupFolderPath}";

        return "Beim Aktualisieren der Datenbank auf den Aufbau dieser Programmversion "
             + "ist ein Fehler aufgetreten. Die Umstellung wurde vollständig "
             + "zurückgenommen.\n\n"
             + "Ihre Daten sind unverändert — es steht alles so darin wie vor dem "
             + "Versuch. Die Anwendung startet aber nicht weiter, weil sie mit dem "
             + "alten Aufbau nicht arbeiten kann.\n\n"
             + "Bitte starten Sie die Anwendung noch einmal. Bleibt es dabei, hilft ein "
             + "Blick ins Protokoll (Schaltfläche unten) weiter.\n\n"
             + sicherung + "\n\n"
             + $"Aufbau der Datenbank: Version {fromVersion}, erwartet wird Version {toVersion}.";
    }

    /// <summary>
    /// Die Datei gehoert zu einer neueren Programmversion. Das ist kein
    /// Schaden, sondern eine Reihenfolge - der Text sagt das auch so.
    /// </summary>
    public static string SchemaTooNew(int actualVersion, int expectedVersion)
        => "Die Datei mit den erfassten Ausgaben stammt aus einer neueren Version "
         + "dieser Anwendung. Diese ältere Version kann sie nicht öffnen, ohne Daten "
         + "zu verlieren, die sie nicht kennt.\n\n"
         + "Ihre Daten sind unverändert und vollständig.\n\n"
         + "Bitte installieren Sie wieder die neuere Version der Ausgabenverwaltung. "
         + "Danach lässt sich die Datei wie gewohnt öffnen.\n\n"
         + $"Aufbau der Datei: Version {actualVersion}. "
         + $"Diese Programmversion kennt bis Version {expectedVersion}.";

    /// <summary>
    /// Die Datei ist zwar lesbar, aber ihr Aufbau ist nicht zu bestimmen -
    /// etwa eine leere oder halb angelegte Datei aus einem abgebrochenen
    /// ersten Start. Wuerde das nicht hier abgefangen, scheiterte
    /// stattdessen der erste Zugriff irgendwo mitten in der Anwendung.
    /// </summary>
    public static string SchemaUnreadable(string databaseFilePath, string backupFolderPath)
        => "Die Datei mit den erfassten Ausgaben ist unvollständig: Es lässt sich nicht "
         + "feststellen, nach welchem Aufbau sie angelegt wurde.\n\n"
         + "Das passiert, wenn ein früherer Start mittendrin abgebrochen wurde, etwa "
         + "durch einen Stromausfall.\n\n"
         + "Die Anwendung startet nicht weiter, um die Datei nicht weiter zu "
         + "beschädigen. Liegt im Sicherungsordner ein brauchbarer Stand, lässt sich "
         + "die Datei daraus ersetzen (Schaltfläche unten). Ist die Datei dagegen ganz "
         + "neu und enthält noch nichts, können Sie sie auch einfach löschen — dann "
         + "legt die Anwendung beim nächsten Start eine frische an.\n\n"
         + $"Betroffene Datei:\n{databaseFilePath}\n\n"
         + $"Sicherungsordner:\n{backupFolderPath}";

    /// <summary>
    /// Ein Schreibfehler im laufenden Betrieb. Der zweite Absatz ist der
    /// wichtige: die Eingaben des Anwenders stehen noch im Formular, und
    /// er soll es gar nicht erst schliessen.
    /// </summary>
    public static string WriteFailed(StorageProblem problem)
    {
        var erklaerung = problem switch
        {
            StorageProblem.DiskFull =>
                "Auf dem Datenträger ist kein Platz mehr frei. "
                + "Bitte schaffen Sie etwas Platz und versuchen Sie es dann erneut.",

            StorageProblem.AccessDenied =>
                "Die Anwendung darf nicht mehr in die Datei schreiben. "
                + "Möglicherweise haben sich die Berechtigungen geändert oder ein "
                + "Virenschutz greift ein.",

            StorageProblem.PathNotFound =>
                "Die Datei ist nicht mehr erreichbar. Möglicherweise wurde ein "
                + "Wechseldatenträger abgezogen oder ein Netzlaufwerk getrennt. "
                + "Bitte stellen Sie die Verbindung wieder her.",

            StorageProblem.ReadOnly =>
                "Der Datenträger ist schreibgeschützt.",

            StorageProblem.DatabaseLocked =>
                "Die Datei wird gerade von einem anderen Programm benutzt. "
                + "Läuft die Ausgabenverwaltung vielleicht ein zweites Mal? "
                + "Bitte versuchen Sie es gleich noch einmal.",

            StorageProblem.DatabaseCorrupt =>
                "Die Datei ist beschädigt. Bitte beenden Sie die Anwendung und "
                + "stellen Sie den letzten gesicherten Stand wieder her.",

            _ =>
                "Der Grund lässt sich nicht genauer bestimmen.",
        };

        return "Die Änderung konnte nicht gespeichert werden. " + erklaerung + "\n\n"
             + "Der Vorgang wurde vollständig zurückgenommen — in der Datei steht "
             + "alles so wie vorher, halb Gespeichertes gibt es nicht. "
             + "Ihre Eingaben stehen weiterhin im Formular und gehen nicht verloren. "
             + "Sobald die Ursache behoben ist, genügt ein erneutes Speichern.";
    }
}
