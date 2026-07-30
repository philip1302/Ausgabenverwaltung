using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

public class ReportCsvTests
{
    // Eine kleine Tabelle von Hand, damit der erwartete Text vollstaendig
    // im Test steht: Wohnen (Januar und Maerz) mit der Unterkategorie
    // Strom (nur Maerz), Februar bleibt leer.
    private static ReportMatrix BaueTestMatrix()
    {
        var strom = new ReportMatrixRow
        {
            CategoryId = 2,
            Name = "Strom",
            FullPath = "Wohnen › Strom",
            Depth = 1,
            IsArchived = false,
            Cells = new Dictionary<string, ReportAmount>
            {
                ["2026-03"] = new(3000, 1),
            },
            Total = new ReportAmount(3000, 1),
            Children = Array.Empty<ReportMatrixRow>(),
        };

        var wohnen = new ReportMatrixRow
        {
            CategoryId = 1,
            Name = "Wohnen",
            FullPath = "Wohnen",
            Depth = 0,
            IsArchived = false,
            Cells = new Dictionary<string, ReportAmount>
            {
                ["2026-01"] = new(120050, 1),
                ["2026-03"] = new(3000, 1),
            },
            Total = new ReportAmount(123050, 2),
            Children = new[] { strom },
        };

        return new ReportMatrix
        {
            Grouping = ReportGrouping.Month,
            PeriodKeys = new[] { "2026-01", "2026-02", "2026-03" },
            Rows = new[] { wohnen },
            ColumnTotals = new Dictionary<string, ReportAmount>
            {
                ["2026-01"] = new(120050, 1),
                ["2026-03"] = new(3000, 1),
            },
            Total = new ReportAmount(123050, 2),
        };
    }

    [Fact]
    public void Export_bildet_Kopf_Zeilen_und_Summenzeile_ab()
    {
        var matrix = BaueTestMatrix();
        var sichtbar = new[] { matrix.Rows[0], matrix.Rows[0].Children[0] };

        var csv = ReportCsv.Build(matrix, sichtbar);

        Assert.Equal(
            "\"Kategorie\";\"Jan 2026\";\"Feb 2026\";\"Mär 2026\";\"Summe\"\r\n" +
            "\"Wohnen\";1200,50;;30,00;1230,50\r\n" +
            "\"    Strom\";;;30,00;30,00\r\n" +
            "\"Summe\";1200,50;;30,00;1230,50\r\n",
            csv);
    }

    // Der Export bildet ab, was auf dem Bildschirm steht - eine
    // eingeklappte Unterkategorie fehlt darin, die Summenzeile bleibt
    // trotzdem die der ganzen Auswertung.
    [Fact]
    public void Eingeklappte_Unterkategorien_fehlen_im_Export()
    {
        var matrix = BaueTestMatrix();

        var csv = ReportCsv.Build(matrix, new[] { matrix.Rows[0] });

        Assert.DoesNotContain("Strom", csv);
        Assert.Contains("\"Summe\";1200,50;;30,00;1230,50", csv);
    }

    // Semikolon trennt, Komma trennt die Dezimalstellen - so oeffnet Excel
    // im deutschen Gebietsschema die Datei direkt.
    [Fact]
    public void Betraege_verwenden_Komma_und_keinen_Tausenderpunkt()
    {
        var matrix = BaueTestMatrix();

        var csv = ReportCsv.Build(matrix, new[] { matrix.Rows[0] });

        Assert.Contains("1200,50", csv);
        Assert.DoesNotContain("1.200", csv);
    }

    [Fact]
    public void Negative_Betraege_behalten_ihr_Vorzeichen()
    {
        var zeile = new ReportMatrixRow
        {
            CategoryId = 1,
            Name = "Erstattung",
            FullPath = "Erstattung",
            Depth = 0,
            IsArchived = false,
            Cells = new Dictionary<string, ReportAmount> { ["2026"] = new(-4250, 1) },
            Total = new ReportAmount(-4250, 1),
            Children = Array.Empty<ReportMatrixRow>(),
        };

        var matrix = new ReportMatrix
        {
            Grouping = ReportGrouping.Year,
            PeriodKeys = new[] { "2026" },
            Rows = new[] { zeile },
            ColumnTotals = new Dictionary<string, ReportAmount> { ["2026"] = new(-4250, 1) },
            Total = new ReportAmount(-4250, 1),
        };

        Assert.Contains("\"Erstattung\";-42,50;-42,50", ReportCsv.Build(matrix, new[] { zeile }));
    }

    // Ein Semikolon im Kategorienamen darf die Zeile nicht zerlegen.
    [Fact]
    public void Trenn_und_Anfuehrungszeichen_im_Namen_werden_maskiert()
    {
        var zeile = new ReportMatrixRow
        {
            CategoryId = 1,
            Name = "Strom; \"gross\"",
            FullPath = "Strom; \"gross\"",
            Depth = 0,
            IsArchived = false,
            Cells = new Dictionary<string, ReportAmount> { ["2026"] = new(100, 1) },
            Total = new ReportAmount(100, 1),
            Children = Array.Empty<ReportMatrixRow>(),
        };

        var matrix = new ReportMatrix
        {
            Grouping = ReportGrouping.Year,
            PeriodKeys = new[] { "2026" },
            Rows = new[] { zeile },
            ColumnTotals = new Dictionary<string, ReportAmount> { ["2026"] = new(100, 1) },
            Total = new ReportAmount(100, 1),
        };

        Assert.Contains("\"Strom; \"\"gross\"\"\";1,00;1,00", ReportCsv.Build(matrix, new[] { zeile }));
    }

    [Fact]
    public void Auswertung_ohne_Treffer_liefert_Kopf_und_Summenzeile()
    {
        var csv = ReportCsv.Build(
            ReportMatrix.Empty(ReportGrouping.Month), Array.Empty<ReportMatrixRow>());

        Assert.Equal("\"Kategorie\";\"Summe\"\r\n\"Summe\";\r\n", csv);
    }
}
