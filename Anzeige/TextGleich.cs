using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Vergleicht den gebundenen Wert mit dem ConverterParameter als Text -
/// fuer den sichtbaren aktiven Zustand einer Schnellwahl-Schaltfläche
/// (UI/UX-Redesign, Abschnitt 5.3/5.4: "Schnellwahl-Buttons als echte
/// Button-Gruppe mit sichtbarem aktivem Zustand"), z. B.
/// <c>Classes.aktiv="{Binding AktiverZeitraumSchluessel,
/// Converter={x:Static anzeige:TextGleich.Instanz}, ConverterParameter=DiesesJahr}"</c>.
/// </summary>
public sealed class TextGleich : IValueConverter
{
    public static TextGleich Instanz { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Nur fuer den Vergleich, nicht als Zwei-Wege-Bindung gedacht.");
}
