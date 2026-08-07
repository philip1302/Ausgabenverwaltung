using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Aktionsleiste der Ausgabenliste: was fuer das Loeschen laengst
/// geht, geht auch fuer das Aendern.
///
/// Zwei Dinge stehen hier im Vordergrund: dass die Nachfrage nach dem
/// Schadensausmass bemessen wird (erst ab 20 Zeilen), und dass der
/// Erfolgstext die WAHRHEIT sagt, wenn weniger Zeilen gewandert sind als
/// markiert waren (Regel 4).
/// </summary>
public class SammelaktionenTests : IDisposable
{
    private readonly IDbConnection _connection;

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    private readonly int _wohnenId;
    private readonly int _freizeitId;
    private readonly int _ichId;
    private readonly int _annaId;

    public SammelaktionenTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _wohnenId = _kategorien.Create("Wohnen", parentId: null).Id;
        _freizeitId = _kategorien.Create("Freizeit", parentId: null).Id;
        _ichId = _personen.Create("Ich", isSelf: true).Id;
        _annaId = _personen.Create("Anna", isSelf: false).Id;
    }

    public void Dispose() => _connection.Dispose();

    private AusgabenlisteViewModel NeueListe() =>
        new(_ausgaben, _kategorien, _personen, new WeakReferenceMessenger());

    // Der Vorgabezeitraum der Liste ist nicht das ganze Jahr - die
    // Testbuchungen liegen deshalb auf heute, damit sie in der Liste
    // auftauchen und sich markieren lassen.
    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    private static void Markiere(AusgabenlisteViewModel vm, params int[] ids)
    {
        foreach (var zeile in vm.Zeilen.Where(z => ids.Contains(z.Id)))
        {
            zeile.IstAusgewaehlt = true;
        }
    }

    [Fact]
    public void Kategorie_aendern_bucht_die_markierten_Zeilen_um()
    {
        var eins = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);
        var kontrolle = _ausgaben.Create(_wohnenId, 2000, Heute, _ichId);

        var vm = NeueListe();
        Markiere(vm, eins.Id);

        var ziel = vm.SammelKategorien.Single(o => o.Id == _freizeitId);
        vm.SammelKategorieSetzenCommand.Execute(ziel);

        Assert.Equal(_freizeitId, _ausgaben.GetById(eins.Id)!.CategoryId);
        Assert.Equal(_wohnenId, _ausgaben.GetById(kontrolle.Id)!.CategoryId);
        Assert.Contains("Freizeit", vm.ErfolgText);
    }

    [Fact]
    public void Zahler_aendern_bucht_die_markierten_Zeilen_um()
    {
        var eins = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        Markiere(vm, eins.Id);

        vm.SammelZahlerSetzenCommand.Execute(vm.ZahlerOptionen.Single(o => o.Id == _annaId));

        Assert.Equal(_annaId, _ausgaben.GetById(eins.Id)!.PayerId);
    }

    [Fact]
    public void Als_beglichen_setzt_das_heutige_Datum()
    {
        var fremde = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);

        var vm = NeueListe();
        Markiere(vm, fremde.Id);

        vm.SammelAlsBeglichenCommand.Execute(null);

        Assert.Equal(Heute, _ausgaben.GetById(fremde.Id)!.SettledDate);
    }

    [Fact]
    public void Uebersprungene_eigene_Ausgaben_werden_benannt()
    {
        // Regel 4: bei einer eigenen Ausgabe gibt es keinen
        // Beglichen-Status. Ohne diesen Satz saehe es aus, als haette die
        // Aktion die Haelfte vergessen.
        var eigene = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);
        var fremde = _ausgaben.Create(_wohnenId, 2000, Heute, _annaId);

        var vm = NeueListe();
        Markiere(vm, eigene.Id, fremde.Id);

        vm.SammelAlsBeglichenCommand.Execute(null);

        Assert.Null(_ausgaben.GetById(eigene.Id)!.SettledDate);
        Assert.Equal(Heute, _ausgaben.GetById(fremde.Id)!.SettledDate);

        Assert.Contains("1 Buchung als beglichen markiert", vm.ErfolgText);
        Assert.Contains("eigenen Ausgaben", vm.ErfolgText);
    }

    // Der Einzelfall aus dem Kontextmenue der Zeile - ohne vorher zu
    // markieren. Er schreibt ueber dieselbe Core-Methode wie die
    // Sammelaktion darueber, damit Regel 4 an genau einer Stelle steht.

    [Fact]
    public void Eine_einzelne_Zeile_laesst_sich_abhaken()
    {
        var fremde = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == fremde.Id));

        Assert.Equal(Heute, _ausgaben.GetById(fremde.Id)!.SettledDate);
        Assert.Contains("Als beglichen markiert", vm.ErfolgText);
    }

    [Fact]
    public void Eine_eigene_Ausgabe_laesst_sich_nicht_abhaken()
    {
        // Regel 4: dort bedeutet SettledDate nichts. Der Menuepunkt ist
        // ausgegraut - das Kommando prueft es trotzdem selbst, weil es
        // sich nicht darauf verlassen darf, wer es aufruft.
        var eigene = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == eigene.Id));

        Assert.Null(_ausgaben.GetById(eigene.Id)!.SettledDate);
        Assert.Null(vm.ErfolgText);
    }

    [Fact]
    public void Eine_bereits_beglichene_Zeile_behaelt_ihr_Datum()
    {
        // Sonst wanderte ein aus gutem Grund zurueckdatiertes
        // Begleichungsdatum bei einem Fehlklick still auf heute.
        var gestern = Heute.AddDays(-1);
        var fremde = _ausgaben.Create(
            _wohnenId, 1000, Heute, _annaId, settledDate: gestern);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == fremde.Id));

        Assert.Equal(gestern, _ausgaben.GetById(fremde.Id)!.SettledDate);
    }

    [Fact]
    public void Unter_der_Schwelle_wird_nicht_nachgefragt()
    {
        var eins = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        Markiere(vm, eins.Id);

        vm.SammelKategorieSetzenCommand.Execute(vm.SammelKategorien.Single(o => o.Id == _freizeitId));

        Assert.False(vm.SammelAnfrageAktiv);
        Assert.Equal(_freizeitId, _ausgaben.GetById(eins.Id)!.CategoryId);
    }

    [Fact]
    public void Ab_zwanzig_Zeilen_wird_erst_nachgefragt()
    {
        var ids = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            ids.Add(_ausgaben.Create(_wohnenId, 1000, Heute, _ichId).Id);
        }

        var vm = NeueListe();
        Markiere(vm, ids.ToArray());

        vm.SammelKategorieSetzenCommand.Execute(vm.SammelKategorien.Single(o => o.Id == _freizeitId));

        // Noch ist nichts passiert.
        Assert.True(vm.SammelAnfrageAktiv);
        Assert.Contains("20 Buchungen", vm.SammelAnfrageText);
        Assert.Equal(_wohnenId, _ausgaben.GetById(ids[0])!.CategoryId);

        vm.SammelBestaetigenCommand.Execute(null);

        Assert.False(vm.SammelAnfrageAktiv);
        Assert.Equal(_freizeitId, _ausgaben.GetById(ids[0])!.CategoryId);
    }

    [Fact]
    public void Abbrechen_laesst_alles_stehen()
    {
        var ids = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            ids.Add(_ausgaben.Create(_wohnenId, 1000, Heute, _ichId).Id);
        }

        var vm = NeueListe();
        Markiere(vm, ids.ToArray());

        vm.SammelKategorieSetzenCommand.Execute(vm.SammelKategorien.Single(o => o.Id == _freizeitId));
        vm.SammelAbbrechenCommand.Execute(null);

        Assert.False(vm.SammelAnfrageAktiv);
        Assert.Equal(_wohnenId, _ausgaben.GetById(ids[0])!.CategoryId);
    }

    [Fact]
    public void Ohne_Auswahl_passiert_nichts()
    {
        var eins = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);

        var vm = NeueListe();
        vm.SammelAlsBeglichenCommand.Execute(null);
        vm.SammelKategorieSetzenCommand.Execute(vm.SammelKategorien.Single(o => o.Id == _freizeitId));

        Assert.Null(vm.ErfolgText);
        Assert.False(vm.SammelAnfrageAktiv);
        Assert.Equal(_wohnenId, _ausgaben.GetById(eins.Id)!.CategoryId);
        Assert.Null(_ausgaben.GetById(eins.Id)!.SettledDate);
    }

    [Fact]
    public void Die_Aktionsleiste_erscheint_erst_mit_der_Auswahl()
    {
        var eins = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        Assert.False(vm.HatAuswahl);

        Markiere(vm, eins.Id);

        Assert.True(vm.HatAuswahl);
        Assert.Equal("1 markiert", vm.AuswahlText);
    }

    [Fact]
    public void Die_Ziele_der_Kategorieauswahl_sind_nur_Blattknoten()
    {
        // Eine Kategorie mit Kindern kann keine Buchung aufnehmen - sie
        // darf deshalb auch nicht als Ziel angeboten werden.
        _kategorien.Create("Heizkosten", _wohnenId);

        var vm = NeueListe();

        Assert.DoesNotContain(vm.SammelKategorien, o => o.Id == _wohnenId);
        Assert.Contains(vm.SammelKategorien, o => o.Id == _freizeitId);
    }
}
