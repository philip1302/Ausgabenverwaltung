using System.Globalization;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Die Saetze zum Wiederherstellen einer Sicherung.
///
/// Der wichtigste davon ist <see cref="Folgen"/>: er beziffert, was das
/// Einspielen kostet. "Diese Sicherung wiederherstellen?" ist keine Frage,
/// die jemand beantworten kann - "94 Buchungen waeren danach weg" ist eine.
/// Deshalb steht die Zahl im Text und nicht bloss der Dateiname.
///
/// Reine Textbildung ohne Dateizugriff, damit sie pruefbar bleibt
/// (Regel 7); <c>MeldungsGrundsaetzeTests</c> haelt sie gegen dieselben
/// Grundsaetze wie alle uebrigen Texte.
/// </summary>
public static class RestoreText
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Was das Einspielen dieser Sicherung bedeutet - der Text ueber der
    /// Eingabebestaetigung.
    /// </summary>
    /// <param name="fileName">Die gewaehlte Sicherung.</param>
    /// <param name="activeCount">Buchungen in der aktiven Datenbank.</param>
    /// <param name="backupCount">Buchungen in der Sicherung.</param>
    public static string Folgen(string fileName, int activeCount, int backupCount)
    {
        var unterschied = activeCount - backupCount;

        // Der Vergleich ist bewusst nur eine ANZAHL und keine Aufzaehlung
        // dessen, was fehlt: die Sicherung ist ein anderer Stand, nicht eine
        // Teilmenge des heutigen. Gleiche Anzahl heisst deshalb auch nicht
        // gleicher Inhalt - und genau das sagt der dritte Satz.
        var vergleich = unterschied switch
        {
            > 0 => $"Die aktive Datenbank enthält {Zahl(activeCount)} "
                   + $"{Buchungen(activeCount)}, diese Sicherung "
                   + $"{Zahl(backupCount)} — {Zahl(unterschied)} "
                   + $"{Buchungen(unterschied)} {Waeren(unterschied)} danach weg.",

            < 0 => $"Die aktive Datenbank enthält {Zahl(activeCount)} "
                   + $"{Buchungen(activeCount)}, diese Sicherung "
                   + $"{Zahl(backupCount)} — die Sicherung ist also die "
                   + "umfangreichere von beiden.",

            _ => $"Beide enthalten {Zahl(activeCount)} {Buchungen(activeCount)}. "
                 + "Gleiche Anzahl heißt aber nicht gleicher Inhalt: geändert, "
                 + "verschoben und umbenannt kann trotzdem alles sein.",
        };

        return vergleich
               + $"\n\nEingespielt wird „{fileName}“. Die bisherige Datenbank wird "
               + "vorher vollständig unter einem eigenen Namen gesichert und bleibt "
               + "im Sicherungsordner liegen — der Weg zurück ist also offen. "
               + "Die Anwendung startet danach neu.";
    }

    /// <summary>
    /// Das Band nach dem Bereitlegen: was jetzt passiert und was noch
    /// fehlt. Bewusst kein "Erledigt" - erledigt ist es erst nach dem
    /// Neustart.
    /// </summary>
    public static string Bereitgelegt(string fileName, string safetyCopyName)
        => $"Die Sicherung „{fileName}“ liegt bereit und wird beim nächsten Start "
           + "eingespielt. Die bisherige Datenbank ist vollständig gesichert und "
           + $"liegt als „{safetyCopyName}“ im Sicherungsordner. Bis zum Neustart "
           + "arbeitet die Anwendung unverändert mit den bisherigen Daten — erst "
           + "danach gilt der Stand aus der Sicherung.";

    /// <summary>
    /// Die Meldung beim ersten Start nach einer Uebernahme. Sie steht auf
    /// der Seite, von der aus die Wiederherstellung angestossen wurde -
    /// dort sieht nach, wer wissen will, ob es geklappt hat.
    /// </summary>
    public static string Uebernommen(string fileName, string safetyCopyName)
        => $"Die Sicherung „{fileName}“ wurde eingespielt. Die Anwendung arbeitet "
           + "ab jetzt mit diesem Stand. Die Datenbank von vorher ist vollständig "
           + $"erhalten und liegt als „{safetyCopyName}“ im Sicherungsordner; wer "
           + "sich vertan hat, spielt sie genauso wieder ein.";

    /// <summary>
    /// Die Meldung, wenn beim Start eine bereitliegende Wiederherstellung
    /// verworfen werden musste. Sie sagt ausdruecklich, dass nichts ersetzt
    /// wurde - sonst bliebe die bange Frage, auf welchem Stand die
    /// Anwendung gerade laeuft.
    /// </summary>
    public static string Verworfen()
        => "Eine bereitliegende Wiederherstellung ließ sich beim Start nicht "
           + "einspielen und wurde verworfen — die Datei war unvollständig oder "
           + "nicht mehr lesbar. Die Anwendung läuft unverändert mit den bisherigen "
           + "Daten weiter, es wurde nichts ersetzt. Der Versuch lässt sich unten "
           + "wiederholen; bitte die Sicherung vorher prüfen.";

    /// <summary>
    /// Was in das Feld der Eingabebestaetigung gehoert - die Aufforderung
    /// nennt den Namen, damit niemand ihn suchen muss.
    /// </summary>
    public static string Abtippen(string fileName)
        => $"Zum Bestätigen bitte den Dateinamen abtippen: {fileName}";

    // Tausenderpunkt, damit "1.284" nicht als "1284" ueberflogen wird -
    // bei einer Zahl, die eine Entscheidung traegt, zaehlt jede Stelle.
    private static string Zahl(int anzahl) => anzahl.ToString("N0", DeDe);

    private static string Buchungen(int anzahl) => anzahl == 1 ? "Buchung" : "Buchungen";

    private static string Waeren(int anzahl) => anzahl == 1 ? "wäre" : "wären";
}
