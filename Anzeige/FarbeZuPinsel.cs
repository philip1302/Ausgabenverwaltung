using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Macht aus einem gespeicherten Farbwert ('#RRGGBB') einen Pinsel -
/// fuer Bindungen an Werte, die als Text aus Core kommen (etwa
/// <see cref="Core.Categories.CategoryOption.Color"/>).
///
/// Verwendung:
/// <c>Background="{Binding Color, Converter={x:Static anzeige:FarbeZuPinsel.Instanz}}"</c>
/// </summary>
public sealed class FarbeZuPinsel : IValueConverter
{
    public static FarbeZuPinsel Instanz { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Farbpinsel.Fuer(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Farben werden nur gelesen.");
}
