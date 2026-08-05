using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Abfragen hinter dem Bereich "Ausgabenliste":
/// <see cref="ExpenseRepository.Query"/>, <see cref="ExpenseRepository.Summarize"/>
/// und <see cref="ExpenseRepository.DeleteMany"/>. Bewusst in einer eigenen
/// Datei, weil die Filter- und Sortierfaelle einen gemeinsamen, etwas
/// reichhaltigeren Datenbestand brauchen als die CRUD-Tests in
/// <see cref="ExpenseRepositoryTests"/>.
/// </summary>
public class ExpenseListQueryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly ExpenseRepository _expenses;
    private readonly CategoryRepository _categories;
    private readonly int _selfId;
    private readonly int _otherId;

    private readonly int _wohnenId;
    private readonly int _nebenkostenId;
    private readonly int _stromId;
    private readonly int _pferdeId;

    public ExpenseListQueryTests()
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
        _pferdeId = _categories.Create("Pferde", null).Id;
    }

    public void Dispose() => _connection.Dispose();

    // Filter ohne zeitliche Einschraenkung; alles Weitere optional. Haelt
    // die Tests darauf beschraenkt, was sie tatsaechlich einschraenken.
    private static ReportFilter Alles(
        int? categoryRootId = null,
        int? payerId = null,
        SettlementStatus status = SettlementStatus.Alle,
        string? searchText = null,
        int? recurringExpenseId = null,
        PayerScope payerScope = PayerScope.All) => new()
    {
        From = DateOnly.MinValue,
        To = DateOnly.MaxValue,
        CategoryRootId = categoryRootId,
        PayerId = payerId,
        Status = status,
        SearchText = searchText,
        RecurringExpenseId = recurringExpenseId,
        PayerScope = payerScope,
    };

    private IReadOnlyList<ExpenseListItem> Query(
        ReportFilter filter,
        ExpenseSortColumn sort = ExpenseSortColumn.Datum,
        bool ascending = false) => _expenses.Query(filter, sort, ascending);

    // ---------------------------------------------------------------
    // Verbundene Anzeigewerte
    // ---------------------------------------------------------------

    [Fact]
    public void Query_liefert_den_vollen_Kategoriepfad_ab_der_Wurzel()
    {
        _expenses.Create(_stromId, 1000, new DateOnly(2026, 3, 1), _selfId);

        var item = Assert.Single(Query(Alles()));

        Assert.Equal("Wohnen › Nebenkosten › Strom", item.CategoryFullPath);
        Assert.Equal(_stromId, item.CategoryId);
    }

    [Fact]
    public void Query_liefert_Zahlername_und_ob_es_die_eigene_Person_ist()
    {
        _expenses.Create(_wohnenId, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 2000, new DateOnly(2026, 3, 2), _otherId);

        var items = Query(Alles(), ExpenseSortColumn.Betrag, ascending: true);

        Assert.Equal("Ich", items[0].PayerName);
        Assert.True(items[0].PayerIsSelf);
        Assert.Equal("Mitbewohner", items[1].PayerName);
        Assert.False(items[1].PayerIsSelf);
    }

    [Fact]
    public void Query_markiert_aus_einer_Vorlage_erzeugte_Buchungen_mit_Vorlagentitel()
    {
        var vorlagen = new RecurringExpenseRepository(_connection);
        var vorlage = vorlagen.Create(
            _pferdeId, _selfId, 12000, "Stallmiete Ponyhof Weber",
            intervalUnit: "month", intervalCount: 1, anchorDay: 1,
            startDate: new DateOnly(2026, 1, 1), endDate: null);

        _expenses.Create(
            _pferdeId, 12000, new DateOnly(2026, 3, 1), _selfId,
            recurringExpenseId: vorlage.Id);
        _expenses.Create(_pferdeId, 500, new DateOnly(2026, 3, 2), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Betrag, ascending: false);

        Assert.Equal(vorlage.Id, items[0].RecurringExpenseId);
        Assert.Equal("Stallmiete Ponyhof Weber", items[0].RecurringExpenseTitle);

        Assert.Null(items[1].RecurringExpenseId);
        Assert.Null(items[1].RecurringExpenseTitle);
    }

    [Fact]
    public void Query_kann_auf_die_Buchungen_einer_Vorlage_eingeschraenkt_werden()
    {
        // Der Sprung "zeig mir, was diese Vorlage bisher gebucht hat" aus
        // der Vorlagenverwaltung.
        var vorlagen = new RecurringExpenseRepository(_connection);
        var stallmiete = vorlagen.Create(
            _pferdeId, _selfId, 12000, "Stallmiete", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var versicherung = vorlagen.Create(
            _pferdeId, _selfId, 3000, "Versicherung", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _expenses.Create(_pferdeId, 12000, new DateOnly(2026, 1, 1), _selfId, recurringExpenseId: stallmiete.Id);
        _expenses.Create(_pferdeId, 12000, new DateOnly(2026, 2, 1), _selfId, recurringExpenseId: stallmiete.Id);
        _expenses.Create(_pferdeId, 3000, new DateOnly(2026, 1, 1), _selfId, recurringExpenseId: versicherung.Id);
        _expenses.Create(_pferdeId, 500, new DateOnly(2026, 1, 3), _selfId);

        var items = Query(Alles(recurringExpenseId: stallmiete.Id));

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(stallmiete.Id, item.RecurringExpenseId));
    }

    [Fact]
    public void Summarize_beachtet_den_Vorlagenfilter()
    {
        // Liste, Trefferzahl und Summe muessen bei gleichem Filter
        // zwingend dieselbe Menge treffen - dafuer gibt es den gemeinsamen
        // WHERE-Block in ReportFilterSql.
        var vorlagen = new RecurringExpenseRepository(_connection);
        var vorlage = vorlagen.Create(
            _pferdeId, _selfId, 12000, "Stallmiete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _expenses.Create(_pferdeId, 12000, new DateOnly(2026, 1, 1), _selfId, recurringExpenseId: vorlage.Id);
        _expenses.Create(_pferdeId, 12000, new DateOnly(2026, 2, 1), _selfId, recurringExpenseId: vorlage.Id);
        _expenses.Create(_pferdeId, 99900, new DateOnly(2026, 1, 3), _selfId);

        var summary = _expenses.Summarize(Alles(recurringExpenseId: vorlage.Id));

        Assert.Equal(2, summary.Count);
        Assert.Equal(-24000, summary.SumCents);
    }

    [Fact]
    public void Ohne_Vorlagenfilter_erscheinen_weiterhin_alle_Buchungen()
    {
        var vorlagen = new RecurringExpenseRepository(_connection);
        var vorlage = vorlagen.Create(
            _pferdeId, _selfId, 12000, "Stallmiete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _expenses.Create(_pferdeId, 12000, new DateOnly(2026, 1, 1), _selfId, recurringExpenseId: vorlage.Id);
        _expenses.Create(_pferdeId, 500, new DateOnly(2026, 1, 3), _selfId);

        Assert.Equal(2, Query(Alles()).Count);
    }

    // ---------------------------------------------------------------
    // Filter
    // ---------------------------------------------------------------

    [Fact]
    public void Query_Zeitraum_ist_unten_einschliesslich_und_oben_ausschliesslich()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 31), _selfId);
        _expenses.Create(_wohnenId, 300, new DateOnly(2026, 4, 1), _selfId);

        var items = Query(new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 4, 1),
        });

        Assert.Equal(new long[] { 200, 100 }, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_mit_CategoryRootId_umfasst_immer_alle_Unterkategorien()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_nebenkostenId, 200, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(_stromId, 300, new DateOnly(2026, 3, 3), _selfId);
        _expenses.Create(_pferdeId, 999, new DateOnly(2026, 3, 4), _selfId);

        var items = Query(Alles(categoryRootId: _wohnenId));

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(999, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_mit_CategoryRootId_auf_einem_Blatt_liefert_nur_dieses()
    {
        _expenses.Create(_nebenkostenId, 200, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(_stromId, 300, new DateOnly(2026, 3, 3), _selfId);

        var item = Assert.Single(Query(Alles(categoryRootId: _stromId)));

        Assert.Equal(300, item.AmountCents);
    }

    [Fact]
    public void Query_mit_PayerId_schraenkt_auf_einen_Zahler_ein()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _otherId);

        var item = Assert.Single(Query(Alles(payerId: _otherId)));

        Assert.Equal(200, item.AmountCents);
    }

    [Fact]
    public void Query_ohne_PayerId_liefert_alle_Zahler()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _otherId);

        Assert.Equal(2, Query(Alles()).Count);
    }

    // Regel 4: SettledDate IS NULL bedeutet nur bei fremdem Zahler "offen".
    [Fact]
    public void Query_NurOffene_liefert_keine_eigenen_Ausgaben()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _otherId);
        _expenses.Create(
            _wohnenId, 300, new DateOnly(2026, 3, 3), _otherId,
            settledDate: new DateOnly(2026, 3, 5));

        var item = Assert.Single(Query(Alles(status: SettlementStatus.NurOffene)));

        Assert.Equal(200, item.AmountCents);
    }

    [Fact]
    public void Query_NurBeglichene_liefert_nur_Buchungen_mit_SettledDate()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _otherId);
        _expenses.Create(
            _wohnenId, 300, new DateOnly(2026, 3, 3), _otherId,
            settledDate: new DateOnly(2026, 3, 5));

        var item = Assert.Single(Query(Alles(status: SettlementStatus.NurBeglichene)));

        Assert.Equal(300, item.AmountCents);
        Assert.Equal(new DateOnly(2026, 3, 5), item.SettledDate);
    }

    [Fact]
    public void Query_Status_Alle_liefert_auch_die_statuslosen_eigenen_Ausgaben()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _otherId);

        Assert.Equal(2, Query(Alles(status: SettlementStatus.Alle)).Count);
    }

    [Fact]
    public void Query_mit_SearchText_findet_Teiltreffer_in_der_Bemerkung()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId, note: "Stadtwerke Januar");
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _selfId, note: "Hufschmied");
        _expenses.Create(_wohnenId, 300, new DateOnly(2026, 3, 3), _selfId);

        var item = Assert.Single(Query(Alles(searchText: "werke")));

        Assert.Equal(100, item.AmountCents);
    }

    [Fact]
    public void Query_kombiniert_mehrere_Filter_gleichzeitig()
    {
        _expenses.Create(_stromId, 100, new DateOnly(2026, 3, 1), _otherId, note: "Strom Maerz");
        _expenses.Create(_stromId, 200, new DateOnly(2026, 3, 2), _selfId, note: "Strom Maerz");
        _expenses.Create(_stromId, 300, new DateOnly(2025, 3, 1), _otherId, note: "Strom Maerz");
        _expenses.Create(_pferdeId, 400, new DateOnly(2026, 3, 4), _otherId, note: "Strom Maerz");

        var items = Query(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            CategoryRootId = _wohnenId,
            PayerId = _otherId,
            Status = SettlementStatus.NurOffene,
            SearchText = "Strom",
        });

        var item = Assert.Single(items);
        Assert.Equal(100, item.AmountCents);
    }

    // ---------------------------------------------------------------
    // Sortierung
    // ---------------------------------------------------------------

    [Fact]
    public void Query_sortiert_nach_Datum_absteigend_neueste_zuerst()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 1, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 5, 1), _selfId);
        _expenses.Create(_wohnenId, 300, new DateOnly(2026, 3, 1), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Datum, ascending: false);

        Assert.Equal(new long[] { 200, 300, 100 }, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_sortiert_nach_Datum_aufsteigend()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 1, 1), _selfId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 5, 1), _selfId);
        _expenses.Create(_wohnenId, 300, new DateOnly(2026, 3, 1), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Datum, ascending: true);

        Assert.Equal(new long[] { 100, 300, 200 }, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_sortiert_nach_Betrag_und_beachtet_negative_Erstattungen()
    {
        _expenses.Create(_wohnenId, 500, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_wohnenId, -200, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 3), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Betrag, ascending: true);

        Assert.Equal(new long[] { -200, 100, 500 }, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_sortiert_nach_Kategoriepfad()
    {
        _expenses.Create(_pferdeId, 100, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_stromId, 200, new DateOnly(2026, 3, 2), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Kategorie, ascending: true);

        Assert.Equal(
            new[] { "Pferde", "Wohnen › Nebenkosten › Strom" },
            items.Select(i => i.CategoryFullPath));
    }

    [Fact]
    public void Query_sortiert_nach_Zahler()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _otherId);
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Zahler, ascending: true);

        Assert.Equal(new[] { "Ich", "Mitbewohner" }, items.Select(i => i.PayerName));
    }

    // Eigene Ausgaben haben keinen Status (Regel 4) und sortieren deshalb
    // hinter offen und beglichen, statt mit einem der beiden zu verschmelzen.
    [Fact]
    public void Query_sortiert_nach_Status_offen_dann_beglichen_dann_ohne_Status()
    {
        _expenses.Create(_wohnenId, 300, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            _wohnenId, 200, new DateOnly(2026, 3, 2), _otherId,
            settledDate: new DateOnly(2026, 3, 5));
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 3), _otherId);

        var items = Query(Alles(), ExpenseSortColumn.Status, ascending: true);

        Assert.Equal(new long[] { 100, 200, 300 }, items.Select(i => i.AmountCents));
    }

    [Fact]
    public void Query_sortiert_nach_Bemerkung()
    {
        _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId, note: "Zahnarzt");
        _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _selfId, note: "apotheke");

        var items = Query(Alles(), ExpenseSortColumn.Bemerkung, ascending: true);

        Assert.Equal(new[] { "apotheke", "Zahnarzt" }, items.Select(i => i.Note));
    }

    [Fact]
    public void Query_sortiert_bei_gleichem_Wert_stabil_nach_Id_absteigend()
    {
        var erste = _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        var zweite = _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);

        var items = Query(Alles(), ExpenseSortColumn.Datum, ascending: true);

        Assert.Equal(new[] { zweite.Id, erste.Id }, items.Select(i => i.Id));
    }

    // ---------------------------------------------------------------
    // Summenzeile
    // ---------------------------------------------------------------

    [Fact]
    public void Summarize_liefert_Anzahl_und_Summe_desselben_Filterergebnisses()
    {
        _expenses.Create(_stromId, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(_nebenkostenId, 2500, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(_pferdeId, 9999, new DateOnly(2026, 3, 3), _selfId);

        var filter = Alles(categoryRootId: _wohnenId);

        var summary = _expenses.Summarize(filter);

        Assert.Equal(2, summary.Count);
        Assert.Equal(-3500, summary.SumCents);
        Assert.Equal(summary.Count, Query(filter).Count);
        // Summarize liefert das vorzeichenbehaftete Ergebnis (Ausgabe
        // negativ), Query die rohen, immer positiven Betraege - deshalb
        // hier mit demselben Vorzeichen wie Format Signed nachgerechnet.
        Assert.Equal(
            summary.SumCents,
            Query(filter).Sum(i => i.IsIncome ? i.AmountCents : -i.AmountCents));
    }

    [Fact]
    public void Summarize_verrechnet_eine_beglichene_Einnahme_positiv_gegen_die_Ausgabe()
    {
        // Ersetzt die frueher separat erfasste "negative Erstattung": ein
        // tatsaechlicher Rueckfluss wird jetzt als Einnahme gebucht.
        _expenses.Create(_wohnenId, 5000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            _wohnenId, 1500, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));

        var summary = _expenses.Summarize(Alles());

        Assert.Equal(2, summary.Count);
        Assert.Equal(-3500, summary.SumCents);
    }

    // PayerScope.SelfAndOpen wird bisher nirgends in der Ausgabenliste
    // gesetzt, aber die WHERE-Klausel ist mit der Auswertung geteilt
    // (ReportFilterSql) und muss dieselbe Treffermenge liefern - siehe
    // ReportRepositoryTests.PayerScope_SelfAndOpen_zaehlt_eine_beglichene_fremde_Einnahme_positiv_mit.
    [Fact]
    public void Summarize_beachtet_PayerScope_SelfAndOpen_auch_fuer_beglichene_fremde_Einnahmen()
    {
        _expenses.Create(_wohnenId, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            _wohnenId, 400, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));
        _expenses.Create(
            _wohnenId, 900, new DateOnly(2026, 3, 3), _otherId, isIncome: true);

        var summary = _expenses.Summarize(Alles(payerScope: PayerScope.SelfAndOpen));

        // -1000 (eigene Ausgabe) + 400 (beglichene fremde Einnahme) + 0
        // (offene fremde Einnahme, zaehlt aber zur Trefferzahl) = -600.
        Assert.Equal(3, summary.Count);
        Assert.Equal(-600, summary.SumCents);
    }

    [Fact]
    public void Summarize_ohne_Treffer_liefert_Null_statt_NULL()
    {
        var summary = _expenses.Summarize(Alles());

        Assert.Equal(0, summary.Count);
        Assert.Equal(0, summary.SumCents);
    }

    // ---------------------------------------------------------------
    // Sammel-Loeschen
    // ---------------------------------------------------------------

    [Fact]
    public void DeleteMany_entfernt_genau_die_uebergebenen_Ausgaben()
    {
        var eins = _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);
        var zwei = _expenses.Create(_wohnenId, 200, new DateOnly(2026, 3, 2), _selfId);
        var drei = _expenses.Create(_wohnenId, 300, new DateOnly(2026, 3, 3), _selfId);

        _expenses.DeleteMany(new[] { eins.Id, drei.Id });

        Assert.Null(_expenses.GetById(eins.Id));
        Assert.NotNull(_expenses.GetById(zwei.Id));
        Assert.Null(_expenses.GetById(drei.Id));
    }

    [Fact]
    public void DeleteMany_mit_leerer_Liste_veraendert_nichts()
    {
        var eins = _expenses.Create(_wohnenId, 100, new DateOnly(2026, 3, 1), _selfId);

        _expenses.DeleteMany(Array.Empty<int>());

        Assert.NotNull(_expenses.GetById(eins.Id));
    }
}
