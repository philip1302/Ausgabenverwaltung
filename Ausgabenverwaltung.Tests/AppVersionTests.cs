using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Versionsvergleich ist die Stelle, an der eine automatische
/// Aktualisierung falsch abbiegen kann: liest sie zu streng, gibt es nie
/// ein Update, liest sie zu grosszuegig, tauscht sie sich gegen einen
/// aelteren Stand aus.
/// </summary>
public class AppVersionTests
{
    [Theory]
    [InlineData("1.1.0", 1, 1, 0)]
    [InlineData("v1.1.0", 1, 1, 0)]
    [InlineData("V1.1.0", 1, 1, 0)]
    [InlineData("  1.1.0  ", 1, 1, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1.1.0.0", 1, 1, 0)]
    public void Gebraeuchliche_Schreibweisen_werden_gelesen(
        string text, int major, int minor, int build)
    {
        Assert.True(AppVersion.TryParse(text, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    /// <summary>
    /// Die eigene Version traegt IMMER den Git-Stand hinter einem
    /// Pluszeichen - das haengt die .NET-Werkzeugkette von sich aus an.
    /// Wer ihn nicht abschneidet, findet nie eine Uebereinstimmung.
    /// </summary>
    [Fact]
    public void Die_eigene_Version_mit_Git_Stand_wird_ohne_diesen_gelesen()
    {
        Assert.True(
            AppVersion.TryParse("1.1.0+c63496bec93093cc2c9f4fc0cf62ecc2abdfb345", out var version));

        Assert.Equal(new Version(1, 1, 0), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unbekannt")]
    [InlineData("v")]
    [InlineData("1")]
    public void Unlesbares_wird_abgelehnt_statt_zu_werfen(string? text)
    {
        Assert.False(AppVersion.TryParse(text, out _));
    }

    [Fact]
    public void Eine_hoehere_Version_gilt_als_neuer()
    {
        Assert.True(AppVersion.IstNeuer("1.1.0", "v1.2.0"));
        Assert.True(AppVersion.IstNeuer("1.1.0+abc", "v2.0.0"));
        Assert.True(AppVersion.IstNeuer("1.1.0", "v1.1.1"));
    }

    [Fact]
    public void Der_gleiche_Stand_gilt_nicht_als_neuer()
    {
        Assert.False(AppVersion.IstNeuer("1.1.0", "v1.1.0"));

        // Dieselbe Version, einmal mit und einmal ohne Git-Stand.
        Assert.False(AppVersion.IstNeuer("1.1.0+c63496b", "v1.1.0"));
    }

    /// <summary>
    /// Der Fall, der eine Installation zurueckdrehen wuerde.
    /// </summary>
    [Fact]
    public void Eine_aeltere_Version_gilt_nie_als_neuer()
    {
        Assert.False(AppVersion.IstNeuer("1.1.0", "v1.0.0"));
        Assert.False(AppVersion.IstNeuer("2.0.0", "v1.9.9"));
    }

    [Fact]
    public void Ohne_lesbare_Version_wird_im_Zweifel_nicht_aktualisiert()
    {
        Assert.False(AppVersion.IstNeuer("unbekannt", "v9.9.9"));
        Assert.False(AppVersion.IstNeuer("1.0.0", "kaputt"));
        Assert.False(AppVersion.IstNeuer(null, null));
    }

    /// <summary>
    /// "1.1" und "1.1.0" sind derselbe Stand. Ohne Vereinheitlichung
    /// waere "1.1" kleiner (Version fuellt fehlende Stellen mit -1) und
    /// wer auf "1.1" laeuft, bekaeme "1.1.0" als vermeintlich neuer
    /// angeboten.
    /// </summary>
    [Fact]
    public void Fehlende_Stellen_zaehlen_als_Null_und_nicht_als_kleiner()
    {
        Assert.False(AppVersion.IstNeuer("1.1", "v1.1.0"));
        Assert.False(AppVersion.IstNeuer("1.1.0", "v1.1"));
    }

    [Fact]
    public void Der_Anzeigetext_laesst_den_Git_Stand_weg()
    {
        Assert.Equal("1.1.0", AppVersion.Anzeigetext("1.1.0+c63496bec93093cc"));
        Assert.Equal("1.1.0", AppVersion.Anzeigetext("v1.1.0"));
    }

    [Fact]
    public void Ein_unlesbarer_Anzeigetext_wird_nicht_verschwiegen()
    {
        Assert.Equal("Eigenbau", AppVersion.Anzeigetext("Eigenbau"));
        Assert.Equal("unbekannt", AppVersion.Anzeigetext(null));
    }

    [Fact]
    public void Eine_Version_wird_zur_Marke_wie_sie_im_Projekt_vergeben_wird()
    {
        Assert.Equal("v1.1.0", AppVersion.ZuMarke(new Version(1, 1, 0)));
    }
}
