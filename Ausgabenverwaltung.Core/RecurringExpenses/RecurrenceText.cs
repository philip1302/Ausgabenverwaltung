namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Der Rhythmus einer Vorlage in lesbarer Form ("monatlich am 15.",
/// "quartalsweise am 1.", "jaehrlich am 1.3.") fuer die Verwaltungsliste.
/// Steht in Core und nicht im ViewModel, weil die Regeln pruefbar sind
/// (Regel 7).
///
/// Zwei Werte kommen nicht aus dem Rhythmus selbst, sondern aus dem
/// Startdatum - genau wie im <see cref="RecurrenceGenerator"/>:
/// der Faelligkeitstag, wenn kein AnchorDay gesetzt ist, und bei
/// Jahresrhythmus zusaetzlich der Monat.
/// </summary>
public static class RecurrenceText
{
    // Wochentage in der Mehrzahlform, wie man sie im Deutschen fuer
    // Wiederholungen benutzt ("wöchentlich montags").
    private static readonly string[] WochentagPlural =
    [
        "sonntags", "montags", "dienstags", "mittwochs",
        "donnerstags", "freitags", "samstags",
    ];

    public static string Describe(
        string intervalUnit, int intervalCount, int? anchorDay, DateOnly startDate)
    {
        if (intervalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalCount), intervalCount, "IntervalCount muss positiv sein.");
        }

        var tag = anchorDay ?? startDate.Day;

        return intervalUnit switch
        {
            "day" => intervalCount == 1
                ? "täglich"
                : $"alle {intervalCount} Tage",

            "week" => intervalCount == 1
                ? $"wöchentlich {Wochentag(startDate)}"
                : $"alle {intervalCount} Wochen {Wochentag(startDate)}",

            "month" => BeschreibeMonat(intervalCount, tag, startDate.Month),

            "year" => intervalCount == 1
                ? $"jährlich am {tag}.{startDate.Month}."
                : $"alle {intervalCount} Jahre am {tag}.{startDate.Month}.",

            _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unbekannte IntervalUnit."),
        };
    }

    // Die gebraeuchlichen Monatsvielfachen haben eigene Namen - "alle 3
    // Monate" waere zwar richtig, aber niemand sagt das ueber eine
    // Quartalszahlung. Zwoelf Monate sind ein Jahr und werden deshalb
    // auch so benannt, samt Monatsangabe.
    private static string BeschreibeMonat(int intervalCount, int tag, int startMonat) => intervalCount switch
    {
        1 => $"monatlich am {tag}.",
        3 => $"quartalsweise am {tag}.",
        6 => $"halbjährlich am {tag}.",
        12 => $"jährlich am {tag}.{startMonat}.",
        _ => $"alle {intervalCount} Monate am {tag}.",
    };

    private static string Wochentag(DateOnly startDate) =>
        WochentagPlural[(int)startDate.DayOfWeek];
}
