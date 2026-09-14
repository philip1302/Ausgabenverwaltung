using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine der drei Kacheln ueber dem Jahresrueckblick. Unveraenderlich und
/// ohne Benachrichtigungen - bei jedem Neuaufbau wird sie neu erzeugt, wie
/// <see cref="ReportZeile"/>.
///
/// Traegt nur fertigen Text und ein Kennzeichen; welche Farbe eine
/// Verbesserung bekommt, entscheidet die Ansicht (Regel 10 - Farbe ist
/// Zusatz, nie Ersatz, und "unverändert" wird gar nicht hervorgehoben).
/// </summary>
public sealed class RueckblickKennzahl
{
    private RueckblickKennzahl(
        string beschriftung,
        string wertText,
        string vorjahrText,
        string veraenderungText,
        bool istVerbesserung,
        bool istVerschlechterung)
    {
        Beschriftung = beschriftung;
        WertText = wertText;
        VorjahrText = vorjahrText;
        VeraenderungText = veraenderungText;
        IstVerbesserung = istVerbesserung;
        IstVerschlechterung = istVerschlechterung;
    }

    public string Beschriftung { get; }

    /// <summary>Der Wert des Jahres, z. B. "24.310,00 €".</summary>
    public string WertText { get; }

    /// <summary>Der Vorjahreswert mit seiner Jahreszahl, z. B. "2025: 22.430,00 €".</summary>
    public string VorjahrText { get; }

    /// <summary>Die Veraenderung in Worten, z. B. "1.880,00 € mehr als 2025".</summary>
    public string VeraenderungText { get; }

    public bool IstVerbesserung { get; }

    /// <summary>
    /// Ausdruecklich nicht einfach die Verneinung von
    /// <see cref="IstVerbesserung"/>: eine unveraenderte Kennzahl ist
    /// weder das eine noch das andere und wird gar nicht hervorgehoben.
    /// </summary>
    public bool IstVerschlechterung { get; }

    public static RueckblickKennzahl Aus(ReviewMetric kennzahl, string vorjahrLabel)
    {
        var unveraendert = kennzahl.DeltaCents == 0;

        return new RueckblickKennzahl(
            beschriftung: ReviewText.KennzahlName(kennzahl.Kind),
            // Das Netto darf hier sein Vorzeichen behalten - es ist keine
            // Richtungsangabe, sondern der Wert selbst: ein Minus heisst,
            // dass mehr aus- als eingegangen ist.
            wertText: EuroText.Format(kennzahl.CurrentCents),
            vorjahrText: $"{vorjahrLabel}: {EuroText.Format(kennzahl.PreviousCents)}",
            veraenderungText: ReviewText.KennzahlVeraenderung(kennzahl, vorjahrLabel),
            istVerbesserung: !unveraendert && kennzahl.IstVerbesserung,
            istVerschlechterung: !unveraendert && !kennzahl.IstVerbesserung);
    }
}
