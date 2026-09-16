using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Die eine Kultur der Anwendung. Alles, was ein Anwender zu lesen
/// bekommt - Betraege, Datumsangaben, Anzahlen, die Sortierfolge von
/// Namen - wird in de-DE gebildet, und zwar fest und nicht nach der
/// Einstellung des Betriebssystems (die Begruendung steht bei
/// <see cref="EuroText"/>).
///
/// Vorher stand <c>CultureInfo.GetCultureInfo("de-DE")</c> in elf
/// Dateien nebeneinander - in Core, in den ViewModels, einmal sogar als
/// lokale Variable mitten in einer Methode. Elf Stellen, die dasselbe
/// sagen, sind zehn Stellen, an denen es eines Tages nicht mehr
/// dasselbe sagt; wer die Kultur der Anwendung sucht, soll genau einen
/// Ort finden.
///
/// Die Formate selbst gehoeren NICHT hierher: fuer Betraege ist
/// <see cref="EuroText"/> zustaendig (Regel 1), fuer Datumsangaben in
/// der Datenbank <see cref="IsoDate"/> und <see cref="IsoDateTime"/>
/// (Regel 3). Hier steht nur die Kultur, mit der sie rechnen.
/// </summary>
public static class Kultur
{
    /// <summary>
    /// Deutsch (Deutschland): Komma als Dezimaltrennzeichen, Punkt als
    /// Tausendertrennzeichen, 'dd.MM.yyyy' als Datumsformat.
    /// </summary>
    public static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Vergleich von Namen so, wie der Anwender sie sortiert erwartet -
    /// Umlaute einsortiert statt hinter Z, Gross- und Kleinschreibung
    /// egal. Gedacht fuer Namen von Kategorien, Personen und
    /// gespeicherten Filtern.
    /// </summary>
    public static readonly StringComparer NamensVergleich =
        StringComparer.Create(DeDe, ignoreCase: true);

    /// <summary>
    /// Anzahl mit Tausendertrennzeichen ("1.234"). Eine eigene Methode,
    /// weil <c>ToString("N0", ...)</c> sonst in acht Dateien steht und
    /// dabei zweimal verrutscht ist: einmal als "N1", einmal ganz ohne
    /// Kultur.
    /// </summary>
    public static string Anzahl(long wert) => wert.ToString("N0", DeDe);

    /// <summary>
    /// Datum in deutscher Schreibweise ("31.01.2026") - fuer die
    /// ANZEIGE. In die Datenbank und in die Einstellungsdatei gehoert
    /// das ISO-Format, siehe <see cref="IsoDate"/>.
    /// </summary>
    public static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", DeDe);
}
