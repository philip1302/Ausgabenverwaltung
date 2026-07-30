using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Rechnet eine in der Ansicht angegebene Breite (Wert bei Stufe
/// "Normal") mit dem aktuellen Skalierungsfaktor um. Wird nicht direkt
/// in XAML geschrieben, sondern von <see cref="BreiteExtension"/>
/// verwendet.
/// </summary>
public sealed class SkalierteBreite : IValueConverter
{
    public static SkalierteBreite Instanz { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var faktor = value as double? ?? 1.0;
        var basis = parameter as double? ?? 0.0;

        // Auf ganze Pixel: halbe Pixel bringen bei Breiten nichts und
        // fuehren nur dazu, dass Kopfzeile und Zeile unterschiedlich
        // gerundet werden.
        return Math.Round(basis * faktor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Breiten werden nur gelesen.");
}
