namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Saetze rund um die Selbstaktualisierung.
///
/// Wie ueberall dieselbe Ordnung: WAS ist passiert, WAS bedeutet das fuer
/// die Daten, WAS kann der Anwender tun. Keine Ausnahmenamen, keine
/// Fehlernummern.
///
/// Ein eigener Punkt kommt hier hinzu, den es sonst nirgends gibt: Beim
/// Austausch der PROGRAMMDATEI liegt die Sorge nahe, es koennten dabei
/// Daten verloren gehen. Sie sind unbegruendet - Datenbank, Sicherungen
/// und Einstellungen liegen im Anwendungsdatenordner und nicht neben der
/// Programmdatei (siehe Database.AppPaths). Genau das sagen diese Texte
/// deshalb ausdruecklich, statt es den Anwender vermuten zu lassen.
///
/// Reine Textbildung, deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class UpdateText
{
    /// <summary>
    /// Eine neue Fassung liegt geladen und geprueft bereit. Kein Fehler,
    /// sondern die eigentliche gute Nachricht dieser Funktion.
    /// </summary>
    public static string Bereitgelegt(string version)
    {
        return $"Die neue Fassung {version} wurde geladen und geprüft. "
            + "Sie wird beim nächsten Start der Anwendung übernommen — "
            + "bis dahin läuft alles unverändert weiter. "
            + "Ihre Daten sind davon nicht betroffen: Buchungen, Sicherungen und "
            + "Einstellungen liegen getrennt von der Programmdatei und bleiben, "
            + "wo sie sind. "
            + "Wer nicht warten möchte, startet die Anwendung gleich neu.";
    }

    /// <summary>
    /// Es gibt etwas Neueres, aber es laesst sich nicht selbst
    /// uebernehmen - fehlende Datei fuer diese Plattform, fehlende
    /// Pruefsumme oder ein schreibgeschuetzter Programmordner.
    /// </summary>
    public static string NurHinweis(string version, UpdateHindernis hindernis)
    {
        var kopf = $"Es gibt eine neuere Fassung: {version}.";

        var erklaerung = hindernis switch
        {
            UpdateHindernis.OrdnerSchreibgeschuetzt =>
                "Sie lässt sich hier nicht selbst einspielen, weil in den Ordner der "
                + "Anwendung nicht geschrieben werden darf. Das ist üblich, wenn das "
                + "Programm unter „Programme“ liegt.",

            UpdateHindernis.KeineDateiFuerDiesesSystem =>
                "Für dieses Betriebssystem ist zu dieser Fassung keine fertige Datei "
                + "hinterlegt.",

            UpdateHindernis.OhnePruefsumme =>
                "Sie wird nicht selbst eingespielt, weil sich nicht nachprüfen lässt, "
                + "ob die Datei unterwegs unverändert geblieben ist.",

            _ => "Sie lässt sich hier nicht selbst einspielen.",
        };

        return kopf + " " + erklaerung + "\n\n"
            + "Die Anwendung läuft in der bisherigen Fassung ganz normal weiter, "
            + "es wurde nichts verändert. "
            + "Wer aktualisieren möchte, lädt die neue Fassung von der "
            + "Veröffentlichungsseite und ersetzt die Programmdatei von Hand.";
    }

    /// <summary>
    /// Der Austausch beim Start ist misslungen. Der Anwender merkt davon
    /// im Regelfall nichts (die Anwendung startet ja), aber wenn er es
    /// erfaehrt, soll er wissen, dass nichts kaputt ist.
    /// </summary>
    public static string AustauschGescheitert(string version)
    {
        return $"Die bereitgelegte Fassung {version} konnte nicht übernommen werden "
            + "und wurde verworfen.\n\n"
            + "Die Anwendung läuft in der bisherigen Fassung vollständig weiter, "
            + "und Ihre Daten sind unverändert — Buchungen, Sicherungen und "
            + "Einstellungen werden von einem Austausch der Programmdatei nicht "
            + "berührt. "
            + "Beim nächsten Start wird es erneut versucht. "
            + "Bleibt es dabei, hilft das Herunterladen der neuen Fassung von der "
            + "Veröffentlichungsseite.";
    }
}

/// <summary>
/// Was einem selbsttaetigen Einspielen im Weg steht.
/// </summary>
public enum UpdateHindernis
{
    OrdnerSchreibgeschuetzt,
    KeineDateiFuerDiesesSystem,
    OhnePruefsumme,
}
