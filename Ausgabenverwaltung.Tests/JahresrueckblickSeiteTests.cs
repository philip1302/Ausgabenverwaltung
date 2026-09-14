using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.YearInReview;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Seite "Jahresrueckblick": Jahresauswahl, Umschalter, Leerzustaende
/// und die Spruenge in die Ausgabenliste.
///
/// Der heutige Tag wird hereingereicht statt aus der Systemuhr gelesen -
/// sonst haetten die Tests im naechsten Januar ein anderes Ergebnis.
/// </summary>
public class JahresrueckblickSeiteTests : IDisposable
{
    private static readonly DateOnly Heute = new(2026, 9, 13);

    private readonly System.Data.IDbConnection _connection;
    private readonly YearInReviewService _service;
    private readonly CategoryRepository _categories;
    private readonly ExpenseRepository _expenses;

    private readonly int _selfId;
    private readonly int _wohnenId;
    private readonly int _stromId;
    private readonly int _freizeitId;

    public JahresrueckblickSeiteTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _expenses = new ExpenseRepository(_connection);
        _categories = new CategoryRepository(_connection);

        _selfId = new PersonRepository(_connection).Create("Ich", isSelf: true).Id;

        _wohnenId = _categories.Create("Wohnen", null).Id;
        _stromId = _categories.Create("Strom", _wohnenId).Id;
        _freizeitId = _categories.Create("Freizeit", null).Id;

        _service = new YearInReviewService(
            new ReportRepository(_connection), _categories, _expenses);
    }

    public void Dispose() => _connection.Dispose();

    // Eigene Messenger-Instanz je Aufruf (Regel 14) - ein prozessweit
    // geteilter liesse Registrierungen frueherer Tests hineinwirken.
    private JahresrueckblickViewModel NeueSeite(IMessenger? messenger = null) =>
        new(_service, _categories, messenger ?? new WeakReferenceMessenger(), () => Heute);

    private void Buche(int kategorieId, long cents, DateOnly datum) =>
        _expenses.Create(kategorieId, cents, datum, _selfId);

    private void ZweiVolleJahre()
    {
        Buche(_stromId, 174_000, new DateOnly(2025, 5, 1));
        Buche(_freizeitId, 300_000, new DateOnly(2025, 6, 1));
        Buche(_stromId, 298_000, new DateOnly(2026, 5, 1));
        Buche(_freizeitId, 120_000, new DateOnly(2026, 6, 1));
    }

    // ================= Jahresauswahl =================

    [Fact]
    public void Angeboten_werden_die_Jahre_mit_Buchungen_das_juengste_zuerst()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();

        Assert.Equal(new[] { 2026, 2025 }, seite.JahrOptionen);
        Assert.Equal(2026, seite.AusgewaehltesJahr);
    }

    [Fact]
    public void Ein_anderes_Jahr_zu_waehlen_baut_die_Seite_neu_auf()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        Assert.Equal("2026", seite.JahrKopf);

        seite.AusgewaehltesJahr = 2025;

        Assert.Equal("2025", seite.JahrKopf);
        Assert.Equal("2024", seite.VorjahrKopf);
    }

    // ================= Umschalter =================

    [Fact]
    public void Das_laufende_Jahr_bietet_den_Umschalter_an()
    {
        ZweiVolleJahre();

        Assert.True(NeueSeite().ZeitraumUmschaltbar);
    }

    [Fact]
    public void Ein_abgeschlossenes_Jahr_bietet_den_Umschalter_nicht_an()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        seite.AusgewaehltesJahr = 2025;

        Assert.False(seite.ZeitraumUmschaltbar);
    }

    [Fact]
    public void Der_Umschalter_auf_ganze_Kalenderjahre_aendert_die_Kennzahlen()
    {
        ZweiVolleJahre();
        // Liegt hinter dem heutigen Tag und zaehlt deshalb nur beim
        // Vergleich ganzer Kalenderjahre mit.
        Buche(_freizeitId, 500_000, new DateOnly(2026, 11, 1));

        var seite = NeueSeite();
        var bisHeute = seite.AusgabenKennzahl!.WertText;

        seite.GanzeKalenderjahre = true;

        Assert.NotEqual(bisHeute, seite.AusgabenKennzahl!.WertText);
    }

    [Fact]
    public void Ganze_Kalenderjahre_im_laufenden_Jahr_werden_ausdruecklich_eingeordnet()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        Assert.False(seite.HatGanzjahresWarnung);

        seite.GanzeKalenderjahre = true;

        Assert.True(seite.HatGanzjahresWarnung);
    }

    // ================= Lagen =================

    [Fact]
    public void Ohne_jede_Buchung_bietet_die_Seite_das_Erfassen_an()
    {
        var seite = NeueSeite();

        Assert.True(seite.NochNichtsErfasst);
        Assert.False(seite.ZeigtInhalt);
        Assert.NotEqual(string.Empty, seite.LageHinweis);
    }

    [Fact]
    public void Der_Knopf_im_leeren_Rueckblick_fuehrt_in_die_Erfassung()
    {
        var seite = NeueSeite();
        var gerufen = false;
        seite.ErfassenAngefordert += (_, _) => gerufen = true;

        seite.AusgabeErfassenCommand.Execute(null);

        Assert.True(gerufen);
    }

    /// <summary>
    /// Ohne Vorjahr fehlt der Vergleich, nicht der Inhalt: Kennzahlen und
    /// Tabelle gelten fuer sich und bleiben stehen.
    /// </summary>
    [Fact]
    public void Ohne_Vorjahresbuchung_bleiben_Kennzahlen_und_Tabelle_stehen()
    {
        Buche(_freizeitId, 300_000, new DateOnly(2026, 6, 1));

        var seite = NeueSeite();

        Assert.True(seite.VorjahrOhneBuchung);
        Assert.True(seite.ZeigtInhalt);
        Assert.NotEmpty(seite.Zeilen);
        Assert.NotNull(seite.SummenZeile);
    }

    // ================= Tabelle =================

    [Fact]
    public void Der_groesste_Posten_steht_oben()
    {
        Buche(_freizeitId, 100_000, new DateOnly(2026, 6, 1));
        Buche(_stromId, 500_000, new DateOnly(2026, 5, 1));

        var seite = NeueSeite();

        Assert.Equal("Wohnen", seite.Zeilen[0].Name);
    }

    [Fact]
    public void Unterkategorien_erscheinen_erst_nach_dem_Aufklappen()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        Assert.DoesNotContain(seite.Zeilen, z => z.Name == "Strom");

        seite.ZeileUmschaltenCommand.Execute(
            seite.Zeilen.First(z => z.Name == "Wohnen"));

        Assert.Contains(seite.Zeilen, z => z.Name == "Strom");
    }

    [Fact]
    public void Das_Aufklappen_ueberdauert_einen_Jahreswechsel()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        seite.ZeileUmschaltenCommand.Execute(seite.Zeilen.First(z => z.Name == "Wohnen"));

        seite.AusgewaehltesJahr = 2025;

        Assert.Contains(seite.Zeilen, z => z.Name == "Strom");
    }

    // ================= Spruenge =================

    [Fact]
    public void Ein_Klick_auf_eine_Zeile_fordert_Kategorie_und_Zeitraum_an()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        RueckblickSprung? sprung = null;
        seite.AusgabenlisteAngefordert += (_, s) => sprung = s;

        seite.ZeileOeffnenCommand.Execute(seite.Zeilen.First(z => z.Name == "Wohnen"));

        Assert.NotNull(sprung);
        Assert.Equal(_wohnenId, sprung!.KategorieId);
        Assert.Equal(new DateOnly(2026, 1, 1), sprung.Von);
        Assert.Equal(Heute, sprung.BisEinschliesslich);
    }

    [Fact]
    public void Ein_Klick_auf_eine_Karte_fordert_ihren_Gegenstand_an()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        Assert.NotEmpty(seite.Karten);

        RueckblickSprung? sprung = null;
        seite.AusgabenlisteAngefordert += (_, s) => sprung = s;

        var karte = seite.Karten.First(k => k.KategorieId is not null);
        seite.KarteOeffnenCommand.Execute(karte);

        Assert.NotNull(sprung);
        Assert.Equal(karte.KategorieId, sprung!.KategorieId);
    }

    /// <summary>
    /// Ein Monat des laufenden Jahres endet heute und nicht an seinem
    /// Monatsende - sonst zeigte die Liste mehr Buchungen, als in die
    /// Karte eingerechnet wurden.
    /// </summary>
    [Fact]
    public void Ein_Monatssprung_geht_nie_ueber_den_heutigen_Tag_hinaus()
    {
        ZweiVolleJahre();
        Buche(_freizeitId, 900_000, new DateOnly(2026, 9, 2));

        var seite = NeueSeite();
        var monatskarte = seite.Karten.FirstOrDefault(k => k.Quelle.PeriodKey == "2026-09");
        Assert.NotNull(monatskarte);

        RueckblickSprung? sprung = null;
        seite.AusgabenlisteAngefordert += (_, s) => sprung = s;
        seite.KarteOeffnenCommand.Execute(monatskarte);

        Assert.NotNull(sprung);
        Assert.Equal(new DateOnly(2026, 9, 1), sprung!.Von);
        Assert.Equal(Heute, sprung.BisEinschliesslich);
    }

    // ================= Aktualisierung =================

    /// <summary>
    /// Regel 14: eine anderswo erfasste Buchung muss hier sofort
    /// ankommen, nicht erst beim naechsten Navigieren.
    /// </summary>
    [Fact]
    public void Die_Seite_laedt_bei_der_Aenderungsnachricht_neu()
    {
        IMessenger messenger = new WeakReferenceMessenger();
        var seite = NeueSeite(messenger);

        Assert.True(seite.NochNichtsErfasst);

        ZweiVolleJahre();
        messenger.Send(new BuchungenGeaendertNachricht());

        Assert.False(seite.NochNichtsErfasst);
        Assert.NotEmpty(seite.Zeilen);
    }

    // ================= Export =================

    [Fact]
    public void Der_Dateiname_nennt_das_Jahr()
    {
        ZweiVolleJahre();

        Assert.Equal("Jahresrueckblick_2026.csv", NeueSeite().CsvDateiname);
    }

    [Fact]
    public void Der_Export_bildet_die_aufgeklappte_Struktur_ab()
    {
        ZweiVolleJahre();

        var seite = NeueSeite();
        Assert.DoesNotContain("Strom", seite.BaueCsv(), StringComparison.Ordinal);

        seite.ZeileUmschaltenCommand.Execute(seite.Zeilen.First(z => z.Name == "Wohnen"));

        Assert.Contains("Strom", seite.BaueCsv(), StringComparison.Ordinal);
    }
}
