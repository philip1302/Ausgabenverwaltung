using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Wegklickbarer Hinweis ueber wiederkehrende Buchungen, die gerade
/// automatisch erzeugt wurden - beim Programmstart und, weil die Erzeugung
/// sonst am Start haengen wuerde, auch bei den Laeufen waehrend der
/// Sitzung (siehe <see cref="MainViewModel"/> und
/// Core.RecurringExpenses.RecurringExpenseScheduler). Baut nur den
/// Anzeigetext, keine Fachlogik (die steckt in Core).
/// </summary>
public sealed partial class StartupNoticeViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private int _generatedExpenseCount;

    [ObservableProperty]
    private IReadOnlyList<string> _generatedExpenseDescriptions = [];

    public StartupNoticeViewModel(StartupResult startupResult)
    {
        Zeige(startupResult.GeneratedExpenses);
    }

    /// <summary>
    /// Zeigt einen Erzeugungslauf an. Bei null Buchungen bleibt das Banner
    /// unsichtbar - ein "es war nichts faellig" beim Bereichswechsel waere
    /// nur Rauschen.
    /// </summary>
    public void Zeige(IReadOnlyList<Expense> erzeugte)
    {
        GeneratedExpenseCount = erzeugte.Count;
        GeneratedExpenseDescriptions = erzeugte
            .Select(expense =>
                $"{IsoDate.ToDateText(expense.ExpenseDate)} - " +
                $"{Money.ToDecimal(expense.AmountCents).ToString("N2", CultureInfo.InvariantCulture)} EUR" +
                (string.IsNullOrEmpty(expense.Note) ? string.Empty : $" ({expense.Note})"))
            .ToList();

        SummaryText = GeneratedExpenseCount == 1
            ? "1 wiederkehrende Buchung wurde erzeugt."
            : $"{GeneratedExpenseCount} wiederkehrende Buchungen wurden erzeugt.";

        IsVisible = GeneratedExpenseCount > 0;
    }

    [RelayCommand]
    private void Dismiss() => IsVisible = false;
}
