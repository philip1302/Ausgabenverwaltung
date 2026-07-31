using System.Globalization;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Prueft die Eingaben des Vorlagenformulars vollstaendig und liefert
/// gleichzeitig die umgewandelten Werte. Liegt in Core, weil das die
/// eigentliche Fachpruefung ist und ohne Oberflaeche testbar sein muss
/// (Regel 7) - die ViewModels verteilen nur noch die Meldungen auf ihre
/// Felder.
/// </summary>
public static class RecurringExpenseValidator
{
    public static RecurringExpenseValidation Validate(RecurringExpenseInput input)
    {
        if (input.IntervalUnit is not ("day" or "week" or "month" or "year"))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input), input.IntervalUnit, "Unbekannte IntervalUnit.");
        }

        var (amountCents, betragFehler) = PruefeBetrag(input.AmountText);

        var anzahlGueltig =
            int.TryParse(
                input.IntervalCountText?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var intervalCount)
            && intervalCount >= 1;

        var (startDate, startFehler) = PruefeStartdatum(input.StartDateText);
        var (anchorDay, anchorFehler) = PruefeAnkertag(input);
        var (endDate, endFehler) = PruefeEnddatum(input, startFehler is null, startDate);

        return new RecurringExpenseValidation
        {
            TitleError = string.IsNullOrWhiteSpace(input.Title)
                ? "Bitte einen Titel angeben."
                : null,

            CategoryError = input.CategoryId is null
                ? "Bitte eine Kategorie wählen."
                : null,

            PayerError = input.PayerId is null
                ? "Bitte einen Zahler wählen."
                : null,

            AmountError = betragFehler,

            IntervalCountError = anzahlGueltig
                ? null
                : "Die Intervallanzahl muss eine ganze Zahl ab 1 sein.",

            AnchorDayError = anchorFehler,

            StartDateError = startFehler,

            EndDateError = endFehler,

            AmountCents = amountCents,
            IntervalCount = intervalCount,
            AnchorDay = anchorDay,
            StartDate = startDate,
            EndDate = endDate,
        };
    }

    // Dieselbe Regel wie bei einer einzelnen Ausgabe (siehe
    // Expenses.ExpenseValidator): eine Vorlage ueber 0,00 € erzeugt Monat
    // fuer Monat Buchungen, die nichts aussagen.
    private static (long Cents, string? Fehler) PruefeBetrag(string? amountText)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return (0, "Bitte einen Betrag eingeben.");
        }

        if (!Money.TryParseEuroText(amountText, out var cents))
        {
            return (0, "Das ist kein gültiger Betrag. Beispiel: 12,50");
        }

        if (cents == 0)
        {
            return (0, "Der Betrag darf nicht null sein.");
        }

        return (cents, null);
    }

    private static (DateOnly Datum, string? Fehler) PruefeStartdatum(string? startDateText)
    {
        if (string.IsNullOrWhiteSpace(startDateText))
        {
            return (default, "Bitte ein Startdatum eingeben (TT.MM.JJJJ).");
        }

        if (!GermanDateInput.TryParse(startDateText.Trim(), out var startDate))
        {
            return (default, "Das ist kein gültiges Startdatum. Beispiel: 05.03.2026");
        }

        // Nur die harte Grenze, keine Rueckfrage: bei einer Vorlage ist ein
        // weit zurueckliegender Beginn ein gewoehnlicher Fall, und was
        // dabei rueckwirkend entsteht, laesst sich das Formular ohnehin
        // gesondert bestaetigen (siehe RueckwirkendBestaetigungNoetig).
        return (startDate, DatePlausibility.Error(startDate));
    }

    // Der Ankertag ist nur bei Monats- und Jahresrhythmus ueberhaupt
    // gemeint. Bei Tagen und Wochen gibt es keinen "immer am 15." - der
    // Rhythmus zaehlt dort schlicht vom Startdatum weiter.
    private static (int? AnchorDay, string? Fehler) PruefeAnkertag(RecurringExpenseInput input)
    {
        if (input.IntervalUnit is not ("month" or "year"))
        {
            if (string.IsNullOrWhiteSpace(input.AnchorDayText))
            {
                return (null, null);
            }

            // Frueher wurde ein hier stehen gebliebener Wert
            // stillschweigend verworfen. Das ist die schlechtere Antwort:
            // wer "alle 2 Wochen" und daneben "am 15." einstellt, meint
            // etwas Bestimmtes, und bekaeme sonst ohne ein Wort etwas
            // anderes. Das Formular raeumt das Feld beim Wechsel der
            // Einheit; kommt hier trotzdem ein Wert an, wird er benannt.
            return (null,
                "Ein fester Tag im Monat lässt sich nur bei einem Monats- oder "
                + "Jahresrhythmus angeben. Bitte das Feld leeren oder den Rhythmus "
                + "umstellen.");
        }

        if (string.IsNullOrWhiteSpace(input.AnchorDayText))
        {
            // Leer ist zulaessig: dann gilt der Tag des Startdatums.
            return (null, null);
        }

        var gueltig =
            int.TryParse(
                input.AnchorDayText.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var anchorDay)
            && anchorDay is >= 1 and <= 31;

        return gueltig
            ? (anchorDay, null)
            : (null, "Der Ankertag muss zwischen 1 und 31 liegen.");
    }

    private static (DateOnly? EndDate, string? Fehler) PruefeEnddatum(
        RecurringExpenseInput input, bool startGueltig, DateOnly startDate)
    {
        if (string.IsNullOrWhiteSpace(input.EndDateText))
        {
            // Leer = unbefristet, kein Fehler.
            return (null, null);
        }

        if (!GermanDateInput.TryParse(input.EndDateText.Trim(), out var endDate))
        {
            return (null, "Das ist kein gültiges Enddatum. Beispiel: 31.12.2030");
        }

        if (DatePlausibility.Error(endDate) is string jahresFehler)
        {
            return (endDate, jahresFehler);
        }

        // Der Vergleich ist nur aussagekraeftig, wenn das Startdatum selbst
        // lesbar war - sonst stuende hier eine Folgemeldung, die vom
        // eigentlichen Fehler ablenkt.
        if (startGueltig && endDate < startDate)
        {
            return (endDate,
                $"Das Enddatum liegt vor dem Startdatum ({GermanDateInput.ToText(startDate)}). "
                + "Bitte ein späteres Enddatum wählen oder das Feld leeren, "
                + "damit die Vorlage unbefristet läuft.");
        }

        return (endDate, null);
    }
}
