using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.RecurringExpenses;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Rahmen der Anwendung: Navigation zwischen den Bereichen plus der
/// Hinweis ueber erzeugte wiederkehrende Buchungen. Enthaelt selbst keine
/// Fachlogik, nur Verdrahtung der per DI bereitgestellten
/// Bereichs-ViewModels.
///
/// Die Navigation ist seit dem UI/UX-Redesign (Abschnitt 3) in Gruppen
/// gegliedert (siehe <see cref="NavigationGruppe"/>) statt einer flachen
/// Liste von fuenf Eintraegen, und "Verwaltung" traegt fuenf eigene
/// Unterpunkte, die auf dieselbe <see cref="VerwaltungViewModel"/>-Instanz
/// zeigen, aber jeweils einen anderen Tab aktivieren (siehe
/// <see cref="NavigationItem.VerwaltungsTabIndex"/>).
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly StartseiteViewModel _startseite;
    private readonly ErfassenViewModel _erfassen;
    private readonly OffenePostenViewModel _offenePosten;
    private readonly ReportViewModel _report;
    private readonly AusgabenlisteViewModel _ausgabenliste;
    private readonly VerwaltungViewModel _verwaltung;
    private readonly RecurringExpenseScheduler _scheduler;

    public StartupNoticeViewModel StartupNotice { get; }

    /// <summary>
    /// Das Band zur Selbstaktualisierung - erscheint erst, wenn im
    /// Hintergrund tatsaechlich eine neue Fassung gefunden wurde (siehe
    /// <see cref="AktualisierungViewModel"/>).
    /// </summary>
    public AktualisierungViewModel Aktualisierung { get; }

    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    // Vorgefilterte Teillisten je Sidebar-Gruppe (UI/UX-Redesign, Abschnitt
    // 3) - so binden die vier Abschnitte in Views/MainWindow.axaml direkt
    // an je eine Liste, ohne dass die Ansicht selbst nach Gruppe filtert
    // (Regel 7: Filterlogik gehoert ins ViewModel).
    public NavigationItem StartseiteEintrag { get; }
    public IReadOnlyList<NavigationItem> ErfassenUndVerwaltenEintraege { get; }
    public IReadOnlyList<NavigationItem> AuswertungEintraege { get; }
    public IReadOnlyList<NavigationItem> EinstellungenEintraege { get; }

    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

    /// <summary>Zaehler-Badge an "Offene Posten" - siehe Views/MainWindow.axaml.</summary>
    public OffenePostenViewModel OffenePostenFuerBadge => _offenePosten;

    /// <summary>Fuer die Sidebar-Fusszeile ("Hell · Normal") - siehe Views/MainWindow.axaml.</summary>
    public DarstellungViewModel Darstellung => _verwaltung.Darstellung;

    public MainViewModel(
        StartupNoticeViewModel startupNotice,
        AktualisierungViewModel aktualisierung,
        StartseiteViewModel startseite,
        ErfassenViewModel erfassen,
        OffenePostenViewModel offenePosten,
        ReportViewModel report,
        AusgabenlisteViewModel ausgabenliste,
        VerwaltungViewModel verwaltung,
        RecurringExpenseScheduler scheduler)
    {
        StartupNotice = startupNotice;
        Aktualisierung = aktualisierung;
        _startseite = startseite;
        _erfassen = erfassen;
        _offenePosten = offenePosten;
        _report = report;
        _ausgabenliste = ausgabenliste;
        _verwaltung = verwaltung;
        _scheduler = scheduler;

        NavigationItems = new List<NavigationItem>
        {
            new("Startseite", startseite, "IconStartseite"),

            new("Erfassen", erfassen, "IconErfassen", NavigationGruppe.ErfassenUndVerwalten),
            new("Offene Posten", offenePosten, "IconOffenePosten", NavigationGruppe.ErfassenUndVerwalten),

            new("Report", report, "IconReport", NavigationGruppe.Auswertung),
            new("Ausgabenliste", ausgabenliste, "IconAusgabenliste", NavigationGruppe.Auswertung),

            new("Kategorien", verwaltung, "IconKategorien", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 0),
            new("Personen", verwaltung, "IconPersonen", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 1),
            new("Wiederkehrende Ausgaben", verwaltung, "IconVorlagen", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 2),
            new("Datensicherung", verwaltung, "IconDatensicherung", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 3),
            new("Darstellung", verwaltung, "IconDarstellung", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 4),
        };

        StartseiteEintrag = NavigationItems[0];
        ErfassenUndVerwaltenEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.ErfassenUndVerwalten).ToList();
        AuswertungEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.Auswertung).ToList();
        EinstellungenEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.Einstellungen).ToList();

        _selectedNavigationItem = NavigationItems[0];

        // Wird sonst erst beim naechsten Wechsel gesetzt (siehe
        // OnSelectedNavigationItemChanged) - die direkte Feldzuweisung
        // oben loest den Aenderungshandler nicht aus.
        AktualisiereAktivStatus();

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

        // "Ausgabe erfassen" auf der Startseite springt in den
        // Erfassen-Bereich - derselbe Weg wie beim Vorlagen-Sprung oben.
        startseite.ErfassenAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _erfassen));
        };

        startseite.AusgabenlisteAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        // Zaehler-Badge an "Offene Posten" (UI/UX-Redesign, Abschnitt 3) -
        // OffenePostenViewModel ist ein DI-Singleton und meldet jede
        // Neuberechnung ueber PropertyChanged weiter.
        var offenePostenEintrag = NavigationItems.First(item => ReferenceEquals(item.ViewModel, _offenePosten));
        offenePostenEintrag.BadgeAnzahl = offenePosten.AnzahlOffenerPosten;
        offenePosten.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OffenePostenViewModel.AnzahlOffenerPosten))
            {
                offenePostenEintrag.BadgeAnzahl = offenePosten.AnzahlOffenerPosten;
            }
        };
    }

    /// <summary>
    /// Waehlt einen Navigationseintrag aus - gebunden an jeden
    /// Sidebar-Button (Views/MainWindow.axaml), statt SelectedNavigationItem
    /// direkt aus der Ansicht zu setzen (Regel 7: die Auswahl bleibt eine
    /// Reaktion auf eine Anwenderaktion, kein Zwei-Wege-Binding an eine
    /// beliebige Stelle).
    /// </summary>
    [RelayCommand]
    private void Waehle(NavigationItem? item)
    {
        if (item is not null)
        {
            SelectedNavigationItem = item;
        }
    }

    // Genau ein Eintrag ist aktiv - siehe NavigationItem.IstAktiv. Bei den
    // fuenf Verwaltungs-Unterpunkten sind das fuenf verschiedene Objekte
    // (dieselbe ViewModel-Instanz, aber je ein eigener Navigationseintrag),
    // ReferenceEquals traegt deshalb auch dort korrekt genau einen Treffer.
    private void AktualisiereAktivStatus()
    {
        foreach (var item in NavigationItems)
        {
            item.IstAktiv = ReferenceEquals(item, SelectedNavigationItem);
        }
    }

    // Bereichs-ViewModels sind DI-Singletons und laden Daten wie die
    // Kategorienliste nur einmal. Beim Wechsel deshalb neu laden, damit
    // Aenderungen aus anderen Bereichen ohne Neustart sichtbar sind.
    partial void OnSelectedNavigationItemChanged(NavigationItem value)
    {
        AktualisiereAktivStatus();

        // Erst erzeugen, dann laden: sonst zeigt der Bereich, in den gerade
        // gewechselt wird, den Stand von vor der Erzeugung. Der Scheduler
        // laeuft hoechstens einmal pro Kalendertag, ein Bereichswechsel
        // kostet also in aller Regel keinen Datenbankzugriff.
        var erzeugt = _scheduler.RunIfDue(DateOnly.FromDateTime(DateTime.Now));
        if (erzeugt.Count > 0)
        {
            StartupNotice.Zeige(erzeugt);
        }

        if (ReferenceEquals(value.ViewModel, _startseite))
        {
            _startseite.Aktualisiere();
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

        if (ReferenceEquals(value.ViewModel, _report))
        {
            _report.AktualisiereAuswertung();
        }

        if (ReferenceEquals(value.ViewModel, _ausgabenliste))
        {
            _ausgabenliste.AktualisiereListe();
        }

        if (ReferenceEquals(value.ViewModel, _verwaltung))
        {
            // Ein Unterpunkt der Gruppe "Verwaltung" bringt seinen
            // Ziel-Tab mit (siehe NavigationItem.VerwaltungsTabIndex) -
            // die Tab-Leiste innerhalb der Seite bleibt zusaetzlich als
            // Kontext-Umschalter erhalten (UI/UX-Redesign, Abschnitt 3).
            if (value.VerwaltungsTabIndex is int tabIndex)
            {
                _verwaltung.AusgewaehlterTabIndex = tabIndex;
            }

            // Die Spalten "erzeugt" und "naechste Faelligkeit" veralten,
            // sobald anderswo etwas erzeugt oder geloescht wurde.
            _verwaltung.Vorlagen.AktualisiereListe();

            // Das Alter der letzten externen Sicherung waechst waehrend
            // der Sitzung weiter, und im Sicherungsordner kann von aussen
            // aufgeraeumt worden sein.
            _verwaltung.Datensicherung.Aktualisiere();
        }
    }
}
