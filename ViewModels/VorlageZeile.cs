using System;
using System.Globalization;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Vorlagenliste: fertige Anzeigewerte zur Vorlage, dazu der
/// volle Kategoriepfad, der Zahlername und die Anzahl bereits erzeugter
/// Buchungen. Die Vorlage selbst bleibt erhalten, weil der
/// Bearbeiten-Dialog und die Rueckstandsrechnung sie brauchen.
///
/// Rhythmustext und naechste Faelligkeit kommen aus Core
/// (<see cref="RecurrenceText"/>, <see cref="RecurrenceGenerator"/>) - hier
/// wird nur formatiert (Regel 7).
/// </summary>
public sealed class VorlageZeile
{
    public VorlageZeile(
        RecurringExpense vorlage,
        string categoryFullPath,
        string payerName,
        int erzeugteAnzahl,
        DateOnly heute)
    {
        Vorlage = vorlage;
        CategoryFullPath = categoryFullPath;
        PayerName = payerName;
        ErzeugteAnzahl = erzeugteAnzahl;

        BetragText = Money.ToDecimal(vorlage.AmountCents)
            .ToString("N2", CultureInfo.GetCultureInfo("de-DE"));

        RhythmusText = RecurrenceText.Describe(
            vorlage.IntervalUnit, vorlage.IntervalCount, vorlage.AnchorDay, vorlage.StartDate);

        IstAbgelaufen = vorlage.EndDate is DateOnly ende && ende < heute;

        StatusText = !vorlage.IsActive
            ? "inaktiv"
            : IstAbgelaufen
                ? "abgelaufen"
                : "aktiv";

        // Eine stillgelegte Vorlage hat keine naechste Faelligkeit - sie
        // erzeugt nichts mehr, egal was der Kalender sagt.
        var naechste = vorlage.IsActive
            ? RecurrenceGenerator.GetNextDueDate(
                vorlage.StartDate, vorlage.EndDate, vorlage.IntervalUnit,
                vorlage.IntervalCount, vorlage.AnchorDay, heute)
            : null;

        NaechsteFaelligkeitText = naechste is DateOnly termin
            ? GermanDateInput.ToText(termin)
            : "—";

        ErzeugtText = erzeugteAnzahl == 0 ? "—" : erzeugteAnzahl.ToString(CultureInfo.InvariantCulture);
    }

    public RecurringExpense Vorlage { get; }

    public int Id => Vorlage.Id;
    public string Titel => Vorlage.Title;
    public bool IstAktiv => Vorlage.IsActive;

    public string CategoryFullPath { get; }
    public string PayerName { get; }
    public string BetragText { get; }
    public string RhythmusText { get; }
    public string NaechsteFaelligkeitText { get; }
    public string StatusText { get; }
    public bool IstAbgelaufen { get; }

    public int ErzeugteAnzahl { get; }
    public string ErzeugtText { get; }

    /// <summary>Der Sprung in die Ausgabenliste lohnt nur, wenn es etwas zu zeigen gibt.</summary>
    public bool HatErzeugteBuchungen => ErzeugteAnzahl > 0;

    /// <summary>
    /// Beschreibung fuer die Loesch-Sicherheitsabfrage: Titel, Kategorie,
    /// Betrag und Rhythmus, damit erkennbar bleibt, was verschwindet.
    /// </summary>
    public string LoeschBeschreibung =>
        $"{Titel} · {CategoryFullPath} · {BetragText} EUR · {RhythmusText}";
}
