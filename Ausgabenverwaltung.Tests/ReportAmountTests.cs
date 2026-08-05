using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Reine Rechenlogik ohne Datenbank - insbesondere die Rundung des
/// Durchschnitts, die einzige Stelle in <see cref="ReportAmount"/>, an der
/// tatsaechlich gerechnet statt nur addiert wird.
/// </summary>
public class ReportAmountTests
{
    [Fact]
    public void AveragePerPeriod_teilt_die_Summe_glatt_auf()
    {
        var betrag = new ReportAmount(3000, 1);

        Assert.Equal(1000, betrag.AveragePerPeriod(3));
    }

    [Fact]
    public void AveragePerPeriod_rundet_kaufmaennisch_auf_den_naechsten_Cent()
    {
        // 1230,50 EUR / 3 Zeitabschnitte = 410,1666...66 EUR -> 410,17 EUR.
        var betrag = new ReportAmount(123050, 2);

        Assert.Equal(41017, betrag.AveragePerPeriod(3));
    }

    [Fact]
    public void AveragePerPeriod_ohne_Buchungen_ist_null()
    {
        Assert.Equal(0, ReportAmount.Empty.AveragePerPeriod(3));
    }

    [Fact]
    public void AveragePerPeriod_ohne_Zeitabschnitte_ist_null()
    {
        var betrag = new ReportAmount(3000, 1);

        Assert.Equal(0, betrag.AveragePerPeriod(0));
    }

    [Fact]
    public void AveragePerPeriod_behaelt_das_Vorzeichen_einer_Erstattung()
    {
        var betrag = new ReportAmount(-4250, 1);

        Assert.Equal(-4250, betrag.AveragePerPeriod(1));
    }
}
