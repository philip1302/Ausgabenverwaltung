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
    /// </summary>
    public static string Format(long cents) =>
        Money.ToDecimal(cents).ToString("N2", DeDe) + NonBreakingSpace + Symbol;

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
    /// Ob der Betrag eine Erstattung ist. Steht hier und nicht als
    /// "&lt; 0" in jeder Zeile, damit die Anzeige einen Namen fuer das hat,
    /// was sie farblich hervorhebt.
    /// </summary>
    public static bool IsNegative(long cents) => cents < 0;
}
