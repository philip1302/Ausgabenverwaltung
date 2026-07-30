using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

public class ReportPeriodsTests
{
    // Die Schluessel muessen zeichengenau dem entsprechen, was
    // ReportRepository in SQL erzeugt - sonst finden die Spalten der
    // Kreuztabelle ihre Werte nicht wieder.
    [Theory]
    [InlineData(2026, 3, 17, ReportGrouping.Year, "2026")]
    [InlineData(2026, 3, 17, ReportGrouping.Quarter, "2026-Q1")]
    [InlineData(2026, 3, 17, ReportGrouping.Month, "2026-03")]
    [InlineData(2026, 12, 31, ReportGrouping.Quarter, "2026-Q4")]
    [InlineData(2026, 10, 1, ReportGrouping.Quarter, "2026-Q4")]
    [InlineData(2026, 9, 30, ReportGrouping.Quarter, "2026-Q3")]
    public void Key_bildet_die_Formate_der_Abfrage_nach(
        int jahr, int monat, int tag, ReportGrouping gruppierung, string erwartet)
    {
        Assert.Equal(erwartet, ReportPeriods.Key(new DateOnly(jahr, monat, tag), gruppierung));
    }

    // Alle drei Formate sortieren alphabetisch = chronologisch. Darauf
    // beruht die Ermittlung des fruehesten und spaetesten Abschnitts.
    [Fact]
    public void Schluessel_sortieren_alphabetisch_chronologisch()
    {
        var monate = new[] { "2026-10", "2026-02", "2025-12" };
        var quartale = new[] { "2026-Q4", "2026-Q1", "2025-Q3" };

        Assert.Equal(
            new[] { "2025-12", "2026-02", "2026-10" },
            monate.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "2025-Q3", "2026-Q1", "2026-Q4" },
            quartale.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Enumerate_laesst_leere_Zeitabschnitte_dazwischen_stehen()
    {
        var keys = ReportPeriods.Enumerate("2026-01", "2026-05", ReportGrouping.Month);

        Assert.Equal(
            new[] { "2026-01", "2026-02", "2026-03", "2026-04", "2026-05" },
            keys);
    }

    [Fact]
    public void Enumerate_zaehlt_Monate_ueber_den_Jahreswechsel()
    {
        var keys = ReportPeriods.Enumerate("2025-11", "2026-02", ReportGrouping.Month);

        Assert.Equal(new[] { "2025-11", "2025-12", "2026-01", "2026-02" }, keys);
    }

    [Fact]
    public void Enumerate_zaehlt_Quartale_ueber_den_Jahreswechsel()
    {
        var keys = ReportPeriods.Enumerate("2025-Q3", "2026-Q2", ReportGrouping.Quarter);

        Assert.Equal(new[] { "2025-Q3", "2025-Q4", "2026-Q1", "2026-Q2" }, keys);
    }

    [Fact]
    public void Enumerate_liefert_bei_gleichem_Anfang_und_Ende_genau_eine_Spalte()
    {
        Assert.Equal(
            new[] { "2026" },
            ReportPeriods.Enumerate("2026", "2026", ReportGrouping.Year));
    }

    [Fact]
    public void Enumerate_liefert_nichts_wenn_das_Ende_vor_dem_Anfang_liegt()
    {
        Assert.Empty(ReportPeriods.Enumerate("2026-05", "2026-01", ReportGrouping.Month));
    }

    // Am aeussersten Kalenderrand darf die Schrittweite nicht ueberlaufen.
    [Fact]
    public void Enumerate_laeuft_am_hoechsten_Datum_nicht_ueber()
    {
        var keys = ReportPeriods.Enumerate("9999-10", "9999-12", ReportGrouping.Month);

        Assert.Equal(new[] { "9999-10", "9999-11", "9999-12" }, keys);
    }

    [Theory]
    [InlineData("2026", ReportGrouping.Year, "2026")]
    [InlineData("2026-Q3", ReportGrouping.Quarter, "Q3 2026")]
    [InlineData("2026-03", ReportGrouping.Month, "Mär 2026")]
    [InlineData("2026-12", ReportGrouping.Month, "Dez 2026")]
    public void Label_beschriftet_den_Spaltenkopf(
        string key, ReportGrouping gruppierung, string erwartet)
    {
        Assert.Equal(erwartet, ReportPeriods.Label(key, gruppierung));
    }

    // Der Zeitraum eines Abschnitts geht direkt als Filtergrenze in den
    // Sprung zu den Einzelbuchungen - unten einschliessend, oben
    // ausschliessend, genau wie ReportFilter.From/To.
    [Fact]
    public void Range_liefert_die_Grenzen_eines_Quartals()
    {
        var range = ReportPeriods.Range("2026-Q2", ReportGrouping.Quarter);

        Assert.Equal(new DateOnly(2026, 4, 1), range.From);
        Assert.Equal(new DateOnly(2026, 7, 1), range.ToExclusive);
    }

    [Fact]
    public void Range_liefert_die_Grenzen_eines_Monats_und_eines_Jahres()
    {
        var monat = ReportPeriods.Range("2026-12", ReportGrouping.Month);
        var jahr = ReportPeriods.Range("2026", ReportGrouping.Year);

        Assert.Equal(new DateOnly(2026, 12, 1), monat.From);
        Assert.Equal(new DateOnly(2027, 1, 1), monat.ToExclusive);

        Assert.Equal(new DateOnly(2026, 1, 1), jahr.From);
        Assert.Equal(new DateOnly(2027, 1, 1), jahr.ToExclusive);
    }

    [Fact]
    public void Start_lehnt_einen_Schluessel_der_falschen_Gruppierung_ab()
    {
        Assert.Throws<FormatException>(
            () => ReportPeriods.Start("2026-03", ReportGrouping.Quarter));
        Assert.Throws<FormatException>(
            () => ReportPeriods.Start("2026", ReportGrouping.Month));
    }
}
