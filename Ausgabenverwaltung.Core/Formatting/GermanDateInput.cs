using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Umwandlung zwischen <see cref="DateOnly"/> und dem vom Anwender
/// getippten deutschen Datumsformat ('TT.MM.JJJJ'), z. B. in der
/// Erfassungsmaske. Bewusst getrennt von <see cref="IsoDate"/>, das
/// ausschliesslich das DB-Speicherformat ('YYYY-MM-DD', Regel 3) behandelt
/// - dieses Format ist reine Anzeige-/Eingabesache und darf nie in die
/// Datenbank gelangen.
///
/// Zusaetzlich zum vollstaendigen Datum versteht die Ueberladung mit
/// Bezugstag ein paar Kurzformen ("heute", "gestern", "-3", "15."). Sie
/// sparen bei jeder Erfassung ein paar Tastenanschlaege; damit sich
/// niemand vertut, schreibt <see cref="Normalize"/> das erkannte Datum
/// beim Verlassen des Feldes in voller Form zurueck.
/// </summary>
public static class GermanDateInput
{
    // Ein- und zweistellige Tages-/Monatsangaben werden beide akzeptiert,
    // damit schnelles Tippen ohne fuehrende Nullen nicht blockiert.
    private static readonly string[] Formats = ["dd.MM.yyyy", "d.M.yyyy"];

    public static string ToText(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Nur das vollstaendige Datum 'TT.MM.JJJJ'. Diese Form gilt ueberall
    /// dort, wo es keinen sinnvollen Bezugstag gibt - etwa im Von/Bis
    /// eines Zeitraums, der auch weit in der Vergangenheit liegen darf.
    /// </summary>
    public static bool TryParse(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>
    /// Das vollstaendige Datum ODER eine Kurzform, gerechnet vom
    /// <paramref name="today"/> aus:
    ///
    /// - "heute", "gestern", "vorgestern"
    /// - "-3" fuer "vor drei Tagen"
    /// - "15." fuer den 15. des laufenden Monats; liegt der in der
    ///   Zukunft, ist der 15. des Vormonats gemeint. Beim Nacherfassen
    ///   eines Belegs ist fast immer der letzte gemeinte Tag der richtige,
    ///   und ein Datum in der Zukunft waere hier so gut wie nie gewollt.
    ///
    /// Das vollstaendige Datum hat Vorrang: was sich als 'TT.MM.JJJJ'
    /// lesen laesst, wird nie als Kurzform gedeutet.
    ///
    /// Der Bezugstag kommt von aussen herein und wird nicht selbst
    /// ermittelt, damit sich die Grenzfaelle pruefen lassen, ohne die
    /// Systemuhr zu stellen (wie in <see cref="Expenses.ExpenseInput"/>).
    /// </summary>
    public static bool TryParse(string text, DateOnly today, out DateOnly date)
    {
        var eingabe = text?.Trim() ?? string.Empty;

        return TryParse(eingabe, out date) || TryParseKurzform(eingabe, today, out date);
    }

    /// <summary>
    /// Das erkannte Datum in voller Schreibweise ("heute" wird zu
    /// "07.08.2026"), zum Zurueckschreiben ins Feld, sobald es den Fokus
    /// verliert. NULL bedeutet "nichts zurueckzuschreiben" - dann bleibt
    /// stehen, was getippt wurde, und die Pruefung beim Speichern erklaert
    /// warum.
    /// </summary>
    public static string? Normalize(string? text, DateOnly today) =>
        text is not null && TryParse(text, today, out var date) ? ToText(date) : null;

    private static bool TryParseKurzform(string eingabe, DateOnly today, out DateOnly date)
    {
        date = default;

        if (eingabe.Length == 0)
        {
            return false;
        }

        switch (eingabe.ToLowerInvariant())
        {
            case "heute":
                date = today;
                return true;
            case "gestern":
                date = today.AddDays(-1);
                return true;
            case "vorgestern":
                date = today.AddDays(-2);
                return true;
        }

        // "-3" = vor drei Tagen. NumberStyles.None laesst weder ein
        // weiteres Vorzeichen noch Leerzeichen durch, das Minus steht
        // bereits davor.
        if (eingabe[0] == '-' &&
            int.TryParse(eingabe[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var tage))
        {
            // Ueber DayNumber statt AddDays: eine unsinnig grosse Zahl
            // liefert damit false, statt eine Ausnahme zu werfen.
            var tagesnummer = today.DayNumber - tage;
            if (tagesnummer < DateOnly.MinValue.DayNumber)
            {
                return false;
            }

            date = DateOnly.FromDayNumber(tagesnummer);
            return true;
        }

        if (eingabe[^1] == '.' &&
            int.TryParse(eingabe[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var tag))
        {
            return TryMonatstag(tag, today, out date);
        }

        return false;
    }

    /// <summary>
    /// Der genannte Tag im laufenden Monat, sofern er nicht in der Zukunft
    /// liegt - sonst derselbe Tag im Vormonat. Gibt es den Tag im
    /// gewaehlten Monat nicht (der 30. im Februar), schlaegt die Kurzform
    /// fehl: ein stillschweigend verschobenes Datum waere schlimmer als
    /// eine Meldung.
    /// </summary>
    private static bool TryMonatstag(int tag, DateOnly today, out DateOnly date)
    {
        date = default;

        if (tag < 1 || tag > 31)
        {
            return false;
        }

        if (tag <= DateTime.DaysInMonth(today.Year, today.Month))
        {
            var imMonat = new DateOnly(today.Year, today.Month, tag);
            if (imMonat <= today)
            {
                date = imMonat;
                return true;
            }
        }

        var vormonat = today.AddMonths(-1);
        if (tag > DateTime.DaysInMonth(vormonat.Year, vormonat.Month))
        {
            return false;
        }

        date = new DateOnly(vormonat.Year, vormonat.Month, tag);
        return true;
    }
}
