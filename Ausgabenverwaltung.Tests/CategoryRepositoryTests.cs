using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Dapper;

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
    public void Neue_Kategorien_haben_keine_eigene_Farbe()
    {
        var pferde = _repository.Create("Pferde", null);

        Assert.Null(_repository.GetTree().Single(n => n.Category.Id == pferde.Id).Category.Color);
        Assert.Equal(CategoryColorPalette.DefaultHex, _repository.GetResolvedColors()[pferde.Id]);
    }

    [Fact]
    public void SetColor_setzt_die_Farbe_und_vererbt_sie_an_den_Ast()
    {
        var blau = CategoryColorPalette.Colors.Single(f => f.Name == "Blau").Hex;
        var pferde = _repository.Create("Pferde", null);
        var hufschmied = _repository.Create("Hufschmied", pferde.Id);

        _repository.SetColor(pferde.Id, blau);

        var farben = _repository.GetResolvedColors();
        Assert.Equal(blau, farben[pferde.Id]);
        Assert.Equal(blau, farben[hufschmied.Id]);
        Assert.Equal(blau, _repository.GetSelectableLeaves().Single().Color);
    }

    [Fact]
    public void SetColor_mit_NULL_nimmt_die_eigene_Farbe_zurueck()
    {
        var blau = CategoryColorPalette.Colors.Single(f => f.Name == "Blau").Hex;
        var pferde = _repository.Create("Pferde", null);
        _repository.SetColor(pferde.Id, blau);

        _repository.SetColor(pferde.Id, null);

        Assert.Null(_repository.GetTree().Single().Category.Color);
        Assert.Equal(CategoryColorPalette.DefaultHex, _repository.GetResolvedColors()[pferde.Id]);
    }

    [Fact]
    public void SetColor_speichert_nur_Werte_aus_der_Palette()
    {
        var pferde = _repository.Create("Pferde", null);

        _repository.SetColor(pferde.Id, "#ABCDEF");

        Assert.Null(_repository.GetTree().Single().Category.Color);
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

    // ---------------- Loeschen ----------------
    //
    // Regel 8: archiviert wird der Normalfall, geloescht nur, was
    // vollstaendig unbenutzt ist.

    [Fact]
    public void Delete_entfernt_eine_unbenutzte_Kategorie()
    {
        var wohnen = _repository.Create("Wohnen", null);

        _repository.Delete(wohnen.Id);

        Assert.Empty(_repository.GetTree());
    }

    [Fact]
    public void Delete_lehnt_bei_zugeordneten_Ausgaben_ab()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        new ExpenseRepository(_connection)
            .Create(wohnen.Id, 1000, new DateOnly(2026, 1, 1), person.Id);

        var ex = Assert.Throws<CategoryInUseException>(() => _repository.Delete(wohnen.Id));

        Assert.Equal(1, ex.Usage.ExpenseCount);
        Assert.Equal(0, ex.Usage.ChildCount);
        Assert.Equal(0, ex.Usage.RecurringExpenseCount);
        Assert.Single(_repository.GetTree());
    }

    [Fact]
    public void Delete_lehnt_bei_Unterkategorien_ab()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var heizkosten = _repository.Create("Heizkosten", wohnen.Id);
        _repository.Create("Strom", heizkosten.Id);

        var ex = Assert.Throws<CategoryInUseException>(() => _repository.Delete(wohnen.Id));

        // Alle Nachfahren, nicht nur die direkten Kinder - der ganze Ast
        // haengt daran.
        Assert.Equal(2, ex.Usage.ChildCount);
        Assert.Equal(0, ex.Usage.ExpenseCount);
        Assert.Equal(0, ex.Usage.RecurringExpenseCount);
        Assert.Single(_repository.GetTree());
    }

    [Fact]
    public void Delete_lehnt_bei_verweisender_Vorlage_ab()
    {
        var wohnen = _repository.Create("Wohnen", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        LegeVorlageAn(wohnen.Id, person.Id, 5000);

        var ex = Assert.Throws<CategoryInUseException>(() => _repository.Delete(wohnen.Id));

        Assert.Equal(1, ex.Usage.RecurringExpenseCount);
        Assert.Equal(0, ex.Usage.ExpenseCount);
        Assert.Equal(0, ex.Usage.ChildCount);
        Assert.Single(_repository.GetTree());
    }

    [Fact]
    public void Delete_einer_archivierten_unbenutzten_Kategorie_ist_moeglich()
    {
        var wohnen = _repository.Create("Wohnen", null);
        _repository.Archive(wohnen.Id, includeDescendants: false);

        _repository.Delete(wohnen.Id);

        Assert.Empty(_repository.GetTree());
    }

    // ---------------- Zusammenfuehren ----------------

    [Fact]
    public void Merge_haengt_alle_Buchungen_um_und_laesst_die_Summe_unveraendert()
    {
        var (quelle, ziel, personId) = LegeZweiKategorienAn();
        var expenses = new ExpenseRepository(_connection);

        expenses.Create(quelle, 1000, new DateOnly(2026, 1, 1), personId);
        expenses.Create(quelle, -250, new DateOnly(2026, 1, 2), personId); // Erstattung
        expenses.Create(ziel, 4000, new DateOnly(2026, 1, 3), personId);
        LegeVorlageAn(quelle, personId, 5000);

        var summeVorher = SummeAllerAusgaben();

        _repository.Merge(quelle, ziel);

        // Alle Ausgaben und die Vorlage stehen jetzt beim Ziel ...
        Assert.Equal(3, ZaehleAusgaben(ziel));
        Assert.Equal(1, ZaehleVorlagen(ziel));

        // ... die Quelle ist weg ...
        Assert.Null(_repository.GetTree().SingleOrDefault(n => n.Category.Id == quelle));

        // ... und keine einzige Buchung hat ihren Betrag geaendert.
        Assert.Equal(summeVorher, SummeAllerAusgaben());
        Assert.Equal(4750, SummeAllerAusgaben());
    }

    [Fact]
    public void Merge_aktualisiert_ModifiedUtc_der_umgehaengten_Ausgaben()
    {
        var (quelle, ziel, personId) = LegeZweiKategorienAn();
        var expenses = new ExpenseRepository(_connection);
        var ausgabe = expenses.Create(quelle, 1000, new DateOnly(2026, 1, 1), personId);

        // Auf einen alten Stand zurueckdatieren, damit die Aenderung
        // sichtbar wird - sonst laegen beide Zeitstempel in derselben
        // Sekunde.
        _connection.Execute(
            "UPDATE Expense SET ModifiedUtc = @Alt WHERE Id = @Id",
            new { Alt = "2020-01-01T00:00:00Z", Id = ausgabe.Id });

        // Gespeicherte Zeitstempel sind auf Sekunden genau (Regel 3) -
        // deshalb der Vergleich gegen den gelesenen und nicht gegen den
        // von Create() zurueckgegebenen Wert.
        var vorher = expenses.GetById(ausgabe.Id)!;

        _repository.Merge(quelle, ziel);

        var danach = expenses.GetById(ausgabe.Id)!;
        Assert.Equal(ziel, danach.CategoryId);
        Assert.True(danach.ModifiedUtc > new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        // CreatedUtc bleibt: erfasst wurde die Buchung damals.
        Assert.Equal(vorher.CreatedUtc, danach.CreatedUtc);
    }

    [Fact]
    public void Merge_lehnt_ab_wenn_die_Quelle_Unterkategorien_hat()
    {
        var (quelle, ziel, personId) = LegeZweiKategorienAn();
        _repository.Create("Unterkategorie", quelle);
        new ExpenseRepository(_connection).Create(quelle, 1000, new DateOnly(2026, 1, 1), personId);

        var ex = Assert.Throws<CategoryHasChildrenException>(() => _repository.Merge(quelle, ziel));

        Assert.Equal(1, ex.ChildCount);

        // Nichts bewegt: die Ausgabe steht weiterhin bei der Quelle.
        Assert.Equal(1, ZaehleAusgaben(quelle));
        Assert.Equal(0, ZaehleAusgaben(ziel));
    }

    [Fact]
    public void PreviewMerge_nennt_Anzahl_und_betroffene_Summe_ohne_etwas_zu_veraendern()
    {
        var (quelle, ziel, personId) = LegeZweiKategorienAn();
        var expenses = new ExpenseRepository(_connection);
        expenses.Create(quelle, 1000, new DateOnly(2026, 1, 1), personId);
        expenses.Create(quelle, 2500, new DateOnly(2026, 1, 2), personId);
        LegeVorlageAn(quelle, personId, 5000);

        var vorschau = _repository.PreviewMerge(quelle, ziel);

        Assert.Equal(2, vorschau.ExpenseCount);
        Assert.Equal(1, vorschau.RecurringExpenseCount);
        Assert.Equal(3500, vorschau.SumCents);

        // Eine Vorschau veraendert nichts.
        Assert.Equal(2, ZaehleAusgaben(quelle));
        Assert.NotNull(_repository.GetTree().SingleOrDefault(n => n.Category.Id == quelle));
    }

    [Fact]
    public void PreviewMerge_lehnt_dieselben_Faelle_ab_wie_Merge()
    {
        var (quelle, ziel, _) = LegeZweiKategorienAn();
        _repository.Create("Unterkategorie", quelle);

        Assert.Throws<CategoryHasChildrenException>(() => _repository.PreviewMerge(quelle, ziel));
    }

    // ---------------- Hilfen ----------------

    // Zwei Kategorien nebeneinander (beide Blattknoten) und die
    // IsSelf-Person, die jede Buchung braucht.
    private (int Quelle, int Ziel, int PersonId) LegeZweiKategorienAn()
    {
        var quelle = _repository.Create("Hufschmied", null);
        var ziel = _repository.Create("Tierarzt", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);

        return (quelle.Id, ziel.Id, person.Id);
    }

    private void LegeVorlageAn(int categoryId, int payerId, long amountCents) =>
        new RecurringExpenseRepository(_connection).Create(
            categoryId, payerId, amountCents, "Vorlage",
            intervalUnit: "month", intervalCount: 1, anchorDay: 1,
            startDate: new DateOnly(2026, 1, 1), endDate: null);

    private int ZaehleAusgaben(int categoryId) => _connection.ExecuteScalar<int>(
        "SELECT COUNT(*) FROM Expense WHERE CategoryId = @Id", new { Id = categoryId });

    private int ZaehleVorlagen(int categoryId) => _connection.ExecuteScalar<int>(
        "SELECT COUNT(*) FROM RecurringExpense WHERE CategoryId = @Id", new { Id = categoryId });

    private long SummeAllerAusgaben() => _connection.ExecuteScalar<long>(
        "SELECT COALESCE(SUM(AmountCents), 0) FROM Expense");

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

    // ================= Schnellwahl =================

    [Fact]
    public void GetMostUsed_sortiert_nach_Haeufigkeit()
    {
        var selten = _repository.Create("Selten", null);
        var oft = _repository.Create("Oft", null);
        var mittel = _repository.Create("Mittel", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenses = new ExpenseRepository(_connection);

        expenses.Create(selten.Id, 1000, new DateOnly(2026, 6, 1), person.Id);
        for (var i = 0; i < 3; i++)
        {
            expenses.Create(oft.Id, 1000, new DateOnly(2026, 6, 2), person.Id);
        }

        expenses.Create(mittel.Id, 1000, new DateOnly(2026, 6, 3), person.Id);
        expenses.Create(mittel.Id, 1000, new DateOnly(2026, 6, 4), person.Id);

        var haeufigste = _repository.GetMostUsed(5, new DateOnly(2026, 1, 1));

        Assert.Equal(
            new[] { oft.Id, mittel.Id, selten.Id },
            haeufigste.Select(option => option.Id));
    }

    [Fact]
    public void GetMostUsed_liefert_hoechstens_die_gewuenschte_Anzahl()
    {
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenses = new ExpenseRepository(_connection);

        for (var i = 1; i <= 4; i++)
        {
            var kategorie = _repository.Create($"Kategorie {i}", null);
            expenses.Create(kategorie.Id, 1000, new DateOnly(2026, 6, i), person.Id);
        }

        Assert.Equal(2, _repository.GetMostUsed(2, new DateOnly(2026, 1, 1)).Count);
    }

    [Fact]
    public void GetMostUsed_beachtet_die_Zeitraumgrenze()
    {
        // Was vor zwei Jahren oft gebraucht wurde, sagt ueber den
        // naechsten Beleg wenig - deshalb der Stichtag.
        var alt = _repository.Create("Alt", null);
        var neu = _repository.Create("Neu", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenses = new ExpenseRepository(_connection);

        expenses.Create(alt.Id, 1000, new DateOnly(2026, 3, 30), person.Id);
        expenses.Create(alt.Id, 1000, new DateOnly(2026, 3, 31), person.Id);
        expenses.Create(neu.Id, 1000, new DateOnly(2026, 4, 1), person.Id);

        var haeufigste = _repository.GetMostUsed(5, new DateOnly(2026, 4, 1));

        // Der Stichtag zaehlt selbst noch dazu (>=), die beiden Tage davor
        // nicht mehr.
        Assert.Equal(new[] { neu.Id }, haeufigste.Select(option => option.Id));
    }

    [Fact]
    public void GetMostUsed_laesst_archivierte_Kategorien_weg()
    {
        var archiviert = _repository.Create("Archiviert", null);
        var aktiv = _repository.Create("Aktiv", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenses = new ExpenseRepository(_connection);

        // Die archivierte ist die haeufigere - trotzdem darf sie nicht
        // erscheinen: die Schnellwahl kann nichts anbieten, was sich im
        // Kategoriefeld daneben nicht auswaehlen laesst.
        expenses.Create(archiviert.Id, 1000, new DateOnly(2026, 6, 1), person.Id);
        expenses.Create(archiviert.Id, 1000, new DateOnly(2026, 6, 2), person.Id);
        expenses.Create(aktiv.Id, 1000, new DateOnly(2026, 6, 3), person.Id);

        _repository.Archive(archiviert.Id, includeDescendants: false);

        var haeufigste = _repository.GetMostUsed(5, new DateOnly(2026, 1, 1));

        Assert.Equal(new[] { aktiv.Id }, haeufigste.Select(option => option.Id));
    }

    [Fact]
    public void GetMostUsed_laesst_Kategorien_mit_Unterkategorien_weg()
    {
        // Eine Kategorie mit Kindern ist nicht waehlbar (siehe
        // GetSelectableLeaves) - auch dann nicht, wenn aus einer frueheren
        // Zeit noch Buchungen direkt an ihr haengen.
        var wohnen = _repository.Create("Wohnen", null);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);
        var expenses = new ExpenseRepository(_connection);

        expenses.Create(wohnen.Id, 1000, new DateOnly(2026, 6, 1), person.Id);
        _repository.Create("Heizkosten", wohnen.Id);

        Assert.Empty(_repository.GetMostUsed(5, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void GetMostUsed_liefert_den_vollen_Pfad_und_die_aufgeloeste_Farbe()
    {
        // Dieselben Angaben wie im Kategoriefeld daneben - die Schnellwahl
        // laedt den Baum dafuer nicht ein zweites Mal.
        var pferde = _repository.Create("Pferde", null);
        var hufschmied = _repository.Create("Hufschmied", pferde.Id);
        var person = new PersonRepository(_connection).Create("Ich", isSelf: true);

        _repository.SetColor(pferde.Id, CategoryColorPalette.Colors[0].Hex);
        new ExpenseRepository(_connection)
            .Create(hufschmied.Id, 1000, new DateOnly(2026, 6, 1), person.Id);

        var option = Assert.Single(_repository.GetMostUsed(5, new DateOnly(2026, 1, 1)));

        Assert.Equal(CategoryPaths.Append("Pferde", "Hufschmied"), option.FullPath);
        Assert.Equal(CategoryColorPalette.Colors[0].Hex, option.Color);
    }
}
