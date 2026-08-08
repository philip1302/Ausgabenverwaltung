namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Die Regeln zu einer gemerkten Fensterlage: was noch brauchbar ist und
/// was nicht.
///
/// Der Grund fuer diese Klasse ist der eine Fall, der ein gemerktes
/// Fenster gefaehrlich macht: Der Anwender arbeitet am Schreibtisch mit
/// zwei Bildschirmen, schiebt das Fenster auf den zweiten und faehrt dann
/// mit dem Notebook allein weiter. Die gemerkte Lage zeigt jetzt ins
/// Nichts. Ohne Pruefung geht das Fenster dort auf, wo kein Bildschirm
/// mehr ist - unsichtbar, unerreichbar, und fuer den Anwender ist die
/// Anwendung einfach kaputt. Er kann sie nicht einmal zurueckholen, denn
/// dafuer muesste er die Titelzeile greifen koennen.
///
/// Deshalb entscheidet nicht die Oberflaeche, sondern diese Klasse - sie
/// ist pruefbar (Regel 7), und die Faelle, um die es geht, lassen sich in
/// Wirklichkeit nur mit einem zweiten Bildschirm herstellen.
/// </summary>
public static class WindowPlacements
{
    /// <summary>
    /// Schmalstes Fenster, das gemerkt wird. Darunter stehen Seitenleiste
    /// und Inhalt nicht mehr sinnvoll nebeneinander. Die Grenze schuetzt
    /// nicht vor dem Anwender - so klein laesst sich das Fenster ohnehin
    /// kaum ziehen - sondern vor einer von Hand veraenderten
    /// Einstellungsdatei und vor einem Wert, der bei einem Absturz
    /// unvollstaendig geschrieben wurde.
    /// </summary>
    public const double MinWidth = 800;

    /// <summary>Niedrigstes Fenster, das gemerkt wird.</summary>
    public const double MinHeight = 560;

    /// <summary>
    /// Obergrenze gegen Unsinn. Kein Bildschirm ist so gross; ein
    /// groesserer Wert kann nur aus einer verdorbenen Datei stammen.
    /// </summary>
    public const double MaxSize = 20000;

    /// <summary>
    /// Wie weit in das Fenster hinein gemessen wird, um zu entscheiden, ob
    /// es erreichbar ist - in physischen Pixeln.
    ///
    /// Geprueft wird bewusst EIN Punkt kurz hinter der linken oberen Ecke
    /// und nicht, ob das Fenster irgendwie mit einem Bildschirm
    /// ueberlappt. Der Unterschied zaehlt: ein Fenster, von dem nur der
    /// rechte untere Zipfel auf den Bildschirm ragt, ueberlappt zwar - hat
    /// aber keine greifbare Titelzeile und ist damit genauso verloren wie
    /// eines, das ganz daneben liegt. Der Punkt hier liegt dort, wo die
    /// Titelzeile ist.
    /// </summary>
    public const int GrabInset = 24;

    /// <summary>
    /// Bringt eine gelesene Lage in brauchbare Groessen oder verwirft sie.
    /// Laeuft ueber jeden gelesenen Wert, damit weder eine von Hand
    /// veraenderte Einstellungsdatei noch ein halb geschriebener Wert ein
    /// Fenster hinterlassen kann, das niemand mehr benutzen kann.
    ///
    /// <c>null</c> heisst "nichts Brauchbares gemerkt" - dann startet das
    /// Fenster wie beim allerersten Mal. Die LAGE wird hier nicht geprueft,
    /// dafuer braucht es die Bildschirme (siehe <see cref="IsOnScreen"/>).
    /// </summary>
    public static WindowPlacement? Normalize(WindowPlacement? placement)
    {
        if (placement is null)
        {
            return null;
        }

        // Nicht darstellbare Zahlen kommen aus einer verdorbenen Datei. Sie
        // zu beschneiden waere Rateraten - eine solche Lage gilt als nicht
        // vorhanden.
        if (!double.IsFinite(placement.Width) || !double.IsFinite(placement.Height))
        {
            return null;
        }

        return placement with
        {
            Width = Math.Round(Math.Clamp(placement.Width, MinWidth, MaxSize)),
            Height = Math.Round(Math.Clamp(placement.Height, MinHeight, MaxSize)),
        };
    }

    /// <summary>
    /// Ist das Fenster an dieser Lage noch erreichbar? Geprueft wird, ob
    /// ein Punkt kurz hinter der linken oberen Ecke auf der Arbeitsflaeche
    /// eines vorhandenen Bildschirms liegt (siehe
    /// <see cref="GrabInset"/>).
    ///
    /// Eine LEERE Bildschirmliste ergibt <c>false</c>: dann liess sich
    /// nicht feststellen, welche Bildschirme es gibt, und im Zweifel ist
    /// ein zentriert geoeffnetes Fenster immer richtig, ein unsichtbares
    /// nie.
    ///
    /// Negative Koordinaten sind ausdruecklich kein Ausschlussgrund. Ein
    /// Bildschirm links vom Hauptbildschirm hat negative X-Werte, und ein
    /// Fenster dort ist voellig in Ordnung - eine Pruefung auf
    /// "grosser oder gleich null" waere der naheliegende und falsche Weg.
    /// </summary>
    public static bool IsOnScreen(
        WindowPlacement placement, IReadOnlyList<ScreenArea> screens)
    {
        var x = placement.Left + GrabInset;
        var y = placement.Top + GrabInset;

        foreach (var screen in screens)
        {
            if (screen.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }
}
