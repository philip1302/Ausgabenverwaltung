using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.YearInReview;
using Avalonia.Media;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine sichtbare Zeile der Gegenueberstellung. Unveraenderlich und bei
/// jedem Auf- und Zuklappen neu erzeugt - dieselbe Bauweise wie
/// <see cref="ReportZeile"/>.
///
/// Bewusst NICHT <see cref="ReportZeile"/> wiederverwendet: die bildet n
/// Zeitabschnittsspalten samt Durchschnitt ab, der Rueckblick hat vier
/// feste Spalten. Vor allem braucht die Unterschiedsspalte eine
/// Richtungsangabe ("mehr"/"weniger"), die <see cref="ReportZelle"/> nicht
/// kennt - dort bedeutet ein positiver Wert "Einnahme". Ein zweiter Modus
/// in ReportZeile machte die Auswertung schwerer lesbar und spart nichts.
/// </summary>
public sealed class RueckblickZeile
{
    private RueckblickZeile(
        ReviewCategoryChange? quelle,
        string name,
        int tiefe,
        bool istArchiviert,
        bool hatKinder,
        bool istAufgeklappt,
        bool istSummenZeile,
        long vorjahrCents,
        int vorjahrAnzahl,
        long jahrCents,
        int jahrAnzahl,
        long jahresSummeCents,
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

        // Ein Jahr ohne eine einzige Buchung bekommt einen Bindestrich und
        // keine Null - dieselbe Unterscheidung wie in der Kreuztabelle: ein
        // Betrag von 0,00 € kann bedeuten, dass sich Ausgabe und Erstattung
        // aufheben, und das ist etwas anderes als "nichts gebucht".
        VorjahrText = vorjahrAnzahl > 0 ? EuroText.Format(vorjahrCents) : "–";
        JahrText = jahrAnzahl > 0 ? EuroText.Format(jahrCents) : "–";

        var unterschied = jahrCents - vorjahrCents;
        DifferenzText = ReviewText.Veraenderung(unterschied);
        DifferenzIstMehr = unterschied > 0;
        DifferenzIstWeniger = unterschied < 0;

        AnteilText = jahresSummeCents == 0 || jahrAnzahl == 0
            ? "–"
            : ReviewText.AnteilText(ReviewMath.Share(jahrCents, jahresSummeCents));
    }

    /// <summary>
    /// Die zugrunde liegende Zeile; NULL bei der Summenzeile. Der
    /// CSV-Export gibt genau diese Zeilen weiter, damit er dieselbe
    /// aufgeklappte Struktur abbildet wie die Anzeige - wie bei
    /// <see cref="ReportZeile.Quelle"/>.
    /// </summary>
    public ReviewCategoryChange? Quelle { get; }

    public string Name { get; }

    /// <summary>Kategoriefarbe als Punkt vor dem Namen, als Zusatz zum Text (Regel 10).</summary>
    public IBrush Farbe { get; }

    public bool ZeigtFarbe => !IstSummenZeile;

    public double EinzugBreite { get; }

    public bool IstArchiviert { get; }
    public bool HatKinder { get; }
    public bool IstAufgeklappt { get; }

    public string AufklappZeichen => IstAufgeklappt ? "▾" : "▸";

    public bool IstSummenZeile { get; }

    public string VorjahrText { get; }
    public string JahrText { get; }

    /// <summary>Der Unterschied in Worten - nie mit einem Minuszeichen davor.</summary>
    public string DifferenzText { get; }

    public bool DifferenzIstMehr { get; }
    public bool DifferenzIstWeniger { get; }

    /// <summary>Anteil an den Ausgaben des Jahres, z. B. "18 %".</summary>
    public string AnteilText { get; }

    /// <summary>Nur Kategoriezeilen fuehren in die Ausgabenliste.</summary>
    public bool Anklickbar => Quelle is not null;

    public static RueckblickZeile FuerKategorie(
        ReviewCategoryChange quelle,
        long jahresSummeCents,
        bool hatKinder,
        bool istAufgeklappt,
        string farbe) =>
        new(quelle,
            quelle.Name,
            quelle.Depth,
            quelle.IsArchived,
            hatKinder,
            istAufgeklappt,
            istSummenZeile: false,
            quelle.PreviousCents,
            quelle.PreviousCount,
            quelle.CurrentCents,
            quelle.CurrentCount,
            jahresSummeCents,
            Farbpinsel.Fuer(farbe));

    /// <summary>
    /// Die Summenzeile unten. Sie zaehlt IMMER alles, auch die gerade
    /// eingeklappten Zeilen - sie beschreibt den Rueckblick, nicht den
    /// Bildschirm.
    /// </summary>
    public static RueckblickZeile FuerSumme(ReviewComparison vergleich) =>
        new(quelle: null,
            "Summe",
            tiefe: 0,
            istArchiviert: false,
            hatKinder: false,
            istAufgeklappt: false,
            istSummenZeile: true,
            vergleich.PreviousTotalCents,
            vergleich.PreviousTotalCount,
            vergleich.CurrentTotalCents,
            vergleich.CurrentTotalCount,
            vergleich.CurrentTotalCents,
            Brushes.Transparent);
}
