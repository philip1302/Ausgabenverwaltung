using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class ExternalBackupAgeTests
{
    private static readonly DateTime Jetzt = new(2026, 7, 30, 18, 42, 0, DateTimeKind.Utc);

    [Fact]
    public void Ohne_jede_externe_Sicherung_gilt_der_Zustand_als_veraltet()
    {
        Assert.Null(ExternalBackupAge.DaysSince(null, Jetzt));
        Assert.Equal("noch nie", ExternalBackupAge.ToText(null, Jetzt));
        Assert.True(ExternalBackupAge.IsStale(null, Jetzt));
    }

    [Theory]
    [InlineData(0, "heute")]
    [InlineData(1, "gestern")]
    [InlineData(12, "vor 12 Tagen")]
    public void Alter_wird_in_Kalendertagen_benannt(int tage, string erwartet)
    {
        var zuletzt = Jetzt.Date.AddDays(-tage).AddHours(3);

        Assert.Equal(tage, ExternalBackupAge.DaysSince(zuletzt, Jetzt));
        Assert.Equal(erwartet, ExternalBackupAge.ToText(zuletzt, Jetzt));
    }

    [Fact]
    public void Erst_ueber_vierzehn_Tagen_wird_hervorgehoben()
    {
        Assert.False(ExternalBackupAge.IsStale(Jetzt.AddDays(-ExternalBackupAge.StaleAfterDays), Jetzt));
        Assert.True(ExternalBackupAge.IsStale(Jetzt.AddDays(-ExternalBackupAge.StaleAfterDays - 1), Jetzt));
    }

    [Fact]
    public void Ein_Zeitpunkt_in_der_Zukunft_gilt_als_heute()
    {
        // Verstellte Systemuhr: "vor -3 Tagen" waere Unsinn.
        Assert.Equal(0, ExternalBackupAge.DaysSince(Jetzt.AddDays(3), Jetzt));
        Assert.Equal("heute", ExternalBackupAge.ToText(Jetzt.AddDays(3), Jetzt));
    }
}
