using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die zwei Lagen hinter einer leeren Liste.
///
/// „Nichts da" und „nichts passt" sehen gleich aus, brauchen aber
/// verschiedene Angebote: einmal hilft nur die Erfassungsmaske, einmal
/// genügt das Zurücksetzen des Filters. Ein Angebot, das an der Lage
/// vorbeigeht, ist schlimmer als keines - „Filter zurücksetzen" in einer
/// leeren Datenbank schickt den Anwender im Kreis.
///
/// Deshalb entscheidet das ViewModel und nicht die Ansicht, und deshalb
/// steht die Entscheidung hier unter Test.
/// </summary>
public class LeerzustandTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;
    private readonly TestEinstellungen _einstellungen = new();

    private readonly int _wohnenId;
    private readonly int _ichId;

    public LeerzustandTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _wohnenId = _kategorien.Create("Wohnen", parentId: null).Id;
        _ichId = _personen.Create("Ich", isSelf: true).Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    private AusgabenlisteViewModel NeueListe() => new(
        _ausgaben, _kategorien, _personen, _einstellungen.Store,
        new WeakReferenceMessenger(), new ToastViewModel());

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    // ---------------- Die Abfrage in Core ----------------

    [Fact]
    public void Eine_leere_Datenbank_hat_keine_Buchung()
        => Assert.False(_ausgaben.HasAny());

    [Fact]
    public void Eine_einzige_Buchung_genuegt()
    {
        _ausgaben.Create(_wohnenId, 100, Heute, _ichId);

        Assert.True(_ausgaben.HasAny());
    }

    /// <summary>
    /// Kein Filter greift hier hinein - das ist der Punkt. Eine Buchung
    /// weit ausserhalb des eingestellten Zeitraums zaehlt trotzdem, denn
    /// die Frage lautet „gibt es ueberhaupt etwas", nicht „gibt es etwas
    /// hier".
    /// </summary>
    [Fact]
    public void Auch_eine_Buchung_ausserhalb_jedes_Zeitraums_zaehlt()
    {
        _ausgaben.Create(_wohnenId, 100, new DateOnly(1999, 1, 1), _ichId);

        Assert.True(_ausgaben.HasAny());
    }

    // ---------------- Die Unterscheidung in der Ansicht ----------------

    [Fact]
    public void Ohne_jede_Buchung_bietet_die_Liste_die_Erfassungsmaske_an()
    {
        var liste = NeueListe();

        Assert.True(liste.KeineTreffer);
        Assert.True(liste.NochNichtsErfasst);
        Assert.False(liste.KeinTrefferTrotzDaten);
    }

    /// <summary>
    /// Buchungen sind da, nur nicht im gewaehlten Zeitraum. Jetzt hilft
    /// das Zuruecksetzen des Filters - und ausdruecklich NICHT das Angebot
    /// „erste Ausgabe erfassen", das dem Anwender erzaehlen wuerde, seine
    /// Daten seien weg.
    /// </summary>
    [Fact]
    public void Liegt_es_am_Filter_bietet_die_Liste_das_Zuruecksetzen_an()
    {
        _ausgaben.Create(_wohnenId, 100, new DateOnly(1999, 1, 1), _ichId);

        var liste = NeueListe();

        Assert.True(liste.KeineTreffer);
        Assert.False(liste.NochNichtsErfasst);
        Assert.True(liste.KeinTrefferTrotzDaten);
    }

    [Fact]
    public void Mit_Treffern_ist_kein_Leerzustand_sichtbar()
    {
        _ausgaben.Create(_wohnenId, 100, Heute, _ichId);

        var liste = NeueListe();

        Assert.False(liste.KeineTreffer);
        Assert.False(liste.NochNichtsErfasst);
        Assert.False(liste.KeinTrefferTrotzDaten);
    }

    /// <summary>
    /// Die beiden Leerzustaende schliessen einander aus - sonst stuenden
    /// zwei Angebote uebereinander.
    /// </summary>
    [Fact]
    public void Nie_stehen_beide_Angebote_zugleich()
    {
        foreach (var mitBuchung in new[] { false, true })
        {
            if (mitBuchung)
            {
                _ausgaben.Create(_wohnenId, 100, new DateOnly(1999, 1, 1), _ichId);
            }

            var liste = NeueListe();

            Assert.False(liste.NochNichtsErfasst && liste.KeinTrefferTrotzDaten);
        }
    }

    /// <summary>
    /// Nach dem Erfassen der ersten Buchung ist der Leerzustand weg -
    /// gemeldet ueber dieselbe Nachricht, die auch die Liste neu laedt
    /// (Regel 14).
    /// </summary>
    [Fact]
    public void Die_erste_Buchung_raeumt_den_Leerzustand_weg()
    {
        var messenger = new WeakReferenceMessenger();
        var liste = new AusgabenlisteViewModel(
            _ausgaben, _kategorien, _personen, _einstellungen.Store, messenger,
            new ToastViewModel());

        Assert.True(liste.NochNichtsErfasst);

        _ausgaben.Create(_wohnenId, 100, Heute, _ichId);
        messenger.Send(new BuchungenGeaendertNachricht());

        Assert.False(liste.NochNichtsErfasst);
        Assert.False(liste.KeineTreffer);
    }

    /// <summary>
    /// Das Angebot aus dem Leerzustand loest denselben Wechsel aus wie die
    /// Kachel der Startseite - die Liste kennt die Navigation nicht selbst.
    /// </summary>
    [Fact]
    public void Der_Knopf_im_Leerzustand_bittet_um_die_Erfassungsmaske()
    {
        var liste = NeueListe();
        var gerufen = 0;
        liste.ErfassenAngefordert += (_, _) => gerufen++;

        liste.AusgabeErfassenCommand.Execute(null);

        Assert.Equal(1, gerufen);
    }

    // ---------------- Chips der aktiven Filter ----------------

    /// <summary>
    /// Frisch geoeffnet ist nichts gefiltert - der Knopf traegt keine Zahl
    /// und es steht kein Chip da. Sonst saehe jede Ansicht dauerhaft
    /// gefiltert aus.
    /// </summary>
    [Fact]
    public void Frisch_geoeffnet_gibt_es_keine_aktiven_Filter()
    {
        var liste = NeueListe();

        Assert.Empty(liste.AktiveFilter);
        Assert.False(liste.HatAktiveFilter);
        Assert.Equal("Filterfelder verbergen", liste.FilterKlappHinweis);
    }

    [Fact]
    public void Eine_Suche_erscheint_als_Chip_und_zaehlt_am_Knopf()
    {
        var liste = NeueListe();
        liste.Suchtext = "Rewe";

        var chip = Assert.Single(liste.AktiveFilter);

        Assert.Equal(FilterArt.Suche, chip.Art);

        // Die Anzahl stand frueher als "Filter (1)" auf dem Knopf; seit
        // dort nur noch ein Pfeil steht, traegt sie sein Hinweistext.
        Assert.Equal("Filterfelder verbergen — 1 Filter gesetzt", liste.FilterKlappHinweis);
    }

    /// <summary>
    /// Der eigentliche Zweck des Chips: er hebt GENAU seinen Filter auf.
    /// Ohne das muesste der Anwender die Leiste aufklappen und das Feld
    /// suchen - dann waere das Einklappen ein Rueckschritt.
    /// </summary>
    [Fact]
    public void Das_Kreuz_am_Chip_hebt_genau_diesen_Filter_auf()
    {
        var liste = NeueListe();
        liste.Suchtext = "Rewe";
        liste.MeineKosten = true;

        Assert.Equal(2, liste.AktiveFilter.Count);

        var suche = liste.AktiveFilter.Single(chip => chip.Art == FilterArt.Suche);
        liste.FilterAufhebenCommand.Execute(suche);

        Assert.Equal(string.Empty, liste.Suchtext);
        Assert.True(liste.MeineKosten);

        var uebrig = Assert.Single(liste.AktiveFilter);
        Assert.Equal(FilterArt.MeineKosten, uebrig.Art);
    }

    /// <summary>
    /// Solange der Anwender nicht selbst umschaltet, richtet sich der
    /// Klappzustand nach der Breite. Danach gilt seine Entscheidung - ein
    /// Umschalten, das gleich wieder von selbst zurueckspringt, ist
    /// schlimmer als gar keines.
    /// </summary>
    [Fact]
    public void Die_Breite_klappt_die_Leiste_ein_bis_der_Anwender_entscheidet()
    {
        var liste = NeueListe();

        liste.PasseAnBreiteAn(360);
        Assert.False(liste.FilterAufgeklappt);

        liste.PasseAnBreiteAn(1200);
        Assert.True(liste.FilterAufgeklappt);

        // Ab jetzt entscheidet der Anwender.
        liste.FilterUmschaltenCommand.Execute(null);
        Assert.False(liste.FilterAufgeklappt);

        liste.PasseAnBreiteAn(1200);
        Assert.False(liste.FilterAufgeklappt);
    }

    /// <summary>
    /// Der Pfeil am Klappknopf zeigt den Zustand an: aufgeklappt nach
    /// oben, eingeklappt nach unten - und sein Hinweis sagt, was der
    /// Klick tut (Carbon Design System, "Accordion").
    /// </summary>
    [Fact]
    public void Der_Klapp_Pfeil_zeigt_nach_oben_solange_die_Leiste_offen_ist()
    {
        var liste = NeueListe();

        Assert.True(liste.FilterAufgeklappt);
        Assert.Equal("▲", liste.FilterKlappZeichen);
        Assert.Equal("Filterfelder verbergen", liste.FilterKlappHinweis);

        liste.FilterUmschaltenCommand.Execute(null);

        Assert.Equal("▼", liste.FilterKlappZeichen);
        Assert.Equal("Filterfelder anzeigen", liste.FilterKlappHinweis);
    }
}
