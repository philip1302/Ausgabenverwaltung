using System.Data;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Media;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Kette vom Farbknopf bis zum Punkt im Baum - ohne Fenster, aber
/// mit denselben ViewModels, die die Ansicht bindet.
/// </summary>
public class KategorienFarbenTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly CategoryRepository _repository;
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-farben-");

    public KategorienFarbenTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new CategoryRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _tempDir.Delete(recursive: true);
    }

    // Das ViewModel braucht die Sicherung nur fuer das Zusammenfuehren.
    // Die Farbtests loesen sie nie aus, deshalb genuegt hier ein Dienst
    // auf ein leeres Temp-Verzeichnis.
    private KategorienViewModel NeuesViewModel() =>
        new(_repository, new BackupService(
            _connection,
            Path.Combine(_tempDir.FullName, "Backups"),
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json"))));

    private static Color FarbeVon(IBrush pinsel) => ((ISolidColorBrush)pinsel).Color;

    [Fact]
    public void Der_Pinsel_zu_einem_Palettenwert_hat_genau_diese_Farbe()
    {
        var pinsel = Farbpinsel.Fuer("#2980B9");

        Assert.Equal(Color.Parse("#2980B9"), FarbeVon(pinsel));
        Assert.NotEqual(
            FarbeVon(Farbpinsel.Fuer(CategoryColorPalette.DefaultHex)),
            FarbeVon(pinsel));
    }

    [Fact]
    public void Jede_Farbe_der_Auswahl_traegt_ihren_eigenen_Pinsel()
    {
        var optionen = NeuesViewModel().Farboptionen;

        Assert.Equal(20, optionen.Count);
        Assert.True(optionen[0].IstKeineFarbe);

        var echteFarben = optionen.Skip(1).Select(o => FarbeVon(o.Pinsel)).ToList();
        Assert.Equal(19, echteFarben.Distinct().Count());
        Assert.DoesNotContain(Color.Parse(CategoryColorPalette.DefaultHex), echteFarben);
    }

    [Fact]
    public void Farbe_waehlen_schreibt_sie_und_faerbt_den_Knoten_um()
    {
        var pferde = _repository.Create("Pferde", null);
        var hufschmied = _repository.Create("Hufschmied", pferde.Id);

        var viewModel = NeuesViewModel();
        viewModel.AusgewaehlterKnoten = viewModel.Wurzelknoten.Single();
        viewModel.OeffneFarbwahl();

        // Die Baumauswahl geht beim Oeffnen des Aufklappfensters
        // verloren - die Farbwahl muss das aushalten.
        viewModel.AusgewaehlterKnoten = null;

        var blau = viewModel.Farboptionen.Single(o => o.Bezeichnung == "Blau");
        Assert.True(viewModel.FarbeSetzenCommand.CanExecute(blau));
        viewModel.FarbeSetzenCommand.Execute(blau);

        // In der Datenbank ...
        Assert.Equal(blau.Hex, _repository.GetTree().Single().Category.Color);

        // ... und im Baum, den die Ansicht bindet - samt geerbter Farbe
        // der Unterkategorie.
        var wurzel = viewModel.Wurzelknoten.Single();
        Assert.Equal(blau.Hex, wurzel.EigeneFarbe);
        Assert.Equal(Color.Parse(blau.Hex!), FarbeVon(wurzel.Farbe));

        var kind = wurzel.Children.Single();
        Assert.Equal(hufschmied.Id, kind.Id);
        Assert.Null(kind.EigeneFarbe);
        Assert.Equal(Color.Parse(blau.Hex!), FarbeVon(kind.Farbe));
    }

    [Fact]
    public void Keine_Farbe_waehlen_nimmt_die_eigene_Farbe_zurueck()
    {
        var pferde = _repository.Create("Pferde", null);
        _repository.SetColor(pferde.Id, "#2980B9");

        var viewModel = NeuesViewModel();
        viewModel.AusgewaehlterKnoten = viewModel.Wurzelknoten.Single();
        viewModel.OeffneFarbwahl();
        viewModel.FarbeSetzenCommand.Execute(viewModel.Farboptionen.Single(o => o.IstKeineFarbe));

        Assert.Null(_repository.GetTree().Single().Category.Color);
        Assert.Equal(
            Color.Parse(CategoryColorPalette.DefaultHex),
            FarbeVon(viewModel.Wurzelknoten.Single().Farbe));
    }

    // Die Eintraege der Farbwahl sind bewusst NICHT gesperrt - sonst
    // stuenden sie im Moment des Oeffnens grau da. Ohne Kategorie
    // passiert schlicht nichts.
    [Fact]
    public void Ohne_Kategorie_bewirkt_die_Farbwahl_nichts()
    {
        _repository.Create("Pferde", null);

        var viewModel = NeuesViewModel();
        Assert.Null(viewModel.AusgewaehlterKnoten);

        viewModel.FarbeSetzenCommand.Execute(viewModel.Farboptionen[1]);

        Assert.Null(_repository.GetTree().Single().Category.Color);
    }

    [Fact]
    public void Die_Farbwahl_nennt_die_Kategorie_um_die_es_geht()
    {
        _repository.Create("Pferde", null);

        var viewModel = NeuesViewModel();
        viewModel.AusgewaehlterKnoten = viewModel.Wurzelknoten.Single();
        viewModel.OeffneFarbwahl();

        Assert.Contains("Pferde", viewModel.FarbwahlHinweis);
        Assert.Contains("geerbt", viewModel.FarbwahlHinweis);
    }
}
