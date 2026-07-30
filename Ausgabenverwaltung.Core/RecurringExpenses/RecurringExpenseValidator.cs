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

        var betragGueltig = Money.TryParseEuroText(input.AmountText, out var amountCents);

        var anzahlGueltig =
            int.TryParse(
                input.IntervalCountText?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var intervalCount)
            && intervalCount >= 1;

        var startGueltig = GermanDateInput.TryParse(input.StartDateText?.Trim() ?? string.Empty, out var startDate);

        var (anchorDay, anchorFehler) = PruefeAnkertag(input);
        var (endDate, endFehler) = PruefeEnddatum(input, startGueltig, startDate);

        return new RecurringExpenseValidation
        {
            TitleError = string.IsNullOrWhiteSpace(input.Title)
                ? "Bitte einen Titel angeben."
                : null,

            CategoryError = input.CategoryId is null
                ? "Bitte eine Kategorie waehlen."
                : null,

            PayerError = input.PayerId is null
                ? "Bitte einen Zahler waehlen."
                : null,

            AmountError = betragGueltig
                ? null
                : "Ungueltiger Betrag (Beispiel: 12,50).",

            IntervalCountError = anzahlGueltig
                ? null
                : "Die Intervallanzahl muss eine ganze Zahl ab 1 sein.",

            AnchorDayError = anchorFehler,

            StartDateError = startGueltig
                ? null
                : "Ungueltiges Startdatum (TT.MM.JJJJ).",

            EndDateError = endFehler,

            AmountCents = amountCents,
            IntervalCount = intervalCount,
            AnchorDay = anchorDay,
            StartDate = startDate,
            EndDate = endDate,
        };
    }

    // Der Ankertag ist nur bei Monats- und Jahresrhythmus ueberhaupt
    // gemeint; bei Tagen und Wochen wird ein dort stehen gebliebener Wert
    // stillschweigend verworfen statt bemaengelt - das Formular blendet
    // das Feld dann ohnehin aus.
    private static (int? AnchorDay, string? Fehler) PruefeAnkertag(RecurringExpenseInput input)
    {
        if (input.IntervalUnit is not ("month" or "year"))
        {
            return (null, null);
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
            return (null, "Ungueltiges Enddatum (TT.MM.JJJJ).");
        }

        // Der Vergleich ist nur aussagekraeftig, wenn das Startdatum selbst
        // lesbar war - sonst stuende hier eine Folgemeldung, die vom
        // eigentlichen Fehler ablenkt.
        if (startGueltig && endDate < startDate)
        {
            return (endDate, "Das Enddatum liegt vor dem Startdatum.");
        }

        return (endDate, null);
    }
}
