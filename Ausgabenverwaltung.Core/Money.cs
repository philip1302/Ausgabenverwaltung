using System.Globalization;

namespace Ausgabenverwaltung.Core;

/// <summary>
/// Zentrale Umwandlung zwischen Cent (Speicherform in der DB, immer
/// <see cref="long"/>) und Euro als <see cref="decimal"/> (Rechen- und
/// Anzeigeform). Nirgendwo sonst im Code darf zwischen beiden
/// Darstellungen konvertiert werden.
/// </summary>
public static class Money
{
    public static decimal ToDecimal(long cents) => cents / 100m;

    public static long ToCents(decimal amount)
    {
        var rounded = Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        return Convert.ToInt64(rounded, CultureInfo.InvariantCulture);
    }
}
