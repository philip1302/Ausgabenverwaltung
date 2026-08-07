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

    /// <summary>Die laufende Fassung als Anzeigetext ("1.4.0").</summary>
    public string VersionText { get; init; } = string.Empty;

    /// <summary>
    /// Der anzuzeigende Text, fertig aufbereitet. Werden mehrere
    /// Fassungen auf einmal nachgeholt, traegt jede ihre Ueberschrift.
    /// </summary>
    public IReadOnlyList<ReleaseNoteBlock> Abschnitte { get; init; } = [];
}

/// <summary>
/// Entscheidet, ob nach einem Austausch der Programmdatei einmalig die
/// Seite "Was ist neu" erscheint, und stellt ihren Inhalt zusammen.
///
/// Der Text kommt aus der eingebetteten Aenderungsliste
/// (<see cref="Changelog"/>) und nicht aus dem Netz. Die Anwendung
/// braucht dafuer keine Verbindung, und der Text gehoert unweigerlich zu
/// der Fassung, die gerade laeuft.
///
/// Gemerkt wird nur EINE Zahl: die zuletzt gesehene Fassung. Aus ihr und
/// der laufenden ergibt sich alles Uebrige - auch der Fall, dass jemand
/// zwei Fassungen auf einmal ueberspringt: dann werden beide Abschnitte
/// gezeigt, jeder mit seiner Ueberschrift.
///
/// Zwei Faelle bleiben bewusst still:
/// - die erste Ausfuehrung ueberhaupt (nichts Gemerktes): wer noch nichts
///   Altes kennt, braucht keine Liste der Aenderungen daran;
/// - eine Fassung ohne eigenen Abschnitt in der Aenderungsliste - eine
///   leere Seite "Was ist neu" ist schlechter als keine.
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
    /// <param name="changelogText">
    /// Die Aenderungsliste, ueblicherweise aus
    /// <see cref="Changelog.Eingebettet"/>.
    /// </param>
    public static WasIstNeuEntscheidung Treffe(
        string? laufendeVersion,
        string? zuletztGesehen,
        string? changelogText)
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

        // Alles, was seit der zuletzt gesehenen Fassung dazugekommen ist -
        // die neueste zuerst. Ein uebersprungener Zwischenstand faellt so
        // nicht unter den Tisch.
        var nachzuholen = Changelog.Lies(changelogText)
            .Where(eintrag => eintrag.Version > gesehen && eintrag.Version <= laufend)
            .ToList();

        if (nachzuholen.Count == 0)
        {
            return Nur(laufendText);
        }

        var abschnitte = new List<ReleaseNoteBlock>();

        foreach (var eintrag in nachzuholen)
        {
            // Die Fassungsueberschrift nur, wenn mehr als eine dabei ist:
            // bei der einen erwarteten Fassung stuende sie doppelt, der
            // Seitentitel nennt sie bereits.
            if (nachzuholen.Count > 1)
            {
                abschnitte.Add(new ReleaseNoteBlock
                {
                    Art = ReleaseNoteArt.Ueberschrift,
                    Text = "Fassung " + eintrag.Titel,
                });
            }

            abschnitte.AddRange(eintrag.Inhalt);
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
