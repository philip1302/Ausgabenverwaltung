using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Wertet aus, was im Betragsfeld steht: eine einzelne Zahl wie bisher -
/// und zusaetzlich eine Kette aus Additionen und Subtraktionen,
/// "12,50+3,20" oder "40-2,50".
///
/// Bewusst NUR Plus und Minus: keine Klammern, keine Multiplikation, kein
/// Prozent. Der Anwendungsfall ist "drei Kassenzettel zusammenzaehlen",
/// nicht ein Taschenrechner im Formular. Alles, was darueber hinausgeht,
/// waere eine zweite Sprache, die niemand gelernt hat, und ihre Fehler
/// faenden sich still in den Betraegen wieder.
///
/// Gerechnet wird in <see cref="decimal"/>, umgewandelt wird
/// ausschliesslich ueber <see cref="Money"/> (Regel 1). Ungueltiges
/// liefert NULL und wirft nie - eine Eingabe, die noch im Entstehen ist
/// ("12,50+"), ist der Normalfall und kein Fehler des Programms.
///
/// Unterschied zu <see cref="Money.TryParseEuroText"/>, das bewusst
/// unveraendert bleibt: dort wird der Punkt abgelehnt, weil "12.50" sich
/// still in 1250 verwandeln koennte. Hier wird er als Dezimaltrennzeichen
/// gelesen - aber nur, wo er kein Tausendertrennzeichen sein KANN (siehe
/// <see cref="LiesZahl"/>), und das Ergebnis wird dem Anwender ueber
/// <see cref="Normalform"/> sofort in deutscher Schreibweise
/// zurueckgeschrieben. Er sieht damit, was verstanden wurde, statt es
/// raten zu muessen.
/// </summary>
public static class BetragsAusdruck
{
    // Der groesste Eurobetrag, der sich noch in long-Cent fassen laesst.
    // Steht hier nicht als fachliche Obergrenze, sondern damit die
    // Umwandlung bei einer absurd langen Ziffernfolge NULL liefert statt
    // zu werfen (Math.Round/Convert.ToInt64 wuerden ueberlaufen).
    private const decimal Hoechstbetrag = 92_233_720_368_547_758m;

    /// <summary>
    /// Der Wert des Ausdrucks in Cent, oder NULL, wenn sich daraus keine
    /// Zahl lesen laesst.
    ///
    /// Ein negatives Ergebnis wird hier NICHT abgelehnt: dass Betraege
    /// immer positiv sind, ist eine Fachregel und steht an genau einer
    /// Stelle, in <see cref="Expenses.ExpenseValidator"/>. Nur von dort
    /// kommt die Meldung, die den Grund nennt ("Der Betrag darf nicht
    /// negativ sein.") - hier waere daraus ein pauschales "kein gueltiger
    /// Betrag" geworden.
    /// </summary>
    public static long? Auswerten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Leerzeichen fallen ueberall weg: "12,50 + 3,20" ist dieselbe
        // Eingabe wie "12,50+3,20", und getippt wird mal so, mal so.
        var ausdruck = string.Concat(text.Where(zeichen => !char.IsWhiteSpace(zeichen)));

        var summe = 0m;
        var vorzeichen = 1;
        var stelle = 0;

        // Ein fuehrendes Vorzeichen gehoert zum ersten Summanden.
        if (ausdruck[0] is '+' or '-')
        {
            vorzeichen = ausdruck[0] == '-' ? -1 : 1;
            stelle = 1;
        }

        while (true)
        {
            var anfang = stelle;
            while (stelle < ausdruck.Length && ausdruck[stelle] is not ('+' or '-'))
            {
                stelle++;
            }

            if (LiesZahl(ausdruck[anfang..stelle]) is not decimal wert)
            {
                return null;
            }

            summe += vorzeichen * wert;

            if (Math.Abs(summe) > Hoechstbetrag)
            {
                return null;
            }

            if (stelle == ausdruck.Length)
            {
                return Money.ToCents(summe);
            }

            vorzeichen = ausdruck[stelle] == '-' ? -1 : 1;
            stelle++;
        }
    }

    /// <summary>
    /// Der ausgewertete Ausdruck in deutscher Schreibweise, so wie er
    /// gespeichert wuerde ("12,50+3,20" wird zu "15,70"). Gedacht fuer den
    /// Moment, in dem das Feld den Fokus verliert: der Anwender soll
    /// sehen, was verstanden wurde, bevor er speichert.
    ///
    /// NULL bedeutet "nichts zurueckzuschreiben" - dann bleibt stehen, was
    /// der Anwender getippt hat, und die Pruefung beim Speichern erklaert
    /// warum.
    /// </summary>
    public static string? Normalform(string? text) =>
        Auswerten(text) is long cents ? EuroText.Plain(cents) : null;

    /// <summary>
    /// Ein einzelner Summand. Erlaubt sind Ziffern mit hoechstens einem
    /// Trennzeichen, Komma oder Punkt.
    /// </summary>
    private static decimal? LiesZahl(string teil)
    {
        if (teil.Length == 0)
        {
            return null;
        }

        var trenner = -1;
        for (var stelle = 0; stelle < teil.Length; stelle++)
        {
            var zeichen = teil[stelle];

            if (char.IsAsciiDigit(zeichen))
            {
                continue;
            }

            // Ein zweites Trennzeichen ("12,50,00", "1.234,56") oder ein
            // fremdes Zeichen macht den Summanden ungueltig. "1.234,56"
            // wird damit weiterhin abgelehnt - genau wie bisher in
            // Money.TryParseEuroText.
            if (zeichen is '.' or ',' && trenner < 0)
            {
                trenner = stelle;
                continue;
            }

            return null;
        }

        // Vor und hinter dem Trennzeichen muss eine Ziffer stehen: ",5"
        // und "12," sind angefangene, keine fertigen Zahlen.
        if (trenner == 0 || trenner == teil.Length - 1)
        {
            return null;
        }

        // Genau drei Ziffern hinter einem PUNKT koennten eine
        // Tausendergruppe sein ("1.500" - 1500 € oder 1,50 €?). Diese eine
        // Form bleibt abgelehnt; alle anderen Punktstellungen sind
        // eindeutig, weil eine Tausendergruppe immer dreistellig ist.
        if (trenner >= 0 && teil[trenner] == '.' && teil.Length - trenner - 1 == 3)
        {
            return null;
        }

        var invariant = trenner < 0
            ? teil
            : string.Concat(teil[..trenner], ".", teil[(trenner + 1)..]);

        if (!decimal.TryParse(
                invariant, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var wert))
        {
            return null;
        }

        return Math.Abs(wert) > Hoechstbetrag ? null : wert;
    }
}
