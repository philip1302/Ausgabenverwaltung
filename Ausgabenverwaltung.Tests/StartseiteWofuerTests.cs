using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Karte "Wofuer diesen Monat" auf der Startseite - der ganze Weg:
/// gebuchte Kategorie -> oberster Ast -> Zeile auf der Karte -> Klick ->
/// die Buchungen in der Ausgabenliste.
///
/// Gebucht wird relativ zum heutigen Tag, weil die Karte immer "diesen
/// Monat" meint (wie StartseiteVerlaufTests).
/// </summary>
public class StartseiteWofuerTests : IDisposable
{
    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly DieserMonat = new(Heute.Year, Heute.Month, 1);

    private readonly IDbConnection _connection;
    private readonly ExpenseRepository _ausgaben;
    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly TestEinstellungen _einstellungen = new();

    private readonly int _ichId;
    private readonly int _andererId;

    private readonly int _wohnen;
    private readonly int _strom;
    private readonly int _essen;

    public StartseiteWofuerTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _ausgaben = new ExpenseRepository(_connection);
        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);

        _ichId = _personen.Create("Ich", isSelf: true).Id;
        _andererId = _personen.Create("Mitbewohner").Id;

        _wohnen = _kategorien.Create("Wohnen", parentId: null).Id;
        _strom = _kategorien.Create("Strom", _wohnen).Id;
        _essen = _kategorien.Create("Essen", parentId: null).Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    private StartseiteViewModel NeueSeite() => new(
        _ausgaben,
        new OpenItemsRepository(_connection),
        new RecurringExpenseRepository(_connection),
        new ReportRepository(_connection),
        _kategorien,
        new WeakReferenceMessenger());

    private AusgabenlisteViewModel NeueListe() => new(
        _ausgaben, _kategorien, _personen, _einstellungen.Store, new WeakReferenceMessenger());

    private int Buche(int kategorieId, long cents, int? zahlerId = null, DateOnly? datum = null)
        => _ausgaben.Create(
            kategorieId, cents, datum ?? DieserMonat, zahlerId ?? _ichId).Id;

    [Fact]
    public void Ohne_Ausgaben_bleibt_die_Karte_leer()
    {
        var seite = NeueSeite();

        Assert.False(seite.MonatsKategorienVorhanden);
        Assert.Empty(seite.MonatsKategorien);
    }

    [Fact]
    public void Die_Ueberschrift_nennt_den_Monat()
    {
        Buche(_essen, 5000);

        Assert.Equal(
            "Wofür im " + DieserMonat.ToString("MMMM", Kultur.DeDe),
            NeueSeite().MonatsKategorienUeberschrift);
    }

    /// <summary>
    /// Gebucht wird auf das Blatt, gezeigt wird der Ast ganz oben - sonst
    /// ergaeben die Anteile nicht den ganzen Monat, und die Karte waere
    /// eine Liste von Unterkategorien.
    /// </summary>
    [Fact]
    public void Eine_Unterkategorie_zaehlt_unter_ihrem_obersten_Ast()
    {
        Buche(_strom, 6000);
        Buche(_wohnen, 4000);

        var zeile = Assert.Single(NeueSeite().MonatsKategorien);

        Assert.Equal("Wohnen", zeile.Name);
        Assert.Equal(_wohnen, zeile.KategorieId);
    }

    [Fact]
    public void Die_groesste_Kategorie_steht_oben_und_kennt_ihren_Anteil()
    {
        Buche(_essen, 25000);
        Buche(_wohnen, 75000);

        var zeilen = NeueSeite().MonatsKategorien;

        Assert.Equal(["Wohnen", "Essen"], zeilen.Select(z => z.Name));
        Assert.Equal(0.75, zeilen[0].Anteil, precision: 6);
        Assert.Equal("75 %", zeilen[0].AnteilText);
    }

    /// <summary>
    /// Die Karte zaehlt wie die Ausgabenkachel darueber: eigene Ausgaben
    /// UND noch offene fremde, denn beides traegt der Anwender. Eine
    /// Einnahme gehoert nicht dazu - sonst stuende sie unter "wofuer".
    /// </summary>
    [Fact]
    public void Gezaehlt_wird_dasselbe_wie_in_der_Ausgabenkachel()
    {
        Buche(_essen, 5000);
        Buche(_essen, 3000, _andererId);
        _ausgaben.Create(_essen, 20000, DieserMonat, _andererId, settledDate: DieserMonat, isIncome: true);

        var zeile = Assert.Single(NeueSeite().MonatsKategorien);

        // 50 € selbst gezahlt und 30 € ausgelegt - die Einnahme bleibt draussen.
        Assert.Equal(EuroText.Format(8000), zeile.BetragText);
    }

    [Fact]
    public void Ausgaben_anderer_Monate_zaehlen_nicht_mit()
    {
        Buche(_essen, 5000);
        Buche(_wohnen, 90000, datum: DieserMonat.AddMonths(-1));

        var zeile = Assert.Single(NeueSeite().MonatsKategorien);
        Assert.Equal("Essen", zeile.Name);
    }

    /// <summary>
    /// Der Sammeleintrag fuehrt nirgendwohin: er steht fuer mehrere
    /// Kategorien, und ein Sprung muesste sich fuer eine entscheiden.
    /// </summary>
    [Fact]
    public void Die_Sammelzeile_laesst_sich_nicht_anklicken()
    {
        for (var i = 0; i < 8; i++)
        {
            Buche(_kategorien.Create($"K{i}", parentId: null).Id, 1000 * (i + 1));
        }

        var zeilen = NeueSeite().MonatsKategorien;
        var letzte = zeilen[^1];

        Assert.StartsWith("Übrige", letzte.Name);
        Assert.False(letzte.Anklickbar);
        Assert.Null(letzte.KategorieId);
        Assert.All(zeilen.Take(zeilen.Count - 1), zeile => Assert.True(zeile.Anklickbar));
    }

    /// <summary>
    /// Der Klick fuehrt in genau die Buchungen hinter der Zahl: dieser
    /// Monat, dieser Ast (mit Unterkategorien), nur Ausgaben, nur
    /// getragene. Verdrahtet wie in MainViewModel.
    /// </summary>
    [Fact]
    public void Der_Klick_zeigt_genau_die_Buchungen_hinter_der_Zahl()
    {
        var strombuchung = Buche(_strom, 6000);
        var wohnbuchung = Buche(_wohnen, 4000);
        var offeneAuslage = Buche(_wohnen, 2000, _andererId);
        Buche(_essen, 9000);

        // Schon zurueckbekommen - traegt zur Zahl auf der Karte nichts bei.
        _ausgaben.Create(_wohnen, 50000, DieserMonat, _andererId, settledDate: DieserMonat);

        var seite = NeueSeite();
        var liste = NeueListe();

        seite.KategorieAngefordert += (_, sprung) => liste.ZeigeKategorieAusgabenImMonat(
            sprung.KategorieId, sprung.Von, sprung.BisEinschliesslich);

        var wohnen = seite.MonatsKategorien.Single(z => z.Name == "Wohnen");
        seite.KategorieOeffnenCommand.Execute(wohnen);

        Assert.Equal(
            [strombuchung, wohnbuchung, offeneAuslage],
            liste.Zeilen.Select(z => z.Id).Order().ToList());
    }

    [Fact]
    public void Der_Klick_setzt_den_Monat_der_Kachel_als_Zeitraum()
    {
        Buche(_essen, 5000);

        var seite = NeueSeite();
        var liste = NeueListe();

        seite.KategorieAngefordert += (_, sprung) => liste.ZeigeKategorieAusgabenImMonat(
            sprung.KategorieId, sprung.Von, sprung.BisEinschliesslich);

        seite.KategorieOeffnenCommand.Execute(seite.MonatsKategorien[0]);

        Assert.Equal(DieserMonat.ToString("dd.MM.yyyy"), liste.VonText);
        Assert.Equal(
            DieserMonat.AddMonths(1).AddDays(-1).ToString("dd.MM.yyyy"), liste.BisText);
    }

    [Fact]
    public void Der_Hinweis_an_der_Zeile_verspricht_den_Sprung()
    {
        Buche(_essen, 5000);

        var zeile = NeueSeite().MonatsKategorien[0];

        Assert.Contains("Essen", zeile.Hinweis);
        Assert.Contains("Klicken zeigt diese Buchungen", zeile.Hinweis);
    }
}
