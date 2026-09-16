using System.Globalization;
using System.Text;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die angezeigten Zahlen als CSV-Text: Semikolon als Trennzeichen, Komma
/// als Dezimaltrennzeichen. So oeffnet Excel im deutschen Gebietsschema die
/// Datei mit einem Doppelklick, ohne Import-Assistent.
///
/// Drei Ausgaben, dieselbe Bauform: <see cref="Build"/> fuer die
/// Kreuztabelle der Auswertung, <see cref="BuildExpenseList"/> fuer die
/// flache Buchungsliste und <see cref="BuildYearComparison"/> fuer den
/// Jahresrueckblick. Alle drei teilen Trennzeichen, Zeilenende und
/// Maskierung - eine zweite Datei daneben hiesse zwei Sorten CSV aus
/// derselben Anwendung. Deshalb steht die dritte hier, obwohl sie einen
/// Typ aus dem Jahresrueckblick entgegennimmt.
///
/// Erzeugt nur den Text; geschrieben wird die Datei in der Oberflaeche
/// (Regel 7). Dort gehoert auch die UTF-8-Signatur hin - ohne sie zeigt
/// Excel Umlaute falsch an.
/// </summary>
public static class ReportCsv
{
    private const string Separator = ";";
    private const string LineBreak = "\r\n";


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
        kopf.Add(Quote("Durchschnitt je Zeitabschnitt"));
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
            felder.Add(AverageAmount(row.Total, matrix.PeriodKeys.Count));
            AppendLine(text, felder);
        }

        var summen = new List<string> { Quote("Summe") };
        foreach (var key in matrix.PeriodKeys)
        {
            summen.Add(Amount(matrix.ColumnTotal(key)));
        }

        summen.Add(Amount(matrix.Total));
        summen.Add(AverageAmount(matrix.Total, matrix.PeriodKeys.Count));
        AppendLine(text, summen);

        return text.ToString();
    }

    /// <summary>
    /// Die Gegenueberstellung zweier Jahre aus dem Jahresrueckblick.
    ///
    /// Die Spalte "Unterschied" traegt hier - anders als die Saetze auf der
    /// Seite - ein VORZEICHEN: positiv heisst mehr ausgegeben. In der
    /// Anzeige steht die Richtung im Wort, weil sie gelesen wird; in einer
    /// Tabellendatei wird sie gerechnet, und dafuer braucht Excel eine
    /// Zahl.
    /// </summary>
    /// <param name="visibleRows">
    /// Die Zeilen in der Reihenfolge, in der sie auf dem Bildschirm
    /// stehen - eingeklappte Unterkategorien fehlen, wie bei
    /// <see cref="Build"/>.
    /// </param>
    public static string BuildYearComparison(
        ReviewComparison comparison, IReadOnlyList<ReviewCategoryChange> visibleRows)
    {
        var text = new StringBuilder();

        AppendLine(text, new List<string>
        {
            Quote("Kategorie"),
            Quote(comparison.Periods.PreviousLabel),
            Quote(comparison.Periods.CurrentLabel),
            Quote("Unterschied"),
            Quote("Anteil in %"),
        });

        foreach (var row in visibleRows)
        {
            AppendLine(text, new List<string>
            {
                Quote(new string(' ', row.Depth * 4) + row.Name),
                Jahreswert(row.PreviousCents, row.PreviousCount),
                Jahreswert(row.CurrentCents, row.CurrentCount),
                EuroText.Plain(row.DeltaCents),
                Anteil(row.CurrentCents, comparison.CurrentTotalCents),
            });
        }

        AppendLine(text, new List<string>
        {
            Quote("Summe"),
            Jahreswert(comparison.PreviousTotalCents, comparison.PreviousTotalCount),
            Jahreswert(comparison.CurrentTotalCents, comparison.CurrentTotalCount),
            EuroText.Plain(comparison.DeltaCents),
            Anteil(comparison.CurrentTotalCents, comparison.CurrentTotalCents),
        });

        return text.ToString();
    }

    /// <summary>
    /// Die flache Buchungsliste als CSV - eine Zeile je Buchung, in der
    /// Reihenfolge, in der sie gerade auf dem Bildschirm stehen. Exportiert
    /// wird also, was gefiltert ist, und nicht alles: die Filterleiste ist
    /// die Auswahl, und ein Export, der sie uebergeht, ueberrascht.
    /// </summary>
    /// <param name="items">
    /// Die angezeigten Buchungen. Bewusst die Kernobjekte und nicht die
    /// Anzeigezeilen: dort stehen fertig formatierte Texte, und ein
    /// €-Zeichen im CSV machte die Spalte in Excel zu Text.
    /// </param>
    public static string BuildExpenseList(IReadOnlyList<ExpenseListItem> items)
    {
        var text = new StringBuilder();

        AppendLine(text, [
            Quote("Datum"),
            Quote("Art"),
            Quote("Betrag"),
            Quote("Kategorie"),
            Quote("Zahler"),
            Quote("Beglichen am"),
            Quote("Bemerkung"),
            Quote("Vorlage"),
        ]);

        foreach (var item in items)
        {
            AppendLine(text, [
                Datum(item.ExpenseDate),
                Quote(item.IsIncome ? "Einnahme" : "Ausgabe"),
                Betrag(item),
                Quote(item.CategoryFullPath),
                Quote(item.PayerName),
                item.SettledDate is DateOnly beglichen ? Datum(beglichen) : string.Empty,
                Quote(item.Note ?? string.Empty),
                Quote(item.RecurringExpenseTitle ?? string.Empty),
            ]);
        }

        return text.ToString();
    }

    // Das Vorzeichen kommt vom Buchungstyp, nicht vom gespeicherten Wert:
    // Ausgabe negativ, Einnahme positiv. AmountCents ist immer positiv
    // (Regel 1), Math.Abs faengt zusaetzlich Alt-Datensaetze aus der Zeit
    // vor dieser Regel ab - dieselbe Rechnung wie in EuroText.FormatSigned.
    //
    // Wer die Spalte in Excel summiert, bekommt deshalb nicht zwangslaeufig
    // die Summe unter der Liste: die zaehlt eine noch OFFENE Einnahme mit
    // null, weil das Geld noch nicht geflossen ist. Hier steht stattdessen
    // ihr Betrag - eine Zeile, die ihren eigenen Betrag verschweigt, waere
    // im Export wertlos. Die Spalte "Beglichen am" sagt, welche es sind.
    private static string Betrag(ExpenseListItem item)
    {
        var vorzeichenbehaftet = item.IsIncome
            ? Math.Abs(item.AmountCents)
            : -Math.Abs(item.AmountCents);

        return EuroText.Plain(vorzeichenbehaftet);
    }

    // Datum als "07.08.2026" und nicht im Speicherformat: die Datei liest
    // ein Mensch in einem deutschen Excel, nicht die Datenbank.
    // Unquotiert, damit Excel eine Datumsspalte daraus macht.
    private static string Datum(DateOnly datum) => Kultur.Datum(datum);

    private static void AppendLine(StringBuilder text, IReadOnlyList<string> fields)
    {
        text.Append(string.Join(Separator, fields));
        text.Append(LineBreak);
    }

    // Zeitabschnitte ohne Buchung bleiben LEER, statt wie in der Anzeige
    // einen Bindestrich zu bekommen: Excel liest "-" als Text und bricht
    // damit jede Formel, die ueber die Spalte rechnet.
    //
    // EuroText.Plain und nicht EuroText.Format: ein €-Zeichen machte die
    // Spalte zu Text, und der Tausenderpunkt kann je nach
    // Excel-Einstellung als Trennzeichen missverstanden werden.
    private static string Amount(ReportAmount amount) =>
        amount.HasValues
            ? EuroText.Plain(amount.SumCents)
            : string.Empty;

    // Dieselbe Leer-statt-Bindestrich-Regel wie bei Amount(): Excel soll
    // hier ebenfalls rechnen koennen statt an einem Textzeichen zu
    // scheitern.
    private static string AverageAmount(ReportAmount total, int periodCount) =>
        total.HasValues
            ? EuroText.Plain(total.AveragePerPeriod(periodCount))
            : string.Empty;

    // Dieselbe Leer-statt-Bindestrich-Regel wie bei Amount(): ein Jahr
    // ohne eine einzige Buchung bleibt leer. Die ANZAHL entscheidet und
    // nicht die Summe - eine Kategorie, in der sich Ausgabe und Erstattung
    // aufheben, hat null Euro und war trotzdem in Gebrauch.
    private static string Jahreswert(long cents, int count) =>
        count > 0 ? EuroText.Plain(cents) : string.Empty;

    // Als blanke Zahl ohne Prozentzeichen, damit Excel eine Zahlenspalte
    // daraus macht; worum es sich handelt, steht in der Kopfzeile.
    private static string Anteil(long partCents, long totalCents) =>
        totalCents == 0
            ? string.Empty
            : (partCents * 100m / totalCents).ToString("0.00", Kultur.DeDe);

    // Textfelder stehen immer in Anfuehrungszeichen - sonst wirft Excel
    // die fuehrenden Leerzeichen der Einrueckung weg, und ein Semikolon
    // im Kategorienamen zerlegte die Zeile.
    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
