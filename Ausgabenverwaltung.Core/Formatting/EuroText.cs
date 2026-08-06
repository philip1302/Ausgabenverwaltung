using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Die einzige Stelle, an der ein Geldbetrag zu Anzeigetext wird. Keine
/// Ansicht und kein ViewModel formatiert selbst - sonst weicht frueher
/// oder spaeter eine Tabelle von den anderen ab.
///
/// Die Kultur steht fest auf de-DE und kommt bewusst NICHT aus der
/// Systemeinstellung: die Anwendung ist durchgehend deutsch beschriftet,
/// und auf einem englisch eingestellten Rechner stuende sonst
/// "4,200.00 €" zwischen deutschen Beschriftungen. Denselben Weg geht
/// bereits <see cref="Money.TryParseEuroText"/> beim Einlesen.
///
/// Zwischen Zahl und Zeichen steht ein GESCHUETZTES Leerzeichen (U+00A0),
/// damit der Zeilenumbruch den Betrag nie von seiner Waehrung trennt.
/// </summary>
public static class EuroText
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    // Als Zahlenwert und nicht als Zeichen geschrieben: ein geschuetztes
    // Leerzeichen im Quelltext waere von einem gewoehnlichen nicht zu
    // unterscheiden.
    private const char NonBreakingSpace = (char)0x00A0;

    public const string Symbol = "€";

    /// <summary>
    /// Die Anzeigeform ueberall dort, wo ein Betrag gelesen wird:
    /// Punkt als Tausender-, Komma als Dezimaltrennzeichen, Zeichen
    /// hinten - "4.200,00 €", negativ "-120,00 €".
    ///
    /// <paramref name="isIncome"/> stellt einem positiven Betrag ein "+"
    /// voran ("+120,00 €") - das eigentliche, auch ohne Farbwahrnehmung
    /// lesbare Kennzeichen einer Einnahme (Regel 10). Die gruene
    /// Hervorhebung in der Oberflaeche (Classes.einnahme) ist nur der
    /// Zusatz dazu, genau wie das Minuszeichen bei einer Erstattung durch
    /// die rote Hervorhebung ergaenzt wird. Default false, damit
    /// bestehende Aufrufstellen unveraendert bleiben.
    /// </summary>
    public static string Format(long cents, bool isIncome = false)
    {
        var vorzeichen = isIncome && cents > 0 ? "+" : string.Empty;
        return vorzeichen + Money.ToDecimal(cents).ToString("N2", DeDe) + NonBreakingSpace + Symbol;
    }

    /// <summary>
    /// Die Anzeigeform fuer eine EINZELNE Buchung mit bekanntem Typ:
    /// Vorzeichen kommt ausschliesslich von <paramref name="isIncome"/>
    /// ("+" Einnahme, "-" Ausgabe), nie vom gespeicherten Wert selbst -
    /// deshalb <c>Math.Abs</c>. Das faengt auch Alt-Datensaetze ab, die
    /// noch aus der Zeit vor dieser Regel einen negativen Betrag tragen
    /// (frueher: Erstattung) - sie werden einfach nach ihrem heutigen Typ
    /// formatiert, ohne Sonderbehandlung.
    ///
    /// Nicht zu verwechseln mit der zweistelligen Ueberladung von
    /// <see cref="Format"/>: die bleibt bewusst unveraendert und dient nur
    /// noch der (unveraenderten) Offene-Posten-Liste, wo Ausgaben ihr
    /// gespeichertes Vorzeichen behalten und nur Einnahmen ein "+"
    /// bekommen.
    /// </summary>
    public static string FormatSigned(long cents, bool isIncome)
    {
        var vorzeichen = isIncome ? "+" : "-";
        return vorzeichen + Money.ToDecimal(Math.Abs(cents)).ToString("N2", DeDe) + NonBreakingSpace + Symbol;
    }

    /// <summary>
    /// Die reine Zahl ohne Zeichen und OHNE Tausendertrennzeichen, fuer
    /// die beiden Stellen, an denen ein €-Zeichen schaden wuerde:
    ///
    /// - Eingabefelder. Dort tippt man die blanke Zahl; das Zeichen steht
    ///   als Beschriftung daneben. Der Tausenderpunkt muss weg, weil
    ///   <see cref="Money.TryParseEuroText"/> ihn bewusst ablehnt - ein
    ///   vorbelegtes "1.234,56" liesse sich sonst nicht wieder speichern.
    /// - CSV-Export. Ein €-Zeichen machte die Spalte in Excel zu Text,
    ///   und der Tausenderpunkt kann je nach Einstellung als Trennzeichen
    ///   missverstanden werden.
    /// </summary>
    public static string Plain(long cents) =>
        Money.ToDecimal(cents).ToString("0.00", DeDe);

    /// <summary>
    /// Die verkuerzte Form fuer eine Achsenbeschriftung im Diagramm:
    /// "1.250 €" statt "1.250,00 €", ab tausend "2,5 Tsd. €", ab einer
    /// Million "1,2 Mio. €".
    ///
    /// Grund fuer die Verkuerzung ist der Platz: an einer Wertachse
    /// stehen fuenf bis sieben Beschriftungen untereinander, und
    /// "1.250,00 €" ist dort fast doppelt so breit wie noetig. Die
    /// Nachkommastellen sagen an dieser Stelle ohnehin nichts - wer den
    /// genauen Betrag will, zeigt auf den Balken.
    ///
    /// Steht hier und nicht im Diagramm, weil Betraege ausschliesslich
    /// ueber diese Klasse zu Text werden (Regel 1). Sonst haette die
    /// Anzeige ihre eigene Zahlenformatierung, und genau davon gibt es
    /// hier keine zweite.
    /// </summary>
    public static string Axis(long cents)
    {
        var euro = Money.ToDecimal(cents);
        var betrag = Math.Abs(euro);

        // Die Schwellen sind bewusst hoch angesetzt: erst ab 10.000 wird
        // gekuerzt, damit im ueblichen Bereich eines Haushaltsbuchs
        // (dreistellige bis vierstellige Betraege) die volle, sofort
        // lesbare Zahl stehen bleibt.
        // Auch vor der Einheit ein geschuetztes Leerzeichen: "2,5" und
        // "Tsd." duerfen so wenig auseinandergerissen werden wie Zahl und
        // Waehrungszeichen.
        var (wert, einheit) = betrag switch
        {
            >= 1_000_000m => (euro / 1_000_000m, NonBreakingSpace + "Mio."),
            >= 10_000m => (euro / 1_000m, NonBreakingSpace + "Tsd."),
            _ => (euro, string.Empty),
        };

        // Eine Nachkommastelle nur, wenn sie etwas beitraegt: "2,5 Tsd."
        // ist genauer als "3 Tsd.", aber "2,0 Tsd." ist nur laenger als
        // "2 Tsd.".
        var text = wert == Math.Truncate(wert)
            ? wert.ToString("N0", DeDe)
            : wert.ToString("N1", DeDe);

        return text + einheit + NonBreakingSpace + Symbol;
    }

    /// <summary>
    /// Fuer eine SUMME ueber mehrere Buchungen (gemischt aus Ausgaben und
    /// Einnahmen): ob das Ergebnis insgesamt negativ ist, also die
    /// Ausgaben ueberwiegen. Steht hier und nicht als "&lt; 0" in jeder
    /// Zeile, damit die Anzeige einen Namen fuer das hat, was sie
    /// farblich (rot, Classes.ausgabe) hervorhebt. Fuer eine einzelne
    /// Buchung mit bekanntem Typ gilt stattdessen deren IsIncome direkt -
    /// siehe FormatSigned.
    /// </summary>
    public static bool IsNegative(long cents) => cents < 0;

    /// <summary>
    /// Das Gegenstueck zu <see cref="IsNegative"/>: ob eine Summe
    /// insgesamt positiv ist, also die beglichenen Einnahmen die
    /// Ausgaben eines Zeitabschnitts uebersteigen. Hervorgehoben in Gruen
    /// (Classes.einnahme).
    /// </summary>
    public static bool IsPositive(long cents) => cents > 0;
}
