using Ausgabenverwaltung.Core.Entities;

namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Rechnet Vorlagen unterschiedlichen Rhythmus auf eine gemeinsame
/// Monatsbasis um, damit die Verwaltungsliste eine Summe "was kostet mich
/// das im Monat" zeigen kann.
///
/// Fuer Monat und Jahr ist die Rechnung eindeutig. Fuer Tag und Woche
/// braucht es eine Konvention: hier die mittlere Monatslaenge von
/// 365,25 / 12 = 30,4375 Tagen. Die naheliegende Alternative "vier Wochen
/// sind ein Monat" liegt im Jahr gut 8 % zu niedrig (52 Wochen gegen 48)
/// und wuerde die Belastung systematisch zu guenstig darstellen.
/// </summary>
public static class MonthlyBurden
{
    private static readonly decimal TageProMonat = 365.25m / 12m;
    private static readonly decimal WochenProMonat = TageProMonat / 7m;

    /// <summary>
    /// Monatliche Belastung EINER Vorlage in Euro, ungerundet. Bewusst
    /// decimal und nicht long Cent: gerundet wird erst in
    /// <see cref="TotalPerMonthCents"/>, sonst summiert sich der
    /// Rundungsfehler ueber alle Vorlagen auf (Regel 1 - decimal an der
    /// Rechengrenze, Cent in der Speicherform).
    /// </summary>
    public static decimal PerMonthEuro(RecurringExpense template) =>
        Money.ToDecimal(template.AmountCents)
        * OccurrencesPerMonth(template.IntervalUnit, template.IntervalCount);

    /// <summary>
    /// Summe der monatlichen Belastung aller Vorlagen, die zum Stichtag
    /// tatsaechlich laufen: inaktive und bereits abgelaufene zaehlen nicht.
    /// Eine Vorlage, deren Startdatum noch in der Zukunft liegt, zaehlt
    /// dagegen mit - sie ist eine kommende, aber beschlossene Belastung.
    ///
    /// Das Vorzeichen kommt vom Vorlagentyp: eine Ausgaben-Vorlage
    /// mindert die Belastung (negativ), eine Einnahme-Vorlage (siehe
    /// RecurringExpense.IsIncome, z. B. ein monatliches Gehalt) erhoeht
    /// sie (positiv) - und zwar unbedingt, anders als bei den einzelnen
    /// Buchungen in ExpenseRepository.Summarize/ReportRepository, wo eine
    /// Einnahme erst zaehlt, sobald sie tatsaechlich eingegangen ist
    /// (SettledDate gesetzt). Eine Vorlage selbst hat kein SettledDate -
    /// "monatliche Belastung" ist eine Vorausschau ueber aktive Vorlagen,
    /// kein Rueckblick auf bereits eingegangene Buchungen, und kennt
    /// deshalb keinen offen/beglichen-Zustand, an dem sich jene Regel
    /// festmachen liesse.
    /// </summary>
    public static long TotalPerMonthCents(IEnumerable<RecurringExpense> templates, DateOnly asOf)
    {
        var summeEuro = templates
            .Where(template => template.IsActive)
            .Where(template => template.EndDate is null || template.EndDate >= asOf)
            .Sum(template => template.IsIncome ? PerMonthEuro(template) : -PerMonthEuro(template));

        return Money.ToCents(summeEuro);
    }

    private static decimal OccurrencesPerMonth(string intervalUnit, int intervalCount)
    {
        if (intervalCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalCount), intervalCount, "IntervalCount muss positiv sein.");
        }

        return intervalUnit switch
        {
            "day" => TageProMonat / intervalCount,
            "week" => WochenProMonat / intervalCount,
            "month" => 1m / intervalCount,
            "year" => 1m / (12m * intervalCount),
            _ => throw new ArgumentOutOfRangeException(nameof(intervalUnit), intervalUnit, "Unbekannte IntervalUnit."),
        };
    }
}
