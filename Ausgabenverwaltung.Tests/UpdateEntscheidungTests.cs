using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Entscheidung, ob sich das Programm gegen eine andere Fassung
/// austauscht. Sie faellt allein aus Werten - ohne Netz, ohne Platte -
/// und ist deshalb genau der Teil, der sich vollstaendig pruefen laesst
/// (Regel 7).
/// </summary>
public class UpdateEntscheidungTests
{
    private const string WindowsAsset = "Ausgabenverwaltung-win-x64.zip";

    private static ReleaseInfo Release(
        string tag,
        string? assetName = WindowsAsset,
        string? sha256 = "e070ab8c4e522016f224c978efe43e0ce7202489ea5463d040736474bc0e5191")
    {
        AppVersion.TryParse(tag, out var version);

        return new ReleaseInfo
        {
            TagName = tag,
            Version = version,
            HtmlUrl = "https://example.invalid/release/" + tag,
            Assets = assetName is null
                ? []
                : [
                    new ReleaseAsset
                    {
                        Name = assetName,
                        DownloadUrl = "https://example.invalid/" + assetName,
                        SizeBytes = 45814502,
                        Sha256 = sha256,
                    }
                ],
        };
    }

    [Fact]
    public void Eine_neuere_Veroeffentlichung_mit_passender_Datei_wird_uebernommen()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0+c63496b", [Release("v1.2.0")], WindowsAsset);

        Assert.Equal(UpdateGrund.Verfuegbar, entscheidung.Grund);
        Assert.True(entscheidung.IstVerfuegbar);
        Assert.Equal("v1.2.0", entscheidung.Release!.TagName);
        Assert.Equal(WindowsAsset, entscheidung.Asset!.Name);
    }

    [Fact]
    public void Der_gleiche_Stand_loest_nichts_aus()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0+c63496b", [Release("v1.1.0")], WindowsAsset);

        Assert.Equal(UpdateGrund.Aktuell, entscheidung.Grund);
        Assert.False(entscheidung.IstVerfuegbar);
    }

    /// <summary>
    /// Der Fall, der eine Installation zurueckdrehen wuerde - und der
    /// deshalb nie eintreten darf.
    /// </summary>
    [Fact]
    public void Eine_aeltere_Version_im_Release_loest_keinen_Austausch_aus()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "2.0.0", [Release("v1.1.0"), Release("v1.0.0")], WindowsAsset);

        Assert.Equal(UpdateGrund.Aktuell, entscheidung.Grund);
        Assert.Null(entscheidung.Asset);
    }

    [Fact]
    public void Aus_mehreren_Veroeffentlichungen_wird_die_hoechste_genommen()
    {
        // Absichtlich unsortiert hereingegeben.
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.0.0",
            [Release("v1.1.0"), Release("v1.3.0"), Release("v1.2.0")],
            WindowsAsset);

        Assert.Equal("v1.3.0", entscheidung.Release!.TagName);
    }

    [Fact]
    public void Ohne_Datei_fuer_diese_Plattform_wird_nichts_uebernommen()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0", [Release("v1.2.0", assetName: "Ausgabenverwaltung-osx-arm64.tar.gz")],
            WindowsAsset);

        Assert.Equal(UpdateGrund.KeinPassendesAsset, entscheidung.Grund);
        Assert.Null(entscheidung.Asset);
    }

    /// <summary>
    /// Auch wenn nichts uebernommen wird, soll der Anwender nachlesen
    /// koennen - die Adresse der Veroeffentlichung bleibt deshalb
    /// erhalten.
    /// </summary>
    [Fact]
    public void Auch_ohne_passende_Datei_bleibt_die_Adresse_zum_Nachlesen()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0", [Release("v1.2.0", assetName: null)], WindowsAsset);

        Assert.Equal(UpdateGrund.KeinPassendesAsset, entscheidung.Grund);
        Assert.Equal("https://example.invalid/release/v1.2.0", entscheidung.HtmlUrl);
    }

    /// <summary>
    /// Eine Programmdatei ohne Pruefmoeglichkeit auszutauschen waere
    /// genau die Art von Zutrauen, die ein Selbstaustausch nicht haben
    /// darf.
    /// </summary>
    [Fact]
    public void Ohne_Pruefsumme_wird_nichts_uebernommen()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0", [Release("v1.2.0", sha256: null)], WindowsAsset);

        Assert.Equal(UpdateGrund.OhnePruefsumme, entscheidung.Grund);
        Assert.Null(entscheidung.Asset);
    }

    [Fact]
    public void Auf_einer_Plattform_ohne_Veroeffentlichung_passiert_nichts()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "1.1.0", [Release("v1.2.0")], assetName: null);

        Assert.Equal(UpdateGrund.PlattformOhneVeroeffentlichung, entscheidung.Grund);
    }

    /// <summary>
    /// Ein nicht erreichbares GitHub landet ebenfalls hier - die Liste
    /// ist dann schlicht leer.
    /// </summary>
    [Fact]
    public void Eine_leere_Liste_loest_nichts_aus()
    {
        var entscheidung = UpdateEntscheidung.Treffe("1.1.0", [], WindowsAsset);

        Assert.Equal(UpdateGrund.NichtsGefunden, entscheidung.Grund);
    }

    [Fact]
    public void Ohne_lesbare_eigene_Version_wird_nicht_aktualisiert()
    {
        var entscheidung = UpdateEntscheidung.Treffe(
            "unbekannt", [Release("v9.9.9")], WindowsAsset);

        Assert.Equal(UpdateGrund.Aktuell, entscheidung.Grund);
    }
}
