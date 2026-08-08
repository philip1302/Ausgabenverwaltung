using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Logging;
using Avalonia;
using Avalonia.Controls;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Merkt sich Lage und Groesse des Fensters ueber das Programmende hinaus
/// und stellt sie beim naechsten Start wieder her.
///
/// Hier steht nur die Mechanik: Ereignisse abhoeren, Werte aus dem Fenster
/// lesen, Werte hineinschreiben. Ob eine gemerkte Lage ueberhaupt noch
/// brauchbar ist, entscheidet <see cref="WindowPlacements"/> in Core -
/// diese Frage ist pruefbar, und die Faelle, um die es geht (Bildschirm
/// abgezogen, Aufloesung geaendert), liessen sich hier nicht herstellen.
///
/// <b>Warum die Lage mitgeschrieben und nicht erst beim Schliessen gelesen
/// wird:</b> Ein maximiertes Fenster verraet seine normale Lage nicht mehr.
/// <c>Position</c> und <c>Width</c>/<c>Height</c> tragen dann die Masse des
/// Vollbilds. Wer erst beim Schliessen liest, merkt sich also ein
/// bildschirmgrosses Fenster - und der Anwender, der maximiert gearbeitet
/// hat, bekommt beim naechsten Start ein Fenster, das gross ist, sich aber
/// nicht wiederherstellen laesst, weil es gar nicht maximiert ist. Deshalb
/// wird der Normalzustand fortlaufend nachgehalten und das Maximiert-Sein
/// getrennt gemerkt.
/// </summary>
public static class Fensterzustand
{
    /// <summary>
    /// Stellt die gemerkte Lage her (falls es eine brauchbare gibt) und
    /// sorgt dafuer, dass die aktuelle beim Schliessen gesichert wird.
    ///
    /// Muss aufgerufen werden, BEVOR das Fenster gezeigt wird: eine erst
    /// danach gesetzte Lage laesst es sichtbar springen.
    /// </summary>
    /// <param name="sichern">
    /// Wird beim Schliessen mit der zu merkenden Lage gerufen. Das
    /// Schreiben selbst gehoert nicht hierher - es haengt an den
    /// Einstellungen, und die kennt die Anwendung an einer Stelle
    /// (App.axaml.cs).
    /// </param>
    public static void Verbinde(
        Window fenster, WindowPlacement? gemerkt, Action<WindowPlacement> sichern)
    {
        // Der Normalzustand, fortlaufend nachgehalten. Anfangswert ist die
        // gemerkte Lage: schliesst der Anwender das Fenster, ohne es je
        // bewegt zu haben, soll dieselbe Lage wieder herauskommen und nicht
        // eine, die aus einem maximierten Fenster abgelesen wurde.
        var normal = gemerkt;

        Stelleher(fenster, gemerkt);

        // Beide Ereignisse fuehren zum selben Nachhalten. Gefiltert wird auf
        // den Normalzustand: im maximierten Fenster stehen dort die Masse
        // des Vollbilds, und die wuerden den gemerkten Wert ueberschreiben.
        fenster.PositionChanged += (_, _) => normal = LiesNormal(fenster) ?? normal;
        fenster.SizeChanged += (_, _) => normal = LiesNormal(fenster) ?? normal;

        // Beim Schliessen und nicht beim Beenden der Anwendung: hier steht
        // das Fenster noch, und nur von ihm sind die Werte zu bekommen.
        fenster.Closing += (_, _) =>
        {
            // Ausdruecklich still im Fehlerfall (siehe unten in Sichere):
            // eine nicht gemerkte Fenstergroesse ist ein Aergernis, ein
            // Fehlerdialog beim Schliessen der Anwendung ein Hindernis.
            if (normal is not { } lage)
            {
                return;
            }

            sichern(lage with { IsMaximized = fenster.WindowState == WindowState.Maximized });
        };
    }

    /// <summary>
    /// Die Lage aus dem Fenster - oder <c>null</c>, wenn sie gerade nichts
    /// aussagt (maximiert, zum Symbol verkleinert, oder noch ohne
    /// gemessene Groesse).
    /// </summary>
    private static WindowPlacement? LiesNormal(Window fenster)
    {
        if (fenster.WindowState != WindowState.Normal)
        {
            return null;
        }

        // Gelesen wird Bounds, gesetzt wird Width/Height (siehe
        // Stelleher). Beides ist in Avalonia dieselbe Groesse - die des
        // Innenbereichs, nicht die des Rahmens (fuer den gibt es
        // FrameSize, das hier bewusst nicht vorkommt). Nachgemessen, weil
        // ein Unterschied von der Rahmenbreite bedeutet haette, dass das
        // Fenster bei JEDEM Start ein paar Pixel schrumpft - ein Fehler,
        // der erst nach Wochen auffaellt.
        var breite = fenster.Bounds.Width;
        var hoehe = fenster.Bounds.Height;

        // Vor dem ersten Messen sind beide 0. Ein Fenster ohne Groesse zu
        // merken hiesse, die vorhandene gemerkte Lage wegzuwerfen.
        if (breite <= 0 || hoehe <= 0)
        {
            return null;
        }

        return new WindowPlacement
        {
            Left = fenster.Position.X,
            Top = fenster.Position.Y,
            Width = breite,
            Height = hoehe,
        };
    }

    private static void Stelleher(Window fenster, WindowPlacement? gemerkt)
    {
        if (WindowPlacements.Normalize(gemerkt) is not { } lage)
        {
            // Noch nichts gemerkt: alles bleibt, wie es die Ansicht
            // vorgibt - genau das Verhalten der Fassungen davor.
            return;
        }

        fenster.Width = lage.Width;
        fenster.Height = lage.Height;

        if (WindowPlacements.IsOnScreen(lage, Bildschirme(fenster)))
        {
            fenster.Position = new PixelPoint(lage.Left, lage.Top);
        }
        else
        {
            // Die Lage zeigt ins Nichts - etwa weil der zweite Bildschirm
            // nicht mehr angeschlossen ist. Zentriert oeffnen und die Lage
            // gar nicht setzen; eine Position waere hier schlimmer als
            // keine, denn ein unsichtbares Fenster laesst sich nicht
            // zurueckholen.
            fenster.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            AppLog.Current.Info(LogEvents.WindowPlacementDiscarded(lage.Left, lage.Top));
        }

        if (lage.IsMaximized)
        {
            fenster.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// Die Arbeitsflaechen der vorhandenen Bildschirme, in die Form
    /// gebracht, die Core versteht. Eine leere Liste heisst "nicht
    /// feststellbar" - <see cref="WindowPlacements.IsOnScreen"/> antwortet
    /// darauf mit "nein", und das Fenster geht zentriert auf.
    /// </summary>
    private static IReadOnlyList<ScreenArea> Bildschirme(Window fenster)
    {
        try
        {
            if (fenster.Screens is not { All: { } alle })
            {
                return [];
            }

            return alle
                .Select(schirm => new ScreenArea(
                    schirm.WorkingArea.X,
                    schirm.WorkingArea.Y,
                    schirm.WorkingArea.Width,
                    schirm.WorkingArea.Height))
                .ToList();
        }
        catch (Exception ex)
        {
            // Die Bildschirmliste kommt vom Betriebssystem. Laesst sie sich
            // nicht holen, ist das kein Grund, den Start abzubrechen - das
            // Fenster geht dann zentriert auf.
            AppLog.Current.Exception("Beim Ermitteln der Bildschirme", ex);
            return [];
        }
    }
}
