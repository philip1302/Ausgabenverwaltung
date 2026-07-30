using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class BackupTargetTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-target-");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Beschreibbarer_Ordner_meldet_keinen_Fehler_und_laesst_nichts_zurueck()
    {
        Assert.Null(BackupTarget.TestWritable(_tempDir.FullName));
        Assert.Empty(Directory.GetFiles(_tempDir.FullName));
    }

    [Fact]
    public void Ein_noch_fehlender_Ordner_wird_angelegt()
    {
        var neu = Path.Combine(_tempDir.FullName, "Sicherungen");

        Assert.Null(BackupTarget.TestWritable(neu));
        Assert.True(Directory.Exists(neu));
    }

    [Fact]
    public void Ein_nicht_beschreibbares_Ziel_meldet_den_Fehler_sofort()
    {
        // Elternteil ist eine Datei - der Ordner kann nicht entstehen.
        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        Assert.NotNull(BackupTarget.TestWritable(Path.Combine(blockierer, "Sicherungen")));
    }

    [Fact]
    public void Ein_leerer_Pfad_meldet_einen_Fehler()
    {
        Assert.NotNull(BackupTarget.TestWritable("   "));
    }
}
