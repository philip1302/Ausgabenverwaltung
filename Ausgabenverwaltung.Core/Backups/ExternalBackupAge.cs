namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Alter der letzten erfolgreichen Sicherung in Ziel 2, als Text und als
/// Urteil.
///
/// Das zweite Ziel wird beim Start still uebergangen, wenn es nicht
/// erreichbar ist. Damit daraus kein monatelanges Schweigen wird, zeigen
/// die Einstellungen dieses Alter an - und ab
/// <see cref="StaleAfterDays"/> Tagen hervorgehoben.
/// </summary>
public static class ExternalBackupAge
{
    public const int StaleAfterDays = 14;

    /// <summary>
    /// Volle Tage seit der letzten erfolgreichen externen Sicherung, auf
    /// Kalendertagen gerechnet. NULL = noch nie.
    /// </summary>
    public static int? DaysSince(DateTime? lastUtc, DateTime nowUtc)
    {
        if (lastUtc is not DateTime last)
        {
            return null;
        }

        var days = (nowUtc.Date - last.Date).Days;

        // Eine in der Zukunft liegende Angabe (verstellte Systemuhr) als
        // "heute" behandeln statt als negatives Alter.
        return days < 0 ? 0 : days;
    }

    /// <summary>
    /// Ob der Zustand hervorzuheben ist. "Noch nie" zaehlt dazu: ein
    /// eingetragenes Ziel, in das noch nie geschrieben wurde, ist genau
    /// der Fall, den diese Anzeige aufdecken soll.
    /// </summary>
    public static bool IsStale(DateTime? lastUtc, DateTime nowUtc)
    {
        return DaysSince(lastUtc, nowUtc) is not int days || days > StaleAfterDays;
    }

    public static string ToText(DateTime? lastUtc, DateTime nowUtc)
    {
        return DaysSince(lastUtc, nowUtc) switch
        {
            null => "noch nie",
            0 => "heute",
            1 => "gestern",
            var days => $"vor {days} Tagen",
        };
    }
}
