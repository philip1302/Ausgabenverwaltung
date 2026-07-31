using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Pruefung des Ausgabenformulars - dieselbe fuer die
/// Erfassungsmaske und den Bearbeiten-Dialog.
/// </summary>
public class ExpenseValidatorTests
{
    private static readonly DateOnly Heute = new(2026, 7, 31);

    private static ExpenseInput Eingabe() => new()
    {
        AmountText = "12,50",
        CategoryId = 1,
        PayerId = 1,
        DateText = "31.07.2026",
        Today = Heute,
    };

    [Fact]
    public void Eine_vollstaendige_Eingabe_ist_gueltig()
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe());

        Assert.True(ergebnis.IsValid);
        Assert.False(ergebnis.NeedsConfirmation);
        Assert.Equal(1250L, ergebnis.AmountCents);
        Assert.Equal(new DateOnly(2026, 7, 31), ergebnis.Date);
    }

    // ================= Betrag =================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12.50")]   // Punkt bewusst abgelehnt - sonst wuerde aus 12.50 still 1250
    [InlineData("12,,5")]
    public void Ein_ungueltiger_Betrag_wird_bemaengelt(string text)
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { AmountText = text });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.AmountError);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0,00")]
    [InlineData("-0,00")]
    public void Ein_Betrag_von_null_wird_abgelehnt(string text)
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { AmountText = text });

        Assert.False(ergebnis.IsValid);
        Assert.Contains("null", ergebnis.AmountError);
    }

    [Fact]
    public void Ein_negativer_Betrag_bleibt_erlaubt()
    {
        // So werden Erstattungen erfasst.
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { AmountText = "-15,00" });

        Assert.True(ergebnis.IsValid);
        Assert.Equal(-1500L, ergebnis.AmountCents);
    }

    // ================= Pflichtfelder =================

    [Fact]
    public void Eine_fehlende_Kategorie_wird_bemaengelt()
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { CategoryId = null });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.CategoryError);
    }

    [Fact]
    public void Ein_fehlender_Zahler_wird_bemaengelt()
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { PayerId = null });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.PayerError);
    }

    [Fact]
    public void Alle_Fehler_erscheinen_gleichzeitig()
    {
        // Sonst muesste der Anwender mehrfach hintereinander speichern, um
        // alle Beanstandungen zu sehen.
        var ergebnis = ExpenseValidator.Validate(new ExpenseInput
        {
            AmountText = "keine Zahl",
            CategoryId = null,
            PayerId = null,
            DateText = "kein Datum",
            Today = Heute,
        });

        Assert.NotNull(ergebnis.AmountError);
        Assert.NotNull(ergebnis.CategoryError);
        Assert.NotNull(ergebnis.PayerError);
        Assert.NotNull(ergebnis.DateError);
    }

    // ================= Datum =================

    [Theory]
    [InlineData("")]
    [InlineData("31.13.2026")]
    [InlineData("32.07.2026")]
    [InlineData("2026-07-31")]  // ISO gehoert in die Datenbank, nicht ins Formular
    public void Ein_ungueltiges_Datum_wird_bemaengelt(string text)
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { DateText = text });

        Assert.False(ergebnis.IsValid);
        Assert.NotNull(ergebnis.DateError);
    }

    [Fact]
    public void Ein_Tippfehler_im_Jahr_wird_abgelehnt()
    {
        // Der Fall aus der Aufgabenstellung: Jahr 200 statt 2026.
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { DateText = "31.07.0200" });

        Assert.False(ergebnis.IsValid);
        Assert.Contains("200", ergebnis.DateError);

        // Kein Nachfragen zusaetzlich zum Fehler - sonst staenden zwei
        // Meldungen an einem Feld.
        Assert.Null(ergebnis.DateConfirmation);
    }

    [Fact]
    public void Ein_weit_zurueckliegendes_Datum_wird_nicht_verboten_sondern_nachgefragt()
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { DateText = "31.07.2010" });

        // Ausdruecklich gueltig: wer alte Belege nachtraegt, hat gute
        // Gruende dafuer.
        Assert.True(ergebnis.IsValid);
        Assert.True(ergebnis.NeedsConfirmation);
        Assert.Contains("zurück", ergebnis.DateConfirmation);
    }

    [Fact]
    public void Ein_Datum_weit_in_der_Zukunft_wird_nachgefragt()
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { DateText = "31.07.2030" });

        Assert.True(ergebnis.IsValid);
        Assert.True(ergebnis.NeedsConfirmation);
        Assert.Contains("Zukunft", ergebnis.DateConfirmation);
    }

    [Theory]
    [InlineData("31.07.2025")]   // ein Jahr zurueck
    [InlineData("01.01.2017")]   // gut neun Jahre zurueck
    [InlineData("31.12.2026")]   // knapp in der Zukunft
    public void Gewoehnliche_Datumsangaben_werden_nicht_nachgefragt(string text)
    {
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { DateText = text });

        Assert.True(ergebnis.IsValid);
        Assert.False(ergebnis.NeedsConfirmation);
    }

    // ================= Plausibilitaet fuer sich =================

    [Theory]
    [InlineData(1899)]
    [InlineData(200)]
    [InlineData(2201)]
    public void Unsinnige_Jahreszahlen_werden_abgelehnt(int jahr)
    {
        Assert.NotNull(DatePlausibility.Error(new DateOnly(jahr, 1, 1)));
    }

    [Theory]
    [InlineData(1900)]
    [InlineData(2026)]
    [InlineData(2200)]
    public void Jahreszahlen_im_Rahmen_sind_in_Ordnung(int jahr)
    {
        Assert.Null(DatePlausibility.Error(new DateOnly(jahr, 1, 1)));
    }

    [Fact]
    public void Zu_einem_abgelehnten_Datum_gibt_es_keine_Rueckfrage()
    {
        Assert.Null(DatePlausibility.Confirmation(new DateOnly(200, 1, 1), Heute));
    }
}
