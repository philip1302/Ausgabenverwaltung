using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;

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
    public void Create_mit_doppeltem_Root_Namen_wirft_verstaendliche_Exception()
    {
        _repository.Create("Wohnen", null);

        Assert.Throws<DuplicateCategoryNameException>(() => _repository.Create("Wohnen", null));
    }

    [Fact]
    public void Create_mit_doppeltem_Namen_unter_gleichem_Elternteil_wirft_verstaendliche_Exception()
    {
        var parent = _repository.Create("Wohnen", null);
        _repository.Create("Heizkosten", parent.Id);

        Assert.Throws<DuplicateCategoryNameException>(() => _repository.Create("Heizkosten", parent.Id));
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

        _repository.Archive(category.Id, includeDescendants: false);

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

    [Fact]
    public void GetSelectableLeaves_liefert_nur_Blattknoten_mit_vollem_Pfad()
    {
        var pferde = _repository.Create("Pferde", null);
        var hufschmied = _repository.Create("Hufschmied", pferde.Id);
        _repository.Create("Freizeit", null);

        var leaves = _repository.GetSelectableLeaves();

        // "Pferde" hat ein Kind und ist deshalb kein Blattknoten - nur
        // "Hufschmied" und "Freizeit" sind waehlbar.
        Assert.Equal(2, leaves.Count);
        var hufschmiedOption = leaves.Single(l => l.Id == hufschmied.Id);
        Assert.Equal("Pferde › Hufschmied", hufschmiedOption.FullPath);
        Assert.Contains(leaves, l => l.FullPath == "Freizeit");
    }

    [Fact]
    public void GetSelectableLeaves_ignoriert_archivierte_Blattknoten()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        _repository.Archive(heizkosten.Id, includeDescendants: false);

        var leaves = _repository.GetSelectableLeaves();

        Assert.DoesNotContain(leaves, l => l.Id == heizkosten.Id);
    }

    [Fact]
    public void Rename_mit_doppeltem_Namen_unter_gleichem_Elternteil_wirft_verstaendliche_Exception()
    {
        var parent = _repository.Create("Wohnen", null);
        _repository.Create("Heizkosten", parent.Id);
        var strom = _repository.Create("Strom", parent.Id);

        Assert.Throws<DuplicateCategoryNameException>(() => _repository.Rename(strom.Id, "Heizkosten"));
    }

    [Fact]
    public void Rename_auf_unveraenderten_eigenen_Namen_wirft_nicht()
    {
        var category = _repository.Create("Wohnen", null);

        _repository.Rename(category.Id, "Wohnen");

        Assert.Equal("Wohnen", _repository.GetTree().Single().Category.Name);
    }

    [Fact]
    public void Restore_setzt_IsArchived_zurueck()
    {
        var category = _repository.Create("Wohnen", null);
        _repository.Archive(category.Id, includeDescendants: false);

        _repository.Restore(category.Id);

        Assert.False(_repository.GetTree().Single().Category.IsArchived);
    }

    [Fact]
    public void Archive_mit_includeDescendants_archiviert_auch_Unterkategorien()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        var strom = _repository.Create("Strom", heizkosten.Id);

        _repository.Archive(wohnen.Id, includeDescendants: true);

        var tree = _repository.GetTree();
        var wohnenNode = tree.Single();
        var heizkostenNode = wohnenNode.Children.Single();
        var stromNode = heizkostenNode.Children.Single();

        Assert.True(wohnenNode.Category.IsArchived);
        Assert.True(heizkostenNode.Category.IsArchived);
        Assert.True(stromNode.Category.IsArchived);
    }

    [Fact]
    public void Archive_ohne_includeDescendants_laesst_Unterkategorien_unveraendert()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);

        _repository.Archive(wohnen.Id, includeDescendants: false);

        var tree = _repository.GetTree();
        var wohnenNode = tree.Single();
        Assert.True(wohnenNode.Category.IsArchived);
        Assert.False(wohnenNode.Children.Single().Category.IsArchived);
    }

    [Fact]
    public void GetDescendantIds_liefert_alle_Nachfahren_ohne_den_Knoten_selbst()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        var strom = _repository.Create("Strom", heizkosten.Id);
        _repository.Create("Freizeit", null);

        var descendants = _repository.GetDescendantIds(wohnen.Id);

        Assert.Equal(new[] { heizkosten.Id, strom.Id }, descendants.OrderBy(id => id));
    }

    [Fact]
    public void MoveDown_und_MoveUp_vertauschen_SortOrder_mit_Nachbarn()
    {
        var erste = _repository.Create("Erste", null);
        _repository.Create("Zweite", null);

        _repository.MoveDown(erste.Id);
        var nachUnten = _repository.GetTree();
        Assert.Equal("Zweite", nachUnten[0].Category.Name);
        Assert.Equal("Erste", nachUnten[1].Category.Name);

        _repository.MoveUp(erste.Id);
        var nachOben = _repository.GetTree();
        Assert.Equal("Erste", nachOben[0].Category.Name);
        Assert.Equal("Zweite", nachOben[1].Category.Name);
    }

    [Fact]
    public void MoveUp_am_Anfang_der_Liste_aendert_nichts()
    {
        var erste = _repository.Create("Erste", null);
        _repository.Create("Zweite", null);

        _repository.MoveUp(erste.Id);

        Assert.Equal("Erste", _repository.GetTree()[0].Category.Name);
    }

    [Fact]
    public void GetExpenseCounts_zaehlt_nur_direkt_zugeordnete_Ausgaben()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenseRepository = new ExpenseRepository(_connection);

        expenseRepository.Create(heizkosten.Id, 1000, new DateOnly(2026, 1, 1), person.Id);
        expenseRepository.Create(heizkosten.Id, 2000, new DateOnly(2026, 1, 2), person.Id);

        var counts = _repository.GetExpenseCounts();

        Assert.Equal(2, counts[heizkosten.Id]);
        Assert.False(counts.ContainsKey(wohnen.Id));
    }
}
