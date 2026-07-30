using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Schreibweise fuer die Mindestbreite einer Tabelle mit einstellbarer
/// Kategoriespalte: <c>MinWidth="{anzeige:BreitePlusKategorie 980}"</c>.
///
/// Die Zahl ist alles AUSSER der Kategoriespalte - die Summe der uebrigen
/// festen Spalten plus ein Rest fuer die Sternspalte, in Pixeln bei Stufe
/// "Normal". Dazu kommt die aktuell eingestellte Kategoriebreite.
///
/// Eine feste Zahl reicht hier nicht: wird die Kategoriespalte breiter
/// gezogen, muss die Tabelle im waagerechten Bildlauf entsprechend
/// frueher zu schieben beginnen. Sonst holte sich die breiter gezogene
/// Kategorie den Platz aus der Sternspalte daneben, bis von der Bemerkung
/// nichts mehr uebrig ist.
/// </summary>
public sealed class BreitePlusKategorieExtension
{
    public BreitePlusKategorieExtension()
    {
    }

    public BreitePlusKategorieExtension(double wert)
    {
        Wert = wert;
    }

    /// <summary>Die Breite ohne die Kategoriespalte, bei Stufe "Normal".</summary>
    public double Wert { get; set; }

    public BindingBase ProvideValue(IServiceProvider serviceProvider) => new Binding
    {
        // Ueber die bereits skalierte Breite: sie meldet sich sowohl beim
        // Ziehen als auch beim Wechsel der Schriftgroesse.
        Source = Spaltenbreiten.Aktuell,
        Path = nameof(Spaltenbreiten.KategorieSkaliert),
        Converter = GesamtbreiteRechner.Instanz,
        ConverterParameter = Wert,
    };
}

/// <summary>
/// Zaehlt zur skalierten Kategoriebreite den ebenfalls skalierten Rest der
/// Tabelle hinzu. Wird nicht direkt in XAML geschrieben, sondern von
/// <see cref="BreitePlusKategorieExtension"/> verwendet.
/// </summary>
public sealed class GesamtbreiteRechner : IValueConverter
{
    public static GesamtbreiteRechner Instanz { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kategorie = value as double? ?? 0.0;
        var uebrige = parameter as double? ?? 0.0;

        return kategorie + Math.Round(uebrige * Skalierung.Aktuell.Faktor);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Breiten werden nur gelesen.");
}
