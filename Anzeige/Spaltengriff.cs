using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Der Griff auf der Trennlinie hinter der Kategoriespalte: waagerecht
/// ziehen macht die Spalte breiter oder schmaler, loslassen merkt die
/// Breite dauerhaft.
///
/// Er steht in der Abstandsspalte der KOPFZEILE und nicht in jeder Zeile -
/// gezogen wird an der Spalte, nicht an einer Buchung. Weil alle Raster
/// derselben Ansicht dieselbe Breite lesen (siehe
/// <see cref="Spaltenbreiten"/> und <see cref="Raster"/>), wandern die
/// Zeilen darunter beim Ziehen mit.
///
/// Ein eigener Thumb und kein GridSplitter: der GridSplitter veraendert
/// die Spalten SEINES Rasters, und Kopfzeile und Zeilen sind hier je
/// eigene Raster. Ueber die gemeinsame Quelle wirkt der Zug dagegen
/// ueberall gleichzeitig.
/// </summary>
public sealed class Spaltengriff : Thumb
{
    protected override void OnDragDelta(VectorEventArgs e)
    {
        base.OnDragDelta(e);

        // Gezogen wird in der angezeigten Groesse, gemerkt wird die Breite
        // fuer die Stufe "Normal" - deshalb der Weg zurueck ueber den
        // Faktor. Sonst spraenge die Spalte bei einer anderen
        // Schriftgroesse auf ein Vielfaches des Gezogenen.
        var faktor = Skalierung.Aktuell.Faktor;
        if (faktor <= 0)
        {
            return;
        }

        Spaltenbreiten.Aktuell.SetzeKategorie(
            Spaltenbreiten.Aktuell.Kategorie + (e.Vector.X / faktor));
    }

    protected override void OnDragCompleted(VectorEventArgs e)
    {
        base.OnDragCompleted(e);

        Spaltenbreiten.Aktuell.SichereKategorie();
    }
}
