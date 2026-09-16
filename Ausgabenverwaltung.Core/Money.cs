using System.Globalization;
using Ausgabenverwaltung.Core.Formatting;

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

    // Deutsches Zahlenformat (Komma als Dezimaltrennzeichen), fuer die
    // Eingabe in der Erfassungsmaske. Bewusst OHNE AllowThousands: ein
    // Punkt wuerde sonst als Tausendertrennzeichen verschluckt statt
    // abgelehnt zu werden ("12.50" koennte sich sonst still in 1250
    // verwandeln) - bei Geldbetraegen ist eine klare Fehlermeldung
    // sicherer als eine tolerante, aber mehrdeutige Auswertung.
    // Liefert false statt zu werfen, damit Aufrufer ungueltige Eingabe
    // als normalen Fall behandeln koennen.
    public static bool TryParseEuroText(string text, out long cents)
    {
        const NumberStyles styles =
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite;
        if (decimal.TryParse(text, styles, Kultur.DeDe, out var amount))
        {
            cents = ToCents(amount);
            return true;
        }

        cents = 0;
        return false;
    }
}
