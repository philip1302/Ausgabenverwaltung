using System.Globalization;
using System.Text;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die angezeigte Kreuztabelle als CSV-Text: Semikolon als Trennzeichen,
/// Komma als Dezimaltrennzeichen. So oeffnet Excel im deutschen
/// Gebietsschema die Datei mit einem Doppelklick, ohne Import-Assistent.
///
/// Erzeugt nur den Text; geschrieben wird die Datei in der Oberflaeche
/// (Regel 7). Dort gehoert auch die UTF-8-Signatur hin - ohne sie zeigt
/// Excel Umlaute falsch an.
/// </summary>
public static class ReportCsv
{
    private const string Separator = ";";
    private const string LineBreak = "\r\n";

    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    /// <param name="visibleRows">
    /// Die Zeilen in genau der Reihenfolge, in der sie gerade auf dem
    /// Bildschirm stehen. Eingeklappte Unterkategorien fehlen darin - der
    /// Export bildet also die aufgeklappte Struktur ab und nicht den
    /// ganzen Baum. Die Einrueckung wandert als fuehrende Leerzeichen in
    /// die erste Spalte.
    /// </param>
    public static string Build(ReportMatrix matrix, IReadOnlyList<ReportMatrixRow> visibleRows)
    {
        var text = new StringBuilder();

        var kopf = new List<string> { Quote("Kategorie") };
        foreach (var key in matrix.PeriodKeys)
        {
            kopf.Add(Quote(ReportPeriods.Label(key, matrix.Grouping)));
        }

        kopf.Add(Quote("Summe"));
        AppendLine(text, kopf);

        foreach (var row in visibleRows)
        {
            var felder = new List<string>
            {
                Quote(new string(' ', row.Depth * 4) + row.Name),
            };

            foreach (var key in matrix.PeriodKeys)
            {
                felder.Add(Amount(row.Cell(key)));
            }

            felder.Add(Amount(row.Total));
            AppendLine(text, felder);
        }

        var summen = new List<string> { Quote("Summe") };
        foreach (var key in matrix.PeriodKeys)
        {
            summen.Add(Amount(matrix.ColumnTotal(key)));
        }

        summen.Add(Amount(matrix.Total));
        AppendLine(text, summen);

        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, IReadOnlyList<string> fields)
    {
        text.Append(string.Join(Separator, fields));
        text.Append(LineBreak);
    }

    // Zeitabschnitte ohne Buchung bleiben LEER, statt wie in der Anzeige
    // einen Bindestrich zu bekommen: Excel liest "-" als Text und bricht
    // damit jede Formel, die ueber die Spalte rechnet.
    // Ohne Tausenderpunkt, weil der je nach Excel-Einstellung als
    // Trennzeichen missverstanden werden kann.
    private static string Amount(ReportAmount amount) =>
        amount.HasValues
            ? Money.ToDecimal(amount.SumCents).ToString("0.00", DeDe)
            : string.Empty;

    // Textfelder stehen immer in Anfuehrungszeichen - sonst wirft Excel
    // die fuehrenden Leerzeichen der Einrueckung weg, und ein Semikolon
    // im Kategorienamen zerlegte die Zeile.
    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
