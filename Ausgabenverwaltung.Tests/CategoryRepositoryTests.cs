using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

public class CategoryRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly CategoryRepository _repository;

    public CategoryRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new CategoryRepository(_connection);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Create_legt_Root_Kategorie_an()
    {
        var category = _repository.Create("Wohnen", null);

        Assert.True(category.Id > 0);
        Assert.Equal("Wohnen", category.Name);
        Assert.Null(category.ParentId);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Create_legt_Unterkategorie_an()
    {
        var parent = _repository.Create("Wohnen", null);
        var child = _repository.Create("Heizkosten", parent.Id);

        Assert.Equal(parent.Id, child.ParentId);
    }

    [Fact]
    public void Create_mit_doppeltem_Root_Namen_wirft()
    {
        _repository.Create("Wohnen", null);

        Assert.Throws<SqliteException>(() => _repository.Create("Wohnen", null));
    }

    [Fact]
    public void Create_mit_doppeltem_Namen_unter_gleichem_Elternteil_wirft()
    {
        var parent = _repository.Create("Wohnen", null);
        _repository.Create("Heizkosten", parent.Id);

        Assert.Throws<SqliteException>(() => _repository.Create("Heizkosten", parent.Id));
    }

    [Fact]
    public void Gleicher_Name_unter_verschiedenen_Elternteilen_ist_erlaubt()
    {
        var parentA = _repository.Create("Wohnen", null);
        var parentB = _repository.Create("Freizeit", null);

        _repository.Create("Sonstiges", parentA.Id);
        var child = _repository.Create("Sonstiges", parentB.Id);

        Assert.Equal(parentB.Id, child.ParentId);
    }

    [Fact]
    public void Rename_aendert_den_Namen()
    {
        var category = _repository.Create("Wohnen", null);

        _repository.Rename(category.Id, "Wohnen neu");

        var tree = _repository.GetTree();
        Assert.Equal("Wohnen neu", tree.Single().Category.Name);
    }

    [Fact]
    public void Archive_setzt_IsArchived_ohne_die_Zeile_zu_loeschen()
    {
        var category = _repository.Create("Wohnen", null);

        _repository.Archive(category.Id);

        var tree = _repository.GetTree();
        var node = Assert.Single(tree);
        Assert.Equal(category.Id, node.Category.Id);
        Assert.True(node.Category.IsArchived);
    }

    [Fact]
    public void GetTree_liefert_verschachtelte_Kinder()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        _repository.Create("Freizeit", null);

        var tree = _repository.GetTree();

        Assert.Equal(2, tree.Count);
        var wohnenNode = tree.Single(n => n.Category.Id == wohnen.Id);
        var heizkostenNode = Assert.Single(wohnenNode.Children);
        Assert.Equal(heizkosten.Id, heizkostenNode.Category.Id);
        Assert.Empty(heizkostenNode.Children);
    }
}
