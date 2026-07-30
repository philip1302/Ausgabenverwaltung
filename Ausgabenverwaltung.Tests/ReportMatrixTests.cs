using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Kreuztabelle end-to-end: Aggregation in SQL
/// (<see cref="ReportRepository.EvaluateMatrix"/>) und Zusammenbau
/// (<see cref="ReportMatrixBuilder"/>).
/// </summary>
public class ReportMatrixTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly ReportRepository _repository;
    private readonly CategoryRepository _categories;
    private readonly ExpenseRepository _expenses;
    private readonly int _selfId;
    private readonly int _otherId;

    public ReportMatrixTests()
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

    private ReportMatrix Baue(ReportFilter filter, int? astId = null)
    {
        var zellen = _repository.EvaluateMatrix(filter);
        var baum = _categories.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);

        var wurzeln = astId is int id
            ? new List<CategoryNode> { FindeKnoten(baum, id)! }
            : baum;

        return ReportMatrixBuilder.Build(wurzeln, pfade, zellen, filter.Grouping);
    }

    private static CategoryNode? FindeKnoten(IReadOnlyList<CategoryNode> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Category.Id == id)
            {
                return node;
            }

            if (FindeKnoten(node.Children, id) is { } gefunden)
            {
                return gefunden;
            }
        }

        return null;
    }

    private static ReportFilter Jahr2026(ReportGrouping gruppierung) => new()
    {
        From = new DateOnly(2026, 1, 1),
        To = new DateOnly(2027, 1, 1),
        Grouping = gruppierung,
    };

    // Kern der Kreuztabelle: die Summe einer Oberkategorie enthaelt immer
    // alles darunter - und zwar bereits aus SQL.
    [Fact]
    public void Oberkategorie_enthaelt_alle_Unterkategorien()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var nebenkosten = _categories.Create("Nebenkosten", wohnen.Id);
        var strom = _categories.Create("Strom", nebenkosten.Id);

        _expenses.Create(wohnen.Id, 1000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(nebenkosten.Id, 2000, new DateOnly(2026, 3, 2), _selfId);
        _expenses.Create(strom.Id, 3000, new DateOnly(2026, 3, 3), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Year));

        var wohnenZeile = Assert.Single(matrix.Rows);
        Assert.Equal(6000, wohnenZeile.Total.SumCents);
        Assert.Equal(3, wohnenZeile.Total.Count);

        var nebenkostenZeile = Assert.Single(wohnenZeile.Children);
        Assert.Equal(5000, nebenkostenZeile.Total.SumCents);

        var stromZeile = Assert.Single(nebenkostenZeile.Children);
        Assert.Equal(3000, stromZeile.Total.SumCents);

        // Die Gesamtsumme zaehlt jede Buchung genau einmal, obwohl sie in
        // drei Zeilen steckt.
        Assert.Equal(6000, matrix.Total.SumCents);
        Assert.Equal(3, matrix.Total.Count);
    }

    [Fact]
    public void Werte_verteilen_sich_auf_die_Zeitabschnitte()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 1, 15), _selfId);
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 5), _selfId);
        _expenses.Create(kategorie.Id, 500, new DateOnly(2026, 3, 20), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));
        var zeile = Assert.Single(matrix.Rows);

        Assert.Equal(1000, zeile.Cell("2026-01").SumCents);
        Assert.Equal(3000, zeile.Cell("2026-03").SumCents);
        Assert.Equal(2, zeile.Cell("2026-03").Count);
    }

    // Luecken muessen als Spalte sichtbar bleiben - sonst faellt nicht auf,
    // dass in einem Monat nichts gebucht wurde.
    [Fact]
    public void Zeitabschnitte_ohne_Werte_bleiben_als_Spalte_erhalten()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 1, 15), _selfId);
        _expenses.Create(kategorie.Id, 2000, new DateOnly(2026, 4, 15), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));

        Assert.Equal(
            new[] { "2026-01", "2026-02", "2026-03", "2026-04" },
            matrix.PeriodKeys);

        // Der leere Februar ist als Spalte da, aber ohne Buchung - daran
        // haengt die Darstellung als Bindestrich statt "0,00".
        Assert.False(matrix.Rows[0].Cell("2026-02").HasValues);
        Assert.Equal(0, matrix.Rows[0].Cell("2026-02").SumCents);
    }

    // Eine Zelle mit Summe 0 ist etwas anderes als eine Zelle ohne Buchung.
    [Fact]
    public void Summe_null_aus_Ausgabe_und_Erstattung_gilt_als_belegt()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 5000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(kategorie.Id, -5000, new DateOnly(2026, 3, 2), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));
        var zelle = matrix.Rows[0].Cell("2026-03");

        Assert.True(zelle.HasValues);
        Assert.Equal(0, zelle.SumCents);
        Assert.Equal(2, zelle.Count);
    }

    [Fact]
    public void Spaltensummen_und_Gesamtsumme_zaehlen_jede_Buchung_einmal()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var strom = _categories.Create("Strom", wohnen.Id);
        var freizeit = _categories.Create("Freizeit", null);

        _expenses.Create(strom.Id, 1000, new DateOnly(2026, 1, 10), _selfId);
        _expenses.Create(freizeit.Id, 400, new DateOnly(2026, 1, 20), _selfId);
        _expenses.Create(wohnen.Id, 700, new DateOnly(2026, 2, 10), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));

        Assert.Equal(1400, matrix.ColumnTotal("2026-01").SumCents);
        Assert.Equal(2, matrix.ColumnTotal("2026-01").Count);
        Assert.Equal(700, matrix.ColumnTotal("2026-02").SumCents);
        Assert.Equal(2100, matrix.Total.SumCents);
        Assert.Equal(3, matrix.Total.Count);
    }

    // Ist ein Ast gewaehlt, beginnt die Tabelle dort - die Spaltensummen
    // beziehen sich dann auf diesen Ast.
    [Fact]
    public void Kategorie_Ast_als_Filter_beginnt_die_Tabelle_beim_gewaehlten_Knoten()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var nebenkosten = _categories.Create("Nebenkosten", wohnen.Id);
        var freizeit = _categories.Create("Freizeit", null);

        _expenses.Create(nebenkosten.Id, 2000, new DateOnly(2026, 3, 1), _selfId);
        _expenses.Create(freizeit.Id, 900, new DateOnly(2026, 3, 2), _selfId);

        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Month,
            CategoryRootId = wohnen.Id,
        };

        var matrix = Baue(filter, astId: wohnen.Id);

        var oben = Assert.Single(matrix.Rows);
        Assert.Equal("Wohnen", oben.Name);
        Assert.Equal(2000, matrix.Total.SumCents);
    }

    [Fact]
    public void Kategorien_ohne_Buchungen_stehen_als_leere_Zeile_bereit()
    {
        var benutzt = _categories.Create("Benutzt", null);
        _categories.Create("Ungenutzt", null);
        _expenses.Create(benutzt.Id, 1000, new DateOnly(2026, 3, 1), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));

        // Der Bauplan liefert beide Zeilen; ob die leere angezeigt wird,
        // entscheidet der Schalter in der Oberflaeche.
        Assert.Equal(2, matrix.Rows.Count);
        var leer = matrix.Rows.Single(zeile => zeile.Name == "Ungenutzt");
        Assert.False(leer.Total.HasValues);
    }

    // Derselbe Filter wie in der Ausgabenliste muss auch hier greifen.
    [Fact]
    public void Zahler_und_Volltextfilter_wirken_auch_in_der_Kreuztabelle()
    {
        var kategorie = _categories.Create("Sonstiges", null);
        _expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 3, 1), _selfId, "Baumarkt");
        _expenses.Create(kategorie.Id, 2500, new DateOnly(2026, 3, 2), _otherId, "Baumarkt");
        _expenses.Create(kategorie.Id, 300, new DateOnly(2026, 3, 3), _selfId, "Kino");

        var nurAndere = Baue(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Month,
            PayerScope = PayerScope.Others,
        });

        var nurBaumarkt = Baue(new ReportFilter
        {
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2027, 1, 1),
            Grouping = ReportGrouping.Month,
            SearchText = "Baumarkt",
        });

        Assert.Equal(2500, nurAndere.Total.SumCents);
        Assert.Equal(3500, nurBaumarkt.Total.SumCents);
    }

    // Die Summe der Kreuztabelle muss mit der der Ausgabenliste
    // uebereinstimmen - beide filtern ueber ReportFilterSql.
    [Fact]
    public void Gesamtsumme_stimmt_mit_der_Ausgabenliste_ueberein()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var strom = _categories.Create("Strom", wohnen.Id);

        _expenses.Create(wohnen.Id, 1234, new DateOnly(2026, 2, 1), _selfId);
        _expenses.Create(strom.Id, 5678, new DateOnly(2026, 5, 1), _otherId);
        _expenses.Create(strom.Id, -99, new DateOnly(2026, 9, 1), _selfId);

        var filter = Jahr2026(ReportGrouping.Quarter);
        var matrix = Baue(filter);
        var summary = _expenses.Summarize(filter);

        Assert.Equal(summary.SumCents, matrix.Total.SumCents);
        Assert.Equal(summary.Count, matrix.Total.Count);
    }

    [Fact]
    public void Leere_Auswertung_liefert_eine_Tabelle_ohne_Spalten()
    {
        _categories.Create("Sonstiges", null);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));

        Assert.Empty(matrix.PeriodKeys);
        Assert.Empty(matrix.Rows);
        Assert.False(matrix.Total.HasValues);
    }

    // Archivierte Kategorien bleiben Teil der Historie (Regel 8).
    [Fact]
    public void Archivierte_Kategorien_erscheinen_mit_ihren_Werten()
    {
        var kategorie = _categories.Create("Altlast", null);
        _expenses.Create(kategorie.Id, 4200, new DateOnly(2026, 3, 1), _selfId);
        _categories.Archive(kategorie.Id, includeDescendants: false);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));
        var zeile = Assert.Single(matrix.Rows);

        Assert.True(zeile.IsArchived);
        Assert.Equal(4200, zeile.Total.SumCents);
    }

    [Fact]
    public void Zeile_kennt_Tiefe_und_vollen_Pfad()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var nebenkosten = _categories.Create("Nebenkosten", wohnen.Id);
        _expenses.Create(nebenkosten.Id, 1000, new DateOnly(2026, 3, 1), _selfId);

        var matrix = Baue(Jahr2026(ReportGrouping.Month));
        var oben = Assert.Single(matrix.Rows);
        var unten = Assert.Single(oben.Children);

        Assert.Equal(0, oben.Depth);
        Assert.Equal("Wohnen", oben.FullPath);
        Assert.Equal(1, unten.Depth);
        Assert.Equal("Wohnen › Nebenkosten", unten.FullPath);
    }
}
