using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Rahmen der Anwendung: Navigation zwischen den fuenf Bereichen plus der
/// Startup-Hinweis. Enthaelt selbst keine Fachlogik, nur Verdrahtung der
/// per DI bereitgestellten Bereichs-ViewModels.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    public StartupNoticeViewModel StartupNotice { get; }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

    public MainViewModel(
        StartupNoticeViewModel startupNotice,
        ErfassenViewModel erfassen,
        OffenePostenViewModel offenePosten,
        ReportViewModel report,
        AusgabenlisteViewModel ausgabenliste,
        VerwaltungViewModel verwaltung)
    {
        StartupNotice = startupNotice;

        NavigationItems = new List<NavigationItem>
        {
            new("Erfassen", erfassen),
            new("Offene Posten", offenePosten),
            new("Report", report),
            new("Ausgabenliste", ausgabenliste),
            new("Verwaltung", verwaltung),
        };

        _selectedNavigationItem = NavigationItems[0];
    }
}
