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
    public void Ein_negativer_Betrag_wird_abgelehnt()
    {
        // Erstattung als eigenes Konzept entfaellt - ein tatsaechlicher
        // Rueckfluss wird als Einnahme erfasst, das Vorzeichen kommt beim
        // Anzeigen ausschliesslich vom Typ (siehe EuroText.FormatSigned).
        var ergebnis = ExpenseValidator.Validate(Eingabe() with { AmountText = "-15,00" });

        Assert.False(ergebnis.IsValid);
        Assert.Equal("Der Betrag darf nicht negativ sein.", ergebnis.AmountError);
    }

    [Fact]
    public void Eine_Einnahme_mit_positivem_Betrag_ist_gueltig()
    {
        var ergebnis = ExpenseValidator.Validate(
            Eingabe() with { AmountText = "300,00", IsIncome = true });

        Assert.True(ergebnis.IsValid);
        Assert.Equal(30000L, ergebnis.AmountCents);
    }

    [Fact]
    public void Eine_Einnahme_mit_negativem_Betrag_wird_abgelehnt()
    {
        // Gilt fuer Einnahmen wie Ausgaben gleichermassen - siehe
        // Ein_negativer_Betrag_wird_abgelehnt.
        var ergebnis = ExpenseValidator.Validate(
            Eingabe() with { AmountText = "-15,00", IsIncome = true });

        Assert.False(ergebnis.IsValid);
        Assert.Equal("Der Betrag darf nicht negativ sein.", ergebnis.AmountError);
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
    public void Eine_Einnahme_mit_der_eigenen_Person_als_Zahler_wird_bemaengelt()
    {
        // Eine Einnahme kommt immer von jemand anderem - sonst liesse sich
        // ueber die Offene-Posten-Liste nie verfolgen, ob sie tatsaechlich
        // eingegangen ist (Regel 4).
        var ergebnis = ExpenseValidator.Validate(
            Eingabe() with { IsIncome = true, PayerIsSelf = true });

        Assert.False(ergebnis.IsValid);
        Assert.Equal(
            "Eine Einnahme braucht einen Zahler, der nicht die eigene Person ist.",
            ergebnis.PayerError);
    }

    [Fact]
    public void Eine_Einnahme_mit_fremdem_Zahler_ist_gueltig()
    {
        var ergebnis = ExpenseValidator.Validate(
            Eingabe() with { IsIncome = true, PayerIsSelf = false });

        Assert.Null(ergebnis.PayerError);
    }

    [Fact]
    public void Ein_fehlender_Zahler_geht_bei_einer_Einnahme_vor_der_Selbst_Pruefung()
    {
        // PayerIsSelf ist bei PayerId == null nicht aussagekraeftig - die
        // "kein Zahler gewaehlt"-Meldung muss trotzdem erscheinen, nicht
        // die Einnahme-spezifische.
        var ergebnis = ExpenseValidator.Validate(
            Eingabe() with { IsIncome = true, PayerId = null });

        Assert.Equal("Bitte einen Zahler wählen.", ergebnis.PayerError);
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
