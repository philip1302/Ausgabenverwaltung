using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

public class GermanDateInputTests
{
    [Theory]
    [InlineData("29.07.2026", 2026, 7, 29)]
    [InlineData("1.1.2026", 2026, 1, 1)] // ohne fuehrende Nullen
    [InlineData("01.01.2026", 2026, 1, 1)]
    public void TryParse_erkennt_gueltige_Datumsangaben(string text, int jahr, int monat, int tag)
    {
        var erfolgreich = GermanDateInput.TryParse(text, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2026-07-29")] // ISO-Format wird hier nicht akzeptiert
    [InlineData("32.01.2026")] // Tag existiert nicht
    [InlineData("29.13.2026")] // Monat existiert nicht
    public void TryParse_lehnt_ungueltige_Eingabe_ab(string text)
    {
        var erfolgreich = GermanDateInput.TryParse(text, out _);

        Assert.False(erfolgreich);
    }

    [Fact]
    public void ToText_formatiert_mit_fuehrenden_Nullen()
    {
        var text = GermanDateInput.ToText(new DateOnly(2026, 1, 5));

        Assert.Equal("05.01.2026", text);
    }

    [Fact]
    public void Rundtrip_ist_stabil()
    {
        var date = new DateOnly(2026, 7, 29);

        var erfolgreich = GermanDateInput.TryParse(GermanDateInput.ToText(date), out var geparst);

        Assert.True(erfolgreich);
        Assert.Equal(date, geparst);
    }
}
