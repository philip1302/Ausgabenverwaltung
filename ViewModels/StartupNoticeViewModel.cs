using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Meldung ueber eine beim Start gescheiterte Sicherung. Sie darf den
    /// Start nicht verhindern, aber auch nicht unbemerkt bleiben - ein
    /// Band statt eines Dialogs. Ein nicht erreichbares ZWEITES Ziel
    /// erscheint hier bewusst nicht: das steht still in den Einstellungen
    /// (siehe Core.Backups.BackupResult.NeedsAttention).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupErrorVisible))]
    private string? _backupErrorText;

    public bool BackupErrorVisible => BackupErrorText is not null;

    public StartupNoticeViewModel(StartupResult startupResult)
    {
        Zeige(startupResult.GeneratedExpenses);

        if (startupResult.Backup is { NeedsAttention: true } backup)
        {
            BackupErrorText =
                "Die automatische Datensicherung beim Programmstart ist fehlgeschlagen: "
                + backup.PrimaryError
                + "\nDie Anwendung läuft weiter. Unter „Verwaltung › Datensicherung“ "
                + "lässt sich ein neuer Versuch starten.";
        }
    }

    [RelayCommand]
    private void DismissBackupError() => BackupErrorText = null;

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
                $"{EuroText.Format(expense.AmountCents)}" +
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
