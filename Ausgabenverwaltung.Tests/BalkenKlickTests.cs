using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Charts;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Klick auf einen Balken im Diagramm der Startseite.
///
/// Eine Saeule der Detailansicht ist GESTAPELT: unten die selbst
/// gezahlten Ausgaben, darueber das fuer andere Ausgelegte, das noch
/// offen ist. Der Hinweis am Balken verspricht "Klicken zeigt diese
/// Buchungen" - dann muss die Liste danach auch genau diesen Abschnitt
/// zeigen und nicht den ganzen Monat. Wer auf das rote Stueck zielt,
/// sucht die Auslagen und nicht die Einkaeufe daneben.
///
/// Geprueft wird der ganze Weg: Balken -> Klick -> Zeitraum und Art ->
/// Filter der Ausgabenliste -> tatsaechlich geladene Zeilen. Die
/// Verdrahtung in der Mitte bildet nach, was MainViewModel tut.
/// </summary>
public class BalkenKlickTests : IDisposable
{
    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly DieserMonat = new(Heute.Year, Heute.Month, 1);

    // Ein abgeschlossener Monat, weit genug weg vom heutigen Tag: der
    // Balken soll aus fertigen Zahlen bestehen und nicht aus einem Monat,
    // in dem waehrend des Testlaufs noch etwas dazukommen koennte.
    private static readonly DateOnly Buchungstag = DieserMonat.AddMonths(-3).AddDays(4);

    private readonly IDbConnection _connection;
    private readonly ExpenseRepository _ausgaben;
    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly TestEinstellungen _einstellungen = new();

    private readonly int _ichId;
    private readonly int _andererId;
    private readonly int _kategorieId;

    // Die vier Buchungen des geprueften Monats, je eine je Fall.
    private readonly int _eigeneAusgabeId;
    private readonly int _offeneAuslageId;
    private readonly int _begleicheneAuslageId;
    private readonly int _einnahmeId;

    public BalkenKlickTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _ausgaben = new ExpenseRepository(_connection);
        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);

        _ichId = _personen.Create("Ich", isSelf: true).Id;
        _andererId = _personen.Create("Mitbewohner").Id;
        _kategorieId = _kategorien.Create("Wohnen", parentId: null).Id;

        _eigeneAusgabeId = _ausgaben
            .Create(_kategorieId, 5000, Buchungstag, _ichId).Id;

        // Ausgelegt und noch offen - der rote Abschnitt der Saeule.
        _offeneAuslageId = _ausgaben
            .Create(_kategorieId, 7000, Buchungstag, _andererId).Id;

        // Schon zurueckbekommen: taucht im Diagramm NIRGENDS auf und darf
        // deshalb auch beim Klick auf den roten Abschnitt nicht erscheinen.
        _begleicheneAuslageId = _ausgaben
            .Create(_kategorieId, 9000, Buchungstag, _andererId, settledDate: Buchungstag).Id;

        _einnahmeId = _ausgaben.Create(
            _kategorieId, 12000, Buchungstag, _andererId,
            settledDate: Buchungstag, isIncome: true).Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    // Eigene Messenger-Instanz je ViewModel (Regel 14).
    private StartseiteViewModel NeueSeite()
    {
        var seite = new StartseiteViewModel(
            _ausgaben,
            new OpenItemsRepository(_connection),
            new RecurringExpenseRepository(_connection),
            new ReportRepository(_connection),
            _kategorien,
            new WeakReferenceMessenger());

        seite.Aktualisiere();

        // Ohne gemeldete Groesse rechnet das Diagramm keine Balken - die
        // Ansicht meldet sie sonst ueber SizeChanged.
        seite.ZeichenflaecheGeaendert(600, 300);
        return seite;
    }

    private AusgabenlisteViewModel NeueListe() => new(
        _ausgaben, _kategorien, _personen, _einstellungen.Store,
        new WeakReferenceMessenger(), new ToastViewModel());

    /// <summary>
    /// Klickt den ersten Balken der gesuchten Art an und liefert die
    /// Liste, wie sie danach dasteht - verdrahtet wie in MainViewModel.
    /// </summary>
    private AusgabenlisteViewModel NachKlickAuf(BarKind art)
    {
        var seite = NeueSeite();
        var liste = NeueListe();

        seite.ZeitraumAngefordert += (_, sprung) => liste.ZeigeZeitraum(
            sprung.Zeitraum.From, sprung.Zeitraum.ToExclusive.AddDays(-1), sprung.Art);

        var balken = seite.DiagrammBalken.First(b => b.Art == art);
        seite.MonatOeffnenCommand.Execute(balken);

        return liste;
    }

    private static IReadOnlyList<int> Ids(AusgabenlisteViewModel liste)
        => liste.Zeilen.Select(zeile => zeile.Id).ToList();

    [Fact]
    public void Der_Klick_auf_das_Ausgelegte_zeigt_nur_die_offenen_Auslagen()
    {
        var liste = NachKlickAuf(BarKind.ForeignOpenExpenses);

        Assert.Equal([_offeneAuslageId], Ids(liste));
    }

    [Fact]
    public void Der_Klick_auf_die_eigenen_Ausgaben_zeigt_nur_die_selbst_gezahlten()
    {
        var liste = NachKlickAuf(BarKind.OwnExpenses);

        Assert.Equal([_eigeneAusgabeId], Ids(liste));
    }

    /// <summary>
    /// Eine Einnahme zaehlt erst nach der Begleichung (Regel 4) - der
    /// gruene Balken besteht nur aus zugeflossenem Geld, und genau das
    /// muss die Liste danach zeigen.
    /// </summary>
    [Fact]
    public void Der_Klick_auf_die_Einnahmen_zeigt_nur_das_Zugeflossene()
    {
        _ausgaben.Create(_kategorieId, 4000, Buchungstag, _andererId, isIncome: true);

        var liste = NachKlickAuf(BarKind.Income);

        Assert.Equal([_einnahmeId], Ids(liste));
    }

    /// <summary>
    /// Der Netto-Balken ist die Summe von allem - er schraenkt deshalb
    /// als einziger nicht weiter ein.
    /// </summary>
    [Fact]
    public void Der_Klick_auf_den_Nettobalken_zeigt_den_ganzen_Monat()
    {
        // Die vier Buchungen des Aufbaus heben sich im Netto genau auf,
        // und ein Nullwert bekommt keinen Balken. Eine weitere eigene
        // Ausgabe macht daraus einen sichtbaren Ausgabenueberhang.
        var weitereId = _ausgaben.Create(_kategorieId, 3000, Buchungstag, _ichId).Id;

        var seite = NeueSeite();
        seite.AnsichtWaehlenCommand.Execute(StartseiteViewModel.AnsichtNetto);

        var liste = NeueListe();
        seite.ZeitraumAngefordert += (_, sprung) => liste.ZeigeZeitraum(
            sprung.Zeitraum.From, sprung.Zeitraum.ToExclusive.AddDays(-1), sprung.Art);

        var balken = seite.DiagrammBalken.First(
            b => b.IstNettoPositiv || b.IstNettoNegativ);
        seite.MonatOeffnenCommand.Execute(balken);

        Assert.Equal(
            [_eigeneAusgabeId, _offeneAuslageId, _begleicheneAuslageId, _einnahmeId, weitereId],
            Ids(liste).Order().ToList());
    }

    /// <summary>
    /// Der Zeitraum bleibt der Monat des Balkens - die Einschraenkung auf
    /// den Abschnitt kommt zusaetzlich und nicht an seiner Stelle.
    /// </summary>
    [Fact]
    public void Der_Klick_setzt_den_Monat_des_Balkens_als_Zeitraum()
    {
        var liste = NachKlickAuf(BarKind.ForeignOpenExpenses);

        var monatsAnfang = new DateOnly(Buchungstag.Year, Buchungstag.Month, 1);

        Assert.Equal(monatsAnfang.ToString("dd.MM.yyyy"), liste.VonText);
        Assert.Equal(
            monatsAnfang.AddMonths(1).AddDays(-1).ToString("dd.MM.yyyy"), liste.BisText);
    }

    /// <summary>
    /// Was der Balken verspricht, muss er auch halten: der Hinweis sagt
    /// "diese Buchungen", und die Liste zeigt danach genau sie.
    /// </summary>
    [Fact]
    public void Der_Hinweis_am_Balken_verspricht_genau_diese_Buchungen()
    {
        var seite = NeueSeite();

        var offen = seite.DiagrammBalken.First(b => b.IstOffeneFremdausgabe);

        Assert.Contains("Ausgelegt, noch offen", offen.Hinweis);
        Assert.Contains("Klicken zeigt diese Buchungen", offen.Hinweis);
    }
}
