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
    private readonly ErfassenViewModel _erfassen;
    private readonly OffenePostenViewModel _offenePosten;
    private readonly AusgabenlisteViewModel _ausgabenliste;

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
        _erfassen = erfassen;
        _offenePosten = offenePosten;
        _ausgabenliste = ausgabenliste;

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

    // Bereichs-ViewModels sind DI-Singletons und laden Daten wie die
    // Kategorienliste nur einmal. Beim Wechsel zurueck zu "Erfassen"
    // deshalb neu laden, damit Aenderungen aus der Verwaltung (neue,
    // umbenannte oder archivierte Kategorien) ohne Neustart sichtbar sind.
    partial void OnSelectedNavigationItemChanged(NavigationItem value)
    {
        if (ReferenceEquals(value.ViewModel, _erfassen))
        {
            _erfassen.AktualisiereKategorieVorschlaege();
            _erfassen.AktualisiereZahlerOptionen();
        }

        if (ReferenceEquals(value.ViewModel, _offenePosten))
        {
            _offenePosten.AktualisiereListe();
        }

        if (ReferenceEquals(value.ViewModel, _ausgabenliste))
        {
            _ausgabenliste.AktualisiereListe();
        }
    }
}
