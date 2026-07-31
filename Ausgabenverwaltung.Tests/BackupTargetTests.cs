using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Tests;

public class BackupTargetTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-target-");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Beschreibbarer_Ordner_meldet_keinen_Fehler_und_laesst_nichts_zurueck()
    {
        Assert.True(BackupTarget.Check(_tempDir.FullName).IsWritable);
        Assert.Empty(Directory.GetFiles(_tempDir.FullName));
    }

    [Fact]
    public void Ein_noch_fehlender_Ordner_wird_angelegt()
    {
        var neu = Path.Combine(_tempDir.FullName, "Sicherungen");

        Assert.True(BackupTarget.Check(neu).IsWritable);
        Assert.True(Directory.Exists(neu));
    }

    [Fact]
    public void Ein_nicht_beschreibbares_Ziel_meldet_den_Fehler_sofort()
    {
        // Elternteil ist eine Datei - der Ordner kann nicht entstehen.
        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        var ergebnis = BackupTarget.Check(Path.Combine(blockierer, "Sicherungen"));

        Assert.False(ergebnis.IsWritable);
        Assert.NotNull(ergebnis.Error);
    }

    [Fact]
    public void Ein_leerer_Pfad_meldet_einen_Fehler()
    {
        var ergebnis = BackupTarget.Check("   ");

        Assert.False(ergebnis.IsWritable);
        Assert.Equal(StorageProblem.PathNotFound, ergebnis.Problem);
    }

    // Aus der Einordnung entsteht der Text, den der Anwender liest -
    // deshalb gehoert geprueft, dass ueberhaupt einer entsteht und dass er
    // nicht bei "Ein Fehler ist aufgetreten" stehen bleibt.
    [Fact]
    public void Ein_nicht_beschreibbares_Ziel_bekommt_einen_verstaendlichen_Text()
    {
        var ergebnis = BackupTarget.Check("   ");

        var text = FileErrorText.ForBackupTargetChoice(ergebnis.Problem);

        Assert.Contains("nicht als zweites Ziel übernommen", text);
        Assert.DoesNotContain("Exception", text);
    }
}
