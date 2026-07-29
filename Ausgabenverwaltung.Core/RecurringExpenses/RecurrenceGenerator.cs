namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Reine Datumsberechnung fuer wiederkehrende Buchungen, ohne jeden
/// Datenbankzugriff (deshalb ohne Datenbank testbar). Vorkommen werden
/// gemaess Regel 5 immer als StartDate + n * Intervall berechnet, NIE
/// als letztes Vorkommen + Intervall - sonst wuerde ein Ankertag, der
/// nicht in jedem Monat existiert (z. B. AnchorDay 31), zu einer
/// schleichenden Drift des Faelligkeitstags fuehren.
/// </summary>
public static class RecurrenceGenerator
{
    /// <summary>
    /// Liefert alle noch nicht erzeugten Vorkommen bis einschliesslich
    /// <paramref name="through"/>. "Noch nicht erzeugt" heisst: das
    /// Vorkommen liegt nach <paramref name="generatedThrough"/> (Regel:
    /// GeneratedThrough verhindert sowohl Doppelerzeugung als auch das
    /// Wiederauferstehen geloeschter Buchungen - der Aufrufer muss
    /// GeneratedThrough nach dem Erzeugen entsprechend fortschreiben).
    /// </summary>
    public static IReadOnlyList<DateOnly> GetDueOccurrences(
        DateOnly startDate,
        DateOnly? endDate,
        string intervalUnit,
        int intervalCount,
        int? anchorDay,
        DateOnly? generatedThrough,
        DateOnly through)
    {
        if (intervalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalCount), intervalCount, "IntervalCount muss positiv sein.");
        }

        var occurrences = new List<DateOnly>();

        // n laeuft von 0 an hoch; da jedes Vorkommen unabhaengig von den
        // anderen direkt aus StartDate berechnet wird, ist die Reihenfolge
        // garantiert streng monoton steigend und die Schleife terminiert,
        // sobald through oder endDate ueberschritten ist.
        for (var n = 0; ; n++)
        {
            var occurrence = ComputeOccurrence(startDate, intervalUnit, intervalCount, anchorDay, n);

            if (occurrence > through || (endDate is DateOnly end && occurrence > end))
            {
                break;
            }

            if (generatedThrough is null || occurrence > generatedThrough)
            {
                occurrences.Add(occurrence);
            }
        }

        return occurrences;
    }

    private static DateOnly ComputeOccurrence(
        DateOnly startDate, string intervalUnit, int intervalCount, int? anchorDay, int n)
    {
        return intervalUnit switch
        {
            "day" => startDate.AddDays(n * intervalCount),
            "week" => startDate.AddDays(n * intervalCount * 7),
            "month" => AddCalendarMonths(startDate, intervalCount * n, anchorDay),
            "year" => AddCalendarYears(startDate, intervalCount * n, anchorDay),
            _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unbekannte IntervalUnit."),
        };
    }

    // Monatsarithmetik ueber einen durchlaufenden Monatsindex, damit
    // "StartDate + n * Intervall" auch ueber Jahreswechsel hinweg direkt
    // aus StartDate berechnet wird statt schrittweise vom letzten
    // Vorkommen aus.
    private static DateOnly AddCalendarMonths(DateOnly startDate, int monthsToAdd, int? anchorDay)
    {
        var totalMonths = startDate.Year * 12 + (startDate.Month - 1) + monthsToAdd;
        var year = totalMonths / 12;
        var month = totalMonths % 12 + 1;
        var day = ClampDay(anchorDay ?? startDate.Day, year, month);
        return new DateOnly(year, month, day);
    }

    private static DateOnly AddCalendarYears(DateOnly startDate, int yearsToAdd, int? anchorDay)
    {
        var year = startDate.Year + yearsToAdd;
        var month = startDate.Month;
        var day = ClampDay(anchorDay ?? startDate.Day, year, month);
        return new DateOnly(year, month, day);
    }

    // Existiert der Ankertag im Zielmonat nicht (z. B. 31. April, 29.
    // Februar in einem Nicht-Schaltjahr), wird auf den letzten Tag des
    // Monats gekuerzt (Regel 5).
    private static int ClampDay(int day, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return Math.Min(day, daysInMonth);
    }
}
