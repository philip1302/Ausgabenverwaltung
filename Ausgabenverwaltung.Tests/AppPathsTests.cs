using Ausgabenverwaltung.Core.Database;

namespace Ausgabenverwaltung.Tests;

public class AppPathsTests : IDisposable
{
    private readonly DirectoryInfo _tempRoot = Directory.CreateTempSubdirectory("ausgabenverwaltung-approot-");

    public void Dispose() => _tempRoot.Delete(recursive: true);

    [Fact]
    public void GetDatabaseFilePath_legt_Anwendungsordner_an_falls_er_fehlt()
    {
        var path = AppPaths.GetDatabaseFilePath(_tempRoot.FullName);

        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Fact]
    public void GetDatabaseFilePath_liefert_ausgaben_db_unterhalb_von_Ausgabenverwaltung()
    {
        var path = AppPaths.GetDatabaseFilePath(_tempRoot.FullName);

        Assert.Equal(
            Path.Combine(_tempRoot.FullName, "Ausgabenverwaltung", "ausgaben.db"),
            path);
    }

    [Fact]
    public void GetDatabaseFilePath_wirft_nicht_wenn_Ordner_schon_existiert()
    {
        AppPaths.GetDatabaseFilePath(_tempRoot.FullName);

        var path = AppPaths.GetDatabaseFilePath(_tempRoot.FullName);

        Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
    }
}
