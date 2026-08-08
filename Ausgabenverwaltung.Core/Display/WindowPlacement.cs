namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Lage und Groesse des Hauptfensters, wie der Anwender sie zuletzt
/// hinterlassen hat.
///
/// <b>Zwei Einheiten in einem Typ</b>, und das ist Absicht, weil die
/// Oberflaeche sie so liefert:
/// <list type="bullet">
/// <item><see cref="Left"/>/<see cref="Top"/> sind PHYSISCHE
/// Bildschirmpixel. Nur in dieser Einheit lassen sie sich mit den
/// Arbeitsflaechen der Bildschirme vergleichen - und genau dieser
/// Vergleich ist die Pruefung beim Start
/// (<see cref="WindowPlacements.IsOnScreen"/>).</item>
/// <item><see cref="Width"/>/<see cref="Height"/> sind
/// GERAETEUNABHAENGIGE Punkte. Auf einem Bildschirm mit 150 % Skalierung
/// ist ein 1200 Punkte breites Fenster 1800 Pixel breit.</item>
/// </list>
/// Wer beides in dieselbe Einheit rechnen wollte, braeuchte die
/// Skalierung des Bildschirms, auf dem das Fenster gerade steht - und die
/// kann beim naechsten Start eine andere sein. Deshalb bleiben die
/// Einheiten getrennt, und die Pruefung betrifft ausschliesslich die Lage.
/// </summary>
public sealed record WindowPlacement
{
    /// <summary>Linke Kante in physischen Pixeln. Darf negativ sein: ein
    /// zweiter Bildschirm links vom Hauptbildschirm hat negative
    /// Koordinaten, und ein Fenster dort ist voellig in Ordnung.</summary>
    public required int Left { get; init; }

    /// <summary>Obere Kante in physischen Pixeln. Darf ebenfalls negativ
    /// sein (Bildschirm oberhalb des Hauptbildschirms).</summary>
    public required int Top { get; init; }

    /// <summary>Breite in geraeteunabhaengigen Punkten.</summary>
    public required double Width { get; init; }

    /// <summary>Hoehe in geraeteunabhaengigen Punkten.</summary>
    public required double Height { get; init; }

    /// <summary>
    /// Ob das Fenster maximiert war. Getrennt gemerkt, weil ein maximiertes
    /// Fenster seine Lage und Groesse gar nicht verraet: dort stehen die
    /// Masse des Vollbilds. Gespeichert wird deshalb die letzte Lage im
    /// Normalzustand PLUS dieses Merkmal - sonst bekaeme der Anwender beim
    /// naechsten Start ein bildschirmgrosses, aber nicht maximiertes
    /// Fenster, das sich beim Wiederherstellen nicht verkleinert.
    /// </summary>
    public bool IsMaximized { get; init; }
}

/// <summary>
/// Die nutzbare Flaeche eines Bildschirms in physischen Pixeln - der
/// Ausschnitt ohne Taskleiste.
///
/// Eigener Typ und nicht der der Oberflaechen-Baugruppe, damit Core ohne
/// sie auskommt (CLAUDE.md: Core enthaelt KEINEN UI-Code). Die Umrechnung
/// macht die Anzeige, die Entscheidung faellt hier - so ist sie pruefbar
/// (Regel 7).
/// </summary>
public readonly record struct ScreenArea(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    /// <summary>
    /// Liegt dieser Punkt auf der Flaeche? Rechte und untere Kante zaehlen
    /// nicht mehr dazu - ein Punkt genau auf der rechten Kante gehoert
    /// bereits zum Nachbarbildschirm.
    /// </summary>
    public bool Contains(int x, int y)
        => x >= Left && x < Right && y >= Top && y < Bottom;
}
