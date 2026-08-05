using System;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.OpenItems;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Offene-Posten-Liste: Anzeigewerte aus
/// <see cref="OpenItem"/> plus UI-Zustand fuer Mehrfachauswahl und die
/// Eingabe eines abweichenden Begleichungsdatums. Bewusst getrennt von
/// OpenItem, damit die Core-Entitaet keinen UI-Zustand tragen muss (Regel 7).
/// </summary>
public sealed partial class OffenerPostenZeile : ObservableObject
{
    public int Id { get; }
    public DateOnly ExpenseDate { get; }
    public string DatumText { get; }
    public long AmountCents { get; }
    public string BetragText { get; }

    /// <summary>Erstattung (negativer Betrag) - wird gedaempft rot hervorgehoben.</summary>
    public bool IstErstattung { get; }

    /// <summary>Einnahme - wird gedaempft gruen hervorgehoben.</summary>
    public bool IstEinnahme { get; }
    public string CategoryFullPath { get; }
    public string? Note { get; }
    public int TageOffen { get; }
    public int PayerId { get; }
    public string PayerName { get; }
    public DateOnly? SettledDate { get; }

    public bool IstBeglichen => SettledDate is not null;

    public string BeglichenText => SettledDate is DateOnly datum
        ? $"{(IstEinnahme ? "erhalten am" : "beglichen am")} {GermanDateInput.ToText(datum)}"
        : string.Empty;

    /// <summary>
    /// Wortwahl passend zur Buchungsart: eine Ausgabe wird "abgehakt"
    /// (die fremde Person hat zurueckgezahlt), eine Einnahme "erhalten"
    /// (das Geld ist tatsaechlich eingegangen).
    /// </summary>
    public string AbhakenButtonText => IstEinnahme ? "Erhalten" : "Abhaken";

    public string AbhakenToolTip => IstEinnahme
        ? "Heute als erhalten markieren"
        : "Heute als beglichen markieren";

    [ObservableProperty]
    private bool _istAusgewaehlt;

    [ObservableProperty]
    private bool _waehltAbweichendesDatum;

    [ObservableProperty]
    private string _abweichendesDatumText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbweichendesDatumFehlerSichtbar))]
    private string? _abweichendesDatumFehler;

    public bool AbweichendesDatumFehlerSichtbar => !string.IsNullOrEmpty(AbweichendesDatumFehler);

    public OffenerPostenZeile(OpenItem item)
    {
        Id = item.Id;
        ExpenseDate = item.ExpenseDate;
        DatumText = GermanDateInput.ToText(item.ExpenseDate);
        AmountCents = item.AmountCents;
        BetragText = EuroText.Format(item.AmountCents, item.IsIncome);
        IstErstattung = EuroText.IsNegative(item.AmountCents);
        IstEinnahme = item.IsIncome;
        CategoryFullPath = item.CategoryFullPath;
        Note = item.Note;
        TageOffen = item.TageOffen;
        PayerId = item.PayerId;
        PayerName = item.PayerName;
        SettledDate = item.SettledDate;
    }
}
