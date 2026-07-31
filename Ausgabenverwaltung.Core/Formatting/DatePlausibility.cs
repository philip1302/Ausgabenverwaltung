using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Prueft, ob ein eingegebenes Datum ueberhaupt gemeint sein kann.
///
/// Zwei Stufen, und der Unterschied zwischen ihnen ist der Kern dieser
/// Klasse:
///
/// <b>Abgelehnt</b> wird nur, was gar nicht gemeint sein KANN - das Jahr
/// 200 statt 2026, entstanden durch eine verrutschte Ziffer. Solche Werte
/// haben keinen Anwendungsfall.
///
/// <b>Nachgefragt</b> wird bei allem, was ungewoehnlich, aber denkbar ist:
/// eine Buchung weit in der Zukunft, eine Nachtragung von vor mehr als
/// zehn Jahren. Wer alte Belege nacherfasst, hat gute Gruende dafuer, und
/// eine Anwendung, die ihm das verbietet, ist im Weg. Sie fragt einmal
/// nach - mehr nicht.
///
/// Reine Rechnung auf Datumswerten, deshalb vollstaendig pruefbar
/// (Regel 7).
/// </summary>
public static class DatePlausibility
{
    /// <summary>Frueheste Jahreszahl, die noch als Absicht durchgeht.</summary>
    public const int MinYear = 1900;

    /// <summary>Spaeteste Jahreszahl, die noch als Absicht durchgeht.</summary>
    public const int MaxYear = 2200;

    /// <summary>Ab wie vielen Jahren in der Vergangenheit nachgefragt wird.</summary>
    public const int PastConfirmYears = 10;

    /// <summary>Ab wie vielen Jahren in der Zukunft nachgefragt wird.</summary>
    public const int FutureConfirmYears = 1;

    /// <summary>
    /// Eine Jahreszahl, die niemand so gemeint haben kann. Liefert NULL,
    /// wenn das Datum in Ordnung ist - sonst die Meldung fuer das Feld.
    /// </summary>
    public static string? Error(DateOnly date)
    {
        if (date.Year >= MinYear && date.Year <= MaxYear)
        {
            return null;
        }

        // Die getippte Jahreszahl steht in der Meldung: der Tippfehler ist
        // damit sofort zu sehen, ohne dass man ihn im Feld suchen muss.
        return $"Das Jahr {date.Year.ToString(CultureInfo.InvariantCulture)} ist wohl ein "
             + $"Tippfehler. Bitte ein Jahr zwischen {MinYear} und {MaxYear} eingeben.";
    }

    /// <summary>
    /// Ein ungewoehnliches, aber moegliches Datum. Liefert NULL, wenn
    /// nichts nachzufragen ist - sonst den Text der Rueckfrage.
    ///
    /// Gilt nur fuer das Datum einer einzelnen Ausgabe. Bei einer Vorlage
    /// ist ein Enddatum in ferner Zukunft der Normalfall und eine
    /// Rueckfrage dort nur Rauschen.
    /// </summary>
    public static string? Confirmation(DateOnly date, DateOnly today)
    {
        // Ein bereits abgelehntes Datum wird nicht zusaetzlich noch
        // nachgefragt - sonst stuenden zwei Meldungen an einem Feld.
        if (Error(date) is not null)
        {
            return null;
        }

        if (date > today.AddYears(FutureConfirmYears))
        {
            return $"Der {GermanDateInput.ToText(date)} liegt mehr als "
                 + $"{JahreText(FutureConfirmYears)} in der Zukunft. "
                 + "Ist das so gewollt?";
        }

        if (date < today.AddYears(-PastConfirmYears))
        {
            return $"Der {GermanDateInput.ToText(date)} liegt mehr als "
                 + $"{JahreText(PastConfirmYears)} zurück. "
                 + "Ist das so gewollt?";
        }

        return null;
    }

    private static string JahreText(int jahre)
        => jahre == 1 ? "ein Jahr" : $"{jahre.ToString(CultureInfo.InvariantCulture)} Jahre";
}
