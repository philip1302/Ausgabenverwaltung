using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Tests;

public class MonthlyBurdenTests
{
    private static readonly DateOnly Heute = new(2026, 7, 30);

    [Fact]
    public void Monatliche_Vorlage_zaehlt_mit_ihrem_vollen_Betrag()
    {
        var vorlage = Vorlage(50_00, "month", 1);

        Assert.Equal(50_00, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Jaehrliche_Vorlage_zaehlt_mit_einem_Zwoelftel()
    {
        // 1.200 EUR im Jahr sind 100 EUR im Monat.
        var vorlage = Vorlage(1200_00, "year", 1);

        Assert.Equal(100_00, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Quartalsvorlage_zaehlt_mit_einem_Drittel()
    {
        var vorlage = Vorlage(300_00, "month", 3);

        Assert.Equal(100_00, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Woechentliche_Vorlage_wird_mit_der_mittleren_Monatslaenge_umgerechnet()
    {
        // 10 EUR pro Woche: 365,25 / 12 / 7 = 4,348214... Wochen je Monat,
        // also 43,48 EUR - nicht 40 EUR wie bei "vier Wochen = ein Monat".
        var vorlage = Vorlage(10_00, "week", 1);

        Assert.Equal(43_48, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Taegliche_Vorlage_wird_mit_der_mittleren_Monatslaenge_umgerechnet()
    {
        // 1 EUR taeglich: 30,4375 Tage je Monat -> 30,44 EUR.
        var vorlage = Vorlage(1_00, "day", 1);

        Assert.Equal(30_44, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Inaktive_Vorlagen_zaehlen_nicht()
    {
        var aktiv = Vorlage(50_00, "month", 1);
        var inaktiv = Vorlage(999_00, "month", 1);
        inaktiv.IsActive = false;

        Assert.Equal(50_00, MonthlyBurden.TotalPerMonthCents([aktiv, inaktiv], Heute));
    }

    [Fact]
    public void Abgelaufene_Vorlagen_zaehlen_nicht()
    {
        var laufend = Vorlage(50_00, "month", 1);

        var abgelaufen = Vorlage(999_00, "month", 1);
        abgelaufen.EndDate = Heute.AddDays(-1);

        Assert.Equal(50_00, MonthlyBurden.TotalPerMonthCents([laufend, abgelaufen], Heute));
    }

    [Fact]
    public void Eine_am_Stichtag_endende_Vorlage_zaehlt_noch_mit()
    {
        // Das Enddatum ist einschliessend gemeint - am letzten Tag laeuft
        // die Vorlage noch.
        var vorlage = Vorlage(50_00, "month", 1);
        vorlage.EndDate = Heute;

        Assert.Equal(50_00, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Kuenftig_beginnende_Vorlagen_zaehlen_mit()
    {
        // Sie sind eine beschlossene, nur noch nicht angelaufene Belastung.
        var vorlage = Vorlage(50_00, "month", 1);
        vorlage.StartDate = Heute.AddMonths(2);

        Assert.Equal(50_00, MonthlyBurden.TotalPerMonthCents([vorlage], Heute));
    }

    [Fact]
    public void Die_Summe_wird_erst_am_Ende_gerundet()
    {
        // Drei Vorlagen zu je 10 EUR im Quartal ergeben je 3,3333... EUR
        // im Monat. Wuerde je Vorlage gerundet (3,33), kaeme 9,99 heraus;
        // richtig ist die einmal gerundete Summe von 10,00.
        var vorlagen = new[]
        {
            Vorlage(10_00, "month", 3),
            Vorlage(10_00, "month", 3),
            Vorlage(10_00, "month", 3),
        };

        Assert.Equal(10_00, MonthlyBurden.TotalPerMonthCents(vorlagen, Heute));
    }

    [Fact]
    public void Ohne_Vorlagen_ist_die_Belastung_null()
    {
        Assert.Equal(0, MonthlyBurden.TotalPerMonthCents([], Heute));
    }

    private static RecurringExpense Vorlage(long amountCents, string intervalUnit, int intervalCount) => new()
    {
        Id = 1,
        CategoryId = 1,
        PayerId = 1,
        AmountCents = amountCents,
        Title = "Test",
        IntervalUnit = intervalUnit,
        IntervalCount = intervalCount,
        AnchorDay = 1,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = null,
        GeneratedThrough = null,
        IsActive = true,
        CreatedUtc = DateTime.UnixEpoch,
        ModifiedUtc = DateTime.UnixEpoch,
    };
}
