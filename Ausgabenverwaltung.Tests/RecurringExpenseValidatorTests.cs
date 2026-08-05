using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Tests;

public class RecurringExpenseValidatorTests
{
    [Fact]
    public void Gueltige_Eingabe_liefert_die_geparsten_Werte()
    {
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe());

        Assert.True(ergebnis.IsValid);
        Assert.Equal(42_50, ergebnis.AmountCents);
        Assert.Equal(1, ergebnis.IntervalCount);
        Assert.Equal(15, ergebnis.AnchorDay);
        Assert.Equal(new DateOnly(2026, 1, 15), ergebnis.StartDate);
        Assert.Null(ergebnis.EndDate);
    }

    [Fact]
    public void Alle_Fehler_werden_gleichzeitig_gemeldet()
    {
        // Sonst muesste der Anwender mehrfach hintereinander speichern, um
        // alle Beanstandungen zu sehen.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            Title = "   ",
            CategoryId = null,
            PayerId = null,
            AmountText = "zwoelf",
            IntervalCountText = "0",
            AnchorDayText = "40",
            StartDateText = "31.02.",
        });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.TitleError);
        Assert.NotNull(ergebnis.CategoryError);
        Assert.NotNull(ergebnis.PayerError);
        Assert.NotNull(ergebnis.AmountError);
        Assert.NotNull(ergebnis.IntervalCountError);
        Assert.NotNull(ergebnis.AnchorDayError);
        Assert.NotNull(ergebnis.StartDateError);
    }

    [Fact]
    public void Enddatum_vor_Startdatum_wird_gemeldet()
    {
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            StartDateText = "01.06.2026",
            EndDateText = "31.05.2026",
        });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.EndDateError);
        Assert.Null(ergebnis.StartDateError);
    }

    [Fact]
    public void Enddatum_gleich_Startdatum_ist_zulaessig()
    {
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            StartDateText = "01.06.2026",
            EndDateText = "01.06.2026",
        });

        Assert.True(ergebnis.IsValid);
        Assert.Equal(new DateOnly(2026, 6, 1), ergebnis.EndDate);
    }

    [Fact]
    public void Leeres_Enddatum_bedeutet_unbefristet_und_ist_kein_Fehler()
    {
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with { EndDateText = "  " });

        Assert.True(ergebnis.IsValid);
        Assert.Null(ergebnis.EndDate);
    }

    [Fact]
    public void Bei_unlesbarem_Startdatum_bleibt_das_Enddatum_unbeanstandet()
    {
        // Sonst stuende dort eine Folgemeldung, die vom eigentlichen
        // Fehler ablenkt.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            StartDateText = "Unsinn",
            EndDateText = "01.01.2020",
        });

        Assert.NotNull(ergebnis.StartDateError);
        Assert.Null(ergebnis.EndDateError);
    }

    [Fact]
    public void Ankertag_ausserhalb_von_1_bis_31_wird_gemeldet()
    {
        Assert.NotNull(RecurringExpenseValidator.Validate(Eingabe() with { AnchorDayText = "0" }).AnchorDayError);
        Assert.NotNull(RecurringExpenseValidator.Validate(Eingabe() with { AnchorDayText = "32" }).AnchorDayError);
    }

    [Fact]
    public void Leerer_Ankertag_ist_zulaessig_und_bleibt_null()
    {
        // NULL bedeutet: es gilt der Tag des Startdatums (Regel 5).
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with { AnchorDayText = "" });

        Assert.True(ergebnis.IsValid);
        Assert.Null(ergebnis.AnchorDay);
    }

    [Fact]
    public void Ankertag_ohne_passende_Intervalleinheit_wird_bemaengelt()
    {
        // Frueher wurde ein bei Tag/Woche stehen gebliebener Wert
        // stillschweigend verworfen. Wer "alle 2 Wochen" und daneben "am
        // 15." einstellt, meint aber etwas Bestimmtes und bekaeme sonst
        // ohne ein Wort etwas anderes.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            IntervalUnit = "week",
            AnchorDayText = "15",
        });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.AnchorDayError);
        Assert.Null(ergebnis.AnchorDay);
    }

    [Fact]
    public void Leerer_Ankertag_ist_bei_Wochenrhythmus_in_Ordnung()
    {
        // Der Normalfall aus dem Formular: das Feld ist bei Tag und Woche
        // ausgeblendet und wird beim Wechsel der Einheit geraeumt.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with
        {
            IntervalUnit = "week",
            AnchorDayText = "",
        });

        Assert.True(ergebnis.IsValid);
        Assert.Null(ergebnis.AnchorDay);
    }

    [Fact]
    public void Intervallanzahl_muss_mindestens_eins_sein()
    {
        Assert.NotNull(RecurringExpenseValidator.Validate(Eingabe() with { IntervalCountText = "0" }).IntervalCountError);
        Assert.NotNull(RecurringExpenseValidator.Validate(Eingabe() with { IntervalCountText = "-2" }).IntervalCountError);
        Assert.NotNull(RecurringExpenseValidator.Validate(Eingabe() with { IntervalCountText = "1,5" }).IntervalCountError);
    }

    [Fact]
    public void Negativer_Betrag_ist_zulaessig()
    {
        // Negative Werte sind Erstattungen - dieselbe Regel wie bei den
        // einzelnen Ausgaben.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with { AmountText = "-15,00" });

        Assert.True(ergebnis.IsValid);
        Assert.Equal(-15_00, ergebnis.AmountCents);
    }

    [Fact]
    public void Eine_Einnahme_mit_positivem_Betrag_ist_gueltig()
    {
        var ergebnis = RecurringExpenseValidator.Validate(
            Eingabe() with { AmountText = "3000,00", IsIncome = true });

        Assert.True(ergebnis.IsValid);
        Assert.Equal(300_000, ergebnis.AmountCents);
    }

    [Fact]
    public void Eine_Einnahme_mit_negativem_Betrag_wird_abgelehnt()
    {
        var ergebnis = RecurringExpenseValidator.Validate(
            Eingabe() with { AmountText = "-15,00", IsIncome = true });

        Assert.False(ergebnis.IsValid);
        Assert.Equal("Eine Einnahme darf keinen negativen Betrag haben.", ergebnis.AmountError);
    }

    [Fact]
    public void Betrag_mit_Tausendertrennzeichen_wird_abgelehnt()
    {
        // Money.TryParseEuroText lehnt den Punkt bewusst ab, damit "12.50"
        // nicht still zu 1250 wird.
        var ergebnis = RecurringExpenseValidator.Validate(Eingabe() with { AmountText = "1.234,56" });

        Assert.NotNull(ergebnis.AmountError);
    }

    [Fact]
    public void Unbekannte_Intervalleinheit_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringExpenseValidator.Validate(Eingabe() with { IntervalUnit = "fortnight" }));
    }

    private static RecurringExpenseInput Eingabe() => new()
    {
        Title = "Stallmiete",
        CategoryId = 1,
        PayerId = 2,
        AmountText = "42,50",
        IntervalUnit = "month",
        IntervalCountText = "1",
        AnchorDayText = "15",
        StartDateText = "15.01.2026",
        EndDateText = null,
    };
}
