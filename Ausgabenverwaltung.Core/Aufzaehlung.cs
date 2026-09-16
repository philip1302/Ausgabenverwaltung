namespace Ausgabenverwaltung.Core;

/// <summary>
/// Aufzaehlungswerte aus Text lesen - aus der Einstellungsdatei, aus dem
/// Kommandoparameter einer Spaltenueberschrift, aus allem, was ausserhalb
/// des Programms steht und deshalb nicht stimmen muss.
///
/// Warum es diese Stelle gibt: <see cref="Enum.TryParse{T}(string, out T)"/>
/// ist fuer diesen Zweck zu nachgiebig, und zwar auf zwei Arten, die
/// beide erst weit spaeter auffallen.
///
/// 1. Es nimmt ZAHLEN an. "99" ergibt ein klagloses true mit dem Wert 99,
///    den es in der Aufzaehlung nicht gibt. So ein Wert laeuft durch das
///    ganze Programm, bis ihn jemand in einem switch nachschlaegt - in
///    der ORDER-BY-Weissliste von Expenses.ExpenseRepository etwa endet
///    das als ArgumentOutOfRangeException mitten im Laden der Liste.
/// 2. Es nimmt Zahlen auch dann an, wenn sie zufaellig treffen: "2" wird
///    stillschweigend zum dritten Wert der Aufzaehlung. Das ist kein
///    Absturz, aber es haengt an der REIHENFOLGE der Werte - wer sie
///    eines Tages umsortiert, aendert damit die Bedeutung alter
///    Einstellungsdateien, ohne es zu merken.
///
/// Hier gilt deshalb: gelesen werden NUR Namen, nie Zahlen. Damit ist
/// genau das lesbar, was <see cref="Enum.ToString()"/> beim Speichern
/// geschrieben hat, und nichts sonst.
/// </summary>
public static class Aufzaehlung
{
    /// <summary>
    /// Liefert den Aufzaehlungswert zum Namen, oder NULL, wenn der Text
    /// keiner ist (unbekannt, leer, eine Zahl, anders geschrieben). Was
    /// aus dem NULL wird - Vorgabewert oder ebenfalls NULL -, entscheidet
    /// der Aufrufer; hier wird nichts erfunden.
    /// </summary>
    public static T? NachName<T>(string? text) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text) || !IstName(text))
        {
            return null;
        }

        // Das IsDefined bleibt trotz der Zahlenpruefung stehen: es kostet
        // nichts und haelt auch den Fall ab, dass Enum.TryParse einmal
        // etwas anderes durchlaesst, als hier erwartet wird.
        return Enum.TryParse<T>(text, out var wert) && Enum.IsDefined(wert) ? wert : null;
    }

    // Ein Name einer Aufzaehlung faengt in C# nie mit einer Ziffer oder
    // einem Vorzeichen an - alles, was so beginnt, ist als Zahl gemeint
    // und wird hier nicht gelesen.
    //
    // Das Komma fliegt aus demselben Grund raus: Enum.TryParse liest auch
    // "Datum, Betrag" und verodert die beiden Werte. Fuer eine
    // Flags-Aufzaehlung waere das richtig - keine dieser Anwendung ist
    // eine, und das Ergebnis waere hier eine Spalte, die niemand
    // gespeichert hat.
    private static bool IstName(string text)
    {
        if (text.Contains(','))
        {
            return false;
        }

        var erstes = text[0];
        return char.IsLetter(erstes) || erstes == '_';
    }
}
