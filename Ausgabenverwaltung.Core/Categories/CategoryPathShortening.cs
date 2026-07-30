namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Kuerzt einen vollen Kategoriepfad ("Pferde › Gesundheit › Medizin ›
/// Impfungen") so weit, dass er in eine vorgegebene Spaltenbreite passt.
///
/// Gekuerzt wird ausschliesslich VORNE: der Name der Kategorie selbst ist
/// die Angabe, die zaehlt, und muss vollstaendig lesbar bleiben. Die
/// uebergeordneten Ebenen fallen von der Wurzel her weg, so viele wie
/// noetig, und die Luecke wird durch Auslassungspunkte angedeutet
/// ("… › Medizin › Impfungen").
///
/// Ob ein Vorschlag passt, entscheidet der Aufrufer: nur er kennt Schrift
/// und Spaltenbreite. Dadurch bleibt die Auswahl der Ebenen hier pruefbar,
/// ohne dass Core etwas ueber die Oberflaeche wissen muss (Regel 7).
/// </summary>
public static class CategoryPathShortening
{
    /// <summary>Das Zeichen fuer "hier fehlt etwas".</summary>
    public const string Ellipsis = "…";

    /// <summary>
    /// Das voranstehende "… › ", das eine Kuerzung anzeigt. Es kostet
    /// selbst Platz und ist deshalb Teil jedes geprueften Vorschlags.
    /// </summary>
    public static string Prefix => Ellipsis + CategoryPaths.Separator;

    /// <summary>
    /// Liefert den anzuzeigenden Text. Ist er mit
    /// <paramref name="fullPath"/> identisch, wurde nichts weggelassen -
    /// dann braucht die Zelle auch keinen Hilfetext.
    /// </summary>
    /// <param name="fullPath">Der volle Pfad ab der Wurzel.</param>
    /// <param name="fits">
    /// Prueft, ob ein Vorschlag in die Spalte passt.
    /// </param>
    public static string Shorten(string fullPath, Func<string, bool> fits)
    {
        if (string.IsNullOrEmpty(fullPath) || fits(fullPath))
        {
            return fullPath;
        }

        var segments = fullPath.Split(CategoryPaths.Separator);

        // Eine Kategorie ohne Oberkategorie laesst sich vorne nicht
        // kuerzen - hier bleibt nur, den Namen selbst hinten abzuschneiden,
        // und das macht die Zelle (TextTrimming).
        if (segments.Length == 1)
        {
            return fullPath;
        }

        // Von wenig nach viel weglassen: der erste Vorschlag, der passt,
        // ist der mit den meisten noch sichtbaren Ebenen.
        var vorschlag = fullPath;
        for (var weggelassen = 1; weggelassen < segments.Length; weggelassen++)
        {
            vorschlag = Prefix + string.Join(CategoryPaths.Separator, segments[weggelassen..]);

            if (fits(vorschlag))
            {
                return vorschlag;
            }
        }

        // Selbst die Kategorie allein passt nicht mehr. Der letzte
        // Vorschlag geht trotzdem zurueck: die Zelle schneidet ihn dann
        // hinten ab. Ein Fall fuer sehr schmale Spalten - die kleinste
        // einstellbare Breite ist bewusst so gewaehlt, dass er selten wird.
        return vorschlag;
    }
}
