using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Tastenkuerzel stehen an genau EINER Stelle
/// (<see cref="Tastenkuerzel.Alle"/>): daraus bauen sich sowohl die
/// Bindungen im Hauptfenster und in den Ansichten als auch die
/// Uebersichtsseite (F1).
///
/// Genau das wird hier geprueft. Eine Hilfeseite, die ihren eigenen
/// Bestand pflegt, wird nach dem zweiten neuen Kuerzel falsch, ohne dass
/// es jemandem auffaellt - sie macht ja nichts kaputt, sie schickt nur
/// jeden in die Irre, der ihr glaubt.
/// </summary>
public class TastenkuerzelTests
{
    [Fact]
    public void Jedes_Kuerzel_hat_Taste_Beschreibung_und_Geste()
    {
        Assert.NotEmpty(Tastenkuerzel.Alle);

        foreach (var kuerzel in Tastenkuerzel.Alle)
        {
            Assert.False(string.IsNullOrWhiteSpace(kuerzel.Gruppe));
            Assert.False(string.IsNullOrWhiteSpace(kuerzel.Taste));
            Assert.False(string.IsNullOrWhiteSpace(kuerzel.Beschreibung));
            Assert.NotEmpty(kuerzel.Gesten);
        }
    }

    [Fact]
    public void Jede_Geste_ist_gueltig()
    {
        // Ein Tippfehler in einer Geste faellt sonst erst auf, wenn jemand
        // die Anwendung startet - KeyGesture.Parse wirft dann mitten im
        // Aufbau des Fensters.
        foreach (var geste in Tastenkuerzel.Alle.SelectMany(k => k.Gesten))
        {
            var geparst = KeyGesture.Parse(geste);
            Assert.NotEqual(Key.None, geparst.Key);
        }
    }

    [Fact]
    public void Keine_Geste_kommt_zweimal_vor()
    {
        var gesten = Tastenkuerzel.Alle.SelectMany(k => k.Gesten).ToList();

        // Zwei Kuerzel auf derselben Taste: eines davon wirkt, welches,
        // haengt an der Reihenfolge der Bindungen - und die Uebersicht
        // verspricht beide.
        Assert.Equal(gesten.Count, gesten.Distinct().Count());
    }

    [Fact]
    public void Der_Bereichswechsel_deckt_genau_die_Ziffern_eins_bis_neun_ab()
    {
        var bereiche = Tastenkuerzel.Fuer(TastenkuerzelAktion.BereichWechseln);

        Assert.Equal(Tastenkuerzel.BereicheMitZiffer, bereiche.Gesten.Count);
        Assert.Equal("Ctrl+D1", bereiche.Gesten[0]);
        Assert.Equal("Ctrl+D9", bereiche.Gesten[^1]);
    }

    /// <summary>
    /// Die Seitenleiste hat mehr Eintraege, als es Ziffern gibt - seit der
    /// Jahresrueckblick dazugekommen ist, faellt der letzte Eintrag aus
    /// Strg+1…9 heraus. Der Beschreibungstext darf deshalb nicht laenger
    /// behaupten, jeder Eintrag sei ueber seine Stelle erreichbar; die
    /// Uebersicht waere sonst nachweislich falsch.
    /// </summary>
    [Fact]
    public void Der_Bereichswechsel_verspricht_nicht_mehr_Bereiche_als_es_Ziffern_gibt()
    {
        var bereiche = Tastenkuerzel.Fuer(TastenkuerzelAktion.BereichWechseln);

        Assert.Contains("ersten neun", bereiche.Beschreibung, StringComparison.Ordinal);
    }

    [Fact]
    public void Geste_liefert_nur_bei_eindeutigen_Kuerzeln_einen_Wert()
    {
        Assert.Equal("F1", Tastenkuerzel.Geste(TastenkuerzelAktion.Uebersicht));

        // Speichern hoert auf zwei Tasten - wer hier eine einzelne
        // erwartet, hat sich vertan und soll das sofort merken, statt eine
        // der beiden stillschweigend zu verlieren.
        Assert.Throws<InvalidOperationException>(
            () => Tastenkuerzel.Geste(TastenkuerzelAktion.Speichern));
    }

    [Fact]
    public void Die_Uebersichtsseite_zeigt_genau_die_Kuerzel_der_Liste()
    {
        var seite = new TastenkuerzelViewModel();

        var gezeigt = seite.Gruppen.SelectMany(gruppe => gruppe.Kuerzel).ToList();

        Assert.Equal(Tastenkuerzel.Alle, gezeigt);

        // Und in Gruppen, jede mit ihrer Ueberschrift.
        Assert.All(seite.Gruppen, gruppe => Assert.False(string.IsNullOrWhiteSpace(gruppe.Name)));
        Assert.All(seite.Gruppen, gruppe => Assert.NotEmpty(gruppe.Kuerzel));
    }

    [Fact]
    public void Eine_Bindung_findet_ihr_Kommando_auch_nachtraeglich()
    {
        // Der Kern von Kuerzelbindung: die Ansichten haengen ihre Kuerzel
        // im Konstruktor an, der DataContext kommt erst danach. Wuerde das
        // Kommando dort einmalig abgegriffen, bliebe die Taste fuer immer
        // stumm - und zwar ohne jede Fehlermeldung, weil eine KeyBinding
        // ohne Kommando einfach nichts tut.
        var element = new Border();
        var viewModel = new TastenkuerzelViewModel();
        var ausgeloest = false;
        viewModel.Geschlossen += (_, _) => ausgeloest = true;

        Kuerzelbindung.Binde(
            element,
            TastenkuerzelAktion.Uebersicht,
            nameof(TastenkuerzelViewModel.SchliessenCommand));

        element.DataContext = viewModel;
        element.KeyBindings[0].Command.Execute(null);

        Assert.True(ausgeloest);
    }

    [Fact]
    public void Eine_Bindung_ohne_DataContext_tut_nichts()
    {
        // Kommt ein Tastendruck an, bevor die Ansicht ihr ViewModel hat,
        // darf er nicht mit einer Ausnahme mitten in der Bedienung enden.
        var element = new Border();

        Kuerzelbindung.Binde(
            element,
            TastenkuerzelAktion.Uebersicht,
            nameof(TastenkuerzelViewModel.SchliessenCommand));

        Assert.False(element.KeyBindings[0].Command.CanExecute(null));
        element.KeyBindings[0].Command.Execute(null);
    }

    [Fact]
    public void Schliessen_meldet_sich_bei_der_Navigation()
    {
        var seite = new TastenkuerzelViewModel();
        var gemeldet = false;
        seite.Geschlossen += (_, _) => gemeldet = true;

        seite.SchliessenCommand.Execute(null);

        Assert.True(gemeldet);
    }
}
