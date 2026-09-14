using System.Globalization;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die Zeitabschnitte einer Kreuztabelle: Schluessel bilden, lesbar
/// beschriften, eine LUECKENLOSE Folge erzeugen und zu einem Schluessel den
/// zugehoerigen Zeitraum liefern.
///
/// Die Schluesselformate sind exakt dieselben, die
/// <see cref="ReportRepository"/> in SQL erzeugt ("2026", "2026-Q1",
/// "2026-03") - wird eines davon geaendert, muss die Abfrage
/// mitgeaendert werden. Alle drei Formate sortieren alphabetisch =
/// chronologisch, deshalb genuegt ein reiner Zeichenkettenvergleich, um
/// den fruehesten und den spaetesten belegten Abschnitt zu finden.
///
/// Liegt in Core und nicht im ViewModel, weil die Datumsarithmetik
/// (Quartalsgrenzen, Jahreswechsel) pruefbar sein muss (Regel 7).
/// </summary>
public static class ReportPeriods
{
    // Bewusst fest verdrahtet statt ueber CultureInfo: die Beschriftung
    // der Spaltenkoepfe soll auf jedem Rechner gleich aussehen und in den
    // Tests vergleichbar bleiben, unabhaengig davon, welche Kuerzel die
    // installierten Gebietsschema-Daten gerade liefern.
    private static readonly string[] MonthAbbreviations =
    {
        "Jan", "Feb", "Mär", "Apr", "Mai", "Jun",
        "Jul", "Aug", "Sep", "Okt", "Nov", "Dez",
    };

    /// <summary>Der Schluessel des Zeitabschnitts, in dem das Datum liegt.</summary>
    public static string Key(DateOnly date, ReportGrouping grouping)
    {
        var year = date.Year.ToString("D4", CultureInfo.InvariantCulture);

        return grouping switch
        {
            ReportGrouping.Year => year,
            ReportGrouping.Quarter => year + "-Q" + Quarter(date.Month),
            ReportGrouping.Month =>
                year + "-" + date.Month.ToString("D2", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };
    }

    /// <summary>Der erste Tag des Zeitabschnitts zu einem Schluessel.</summary>
    public static DateOnly Start(string key, ReportGrouping grouping)
    {
        if (!TryParse(key, grouping, out var start))
        {
            throw new FormatException(
                $"'{key}' ist kein Zeitabschnitt-Schluessel der Gruppierung {grouping}.");
        }

        return start;
    }

    /// <summary>
    /// Der Zeitraum eines Zeitabschnitts - mit einschliessendem Anfang und
    /// ausschliessendem Ende, also direkt als <see cref="ReportFilter"/>-
    /// Grenzen verwendbar (Sprung von einer Zelle in die Einzelbuchungen).
    /// </summary>
    public static DateRange Range(string key, ReportGrouping grouping)
    {
        var start = Start(key, grouping);
        return new DateRange(start, Next(start, grouping));
    }

    /// <summary>
    /// Alle Schluessel von <paramref name="firstKey"/> bis
    /// <paramref name="lastKey"/> einschliesslich - auch die dazwischen
    /// liegenden Abschnitte OHNE Buchungen. Genau darum geht es: eine
    /// Luecke ist nur erkennbar, wenn die leere Spalte stehen bleibt.
    /// </summary>
    public static IReadOnlyList<string> Enumerate(
        string firstKey, string lastKey, ReportGrouping grouping)
    {
        var current = Start(firstKey, grouping);
        var last = Start(lastKey, grouping);

        if (current > last)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>();
        while (true)
        {
            keys.Add(Key(current, grouping));
            if (current >= last)
            {
                return keys;
            }

            current = Next(current, grouping);
        }
    }

    /// <summary>Beschriftung des Spaltenkopfs, z. B. "Mär 2026" oder "Q1 2026".</summary>
    public static string Label(string key, ReportGrouping grouping)
    {
        var start = Start(key, grouping);
        var year = start.Year.ToString("D4", CultureInfo.InvariantCulture);

        return grouping switch
        {
            ReportGrouping.Year => year,
            ReportGrouping.Quarter => "Q" + Quarter(start.Month) + " " + year,
            ReportGrouping.Month => MonthAbbreviations[start.Month - 1] + " " + year,
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };
    }

    /// <summary>
    /// Das Monatskuerzel allein, z. B. "Mär" - fuer Achsen, an denen die
    /// Jahreszahl anderswo steht und je Beschriftung wiederholt nur Platz
    /// kostete.
    /// </summary>
    public static string MonthAbbreviation(int month) => MonthAbbreviations[month - 1];

    private static int Quarter(int month) => (month - 1) / 3 + 1;

    // Der Anfang des naechsten Zeitabschnitts. Am aeussersten Rand des
    // Kalenders wuerde AddMonths ueberlaufen; dort bleibt es beim
    // groesstmoeglichen Datum, das Ende ist dann ohnehin erreicht.
    private static DateOnly Next(DateOnly start, ReportGrouping grouping)
    {
        var months = grouping switch
        {
            ReportGrouping.Year => 12,
            ReportGrouping.Quarter => 3,
            ReportGrouping.Month => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };

        // Monate seit Jahresanfang 0001 - so laesst sich der Ueberlauf
        // pruefen, ohne ihn ausloesen zu muessen.
        var monthIndex = (start.Year - 1) * 12 + (start.Month - 1) + months;
        var lastMonthIndex = (DateOnly.MaxValue.Year - 1) * 12 + (DateOnly.MaxValue.Month - 1);

        return monthIndex > lastMonthIndex
            ? DateOnly.MaxValue
            : new DateOnly(monthIndex / 12 + 1, monthIndex % 12 + 1, 1);
    }

    private static bool TryParse(string key, ReportGrouping grouping, out DateOnly start)
    {
        start = default;

        if (key.Length < 4 ||
            !int.TryParse(
                key.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        switch (grouping)
        {
            case ReportGrouping.Year when key.Length == 4:
                start = new DateOnly(year, 1, 1);
                return true;

            case ReportGrouping.Quarter when key.Length == 7 && key[4] == '-' && key[5] == 'Q':
                var quarter = key[6] - '0';
                if (quarter is < 1 or > 4)
                {
                    return false;
                }

                start = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
                return true;

            case ReportGrouping.Month when key.Length == 7 && key[4] == '-':
                if (!int.TryParse(
                        key.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture,
                        out var month) ||
                    month is < 1 or > 12)
                {
                    return false;
                }

                start = new DateOnly(year, month, 1);
                return true;

            default:
                return false;
        }
    }
}
