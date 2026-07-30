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
    public void Wochenintervall_rechnet_in_Siebenertagesschritten()
    {
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 2, 23),
            endDate: null,
            intervalUnit: "week",
            intervalCount: 2,
            anchorDay: null,
            generatedThrough: null,
            through: new DateOnly(2026, 4, 6));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 2, 23),
                new DateOnly(2026, 3, 9),
                new DateOnly(2026, 3, 23),
                new DateOnly(2026, 4, 6),
            },
            occurrences);
    }

    [Fact]
    public void Tagesintervall_rechnet_in_Tagesschritten()
    {
        var occurrences = RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 2, 26),
            endDate: null,
            intervalUnit: "day",
            intervalCount: 1,
            anchorDay: null,
            generatedThrough: null,
            through: new DateOnly(2026, 3, 2));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 2, 26),
                new DateOnly(2026, 2, 27),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 1), // 2026 ist kein Schaltjahr
                new DateOnly(2026, 3, 2),
            },
            occurrences);
    }

    [Fact]
    public void Unbekannte_IntervalUnit_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            intervalUnit: "fortnight",
            intervalCount: 1,
            anchorDay: null,
            generatedThrough: null,
            through: new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void IntervalCount_null_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceGenerator.GetDueOccurrences(
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 0,
            anchorDay: null,
            generatedThrough: null,
            through: new DateOnly(2026, 12, 31)));
    }

    // ---------------------------------------------------------------
    // Vorschau
    // ---------------------------------------------------------------

    [Fact]
    public void GetNextOccurrences_liefert_genau_die_gewuenschte_Anzahl_ab_dem_Stichtag()
    {
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 15,
            from: new DateOnly(2026, 7, 30),
            maxCount: 6);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 8, 15),
                new DateOnly(2026, 9, 15),
                new DateOnly(2026, 10, 15),
                new DateOnly(2026, 11, 15),
                new DateOnly(2026, 12, 15),
                new DateOnly(2027, 1, 15),
            },
            termine);
    }

    [Fact]
    public void GetNextOccurrences_schliesst_den_Stichtag_selbst_ein()
    {
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 15,
            from: new DateOnly(2026, 8, 15),
            maxCount: 1);

        Assert.Equal(new DateOnly(2026, 8, 15), Assert.Single(termine));
    }

    [Fact]
    public void GetNextOccurrences_beginnt_beim_Startdatum_wenn_der_Stichtag_davor_liegt()
    {
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2027, 3, 1),
            endDate: null,
            intervalUnit: "year",
            intervalCount: 1,
            anchorDay: 1,
            from: new DateOnly(2026, 7, 30),
            maxCount: 2);

        Assert.Equal(
            new[] { new DateOnly(2027, 3, 1), new DateOnly(2028, 3, 1) },
            termine);
    }

    [Fact]
    public void GetNextOccurrences_endet_vorzeitig_am_Enddatum()
    {
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 10, 1),
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            from: new DateOnly(2026, 7, 30),
            maxCount: 6);

        // Nur noch August, September und Oktober liegen in der Laufzeit.
        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 10, 1),
            },
            termine);
    }

    [Fact]
    public void GetNextOccurrences_zeigt_die_echten_Termine_unabhaengig_vom_Erzeugungsstand()
    {
        // Die Vorschau soll den Rhythmus zeigen, nicht was noch offen ist -
        // deshalb kennt sie GeneratedThrough gar nicht. Waere das anders
        // geloest, wuerde die Vorschau einer laufenden Vorlage plotzlich
        // leer aussehen.
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2026, 1, 1),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            from: new DateOnly(2026, 1, 1),
            maxCount: 3);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 3, 1),
            },
            termine);
    }

    [Fact]
    public void GetNextOccurrences_kuerzt_den_Ankertag_wie_GetDueOccurrences()
    {
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(2026, 1, 31),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 31,
            from: new DateOnly(2026, 2, 1),
            maxCount: 4);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 4, 30),
                new DateOnly(2026, 5, 31),
            },
            termine);
    }

    [Fact]
    public void GetNextDueDate_liefert_den_naechsten_Termin()
    {
        var naechster = RecurrenceGenerator.GetNextDueDate(
            startDate: new DateOnly(2026, 1, 15),
            endDate: null,
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 15,
            from: new DateOnly(2026, 7, 30));

        Assert.Equal(new DateOnly(2026, 8, 15), naechster);
    }

    [Fact]
    public void GetNextDueDate_liefert_null_nach_Ablauf_des_Enddatums()
    {
        var naechster = RecurrenceGenerator.GetNextDueDate(
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 6, 1),
            intervalUnit: "month",
            intervalCount: 1,
            anchorDay: 1,
            from: new DateOnly(2026, 7, 30));

        Assert.Null(naechster);
    }

    [Fact]
    public void GetNextOccurrences_bleibt_bei_unbefristeter_Vorlage_endlich()
    {
        // Ohne Enddatum ist die Reihe unendlich. Der Aufrufer begrenzt sie
        // ueber maxCount - dass das wirklich terminiert, sichert dieser
        // Test ab (sonst haengt die Vorschau beim Tippen).
        var termine = RecurrenceGenerator.GetNextOccurrences(
            startDate: new DateOnly(1990, 1, 1),
            endDate: null,
            intervalUnit: "day",
            intervalCount: 1,
            anchorDay: null,
            from: new DateOnly(2026, 7, 30),
            maxCount: 3);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 7, 30),
                new DateOnly(2026, 7, 31),
                new DateOnly(2026, 8, 1),
            },
            termine);
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
