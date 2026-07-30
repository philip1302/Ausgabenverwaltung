using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Tests;

public class RecurrenceTextTests
{
    [Fact]
    public void Monatlich_nennt_den_Ankertag()
    {
        var text = RecurrenceText.Describe("month", 1, anchorDay: 15, new DateOnly(2026, 3, 15));

        Assert.Equal("monatlich am 15.", text);
    }

    [Fact]
    public void Drei_Monate_heissen_quartalsweise()
    {
        // "alle 3 Monate" waere zwar richtig, aber ueber eine
        // Quartalszahlung sagt das niemand.
        var text = RecurrenceText.Describe("month", 3, anchorDay: 1, new DateOnly(2026, 1, 1));

        Assert.Equal("quartalsweise am 1.", text);
    }

    [Fact]
    public void Sechs_Monate_heissen_halbjaehrlich()
    {
        var text = RecurrenceText.Describe("month", 6, anchorDay: 1, new DateOnly(2026, 1, 1));

        Assert.Equal("halbjährlich am 1.", text);
    }

    [Fact]
    public void Zwoelf_Monate_werden_als_jaehrlich_mit_Monat_beschrieben()
    {
        // Zwoelf Monate sind ein Jahr - und ein Jahrestermin braucht den
        // Monat, sonst weiss man nicht, wann er faellig ist.
        var text = RecurrenceText.Describe("month", 12, anchorDay: 3, new DateOnly(2026, 9, 3));

        Assert.Equal("jährlich am 3.9.", text);
    }

    [Fact]
    public void Jaehrlich_nennt_Tag_und_Monat_aus_dem_Startdatum()
    {
        var text = RecurrenceText.Describe("year", 1, anchorDay: 1, new DateOnly(2026, 3, 1));

        Assert.Equal("jährlich am 1.3.", text);
    }

    [Fact]
    public void Fehlender_Ankertag_faellt_auf_den_Tag_des_Startdatums_zurueck()
    {
        // Dieselbe Regel wie im RecurrenceGenerator: ohne AnchorDay gilt
        // der Tag des Startdatums.
        var text = RecurrenceText.Describe("month", 1, anchorDay: null, new DateOnly(2026, 5, 27));

        Assert.Equal("monatlich am 27.", text);
    }

    [Fact]
    public void Woechentlich_nennt_den_Wochentag_des_Startdatums()
    {
        // 2026-03-02 ist ein Montag.
        var text = RecurrenceText.Describe("week", 1, anchorDay: null, new DateOnly(2026, 3, 2));

        Assert.Equal("wöchentlich montags", text);
    }

    [Fact]
    public void Alle_zwei_Wochen_nennt_ebenfalls_den_Wochentag()
    {
        // 2026-03-06 ist ein Freitag.
        var text = RecurrenceText.Describe("week", 2, anchorDay: null, new DateOnly(2026, 3, 6));

        Assert.Equal("alle 2 Wochen freitags", text);
    }

    [Fact]
    public void Taeglich_und_mehrtaegig_werden_unterschieden()
    {
        var start = new DateOnly(2026, 1, 1);

        Assert.Equal("täglich", RecurrenceText.Describe("day", 1, null, start));
        Assert.Equal("alle 4 Tage", RecurrenceText.Describe("day", 4, null, start));
    }

    [Fact]
    public void Ungewoehnliche_Monatsvielfache_bekommen_die_allgemeine_Form()
    {
        var text = RecurrenceText.Describe("month", 4, anchorDay: 10, new DateOnly(2026, 1, 10));

        Assert.Equal("alle 4 Monate am 10.", text);
    }

    [Fact]
    public void Unbekannte_Einheit_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurrenceText.Describe("fortnight", 1, null, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Intervallanzahl_null_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurrenceText.Describe("month", 0, 1, new DateOnly(2026, 1, 1)));
    }
}
