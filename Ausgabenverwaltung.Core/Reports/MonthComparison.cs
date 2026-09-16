using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die Einordnung einer Monatszahl: der kurze Satz fuer die Kachel und
/// die ausfuehrliche Fassung als Kurzhinweis daran.
/// </summary>
public sealed record MonthStanding(string Text, string Hinweis);

/// <summary>
/// Beantwortet die Frage, die eine Monatszahl allein offen laesst: ist das
/// viel? Verglichen wird mit dem Schnitt der vorangegangenen Monate.
///
/// ZWEI Dinge machen den Vergleich erst ehrlich:
///
/// 1. Der laufende Monat ist noch nicht zu Ende. Die Vormonate muessen
///    deshalb BIS ZUM SELBEN TAG gezaehlt werden, sonst steht am Dritten
///    jedes Monats "80 % unter dem Schnitt" da, und das heisst nur, dass
///    der Monat jung ist. Diese Rechnung macht der Aufrufer; hier wird
///    vorausgesetzt, dass die Werte vergleichbar erhoben sind - und der
///    Satz sagt es mit ("bis heute").
/// 2. Monate vor der ersten Buchung sind keine Nullmonate, sondern gar
///    keine Monate: wer die Anwendung seit drei Monaten benutzt, hat
///    keinen Jahresschnitt. Fuehrende Nullen fallen deshalb weg, und
///    genannt wird nur, worueber wirklich gerechnet wurde.
///
/// Reine Arithmetik und Textbildung, damit vollstaendig pruefbar
/// (Regel 7).
/// </summary>
public static class MonthComparison
{
    /// <summary>
    /// Wie weit der Monat vom Schnitt abweichen darf, um noch als "wie
    /// immer" zu gelten. Darunter waere die Zahl eine Genauigkeit, die
    /// die Sache nicht hergibt: drei Prozent Unterschied sind ein
    /// Einkauf, der zufaellig auf die andere Seite des Monatsersten
    /// gefallen ist.
    /// </summary>
    public const double Toleranz = 0.05;

    /// <summary>
    /// Wieviele Vormonate mindestens vorliegen muessen. Ein einzelner
    /// Vormonat ist kein Schnitt, sondern der Vormonat - und ein Vergleich
    /// mit ihm allein schwankt so stark, dass er nichts einordnet.
    /// </summary>
    public const int MindestMonate = 2;

    /// <param name="currentCents">Der laufende Monat bis heute.</param>
    /// <param name="previousCents">
    /// Die Vormonate in zeitlicher Reihenfolge, jeder bis zum selben Tag
    /// gezaehlt wie <paramref name="currentCents"/>.
    /// </param>
    /// <param name="monatLaeuft">
    /// Ob der laufende Monat noch nicht zu Ende ist. Dann traegt der Satz
    /// "bis heute" - ohne diesen Zusatz behauptete er einen Vergleich
    /// ganzer Monate.
    /// </param>
    /// <returns>
    /// NULL, wenn sich nichts sagen laesst: zu wenige Vormonate oder ein
    /// Schnitt von null. Die Kachel laesst die Zeile dann weg, statt eine
    /// leere Aussage zu treffen.
    /// </returns>
    public static MonthStanding? Describe(
        long currentCents, IReadOnlyList<long> previousCents, bool monatLaeuft)
    {
        // Ist in diesem Monat noch gar nichts gebucht, sagt die Kachel das
        // schon in ihrer Zusatzzeile ("Noch keine Ausgabe diesen Monat").
        // "100 % unter dem Schnitt" darunter waere dieselbe Nachricht in
        // Prozent - zwei Zeilen fuer eine Aussage.
        if (currentCents == 0)
        {
            return null;
        }

        var gezaehlt = OhneFuehrendeNullen(previousCents);
        if (gezaehlt.Count < MindestMonate)
        {
            return null;
        }

        var schnitt = gezaehlt.Sum() / gezaehlt.Count;
        if (schnitt <= 0)
        {
            return null;
        }

        var bezug = $"Schnitt der letzten {gezaehlt.Count} Monate";
        var anfang = monatLaeuft ? "Bis heute " : string.Empty;
        var abweichung = (currentCents - (double)schnitt) / schnitt;

        var text = Math.Abs(abweichung) < Toleranz
            ? $"{anfang}wie im {bezug}"
            : $"{anfang}{Prozent(abweichung)} % {(abweichung > 0 ? "über" : "unter")} dem {bezug}";

        // Der Kurzhinweis nennt die Zahl, aus der die Prozente kommen. Ohne
        // sie bleibt offen, ob "20 % mehr" zwanzig Euro sind oder zweihundert.
        var hinweis = monatLaeuft
            ? $"{bezug}, bis zum selben Tag gezählt: {EuroText.Format(schnitt)}"
            : $"{bezug}: {EuroText.Format(schnitt)}";

        return new MonthStanding(text, hinweis);
    }

    // Gerundet auf ganze Prozent: die Nachkommastelle taeuscht eine
    // Genauigkeit vor, die ein Monatsvergleich nicht hat.
    private static int Prozent(double abweichung)
        => (int)Math.Round(Math.Abs(abweichung) * 100, MidpointRounding.AwayFromZero);

    // Monate vor der ersten Buchung zaehlen nicht mit - siehe oben. Eine
    // Null MITTEN in der Reihe bleibt dagegen stehen: das ist ein Monat,
    // in dem tatsaechlich nichts gebucht wurde, und er gehoert in den
    // Schnitt.
    private static IReadOnlyList<long> OhneFuehrendeNullen(IReadOnlyList<long> werte)
    {
        var erster = 0;
        while (erster < werte.Count && werte[erster] == 0)
        {
            erster++;
        }

        return werte.Skip(erster).ToList();
    }
}
