using Ausgabenverwaltung.Core.Categories;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Uebersetzung der Haekchen eines Kategorie-Baums in "diese Aeste"
/// und "diese Aeste nicht" (siehe <see cref="CategoryFilterSelection"/>).
///
/// Der Baum ist ueberall derselbe:
/// <code>
///   Haushalt (1)
///     Lebensmittel (2)
///     Restaurant (3)
///       Lieferdienst (4)
///   Auto (5)
///     Versicherung (6)
///     Sprit (7)
/// </code>
/// </summary>
public class CategoryFilterSelectionTests
{
    private static readonly CategoryParentLink[] Baum =
    [
        new(1, null),   // Haushalt
        new(2, 1),      // Lebensmittel
        new(3, 1),      // Restaurant
        new(4, 3),      // Lieferdienst
        new(5, null),   // Auto
        new(6, 5),      // Versicherung
        new(7, 5),      // Sprit
    ];

    private static CategoryFilterChoice Waehle(params int[] angehakt)
        => CategoryFilterSelection.Derive(Baum, new HashSet<int>(angehakt));

    [Fact]
    public void Gar_nichts_angehakt_schraenkt_nicht_ein()
    {
        // Wichtige Festlegung: kein Haekchen heisst "Kategorie ist mir
        // egal", nicht "zeig mir nichts".
        var wahl = Waehle();

        Assert.Empty(wahl.RootIds);
        Assert.Empty(wahl.ExcludedIds);
    }

    [Fact]
    public void Ein_einzelner_Ast_wird_zur_Wurzel_ohne_Ausschluesse()
    {
        var wahl = Waehle(1, 2, 3, 4);

        Assert.Equal([1], wahl.RootIds);
        Assert.Empty(wahl.ExcludedIds);
    }

    [Fact]
    public void Nur_der_oberste_angehakte_Knoten_eines_Zweiges_wird_Wurzel()
    {
        // Lebensmittel haengt unter Haushalt und ist damit ueber den Ast
        // schon erfasst - eine zweite Wurzel waere ueberfluessig.
        var wahl = Waehle(1, 2);

        Assert.Equal([1], wahl.RootIds);
    }

    [Fact]
    public void Eine_abgewaehlte_Unterkategorie_wird_ausgenommen()
    {
        // Der Fall aus der Anforderung: Oberkategorie an, eine
        // Unterkategorie davon aus.
        var wahl = Waehle(1, 2);

        Assert.Equal([1], wahl.RootIds);
        Assert.Equal([3], wahl.ExcludedIds);
    }

    [Fact]
    public void Unter_einem_ausgenommenen_Knoten_wird_nichts_mehr_aufgezaehlt()
    {
        // Lieferdienst haengt unter dem ausgenommenen Restaurant und faellt
        // ohnehin mit weg - es steht deshalb NICHT in der Liste. Sonst
        // wuechse sie mit jeder Tiefe, ohne etwas hinzuzufuegen.
        var wahl = Waehle(1, 2);

        Assert.Equal([3], wahl.ExcludedIds);
        Assert.DoesNotContain(4, wahl.ExcludedIds);
    }

    [Fact]
    public void Mehrere_Aeste_stehen_nebeneinander()
    {
        var wahl = Waehle(1, 2, 3, 4, 5, 6, 7);

        Assert.Equal([1, 5], wahl.RootIds);
        Assert.Empty(wahl.ExcludedIds);
    }

    [Fact]
    public void Mehrere_Aeste_mit_je_eigenen_Ausschluessen()
    {
        // Das Bild aus der Auswahl: Haushalt ohne Restaurant, Auto ohne
        // Versicherung.
        var wahl = Waehle(1, 2, 5, 7);

        Assert.Equal([1, 5], wahl.RootIds);
        Assert.Equal([3, 6], wahl.ExcludedIds);
    }

    [Fact]
    public void Ein_nicht_angehakter_Wurzelknoten_muss_nicht_ausgenommen_werden()
    {
        // Auto ist gar nicht gewaehlt - es faellt schon dadurch heraus,
        // dass es in keinem Ast steckt. Ein Ausschluss waere Larm ohne
        // Wirkung.
        var wahl = Waehle(1, 2, 3, 4);

        Assert.Equal([1], wahl.RootIds);
        Assert.DoesNotContain(5, wahl.ExcludedIds);
    }

    [Fact]
    public void Nur_eine_Unterkategorie_angehakt_macht_sie_selbst_zur_Wurzel()
    {
        var wahl = Waehle(3, 4);

        Assert.Equal([3], wahl.RootIds);
        Assert.Empty(wahl.ExcludedIds);
    }

    [Fact]
    public void Eine_inzwischen_geloeschte_Kategorie_wird_uebergangen()
    {
        // Die Auswahl kann eine Id enthalten, die es nicht mehr gibt -
        // etwa weil nebenher zusammengefuehrt wurde. Das darf die
        // Auswertung nicht zum Stehen bringen.
        var wahl = CategoryFilterSelection.Derive(Baum, new HashSet<int> { 1, 2, 3, 4, 999 });

        Assert.Equal([1], wahl.RootIds);
        Assert.Empty(wahl.ExcludedIds);
    }
}
