using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Aufschluesselung fuer das Diagramm der Startseite. Das ist der
/// eigentliche Prueffall des ganzen Diagramms: ein Balken kann noch so
/// schoen sitzen - wenn er die falschen Buchungen zaehlt, ist er
/// wertlos.
///
/// Geprueft wird jeder der sechs Faelle einzeln, denn die Regeln
/// ueberschneiden sich (Zahler, Buchungstyp und Begleichungsstand
/// wirken zusammen) und ein Fehler in einem Fall faellt in einer
/// Gesamtsumme nicht auf.
/// </summary>
public class TrendAuswertungTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly ReportRepository _repository;
    private readonly ExpenseRepository _expenses;
    private readonly OpenItemsRepository _openItems;
    private readonly int _selfId;
    private readonly int _otherId;
    private readonly int _categoryId;

    private static readonly DateRange Maerz2026 =
        new(new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 1));

    public TrendAuswertungTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _repository = new ReportRepository(_connection);
        _expenses = new ExpenseRepository(_connection);
        _openItems = new OpenItemsRepository(_connection);

        var people = new PersonRepository(_connection);
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;

        _categoryId = new CategoryRepository(_connection).Create("Haushalt", null).Id;
    }

    public void Dispose() => _connection.Dispose();

    private TrendGroupResult Auswerten()
    {
        var ergebnis = _repository.EvaluateTrend(Maerz2026, ReportGrouping.Month);
        return Assert.Single(ergebnis);
    }

    // ================= Die sechs Faelle =================

    [Fact]
    public void Eine_eigene_Ausgabe_zaehlt_als_eigene_Ausgabe()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 3, 10), _selfId);

        var zeile = Auswerten();

        Assert.Equal(5000, zeile.OwnExpenseCents);
        Assert.Equal(0, zeile.ForeignOpenExpenseCents);
        Assert.Equal(0, zeile.IncomeCents);
    }

    [Fact]
    public void Eine_offene_Fremdausgabe_zaehlt_als_offen_getragen()
    {
        _expenses.Create(_categoryId, 3000, new DateOnly(2026, 3, 10), _otherId);

        var zeile = Auswerten();

        Assert.Equal(0, zeile.OwnExpenseCents);
        Assert.Equal(3000, zeile.ForeignOpenExpenseCents);
        Assert.Equal(0, zeile.IncomeCents);
    }

    /// <summary>
    /// Ausgeglichen ist ausgeglichen: eine abgerechnete Fremdausgabe
    /// belastet den Anwender nicht mehr und darf deshalb NIRGENDS
    /// auftauchen - auch nicht in einer der beiden Ausgabenspalten.
    /// </summary>
    [Fact]
    public void Eine_beglichene_Fremdausgabe_zaehlt_nirgends()
    {
        var ausgabe = _expenses.Create(_categoryId, 3000, new DateOnly(2026, 3, 10), _otherId);
        _openItems.SetSettledDate(ausgabe.Id, new DateOnly(2026, 3, 20));

        var zeile = Auswerten();

        Assert.Equal(0, zeile.OwnExpenseCents);
        Assert.Equal(0, zeile.ForeignOpenExpenseCents);
        Assert.Equal(0, zeile.IncomeCents);
    }

    [Fact]
    public void Eine_beglichene_Fremdeinnahme_zaehlt_als_Einnahme()
    {
        var einnahme = _expenses.Create(
            _categoryId, 12000, new DateOnly(2026, 3, 10), _otherId, isIncome: true);
        _openItems.SetSettledDate(einnahme.Id, new DateOnly(2026, 3, 20));

        var zeile = Auswerten();

        Assert.Equal(12000, zeile.IncomeCents);
        Assert.Equal(0, zeile.OwnExpenseCents);
        Assert.Equal(0, zeile.ForeignOpenExpenseCents);
    }

    /// <summary>
    /// Der Fall, den die bisherige Startseite falsch gerechnet hat: eine
    /// offene Einnahme ist Geld, das mir jemand schuldet - ich habe es
    /// noch nicht.
    /// </summary>
    [Fact]
    public void Eine_offene_Fremdeinnahme_zaehlt_noch_nicht()
    {
        _expenses.Create(
            _categoryId, 12000, new DateOnly(2026, 3, 10), _otherId, isIncome: true);

        var zeile = Auswerten();

        Assert.Equal(0, zeile.IncomeCents);
        Assert.Equal(0, zeile.OwnExpenseCents);
        Assert.Equal(0, zeile.ForeignOpenExpenseCents);
    }

    /// <summary>
    /// Eine Einnahme, die ich mir selbst zahle, gleicht sich aus - sie
    /// ist kein Zufluss.
    /// </summary>
    [Fact]
    public void Eine_eigene_Einnahme_zaehlt_nicht()
    {
        _expenses.Create(
            _categoryId, 9000, new DateOnly(2026, 3, 10), _selfId, isIncome: true);

        var zeile = Auswerten();

        Assert.Equal(0, zeile.IncomeCents);
        Assert.Equal(0, zeile.OwnExpenseCents);
        Assert.Equal(0, zeile.ForeignOpenExpenseCents);
    }

    /// <summary>
    /// Auch mit gesetztem Begleichungsdatum nicht - Regel 4 laesst das
    /// Feld bei eigenen Buchungen zwar leer, aber ein Altbestand oder
    /// ein Versehen soll die Summe nicht verfaelschen.
    /// </summary>
    [Fact]
    public void Eine_eigene_Einnahme_zaehlt_auch_mit_Begleichungsdatum_nicht()
    {
        var einnahme = _expenses.Create(
            _categoryId, 9000, new DateOnly(2026, 3, 10), _selfId, isIncome: true);
        _openItems.SetSettledDate(einnahme.Id, new DateOnly(2026, 3, 20));

        Assert.Equal(0, Auswerten().IncomeCents);
    }

    // ================= Zusammenspiel =================

    [Fact]
    public void Alle_Sorten_nebeneinander_landen_in_getrennten_Spalten()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 3, 5), _selfId);
        _expenses.Create(_categoryId, 3000, new DateOnly(2026, 3, 6), _otherId);

        var einnahme = _expenses.Create(
            _categoryId, 20000, new DateOnly(2026, 3, 7), _otherId, isIncome: true);
        _openItems.SetSettledDate(einnahme.Id, new DateOnly(2026, 3, 25));

        var beglichen = _expenses.Create(_categoryId, 7000, new DateOnly(2026, 3, 8), _otherId);
        _openItems.SetSettledDate(beglichen.Id, new DateOnly(2026, 3, 26));

        var zeile = Auswerten();

        Assert.Equal(5000, zeile.OwnExpenseCents);
        Assert.Equal(3000, zeile.ForeignOpenExpenseCents);
        Assert.Equal(20000, zeile.IncomeCents);
    }

    /// <summary>
    /// Die Rechnung, die im Diagramm als Netto-Balken erscheint.
    /// </summary>
    [Fact]
    public void Das_Netto_ist_Einnahmen_minus_getragener_Ausgaben()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 3, 5), _selfId);
        _expenses.Create(_categoryId, 3000, new DateOnly(2026, 3, 6), _otherId);

        var einnahme = _expenses.Create(
            _categoryId, 20000, new DateOnly(2026, 3, 7), _otherId, isIncome: true);
        _openItems.SetSettledDate(einnahme.Id, new DateOnly(2026, 3, 25));

        var zeile = Auswerten();
        var wert = new Ausgabenverwaltung.Core.Charts.PeriodValue(
            zeile.GroupKey, "Mär 2026",
            zeile.OwnExpenseCents, zeile.ForeignOpenExpenseCents, zeile.IncomeCents);

        Assert.Equal(8000, wert.BorneExpenseCents);
        Assert.Equal(12000, wert.NetCents);
    }

    [Fact]
    public void Buchungen_werden_nach_Monaten_getrennt()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 3, 10), _selfId);
        _expenses.Create(_categoryId, 8000, new DateOnly(2026, 4, 10), _selfId);

        var ergebnis = _repository.EvaluateTrend(
            new DateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 1)),
            ReportGrouping.Month);

        Assert.Equal(2, ergebnis.Count);
        Assert.Equal("2026-03", ergebnis[0].GroupKey);
        Assert.Equal(5000, ergebnis[0].OwnExpenseCents);
        Assert.Equal("2026-04", ergebnis[1].GroupKey);
        Assert.Equal(8000, ergebnis[1].OwnExpenseCents);
    }

    [Fact]
    public void Der_Schluessel_passt_zu_dem_der_Zeitabschnitte()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 3, 10), _selfId);

        var zeile = Auswerten();

        // Muss zu ReportPeriods passen, sonst finden Beschriftung und
        // Sprung in die Buchungen den Abschnitt nicht wieder.
        Assert.Equal(
            ReportPeriods.Key(new DateOnly(2026, 3, 10), ReportGrouping.Month),
            zeile.GroupKey);
    }

    [Fact]
    public void Buchungen_ausserhalb_des_Zeitraums_bleiben_draussen()
    {
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 2, 28), _selfId);
        _expenses.Create(_categoryId, 5000, new DateOnly(2026, 4, 1), _selfId);

        Assert.Empty(_repository.EvaluateTrend(Maerz2026, ReportGrouping.Month));
    }

    [Fact]
    public void Ein_Zeitraum_ohne_Buchungen_liefert_eine_leere_Liste()
    {
        Assert.Empty(_repository.EvaluateTrend(Maerz2026, ReportGrouping.Month));
    }
}
