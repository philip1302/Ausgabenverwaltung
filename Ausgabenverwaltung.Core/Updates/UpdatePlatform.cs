using System.Runtime.InteropServices;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Welche Datei einer Veroeffentlichung zu welcher Plattform gehoert -
/// und ob sich diese Plattform ueberhaupt selbst austauschen laesst.
///
/// Die Namen sind dieselben, die publish.ps1 erzeugt und die am Release
/// haengen. Sie stehen hier ein zweites Mal, was unschoen ist; ein
/// gemeinsamer Ort dafuer gibt es nicht, weil das Skript in PowerShell
/// laeuft und diese Baugruppe in C#. Aendert sich ein Name dort, faellt
/// es hier durch <see cref="Ausgabenverwaltung.Core.Updates"/>-Tests auf.
/// </summary>
public static class UpdatePlatform
{
    /// <summary>
    /// Der Dateiname, den eine Veroeffentlichung fuer die laufende
    /// Plattform tragen muss. <c>null</c> bei allem, wofuer nichts
    /// veroeffentlicht wird - dann findet keine Aktualisierung statt.
    /// </summary>
    public static string? AssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            return "Ausgabenverwaltung-win-x64.zip";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "Ausgabenverwaltung-osx-arm64.tar.gz"
                : "Ausgabenverwaltung-osx-x64.tar.gz";
        }

        // Linux wird gebaut, aber nicht veroeffentlicht - es gibt dort
        // also nichts zu holen.
        return null;
    }

    /// <summary>
    /// Sucht in einer Veroeffentlichung die Datei fuer die angegebene
    /// Plattform. <c>null</c>, wenn sie fehlt: eine Veroeffentlichung
    /// kann durchaus nur einen Teil der Plattformen mitbringen, und das
    /// ist kein Fehler, sondern nur "fuer dich ist hier nichts dabei".
    /// </summary>
    public static ReleaseAsset? FindeAsset(ReleaseInfo release, string assetName)
        => release.Assets.FirstOrDefault(
            asset => string.Equals(asset.Name, assetName, StringComparison.OrdinalIgnoreCase));
}
