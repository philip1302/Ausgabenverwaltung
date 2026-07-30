namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Grenzen der von Hand einstellbaren Spaltenbreiten.
///
/// Die Werte gelten - wie alle Breitenangaben der Oberflaeche - fuer die
/// Schriftstufe "Normal" und werden erst beim Anzeigen mit dem aktuellen
/// Faktor multipliziert (siehe Anzeige/Skalierung). Gespeichert wird
/// deshalb die unskalierte Breite: sonst haette ein Wechsel der
/// Schriftgroesse die gemerkte Spaltenbreite mitverschoben.
///
/// <see cref="NormalizeCategory"/> laeuft ueber jeden gelesenen und jeden
/// gezogenen Wert. Damit kann weder eine von Hand veraenderte
/// Einstellungsdatei noch ein weit ueber den Fensterrand hinausgezogener
/// Griff eine Spalte hinterlassen, die niemand mehr zurueckholt.
/// </summary>
public static class ColumnWidths
{
    /// <summary>Breite der Kategoriespalte, solange nichts gezogen wurde.</summary>
    public const double CategoryDefault = 260;

    /// <summary>
    /// Schmalste einstellbare Kategoriespalte. Breit genug, dass neben den
    /// Auslassungspunkten noch ein ueblicher Kategoriename steht.
    /// </summary>
    public const double CategoryMin = 120;

    /// <summary>
    /// Breiteste einstellbare Kategoriespalte. Daraus ergibt sich zugleich
    /// die groesste Mindestbreite der Tabelle - darueber hinaus bliebe von
    /// den Spalten dahinter nichts mehr uebrig.
    /// </summary>
    public const double CategoryMax = 520;

    /// <summary>
    /// Bringt eine gelesene oder gezogene Breite in den erlaubten Bereich
    /// und auf ganze Pixel. Unbrauchbare Werte (NaN, unendlich, 0 aus einer
    /// aelteren Einstellungsdatei) fallen auf die Vorgabe zurueck.
    /// </summary>
    public static double NormalizeCategory(double width)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            return CategoryDefault;
        }

        return Math.Round(Math.Clamp(width, CategoryMin, CategoryMax));
    }
}
