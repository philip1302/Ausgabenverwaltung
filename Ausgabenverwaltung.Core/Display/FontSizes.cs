namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Rechnet einen Skalierungsfaktor in die konkreten Schriftgroessen der
/// Oberflaeche um. Die Ausgangswerte entsprechen der Stufe "Normal" und
/// damit dem, was die Anwendung vor der Einstellung fest eingetragen
/// hatte.
/// </summary>
public static class FontSizes
{
    public const double SmallBase = 11;
    public const double NormalBase = 14;
    public const double HeadingBase = 16;
    public const double PageTitleBase = 26;
    public const double KpiBase = 30;

    public static FontSizeSet For(double factor) => new(
        Scale(SmallBase, factor),
        Scale(NormalBase, factor),
        Scale(HeadingBase, factor),
        Scale(PageTitleBase, factor),
        Scale(KpiBase, factor));

    // Auf eine Nachkommastelle gerundet: 0,875 * 11 waere sonst
    // 9,625 - eine Genauigkeit, die kein Bildschirm darstellt, die aber
    // in jedem Vergleich und in jeder Testmeldung mitgeschleppt wuerde.
    // AwayFromZero statt der kaufmaennischen Vorgabe, damit 12,25 zu
    // 12,3 wird und nicht zu 12,2 - eine halbe Stelle nach oben ist bei
    // Schriftgroessen die harmlosere Richtung.
    private static double Scale(double baseSize, double factor) =>
        Math.Round(baseSize * factor, 1, MidpointRounding.AwayFromZero);
}
