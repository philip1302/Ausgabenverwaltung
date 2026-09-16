using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// "Als beglichen markieren" in der Ausgabenliste - aus dem Kontextmenue
/// einer Zeile wie ueber die Aktionsleiste - laesst sich zuruecknehmen,
/// genau wie das Loeschen (siehe <see cref="LoeschenRueckgaengigTests"/>).
/// Es ist derselbe Handgriff wie das Abhaken in "Offene Posten", und ein
/// Fehlklick soll dieselbe Umkehr finden, egal an welcher Stelle er
/// passiert ist.
///
/// Im Vordergrund steht, dass die Ruecknahme den Stand von VORHER
/// zurueckschreibt und nicht einfach "offen": eine Zeile, die schon ein
/// aelteres Begleichungsdatum trug, bekommt genau dieses wieder.
/// </summary>
public class AbhakenRueckgaengigTests : IDisposable
{
    private readonly IDbConnection _connection;

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    private readonly int _wohnenId;
    private readonly int _ichId;
    private readonly int _annaId;

    public AbhakenRueckgaengigTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _wohnenId = _kategorien.Create("Wohnen", parentId: null).Id;
        _ichId = _personen.Create("Ich", isSelf: true).Id;
        _annaId = _personen.Create("Anna", isSelf: false).Id;
    }

    // Eigene Einstellungsdatei: die Liste merkt sich ihre Sortierung, und
    // das darf nicht in der settings.json des Rechners landen.
    private readonly TestEinstellungen _einstellungen = new();

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    private AusgabenlisteViewModel NeueListe() =>
        new(_ausgaben, _kategorien, _personen, _einstellungen.Store, new WeakReferenceMessenger());

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    private static void Markiere(AusgabenlisteViewModel vm, params int[] ids)
    {
        foreach (var zeile in vm.Zeilen.Where(z => ids.Contains(z.Id)))
        {
            zeile.IstAusgewaehlt = true;
        }
    }

    [Fact]
    public void Das_Abhaken_einer_Zeile_bietet_Rueckgaengig_an()
    {
        var offene = _ausgaben.Create(_wohnenId, 4290, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == offene.Id));

        Assert.True(vm.RueckgaengigSichtbar);
        Assert.Contains("Als beglichen markiert", vm.RueckgaengigText!);

        // Der Hinweis am Knopf gehoert zum Vorgang: nach einem Abhaken darf
        // dort nicht stehen, dass geloeschte Buchungen wieder angelegt
        // werden.
        Assert.Contains("Beglichen", vm.RueckgaengigTipp);
    }

    [Fact]
    public void Rueckgaengig_macht_die_Zeile_wieder_offen()
    {
        var offene = _ausgaben.Create(_wohnenId, 4290, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == offene.Id));
        Assert.Equal(Heute, _ausgaben.GetById(offene.Id)!.SettledDate);

        vm.RueckgaengigCommand.Execute(null);

        Assert.Null(_ausgaben.GetById(offene.Id)!.SettledDate);
        Assert.True(vm.Zeilen.Single(z => z.Id == offene.Id).IstOffen);

        // Das Band ist eingeloest und verschwindet; der Satz darueber steht
        // jetzt im Erfolgsband.
        Assert.False(vm.RueckgaengigSichtbar);
        Assert.Contains("zurückgenommen", vm.ErfolgText!);
    }

    [Fact]
    public void Rueckgaengig_nach_der_Sammelaktion_nimmt_alle_Zeilen_zurueck()
    {
        var eine = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);
        var andere = _ausgaben.Create(_wohnenId, 2000, Heute, _annaId);

        var vm = NeueListe();
        Markiere(vm, eine.Id, andere.Id);
        vm.SammelAlsBeglichenCommand.Execute(null);

        vm.RueckgaengigCommand.Execute(null);

        Assert.Null(_ausgaben.GetById(eine.Id)!.SettledDate);
        Assert.Null(_ausgaben.GetById(andere.Id)!.SettledDate);
    }

    [Fact]
    public void Ein_aelteres_Begleichungsdatum_kommt_zurueck_statt_offen()
    {
        // Die Sammelaktion ueberschreibt ein bestehendes Datum mit heute.
        // Die Ruecknahme muss dann genau dieses Datum wiederherstellen -
        // "offen" waere ein anderer Stand als der vor dem Fehlklick.
        var gestern = Heute.AddDays(-1);
        var schonBeglichen = _ausgaben.Create(
            _wohnenId, 1000, Heute, _annaId, settledDate: gestern);
        var offene = _ausgaben.Create(_wohnenId, 2000, Heute, _annaId);

        var vm = NeueListe();
        Markiere(vm, schonBeglichen.Id, offene.Id);
        vm.SammelAlsBeglichenCommand.Execute(null);

        Assert.Equal(Heute, _ausgaben.GetById(schonBeglichen.Id)!.SettledDate);

        vm.RueckgaengigCommand.Execute(null);

        Assert.Equal(gestern, _ausgaben.GetById(schonBeglichen.Id)!.SettledDate);
        Assert.Null(_ausgaben.GetById(offene.Id)!.SettledDate);
    }

    [Fact]
    public void Eine_eigene_Ausgabe_in_der_Auswahl_bleibt_unberuehrt()
    {
        // Regel 4: bei einer eigenen Ausgabe bedeutet SettledDate nichts.
        // Sie wird beim Abhaken uebersprungen - und die Ruecknahme darf ihr
        // deshalb auch nichts eintragen.
        var eigene = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);
        var fremde = _ausgaben.Create(_wohnenId, 2000, Heute, _annaId);

        var vm = NeueListe();
        Markiere(vm, eigene.Id, fremde.Id);
        vm.SammelAlsBeglichenCommand.Execute(null);
        vm.RueckgaengigCommand.Execute(null);

        Assert.Null(_ausgaben.GetById(eigene.Id)!.SettledDate);
        Assert.Null(_ausgaben.GetById(fremde.Id)!.SettledDate);
    }

    [Fact]
    public void Ohne_getroffene_Zeile_wird_kein_Rueckgaengig_angeboten()
    {
        // Lauter eigene Ausgaben: die Aktion hat nichts geaendert
        // (Regel 4), und ein Knopf "Rückgängig" waere ein Angebot ohne
        // Inhalt. Der Satz steht dann im Erfolgsband.
        var eigene = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        Markiere(vm, eigene.Id);
        vm.SammelAlsBeglichenCommand.Execute(null);

        Assert.False(vm.RueckgaengigSichtbar);
        Assert.Contains("blieben unverändert", vm.ErfolgText!);
    }

    [Fact]
    public void Schliessen_laesst_das_Abhaken_stehen_und_raeumt_das_Band()
    {
        var offene = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == offene.Id));

        vm.RueckgaengigSchliessenCommand.Execute(null);

        Assert.False(vm.RueckgaengigSichtbar);
        Assert.Equal(Heute, _ausgaben.GetById(offene.Id)!.SettledDate);

        // Der Vorrat ist mit dem Band weg: ein zweiter Druck nimmt nichts
        // mehr zurueck.
        vm.RueckgaengigCommand.Execute(null);
        Assert.Equal(Heute, _ausgaben.GetById(offene.Id)!.SettledDate);
    }

    [Fact]
    public void Der_Bereichswechsel_beendet_das_Angebot()
    {
        var offene = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == offene.Id));
        Assert.True(vm.RueckgaengigSichtbar);

        // Genau das ruft der MainViewModel beim Wechsel in den Bereich.
        vm.AktualisiereListe();

        Assert.False(vm.RueckgaengigSichtbar);

        vm.RueckgaengigCommand.Execute(null);
        Assert.Equal(Heute, _ausgaben.GetById(offene.Id)!.SettledDate);
    }

    [Fact]
    public void Ein_zweites_Abhaken_loest_das_erste_Angebot_ab()
    {
        // Sonst holte ein Druck auf "Rückgängig" den Vorgang von vorletzter
        // Stelle zurueck - das Band sagt aber, was gerade geschehen ist.
        var erste = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);
        var zweite = _ausgaben.Create(_wohnenId, 2000, Heute, _annaId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == erste.Id));
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == zweite.Id));

        vm.RueckgaengigCommand.Execute(null);

        Assert.Equal(Heute, _ausgaben.GetById(erste.Id)!.SettledDate);
        Assert.Null(_ausgaben.GetById(zweite.Id)!.SettledDate);
    }

    [Fact]
    public void Nach_dem_Loeschen_bietet_das_Band_wieder_das_Loeschen_an()
    {
        // Beide Vorgaenge teilen sich ein Band. Wer abhakt und dann loescht,
        // muss das Loeschen zurueckholen koennen - nicht das Abhaken.
        var offene = _ausgaben.Create(_wohnenId, 1000, Heute, _annaId);
        var andere = _ausgaben.Create(_wohnenId, 2000, Heute, _ichId);

        var vm = NeueListe();
        vm.AlsBeglichenCommand.Execute(vm.Zeilen.Single(z => z.Id == offene.Id));
        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == andere.Id));

        Assert.Contains("gelöscht", vm.RueckgaengigText!);

        vm.RueckgaengigCommand.Execute(null);

        // Die geloeschte Buchung ist wieder da (mit neuer Id), das Abhaken
        // steht.
        Assert.Equal(2, vm.Zeilen.Count);
        Assert.Equal(Heute, _ausgaben.GetById(offene.Id)!.SettledDate);
    }
}
