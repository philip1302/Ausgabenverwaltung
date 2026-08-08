using Ausgabenverwaltung.Core.Settings;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Ein <see cref="AppSettingsStore"/> auf einer eigenen, wegwerfbaren
/// Datei.
///
/// Noetig, weil ViewModels Einstellungen nicht nur lesen, sondern auch
/// SCHREIBEN - die Sortierung der Listen, das Behalten der Werte in der
/// Erfassungsmaske. Ohne eigenen Pfad liefe jeder solche Test in die echte
/// settings.json des Rechners: er veraenderte die Einstellungen des
/// Entwicklers, und die Tests haengen voneinander ab, weil der eine liest,
/// was der andere geschrieben hat.
///
/// Die Datei wird nicht angelegt, sondern nur benannt. Ein noch nicht
/// vorhandener Pfad ist genau der Zustand "nichts gemerkt", und
/// <see cref="AppSettingsStore.Load"/> liefert dafuer die Vorgabewerte.
/// </summary>
public sealed class TestEinstellungen : IDisposable
{
    private readonly DirectoryInfo _ordner =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-einstellungen-");

    public TestEinstellungen()
    {
        Store = new AppSettingsStore(Path.Combine(_ordner.FullName, "settings.json"));
    }

    public AppSettingsStore Store { get; }

    /// <summary>Der aktuelle Stand aus der Datei - fuer die Zusicherung,
    /// dass etwas tatsaechlich gemerkt wurde.</summary>
    public AppSettings Lies() => Store.Load();

    public void Dispose()
    {
        try
        {
            _ordner.Delete(recursive: true);
        }
        catch (Exception)
        {
            // Ein liegen gebliebener Temp-Ordner ist kein Grund, einen
            // sonst gruenen Testlauf rot zu machen.
        }
    }
}
