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
            + "Ihre Daten sind davon nicht betroffen: Buchungen, Sicherungen und "
            + "Einstellungen liegen getrennt von der Programmdatei und bleiben, "
            + "wo sie sind.\n\n"
            + "„Jetzt neu starten“ übernimmt sie sofort — die Anwendung schließt "
            + "sich und öffnet sich gleich wieder, danach steht einmalig, was sich "
            + "geändert hat. Wer gerade mitten in etwas ist, schließt dieses Band "
            + "einfach: übernommen wird sie dann beim nächsten Start von selbst, "
            + "und bis dahin läuft alles unverändert weiter.";
    }

    /// <summary>
    /// Der Neustart liess sich nicht ausloesen. Wichtig ist hier die
    /// Entwarnung: die geladene Fassung ist NICHT verloren, sie wartet
    /// weiter - der Anwender muss nur selbst schliessen und oeffnen.
    /// </summary>
    public static string NeustartGescheitert()
    {
        return "Die Anwendung ließ sich nicht von selbst neu starten.\n\n"
            + "Die geladene Fassung liegt weiterhin bereit und wird übernommen, "
            + "sobald die Anwendung das nächste Mal gestartet wird — bitte dazu "
            + "einmal schließen und wieder öffnen. Ihre Daten sind unverändert, "
            + "und es ist nichts verloren gegangen.";
    }

    /// <summary>
    /// Es gibt etwas Neueres, aber es laesst sich nicht selbst
    /// uebernehmen - fehlende Datei fuer diese Plattform, fehlende
    /// Pruefsumme oder ein schreibgeschuetzter Programmordner.
    /// </summary>
    /// <param name="dateiname">
    /// Die Datei, die auf der Veroeffentlichungsseite fuer dieses System
    /// gilt (siehe <see cref="Updates.UpdatePlatform.AssetName"/>).
    /// <c>null</c>, wenn es fuer dieses System gar keine gibt - dann
    /// entfaellt die Anleitung, denn es waere nichts zu holen.
    /// </param>
    /// <param name="istBundle">
    /// Unter macOS wird kein Programm<i>datei</i> ersetzt, sondern ein
    /// ganzes Bundle. Der Unterschied gehoert in die Anleitung, sonst
    /// sucht dort jemand nach einer .exe.
    /// </param>
    public static string NurHinweis(
        string version, UpdateHindernis hindernis, string? dateiname = null, bool istBundle = false)
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

        var zustand = "Die Anwendung läuft in der bisherigen Fassung ganz normal "
            + "weiter, es wurde nichts verändert.";

        // Ohne Datei fuer dieses System gibt es nichts anzuleiten - eine
        // Schrittfolge, an deren Ende nichts steht, ist schlimmer als
        // keine.
        if (dateiname is null)
        {
            return kopf + " " + erklaerung + "\n\n" + zustand;
        }

        var was = istBundle ? "das Programm" : "die Programmdatei";
        var wohin = istBundle
            ? "in den Programme-Ordner ziehen und das dortige ersetzen"
            : "in den Ordner der Anwendung kopieren und die dortige ersetzen";

        // Nummerierte Schritte statt eines Satzes, und mit dem KONKRETEN
        // Dateinamen: "die passende Datei" laesst genau die Frage offen,
        // die auf einer Seite mit drei Dateien im Weg steht.
        return kopf + " " + erklaerung + "\n\n" + zustand + "\n\n"
            + "Von Hand geht es so:\n"
            + "1. Anwendung schließen.\n"
            + $"2. Auf der Veröffentlichungsseite „{dateiname}“ herunterladen "
            + "und entpacken.\n"
            + $"3. {char.ToUpperInvariant(was[0]) + was[1..]} daraus {wohin}.\n"
            + "4. Anwendung wieder starten.";
    }

    /// <summary>
    /// Die Einleitung ueber der Liste der Neuerungen - die einzige Stelle,
    /// an der ein Austausch der Programmdatei ueberhaupt sichtbar wird.
    /// Deshalb steht auch hier ausdruecklich, was mit den Daten ist:
    /// zwischen "das Programm ist ein anderes" und "meine Buchungen sind
    /// noch da" liegt genau die Frage, die sich sonst niemand beantwortet.
    /// </summary>
    public static string WasIstNeuEinleitung(string version)
    {
        return $"Die Anwendung läuft jetzt in der Fassung {version}. "
            + "Darunter steht, was sich gegenüber der bisherigen Fassung geändert hat. "
            + "Ihre Daten sind davon nicht betroffen: Buchungen, Sicherungen und "
            + "Einstellungen liegen getrennt von der Programmdatei und sind unverändert. "
            + "Diese Seite erscheint einmalig; weiter geht es mit „Weiter zur Startseite“.";
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
