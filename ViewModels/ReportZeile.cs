using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.Reports;

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
        ReportZelle summe)
    {
        Quelle = quelle;
        Name = name;
        EinzugBreite = tiefe * 16;
        IstArchiviert = istArchiviert;
        HatKinder = hatKinder;
        IstAufgeklappt = istAufgeklappt;
        IstSummenZeile = istSummenZeile;
        Zellen = zellen;
        Summe = summe;
    }

    /// <summary>
    /// Die zugrunde liegende Auswertungszeile; NULL bei der Summenzeile.
    /// Der CSV-Export gibt genau diese Zeilen weiter, damit er dieselbe
    /// aufgeklappte Struktur abbildet wie die Anzeige.
    /// </summary>
    public ReportMatrixRow? Quelle { get; }

    public string Name { get; }

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

    public static ReportZeile FuerKategorie(
        ReportMatrixRow row,
        IReadOnlyList<ReportSpalte> spalten,
        bool hatKinder,
        bool istAufgeklappt)
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
            hatKinder, istAufgeklappt, istSummenZeile: false, zellen, summe);
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
            hatKinder: false, istAufgeklappt: false, istSummenZeile: true, zellen, summe);
    }
}
