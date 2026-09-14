using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.YearInReview;
using Avalonia.Media;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Befund als Karte. Unveraenderlich; bei jedem Neuaufbau des
/// Rueckblicks werden die Karten neu erzeugt.
///
/// Traegt wie <see cref="DiagrammBalken"/> nur BOOLESCHE Kennzeichen und
/// keine Farbwerte - welche Farbe "mehr ausgegeben" bekommt, entscheidet
/// die Ansicht. Die Kategoriefarbe ist die einzige Ausnahme: sie kommt aus
/// der Palette und gehoert zur Kategorie, nicht zur Darstellung.
/// </summary>
public sealed class RueckblickKarte
{
    private RueckblickKarte(
        ReviewFinding befund,
        IBrush farbe,
        bool zeigtFarbe,
        ReviewComparisonPeriods zeitraeume)
    {
        Quelle = befund;
        Farbe = farbe;
        ZeigtFarbe = zeigtFarbe;

        Ueberschrift = ReviewText.Ueberschrift(befund.Kind);
        Gegenstand = befund.Subject;
        Satz = ReviewText.Satz(befund);
        Erklaerung = ReviewText.Erklaerung(befund, zeitraeume);
        BetragText = EuroText.Format(befund.CurrentCents);
        VeraenderungText = ReviewText.Veraenderung(befund.DeltaCents);
        ProzentText = ReviewText.ProzentText(befund.RelativeChange);
    }

    /// <summary>Der zugrunde liegende Befund - fuer den Sprung in die Liste.</summary>
    public ReviewFinding Quelle { get; }

    public string Ueberschrift { get; }

    /// <summary>Die Kategorie oder der Monat, um den es geht.</summary>
    public string Gegenstand { get; }

    /// <summary>Der ganze Satz - auch der zugaengliche Name der Karte.</summary>
    public string Satz { get; }

    /// <summary>
    /// Was hinter der Karte steckt: der Inhalt des Fensters, das am
    /// Fragezeichen aufgeht. Auf der Karte selbst stehen nur drei
    /// einzeilige Angaben - alles Weitere wuerde die Karten verschieden
    /// hoch machen (siehe <see cref="ReviewExplanation"/>).
    /// </summary>
    public ReviewExplanation Erklaerung { get; }

    public string BetragText { get; }
    public string VeraenderungText { get; }
    public string ProzentText { get; }

    /// <summary>
    /// Ob es mehr geworden ist. Bei "weggefallen" und beim ruhigsten Monat
    /// ist es weniger; die Ansicht faerbt danach, der Text sagt es ohnehin.
    /// </summary>
    public bool IstMehr => Quelle.DeltaCents > 0;

    /// <summary>Ob die Karte ueberhaupt eine Richtung hat.</summary>
    public bool ZeigtRichtung => Quelle.DeltaCents != 0;

    public IBrush Farbe { get; }

    /// <summary>Monatskarten gehoeren zu keiner Kategorie und tragen keinen Punkt.</summary>
    public bool ZeigtFarbe { get; }

    /// <summary>NULL bei den Monatskarten.</summary>
    public int? KategorieId => Quelle.CategoryId;

    public static RueckblickKarte Aus(
        ReviewFinding befund, string? farbe, ReviewComparisonPeriods zeitraeume) =>
        new(befund,
            farbe is null ? Brushes.Transparent : Farbpinsel.Fuer(farbe),
            zeigtFarbe: farbe is not null,
            zeitraeume);
}
