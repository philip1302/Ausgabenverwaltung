using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Export der Gegenueberstellung. Dieselben Regeln wie beim uebrigen
/// CSV der Anwendung: kein Waehrungszeichen, leere Zelle statt Bindestrich,
/// Einrueckung als fuehrende Leerzeichen.
/// </summary>
public class JahresrueckblickCsvTests
{
    private static readonly ReviewComparisonPeriods Zeitraeume =
        ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, new DateOnly(2026, 9, 13));

    private static ReviewCategoryChange Knoten(
        int id,
        string name,
        long vorjahr,
        long jahr,
        int tiefe = 0,
        int vorjahrAnzahl = 2,
        int jahrAnzahl = 2) =>
        new()
        {
            CategoryId = id,
            Name = name,
            FullPath = name,
            Depth = tiefe,
            IsArchived = false,
            PreviousCents = vorjahr,
            CurrentCents = jahr,
            PreviousCount = vorjahrAnzahl,
            CurrentCount = jahrAnzahl,
            Children = Array.Empty<ReviewCategoryChange>(),
        };

    private static ReviewComparison Vergleich(params ReviewCategoryChange[] zeilen) =>
        new(
            Zeitraeume,
            zeilen,
            PreviousTotalCents: zeilen.Sum(z => z.PreviousCents),
            CurrentTotalCents: zeilen.Sum(z => z.CurrentCents),
            PreviousTotalCount: zeilen.Sum(z => z.PreviousCount),
            CurrentTotalCount: zeilen.Sum(z => z.CurrentCount));

    private static string[] Zeilen(string csv) =>
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Die_Kopfzeile_nennt_beide_Jahre_und_den_Unterschied()
    {
        var vergleich = Vergleich(Knoten(1, "Wohnen", 100_000, 140_000));

        var kopf = Zeilen(ReportCsv.BuildYearComparison(vergleich, vergleich.Roots))[0];

        Assert.Equal(
            "\"Kategorie\";\"2025\";\"2026\";\"Unterschied\";\"Anteil in %\"", kopf);
    }

    [Fact]
    public void Betraege_stehen_ohne_Waehrungszeichen()
    {
        var vergleich = Vergleich(Knoten(1, "Wohnen", 100_000, 140_000));

        var csv = ReportCsv.BuildYearComparison(vergleich, vergleich.Roots);

        Assert.DoesNotContain("€", csv, StringComparison.Ordinal);
        Assert.Contains("1000,00;1400,00;400,00", csv, StringComparison.Ordinal);
    }

    /// <summary>
    /// Positiv heisst hier "mehr ausgegeben". In der Anzeige steht die
    /// Richtung im Wort, in der Tabellendatei muss Excel damit rechnen
    /// koennen.
    /// </summary>
    [Fact]
    public void Ein_Rueckgang_steht_im_Export_mit_Minuszeichen()
    {
        var vergleich = Vergleich(Knoten(1, "Reisen", 200_000, 120_000));

        var csv = ReportCsv.BuildYearComparison(vergleich, vergleich.Roots);

        Assert.Contains(";-800,00;", csv, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein Jahr ohne Buchung bleibt leer und bekommt keinen Bindestrich -
    /// Excel liest den als Text und bricht damit jede Formel.
    /// </summary>
    [Fact]
    public void Ein_Jahr_ohne_Buchung_bleibt_leer()
    {
        var vergleich = Vergleich(Knoten(1, "Leasing", 0, 240_000, vorjahrAnzahl: 0));

        var zeile = Zeilen(ReportCsv.BuildYearComparison(vergleich, vergleich.Roots))[1];

        Assert.Equal("\"Leasing\";;2400,00;2400,00;100,00", zeile);
    }

    [Fact]
    public void Die_Einrueckung_der_aufgeklappten_Struktur_bleibt_erhalten()
    {
        var vergleich = Vergleich(
            Knoten(1, "Wohnen", 100_000, 140_000),
            Knoten(2, "Strom", 40_000, 80_000, tiefe: 1));

        var zeilen = Zeilen(ReportCsv.BuildYearComparison(vergleich, vergleich.Roots));

        Assert.StartsWith("\"Wohnen\"", zeilen[1], StringComparison.Ordinal);
        Assert.StartsWith("\"    Strom\"", zeilen[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Unten_steht_die_Summe_beider_Jahre()
    {
        var vergleich = Vergleich(
            Knoten(1, "Wohnen", 100_000, 140_000),
            Knoten(2, "Freizeit", 50_000, 30_000));

        var zeilen = Zeilen(ReportCsv.BuildYearComparison(vergleich, vergleich.Roots));

        Assert.Equal("\"Summe\";1500,00;1700,00;200,00;100,00", zeilen[^1]);
    }
}
