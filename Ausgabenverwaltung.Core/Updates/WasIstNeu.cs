namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Das Ergebnis der Frage "hat sich die Fassung geaendert, und gibt es
/// dazu etwas zu erzaehlen?".
/// </summary>
public sealed record WasIstNeuEntscheidung
{
    /// <summary>Ob die Seite "Was ist neu" beim Start erscheinen soll.</summary>
    public required bool Zeigen { get; init; }

    /// <summary>
    /// Welche Fassung als gesehen zu merken ist (siehe
    /// <c>AppSettings.LastSeenVersion</c>). <c>null</c> heisst: es gibt
    /// nichts zu merken, der gespeicherte Stand passt schon.
    ///
    /// Gemerkt wird auch dann, wenn NICHTS gezeigt wird - sonst stuende
    /// bei der naechsten Aktualisierung ein uralter Stand da und die Seite
    /// zaehlte Aenderungen auf, die der Anwender laengst benutzt.
    /// </summary>
    public string? MerkeVersion { get; init; }

    /// <summary>Die laufende Fassung als Anzeigetext ("1.2.0").</summary>
    public string VersionText { get; init; } = string.Empty;

    /// <summary>Die aufbereiteten Neuerungen, leer wenn nichts gezeigt wird.</summary>
    public IReadOnlyList<ReleaseNoteBlock> Abschnitte { get; init; } = [];
}

/// <summary>
/// Entscheidet, ob nach einem Austausch der Programmdatei einmalig die
/// Seite "Was ist neu" erscheint.
///
/// Die Anwendung merkt sich dafuer zweierlei in den Einstellungen: die
/// zuletzt gesehene Fassung, und den Beschreibungstext der
/// Veroeffentlichung, die beim letzten Lauf geladen und bereitgelegt
/// wurde (siehe ViewModels/AktualisierungViewModel). Der Text wird also
/// VOR dem Neustart abgelegt und NACH dem Neustart gezeigt - im neuen
/// Prozess ist kein Netzzugriff mehr noetig, und die Seite steht sofort
/// da, auch wenn GitHub gerade nicht erreichbar ist.
///
/// Gezeigt wird nur, was auch etwas hergibt. Drei Faelle bleiben deshalb
/// bewusst still:
/// - die erste Ausfuehrung ueberhaupt (nichts Gemerktes): wer noch nichts
///   Altes kennt, braucht keine Liste der Aenderungen daran;
/// - eine von Hand ausgetauschte Programmdatei: dann liegt kein Text vor,
///   und eine leere Seite "Was ist neu" ist schlechter als keine;
/// - ein Text, der zu einer anderen Fassung gehoert als der laufenden.
///
/// Entscheidet allein aus Werten - ohne Netz, ohne Platte, ohne Uhr - und
/// ist deshalb vollstaendig pruefbar (Regel 7).
/// </summary>
public static class WasIstNeu
{
    /// <param name="laufendeVersion">
    /// Die Informationsversion der laufenden Baugruppe.
    /// </param>
    /// <param name="zuletztGesehen">
    /// Die zuletzt gemerkte Fassung, <c>null</c> bei der ersten
    /// Ausfuehrung.
    /// </param>
    /// <param name="notizenVersion">
    /// Zu welcher Fassung der abgelegte Beschreibungstext gehoert.
    /// </param>
    /// <param name="notizen">Der abgelegte Beschreibungstext selbst.</param>
    public static WasIstNeuEntscheidung Treffe(
        string? laufendeVersion,
        string? zuletztGesehen,
        string? notizenVersion,
        string? notizen)
    {
        // Ohne lesbare eigene Version laesst sich nichts vergleichen -
        // dann wird auch nichts gemerkt, sonst stuende hinterher ein
        // Unsinn in den Einstellungen.
        if (!AppVersion.TryParse(laufendeVersion, out var laufend))
        {
            return new WasIstNeuEntscheidung { Zeigen = false };
        }

        var laufendText = laufend.ToString();

        // Erste Ausfuehrung (oder eine Einstellungsdatei, deren Eintrag
        // sich nicht lesen laesst): nur merken.
        if (!AppVersion.TryParse(zuletztGesehen, out var gesehen))
        {
            return Nur(laufendText);
        }

        if (laufend <= gesehen)
        {
            // Gleichstand ist der Normalfall bei jedem gewoehnlichen
            // Start - dann gibt es auch nichts zu schreiben. Nur bei einem
            // Rueckschritt auf eine aeltere Fassung wird der Stand
            // nachgezogen, damit ein spaeteres Wiederhochgehen die Seite
            // erneut zeigt.
            return laufend == gesehen
                ? new WasIstNeuEntscheidung { Zeigen = false, VersionText = laufendText }
                : Nur(laufendText);
        }

        // Ab hier ist die laufende Fassung echt neuer als die zuletzt
        // gesehene. Gezeigt wird trotzdem nur mit passendem Text.
        if (!AppVersion.TryParse(notizenVersion, out var notizV) || notizV != laufend)
        {
            return Nur(laufendText);
        }

        var abschnitte = ReleaseNotes.Lies(notizen);
        if (abschnitte.Count == 0)
        {
            return Nur(laufendText);
        }

        return new WasIstNeuEntscheidung
        {
            Zeigen = true,
            MerkeVersion = laufendText,
            VersionText = laufendText,
            Abschnitte = abschnitte,
        };
    }

    // Nichts zu zeigen, aber der Stand gehoert nachgezogen.
    private static WasIstNeuEntscheidung Nur(string laufendText)
        => new()
        {
            Zeigen = false,
            MerkeVersion = laufendText,
            VersionText = laufendText,
        };
}
