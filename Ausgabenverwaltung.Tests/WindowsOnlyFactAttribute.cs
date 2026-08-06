using System.Runtime.InteropServices;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Ein <see cref="FactAttribute"/> fuer Tests, deren Annahme nur unter
/// Windows gilt - und die deshalb ueberall sonst uebersprungen gehoeren
/// statt fehlzuschlagen.
///
/// Betrifft bisher nur die Dateisperren: <c>FileShare.None</c> ist unter
/// Windows verbindlich, unter Linux und macOS dagegen bloss beratend
/// (<c>flock</c>). Das native SQLite sperrt ueber <c>fcntl</c>-Bytebereiche
/// und laesst sich davon nicht aufhalten - der gepruefte Fall "ein anderes
/// Programm haelt die Datei fest" entsteht dort also gar nicht erst.
///
/// Bewusst kein stilles Durchwinken: xUnit fuehrt uebersprungene Tests mit
/// Grund auf, damit im Testlauf sichtbar bleibt, was NICHT geprueft wurde.
/// Unter Windows - und damit im Release-Workflow, der auf windows-latest
/// laeuft - laufen sie unveraendert mit.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Nur unter Windows: verbindliche Dateisperren gibt es "
                 + "unter Linux und macOS nicht.";
        }
    }
}
