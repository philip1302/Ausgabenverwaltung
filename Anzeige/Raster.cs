using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Mitwachsendes Spaltenraster fuer die Tabellen:
/// <c>anzeige:Raster.Spalten="32,24,90,16,3*"</c> statt
/// <c>ColumnDefinitions="32,24,90,16,3*"</c>.
///
/// Die Angabe ist dieselbe wie bisher und bleibt in der Ansicht lesbar -
/// sie gilt fuer die Stufe "Normal". Feste Breiten werden mit dem
/// aktuellen Faktor multipliziert, Stern- und Auto-Spalten bleiben, wie
/// sie sind: sie richten sich ohnehin nach dem, was uebrig bleibt.
///
/// Ein eigener angehaengter Wert und keine Bindung an
/// Grid.ColumnDefinitions, weil das in Avalonia eine gewoehnliche
/// Eigenschaft ohne Bindungsunterstuetzung ist.
///
/// Kopfzeile und Zeilen tragen dasselbe Muster; weil beide durch
/// dieselbe Rechnung laufen und auf ganze Pixel gerundet werden, fallen
/// ihre Spaltengrenzen auf jeder Stufe exakt aufeinander.
///
/// Neben Zahlen, Stern und "Auto" kennt das Muster die Marke
/// <c>Kategorie</c>: sie steht fuer die von Hand einstellbare Breite der
/// Kategoriespalte (siehe <see cref="Spaltenbreiten"/>). Sie ist eine
/// feste Breite wie jede andere - nur eine, die der Benutzer am
/// Spaltengriff der Kopfzeile verschieben kann. Ein Muster aus lauter
/// festen und anteiligen Breiten ist der Grund, warum kein einzelner
/// Zellinhalt die Spalten dahinter nach rechts schieben kann.
/// </summary>
public static class Raster
{
    /// <summary>Marke im Muster fuer die einstellbare Kategoriespalte.</summary>
    private const string KategorieMarke = "Kategorie";

    /// <summary>Das Muster, wie es in der Ansicht steht.</summary>
    public static readonly AttachedProperty<string?> SpaltenProperty =
        AvaloniaProperty.RegisterAttached<Grid, string?>("Spalten", typeof(Raster));

    // Der aktuelle Faktor, hier hineingebunden. Angehaengter Wert statt
    // eines eigenen Ereignisses, weil Avalonia die Bindung an die
    // Singleton-Quelle schwach haelt - ein aus der Liste geworfenes
    // Zeilenraster haengt also nicht an der Skalierung fest.
    private static readonly AttachedProperty<double> FaktorProperty =
        AvaloniaProperty.RegisterAttached<Grid, double>("Faktor", typeof(Raster));

    // Dasselbe fuer die gezogene Kategoriebreite: waehrend des Ziehens
    // laufen die Raster aller sichtbaren Zeilen darueber neu, und die
    // Kuerzung der Kategoriepfade passt sich dabei Schritt fuer Schritt an.
    private static readonly AttachedProperty<double> KategoriebreiteProperty =
        AvaloniaProperty.RegisterAttached<Grid, double>("Kategoriebreite", typeof(Raster));

    static Raster()
    {
        SpaltenProperty.Changed.AddClassHandler<Grid>((grid, _) => UebernimmMuster(grid));
        FaktorProperty.Changed.AddClassHandler<Grid>((grid, _) => Anwenden(grid));
        KategoriebreiteProperty.Changed.AddClassHandler<Grid>((grid, _) => Anwenden(grid));
    }

    public static void SetSpalten(Grid grid, string? muster) =>
        grid.SetValue(SpaltenProperty, muster);

    public static string? GetSpalten(Grid grid) => grid.GetValue(SpaltenProperty);

    private static void UebernimmMuster(Grid grid)
    {
        grid.Bind(FaktorProperty, new Binding
        {
            Source = Skalierung.Aktuell,
            Path = nameof(Skalierung.Faktor),
        });

        grid.Bind(KategoriebreiteProperty, new Binding
        {
            Source = Spaltenbreiten.Aktuell,
            Path = nameof(Spaltenbreiten.KategorieSkaliert),
        });

        // Die Bindung liefert sofort einen Wert; aendert der sich dabei
        // nicht (weil das Raster eines wiederverwendeten Zeilenbehaelters
        // neu gesetzt wurde), bliebe es sonst beim alten Muster.
        Anwenden(grid);
    }

    private static void Anwenden(Grid grid)
    {
        if (grid.GetValue(SpaltenProperty) is not { Length: > 0 } muster)
        {
            return;
        }

        var faktor = grid.GetValue(FaktorProperty);
        if (faktor <= 0)
        {
            faktor = Skalierung.Aktuell.Faktor;
        }

        // Die gezogene Breite kommt fertig skaliert aus derselben Quelle,
        // aus der auch die Mindestbreite der Tabelle stammt - Kopfzeile,
        // Zeilen und Bildlauf rechnen dadurch mit demselben Wert.
        var kategorie = Spaltenbreiten.Aktuell.KategorieSkaliert;

        var spalten = new ColumnDefinitions();
        foreach (var teil in muster.Split(','))
        {
            spalten.Add(new ColumnDefinition(Breite(teil.Trim(), faktor, kategorie)));
        }

        grid.ColumnDefinitions = spalten;

        // Ausdruecklich neu vermessen lassen: das Zuweisen der Spalten
        // reicht als Ausloeser nicht in jedem Fall, und ohne neue
        // Messung bliebe die Tabelle nach dem Umschalten der
        // Schriftgroesse in den alten Breiten stehen.
        grid.InvalidateMeasure();
    }

    private static GridLength Breite(string angabe, double faktor, double kategorie)
    {
        if (angabe.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return GridLength.Auto;
        }

        // Schon skaliert - der Faktor steckt bereits in der Quelle.
        if (angabe.Equals(KategorieMarke, StringComparison.OrdinalIgnoreCase))
        {
            return new GridLength(kategorie);
        }

        if (angabe.EndsWith('*'))
        {
            var anteil = angabe[..^1];
            var wert = anteil.Length == 0
                ? 1.0
                : double.Parse(anteil, CultureInfo.InvariantCulture);

            return new GridLength(wert, GridUnitType.Star);
        }

        // Feste Breite: auf ganze Pixel gerundet, damit Kopfzeile und
        // Zeile nicht unterschiedlich runden.
        var pixel = double.Parse(angabe, CultureInfo.InvariantCulture);
        return new GridLength(Math.Round(pixel * faktor));
    }
}
