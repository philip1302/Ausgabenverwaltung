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
        var (amountCents, amountError) = PruefeBetrag(input.AmountText);
        var (date, dateError, dateConfirmation) = PruefeDatum(input.DateText, input.Today);

        return new ExpenseValidation
        {
            AmountError = amountError,

            CategoryError = input.CategoryId is null
                ? "Bitte eine Kategorie wählen."
                : null,

            // Eine Einnahme kommt immer von jemand anderem - erst damit
            // laesst sich ueber die Offene-Posten-Liste verfolgen, ob sie
            // tatsaechlich eingegangen ist (Regel 4: SettledDate wird bei
            // der eigenen Person nie ausgewertet). Die Oberflaeche
            // verhindert die Wahl der eigenen Person bei einer Einnahme
            // bereits (die Ich-Person faellt aus der Auswahl heraus),
            // diese Pruefung ist die zweite, von der UI unabhaengige
            // Absicherung (Regel 7).
            PayerError = input.PayerId is null
                ? "Bitte einen Zahler wählen."
                : input.IsIncome && input.PayerIsSelf
                    ? "Eine Einnahme braucht einen Zahler, der nicht die eigene Person ist."
                    : null,

            DateError = dateError,
            DateConfirmation = dateConfirmation,

            AmountCents = amountCents,
            Date = date,
        };
    }

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

        // Null ausdruecklich abgelehnt: eine Ausgabe ueber 0,00 € ist
        // keine Ausgabe. Sie entsteht fast immer durch ein abgeschicktes
        // leeres Feld oder eine abgeschnittene Eingabe und wuerde sonst
        // still in der Liste landen.
        if (cents == 0)
        {
            return (0, "Der Betrag darf nicht null sein.");
        }

        // Das Vorzeichen kommt ausschliesslich vom Buchungstyp
        // (Entities.Expense.IsIncome, siehe EuroText.FormatSigned) -
        // Ausgabe erscheint negativ/rot, Einnahme positiv/gruen. Eingegeben
        // wird deshalb immer ein positiver Betrag; es gibt kein manuelles
        // negatives Vorzeichen mehr (frueher: Erstattung - ein tatsaechlicher
        // Rueckfluss wird jetzt als Einnahme erfasst).
        if (cents < 0)
        {
            return (0, "Der Betrag darf nicht negativ sein.");
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
