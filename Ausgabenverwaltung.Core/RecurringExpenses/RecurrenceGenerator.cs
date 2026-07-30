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
        var occurrences = new List<DateOnly>();

        foreach (var occurrence in EnumerateOccurrences(
                     startDate, intervalUnit, intervalCount, anchorDay, endDate))
        {
            if (occurrence > through)
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

    /// <summary>
    /// Die naechsten <paramref name="maxCount"/> Vorkommen ab
    /// <paramref name="from"/> (einschliesslich) - fuer die Vorschau im
    /// Vorlagenformular.
    ///
    /// Bewusst eine eigene Methode statt eines Aufrufs von
    /// <see cref="GetDueOccurrences"/>: dort ist GeneratedThrough eine
    /// AUSSCHLIESSENDE Grenze fuer bereits Erzeugtes. Die Vorschau will
    /// dagegen die tatsaechlichen Termine zeigen, unabhaengig davon, was
    /// schon gebucht wurde - den Parameter dafuer zweckzuentfremden waere
    /// bei jeder spaeteren Aenderung eine Falle.
    /// </summary>
    public static IReadOnlyList<DateOnly> GetNextOccurrences(
        DateOnly startDate,
        DateOnly? endDate,
        string intervalUnit,
        int intervalCount,
        int? anchorDay,
        DateOnly from,
        int maxCount)
    {
        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "maxCount muss positiv sein.");
        }

        var occurrences = new List<DateOnly>();

        foreach (var occurrence in EnumerateOccurrences(
                     startDate, intervalUnit, intervalCount, anchorDay, endDate))
        {
            if (occurrence < from)
            {
                continue;
            }

            occurrences.Add(occurrence);

            if (occurrences.Count == maxCount)
            {
                break;
            }
        }

        return occurrences;
    }

    /// <summary>
    /// Der naechste Termin ab <paramref name="from"/> (einschliesslich),
    /// NULL wenn die Laufzeit bereits abgelaufen ist - fuer die Spalte
    /// "naechste Faelligkeit" in der Vorlagenliste.
    /// </summary>
    public static DateOnly? GetNextDueDate(
        DateOnly startDate,
        DateOnly? endDate,
        string intervalUnit,
        int intervalCount,
        int? anchorDay,
        DateOnly from)
    {
        var next = GetNextOccurrences(
            startDate, endDate, intervalUnit, intervalCount, anchorDay, from, maxCount: 1);

        return next.Count == 0 ? null : next[0];
    }

    /// <summary>
    /// Alle Vorkommen der Reihe nach, streng monoton steigend. Die
    /// Pruefung der Argumente steht bewusst hier und nicht in
    /// <see cref="Iterate"/>: eine Methode mit yield laeuft erst beim
    /// ersten MoveNext los, ein falscher IntervalCount wuerde sonst nicht
    /// beim Aufruf auffallen, sondern irgendwo im Aufrufer.
    /// </summary>
    private static IEnumerable<DateOnly> EnumerateOccurrences(
        DateOnly startDate, string intervalUnit, int intervalCount, int? anchorDay, DateOnly? endDate)
    {
        if (intervalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalCount), intervalCount, "IntervalCount muss positiv sein.");
        }

        if (intervalUnit is not ("day" or "week" or "month" or "year"))
        {
            throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unbekannte IntervalUnit.");
        }

        return Iterate(startDate, intervalUnit, intervalCount, anchorDay, endDate);
    }

    // n laeuft von 0 an hoch; da jedes Vorkommen unabhaengig von den
    // anderen direkt aus StartDate berechnet wird, ist die Reihenfolge
    // garantiert streng monoton steigend. Die Folge ist ohne EndDate
    // unendlich - begrenzt wird sie vom Aufrufer, der entweder bis zu
    // einem Stichtag liest oder nach einer festen Anzahl abbricht.
    private static IEnumerable<DateOnly> Iterate(
        DateOnly startDate, string intervalUnit, int intervalCount, int? anchorDay, DateOnly? endDate)
    {
        for (var n = 0; ; n++)
        {
            // Laeuft die Reihe ueber das Ende des darstellbaren Kalenders
            // hinaus, ist Schluss - sonst wuerde DateOnly werfen, und zwar
            // ausgerechnet bei einer unbefristeten Vorlage.
            if (!TryComputeOccurrence(startDate, intervalUnit, intervalCount, anchorDay, n, out var occurrence))
            {
                yield break;
            }

            if (endDate is DateOnly end && occurrence > end)
            {
                yield break;
            }

            yield return occurrence;
        }
    }

    private static bool TryComputeOccurrence(
        DateOnly startDate, string intervalUnit, int intervalCount, int? anchorDay, int n, out DateOnly occurrence)
    {
        occurrence = default;

        switch (intervalUnit)
        {
            case "day":
            case "week":
            {
                // Ueber die fortlaufende Tageszahl statt AddDays, damit ein
                // Ueberlauf pruefbar ist statt zu werfen.
                var faktor = intervalUnit == "week" ? 7L : 1L;
                var dayNumber = startDate.DayNumber + (long)n * intervalCount * faktor;

                if (dayNumber > DateOnly.MaxValue.DayNumber)
                {
                    return false;
                }

                occurrence = DateOnly.FromDayNumber((int)dayNumber);
                return true;
            }

            // Ein Jahresintervall ist ein ganzer Zwoelfmonatsschritt: der
            // Monat bleibt derselbe, nur die Jahreszahl waechst. Deshalb
            // dieselbe Rechnung wie bei month, nur mit Faktor 12.
            case "month":
                return TryAddCalendarMonths(startDate, (long)intervalCount * n, anchorDay, out occurrence);

            case "year":
                return TryAddCalendarMonths(startDate, (long)intervalCount * n * 12, anchorDay, out occurrence);

            default:
                throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unbekannte IntervalUnit.");
        }
    }

    // Monatsarithmetik ueber einen durchlaufenden Monatsindex, damit
    // "StartDate + n * Intervall" auch ueber Jahreswechsel hinweg direkt
    // aus StartDate berechnet wird statt schrittweise vom letzten
    // Vorkommen aus.
    private static bool TryAddCalendarMonths(
        DateOnly startDate, long monthsToAdd, int? anchorDay, out DateOnly result)
    {
        result = default;

        var totalMonths = (long)startDate.Year * 12 + (startDate.Month - 1) + monthsToAdd;
        var year = totalMonths / 12;
        var month = (int)(totalMonths % 12) + 1;

        if (year > 9999)
        {
            return false;
        }

        var day = ClampDay(anchorDay ?? startDate.Day, (int)year, month);
        result = new DateOnly((int)year, month, day);
        return true;
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
