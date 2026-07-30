using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class BackupFileNameTests
{
    [Fact]
    public void Create_bildet_den_vereinbarten_Namen()
    {
        var name = BackupFileName.Create(new DateTime(2026, 7, 30, 18, 42, 17));

        Assert.Equal("ausgaben_2026-07-30_1842.zip", name);
    }

    [Fact]
    public void TryParseTimestamp_liest_den_Zeitstempel_zurueck()
    {
        var erwartet = new DateTime(2026, 7, 30, 18, 42, 0);

        Assert.True(BackupFileName.TryParseTimestamp(BackupFileName.Create(erwartet), out var gelesen));
        Assert.Equal(erwartet, gelesen);
    }

    // Fremde Dateien duerfen weder als Sicherung gezaehlt noch von der
    // Aufbewahrung geloescht werden.
    [Theory]
    [InlineData("ausgaben.db")]
    [InlineData("notizen.zip")]
    [InlineData("ausgaben_2026-07-30.zip")]
    [InlineData("ausgaben_30.07.2026_1842.zip")]
    [InlineData("ausgaben_2026-13-30_1842.zip")]
    [InlineData("kopie_ausgaben_2026-07-30_1842.zip")]
    public void TryParseTimestamp_lehnt_fremde_Namen_ab(string dateiname)
    {
        Assert.False(BackupFileName.TryParseTimestamp(dateiname, out _));
    }
}
