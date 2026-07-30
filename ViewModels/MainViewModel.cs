using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.RecurringExpenses;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Rahmen der Anwendung: Navigation zwischen den fuenf Bereichen plus der
/// Hinweis ueber erzeugte wiederkehrende Buchungen. Enthaelt selbst keine
/// Fachlogik, nur Verdrahtung der per DI bereitgestellten
/// Bereichs-ViewModels.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly ErfassenViewModel _erfassen;
    private readonly OffenePostenViewModel _offenePosten;
    private readonly AusgabenlisteViewModel _ausgabenliste;
    private readonly VerwaltungViewModel _verwaltung;
    private readonly RecurringExpenseScheduler _scheduler;

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
        VerwaltungViewModel verwaltung,
        RecurringExpenseScheduler scheduler)
    {
        StartupNotice = startupNotice;
        _erfassen = erfassen;
        _offenePosten = offenePosten;
        _ausgabenliste = ausgabenliste;
        _verwaltung = verwaltung;
        _scheduler = scheduler;

        NavigationItems = new List<NavigationItem>
        {
            new("Erfassen", erfassen),
            new("Offene Posten", offenePosten),
            new("Report", report),
            new("Ausgabenliste", ausgabenliste),
            new("Verwaltung", verwaltung),
        };

        _selectedNavigationItem = NavigationItems[0];

        // Der Vorlagenbereich kennt die Navigation nicht - er meldet nur an,
        // dass die Buchungen einer Vorlage gezeigt werden sollen. Erst den
        // Filter setzen, dann wechseln: AktualisiereListe laedt danach nur
        // neu und laesst die Filterwerte stehen.
        verwaltung.Vorlagen.BuchungenAnzeigenAngefordert += (_, anfrage) =>
        {
            _ausgabenliste.ZeigeVorlagenBuchungen(anfrage.VorlageId, anfrage.Titel);
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };
    }

    // Bereichs-ViewModels sind DI-Singletons und laden Daten wie die
    // Kategorienliste nur einmal. Beim Wechsel deshalb neu laden, damit
    // Aenderungen aus anderen Bereichen ohne Neustart sichtbar sind.
    partial void OnSelectedNavigationItemChanged(NavigationItem value)
    {
        // Erst erzeugen, dann laden: sonst zeigt der Bereich, in den gerade
        // gewechselt wird, den Stand von vor der Erzeugung. Der Scheduler
        // laeuft hoechstens einmal pro Kalendertag, ein Bereichswechsel
        // kostet also in aller Regel keinen Datenbankzugriff.
        var erzeugt = _scheduler.RunIfDue(DateOnly.FromDateTime(DateTime.Now));
        if (erzeugt.Count > 0)
        {
            StartupNotice.Zeige(erzeugt);
        }

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

        if (ReferenceEquals(value.ViewModel, _verwaltung))
        {
            // Die Spalten "erzeugt" und "naechste Faelligkeit" veralten,
            // sobald anderswo etwas erzeugt oder geloescht wurde.
            _verwaltung.Vorlagen.AktualisiereListe();
        }
    }
}
