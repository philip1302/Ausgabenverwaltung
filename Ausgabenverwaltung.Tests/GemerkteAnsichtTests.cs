using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Charts;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Was die Anwendung sich ueber das Programmende hinaus merkt: Fensterlage
/// und die Sortierung der beiden Listen.
///
/// Geprueft wird der ganze Weg durch die Einstellungsdatei - schreiben,
/// lesen, wieder anwenden. Dass ein ViewModel eine Eigenschaft setzt,
/// besagt allein nichts; die Frage ist, ob der Wert den Neustart
/// uebersteht, und dafuer muss er wirklich durch die Datei gelaufen sein.
/// Ein zweites ViewModel auf derselben Datei ist der Neustart.
/// </summary>
public class GemerkteAnsichtTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;
    private readonly OpenItemsRepository _offenePosten;
    private readonly TestEinstellungen _einstellungen = new();

    public GemerkteAnsichtTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);
        _offenePosten = new OpenItemsRepository(_connection);

        _kategorien.Create("Wohnen", parentId: null);
        _personen.Create("Ich", isSelf: true);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    private AusgabenlisteViewModel NeueListe() => new(
        _ausgaben, _kategorien, _personen, _einstellungen.Store,
        new WeakReferenceMessenger(), new ToastViewModel());

    private OffenePostenViewModel NeueOffenePosten() => new(
        _offenePosten, _einstellungen.Store, new WeakReferenceMessenger());

    // ---------------- Ausgabenliste ----------------

    [Fact]
    public void Die_Ausgabenliste_beginnt_mit_Datum_absteigend()
    {
        var liste = NeueListe();

        Assert.Equal(ExpenseSortColumn.Datum, liste.SortSpalte);
        Assert.False(liste.SortAufsteigend);
    }

    [Fact]
    public void Die_Sortierung_der_Ausgabenliste_uebersteht_den_Neustart()
    {
        var erste = NeueListe();
        erste.SpalteSortierenCommand.Execute("Betrag");

        Assert.Equal(ExpenseSortColumn.Betrag, erste.SortSpalte);
        Assert.True(erste.SortAufsteigend);

        // Der "Neustart": ein frisches ViewModel auf derselben Datei.
        var zweite = NeueListe();

        Assert.Equal(ExpenseSortColumn.Betrag, zweite.SortSpalte);
        Assert.True(zweite.SortAufsteigend);
    }

    /// <summary>
    /// Der zweite Klick auf dieselbe Spalte dreht die Richtung - auch das
    /// gehoert gemerkt, sonst kaeme die Liste umgekehrt zurueck.
    /// </summary>
    [Fact]
    public void Auch_die_Richtung_wird_gemerkt()
    {
        var erste = NeueListe();
        erste.SpalteSortierenCommand.Execute("Betrag");
        erste.SpalteSortierenCommand.Execute("Betrag");

        Assert.False(erste.SortAufsteigend);
        Assert.False(NeueListe().SortAufsteigend);
    }

    /// <summary>
    /// "Filter zuruecksetzen" laesst die Sortierung stehen - sie ist kein
    /// Filter, sondern die Leserichtung der Liste. Festgehalten, weil das
    /// Merken der Sortierung die Frage erst aufwirft: wer beides
    /// zusammenwirft, raeumt dem Anwender mit einem Klick auf "Filter
    /// zuruecksetzen" auch seine Sortierung weg.
    /// </summary>
    [Fact]
    public void Filter_zuruecksetzen_laesst_die_Sortierung_stehen()
    {
        var erste = NeueListe();
        erste.SpalteSortierenCommand.Execute("Kategorie");
        erste.FilterZuruecksetzenCommand.Execute(null);

        Assert.Equal(ExpenseSortColumn.Kategorie, erste.SortSpalte);
        Assert.Equal(ExpenseSortColumn.Kategorie, NeueListe().SortSpalte);
    }

    /// <summary>
    /// Ein Sprung in die Liste (aus einer Kachel, einer Vorlage, einer
    /// Uebersichtszeile) setzt die Sortierung dagegen ausdruecklich auf
    /// Datum/absteigend zurueck - man kommt mit einer Frage an, nicht mit
    /// einer Leserichtung. Auch DAS gehoert gemerkt, sonst kaeme beim
    /// naechsten Start die Sortierung von vor dem Sprung wieder.
    /// </summary>
    [Fact]
    public void Ein_Sprung_in_die_Liste_merkt_die_zurueckgesetzte_Sortierung()
    {
        var erste = NeueListe();
        erste.SpalteSortierenCommand.Execute("Kategorie");

        // Die Balkenart spielt hier keine Rolle - geprueft wird die
        // Sortierung. Netto schraenkt als einzige gar nicht ein.
        erste.ZeigeZeitraum(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), BarKind.NetPositive);

        Assert.Equal(ExpenseSortColumn.Datum, erste.SortSpalte);
        Assert.False(erste.SortAufsteigend);

        var zweite = NeueListe();

        Assert.Equal(ExpenseSortColumn.Datum, zweite.SortSpalte);
        Assert.False(zweite.SortAufsteigend);
    }

    // ---------------- Offene Posten ----------------

    /// <summary>
    /// Vorgabe hier ist Datum AUFSTEIGEND - anders als in der
    /// Ausgabenliste, weil ein offener Posten mit dem Alter dringender wird.
    /// </summary>
    [Fact]
    public void Die_offenen_Posten_beginnen_mit_Datum_aufsteigend()
    {
        var liste = NeueOffenePosten();

        Assert.Equal(OpenItemsSortColumn.Datum, liste.SortSpalte);
        Assert.True(liste.SortAufsteigend);
    }

    [Fact]
    public void Die_Sortierung_der_offenen_Posten_uebersteht_den_Neustart()
    {
        var erste = NeueOffenePosten();
        erste.SpalteSortierenCommand.Execute("TageOffen");

        var zweite = NeueOffenePosten();

        Assert.Equal(OpenItemsSortColumn.TageOffen, zweite.SortSpalte);
        Assert.True(zweite.SortAufsteigend);
    }

    /// <summary>
    /// Die beiden Listen merken sich getrennt: eine Sortierung in der
    /// Ausgabenliste darf die offenen Posten nicht umstellen.
    /// </summary>
    [Fact]
    public void Beide_Listen_merken_sich_unabhaengig_voneinander()
    {
        NeueListe().SpalteSortierenCommand.Execute("Betrag");

        var offene = NeueOffenePosten();

        Assert.Equal(OpenItemsSortColumn.Datum, offene.SortSpalte);
        Assert.True(offene.SortAufsteigend);
    }

    // ---------------- Fensterlage in der Datei ----------------

    /// <summary>
    /// Die Lage laeuft durch dieselbe Datei wie alles andere und muss die
    /// Runde durch JSON unveraendert ueberstehen - einschliesslich
    /// negativer Koordinaten, die ein Bildschirm links vom Hauptbildschirm
    /// erzeugt.
    /// </summary>
    [Fact]
    public void Die_Fensterlage_uebersteht_das_Schreiben_und_Lesen()
    {
        var lage = new WindowPlacement
        {
            Left = -1720,
            Top = 40,
            Width = 1440,
            Height = 900,
            IsMaximized = true,
        };

        _einstellungen.Store.Save(_einstellungen.Lies() with { WindowPlacement = lage });

        Assert.Equal(lage, _einstellungen.Lies().WindowPlacement);
    }

    [Fact]
    public void Ohne_gemerkte_Lage_steht_dort_nichts()
        => Assert.Null(_einstellungen.Lies().WindowPlacement);

    /// <summary>
    /// Die uebrigen Einstellungen duerfen dabei nicht verloren gehen - jede
    /// Speicherung schreibt den GANZEN Satz, deshalb immer mit "with" auf
    /// dem gerade gelesenen Stand (siehe AppSettingsStore).
    /// </summary>
    [Fact]
    public void Das_Merken_der_Lage_laesst_die_uebrigen_Einstellungen_stehen()
    {
        _einstellungen.Store.Save(_einstellungen.Lies() with
        {
            ExternalFolderPath = @"D:\Sicherungen",
            KeepEntryValues = true,
        });

        // Eine Sortierung merken - der Weg, den ein Klick auf einen
        // Spaltenkopf nimmt.
        NeueListe().SpalteSortierenCommand.Execute("Zahler");

        var stand = _einstellungen.Lies();

        Assert.Equal(@"D:\Sicherungen", stand.ExternalFolderPath);
        Assert.True(stand.KeepEntryValues);
        Assert.Equal(ExpenseSortColumn.Zahler, stand.ExpenseListSortColumn);
    }
}
