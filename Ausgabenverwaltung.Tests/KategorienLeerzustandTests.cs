using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Leerzustand der Kategorienliste. Er unterscheidet zwei Lagen, die
/// gleich aussehen und verschiedenes bedeuten: beim allerersten Start
/// gibt es ueberhaupt keine Kategorie - das Schema saet keine -, und
/// ohne Kategorie laesst sich nichts erfassen. Sind dagegen alle
/// vorhandenen Kategorien archiviert, fehlt nichts; es ist nur nichts zu
/// sehen.
/// </summary>
public class KategorienLeerzustandTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-leer-");

    private readonly IDbConnection _connection;
    private readonly CategoryRepository _repository;

    public KategorienLeerzustandTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new CategoryRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    private KategorienViewModel NeuesViewModel() =>
        new(_repository, new BackupService(
            _connection,
            Path.Combine(_tempDir.FullName, "Backups"),
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json"))),
            new WeakReferenceMessenger());

    [Fact]
    public void Eine_frische_Datenbank_zeigt_den_Leerzustand()
    {
        var ansicht = NeuesViewModel();

        Assert.True(ansicht.NochKeineKategorie);
        Assert.False(ansicht.NurArchivierteVorhanden);
    }

    [Fact]
    public void Mit_einer_Kategorie_bleibt_der_Leerzustand_weg()
    {
        _repository.Create("Wohnen", parentId: null);

        var ansicht = NeuesViewModel();

        Assert.False(ansicht.NochKeineKategorie);
        Assert.False(ansicht.NurArchivierteVorhanden);
    }

    [Fact]
    public void Sind_alle_Kategorien_archiviert_heisst_es_ausgeblendet_und_nicht_leer()
    {
        var id = _repository.Create("Wohnen", parentId: null).Id;
        _repository.Archive(id, includeDescendants: false);

        var ansicht = NeuesViewModel();

        Assert.False(ansicht.NochKeineKategorie);
        Assert.True(ansicht.NurArchivierteVorhanden);
    }

    /// <summary>
    /// Der Knopf des zweiten Leerzustands setzt denselben Schalter wie
    /// die Werkzeugleiste - danach ist die Liste nicht mehr leer.
    /// </summary>
    [Fact]
    public void Archivierte_einblenden_holt_sie_zurueck_in_die_Liste()
    {
        var id = _repository.Create("Wohnen", parentId: null).Id;
        _repository.Archive(id, includeDescendants: false);
        var ansicht = NeuesViewModel();

        ansicht.ArchivierteEinblendenCommand.Execute(null);

        Assert.True(ansicht.ArchivierteAnzeigen);
        Assert.False(ansicht.NurArchivierteVorhanden);
        Assert.Single(ansicht.Wurzelknoten);
    }

    /// <summary>
    /// Der Knopf "Kategorie anlegen" legt eine Zeile an, die auf ihren
    /// Namen wartet - und das Eingabefeld dafuer steckt im Baum. Bliebe
    /// der Leerzustand davor stehen, sähe der Anwender nach dem Druecken
    /// unveraendert denselben Satz.
    /// </summary>
    [Fact]
    public void Nach_dem_Anlegen_weicht_der_Leerzustand_sofort()
    {
        var ansicht = NeuesViewModel();
        Assert.True(ansicht.NochKeineKategorie);

        ansicht.NeueOberkategorieCommand.Execute(null);

        Assert.False(ansicht.NochKeineKategorie);
        Assert.Single(ansicht.Wurzelknoten);
    }
}
