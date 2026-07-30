using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Zahler-Auswahl im Report: "alle", "nur ich", "nur
/// andere". Bewusst nicht <see cref="ZahlerOption"/>: die Ausgabenliste
/// waehlt EINE Person aus, hier geht es um die Gruppe (Regel 4 - "eigen"
/// gegen "fremd").
/// </summary>
public sealed class ZahlerBereichOption
{
    public ZahlerBereichOption(string bezeichnung, PayerScope wert)
    {
        Bezeichnung = bezeichnung;
        Wert = wert;
    }

    public string Bezeichnung { get; }
    public PayerScope Wert { get; }
}
