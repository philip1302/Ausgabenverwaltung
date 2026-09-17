using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Was die Erfassungsmaske nach dem Speichern stehen laesst - mit
/// demselben ViewModel, das die Ansicht bindet, aber ohne Fenster.
///
/// Der Unterschied zwischen den beiden Betriebsarten ist der ganze Punkt:
/// im Regelfall ein leeres Formular, bei angehakter Serienerfassung ein
/// vorbereitetes.
/// </summary>
public class SerienerfassungTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-serie-");

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    // Eigene Instanz statt WeakReferenceMessenger.Default (Regel 14).
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    public SerienerfassungTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _personen.Create("Ich", isSelf: true);
        _kategorien.Create("Lebensmittel", parentId: null);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _tempDir.Delete(recursive: true);
    }

    private AppSettingsStore Speicher()
        => new(Path.Combine(_tempDir.FullName, "settings.json"));

    private ErfassenViewModel NeueErfassung(AppSettingsStore? speicher = null)
        => new(_ausgaben, _kategorien, _personen, speicher ?? Speicher(), _messenger,
            new ToastViewModel());

    [Fact]
    public async Task Ohne_Serienerfassung_ist_die_Kategorie_danach_leer()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Null(vm.AusgewaehlteKategorie);
        Assert.Equal(string.Empty, vm.BetragText);
    }

    [Fact]
    public async Task Mit_Serienerfassung_bleiben_Kategorie_Zahler_und_Datum_stehen()
    {
        var vm = NeueErfassung();
        vm.WerteBehalten = true;
        vm.BetragText = "12,50";
        vm.Bemerkung = "Beleg 1";
        vm.DatumText = "05.03.2026";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        var kategorie = vm.AusgewaehlteKategorie;
        var zahler = vm.AusgewaehlterZahler;

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Same(kategorie, vm.AusgewaehlteKategorie);
        Assert.Same(zahler, vm.AusgewaehlterZahler);
        Assert.Equal("05.03.2026", vm.DatumText);

        // Geleert werden nur die beiden Felder, die sich von Beleg zu
        // Beleg tatsaechlich unterscheiden.
        Assert.Equal(string.Empty, vm.BetragText);
        Assert.Null(vm.Bemerkung);
    }

    [Fact]
    public async Task Mit_Serienerfassung_bleibt_auch_das_Einnahme_Haekchen_stehen()
    {
        // Wer eine Reihe Einnahmen erfasst, will das Haekchen nicht
        // fuenfmal setzen. Unbemerkt bleibt es dabei nicht: es steht
        // sichtbar im Formular.
        _personen.Create("Anna", isSelf: false);

        var vm = NeueErfassung();
        vm.WerteBehalten = true;
        vm.IstEinnahme = true;
        vm.BetragText = "300,00";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.IstEinnahme);
    }

    [Fact]
    public void Die_Einstellung_uebersteht_den_Wechsel_der_Sitzung()
    {
        var speicher = Speicher();

        var erste = NeueErfassung(speicher);
        erste.WerteBehalten = true;

        Assert.True(NeueErfassung(speicher).WerteBehalten);
    }

    [Fact]
    public void Die_Schnellwahl_zeigt_die_haeufigste_Kategorie()
    {
        var brot = _kategorien.GetSelectableLeaves().Single();
        var person = _personen.GetAllActive().Single(p => p.IsSelf);
        _ausgaben.Create(brot.Id, 1000, DateOnly.FromDateTime(DateTime.Now), person.Id);

        var vm = NeueErfassung();

        Assert.True(vm.SchnellwahlSichtbar);
        Assert.Equal(brot.Id, Assert.Single(vm.Schnellwahl).Id);
    }

    [Fact]
    public void Ein_Klick_auf_die_Schnellwahl_setzt_die_Kategorie_aus_den_Vorschlaegen()
    {
        // Nicht die angeklickte Instanz selbst: das Kategoriefeld
        // vergleicht seine Auswahl ueber die Objektgleichheit.
        var person = _personen.GetAllActive().Single(p => p.IsSelf);
        var kategorie = _kategorien.GetSelectableLeaves().Single();
        _ausgaben.Create(kategorie.Id, 1000, DateOnly.FromDateTime(DateTime.Now), person.Id);

        var vm = NeueErfassung();
        vm.SchnellwahlWaehlenCommand.Execute(vm.Schnellwahl[0]);

        Assert.Same(vm.KategorieVorschlaege.Single(o => o.Id == kategorie.Id), vm.AusgewaehlteKategorie);
    }

    [Fact]
    public void Ohne_Buchungen_bleibt_die_Schnellwahl_unsichtbar()
    {
        var vm = NeueErfassung();

        Assert.Empty(vm.Schnellwahl);
        Assert.False(vm.SchnellwahlSichtbar);
    }
}
