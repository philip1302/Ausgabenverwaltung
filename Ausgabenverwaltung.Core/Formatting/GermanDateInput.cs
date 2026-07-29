using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Umwandlung zwischen <see cref="DateOnly"/> und dem vom Anwender
/// getippten deutschen Datumsformat ('TT.MM.JJJJ'), z. B. in der
/// Erfassungsmaske. Bewusst getrennt von <see cref="IsoDate"/>, das
/// ausschliesslich das DB-Speicherformat ('YYYY-MM-DD', Regel 3) behandelt
/// - dieses Format ist reine Anzeige-/Eingabesache und darf nie in die
/// Datenbank gelangen.
/// </summary>
public static class GermanDateInput
{
    // Ein- und zweistellige Tages-/Monatsangaben werden beide akzeptiert,
    // damit schnelles Tippen ohne fuehrende Nullen nicht blockiert.
    private static readonly string[] Formats = ["dd.MM.yyyy", "d.M.yyyy"];

    public static string ToText(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out DateOnly date) =>
        DateOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
