using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

public class ReportRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly ReportRepository _repository;
    private readonly CategoryRepository _categories;
    private readonly ExpenseRepository _expenses;
    private readonly int _selfId;
    private readonly int _otherId;

    public ReportRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _repository = new ReportRepository(_connection);
        _categories = new CategoryRepository(_connection);
        _expenses = new ExpenseRepository(_connection);

        var people = new PersonRepository(_connection);
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Auswertung_ueber_Kategorie_Ast_summiert_alle_drei_Ebenen()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var nebenkosten = _categories.Create("Nebenkosten", wohnen.Id);
        var strom = _categories.Create("Strom", nebenkosten.Id);

        _expenses.Create(wohnen.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(nebenkosten.Id, 2000, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(strom.Id, 3000, new DateOnly(2026, 3, 3), _selfId);

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            CategoryRootIds = [wohnen.Id],
            Grouping = ReportGrouping.Year,
        });

        var group = Assert.Single(result);
        Assert.Equal(-6000, group.SumCents);
        Assert.Equal(3, group.Count);
    }

    [Fact]
    public void Zeitraum_ist_unten_einschliesslich_und_oben_ausschliesslich()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 100, new DateOnly(2026, 3, 1), _selfId);   // From: eingeschlossen
        _expenses.Create(kategorie.Id, 200, new DateOnly(2026, 4, 1), _selfId);   // To: ausgeschlossen

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 4, 1),
            Grouping = ReportGrouping.Month,
        });

        var group = Assert.Single(result);
        Assert.Equal(-100, group.SumCents);
        Assert.Equal(1, group.Count);
    }

    [Fact]
    public void Leeres_Ergebnis_liefert_leere_Liste_statt_null()
    {
        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
        });

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Gruppierung_nach_Quartal_ueber_Jahreswechsel()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 500, new DateOnly(2025, 12, 15), _selfId); // Q4 2025
        _expenses.Create(kategorie.Id, 700, new DateOnly(2026, 1, 15), _selfId);  // Q1 2026

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2025, 10, 1),
            To = new DateOnly(2026, 4, 1),
            Grouping = ReportGrouping.Quarter,
        });

        Assert.Equal(
            new[] { "2025-Q4", "2026-Q1" },
            result.Select(r => r.GroupKey));
        Assert.Equal(-500, result.Single(r => r.GroupKey == "2025-Q4").SumCents);
        Assert.Equal(-700, result.Single(r => r.GroupKey == "2026-Q1").SumCents);
    }

    [Fact]
    public void Zahler_Filter_trennt_eigene_und_fremde_Ausgaben()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 2), _otherId);

        var nurIch = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            PayerScope = PayerScope.Self,
            Grouping = ReportGrouping.Year,
        });
        var nurAndere = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            PayerScope = PayerScope.Others,
            Grouping = ReportGrouping.Year,
        });

        Assert.Equal(-1000, Assert.Single(nurIch).SumCents);
        Assert.Equal(-2500, Assert.Single(nurAndere).SumCents);
    }

    [Fact]
    public void Archivierte_Kategorien_erscheinen_weiterhin_in_der_Auswertung()
    {
        var kategorie = _categories.Create("Altlast", null);
        _expenses.Create(kategorie.Id, 4200, new DateOnly(2026, 3, 1), _selfId);
        _categories.Archive(kategorie.Id, includeDescendants: false);

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            CategoryRootIds = [kategorie.Id],
            Grouping = ReportGrouping.Year,
        });

        var group = Assert.Single(result);
        Assert.Equal(-4200, group.SumCents);
    }

    // PayerId und Status kommen aus demselben Filtermodell wie die
    // Ausgabenliste (ReportFilterSql) und muessen deshalb auch hier wirken.
    [Fact]
    public void PayerId_schraenkt_auf_einen_einzelnen_Zahler_ein()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 2), _otherId);

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            PayerIds = [_otherId],
            Grouping = ReportGrouping.Year,
        });

        Assert.Equal(-2500, Assert.Single(result).SumCents);
    }

    // Regel 4: eigene Ausgaben haben keinen Status und zaehlen weder als
    // offen noch als beglichen.
    [Fact]
    public void Status_NurOffene_laesst_eigene_Ausgaben_aussen_vor()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 2), _otherId);
        _expenses.Create(
            kategorie.Id, 700, new DateOnly(2026, 3, 3), _otherId,
            settledDate: new DateOnly(2026, 3, 9));

        var offene = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Status = SettlementStatus.Offene,
            Grouping = ReportGrouping.Year,
        });
        var beglichene = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Status = SettlementStatus.Beglichene,
            Grouping = ReportGrouping.Year,
        });

        Assert.Equal(-2500, Assert.Single(offene).SumCents);
        Assert.Equal(-700, Assert.Single(beglichene).SumCents);
    }

    // PayerScope.SelfAndOpen mischt bewusst Zahler, Status und Buchungstyp:
    // eigene Buchungen zaehlen immer, fremde Ausgaben nur, solange sie noch
    // offen sind, fremde Einnahmen dagegen erst, sobald sie beglichen sind.
    [Fact]
    public void PayerScope_SelfAndOpen_zaehlt_eigene_und_offene_fremde_Ausgaben_zusammen()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 2), _otherId);
        _expenses.Create(
            kategorie.Id, 700, new DateOnly(2026, 3, 3), _otherId,
            settledDate: new DateOnly(2026, 3, 9));

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            PayerScope = PayerScope.SelfAndOpen,
            Grouping = ReportGrouping.Year,
        });

        // -1000 (eigene) - 2500 (fremd, offen) - die beglichenen 700 fallen
        // heraus.
        var group = Assert.Single(result);
        Assert.Equal(-3500, group.SumCents);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void PayerScope_SelfAndOpen_zaehlt_eine_beglichene_fremde_Einnahme_positiv_mit()
    {
        // Eine beglichene fremde Einnahme faellt beim urspruenglichen Filter
        // komplett heraus (weder Summe noch Trefferzahl) - das war der
        // gemeldete Fehler. Eine noch offene fremde Einnahme erscheint
        // dagegen wie bisher (Trefferzahl), traegt aber 0 zur Summe bei.
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            kategorie.Id, 400, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));
        _expenses.Create(
            kategorie.Id, 900, new DateOnly(2026, 3, 3), _otherId, isIncome: true);

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            PayerScope = PayerScope.SelfAndOpen,
            Grouping = ReportGrouping.Year,
        });

        // -1000 (eigene Ausgabe) + 400 (beglichene fremde Einnahme) + 0
        // (offene fremde Einnahme, zaehlt aber zur Trefferzahl) = -600.
        var group = Assert.Single(result);
        Assert.Equal(-600, group.SumCents);
        Assert.Equal(3, group.Count);
    }

    [Fact]
    public void Evaluate_verrechnet_eine_beglichene_Einnahme_positiv_gegen_die_Ausgabe()
    {
        var kategorie = _categories.Create("Sonstiges", null);

        // 500,00 € Ausgabe (negativ), 200,00 € beglichene (tatsaechlich
        // eingegangene) Einnahme (positiv) -> Summe -300,00 €, nicht
        // -700,00 € (siehe Entities.Expense.IsIncome).
        _expenses.Create(kategorie.Id, 50000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            kategorie.Id, 20000, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Year,
        });

        var group = Assert.Single(result);
        Assert.Equal(-30000, group.SumCents);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void Evaluate_zaehlt_eine_noch_offene_Einnahme_weder_erhoehend_noch_mindernd()
    {
        var kategorie = _categories.Create("Sonstiges", null);

        // Noch nicht eingegangen - darf die Summe nicht veraendern, bis
        // sie ueber die Offene-Posten-Liste als erhalten markiert wurde.
        _expenses.Create(kategorie.Id, 50000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(
            kategorie.Id, 20000, new DateOnly(2026, 3, 2), _otherId, isIncome: true);

        var result = _repository.Evaluate(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Year,
        });

        var group = Assert.Single(result);
        Assert.Equal(-50000, group.SumCents);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void EvaluateMatrix_verrechnet_eine_beglichene_Einnahme_positiv_gegen_die_Ausgabe()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 50000, new DateOnly(2026, 3, 1), _selfId, isIncome: false);
        _expenses.Create(
            kategorie.Id, 20000, new DateOnly(2026, 3, 2), _otherId,
            isIncome: true, settledDate: new DateOnly(2026, 3, 10));

        var cells = _repository.EvaluateMatrix(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Year,
        });

        var cell = Assert.Single(cells);
        Assert.Equal(kategorie.Id, cell.CategoryId);
        Assert.Equal(-30000, cell.SumCents);
        Assert.Equal(2, cell.Count);
    }

    [Fact]
    public void EvaluateMatrix_zaehlt_eine_noch_offene_Einnahme_weder_erhoehend_noch_mindernd()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 50000, new DateOnly(2026, 3, 1), _selfId, isIncome: false);
        _expenses.Create(kategorie.Id, 20000, new DateOnly(2026, 3, 2), _otherId, isIncome: true);

        var cells = _repository.EvaluateMatrix(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Year,
        });

        var cell = Assert.Single(cells);
        Assert.Equal(kategorie.Id, cell.CategoryId);
        Assert.Equal(-50000, cell.SumCents);
        Assert.Equal(2, cell.Count);
    }
}
