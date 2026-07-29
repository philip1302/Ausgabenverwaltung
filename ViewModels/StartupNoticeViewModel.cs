using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Wegklickbarer Hinweis nach dem Start: wie viele wiederkehrende
/// Buchungen erzeugt wurden, mit aufklappbarer Liste. Baut den Anzeige-
/// text aus dem <see cref="StartupResult"/> des Startdiensts - reine
/// Darstellung, keine Fachlogik (die steckt in StartupService/Core).
/// </summary>
public sealed partial class StartupNoticeViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible;

    public string SummaryText { get; }
    public int GeneratedExpenseCount { get; }
    public IReadOnlyList<string> GeneratedExpenseDescriptions { get; }

    public StartupNoticeViewModel(StartupResult startupResult)
    {
        GeneratedExpenseCount = startupResult.GeneratedExpenseCount;
        GeneratedExpenseDescriptions = startupResult.GeneratedExpenses
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
