using System.Globalization;
using Ausgabenverwaltung.Core;
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
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    public ReportZelle(
        ReportAmount betrag, int? kategorieId, string? periodenKey, string beschreibung)
    {
        HatWerte = betrag.HasValues;

        // Zeitabschnitte ohne Buchung zeigen einen Bindestrich statt
        // "0,00" - sonst waere nicht unterscheidbar, ob nichts gebucht
        // wurde oder sich Ausgabe und Erstattung aufgehoben haben.
        Text = betrag.HasValues
            ? Money.ToDecimal(betrag.SumCents).ToString("N2", DeDe)
            : "–";

        KategorieId = kategorieId;
        PeriodenKey = periodenKey;
        Beschreibung = beschreibung;
    }

    public string Text { get; }

    /// <summary>Ob in der Zelle Buchungen liegen - nur dann ist sie anklickbar.</summary>
    public bool HatWerte { get; }

    /// <summary>NULL = alle Kategorien (Summenzeile).</summary>
    public int? KategorieId { get; }

    /// <summary>NULL = ganzer Zeitraum (Summenspalte).</summary>
    public string? PeriodenKey { get; }

    /// <summary>Ueberschrift des Dialogs mit den Einzelbuchungen.</summary>
    public string Beschreibung { get; }
}
