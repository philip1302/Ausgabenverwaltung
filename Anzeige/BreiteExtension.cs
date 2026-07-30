using System;
using Avalonia.Data;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Schreibweise fuer eine mitwachsende Breite:
/// <c>Width="{anzeige:Breite 110}"</c>.
///
/// Die Zahl ist die Breite bei Stufe "Normal" - genau der Wert, der
/// vorher fest im Attribut stand. Daraus wird eine Bindung an
/// <see cref="Skalierung.Faktor"/>, die bei jedem Umschalten der
/// Schriftgroesse neu rechnet.
///
/// Absichtlich keine Automatik ueber "Auto": Kopfzeile und Zeilen sind
/// getrennte Raster und muessen exakt uebereinander stehen; sie brauchen
/// deshalb dieselbe, berechenbare Breite und keine, die sich je Zeile
/// aus dem Inhalt ergibt.
/// </summary>
public sealed class BreiteExtension
{
    public BreiteExtension()
    {
    }

    public BreiteExtension(double wert)
    {
        Wert = wert;
    }

    /// <summary>Die Breite bei Stufe "Normal", in Pixeln.</summary>
    public double Wert { get; set; }

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => new Binding
    {
        Source = Skalierung.Aktuell,
        Path = nameof(Skalierung.Faktor),
        Converter = SkalierteBreite.Instanz,
        ConverterParameter = Wert,
    };
}
