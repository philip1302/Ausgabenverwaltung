namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die Zeitraum-Schnellwahl der Ausgabenliste ("dieser Monat", "dieses
/// Jahr", "letzte 12 Monate", "alles"). Liegt in Core und bekommt das
/// heutige Datum uebergeben statt es selbst zu lesen - so ist die
/// Datumsarithmetik (Monats-/Jahresgrenzen, Schaltjahre) pruefbar und
/// steckt nicht im ViewModel (Regel 7).
/// </summary>
public static class DateRangePresets
{
    /// <summary>Vom Monatsersten bis zum Ersten des Folgemonats.</summary>
    public static DateRange ThisMonth(DateOnly today)
    {
        var from = new DateOnly(today.Year, today.Month, 1);
        return new DateRange(from, from.AddMonths(1));
    }

    /// <summary>Vom 1. Januar bis zum 1. Januar des Folgejahres.</summary>
    public static DateRange ThisYear(DateOnly today)
    {
        var from = new DateOnly(today.Year, 1, 1);
        return new DateRange(from, from.AddYears(1));
    }

    /// <summary>Das komplette Vorjahr, vom 1. Januar bis zum 1. Januar.</summary>
    public static DateRange LastYear(DateOnly today)
    {
        var from = new DateOnly(today.Year - 1, 1, 1);
        return new DateRange(from, from.AddYears(1));
    }

    /// <summary>
    /// Die letzten drei KALENDERjahre einschliesslich des laufenden.
    /// Bewusst nicht rollierend wie <see cref="LastTwelveMonths"/>: in der
    /// nach Jahren gruppierten Auswertung sollen die Jahresspalten
    /// vollstaendig sein und nicht mit einem angebrochenen Monat beginnen.
    /// </summary>
    public static DateRange LastThreeYears(DateOnly today) =>
        new(new DateOnly(today.Year - 2, 1, 1), new DateOnly(today.Year + 1, 1, 1));

    /// <summary>
    /// Die letzten zwoelf Monate, heute eingeschlossen. AddMonths kuerzt
    /// selbst auf den letzten Tag des Zielmonats, wenn es den Ankertag
    /// dort nicht gibt (29.02. -> 28.02.).
    /// </summary>
    public static DateRange LastTwelveMonths(DateOnly today) =>
        new(today.AddMonths(-12), today.AddDays(1));

    /// <summary>
    /// Keine zeitliche Einschraenkung. Das Ende ist ausschliessend, der
    /// 31.12.9999 fiele also formal heraus - fuer Belegdatumsangaben ohne
    /// praktische Bedeutung.
    /// </summary>
    public static DateRange Everything() => new(DateOnly.MinValue, DateOnly.MaxValue);

    /// <summary>
    /// Zeitraum aus den von/bis-Eingaben der Filterleiste. Beide Grenzen
    /// sind dort EINschliessend gemeint und einzeln weglassbar (NULL =
    /// offene Grenze). Das Ende wird deshalb um einen Tag verschoben,
    /// damit die ausschliessende Filtersemantik erhalten bleibt - der
    /// eingegebene Tag selbst gehoert noch dazu.
    /// </summary>
    public static DateRange FromInclusiveBounds(DateOnly? from, DateOnly? toInclusive)
    {
        var unbegrenzt = Everything();

        // AddDays am 31.12.9999 wuerde ueberlaufen; dort ist das offene
        // Ende ohnehin schon erreicht.
        var toExclusive = unbegrenzt.ToExclusive;
        if (toInclusive is DateOnly bis && bis < DateOnly.MaxValue)
        {
            toExclusive = bis.AddDays(1);
        }

        return new DateRange(from ?? unbegrenzt.From, toExclusive);
    }
}
