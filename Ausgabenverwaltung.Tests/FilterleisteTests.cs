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
/// Die Filterleiste, die sich Ausgabenliste und Auswertung teilen.
///
/// Jeder Fall hier laeuft gegen BEIDE Bereiche. Das ist der Punkt: die
/// Leiste ist dieselbe, und sie war frueher zweimal abgeschrieben - rund
/// 500 Zeilen, an mehreren Stellen mit dem Kommentar "siehe die gleiche
/// Stelle in AusgabenlisteViewModel". Zwei Abschriften laufen
/// auseinander, sobald jemand nur eine davon anfasst, und das faellt
/// niemandem auf: beide Bereiche sehen fuer sich genommen richtig aus.
///
/// Seit beide von <see cref="FilterleisteViewModel"/> erben, kann das
/// nicht mehr passieren - diese Tests halten fest, dass es auch so
/// bleibt, und beschreiben zugleich, was die Leiste eigentlich tut.
/// </summary>
public class FilterleisteTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;
    private readonly ReportRepository _auswertung;
    private readonly TestEinstellungen _einstellungen = new();

    public FilterleisteTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);
        _auswertung = new ReportRepository(_connection);

        _kategorien.Create("Wohnen", parentId: null);
        _personen.Create("Ich", isSelf: true);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    /// <summary>
    /// Beide Bereiche als Filterleiste. Die Namen stehen im Testnamen der
    /// fehlgeschlagenen Zeile - so ist sofort zu sehen, WELCHER der
    /// beiden abweicht.
    /// </summary>
    public static TheoryData<string> BeideBereiche => new() { "Ausgabenliste", "Auswertung" };

    private FilterleisteViewModel Leiste(string bereich) => bereich switch
    {
        "Ausgabenliste" => new AusgabenlisteViewModel(
            _ausgaben, _kategorien, _personen,
            _einstellungen.Store, new WeakReferenceMessenger(), new ToastViewModel()),

        "Auswertung" => new ReportViewModel(
            _auswertung, _ausgaben, _kategorien, _personen,
            _einstellungen.Store, new WeakReferenceMessenger()),

        _ => throw new ArgumentOutOfRangeException(nameof(bereich)),
    };

    // ---------------- Zeitraum ----------------

    // Frisch geoeffnet steht in beiden Bereichen das laufende Jahr - und
    // weil das die Vorgabe ist, ist es KEIN gesetzter Filter und bekommt
    // keinen Chip.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Frisch_geoeffnet_gilt_der_Vorgabezeitraum_ohne_Chip(string bereich)
    {
        var leiste = Leiste(bereich);

        Assert.Equal(0, leiste.AktiveFilterAnzahl);
        Assert.False(leiste.HatAktiveFilter);
        Assert.NotEmpty(leiste.VonText);
        Assert.NotEmpty(leiste.BisText);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Eine_Schnellwahl_merkt_sich_ihren_Schluessel(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.SchnellwahlCommand.Execute("LetztesJahr");

        Assert.Equal("LetztesJahr", leiste.AktiverZeitraumSchluessel);
    }

    // Wer danach von Hand ein Datum eintippt, hat keine Schnellwahl mehr -
    // sonst stuende der Knopf hervorgehoben da, waehrend etwas anderes
    // gilt.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Eine_Handeingabe_loescht_die_Schnellwahl(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.SchnellwahlCommand.Execute("LetztesJahr");

        leiste.VonText = "01.03.2026";

        Assert.Null(leiste.AktiverZeitraumSchluessel);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Ein_unlesbares_Datum_erklaert_sich_am_Feld(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.VonText = "kein Datum";

        Assert.True(leiste.ZeitraumFehlerSichtbar);
        Assert.NotNull(leiste.ZeitraumFehler);
    }

    // ---------------- Chips ----------------

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Eine_Suche_erscheint_als_Chip(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.Suchtext = "Kaffee";

        var chip = Assert.Single(leiste.AktiveFilter);
        Assert.Equal(FilterArt.Suche, chip.Art);
        Assert.Equal(1, leiste.AktiveFilterAnzahl);
    }

    // Der Weg vom Chip zurueck. Ohne ihn muesste der Anwender die Leiste
    // aufklappen und das Feld suchen - und dann waere das Einklappen ein
    // Rueckschritt.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Das_Kreuz_am_Chip_hebt_genau_diesen_Filter_auf(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.Suchtext = "Kaffee";
        leiste.StatusOffen = true;

        var suchChip = leiste.AktiveFilter.Single(c => c.Art == FilterArt.Suche);
        leiste.FilterAufhebenCommand.Execute(suchChip);

        Assert.Equal(string.Empty, leiste.Suchtext);

        // Der andere Filter bleibt stehen - aufgehoben wird genau einer.
        Assert.True(leiste.StatusOffen);
        Assert.Contains(leiste.AktiveFilter, c => c.Art == FilterArt.Status);
    }

    // Beide Haekchen zusammen heissen "alles" und schraenken nicht ein -
    // eine Buchung ist entweder Einnahme oder Ausgabe, nie beides.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Beide_Buchungsarten_zusammen_ergeben_keinen_Chip(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.NurEinnahmen = true;
        leiste.NurAusgaben = true;

        Assert.DoesNotContain(leiste.AktiveFilter, c => c.Art == FilterArt.Buchungsart);
    }

    // ---------------- Zuruecksetzen ----------------

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Zuruecksetzen_raeumt_die_ganze_Leiste(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.Suchtext = "Kaffee";
        leiste.StatusOffen = true;
        leiste.MeineKosten = true;
        leiste.SchnellwahlCommand.Execute("LetztesJahr");

        leiste.FilterZuruecksetzenCommand.Execute(null);

        Assert.Equal(string.Empty, leiste.Suchtext);
        Assert.False(leiste.StatusOffen);
        Assert.False(leiste.MeineKosten);
        Assert.Null(leiste.AktiverZeitraumSchluessel);

        // Und damit steht auch wieder der Vorgabezeitraum da, also kein
        // einziger Chip.
        Assert.Equal(0, leiste.AktiveFilterAnzahl);
    }

    // ---------------- Ein- und Ausklappen ----------------

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Ein_schmales_Fenster_klappt_die_Leiste_zu(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.PasseAnBreiteAn(300);
        Assert.False(leiste.FilterAufgeklappt);

        leiste.PasseAnBreiteAn(1200);
        Assert.True(leiste.FilterAufgeklappt);
    }

    // Sobald der Anwender selbst geklappt hat, entscheidet er - ein
    // Umschalten, das beim naechsten Ziehen am Fenster von selbst
    // zurueckspringt, ist schlimmer als gar keines.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Nach_dem_Klappen_von_Hand_schweigt_die_Breite(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.FilterUmschaltenCommand.Execute(null);
        var vonHand = leiste.FilterAufgeklappt;

        leiste.PasseAnBreiteAn(300);
        leiste.PasseAnBreiteAn(1200);

        Assert.Equal(vonHand, leiste.FilterAufgeklappt);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Der_Klapphinweis_nennt_die_Anzahl_der_Filter(string bereich)
    {
        var leiste = Leiste(bereich);

        Assert.DoesNotContain("gesetzt", leiste.FilterKlappHinweis);

        leiste.Suchtext = "Kaffee";

        Assert.Contains("1 Filter gesetzt", leiste.FilterKlappHinweis);
    }

    // ---------------- Gespeicherte Filter ----------------

    // Die Liste gehoert beiden Bereichen gemeinsam (sie steht in der
    // Einstellungsdatei): was der eine speichert, muss der andere sehen.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Ein_gespeicherter_Stand_kommt_wieder_zurueck(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.Suchtext = "Kaffee";
        leiste.StatusOffen = true;
        leiste.FilterName = "Mein Stand";

        leiste.FilterSpeichernCommand.Execute(null);

        Assert.Null(leiste.FilterNameFehler);
        Assert.Equal(string.Empty, leiste.FilterName);
        Assert.True(leiste.HatGespeicherteFilter);

        // Erst alles wegraeumen, dann den Stand wieder anwenden.
        leiste.FilterZuruecksetzenCommand.Execute(null);
        Assert.Equal(string.Empty, leiste.Suchtext);

        var gespeichert = leiste.GespeicherteFilter.Single();
        leiste.GespeichertenFilterAnwendenCommand.Execute(gespeichert);

        Assert.Equal("Kaffee", leiste.Suchtext);
        Assert.True(leiste.StatusOffen);
    }

    /// <summary>
    /// Der Grund, warum es dieselbe Leiste sein MUSS: ein in der
    /// Ausgabenliste abgelegter Filter wird in der Auswertung angewendet
    /// und bedeutet dort dasselbe. Die Liste ist geteilt - liefen die
    /// beiden Leisten auseinander, waere sie eine Falle.
    /// </summary>
    [Fact]
    public void Ein_in_der_Liste_gespeicherter_Filter_wirkt_in_der_Auswertung()
    {
        var liste = Leiste("Ausgabenliste");
        liste.Suchtext = "Kaffee";
        liste.StatusOffen = true;
        liste.MeineKosten = true;
        liste.SchnellwahlCommand.Execute("LetztesJahr");
        liste.FilterName = "Kaffee letztes Jahr";
        liste.FilterSpeichernCommand.Execute(null);

        // Frisch aufgebaut, damit der Stand wirklich aus der Datei kommt.
        var auswertung = Leiste("Auswertung");
        var gespeichert = auswertung.GespeicherteFilter.Single();

        auswertung.GespeichertenFilterAnwendenCommand.Execute(gespeichert);

        Assert.Equal("Kaffee", auswertung.Suchtext);
        Assert.True(auswertung.StatusOffen);
        Assert.True(auswertung.MeineKosten);

        // Der Schnellwahl-Zeitraum wird neu ausgerechnet und nicht als
        // festes Datum uebernommen - "letztes Jahr" heisst in jedem Jahr
        // etwas anderes.
        Assert.Equal("LetztesJahr", auswertung.AktiverZeitraumSchluessel);
        Assert.Equal(liste.VonText, auswertung.VonText);
        Assert.Equal(liste.BisText, auswertung.BisText);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Ein_namenloser_Stand_wird_am_Feld_abgelehnt(string bereich)
    {
        var leiste = Leiste(bereich);

        leiste.FilterName = "   ";
        leiste.FilterSpeichernCommand.Execute(null);

        // Regel 13: der Fehler steht am Feld, und gespeichert wurde nichts.
        Assert.True(leiste.FilterNameFehlerSichtbar);
        Assert.False(leiste.HatGespeicherteFilter);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Ein_geloeschter_Stand_verschwindet_aus_der_Liste(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.FilterName = "Weg damit";
        leiste.FilterSpeichernCommand.Execute(null);

        leiste.GespeichertenFilterLoeschenCommand.Execute(leiste.GespeicherteFilter.Single());

        Assert.False(leiste.HatGespeicherteFilter);
        Assert.Empty(leiste.GespeicherteFilter);
    }

    // ---------------- Auswahllisten ----------------

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Der_Kategoriebaum_steht_in_beiden_Bereichen(string bereich)
    {
        var leiste = Leiste(bereich);

        Assert.Contains(leiste.KategorieWurzeln, k => k.Name == "Wohnen");
        Assert.Contains(leiste.ZahlerOptionen, z => z.Bezeichnung.Contains("Ich"));
    }

    // Ein Haekchen im Baum schlaegt bis in die Beschriftung durch - sie
    // haengt an keiner beobachtbaren Eigenschaft und muss von Hand
    // angestossen werden.
    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Eine_angehakte_Kategorie_steht_in_der_Beschriftung(string bereich)
    {
        var leiste = Leiste(bereich);
        var wohnen = leiste.KategorieWurzeln.Single(k => k.Name == "Wohnen");

        wohnen.IstGewaehlt = true;

        Assert.Contains("Wohnen", leiste.KategorieFilterText);
        Assert.Contains(leiste.AktiveFilter, c => c.Art == FilterArt.Kategorien);
    }

    [Theory]
    [MemberData(nameof(BeideBereiche))]
    public void Alle_Kategorien_hebt_die_Auswahl_wieder_auf(string bereich)
    {
        var leiste = Leiste(bereich);
        leiste.KategorieWurzeln.Single(k => k.Name == "Wohnen").IstGewaehlt = true;

        leiste.AlleKategorienCommand.Execute(null);

        Assert.DoesNotContain(leiste.AktiveFilter, c => c.Art == FilterArt.Kategorien);
    }
}
