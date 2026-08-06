using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Geprueft wird gegen einen echten, mitgeschriebenen Ausschnitt der
/// GitHub-Antwort dieses Vorhabens - nicht gegen eine Nachbildung. Eine
/// selbst erfundene Antwort wuerde genau die Felder enthalten, die der
/// Leser ohnehin erwartet, und damit nichts beweisen.
/// </summary>
public class GitHubReleasesTests
{
    // Gekuerzt auf die ausgewerteten Felder, Struktur und Schreibweise
    // unveraendert (abgerufen am 06.08.2026).
    private const string EchteAntwort = """
        [
          {
            "tag_name": "v1.1.0",
            "draft": false,
            "prerelease": false,
            "html_url": "https://github.com/philip1302/Ausgabenverwaltung/releases/tag/v1.1.0",
            "assets": [
              {
                "name": "Ausgabenverwaltung-osx-arm64.tar.gz",
                "size": 44462167,
                "digest": "sha256:eae2bb98ce53f972c379177a85e70da759a628c08daa1c4249c5e7d5a42c1082",
                "browser_download_url": "https://github.com/philip1302/Ausgabenverwaltung/releases/download/v1.1.0/Ausgabenverwaltung-osx-arm64.tar.gz"
              },
              {
                "name": "Ausgabenverwaltung-win-x64.zip",
                "size": 45814502,
                "digest": "sha256:e070ab8c4e522016f224c978efe43e0ce7202489ea5463d040736474bc0e5191",
                "browser_download_url": "https://github.com/philip1302/Ausgabenverwaltung/releases/download/v1.1.0/Ausgabenverwaltung-win-x64.zip"
              }
            ]
          },
          {
            "tag_name": "v1.0.0",
            "draft": false,
            "prerelease": false,
            "html_url": "https://github.com/philip1302/Ausgabenverwaltung/releases/tag/v1.0.0",
            "assets": [
              {
                "name": "Ausgabenverwaltung-win-x64.zip",
                "size": 45703890,
                "digest": "sha256:0d5c7abf6621da3feaaf12962675b16680529042d1382cf6cefdb0d339479278",
                "browser_download_url": "https://github.com/philip1302/Ausgabenverwaltung/releases/download/v1.0.0/Ausgabenverwaltung-win-x64.zip"
              }
            ]
          }
        ]
        """;

    [Fact]
    public void Die_echte_Antwort_wird_vollstaendig_gelesen()
    {
        var releases = GitHubReleases.Lies(EchteAntwort);

        Assert.Equal(2, releases.Count);
        Assert.Equal("v1.1.0", releases[0].TagName);
        Assert.Equal(new Version(1, 1, 0), releases[0].Version);
        Assert.Equal(
            "https://github.com/philip1302/Ausgabenverwaltung/releases/tag/v1.1.0",
            releases[0].HtmlUrl);
    }

    [Fact]
    public void Die_hoechste_Version_steht_vorn()
    {
        var releases = GitHubReleases.Lies(EchteAntwort);

        Assert.Equal(new Version(1, 1, 0), releases[0].Version);
        Assert.Equal(new Version(1, 0, 0), releases[1].Version);
    }

    [Fact]
    public void Groesse_und_Pruefsumme_eines_Assets_werden_gelesen()
    {
        var releases = GitHubReleases.Lies(EchteAntwort);
        var asset = UpdatePlatform.FindeAsset(releases[0], "Ausgabenverwaltung-win-x64.zip");

        Assert.NotNull(asset);
        Assert.Equal(45814502, asset.SizeBytes);
        Assert.Equal(
            "e070ab8c4e522016f224c978efe43e0ce7202489ea5463d040736474bc0e5191",
            asset.Sha256);
        Assert.EndsWith("Ausgabenverwaltung-win-x64.zip", asset.DownloadUrl);
    }

    /// <summary>
    /// Das Vorzeichen "sha256:" gehoert nicht zur Pruefsumme selbst - wer
    /// es stehen laesst, vergleicht spaeter nie erfolgreich.
    /// </summary>
    [Fact]
    public void Das_Verfahrensvorzeichen_steht_nicht_in_der_Pruefsumme()
    {
        var releases = GitHubReleases.Lies(EchteAntwort);
        var asset = UpdatePlatform.FindeAsset(releases[0], "Ausgabenverwaltung-win-x64.zip");

        Assert.NotNull(asset);
        Assert.DoesNotContain("sha256", asset.Sha256!);
        Assert.Equal(64, asset.Sha256!.Length);
    }

    [Fact]
    public void Ein_Entwurf_wird_uebergangen()
    {
        const string json = """
            [ { "tag_name": "v2.0.0", "draft": true, "prerelease": false, "assets": [] } ]
            """;

        Assert.Empty(GitHubReleases.Lies(json));
    }

    [Fact]
    public void Eine_Vorabversion_wird_uebergangen()
    {
        const string json = """
            [ { "tag_name": "v2.0.0", "draft": false, "prerelease": true, "assets": [] } ]
            """;

        Assert.Empty(GitHubReleases.Lies(json));
    }

    [Fact]
    public void Eine_Marke_ohne_lesbare_Version_wird_uebergangen()
    {
        const string json = """
            [ { "tag_name": "nightly", "draft": false, "prerelease": false, "assets": [] } ]
            """;

        Assert.Empty(GitHubReleases.Lies(json));
    }

    /// <summary>
    /// Eine gestoerte oder veraenderte Antwort darf nichts ausloesen -
    /// schon gar keinen Fehlerdialog wegen der Nebenfrage, ob es etwas
    /// Neueres gibt.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("kein JSON")]
    [InlineData("{ \"message\": \"Not Found\" }")]
    [InlineData("[]")]
    public void Unbrauchbare_Antworten_ergeben_eine_leere_Liste_ohne_Ausnahme(string json)
    {
        Assert.Empty(GitHubReleases.Lies(json));
    }

    [Fact]
    public void Ein_Asset_ohne_Pruefsumme_wird_als_solches_gelesen()
    {
        const string json = """
            [
              {
                "tag_name": "v2.0.0", "draft": false, "prerelease": false,
                "assets": [
                  { "name": "Ausgabenverwaltung-win-x64.zip", "size": 1,
                    "browser_download_url": "https://example.invalid/x.zip" }
                ]
              }
            ]
            """;

        var asset = UpdatePlatform.FindeAsset(
            GitHubReleases.Lies(json)[0], "Ausgabenverwaltung-win-x64.zip");

        Assert.NotNull(asset);
        Assert.Null(asset.Sha256);
    }

    /// <summary>
    /// Ein anderes Verfahren als SHA256 gilt als "keine Pruefsumme" -
    /// lieber keine Aktualisierung als eine ungeprueft uebernommene.
    /// </summary>
    [Fact]
    public void Eine_Pruefsumme_eines_anderen_Verfahrens_gilt_als_keine()
    {
        const string json = """
            [
              {
                "tag_name": "v2.0.0", "draft": false, "prerelease": false,
                "assets": [
                  { "name": "Ausgabenverwaltung-win-x64.zip", "size": 1,
                    "digest": "md5:0d5c7abf6621da3feaaf12962675b166",
                    "browser_download_url": "https://example.invalid/x.zip" }
                ]
              }
            ]
            """;

        var asset = UpdatePlatform.FindeAsset(
            GitHubReleases.Lies(json)[0], "Ausgabenverwaltung-win-x64.zip");

        Assert.NotNull(asset);
        Assert.Null(asset.Sha256);
    }

    [Fact]
    public void Ein_fehlendes_Asset_wird_nicht_erfunden()
    {
        var releases = GitHubReleases.Lies(EchteAntwort);

        // Die zweite Veroeffentlichung bringt nur die Windows-Datei mit.
        Assert.Null(UpdatePlatform.FindeAsset(releases[1], "Ausgabenverwaltung-osx-arm64.tar.gz"));
    }
}
