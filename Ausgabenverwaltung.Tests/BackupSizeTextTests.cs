using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class BackupSizeTextTests
{
    [Theory]
    [InlineData(0, "0 Bytes")]
    [InlineData(512, "512 Bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "2 KB")]
    [InlineData(1048576, "1,0 MB")]
    [InlineData(1572864, "1,5 MB")]
    [InlineData(1520435, "1,4 MB")]
    public void Groesse_wird_deutsch_und_gerundet_ausgegeben(long bytes, string erwartet)
    {
        Assert.Equal(erwartet, BackupSizeText.Format(bytes));
    }
}
