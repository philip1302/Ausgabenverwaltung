using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Einfaerbung der Kreuztabelle nach Betragshoehe.
///
/// Geprueft wird hier nicht die Farbe - die waehlt die Ansicht ueber die
/// Merkmale der Zelle -, sondern WELCHE Zelle ueberhaupt eine Stufe
/// bekommt. Genau daran haengt, ob die Einfaerbung etwas aussagt.
/// </summary>
public class ReportEinfaerbungTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly TestEinstellungen _einstellungen = new();

    private readonly CategoryRepository _categories;
    private readonly ExpenseRepository _expenses;

    private readonly int _selfId;
    private readonly int _otherId;
    private readonly int _wohnenId;
    private readonly int _stromId;
    private readonly int _freizeitId;

    public ReportEinfaerbungTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _categories = new CategoryRepository(_connection);
        _expenses = new ExpenseRepository(_connection);

        var people = new PersonRepository(_connection);
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;

        _wohnenId = _categories.Create("Wohnen", null).Id;
        _stromId = _categories.Create("Strom", _wohnenId).Id;
        _freizeitId = _categories.Create("Freizeit", null).Id;
    }

    public void Dispose()
    {
        _einstellungen.Dispose();
        _connection.Dispose();
    }

    // Eigene Messenger-Instanz je Seite (Regel 14).
    private ReportViewModel NeueSeite() => new(
        new ReportRepository(_connection),
        _expenses,
        _categories,
        new PersonRepository(_connection),
        _einstellungen.Store,
        new WeakReferenceMessenger());

    private void Buche(int kategorieId, long cents, DateOnly datum) =>
        _expenses.Create(kategorieId, cents, datum, _selfId);

    /// <summary>
    /// Eine Einnahme zaehlt erst als solche, wenn sie beglichen ist
    /// (Regel 4) - deshalb mit Begleichungsdatum und fremdem Zahler.
    /// </summary>
    private void BucheEinnahme(int kategorieId, long cents, DateOnly datum) =>
        _expenses.Create(
            kategorieId, cents, datum, _otherId,
            settledDate: datum, isIncome: true);

    private static ReportZeile Zeile(ReportViewModel seite, string name) =>
        seite.Zeilen.Single(z => z.Name == name);

    // ---------------- Was eine Stufe bekommt ----------------

    [Fact]
    public void Die_groesste_Ausgabe_traegt_die_hoechste_Stufe()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();

        Assert.Equal(4, Zeile(seite, "Wohnen").Zellen.Max(z => z.Stufe));
        Assert.Equal(1, Zeile(seite, "Freizeit").Zellen.Max(z => z.Stufe));
    }

    [Fact]
    public void Eine_leere_Zelle_bleibt_ohne_Stufe()
    {
        Buche(_wohnenId, 50000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 20000, new DateOnly(2026, 6, 5));

        var seite = NeueSeite();

        // Freizeit hat im Maerz nichts - die Zelle traegt einen Bindestrich
        // und darf keinen Ton bekommen.
        var leere = Zeile(seite, "Freizeit").Zellen.Where(z => !z.HatWerte);

        Assert.NotEmpty(leere);
        Assert.All(leere, z => Assert.Equal(0, z.Stufe));
    }

    /// <summary>
    /// Eine Zelle mit Einnahmenueberhang traegt bereits ihre gruene
    /// Auszeichnung. Beides uebereinanderzulegen macht beides unlesbar.
    /// </summary>
    [Fact]
    public void Eine_Zelle_mit_Einnahmenueberhang_bleibt_ungefaerbt()
    {
        BucheEinnahme(_freizeitId, 80000, new DateOnly(2026, 3, 5));
        Buche(_wohnenId, 50000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();
        var einnahme = Zeile(seite, "Freizeit").Zellen.Single(z => z.IstEinnahme);

        Assert.Equal(0, einnahme.Stufe);
    }

    /// <summary>
    /// Summenzeile und Summenspalte sind Rechnungen ueber die Zellen und
    /// laegen zwangslaeufig ganz oben - sie wuerden die Skala nach oben
    /// ziehen und saehen selbst immer am schwersten aus.
    /// </summary>
    [Fact]
    public void Die_Summenspalte_bleibt_ungefaerbt()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 4, 5));

        var seite = NeueSeite();

        Assert.Equal(0, Zeile(seite, "Wohnen").Summe.Stufe);
        Assert.Equal(0, Zeile(seite, "Freizeit").Summe.Stufe);
    }

    [Fact]
    public void Die_Summenzeile_bleibt_ungefaerbt()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 4, 5));

        var seite = NeueSeite();
        var summenZeile = seite.SummenZeile;

        Assert.NotNull(summenZeile);
        Assert.All(summenZeile.Zellen, z => Assert.Equal(0, z.Stufe));
        Assert.Equal(0, summenZeile.Summe.Stufe);
    }

    // ---------------- Der Umschalter ----------------

    [Fact]
    public void Abgeschaltet_traegt_keine_einzige_Zelle_eine_Stufe()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();
        seite.WerteEinfaerben = false;

        Assert.All(
            seite.Zeilen.SelectMany(z => z.Zellen),
            zelle => Assert.Equal(0, zelle.Stufe));
    }

    [Fact]
    public void Wieder_angeschaltet_sind_die_Stufen_zurueck()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();
        seite.WerteEinfaerben = false;
        seite.WerteEinfaerben = true;

        Assert.Contains(
            seite.Zeilen.SelectMany(z => z.Zellen),
            zelle => zelle.Stufe > 0);
    }

    [Fact]
    public void Der_Schalter_wird_gemerkt()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();
        seite.WerteEinfaerben = false;

        Assert.False(_einstellungen.Lies().ReportHeatmap);

        // Und beim naechsten Start gilt der gemerkte Stand.
        Assert.False(NeueSeite().WerteEinfaerben);
    }

    [Fact]
    public void Ohne_gemerkten_Stand_ist_die_Einfaerbung_an()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));

        Assert.True(NeueSeite().WerteEinfaerben);
    }

    // ---------------- Stabilitaet beim Aufklappen ----------------

    /// <summary>
    /// Der wichtigste Fall: die Stufen haengen an der Auswertung, nicht an
    /// der Anzeige. Sonst wechselten beim Aufklappen die Farben von
    /// Zellen, an denen sich nichts geaendert hat.
    /// </summary>
    [Fact]
    public void Das_Aufklappen_aendert_die_Stufen_der_uebrigen_Zeilen_nicht()
    {
        Buche(_wohnenId, 90000, new DateOnly(2026, 3, 5));
        Buche(_stromId, 4000, new DateOnly(2026, 3, 5));
        Buche(_freizeitId, 1000, new DateOnly(2026, 3, 5));

        var seite = NeueSeite();
        var vorher = Zeile(seite, "Freizeit").Zellen.Select(z => z.Stufe).ToList();

        seite.ZeileUmschaltenCommand.Execute(Zeile(seite, "Wohnen"));

        var nachher = Zeile(seite, "Freizeit").Zellen.Select(z => z.Stufe).ToList();

        Assert.Equal(vorher, nachher);
    }
}
