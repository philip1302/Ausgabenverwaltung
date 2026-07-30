using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;

namespace Ausgabenverwaltung.Tests;

public class CategoryColorsTests
{
    private const string Blau = "#2980B9";
    private const string Gruen = "#2E9E5B";

    // Baut einen Knoten ohne Datenbank - die Vererbung ist reine
    // Baumlogik und wird genau so geprueft.
    private static CategoryNode Knoten(int id, string name, string? farbe, params CategoryNode[] kinder)
    {
        var knoten = new CategoryNode(new Category
        {
            Id = id,
            Name = name,
            Color = farbe,
        });

        knoten.Children.AddRange(kinder);
        return knoten;
    }

    [Fact]
    public void Farbe_wird_ueber_drei_Ebenen_vererbt()
    {
        var baum = new[]
        {
            Knoten(1, "Pferde", Blau,
                Knoten(2, "Versorgung", null,
                    Knoten(3, "Hufschmied", null))),
        };

        var farben = CategoryColors.Resolve(baum);

        Assert.Equal(Blau, farben[1]);
        Assert.Equal(Blau, farben[2]);
        Assert.Equal(Blau, farben[3]);
    }

    [Fact]
    public void Eine_eigene_Farbe_ueberschreibt_die_geerbte_fuer_den_ganzen_Ast()
    {
        var baum = new[]
        {
            Knoten(1, "Pferde", Blau,
                Knoten(2, "Versorgung", Gruen,
                    Knoten(3, "Hufschmied", null))),
        };

        var farben = CategoryColors.Resolve(baum);

        Assert.Equal(Blau, farben[1]);
        Assert.Equal(Gruen, farben[2]);
        Assert.Equal(Gruen, farben[3]);
    }

    [Fact]
    public void Ohne_jede_Farbe_im_Baum_gilt_der_Standardwert()
    {
        var baum = new[]
        {
            Knoten(1, "Pferde", null,
                Knoten(2, "Versorgung", null,
                    Knoten(3, "Hufschmied", null))),
            Knoten(4, "Wohnen", null),
        };

        var farben = CategoryColors.Resolve(baum);

        Assert.All(farben.Values, farbe => Assert.Equal(CategoryColorPalette.DefaultHex, farbe));
        Assert.Equal(4, farben.Count);
    }

    [Fact]
    public void Geschwisteraeste_erben_unabhaengig_voneinander()
    {
        var baum = new[]
        {
            Knoten(1, "Pferde", Blau, Knoten(2, "Hufschmied", null)),
            Knoten(3, "Wohnen", null, Knoten(4, "Heizkosten", null)),
        };

        var farben = CategoryColors.Resolve(baum);

        Assert.Equal(Blau, farben[2]);
        Assert.Equal(CategoryColorPalette.DefaultHex, farben[4]);
    }

    [Fact]
    public void Ein_Wert_ausserhalb_der_Palette_gilt_als_keine_Farbe()
    {
        var baum = new[]
        {
            Knoten(1, "Pferde", Blau,
                Knoten(2, "Hufschmied", "#ABCDEF")),
        };

        var farben = CategoryColors.Resolve(baum);

        Assert.Equal(Blau, farben[2]);
    }

    [Fact]
    public void Die_Palette_ist_vierzehn_verschiedene_gueltige_Farben()
    {
        var palette = CategoryColorPalette.Colors;

        Assert.Equal(14, palette.Count);
        Assert.Equal(14, palette.Select(farbe => farbe.Hex.ToUpperInvariant()).Distinct().Count());
        Assert.Equal(14, palette.Select(farbe => farbe.Name).Distinct().Count());

        // Format '#RRGGBB' - alles andere waere fuer die Anzeige unbrauchbar.
        Assert.All(palette, farbe =>
        {
            Assert.Equal(7, farbe.Hex.Length);
            Assert.StartsWith("#", farbe.Hex);
            Assert.True(int.TryParse(
                farbe.Hex[1..],
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out _));
        });

        // Der Standardwert ist bewusst KEINE waehlbare Farbe: er steht
        // fuer "keine gewaehlt" und darf nicht als Auswahl erscheinen.
        Assert.False(CategoryColorPalette.IsKnown(CategoryColorPalette.DefaultHex));
    }

    [Fact]
    public void Bekannte_Farben_werden_unabhaengig_von_der_Schreibweise_erkannt()
    {
        Assert.True(CategoryColorPalette.IsKnown(Blau));
        Assert.True(CategoryColorPalette.IsKnown(Blau.ToLowerInvariant()));
        Assert.False(CategoryColorPalette.IsKnown(null));
        Assert.False(CategoryColorPalette.IsKnown("blau"));
    }
}
