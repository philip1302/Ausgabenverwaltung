using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;
using Avalonia.Media;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine sichtbare Zeile der Kreuztabelle. Die Zeilen werden bei jedem
/// Auf- und Zuklappen neu erzeugt (siehe
/// <see cref="ReportViewModel.BaueZeilen"/>) - deshalb bewusst
/// unveraenderlich und ohne Benachrichtigungen.
/// </summary>
public sealed class ReportZeile
{
    private ReportZeile(
        ReportMatrixRow? quelle,
        string name,
        int tiefe,
        bool istArchiviert,
        bool hatKinder,
        bool istAufgeklappt,
        bool istSummenZeile,
        IReadOnlyList<ReportZelle> zellen,
        ReportZelle summe,
        ReportAmount summenBetrag,
        int periodenAnzahl,
        IBrush farbe)
    {
        Quelle = quelle;
        Name = name;
        Farbe = farbe;
        EinzugBreite = tiefe * 16;
        IstArchiviert = istArchiviert;
        HatKinder = hatKinder;
        IstAufgeklappt = istAufgeklappt;
        IstSummenZeile = istSummenZeile;
        Zellen = zellen;
        Summe = summe;

        // Durchschnitt je Zeitabschnitt statt einer weiteren anklickbaren
        // Zelle: er bildet keinen eigenen Ausschnitt der Buchungen ab
        // (den zeigt schon die Summenspalte), sondern nur deren Rechnung
        // ueber die angezeigten Zeitabschnitte - siehe
        // ReportAmount.AveragePerPeriod.
        if (summenBetrag.HasValues && periodenAnzahl > 0)
        {
            var cents = summenBetrag.AveragePerPeriod(periodenAnzahl);
            DurchschnittText = EuroText.Format(cents);
            DurchschnittIstAusgabe = EuroText.IsNegative(cents);
            DurchschnittIstEinnahme = EuroText.IsPositive(cents);
        }
        else
        {
            DurchschnittText = "–";
            DurchschnittIstAusgabe = false;
            DurchschnittIstEinnahme = false;
        }
    }

    /// <summary>
    /// Die zugrunde liegende Auswertungszeile; NULL bei der Summenzeile.
    /// Der CSV-Export gibt genau diese Zeilen weiter, damit er dieselbe
    /// aufgeklappte Struktur abbildet wie die Anzeige.
    /// </summary>
    public ReportMatrixRow? Quelle { get; }

    public string Name { get; }

    /// <summary>
    /// Die aufgeloeste Farbe der Kategorie als Punkt vor der
    /// Zeilenbeschriftung. Sie ergaenzt den Namen; die Zeile bleibt ohne
    /// Farbwahrnehmung vollstaendig lesbar. Die Summenzeile hat keine
    /// Kategorie und deshalb auch keinen Punkt
    /// (<see cref="ZeigtFarbe"/>).
    /// </summary>
    public IBrush Farbe { get; }

    public bool ZeigtFarbe => !IstSummenZeile;

    /// <summary>Einrueckung nach Baumtiefe, als Breite eines Platzhalters.</summary>
    public double EinzugBreite { get; }

    public bool IstArchiviert { get; }

    /// <summary>
    /// Ob unter dieser Zeile ueberhaupt etwas ANZUZEIGEN ist. Bezieht den
    /// Schalter "Kategorien ohne Ausgaben anzeigen" mit ein, damit kein
    /// Aufklapp-Pfeil erscheint, der nichts hervorbringt.
    /// </summary>
    public bool HatKinder { get; }

    public bool IstAufgeklappt { get; }

    public string AufklappZeichen => IstAufgeklappt ? "▾" : "▸";

    public bool IstSummenZeile { get; }

    /// <summary>Die Werte je Zeitabschnitt, in der Reihenfolge der Spalten.</summary>
    public IReadOnlyList<ReportZelle> Zellen { get; }

    /// <summary>Die Summenspalte rechts.</summary>
    public ReportZelle Summe { get; }

    /// <summary>
    /// Durchschnitt je Zeitabschnitt ganz rechts, neben der Summe: dieselbe
    /// Summe geteilt durch die Anzahl der angezeigten Zeitabschnitte. Ein
    /// Bindestrich statt "0,00", wenn die Zeile keine Buchung traegt oder
    /// gar keine Spalte angezeigt wird - dieselbe Ausnahme wie bei
    /// <see cref="ReportZelle"/>.
    /// </summary>
    public string DurchschnittText { get; }

    public bool DurchschnittIstAusgabe { get; }

    public bool DurchschnittIstEinnahme { get; }

    public static ReportZeile FuerKategorie(
        ReportMatrixRow row,
        IReadOnlyList<ReportSpalte> spalten,
        bool hatKinder,
        bool istAufgeklappt,
        string farbe)
    {
        var zellen = spalten
            .Select(spalte => new ReportZelle(
                row.Cell(spalte.Key),
                row.CategoryId,
                spalte.Key,
                $"{row.FullPath} · {spalte.Beschriftung}"))
            .ToList();

        var summe = new ReportZelle(
            row.Total, row.CategoryId, null, $"{row.FullPath} · gesamter Zeitraum");

        return new ReportZeile(
            row, row.Name, row.Depth, row.IsArchived,
            hatKinder, istAufgeklappt, istSummenZeile: false, zellen, summe,
            row.Total, spalten.Count,
            Farbpinsel.Fuer(farbe));
    }

    /// <summary>
    /// Die Summenzeile unten. Sie summiert IMMER alle Kategorien, auch die
    /// gerade eingeklappten oder ausgeblendeten - sie beschreibt die
    /// Auswertung, nicht den Bildschirm.
    /// </summary>
    public static ReportZeile FuerSumme(
        ReportMatrix matrix, IReadOnlyList<ReportSpalte> spalten)
    {
        var zellen = spalten
            .Select(spalte => new ReportZelle(
                matrix.ColumnTotal(spalte.Key),
                kategorieId: null,
                spalte.Key,
                $"Alle Kategorien · {spalte.Beschriftung}"))
            .ToList();

        var summe = new ReportZelle(
            matrix.Total, kategorieId: null, periodenKey: null,
            "Alle Kategorien · gesamter Zeitraum");

        return new ReportZeile(
            quelle: null, "Summe", tiefe: 0, istArchiviert: false,
            hatKinder: false, istAufgeklappt: false, istSummenZeile: true, zellen, summe,
            matrix.Total, spalten.Count,
            Brushes.Transparent);
    }
}
