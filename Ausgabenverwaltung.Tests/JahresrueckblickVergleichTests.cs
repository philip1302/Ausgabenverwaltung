using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Gegenueberstellung zweier Jahre gegen eine echte Datenbank: dass
/// zwei getrennte Abfragen in EINER Tabelle landen, dass die Vorzeichen
/// genau einmal gedreht werden und dass die Kennzahlen oben nicht von der
/// Summe unten abweichen koennen.
/// </summary>
public class JahresrueckblickVergleichTests : IDisposable
{
    private static readonly DateOnly Heute = new(2026, 9, 13);

    private readonly System.Data.IDbConnection _connection;
    private readonly YearInReviewService _service;
    private readonly ExpenseRepository _expenses;
    private readonly CategoryRepository _categories;

    private readonly int _selfId;
    private readonly int _otherId;

    // Wohnen > Nebenkosten > Strom, daneben Freizeit.
    private readonly int _wohnenId;
    private readonly int _nebenkostenId;
    private readonly int _stromId;
    private readonly int _freizeitId;

    public JahresrueckblickVergleichTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _expenses = new ExpenseRepository(_connection);
        _categories = new CategoryRepository(_connection);

        var people = new PersonRepository(_connection);
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;

        _wohnenId = _categories.Create("Wohnen", null).Id;
        _nebenkostenId = _categories.Create("Nebenkosten", _wohnenId).Id;
        _stromId = _categories.Create("Strom", _nebenkostenId).Id;
        _freizeitId = _categories.Create("Freizeit", null).Id;

        _service = new YearInReviewService(
            new ReportRepository(_connection), _categories, _expenses);
    }

    public void Dispose() => _connection.Dispose();

    private YearInReviewResult Rueckblick(
        ReviewSpan spanne = ReviewSpan.GanzeKalenderjahre) =>
        _service.Build(2026, spanne, Heute);

    private static ReviewCategoryChange Finde(ReviewComparison vergleich, int kategorieId)
    {
        var treffer = Suche(vergleich.Roots, kategorieId);
        Assert.NotNull(treffer);
        return treffer!;
    }

    private static ReviewCategoryChange? Suche(
        IReadOnlyList<ReviewCategoryChange> zeilen, int kategorieId)
    {
        foreach (var zeile in zeilen)
        {
            if (zeile.CategoryId == kategorieId)
            {
                return zeile;
            }

            var tiefer = Suche(zeile.Children, kategorieId);
            if (tiefer is not null)
            {
                return tiefer;
            }
        }

        return null;
    }

    // ================= Vorzeichen =================

    /// <summary>
    /// Die Auswertung liefert Ausgaben negativ. Gedreht wird genau einmal,
    /// beim Bau der Zeilen - ab da heisst "mehr" auch mehr.
    /// </summary>
    [Fact]
    public void Ausgaben_stehen_im_Vergleich_als_positive_Betraege()
    {
        _expenses.Create(_stromId, 40_000, new DateOnly(2025, 5, 1), _selfId);
        _expenses.Create(_stromId, 70_000, new DateOnly(2026, 5, 1), _selfId);

        var strom = Finde(Rueckblick().Comparison, _stromId);

        Assert.Equal(40_000, strom.PreviousCents);
        Assert.Equal(70_000, strom.CurrentCents);
        Assert.Equal(30_000, strom.DeltaCents);
    }

    // ================= Beide Jahre in einer Zeile =================

    /// <summary>
    /// Der Kern des Aufbaus: zwei getrennte Abfragen, ein Ergebnis. Weil
    /// jeder Zeitraum in genau einem Kalenderjahr liegt, koennen sich die
    /// beiden Gruppenschluessel nicht in die Quere kommen.
    /// </summary>
    [Fact]
    public void Beide_Jahre_stehen_nebeneinander_in_derselben_Zeile()
    {
        _expenses.Create(_freizeitId, 10_000, new DateOnly(2025, 2, 1), _selfId);
        _expenses.Create(_freizeitId, 25_000, new DateOnly(2026, 2, 1), _selfId);

        var freizeit = Finde(Rueckblick().Comparison, _freizeitId);

        Assert.Equal(10_000, freizeit.PreviousCents);
        Assert.Equal(25_000, freizeit.CurrentCents);
        Assert.Equal(1, freizeit.PreviousCount);
        Assert.Equal(1, freizeit.CurrentCount);
    }

    [Fact]
    public void Eine_Oberkategorie_enthaelt_ihren_ganzen_Ast_in_beiden_Jahren()
    {
        _expenses.Create(_stromId, 30_000, new DateOnly(2025, 5, 1), _selfId);
        _expenses.Create(_wohnenId, 20_000, new DateOnly(2025, 6, 1), _selfId);
        _expenses.Create(_stromId, 50_000, new DateOnly(2026, 5, 1), _selfId);
        _expenses.Create(_wohnenId, 20_000, new DateOnly(2026, 6, 1), _selfId);

        var wohnen = Finde(Rueckblick().Comparison, _wohnenId);

        Assert.Equal(50_000, wohnen.PreviousCents);
        Assert.Equal(70_000, wohnen.CurrentCents);
    }

    // ================= Zeitraum =================

    [Fact]
    public void Der_eingeschraenkte_Zeitraum_laesst_spaetere_Buchungen_aussen_vor()
    {
        _expenses.Create(_freizeitId, 10_000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_freizeitId, 99_000, new DateOnly(2026, 9, 20), _selfId);

        var bisHeute = Rueckblick(ReviewSpan.GleicherZeitraum).Comparison;
        var ganzesJahr = Rueckblick(ReviewSpan.GanzeKalenderjahre).Comparison;

        Assert.Equal(10_000, bisHeute.CurrentTotalCents);
        Assert.Equal(109_000, ganzesJahr.CurrentTotalCents);
    }

    // ================= Einnahmen =================

    [Fact]
    public void Einnahmen_zaehlen_nicht_in_die_Kategoriezeilen()
    {
        _expenses.Create(_freizeitId, 20_000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            _freizeitId, 500_000, new DateOnly(2026, 3, 15), _otherId,
            settledDate: new DateOnly(2026, 3, 16), isIncome: true);

        var ergebnis = Rueckblick();

        Assert.Equal(20_000, Finde(ergebnis.Comparison, _freizeitId).CurrentCents);
        Assert.Equal(500_000, ergebnis.Headline.Einnahmen.CurrentCents);
    }

    /// <summary>
    /// Eine Einnahme, die noch aussteht, ist noch kein Geld. Dieselbe
    /// Regel wie im Diagramm der Startseite.
    /// </summary>
    [Fact]
    public void Eine_offene_Einnahme_erhoeht_die_Einnahmen_Kennzahl_nicht()
    {
        _expenses.Create(
            _freizeitId, 500_000, new DateOnly(2026, 3, 15), _otherId, isIncome: true);

        Assert.Equal(0, Rueckblick().Headline.Einnahmen.CurrentCents);
    }

    [Fact]
    public void Netto_ist_Einnahmen_minus_Ausgaben()
    {
        _expenses.Create(_freizeitId, 30_000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            _freizeitId, 100_000, new DateOnly(2026, 3, 15), _otherId,
            settledDate: new DateOnly(2026, 3, 16), isIncome: true);

        var kennzahlen = Rueckblick().Headline;

        Assert.Equal(70_000, kennzahlen.Netto.CurrentCents);
    }

    /// <summary>
    /// Die Ausgabenkachel wird nicht eigens abgefragt, sondern kommt aus
    /// derselben Summe wie die Tabelle. Sie KANN deshalb nicht abweichen -
    /// dieser Test haelt genau das fest.
    /// </summary>
    [Fact]
    public void Die_Ausgaben_Kennzahl_stimmt_mit_der_Summenzeile_ueberein()
    {
        _expenses.Create(_stromId, 30_000, new DateOnly(2026, 1, 5), _selfId);
        _expenses.Create(_freizeitId, 45_000, new DateOnly(2026, 2, 5), _selfId);
        _expenses.Create(_wohnenId, 12_000, new DateOnly(2025, 2, 5), _selfId);

        var ergebnis = Rueckblick();

        Assert.Equal(
            ergebnis.Comparison.CurrentTotalCents, ergebnis.Headline.Ausgaben.CurrentCents);
        Assert.Equal(
            ergebnis.Comparison.PreviousTotalCents, ergebnis.Headline.Ausgaben.PreviousCents);
        Assert.Equal(75_000, ergebnis.Headline.Ausgaben.CurrentCents);
    }

    // ================= Zeilen, die wegfallen und bleiben =================

    [Fact]
    public void Eine_Kategorie_ohne_Buchung_in_beiden_Jahren_faellt_aus_der_Tabelle()
    {
        _expenses.Create(_freizeitId, 20_000, new DateOnly(2026, 3, 1), _selfId);

        var vergleich = Rueckblick().Comparison;

        Assert.Null(Suche(vergleich.Roots, _wohnenId));
        Assert.NotNull(Suche(vergleich.Roots, _freizeitId));
    }

    /// <summary>
    /// Archiviert heisst "wird nicht mehr bebucht", nicht "hat es nie
    /// gegeben". Die Historie bleibt stehen (Regel 8).
    /// </summary>
    [Fact]
    public void Eine_archivierte_Kategorie_bleibt_mit_ihren_Werten_stehen()
    {
        _expenses.Create(_freizeitId, 60_000, new DateOnly(2025, 3, 1), _selfId);
        _categories.Archive(_freizeitId, includeDescendants: false);

        var freizeit = Finde(Rueckblick().Comparison, _freizeitId);

        Assert.True(freizeit.IsArchived);
        Assert.Equal(60_000, freizeit.PreviousCents);
        Assert.Equal(0, freizeit.CurrentCents);
    }

    /// <summary>
    /// Beim Zusammenfuehren wandern ALLE Buchungen der Quelle zum Ziel -
    /// auch die des Vorjahres. Der Vergleich bleibt dadurch von selbst
    /// stimmig: es entsteht weder eine verwaiste Zeile noch ein
    /// Scheinsprung. Hier festgehalten, damit niemand spaeter eine
    /// Sonderbehandlung einbaut, die es nicht braucht.
    /// </summary>
    [Fact]
    public void Ein_Zusammenfuehren_verschiebt_beide_Jahre_gemeinsam()
    {
        _expenses.Create(_freizeitId, 20_000, new DateOnly(2025, 3, 1), _selfId);
        _expenses.Create(_freizeitId, 30_000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_stromId, 10_000, new DateOnly(2025, 4, 1), _selfId);
        _expenses.Create(_stromId, 10_000, new DateOnly(2026, 4, 1), _selfId);

        _categories.Merge(_freizeitId, _stromId);

        var vergleich = Rueckblick().Comparison;
        var strom = Finde(vergleich, _stromId);

        Assert.Null(Suche(vergleich.Roots, _freizeitId));
        Assert.Equal(30_000, strom.PreviousCents);
        Assert.Equal(40_000, strom.CurrentCents);
    }

    // ================= Jahresauswahl =================

    [Fact]
    public void Angeboten_werden_nur_Jahre_mit_Buchungen_das_juengste_zuerst()
    {
        _expenses.Create(_freizeitId, 1_000, new DateOnly(2023, 3, 1), _selfId);
        _expenses.Create(_freizeitId, 1_000, new DateOnly(2026, 3, 1), _selfId);

        Assert.Equal(new[] { 2026, 2023 }, _service.JahreMitBuchungen());
    }

    [Fact]
    public void Ohne_jede_Buchung_gibt_es_kein_Jahr_zur_Auswahl()
    {
        Assert.Empty(_service.JahreMitBuchungen());
    }

    // ================= Lage =================

    [Fact]
    public void Eine_leere_Datenbank_fuehrt_zum_Erfassungsangebot()
    {
        Assert.Equal(ReviewDataState.NochNichtsErfasst, Rueckblick().State);
    }

    [Fact]
    public void Ohne_Vorjahresbuchung_meldet_der_Rueckblick_das_fehlende_Vergleichsjahr()
    {
        _expenses.Create(_freizeitId, 200_000, new DateOnly(2026, 3, 1), _selfId);

        Assert.Equal(ReviewDataState.VorjahrOhneBuchung, Rueckblick().State);
    }
}
