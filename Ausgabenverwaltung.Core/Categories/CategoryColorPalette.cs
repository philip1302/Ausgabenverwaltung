namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Die waehlbaren Kategoriefarben. Bewusst eine feste, kurze Palette und
/// kein freier Farbwaehler: frei gewaehlte Farben ergeben nach dem
/// dritten Mal Waehlen Paare, die nebeneinander nicht mehr
/// unterscheidbar sind, und Toene, auf denen Text nicht mehr lesbar ist.
///
/// Alle Werte sind so gewaehlt, dass sie
/// - nebeneinander unterscheidbar bleiben (14 Toene ueber den ganzen
///   Farbkreis, keine zwei benachbarten aus derselben Ecke),
/// - dunkel genug sind, um als Punkt oder schmaler Balken auf dem hellen
///   Hintergrund der Anwendung sichtbar zu sein,
/// - in der Saettigung zum uebrigen, zurueckhaltenden Erscheinungsbild
///   passen (keine Neontoene neben den grauen Tabellenlinien).
///
/// Die Farbe ist immer nur ein Zusatz zum Namen, nie sein Ersatz - siehe
/// <see cref="CategoryColors"/>.
/// </summary>
public static class CategoryColorPalette
{
    /// <summary>
    /// Farbe fuer Kategorien, die weder selbst noch ueber einen Vorfahren
    /// eine Farbe haben. Ein neutrales Grau: erkennbar als "keine gewaehlt"
    /// und trotzdem sichtbar, damit die Spalte nicht zwischen leer und
    /// gefaerbt springt.
    /// </summary>
    public const string DefaultHex = "#9E9E9E";

    public static IReadOnlyList<CategoryColor> Colors { get; } =
    [
        new("#C0392B", "Rot"),
        new("#E67E22", "Orange"),
        new("#C9A227", "Ocker"),
        new("#7F8C3A", "Oliv"),
        new("#2E9E5B", "Grün"),
        new("#159A8C", "Petrol"),
        new("#2980B9", "Blau"),
        new("#34495E", "Marine"),
        new("#5D5FC6", "Indigo"),
        new("#8E44AD", "Violett"),
        new("#B03A6E", "Beere"),
        new("#D96C8F", "Rosé"),
        new("#8D6E63", "Braun"),
        new("#607D8B", "Graublau"),
    ];

    /// <summary>
    /// Ob ein Wert zur Palette gehoert. Alles andere - von Hand in die
    /// Datenbank geschriebene Werte, Reste einer aelteren Palette - gilt
    /// als keine Farbe und erbt (siehe <see cref="CategoryColors.Resolve"/>).
    /// Damit kann kein Wert in die Anzeige gelangen, der dort nie
    /// geprueft wurde.
    /// </summary>
    public static bool IsKnown(string? hex) =>
        hex is not null &&
        Colors.Any(color => string.Equals(color.Hex, hex, StringComparison.OrdinalIgnoreCase));

    public static string? NameOf(string? hex) =>
        Colors.FirstOrDefault(
            color => string.Equals(color.Hex, hex, StringComparison.OrdinalIgnoreCase))?.Name;
}
