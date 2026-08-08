using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Farblogik einzelner Buchungszeilen: Rot fuer offen, Gruen fuer eine
/// beglichene Einnahme, Blau fuer eine beglichene Ausgabe, neutral (keine
/// der drei Eigenschaften) fuer alles mit eigenem Zahler.
///
/// Reine Konstruktion ohne Datenbank - die drei geprueften Typen
/// (<see cref="AusgabeZeile"/>, <see cref="LetzteAusgabeZeile"/>,
/// <see cref="OffenerPostenZeile"/>) berechnen ihre Farbe ausschliesslich
/// aus dem uebergebenen DTO.
/// </summary>
public class BuchungsfarbenTests
{
    private static ExpenseListItem ListItem(
        bool isIncome, bool payerIsSelf, DateOnly? settledDate)
        => new()
        {
            Id = 1,
            ExpenseDate = new DateOnly(2026, 3, 10),
            CategoryId = 1,
            CategoryFullPath = "Test",
            AmountCents = 5000,
            IsIncome = isIncome,
            PayerId = 1,
            PayerName = "Jemand",
            PayerIsSelf = payerIsSelf,
            SettledDate = settledDate,
        };

    private static ExpenseOverview Overview(
        bool isIncome, bool payerIsSelf, DateOnly? settledDate)
        => new()
        {
            Id = 1,
            ExpenseDate = new DateOnly(2026, 3, 10),
            AmountCents = 5000,
            CategoryName = "Test",
            PayerName = "Jemand",
            IsIncome = isIncome,
            PayerIsSelf = payerIsSelf,
            SettledDate = settledDate,
        };

    private static OpenItem Item(bool isIncome, DateOnly? settledDate)
        => new()
        {
            Id = 1,
            ExpenseDate = new DateOnly(2026, 3, 10),
            AmountCents = 5000,
            IsIncome = isIncome,
            PayerId = 1,
            PayerName = "Jemand",
            CategoryFullPath = "Test",
            SettledDate = settledDate,
            TageOffen = 5,
        };

    // ================= AusgabeZeile =================

    [Fact]
    public void Eine_eigene_Ausgabe_ist_neutral()
    {
        var zeile = new AusgabeZeile(ListItem(isIncome: false, payerIsSelf: true, null), "#000000");

        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Eine_eigene_Einnahme_ist_ebenfalls_neutral()
    {
        var zeile = new AusgabeZeile(ListItem(isIncome: true, payerIsSelf: true, null), "#000000");

        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Eine_offene_fremde_Ausgabe_ist_rot()
    {
        var zeile = new AusgabeZeile(
            ListItem(isIncome: false, payerIsSelf: false, null), "#000000");

        Assert.True(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Eine_offene_fremde_Einnahme_ist_ebenfalls_rot()
    {
        var zeile = new AusgabeZeile(
            ListItem(isIncome: true, payerIsSelf: false, null), "#000000");

        Assert.True(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
    }

    [Fact]
    public void Eine_beglichene_fremde_Einnahme_ist_gruen()
    {
        var zeile = new AusgabeZeile(
            ListItem(isIncome: true, payerIsSelf: false, new DateOnly(2026, 3, 20)), "#000000");

        Assert.False(zeile.IstOffen);
        Assert.True(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Eine_beglichene_fremde_Ausgabe_ist_blau()
    {
        var zeile = new AusgabeZeile(
            ListItem(isIncome: false, payerIsSelf: false, new DateOnly(2026, 3, 20)), "#000000");

        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.True(zeile.IstBeglichenAusgabe);
    }

    /// <summary>
    /// Der Buchungstyp selbst bleibt unabhaengig von der Farbe erhalten -
    /// das Bearbeiten-Formular belegt seine Checkbox damit vor
    /// (AusgabeBearbeitenViewModel), unabhaengig davon, ob die Zeile
    /// gerade offen, beglichen oder eigen ist.
    /// </summary>
    [Fact]
    public void Der_Buchungstyp_bleibt_unabhaengig_vom_Beglichen_Status_erhalten()
    {
        var offen = new AusgabeZeile(ListItem(isIncome: true, payerIsSelf: false, null), "#000000");
        Assert.True(offen.IstEinnahme);

        var eigen = new AusgabeZeile(ListItem(isIncome: true, payerIsSelf: true, null), "#000000");
        Assert.True(eigen.IstEinnahme);
    }

    // ================= LetzteAusgabeZeile =================

    [Fact]
    public void Letzte_Ausgabe_eigene_Buchung_ist_neutral()
    {
        var zeile = new LetzteAusgabeZeile(Overview(isIncome: false, payerIsSelf: true, null));

        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Letzte_Ausgabe_offene_fremde_Buchung_ist_rot()
    {
        var zeile = new LetzteAusgabeZeile(Overview(isIncome: false, payerIsSelf: false, null));

        Assert.True(zeile.IstOffen);
    }

    [Fact]
    public void Letzte_Ausgabe_beglichene_fremde_Einnahme_ist_gruen()
    {
        var zeile = new LetzteAusgabeZeile(
            Overview(isIncome: true, payerIsSelf: false, new DateOnly(2026, 3, 20)));

        Assert.True(zeile.IstEinnahme);
        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Letzte_Ausgabe_beglichene_fremde_Ausgabe_ist_blau()
    {
        var zeile = new LetzteAusgabeZeile(
            Overview(isIncome: false, payerIsSelf: false, new DateOnly(2026, 3, 20)));

        Assert.True(zeile.IstBeglichenAusgabe);
        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstEinnahme);
    }

    // ================= OffenerPostenZeile =================
    //
    // Jede Zeile hier hat per Definition einen fremden Zahler (siehe
    // OpenItem) - die Unterscheidung "eigen/fremd" entfaellt deshalb.

    [Fact]
    public void Offener_Posten_noch_nicht_beglichen_ist_rot()
    {
        var zeile = new OffenerPostenZeile(Item(isIncome: false, null));

        Assert.True(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Offener_Posten_beglichene_Einnahme_ist_gruen()
    {
        var zeile = new OffenerPostenZeile(Item(isIncome: true, new DateOnly(2026, 3, 20)));

        Assert.False(zeile.IstOffen);
        Assert.True(zeile.IstBeglicheneEinnahme);
        Assert.False(zeile.IstBeglichenAusgabe);
    }

    [Fact]
    public void Offener_Posten_beglichene_Ausgabe_ist_blau()
    {
        var zeile = new OffenerPostenZeile(Item(isIncome: false, new DateOnly(2026, 3, 20)));

        Assert.False(zeile.IstOffen);
        Assert.False(zeile.IstBeglicheneEinnahme);
        Assert.True(zeile.IstBeglichenAusgabe);
    }

    /// <summary>
    /// Die Wortwahl von Knopf und Kurzhinweis haengt am Buchungstyp
    /// selbst, nicht an der Farbe - eine noch offene Einnahme heisst
    /// weiterhin "Erhalten", nicht "Abhaken".
    /// </summary>
    [Fact]
    public void Die_Knopfbeschriftung_richtet_sich_nach_dem_Buchungstyp_nicht_nach_der_Farbe()
    {
        var offeneEinnahme = new OffenerPostenZeile(Item(isIncome: true, null));

        Assert.True(offeneEinnahme.IstOffen);
        Assert.Equal("Erhalten", offeneEinnahme.AbhakenButtonText);
    }

    /// <summary>
    /// Die Form dieses Textes ist festgenagelt, weil die Aktionsspalte der
    /// Offene-Posten-Liste einen Platz FESTER Breite dafuer reserviert
    /// (Views/OffenePostenView.axaml). Wird der Text laenger - etwa
    /// "beglichen am Donnerstag, 05.08.2026" - passt er dort nicht mehr
    /// hinein und wird stillschweigend abgeschnitten. Der feste Platz ist
    /// noetig, damit der Knopf dahinter in jeder Zeile an derselben Stelle
    /// sitzt; wer diesen Text verlaengert, muss dort mitziehen.
    /// </summary>
    [Theory]
    [InlineData(false, "beglichen am 05.08.2026")]
    [InlineData(true, "erhalten am 05.08.2026")]
    public void Der_Beglichen_Text_bleibt_kurz_genug_fuer_seinen_Platz(
        bool istEinnahme, string erwartet)
    {
        var zeile = new OffenerPostenZeile(
            Item(isIncome: istEinnahme, new DateOnly(2026, 8, 5)));

        Assert.Equal(erwartet, zeile.BeglichenText);
    }
}
