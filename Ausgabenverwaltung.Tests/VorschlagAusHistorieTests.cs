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
/// Das Angebot der Erfassungsmaske: Bemerkung getippt, die Werte der
/// letzten gleichlautenden Buchung stehen bereit.
///
/// Eingesetzt wird erst auf Klick - das ist der Punkt dieser Tests. Eine
/// Maske, die sich beim Tippen von selbst fuellt, ueberschreibt frueher
/// oder spaeter etwas, das schon richtig war.
/// </summary>
public class VorschlagAusHistorieTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-vorschlag-");

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    private readonly int _lebensmittelId;
    private readonly int _annaId;

    public VorschlagAusHistorieTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _lebensmittelId = _kategorien.Create("Lebensmittel", parentId: null).Id;
        _kategorien.Create("Sonstiges", parentId: null);
        _personen.Create("Ich", isSelf: true);
        _annaId = _personen.Create("Anna", isSelf: false).Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _tempDir.Delete(recursive: true);
    }

    private ErfassenViewModel NeueErfassung() => new(
        _ausgaben, _kategorien, _personen,
        new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json")),
        new WeakReferenceMessenger());

    /// <summary>
    /// Wartet auf das entprellte Angebot. Gepollt statt fest gewartet,
    /// damit der Test nicht an der Entprellzeit klebt.
    /// </summary>
    private static async Task<bool> WarteAufVorschlag(ErfassenViewModel vm, bool erwartet = true)
    {
        for (var versuch = 0; versuch < 40; versuch++)
        {
            if (vm.VorschlagSichtbar == erwartet)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return vm.VorschlagSichtbar == erwartet;
    }

    [Fact]
    public async Task Eine_getippte_Bemerkung_holt_das_Angebot()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.Bemerkung = "Aldi";

        Assert.True(await WarteAufVorschlag(vm));
        Assert.Contains("Lebensmittel", vm.VorschlagText);
        Assert.Contains("42,90", vm.VorschlagText);
        Assert.Contains("Anna", vm.VorschlagText);
    }

    [Fact]
    public async Task Das_Angebot_setzt_sich_nicht_von_selbst_ein()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.Bemerkung = "Aldi";
        await WarteAufVorschlag(vm);

        Assert.Equal(string.Empty, vm.BetragText);
        Assert.Null(vm.AusgewaehlteKategorie);
    }

    [Fact]
    public async Task Uebernehmen_fuellt_Kategorie_Betrag_und_Zahler()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.DatumText = "05.03.2026";
        vm.Bemerkung = "Aldi";
        await WarteAufVorschlag(vm);

        vm.VorschlagUebernehmenCommand.Execute(null);

        Assert.Equal("42,90", vm.BetragText);
        Assert.Equal(_lebensmittelId, vm.AusgewaehlteKategorie!.Id);
        Assert.Equal(_annaId, vm.AusgewaehlterZahler.Id);

        // Das Datum gehoert dem Beleg, der gerade vor einem liegt, nicht
        // dem von damals.
        Assert.Equal("05.03.2026", vm.DatumText);

        // Angenommen heisst erledigt.
        Assert.False(vm.VorschlagSichtbar);
    }

    [Fact]
    public async Task Uebernehmen_behaelt_die_Buchungsart()
    {
        // Sonst wuerde aus einer Einnahme von Anna still eine Ausgabe an
        // Anna.
        _ausgaben.Create(
            _lebensmittelId, 30000, new DateOnly(2026, 3, 1), _annaId, note: "Gehalt", isIncome: true);

        var vm = NeueErfassung();
        vm.Bemerkung = "Gehalt";
        await WarteAufVorschlag(vm);

        vm.VorschlagUebernehmenCommand.Execute(null);

        Assert.True(vm.IstEinnahme);
        Assert.Equal(_annaId, vm.AusgewaehlterZahler.Id);
    }

    [Fact]
    public async Task Unter_drei_Zeichen_wird_gar_nicht_erst_gesucht()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Al");

        var vm = NeueErfassung();
        vm.Bemerkung = "Al";

        Assert.True(await WarteAufVorschlag(vm, erwartet: false));
    }

    [Fact]
    public async Task Ohne_Treffer_bleibt_das_Band_weg()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.Bemerkung = "Rewe";

        Assert.True(await WarteAufVorschlag(vm, erwartet: false));
    }

    [Fact]
    public async Task Eine_geaenderte_Bemerkung_raeumt_das_alte_Angebot_sofort_weg()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.Bemerkung = "Aldi";
        await WarteAufVorschlag(vm);

        vm.Bemerkung = "Aldi Getränke";

        // Ohne Warten: das Band zu "Aldi" darf nicht neben einer anderen
        // Bemerkung stehen bleiben.
        Assert.False(vm.VorschlagSichtbar);
    }

    [Fact]
    public async Task Verwerfen_schliesst_das_Band()
    {
        _ausgaben.Create(_lebensmittelId, 4290, new DateOnly(2026, 3, 1), _annaId, note: "Aldi");

        var vm = NeueErfassung();
        vm.Bemerkung = "Aldi";
        await WarteAufVorschlag(vm);

        vm.VorschlagVerwerfenCommand.Execute(null);

        Assert.False(vm.VorschlagSichtbar);
        Assert.Equal(string.Empty, vm.BetragText);
    }
}
