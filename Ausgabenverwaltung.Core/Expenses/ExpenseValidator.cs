using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Prueft die Eingaben eines Ausgabenformulars vollstaendig und liefert
/// gleichzeitig die umgewandelten Werte.
///
/// Liegt in Core, weil das die eigentliche Fachpruefung ist und ohne
/// Oberflaeche testbar sein muss (Regel 7). Erfassungsmaske und
/// Bearbeiten-Dialog rufen beide hierher - vorher stand die Pruefung
/// zweimal fast, aber eben nur fast gleich in den ViewModels.
/// </summary>
public static class ExpenseValidator
{
    public static ExpenseValidation Validate(ExpenseInput input)
    {
        var (amountCents, amountError) = PruefeBetrag(input.AmountText, input.IsIncome);
        var (date, dateError, dateConfirmation) = PruefeDatum(input.DateText, input.Today);

        return new ExpenseValidation
        {
            AmountError = amountError,

            CategoryError = input.CategoryId is null
                ? "Bitte eine Kategorie wählen."
                : null,

            PayerError = input.PayerId is null
                ? "Bitte einen Zahler wählen."
                : null,

            DateError = dateError,
            DateConfirmation = dateConfirmation,

            AmountCents = amountCents,
            Date = date,
        };
    }

    private static (long Cents, string? Fehler) PruefeBetrag(string? amountText, bool isIncome)
    {
        if (string.IsNullOrWhiteSpace(amountText))
        {
            return (0, "Bitte einen Betrag eingeben.");
        }

        if (!Money.TryParseEuroText(amountText, out var cents))
        {
            return (0, "Das ist kein gültiger Betrag. Beispiel: 12,50");
        }

        // Null ausdruecklich abgelehnt: eine Ausgabe ueber 0,00 € ist
        // keine Ausgabe. Sie entsteht fast immer durch ein abgeschicktes
        // leeres Feld oder eine abgeschnittene Eingabe und wuerde sonst
        // still in der Liste landen.
        //
        // Negative Betraege bleiben ausdruecklich erlaubt - so werden
        // Erstattungen erfasst (siehe die Darstellung als "Erstattung" in
        // der Ausgabenliste). Bei einer Einnahme waere ein negativer
        // Betrag aber zweideutig (mindert er die Summe zusaetzlich, oder
        // hebt er sich mit dem Einnahme-Attribut gerade auf?) - deshalb
        // hier ausdruecklich abgelehnt, statt die beiden Vorzeichen-
        // Konzepte miteinander zu vermischen.
        if (cents == 0)
        {
            return (0, "Der Betrag darf nicht null sein.");
        }

        if (isIncome && cents < 0)
        {
            return (0, "Eine Einnahme darf keinen negativen Betrag haben.");
        }

        return (cents, null);
    }

    private static (DateOnly Datum, string? Fehler, string? Rueckfrage) PruefeDatum(
        string? dateText, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(dateText))
        {
            return (default, "Bitte ein Datum eingeben (TT.MM.JJJJ).", null);
        }

        if (!GermanDateInput.TryParse(dateText.Trim(), out var date))
        {
            return (default, "Das ist kein gültiges Datum. Beispiel: 05.03.2026", null);
        }

        // Erst die harte Grenze (Tippfehler im Jahr), dann die Rueckfrage.
        // Confirmation liefert fuer ein bereits abgelehntes Datum von sich
        // aus NULL, damit an einem Feld nie zwei Meldungen stehen.
        var fehler = DatePlausibility.Error(date);
        var rueckfrage = DatePlausibility.Confirmation(date, today);

        return (date, fehler, rueckfrage);
    }
}
