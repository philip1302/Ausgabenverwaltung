using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Tests;

public class RecurrenceGeneratorTests
{
    [Fact]
    public void AnchorDay_29_wird_in_Nicht_Schaltjahren_gekuerzt_und_kehrt_im_naechsten_Schaltjahr_zurueck()
    {
        // StartDate liegt auf einem 29. Februar (Schaltjahr 2024). Bei
        // jaehrlichem Intervall gibt es den 29. nur in Schaltjahren -
        // dazwischen muss auf den 28. gekuerzt werden, OHNE dass das
        // die spaeteren Jahre (Regel 5: StartDate + n * Intervall)
        // beeinflusst: 2028 ist wieder ein Schaltjahr und muss wieder
        // exakt auf den 29. treffen, nicht auf den 28.
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2024, 2, 29),
            endDate: null,
            intervalUnit: "year",
            intervalCount: 1,
            anchorDay: 29,
            generatedThrough: null,
            through: new DateOnly(2028, 3, 1));

        Assert.Equal(
            new[]
            {
                new DateOnly(2024, 2, 29),
                new DateOnly(2025, 2, 28),
                new DateOnly(2026, 2, 28),
                new DateOnly(2027, 2, 28),
                new DateOnly(2028, 2, 29),
            },
            occurrences);
    }

    [Fact]
    public void AnchorDay_31_driftet_ueber_Februar_April_und_Mai_hinweg_nicht()
    {
        // Waere die Berechnung "letztes Vorkommen + 1 Monat" statt
        // "StartDate + n * Intervall", wuerde aus dem gekuerzten
        // 28. Februar im Maerz faelschlich der 28. statt der 31. werden.
        // Der Test belegt, dass jedes Vorkommen unabhaengig aus
        // StartDate neu berechnet wird.
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 1, 31),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 31,
            generatedThrough: null,
            through: new DateOnly(2026, 5, 31));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28), // 2026 ist kein Schaltjahr
                new DateOnly(2026, 3, 31), // kein Drift: wieder der 31., nicht der 28. + 1 Monat
                new DateOnly(2026, 4, 30),
                new DateOnly(2026, 5, 31),
            },
            occurrences);
    }

    [Fact]
    public void Bereits_generierte_Vorkommen_werden_bei_gleichem_Cutoff_nicht_erneut_geliefert()
    {
        var erstesEreignis = new DateOnly(2026, 3, 1);
        var through = new DateOnly(2026, 3, 1);

        var ersterLauf = RecurrenceGenerator.GetDueOccurrences(
            startDate: erstesEreignis,
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            generatedThrough: null,
            through: through);

        // Zweiter Aufruf mit demselben Cutoff, aber generatedThrough
        // bereits auf den Cutoff fortgeschrieben (so wie es der
        // Aufrufer nach dem ersten Lauf tun muss).
        var zweiterLauf = RecurrenceGenerator.GetDueOccurrences(
            startDate: erstesEreignis,
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            generatedThrough: through,
            through: through);

        Assert.Single(ersterLauf);
        Assert.Empty(zweiterLauf);
    }

    [Fact]
    public void EndDate_stoppt_die_Erzeugung_dauerhaft_auch_bei_weit_spaeterem_Cutoff()
    {
        // Die Vorlage endet 2026-06-01. Ein Cutoff weit danach darf
        // keine Vorkommen nach dem EndDate liefern.
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 6, 1),
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            generatedThrough: null,
            through: new DateOnly(2030, 1, 1));

        Assert.Equal(new DateOnly(2026, 6, 1), occurrences[^1]);
        Assert.All(occurrences, o => Assert.True(o <= new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void Quartalsintervall_ueberspringt_den_Jahreswechsel_korrekt()
    {
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2025, 11, 15),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 3,
            anchorDay: 15,
            generatedThrough: null,
            through: new DateOnly(2026, 6, 1));

        Assert.Equal(
            new[]
            {
                new DateOnly(2025, 11, 15),
                new DateOnly(2026, 2, 15),
                new DateOnly(2026, 5, 15),
            },
            occurrences);
    }
}
