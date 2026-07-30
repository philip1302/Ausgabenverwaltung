namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Faktoren und Beschriftungen der Schriftgroessen-Stufen und die
/// Umrechnung zwischen beidem.
///
/// Gespeichert wird der FAKTOR und nicht der Name der Stufe: eine Zahl in
/// der Einstellungsdatei bleibt verstaendlich, auch wenn spaeter eine
/// Stufe dazukommt oder umbenannt wird. Beim Laden wird auf die naechste
/// bekannte Stufe gerundet (<see cref="Normalize"/>), damit ein von Hand
/// eingetragener oder beschaedigter Wert nie zu einer Schriftgroesse
/// fuehrt, die niemand mehr zurueckstellen kann.
/// </summary>
public static class FontScales
{
    /// <summary>Faktor der Stufe <see cref="FontScaleStep.Normal"/>.</summary>
    public const double DefaultFactor = 1.0;

    // Die Staffelung reicht bis zum Doppelten. Die Abstaende wachsen
    // dabei gleichmaessig (jede Stufe ist rund ein Viertel bis zwei
    // Fuenftel groesser als die vorige) - eine gleichmaessige Staffelung
    // waere bei 200 % am Ende entweder in winzigen Schritten unterwegs
    // oder haette einen Sprung ganz am Schluss.
    private static readonly (FontScaleStep Step, double Factor, string Label)[] Table =
    [
        (FontScaleStep.Small,      0.8, "Klein"),
        (FontScaleStep.Normal,     1.0, "Normal"),
        (FontScaleStep.Large,      1.4, "Groß"),
        (FontScaleStep.ExtraLarge, 2.0, "Sehr groß"),
    ];

    public static IReadOnlyList<FontScaleStep> Steps =>
        Table.Select(entry => entry.Step).ToList();

    public static double Factor(FontScaleStep step) =>
        Table.First(entry => entry.Step == step).Factor;

    /// <summary>Beschriftung fuer die Auswahl in der Oberflaeche.</summary>
    public static string Label(FontScaleStep step) =>
        Table.First(entry => entry.Step == step).Label;

    /// <summary>
    /// Die Stufe, deren Faktor dem uebergebenen am naechsten liegt. Werte
    /// ausserhalb des Bereichs landen bei der kleinsten bzw. groessten
    /// Stufe, unbrauchbare Werte (NaN, unendlich) bei
    /// <see cref="FontScaleStep.Normal"/>.
    /// </summary>
    public static FontScaleStep FromFactor(double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor))
        {
            return FontScaleStep.Normal;
        }

        var nearest = Table[0];
        foreach (var entry in Table)
        {
            if (Math.Abs(entry.Factor - factor) < Math.Abs(nearest.Factor - factor))
            {
                nearest = entry;
            }
        }

        return nearest.Step;
    }

    /// <summary>
    /// Bringt einen gespeicherten Faktor auf den exakten Wert der
    /// naechsten Stufe.
    /// </summary>
    public static double Normalize(double factor) => Factor(FromFactor(factor));
}
