using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zelle der Kreuztabelle. Traegt neben dem Anzeigetext genau die
/// beiden Angaben, die der Sprung in die Einzelbuchungen braucht:
/// Kategorie und Zeitabschnitt. NULL heisst dabei jeweils "keine
/// Einschraenkung" - so beschreiben Summenzeile und Summenspalte sich mit
/// derselben Zelle wie eine gewoehnliche Kreuzung.
/// </summary>
public sealed class ReportZelle
{
    public ReportZelle(
        ReportAmount betrag,
        int? kategorieId,
        string? periodenKey,
        string beschreibung,
        int stufe = 0)
    {
        HatWerte = betrag.HasValues;

        // Zeitabschnitte ohne Buchung zeigen einen Bindestrich statt
        // "0,00" - sonst waere nicht unterscheidbar, ob nichts gebucht
        // wurde oder sich Ausgabe und Einnahme aufgehoben haben.
        Text = betrag.HasValues
            ? EuroText.Format(betrag.SumCents)
            : "–";

        IstEinnahme = betrag.HasValues && EuroText.IsPositive(betrag.SumCents);

        KategorieId = kategorieId;
        PeriodenKey = periodenKey;
        Beschreibung = beschreibung;

        Stufe = stufe;
        IstStufe1 = stufe == 1;
        IstStufe2 = stufe == 2;
        IstStufe3 = stufe == 3;
        IstStufe4 = stufe == 4;
    }

    public string Text { get; }

    /// <summary>Ob in der Zelle Buchungen liegen - nur dann ist sie anklickbar.</summary>
    public bool HatWerte { get; }

    /// <summary>
    /// Einnahmenueberhang: die Zelle summiert sich auf einen positiven
    /// Betrag. Wird gruen hervorgehoben. Ein Ausgabenueberhang (negativer
    /// Betrag) bleibt dagegen neutral - bei einer Summe zaehlt nur noch
    /// das Vorzeichen, kein einzelner Zahler oder Beglichen-Status mehr.
    /// </summary>
    public bool IstEinnahme { get; }

    /// <summary>
    /// Wie schwer die Zelle im Vergleich zu den uebrigen wiegt: 1 bis 4,
    /// oder 0 fuer "nicht eingefaerbt". Die Stufe kommt aus
    /// <see cref="Ausgabenverwaltung.Core.Charts.Intensity"/> und ist nach
    /// RANG vergeben, nicht linear nach Betrag.
    ///
    /// Stufe 0 haben: leere Zellen, Zellen mit Einnahmenueberhang (sie
    /// tragen bereits die gruene Auszeichnung, zwei Farbsysteme in einer
    /// Zelle machen beide unlesbar), die Summenzeile und die Summenspalte
    /// (Rechnungen ueber die Zellen, sie laegen zwangslaeufig ganz oben) -
    /// und alle Zellen, solange der Anwender die Einfaerbung abgeschaltet
    /// hat.
    ///
    /// Die Farbe waehlt die Ansicht ueber die vier Merkmale darunter; das
    /// ViewModel kennt keine Farbwerte, sonst waeren sie nicht mehr
    /// themenabhaengig (Muster von DiagrammBalken).
    /// </summary>
    public int Stufe { get; }

    public bool IstStufe1 { get; }

    public bool IstStufe2 { get; }

    public bool IstStufe3 { get; }

    public bool IstStufe4 { get; }

    /// <summary>NULL = alle Kategorien (Summenzeile).</summary>
    public int? KategorieId { get; }

    /// <summary>NULL = ganzer Zeitraum (Summenspalte).</summary>
    public string? PeriodenKey { get; }

    /// <summary>Ueberschrift des Dialogs mit den Einzelbuchungen.</summary>
    public string Beschreibung { get; }
}
