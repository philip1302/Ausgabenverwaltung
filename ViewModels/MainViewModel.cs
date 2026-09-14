using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
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
/// Liste von fuenf Eintraegen.
///
/// Nur noch die drei Unterpunkte von "Verwaltung" (Kategorien, Personen,
/// Datensicherung) zeigen auf dieselbe
/// <see cref="VerwaltungViewModel"/>-Instanz und aktivieren dort jeweils
/// einen anderen Tab (siehe <see cref="NavigationItem.VerwaltungsTabIndex"/>).
///
/// "Wiederkehrende Ausgaben" und "Darstellung" sind dagegen eigenstaendige
/// Bereiche: ihr Navigationseintrag zeigt direkt auf
/// <see cref="VorlagenViewModel"/> bzw. <see cref="DarstellungViewModel"/>,
/// nicht auf die Verwaltungsseite. Sie oeffnen deren Tab-Leiste damit gar
/// nicht erst und bieten von sich aus auch keinen Weg zurueck in die
/// anderen Verwaltungsbereiche - beides waere ein zweiter, ungewollter
/// Zugang zu Seiten, die mit ihnen nichts zu tun haben.
///
/// "Darstellung" hat zusaetzlich keinen sichtbaren Sidebar-Platz (Gruppe
/// <see cref="NavigationGruppe.Keine"/>, wie die Startseite): die Fusszeile
/// (Views/MainWindow.axaml) waehlt den Eintrag ueber
/// <see cref="OeffneDarstellungCommand"/> an und wechselt damit ganz normal
/// den Hauptinhalt statt ein Flyout zu oeffnen.
///
/// Dasselbe gilt fuer "Was ist neu": ein gewoehnlicher Bereich ohne
/// Sidebar-Platz, der beim ersten Start nach einer Aktualisierung
/// vorausgewaehlt ist (siehe <see cref="WasIstNeuViewModel"/>) und beim
/// Weiterklicken der Startseite Platz macht. Bewusst kein Dialogfenster:
/// eine Seite laesst sich lesen, rollen und in der eingestellten
/// Schriftgroesse anzeigen, ohne dass sie den Start blockiert.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly StartseiteViewModel _startseite;
    private readonly ErfassenViewModel _erfassen;
    private readonly OffenePostenViewModel _offenePosten;
    private readonly ReportViewModel _report;
    private readonly JahresrueckblickViewModel _jahresrueckblick;
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

    // "Darstellung" hat keinen sichtbaren Sidebar-Platz mehr (Gruppe
    // "Keine", wie die Startseite), bleibt aber ein normaler
    // Navigationseintrag: ein Klick auf die Fusszeile (Views/MainWindow.axaml)
    // wechselt wie jeder andere Bereichswechsel den Hauptinhalt, statt ein
    // Flyout zu oeffnen.
    private readonly NavigationItem _darstellungEintrag;

    // Der Eintrag der Kuerzeluebersicht (F1) - ebenfalls ohne
    // Sidebar-Platz, angewaehlt ueber OeffneTastenkuerzelCommand.
    private readonly NavigationItem _tastenkuerzelEintrag;

    // Wohin "Schließen" auf der Kuerzeluebersicht zurueckfuehrt. Die Seite
    // ist eine Zwischenfrage mitten in der Arbeit, keine Station: wer sie
    // aus der Ausgabenliste heraus aufschlaegt, will danach wieder in die
    // Ausgabenliste und nicht auf die Startseite.
    private NavigationItem? _vorTastenkuerzel;

    /// <summary>
    /// Die Bereiche, die Strg+1 bis Strg+9 anspringen - genau die
    /// Eintraege mit sichtbarem Sidebar-Platz, in ihrer Reihenfolge von
    /// oben nach unten. Aus <see cref="NavigationItems"/> abgeleitet und
    /// nicht als zweite Liste gepflegt: sonst zeigte Strg+4 nach dem
    /// naechsten Umbau der Seitenleiste woandershin als der vierte Eintrag.
    /// </summary>
    public IReadOnlyList<NavigationItem> BereicheMitZiffer { get; }

    public MainViewModel(
        StartupNoticeViewModel startupNotice,
        AktualisierungViewModel aktualisierung,
        WasIstNeuViewModel wasIstNeu,
        TastenkuerzelViewModel tastenkuerzel,
        StartseiteViewModel startseite,
        ErfassenViewModel erfassen,
        OffenePostenViewModel offenePosten,
        ReportViewModel report,
        JahresrueckblickViewModel jahresrueckblick,
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
        _jahresrueckblick = jahresrueckblick;
        _ausgabenliste = ausgabenliste;
        _verwaltung = verwaltung;
        _scheduler = scheduler;

        NavigationItems = new List<NavigationItem>
        {
            new("Startseite", startseite, "IconStartseite"),

            new("Erfassen", erfassen, "IconErfassen", NavigationGruppe.ErfassenUndVerwalten),
            new("Offene Posten", offenePosten, "IconOffenePosten", NavigationGruppe.ErfassenUndVerwalten),

            // Zeigt auf VorlagenViewModel, NICHT auf VerwaltungViewModel:
            // "Wiederkehrende Ausgaben" ist ein eigenstaendiger Bereich,
            // der die Verwaltungsseite samt ihrer Tab-Leiste gar nicht
            // erst oeffnet.
            new("Wiederkehrende Ausgaben", verwaltung.Vorlagen, "IconVorlagen", NavigationGruppe.ErfassenUndVerwalten),

            new("Report", report, "IconReport", NavigationGruppe.Auswertung),
            new("Jahresrückblick", jahresrueckblick, "IconJahresrueckblick", NavigationGruppe.Auswertung),
            new("Ausgabenliste", ausgabenliste, "IconAusgabenliste", NavigationGruppe.Auswertung),

            new("Kategorien", verwaltung, "IconKategorien", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 0),
            new("Personen", verwaltung, "IconPersonen", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 1),
            new("Datensicherung", verwaltung, "IconDatensicherung", NavigationGruppe.Einstellungen, istUnterpunkt: true, verwaltungsTabIndex: 2),

            // Ebenfalls eigenstaendig - nur ohne Sidebar-Platz, angewaehlt
            // ueber die Fusszeile (OeffneDarstellungCommand).
            new("Darstellung", verwaltung.Darstellung, "IconDarstellung", NavigationGruppe.Keine),

            // Ohne Sidebar-Platz und ohne Kommando: diesen Bereich waehlt
            // nur der Start aus, und zwar hoechstens einmal je Fassung.
            new("Was ist neu", wasIstNeu, "IconDarstellung", NavigationGruppe.Keine),

            // Ebenfalls ohne Sidebar-Platz - aufgeschlagen wird die
            // Uebersicht ueber F1 (OeffneTastenkuerzelCommand).
            new("Tastenkürzel", tastenkuerzel, "IconDarstellung", NavigationGruppe.Keine),
        };

        StartseiteEintrag = NavigationItems[0];
        ErfassenUndVerwaltenEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.ErfassenUndVerwalten).ToList();
        AuswertungEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.Auswertung).ToList();
        EinstellungenEintraege = NavigationItems.Where(i => i.Gruppe == NavigationGruppe.Einstellungen).ToList();
        // Ueber die ViewModel-Instanz und nicht ueber die Position in der
        // Liste: seit "Was ist neu" daneben steht, waere "der letzte
        // Eintrag" der falsche.
        _darstellungEintrag = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, verwaltung.Darstellung));

        _tastenkuerzelEintrag = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, tastenkuerzel));

        // Dieselbe Reihenfolge wie in der Seitenleiste (Views/MainWindow.axaml):
        // Startseite, dann die drei Gruppen. Mehr als neun Eintraege waeren
        // ueber Ziffern ohnehin nicht zu erreichen - die Liste wird deshalb
        // gekappt statt eine Zuordnung zu behaupten, die es nicht gibt.
        BereicheMitZiffer = new[] { StartseiteEintrag }
            .Concat(ErfassenUndVerwaltenEintraege)
            .Concat(AuswertungEintraege)
            .Concat(EinstellungenEintraege)
            .Take(Tastenkuerzel.BereicheMitZiffer)
            .ToList();

        var wasIstNeuEintrag = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, wasIstNeu));

        // Beim ersten Start nach einer Aktualisierung steht "Was ist neu"
        // vorn, sonst wie immer die Startseite. Direkte Feldzuweisung wie
        // bisher - der Aenderungshandler soll hier noch nicht laufen, die
        // Bereiche sind gerade erst erzeugt.
        _selectedNavigationItem = wasIstNeu.Sichtbar
            ? wasIstNeuEintrag
            : NavigationItems[0];

        // Die Seite kennt die Navigation nicht, sie meldet nur, dass sie
        // fertig ist - dasselbe Muster wie bei den Spruengen unten.
        wasIstNeu.Geschlossen += (_, _) => SelectedNavigationItem = StartseiteEintrag;

        // Die Kuerzeluebersicht kehrt dorthin zurueck, wo sie
        // aufgeschlagen wurde. Nur wenn das nicht mehr zu ermitteln ist,
        // uebernimmt die Startseite.
        tastenkuerzel.Geschlossen += (_, _) =>
            SelectedNavigationItem = _vorTastenkuerzel ?? StartseiteEintrag;

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

        // Derselbe Sprung aus den Leerzustaenden von Ausgabenliste und
        // Auswertung: solange noch gar nichts erfasst ist, hilft dort kein
        // Filter, sondern nur die Erfassungsmaske.
        ausgabenliste.ErfassenAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _erfassen));
        };

        report.ErfassenAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _erfassen));
        };

        jahresrueckblick.ErfassenAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _erfassen));
        };

        // Klick auf eine Karte oder eine Zeile des Rueckblicks: zeigt genau
        // die Buchungen dahinter. Erst filtern, dann wechseln - sonst
        // stuende die Liste kurz mit ihrem alten Inhalt da.
        jahresrueckblick.AusgabenlisteAngefordert += (_, sprung) =>
        {
            _ausgabenliste.ZeigeKategorieZeitraum(
                sprung.KategorieId, sprung.Von, sprung.BisEinschliesslich);

            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        startseite.AusgabenlisteAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        // Klick auf einen Balken im Diagramm: zeigt die Buchungen genau
        // dieses Monats - derselbe Weg wie beim Sprung aus einer Zelle
        // der Auswertung.
        startseite.ZeitraumAngefordert += (_, zeitraum) =>
        {
            // Das Ende des Zeitraums ist ausschliessend, die Filterleiste
            // versteht ihre Felder einschliessend.
            _ausgabenliste.ZeigeZeitraum(zeitraum.From, zeitraum.ToExclusive.AddDays(-1));

            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        // Die vier KPI-Kacheln der Startseite fuehren dorthin, wo ihre Zahl
        // herkommt. Wie beim Balken-Klick oben gilt ueberall: erst den
        // Filter setzen, dann wechseln - AktualisiereListe laedt danach nur
        // neu und laesst die Filterwerte stehen.
        startseite.AusgabenMonatAngefordert += (_, monatsAnfang) =>
        {
            _ausgabenliste.ZeigeMonat(monatsAnfang, nurEinnahmen: false, meineKosten: true);

            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        startseite.EinnahmenMonatAngefordert += (_, monatsAnfang) =>
        {
            _ausgabenliste.ZeigeMonat(monatsAnfang, nurEinnahmen: true, meineKosten: false);

            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
        };

        startseite.OffenePostenAngefordert += (_, _) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, _offenePosten));
        };

        // Umgekehrte Reihenfolge als bei den uebrigen Spruengen: der
        // Bereichswechsel laedt die Vorlagenliste neu und wuerde eine
        // vorher gesetzte Markierung mit wegwerfen (siehe WaehleVorlage).
        startseite.NaechsteFaelligkeitAngefordert += (_, vorlageId) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, verwaltung.Vorlagen));

            verwaltung.Vorlagen.WaehleVorlage(vorlageId);
        };

        // "Als Vorlage" an einer Zeile der Ausgabenliste. Umgekehrte
        // Reihenfolge wie bei den uebrigen Spruengen - aus demselben Grund
        // wie bei WaehleVorlage oben: der Bereichswechsel laedt die
        // Vorlagenliste neu, das vorbelegte Formular soll danach entstehen.
        ausgabenliste.VorlageAusBuchungAngefordert += (_, expenseId) =>
        {
            SelectedNavigationItem = NavigationItems
                .First(item => ReferenceEquals(item.ViewModel, verwaltung.Vorlagen));

            verwaltung.Vorlagen.NeueVorlageAus(expenseId);
        };

        // Klick auf eine Zeile unter "Letzte Buchungen" bzw. "Letzte
        // Ausgaben": zeigt genau diese eine Buchung.
        startseite.BuchungAngefordert += (_, zeile) => ZeigeEinzelneBuchung(zeile);
        erfassen.BuchungAngefordert += (_, zeile) => ZeigeEinzelneBuchung(zeile);

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

    // Beide Uebersichtslisten ("Letzte Buchungen" auf der Startseite,
    // "Letzte Ausgaben" unter der Erfassungsmaske) fuehren auf denselben
    // Weg - deshalb einmal hier statt zweimal in den Handlern.
    private void ZeigeEinzelneBuchung(LetzteAusgabeZeile zeile)
    {
        _ausgabenliste.ZeigeEinzelneBuchung(zeile.Id, zeile.Beschreibung);

        SelectedNavigationItem = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));
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

    /// <summary>
    /// Gebunden an die Sidebar-Fusszeile (Views/MainWindow.axaml): wechselt
    /// wie ein gewaehlter Navigationseintrag in den normalen Anzeigebereich,
    /// nur eben zu einem Eintrag ohne eigenen Sidebar-Platz.
    /// </summary>
    [RelayCommand]
    private void OeffneDarstellung()
    {
        SelectedNavigationItem = _darstellungEintrag;
    }

    // ---------------- Tastenkuerzel ----------------
    //
    // Die Gesten selbst stehen nicht hier, sondern in
    // Anzeige/Tastenkuerzel.cs; das Hauptfenster baut seine Bindungen
    // daraus auf (Views/MainWindow.axaml.cs). Hier steht nur, was beim
    // Druck passiert.

    /// <summary>Strg+N: die Erfassungsmaske, Zeiger im Betragsfeld.</summary>
    [RelayCommand]
    private void NeueBuchung()
    {
        SelectedNavigationItem = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, _erfassen));

        // Den Fokus setzt die Ansicht selbst - beim Betreten des Bereichs
        // steht der Zeiger ohnehin im Betragsfeld (siehe ErfassenView).
    }

    /// <summary>
    /// Strg+F: in die Ausgabenliste und in ihr Suchfeld. Der Fokuswunsch
    /// ueberlebt den Bereichswechsel, weil die Ansicht erst im naechsten
    /// Layoutlauf entsteht (siehe
    /// <see cref="AusgabenlisteViewModel.FokussiereSuche"/>).
    /// </summary>
    [RelayCommand]
    private void SucheFokussieren()
    {
        SelectedNavigationItem = NavigationItems
            .First(item => ReferenceEquals(item.ViewModel, _ausgabenliste));

        _ausgabenliste.FokussiereSuche();
    }

    /// <summary>
    /// Strg+1 bis Strg+9: der Bereich an dieser Stelle der Seitenleiste.
    /// Eine Ziffer ohne Bereich (kuenftig weniger Eintraege) tut nichts.
    /// </summary>
    [RelayCommand]
    private void WaehleNummer(int nummer)
    {
        if (nummer >= 1 && nummer <= BereicheMitZiffer.Count)
        {
            SelectedNavigationItem = BereicheMitZiffer[nummer - 1];
        }
    }

    /// <summary>
    /// F1: die Kuerzeluebersicht. Ein zweiter Druck auf der Uebersicht
    /// selbst merkt sich NICHT sie als Rueckweg - sonst fuehrte
    /// "Schließen" wieder auf dieselbe Seite.
    /// </summary>
    [RelayCommand]
    private void OeffneTastenkuerzel()
    {
        if (!ReferenceEquals(SelectedNavigationItem, _tastenkuerzelEintrag))
        {
            _vorTastenkuerzel = SelectedNavigationItem;
        }

        SelectedNavigationItem = _tastenkuerzelEintrag;
    }

    // Genau ein Eintrag ist aktiv - siehe NavigationItem.IstAktiv. Bei den
    // vier Eintraegen, die auf VerwaltungViewModel zeigen (Wiederkehrende
    // Ausgaben plus die drei Verwaltungs-Unterpunkte), sind das vier
    // verschiedene Objekte (dieselbe ViewModel-Instanz, aber je ein eigener
    // Navigationseintrag), ReferenceEquals traegt deshalb auch dort korrekt
    // genau einen Treffer.
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

        if (ReferenceEquals(value.ViewModel, _jahresrueckblick))
        {
            _jahresrueckblick.Aktualisiere();
        }

        if (ReferenceEquals(value.ViewModel, _ausgabenliste))
        {
            _ausgabenliste.AktualisiereListe();
        }

        // Eigenstaendiger Bereich, nicht mehr Teil der Verwaltungsseite:
        // die Spalten "erzeugt" und "naechste Faelligkeit" veralten,
        // sobald anderswo etwas erzeugt oder geloescht wurde.
        if (ReferenceEquals(value.ViewModel, _verwaltung.Vorlagen))
        {
            _verwaltung.Vorlagen.AktualisiereListe();
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

            // Das Alter der letzten externen Sicherung waechst waehrend
            // der Sitzung weiter, und im Sicherungsordner kann von aussen
            // aufgeraeumt worden sein.
            _verwaltung.Datensicherung.Aktualisiere();
        }
    }
}
