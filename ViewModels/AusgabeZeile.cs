using System;
using System.Globalization;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Ausgabenliste: Anzeigewerte aus
/// <see cref="ExpenseListItem"/> plus der Auswahlzustand fuer das
/// Sammel-Loeschen. Bewusst getrennt von der Core-Entitaet, damit diese
/// keinen UI-Zustand tragen muss (Regel 7).
/// </summary>
public sealed partial class AusgabeZeile : ObservableObject
{
    public int Id { get; }
    public DateOnly ExpenseDate { get; }
    public string DatumText { get; }
    public int CategoryId { get; }
    public string CategoryFullPath { get; }
    public long AmountCents { get; }
    public string BetragText { get; }
    public int PayerId { get; }
    public string PayerName { get; }
    public bool PayerIsSelf { get; }
    public DateOnly? SettledDate { get; }
    public string? Note { get; }
    public int? RecurringExpenseId { get; }

    /// <summary>
    /// Aus einer Vorlage erzeugt - wird in der Tabelle mit einem Zeichen
    /// markiert und im Bearbeiten-Dialog erklaert (Regel 6: die Historie
    /// haengt nicht an der Vorlage).
    /// </summary>
    public bool IstAusVorlage { get; }

    public string VorlageHinweis { get; }

    /// <summary>
    /// Regel 4: SettledDate bedeutet nur in Kombination mit einem fremden
    /// Zahler etwas. Bei eigenen Ausgaben bleibt die Spalte deshalb leer
    /// statt "offen" zu behaupten.
    /// </summary>
    public string StatusText { get; }

    public bool IstOffen { get; }

    [ObservableProperty]
    private bool _istAusgewaehlt;

    public AusgabeZeile(ExpenseListItem item)
    {
        Id = item.Id;
        ExpenseDate = item.ExpenseDate;
        DatumText = GermanDateInput.ToText(item.ExpenseDate);
        CategoryId = item.CategoryId;
        CategoryFullPath = item.CategoryFullPath;
        AmountCents = item.AmountCents;
        BetragText = Money.ToDecimal(item.AmountCents)
            .ToString("N2", CultureInfo.GetCultureInfo("de-DE"));
        PayerId = item.PayerId;
        PayerName = item.PayerName;
        PayerIsSelf = item.PayerIsSelf;
        SettledDate = item.SettledDate;
        Note = item.Note;
        RecurringExpenseId = item.RecurringExpenseId;

        IstAusVorlage = item.RecurringExpenseId is not null;
        VorlageHinweis = item.RecurringExpenseTitle is { } titel
            ? $"Aus Vorlage \"{titel}\" erzeugt"
            : IstAusVorlage
                ? "Aus einer inzwischen geloeschten Vorlage erzeugt"
                : string.Empty;

        IstOffen = !item.PayerIsSelf && item.SettledDate is null;
        StatusText = item.PayerIsSelf
            ? "—"
            : item.SettledDate is DateOnly beglichen
                ? $"beglichen am {GermanDateInput.ToText(beglichen)}"
                : "offen";
    }

    /// <summary>
    /// Beschreibung der Zeile fuer die Loesch-Sicherheitsabfrage: Datum,
    /// Kategorie und Betrag, damit erkennbar bleibt, was verschwindet.
    /// </summary>
    public string LoeschBeschreibung =>
        $"{DatumText} · {CategoryFullPath} · {BetragText} EUR";
}
