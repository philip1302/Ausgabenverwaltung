using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Loest einen Icon-Ressourcenschluessel (<see cref="ViewModels.NavigationItem.IconKey"/>,
/// z. B. "IconStartseite") in die zugehoerige StreamGeometry aus
/// App.axaml auf. Ein Konverter statt einer direkten DynamicResource-
/// Bindung, weil der Schluessel selbst erst zur Laufzeit aus dem
/// ViewModel kommt - {DynamicResource} kann keinen gebundenen Schluessel
/// entgegennehmen.
///
/// Icons sind nicht themenabhaengig (nur Farben sind es, siehe
/// App.axaml/ThemeDictionaries), deshalb genuegt ein einmaliges
/// Nachschlagen statt einer lebenden Bindung.
/// </summary>
public sealed class IconSchluesselZuGeometrie : IValueConverter
{
    public static IconSchluesselZuGeometrie Instanz { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string schluessel || Application.Current is not { } anwendung)
        {
            return null;
        }

        return anwendung.TryGetResource(schluessel, null, out var geometrie) ? geometrie : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Icons werden nur gelesen.");
}
