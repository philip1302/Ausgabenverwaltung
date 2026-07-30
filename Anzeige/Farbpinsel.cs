using System;
using System.Collections.Concurrent;
using Ausgabenverwaltung.Core.Categories;
using Avalonia.Media;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Wandelt die in der Datenbank gespeicherten Farbwerte ('#RRGGBB') in
/// Pinsel um und merkt sich das Ergebnis.
///
/// Die Zwischenspeicherung ist kein Selbstzweck: die Ausgabenliste
/// erzeugt fuer jede Zeile einen Farbbalken, bei tausenden Buchungen
/// waeren das tausende Pinsel fuer eine Handvoll Farben.
///
/// Unbrauchbare Werte ergeben den Standard-Pinsel statt einer Ausnahme:
/// eine unlesbare Farbe darf die Liste nicht aufhalten.
/// </summary>
public static class Farbpinsel
{
    private static readonly ConcurrentDictionary<string, IBrush> Pinsel = new();

    public static IBrush Fuer(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            hex = CategoryColorPalette.DefaultHex;
        }

        return Pinsel.GetOrAdd(hex, wert =>
        {
            try
            {
                return new SolidColorBrush(Color.Parse(wert)).ToImmutable();
            }
            catch (Exception)
            {
                return new SolidColorBrush(Color.Parse(CategoryColorPalette.DefaultHex)).ToImmutable();
            }
        });
    }
}
