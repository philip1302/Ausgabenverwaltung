using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;

namespace Ausgabenverwaltung.Tests;

public class CategoryPathsTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly CategoryRepository _categories;

    public CategoryPathsTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _categories = new CategoryRepository(_connection);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void BuildFullPaths_liefert_den_Pfad_jeder_Ebene_nicht_nur_der_Blaetter()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var nebenkosten = _categories.Create("Nebenkosten", wohnen.Id);
        var strom = _categories.Create("Strom", nebenkosten.Id);

        var pfade = CategoryPaths.BuildFullPaths(_categories.GetTree());

        Assert.Equal("Wohnen", pfade[wohnen.Id]);
        Assert.Equal("Wohnen › Nebenkosten", pfade[nebenkosten.Id]);
        Assert.Equal("Wohnen › Nebenkosten › Strom", pfade[strom.Id]);
    }

    [Fact]
    public void BuildFullPaths_umfasst_mehrere_Wurzeln()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var pferde = _categories.Create("Pferde", null);
        var hufschmied = _categories.Create("Hufschmied", pferde.Id);

        var pfade = CategoryPaths.BuildFullPaths(_categories.GetTree());

        Assert.Equal(3, pfade.Count);
        Assert.Equal("Wohnen", pfade[wohnen.Id]);
        Assert.Equal("Pferde › Hufschmied", pfade[hufschmied.Id]);
    }

    [Fact]
    public void BuildFullPaths_beruecksichtigt_auch_archivierte_Kategorien()
    {
        var wohnen = _categories.Create("Wohnen", null);
        var alt = _categories.Create("Altlast", wohnen.Id);
        _categories.Archive(alt.Id, includeDescendants: false);

        var pfade = CategoryPaths.BuildFullPaths(_categories.GetTree());

        Assert.Equal("Wohnen › Altlast", pfade[alt.Id]);
    }

    [Fact]
    public void Append_bildet_Wurzel_und_Unterpfad()
    {
        Assert.Equal("Wohnen", CategoryPaths.Append(null, "Wohnen"));
        Assert.Equal("Wohnen › Strom", CategoryPaths.Append("Wohnen", "Strom"));
    }

    // Das Trennzeichen steckt zusaetzlich in den SQL-CTEs, die den Pfad
    // direkt in der Datenbank bilden - laeuft es hier auseinander, stimmen
    // Filteranzeige und Tabellenspalte nicht mehr ueberein.
    [Fact]
    public void Separator_entspricht_dem_in_den_SQL_Abfragen_verwendeten_Zeichen()
    {
        Assert.Equal(" › ", CategoryPaths.Separator);
    }
}
