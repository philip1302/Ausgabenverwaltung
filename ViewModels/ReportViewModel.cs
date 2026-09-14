using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Report": die Kreuztabelle Kategorien x Zeitabschnitte.
///
/// Gefiltert und aggregiert wird komplett in SQL
/// (<see cref="ReportRepository.EvaluateMatrix"/>), die Zeitraum- und
/// Zeitabschnitt-Arithmetik steckt in <see cref="DateRangePresets"/> und
/// <see cref="ReportPeriods"/>, der Tabellenaufbau in
/// <see cref="ReportMatrixBuilder"/> und der CSV-Text in
/// <see cref="ReportCsv"/> - hier bleiben nur Bindung, Formatierung und
/// Anzeigezustand (Regel 7). Das Filtermodell ist dasselbe
/// <see cref="ReportFilter"/> wie in der Ausgabenliste.
/// </summary>
public sealed partial class ReportViewModel : ViewModelBase
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private readonly ReportRepository _reportRepository;
    private readonly ExpenseRepository _expenseRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PersonRepository _personRepository;
    private readonly AppSettingsStore _settingsStore;

    // Unterdrueckt das Neuladen, solange mehrere Filterwerte auf einmal
    // gesetzt werden (Schnellwahl, Zuruecksetzen, Neuaufbau der
    // Kategorieauswahl) - sonst laeuft die Auswertung pro Eigenschaft neu.
    private bool _ladenGesperrt;

    // Welche Kategorien aufgeklappt sind, ueberdauert das Neuladen:
    // sonst fiele die Tabelle bei jeder Filteraenderung wieder zu.
    private readonly HashSet<int> _aufgeklappteKategorien = new();

    private ReportMatrix? _matrix;

    // Die aufgeloesten Kategoriefarben zur angezeigten Tabelle. Sie
    // ueberdauern das Auf- und Zuklappen, weil dabei nur die Zeilen neu
    // gebaut werden und der Kategoriebaum nicht erneut geladen wird.
    private IReadOnlyDictionary<int, string> _farben = new Dictionary<int, string>();

    // Der Filter, aus dem die angezeigte Tabelle entstanden ist. Der
    // Sprung in die Einzelbuchungen setzt genau darauf auf - nur so kann
    // der Dialog nie etwas anderes zeigen, als die angeklickte Zelle
    // summiert.
    private ReportFilter? _angezeigterFilter;

    public ObservableCollection<ReportSpalte> Spalten { get; } = new();

    public ObservableCollection<ReportZeile> Zeilen { get; } = new();

    public ObservableCollection<KategorieFilterKnoten> KategorieWurzeln { get; } = new();

    public IReadOnlyList<GruppierungOption> GruppierungOptionen { get; } = new[]
    {
        new GruppierungOption("Jahr", ReportGrouping.Year),
        new GruppierungOption("Quartal", ReportGrouping.Quarter),
        new GruppierungOption("Monat", ReportGrouping.Month),
    };

    /// <summary>
    /// Zahler, einzeln an- und abwaehlbar. Die frueheren Gruppen-Optionen
    /// ("alle", "nur ich", "nur andere") sind entfallen - sie ergeben sich
    /// aus den Haekchen. Nur "ich + offene Posten" liess sich so nicht
    /// nachbauen und sitzt jetzt im eigenen Schalter
    /// <see cref="MeineKosten"/>.
    /// </summary>
    public ObservableCollection<ZahlerOption> ZahlerOptionen { get; } = new();

    // ---------------- Filter ----------------

    /// <summary>Leer = offene Grenze (siehe <see cref="DateRangePresets.FromInclusiveBounds"/>).</summary>
    [ObservableProperty]
    private string _vonText = string.Empty;

    /// <summary>Leer = offene Grenze. Einschliessend gemeint.</summary>
    [ObservableProperty]
    private string _bisText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeitraumFehlerSichtbar))]
    private string? _zeitraumFehler;

    public bool ZeitraumFehlerSichtbar => !string.IsNullOrEmpty(ZeitraumFehler);

    /// <summary>Beschriftung der Kategorie-Auswahl in der Filterleiste.</summary>
    public string KategorieFilterText =>
        FilterCaption.Categories(KategorieAuswahl(), _kategorieNamen);

    /// <summary>Beschriftung der Zahler-Auswahl in der Filterleiste.</summary>
    // ---------------- Ein- und Ausklappen, aktive Filter ----------------
    //
    // Wortgleich zur Ausgabenliste: es ist dieselbe Leiste, und wenn sie
    // sich in zwei Bereichen verschieden verhielte, waere das schlimmer
    // als jede der beiden Fassungen fuer sich.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterKlappZeichen))]
    [NotifyPropertyChangedFor(nameof(FilterKlappHinweis))]
    private bool _filterAufgeklappt = true;

    /// <summary>Siehe AusgabenlisteViewModel: aufgeklappt zeigt der Pfeil
    /// nach oben, eingeklappt nach unten.</summary>
    public string FilterKlappZeichen => FilterAufgeklappt ? "▲" : "▼";

    private bool _klappzustandVonHand;

    [RelayCommand]
    private void FilterUmschalten()
    {
        _klappzustandVonHand = true;
        FilterAufgeklappt = !FilterAufgeklappt;
    }

    public void PasseAnBreiteAn(double breite)
    {
        if (_klappzustandVonHand)
        {
            return;
        }

        FilterAufgeklappt = Filterleiste.PasstAufgeklappt(breite);
    }

    public ObservableCollection<FilterChip> AktiveFilter { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAktiveFilter))]
    [NotifyPropertyChangedFor(nameof(FilterKlappHinweis))]
    private int _aktiveFilterAnzahl;

    public bool HatAktiveFilter => AktiveFilterAnzahl > 0;

    public string FilterKlappHinweis
    {
        get
        {
            var was = FilterAufgeklappt ? "Filterfelder verbergen" : "Filterfelder anzeigen";

            return AktiveFilterAnzahl == 0
                ? was
                : $"{was} — {AktiveFilterAnzahl} Filter gesetzt";
        }
    }

    private void AktualisiereAktiveFilter()
    {
        var chips = FilterChips.Bestimme(new FilterZustand
        {
            Zeitraum = ZeitraumWeichtAb() ? $"{VonText} – {BisText}" : null,
            Kategorien = KategorieAuswahl().RootIds.Count == 0
                         && KategorieAuswahl().ExcludedIds.Count == 0
                ? null
                : KategorieFilterText,
            Zahler = ZahlerOptionen.Any(option => option.IstGewaehlt) ? ZahlerFilterText : null,
            StatusOffen = StatusOffen,
            StatusBeglichen = StatusBeglichen,
            NurEinnahmen = NurEinnahmen,
            NurAusgaben = NurAusgaben,
            MeineKosten = MeineKosten,
            Suche = Suchtext,
        });

        AktiveFilter.Clear();
        foreach (var chip in chips)
        {
            AktiveFilter.Add(chip);
        }

        AktiveFilterAnzahl = AktiveFilter.Count;
    }

    // Die Gruppierung (Jahr/Quartal/Monat) bekommt bewusst KEINEN Chip:
    // sie schraenkt nichts ein, sondern sagt nur, wie fein die Spalten
    // stehen. Ein Chip dafuer waere ein Filter, den es aufzuheben gaebe -
    // und "Gruppierung aufheben" ergibt keinen Sinn.
    private bool ZeitraumWeichtAb()
    {
        var vorgabe = DateRangePresets.ThisYear(DateOnly.FromDateTime(DateTime.Now));

        return VonText != GermanDateInput.ToText(vorgabe.From)
            || BisText != GermanDateInput.ToText(vorgabe.ToExclusive.AddDays(-1));
    }

    [RelayCommand]
    private void FilterAufheben(FilterChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        _ladenGesperrt = true;

        switch (chip.Art)
        {
            case FilterArt.Zeitraum:
                AktiverZeitraumSchluessel = null;
                SetzeVorgabeZeitraum();
                break;

            case FilterArt.Kategorien:
                foreach (var knoten in KategorieWurzeln)
                {
                    knoten.SetzeStill(false);
                }

                OnPropertyChanged(nameof(KategorieFilterText));
                break;

            case FilterArt.Zahler:
                foreach (var option in ZahlerOptionen)
                {
                    option.SetzeStill(false);
                }

                OnPropertyChanged(nameof(ZahlerFilterText));
                break;

            case FilterArt.Status:
                StatusOffen = false;
                StatusBeglichen = false;
                break;

            case FilterArt.Buchungsart:
                NurEinnahmen = false;
                NurAusgaben = false;
                break;

            case FilterArt.MeineKosten:
                MeineKosten = false;
                break;

            case FilterArt.Suche:
                Suchtext = string.Empty;
                break;
        }

        _ladenGesperrt = false;

        LadeDaten();
    }

    public string ZahlerFilterText => FilterCaption.Payers(
        ZahlerOptionen.Where(option => option.IstGewaehlt)
                      .Select(option => option.Bezeichnung).ToList());

    // ---------------- Gespeicherte Filter ----------------
    //
    // Wieder wortgleich zur Ausgabenliste, mit einem Unterschied: hier
    // gehoert die Gruppierung zum gespeicherten Stand.

    /// <summary>
    /// Die benannten Filtereinstellungen hinter dem Knopf "Gespeichert"
    /// (siehe <see cref="SavedFilters"/>). Dieselbe Liste wie in der
    /// Ausgabenliste - beide lesen sie aus den Einstellungen.
    /// </summary>
    public ObservableCollection<SavedFilter> GespeicherteFilter { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGespeicherteFilter))]
    private int _gespeicherteFilterAnzahl;

    public bool HatGespeicherteFilter => GespeicherteFilterAnzahl > 0;

    /// <summary>Der Name, unter dem der aktuelle Stand abgelegt wird.</summary>
    [ObservableProperty]
    private string _filterName = string.Empty;

    /// <summary>Fehlertext am Namensfeld, NULL wenn alles in Ordnung ist.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterNameFehlerSichtbar))]
    private string? _filterNameFehler;

    public bool FilterNameFehlerSichtbar => !string.IsNullOrEmpty(FilterNameFehler);

    private void LadeGespeicherteFilter()
    {
        GespeicherteFilter.Clear();

        foreach (var filter in _settingsStore.Load().SavedFilters)
        {
            GespeicherteFilter.Add(filter);
        }

        GespeicherteFilterAnzahl = GespeicherteFilter.Count;
    }

    /// <summary>
    /// Legt den aktuellen Filterstand unter dem eingegebenen Namen ab.
    /// Ein schon vergebener Name ersetzt seinen Eintrag.
    /// </summary>
    [RelayCommand]
    private void FilterSpeichern()
    {
        var einstellungen = _settingsStore.Load();

        FilterNameFehler = SavedFilters.Validate(FilterName, einstellungen.SavedFilters);
        if (FilterNameFehler is not null)
        {
            return;
        }

        var liste = SavedFilters.Save(einstellungen.SavedFilters, SammleFilter(FilterName));

        // Regel 13: das Namensfeld wird erst geraeumt, wenn geschrieben
        // wurde.
        FilterNameFehler = Schreibvorgang.Versuche(
            "Beim Speichern eines Filters",
            () => _settingsStore.Save(einstellungen with { SavedFilters = liste })) is null
            ? null
            : "Der Filter ließ sich nicht merken — die Einstellungsdatei ist gerade "
              + "nicht beschreibbar. An den Buchungen ändert das nichts, und die "
              + "eingestellten Filter wirken weiter; ein zweiter Versuch hilft oft.";

        if (FilterNameFehler is not null)
        {
            return;
        }

        LadeGespeicherteFilter();
        FilterName = string.Empty;
    }

    /// <summary>
    /// Stellt die Leiste auf einen gespeicherten Filter um. Wie beim
    /// Zuruecksetzen wird vorher alles geraeumt.
    /// </summary>
    [RelayCommand]
    private void GespeichertenFilterAnwenden(SavedFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        _ladenGesperrt = true;

        FilterAuswahlLeeren();

        // Ein gespeicherter Schnellwahl-Zeitraum wird neu ausgerechnet und
        // nicht als Datum uebernommen: "dieses Jahr" heisst in jedem Jahr
        // etwas anderes (siehe SavedFilter.PeriodKey).
        if (!WendeSchnellwahlAn(filter.PeriodKey))
        {
            AktiverZeitraumSchluessel = null;
            VonText = filter.From is DateOnly von ? GermanDateInput.ToText(von) : string.Empty;
            BisText = filter.ToInclusive is DateOnly bis
                ? GermanDateInput.ToText(bis)
                : string.Empty;
        }

        GespeicherteFilterHilfe.SetzeHaken(
            KategorieWurzeln, filter.CategoryIds.ToHashSet());

        foreach (var option in ZahlerOptionen)
        {
            option.SetzeStill(filter.PayerIds.Contains(option.Id));
        }

        StatusOffen = filter.StatusOpen;
        StatusBeglichen = filter.StatusSettled;
        NurEinnahmen = filter.IncomeOnly;
        NurAusgaben = filter.ExpensesOnly;
        MeineKosten = filter.MyCosts;
        Suchtext = filter.SearchText ?? string.Empty;

        // Ein in der Ausgabenliste gespeicherter Filter kennt keine
        // Gruppierung; dann bleibt die eingestellte stehen, statt auf eine
        // erfundene zu springen.
        if (filter.Grouping is ReportGrouping gruppierung)
        {
            AusgewaehlteGruppierung = GruppierungOptionen
                .FirstOrDefault(option => option.Wert == gruppierung)
                ?? AusgewaehlteGruppierung;
        }

        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));

        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Loescht einen gespeicherten Filter. Ohne Rueckfrage: an den Daten
    /// aendert das nichts.
    /// </summary>
    [RelayCommand]
    private void GespeichertenFilterLoeschen(SavedFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        var einstellungen = _settingsStore.Load();
        var liste = SavedFilters.Remove(einstellungen.SavedFilters, filter.Name);

        if (Schreibvorgang.Versuche(
                "Beim Loeschen eines Filters",
                () => _settingsStore.Save(einstellungen with { SavedFilters = liste })) is not null)
        {
            // Der Eintrag bleibt stehen - er ist ja noch da.
            return;
        }

        LadeGespeicherteFilter();
    }

    /// <summary>Der Filterstand, so wie er in der Leiste steht.</summary>
    private SavedFilter SammleFilter(string name)
    {
        var ueberSchnellwahl = AktiverZeitraumSchluessel is not null;

        return new SavedFilter
        {
            Name = name,
            PeriodKey = AktiverZeitraumSchluessel,
            From = ueberSchnellwahl ? null : GespeicherteFilterHilfe.LiesGrenze(VonText),
            ToInclusive = ueberSchnellwahl
                ? null
                : GespeicherteFilterHilfe.LiesGrenze(BisText),

            CategoryIds = GespeicherteFilterHilfe.Angehakt(KategorieWurzeln),
            PayerIds = ZahlerOptionen
                .Where(option => option.IstGewaehlt)
                .Select(option => option.Id)
                .ToList(),

            StatusOpen = StatusOffen,
            StatusSettled = StatusBeglichen,
            IncomeOnly = NurEinnahmen,
            ExpensesOnly = NurAusgaben,
            MyCosts = MeineKosten,
            SearchText = string.IsNullOrWhiteSpace(Suchtext) ? null : Suchtext.Trim(),

            // Die Gruppierung gehoert hier zum Stand: "Auto nach Jahren"
            // ist eine andere Frage als "Auto nach Monaten", und sie
            // wieder von Hand umzustellen waere der halbe Weg.
            Grouping = AusgewaehlteGruppierung.Wert,
        };
    }

    // Siehe AusgabenlisteViewModel: Namen fuer die Beschriftung, beim
    // Aufbau des Baums mitgefuellt.
    private Dictionary<int, string> _kategorieNamen = new();

    [ObservableProperty]
    private GruppierungOption _ausgewaehlteGruppierung;

    /// <summary>
    /// Status-Haekchen, kombinierbar. Beide aus = keine Einschraenkung;
    /// beide an ist NICHT dasselbe (Regel 4, siehe SettlementStatus).
    /// </summary>
    [ObservableProperty]
    private bool _statusOffen;

    [ObservableProperty]
    private bool _statusBeglichen;

    /// <summary>
    /// Einschraenkung auf einen Buchungstyp - wortgleich zur
    /// Ausgabenliste: zwei unabhaengige Haekchen nach dem Muster von
    /// <see cref="StatusOffen"/>/<see cref="StatusBeglichen"/>. Beide aus
    /// (und ebenso beide an) heisst "alles", weil eine Buchung nicht
    /// zugleich Einnahme und Ausgabe sein kann. Vorbelegt ist deshalb
    /// beides zusammen, also kein Haekchen.
    /// </summary>
    [ObservableProperty]
    private bool _nurEinnahmen;

    [ObservableProperty]
    private bool _nurAusgaben;

    /// <summary>
    /// "Meine Kosten": eigene Buchungen plus alles, was von anderen noch
    /// offen ist (<see cref="PayerScope.SelfAndOpen"/>) - die frueher als
    /// "ich + offene Posten &amp; Einnahmen" gewaehlte Sicht. Bewusst ein
    /// eigener Schalter: die Bedingung mischt Zahler, Status und
    /// Buchungstyp und laesst sich aus Zahler-Haekchen nicht bauen.
    /// </summary>
    [ObservableProperty]
    private bool _meineKosten;

    [ObservableProperty]
    private string _suchtext = string.Empty;

    /// <summary>
    /// Kategorien ohne jede Buchung im Zeitraum. Standardmaessig
    /// ausgeblendet, damit die Tabelle nicht aus leeren Zeilen besteht.
    /// Wirkt rein auf die Anzeige - die Auswertung laeuft dafuer nicht neu.
    /// </summary>
    [ObservableProperty]
    private bool _kategorienOhneAusgabenAnzeigen;

    // ---------------- Ergebnis ----------------

    [ObservableProperty]
    private ReportZeile? _summenZeile;

    [ObservableProperty]
    private string _trefferText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTrefferTrotzDaten))]
    private bool _keineTreffer;

    /// <summary>
    /// Leer, WEIL es noch gar keine Buchung gibt - nicht, weil der Filter
    /// zu eng steht. Dieselbe Unterscheidung wie in der Ausgabenliste: die
    /// beiden Lagen sehen gleich aus und brauchen verschiedene Angebote.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTrefferTrotzDaten))]
    private bool _nochNichtsErfasst;

    /// <summary>Leer, obwohl es Buchungen gibt - dann liegt es am Filter.</summary>
    public bool KeinTrefferTrotzDaten => KeineTreffer && !NochNichtsErfasst;

    /// <summary>
    /// Bitte um einen Wechsel in die Erfassungsmaske - aus dem
    /// Leerzustand, solange noch gar nichts erfasst ist.
    /// </summary>
    public event EventHandler? ErfassenAngefordert;

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private string? _exportHinweis;

    // ---------------- Einzelbuchungen zu einer Zelle ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsAktiv))]
    private string? _detailTitel;

    public bool DetailsAktiv => DetailTitel is not null;

    public ObservableCollection<AusgabeZeile> DetailZeilen { get; } = new();

    [ObservableProperty]
    private string _detailTrefferText = string.Empty;

    [ObservableProperty]
    private string _detailSummeText = string.Empty;

    /// <summary>Die Summe des Dialogs ist positiv - Einnahmen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _detailSummeIstEinnahme;

    public ReportViewModel(
        ReportRepository reportRepository,
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        AppSettingsStore settingsStore,
        IMessenger messenger)
    {
        _reportRepository = reportRepository;
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _settingsStore = settingsStore;

        _ausgewaehlteGruppierung = GruppierungOptionen[2];

        _ladenGesperrt = true;
        LadeKategorieAuswahl();
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeGespeicherteFilter();

        LadeDaten();

        // Buchungsaenderungen aus anderen Bereichen sollen die Auswertung
        // sofort aktualisieren, nicht erst beim naechsten Navigieren zum
        // Report (Regel 14).
        messenger.Register<ReportViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.AktualisiereAuswertung());
    }

    /// <summary>
    /// Laedt Kategorieauswahl und Auswertung neu. Wird bei Navigation in
    /// diesen Bereich aufgerufen (siehe <see cref="MainViewModel"/>), damit
    /// zwischenzeitlich erfasste Buchungen und geaenderte Kategorien ohne
    /// Neustart erscheinen - die Bereichs-ViewModels sind DI-Singletons.
    /// </summary>
    public void AktualisiereAuswertung()
    {
        _ladenGesperrt = true;
        LadeKategorieAuswahl();
        _ladenGesperrt = false;

        // Die gespeicherten Filter koennen sich in der Ausgabenliste
        // geaendert haben - beide Bereiche fuehren dieselbe Liste.
        LadeGespeicherteFilter();

        LadeDaten();
    }

    /// <summary>
    /// Welcher Schnellwahl-Zeitraum zuletzt gewaehlt wurde (UI/UX-Redesign,
    /// Abschnitt 5.3: "sichtbarer aktiver Zustand" der Schnellwahl-Buttons).
    /// NULL, sobald Von/Bis von Hand veraendert werden - dann passt keine
    /// der Vorgaben mehr exakt.
    /// </summary>
    [ObservableProperty]
    private string? _aktiverZeitraumSchluessel;

    // Jede Filteraenderung wertet neu aus. _ladenGesperrt unterscheidet
    // eine Handeingabe (loescht die Schnellwahl-Markierung) von einer
    // durch Schnellwahl() selbst gesetzten Aenderung.
    partial void OnVonTextChanged(string value)
    {
        if (!_ladenGesperrt)
        {
            AktiverZeitraumSchluessel = null;
        }

        LadeDaten();
    }

    partial void OnBisTextChanged(string value)
    {
        if (!_ladenGesperrt)
        {
            AktiverZeitraumSchluessel = null;
        }

        LadeDaten();
    }
    partial void OnAusgewaehlteGruppierungChanged(GruppierungOption value) => LadeDaten();
    partial void OnStatusOffenChanged(bool value) => LadeDaten();
    partial void OnStatusBeglichenChanged(bool value) => LadeDaten();
    partial void OnMeineKostenChanged(bool value) => LadeDaten();
    partial void OnNurEinnahmenChanged(bool value) => LadeDaten();
    partial void OnNurAusgabenChanged(bool value) => LadeDaten();
    partial void OnSuchtextChanged(string value) => LadeDaten();

    /// <summary>
    /// Ein Haekchen im Kategorie-Baum oder in der Zahlerliste wurde
    /// umgestellt - siehe die gleiche Stelle in
    /// <see cref="AusgabenlisteViewModel"/>.
    /// </summary>
    private void FilterAuswahlGeaendert()
    {
        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));
        LadeDaten();
    }

    /// <summary>
    /// Die angehakten Kategorien als Aeste und Ausschluesse; die
    /// Umrechnung selbst steht in Core (Regel 7).
    /// </summary>
    private CategoryFilterChoice KategorieAuswahl()
    {
        var verbindungen = new List<CategoryParentLink>();
        var angehakt = new HashSet<int>();

        void Sammle(IEnumerable<KategorieFilterKnoten> knoten)
        {
            foreach (var k in knoten)
            {
                verbindungen.Add(new CategoryParentLink(k.Id, k.ParentId));
                if (k.IstGewaehlt)
                {
                    angehakt.Add(k.Id);
                }

                Sammle(k.Children);
            }
        }

        Sammle(KategorieWurzeln);

        return CategoryFilterSelection.Derive(verbindungen, angehakt);
    }

    // Der Schalter blendet nur aus, was schon ausgewertet ist.
    partial void OnKategorienOhneAusgabenAnzeigenChanged(bool value) => BaueZeilen();

    [RelayCommand]
    private void Schnellwahl(string? bereich)
    {
        _ladenGesperrt = true;
        var gesetzt = WendeSchnellwahlAn(bereich);
        _ladenGesperrt = false;

        if (gesetzt)
        {
            LadeDaten();
        }
    }

    /// <summary>
    /// Legt Von/Bis auf einen Schnellwahl-Zeitraum - wortgleich zur
    /// Ausgabenliste. Liefert false bei einem unbekannten Schluessel,
    /// laedt nicht neu und schliesst die Ladesperre nicht selbst.
    /// </summary>
    private bool WendeSchnellwahlAn(string? bereich)
    {
        var heute = DateOnly.FromDateTime(DateTime.Now);

        if (!DateRangePresets.TryByKey(bereich, heute, out var bereichWerte))
        {
            return false;
        }

        AktiverZeitraumSchluessel = bereich;

        if (bereichWerte is DateRange werte)
        {
            VonText = GermanDateInput.ToText(werte.From);
            // Das Ende des Bereichs ist ausschliessend, die Anzeige
            // einschliessend.
            BisText = GermanDateInput.ToText(werte.ToExclusive.AddDays(-1));
        }
        else
        {
            VonText = string.Empty;
            BisText = string.Empty;
        }

        return true;
    }

    [RelayCommand]
    private void FilterZuruecksetzen()
    {
        _ladenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        AusgewaehlteGruppierung = GruppierungOptionen[2];
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        KategorienOhneAusgabenAnzeigen = false;
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Hebt die Kategorie-Auswahl auf - siehe die gleiche Stelle in
    /// <see cref="AusgabenlisteViewModel"/>.
    /// </summary>
    [RelayCommand]
    private void AlleKategorien()
    {
        foreach (var knoten in KategorieWurzeln)
        {
            knoten.SetzeStill(false);
        }

        FilterAuswahlGeaendert();
    }

    /// <summary>Hebt die Zahler-Auswahl auf - wieder alle Zahler.</summary>
    [RelayCommand]
    private void AlleZahler()
    {
        foreach (var option in ZahlerOptionen)
        {
            option.SetzeStill(false);
        }

        FilterAuswahlGeaendert();
    }

    private void FilterAuswahlLeeren()
    {
        foreach (var knoten in KategorieWurzeln)
        {
            knoten.SetzeStill(false);
        }

        foreach (var option in ZahlerOptionen)
        {
            option.SetzeStill(false);
        }

        StatusOffen = false;
        StatusBeglichen = false;
        MeineKosten = false;
        NurEinnahmen = false;
        NurAusgaben = false;

        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));
    }

    [RelayCommand]
    private void ZeileUmschalten(ReportZeile? zeile)
    {
        if (zeile?.Quelle is not { } quelle || !zeile.HatKinder)
        {
            return;
        }

        if (!_aufgeklappteKategorien.Add(quelle.CategoryId))
        {
            _aufgeklappteKategorien.Remove(quelle.CategoryId);
        }

        BaueZeilen();
    }

    // ---------------- Einzelbuchungen ----------------

    /// <summary>
    /// Zeigt die Buchungen hinter einer Zelle. Bewusst ein Dialog und kein
    /// Sprung in die Ausgabenliste: der Report filtert nach ZahlerBEREICH
    /// (alle / nur ich / nur andere), die Ausgabenliste dagegen nach einer
    /// einzelnen Person. Ein Sprung muesste diesen Filter fallen lassen -
    /// die Liste zeigte dann mehr, als die angeklickte Zelle summiert.
    /// </summary>
    [RelayCommand]
    private void ZelleOeffnen(ReportZelle? zelle)
    {
        if (zelle is null || !zelle.HatWerte || _angezeigterFilter is null)
        {
            return;
        }

        var filter = DetailFilter(zelle, _angezeigterFilter);

        var farben = _categoryRepository.GetResolvedColors();

        DetailZeilen.Clear();
        foreach (var item in _expenseRepository.Query(
                     filter, ExpenseSortColumn.Datum, ascending: true))
        {
            DetailZeilen.Add(new AusgabeZeile(item, CategoryColors.Of(farben, item.CategoryId)));
        }

        // Eigene Aggregatabfrage statt einer Summe ueber die geladenen
        // Zeilen - die Fusszeile des Dialogs muss der Zelle entsprechen,
        // unabhaengig davon, was die Liste gerade darstellt.
        var summary = _expenseRepository.Summarize(filter);
        DetailTrefferText = summary.Count == 1
            ? "1 Buchung"
            : $"{summary.Count.ToString("N0", DeDe)} Buchungen";
        DetailSummeText = EuroText.Format(summary.SumCents);
        DetailSummeIstEinnahme = EuroText.IsPositive(summary.SumCents);

        DetailTitel = zelle.Beschreibung;
    }

    [RelayCommand]
    private void DetailsSchliessen()
    {
        DetailTitel = null;
        DetailZeilen.Clear();
    }

    /// <summary>
    /// Der Filter hinter einer Zelle: der angezeigte Filter, zusaetzlich
    /// eingeschraenkt auf Kategorie und Zeitabschnitt dieser Zelle.
    /// </summary>
    private static ReportFilter DetailFilter(ReportZelle zelle, ReportFilter basis)
    {
        var von = basis.From;
        var bis = basis.To;

        if (zelle.PeriodenKey is string key)
        {
            var abschnitt = ReportPeriods.Range(key, basis.Grouping);

            // Der Zeitabschnitt kann ueber den Filterzeitraum hinausragen -
            // etwa das Jahr 2026 bei einem Filter ab dem 01.03.2026. Dann
            // gilt die engere Grenze, sonst zeigte der Dialog mehr als die
            // angeklickte Zelle summiert.
            von = abschnitt.From > von ? abschnitt.From : von;
            bis = abschnitt.ToExclusive < bis ? abschnitt.ToExclusive : bis;
        }

        return new ReportFilter
        {
            From = von,
            To = bis,
            // Ohne Kategorie bei Summenzeile und Summenspalte: dort bleibt
            // es beim Kategoriefilter der Filterleiste. Die Ausschluesse
            // gelten in jedem Fall weiter - wer "Haushalt ohne Restaurant"
            // eingestellt hat, will in der Detailansicht einer Zelle nicht
            // ploetzlich doch die Restaurantbuchungen sehen.
            CategoryRootIds = zelle.KategorieId is int zellenKategorie
                ? [zellenKategorie]
                : basis.CategoryRootIds,
            ExcludedCategoryIds = basis.ExcludedCategoryIds,
            Grouping = basis.Grouping,
            PayerScope = basis.PayerScope,
            PayerIds = basis.PayerIds,
            Status = basis.Status,
            SearchText = basis.SearchText,
            RecurringExpenseId = basis.RecurringExpenseId,
        };
    }

    // ---------------- Export ----------------

    public string CsvDateiname =>
        "Auswertung_" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";

    /// <summary>
    /// Der CSV-Text zur aktuell ANGEZEIGTEN Tabelle, inklusive der gerade
    /// aufgeklappten Struktur. Geschrieben wird die Datei in der Ansicht
    /// (Dateiauswahl ist reine Bedienmechanik).
    /// </summary>
    public string BaueCsv() =>
        _matrix is null
            ? string.Empty
            : ReportCsv.Build(
                _matrix,
                Zeilen.Where(zeile => zeile.Quelle is not null)
                      .Select(zeile => zeile.Quelle!)
                      .ToList());

    public void MeldeExport(string hinweis) => ExportHinweis = hinweis;

    // ---------------- Laden ----------------

    private void LadeDaten()
    {
        if (_ladenGesperrt)
        {
            return;
        }

        if (!TryBaueFilter(out var filter))
        {
            // Bei ungueltiger Zeitraumeingabe bleibt die letzte Tabelle
            // stehen; der Fehlertext erklaert, warum sich nichts bewegt.
            return;
        }

        _angezeigterFilter = filter;
        ExportHinweis = null;

        var zellen = _reportRepository.EvaluateMatrix(filter);
        var baum = _categoryRepository.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);

        // Ueber den GANZEN Baum aufloesen, nicht erst ueber den gefilterten
        // Ast: sonst ginge die geerbte Farbe eines Vorfahren verloren, der
        // ausserhalb des gewaehlten Astes liegt.
        _farben = CategoryColors.Resolve(baum);

        // Sind Kategorie-Aeste gewaehlt, beginnt die Tabelle bei diesen
        // Knoten. Sonst waere die oberste Zeile immer die Oberkategorie mit
        // exakt derselben Summe - eine Zeile ohne Aussage. Bei mehreren
        // gewaehlten Aesten stehen sie nebeneinander an der Wurzel.
        var wurzeln = filter.CategoryRootIds.Count > 0
            ? filter.CategoryRootIds
                .Select(astId => FindeKnoten(baum, astId))
                .OfType<CategoryNode>()
                .ToList()
            : baum;

        _matrix = ReportMatrixBuilder.Build(wurzeln, pfade, zellen, filter.Grouping);

        Spalten.Clear();
        foreach (var key in _matrix.PeriodKeys)
        {
            Spalten.Add(new ReportSpalte(key, ReportPeriods.Label(key, filter.Grouping)));
        }

        BaueZeilen();

        TrefferText = _matrix.Total.Count == 1
            ? "1 Buchung in 1 Zeitabschnitt"
            : $"{_matrix.Total.Count.ToString("N0", DeDe)} Buchungen in " +
              $"{Spalten.Count} Zeitabschnitt{(Spalten.Count == 1 ? string.Empty : "en")}";
    }

    /// <summary>
    /// Flacht den Zeilenbaum auf die gerade sichtbaren Zeilen ab. Laeuft
    /// bei jedem Auf-/Zuklappen und beim Umschalten des Schalters
    /// "Kategorien ohne Ausgaben anzeigen" - ohne erneute Abfrage.
    /// </summary>
    private void BaueZeilen()
    {
        Zeilen.Clear();

        if (_matrix is null)
        {
            SummenZeile = null;
            KeineTreffer = true;
            NochNichtsErfasst = !_expenseRepository.HasAny();
            AktualisiereAktiveFilter();
            return;
        }

        FuegeZeilenEin(_matrix.Rows);

        SummenZeile = ReportZeile.FuerSumme(_matrix, Spalten);
        KeineTreffer = _matrix.Total.Count == 0;

        // Nur nachfragen, wenn nichts dasteht - sonst liefe die Abfrage bei
        // jeder Filteraenderung mit, ohne je etwas zu entscheiden.
        NochNichtsErfasst = KeineTreffer && !_expenseRepository.HasAny();

        AktualisiereAktiveFilter();
    }

    private void FuegeZeilenEin(IReadOnlyList<ReportMatrixRow> rows)
    {
        foreach (var row in rows)
        {
            if (!IstSichtbar(row))
            {
                continue;
            }

            var hatKinder = row.Children.Any(IstSichtbar);
            var aufgeklappt = hatKinder && _aufgeklappteKategorien.Contains(row.CategoryId);

            Zeilen.Add(ReportZeile.FuerKategorie(
                row, Spalten, hatKinder, aufgeklappt, CategoryColors.Of(_farben, row.CategoryId)));

            if (aufgeklappt)
            {
                FuegeZeilenEin(row.Children);
            }
        }
    }

    // Eine Kategorie ohne Buchungen im Zeitraum verschwindet - samt ihrem
    // Ast, denn ihre Werte enthalten die Unterkategorien bereits.
    private bool IstSichtbar(ReportMatrixRow row) =>
        KategorienOhneAusgabenAnzeigen || row.Total.HasValues;

    private bool TryBaueFilter(out ReportFilter filter)
    {
        filter = null!;

        DateOnly? von = null;
        if (!string.IsNullOrWhiteSpace(VonText))
        {
            if (!GermanDateInput.TryParse(VonText.Trim(), out var geparst))
            {
                ZeitraumFehler = "Ungueltiges Von-Datum (TT.MM.JJJJ).";
                return false;
            }

            von = geparst;
        }

        DateOnly? bis = null;
        if (!string.IsNullOrWhiteSpace(BisText))
        {
            if (!GermanDateInput.TryParse(BisText.Trim(), out var geparst))
            {
                ZeitraumFehler = "Ungueltiges Bis-Datum (TT.MM.JJJJ).";
                return false;
            }

            bis = geparst;
        }

        var zeitraum = DateRangePresets.FromInclusiveBounds(von, bis);
        if (zeitraum.IsEmpty)
        {
            ZeitraumFehler = "Das Bis-Datum liegt vor dem Von-Datum.";
            return false;
        }

        ZeitraumFehler = null;

        var kategorien = KategorieAuswahl();

        filter = new ReportFilter
        {
            From = zeitraum.From,
            To = zeitraum.ToExclusive,
            CategoryRootIds = kategorien.RootIds,
            ExcludedCategoryIds = kategorien.ExcludedIds,
            Grouping = AusgewaehlteGruppierung.Wert,

            // "Meine Kosten" ist der einzige Grund, den PayerScope noch
            // anzufassen - die Zahler selbst kommen als Liste.
            PayerScope = MeineKosten ? PayerScope.SelfAndOpen : PayerScope.All,
            PayerIds = ZahlerOptionen.Where(option => option.IstGewaehlt)
                                     .Select(option => option.Id).ToList(),

            Status = (StatusOffen ? SettlementStatus.Offene : SettlementStatus.Alle)
                   | (StatusBeglichen ? SettlementStatus.Beglichene : SettlementStatus.Alle),

            // Gleiche Haekchen, gleiche Regel wie in der Ausgabenliste:
            // gleich gesetzt (beide an ODER beide aus) heisst "beides" und
            // schraenkt nicht ein.
            IsIncome = NurEinnahmen == NurAusgaben ? null : NurEinnahmen,

            SearchText = string.IsNullOrWhiteSpace(Suchtext) ? null : Suchtext.Trim(),
        };

        return true;
    }

    private void SetzeVorgabeZeitraum()
    {
        var zeitraum = DateRangePresets.ThisYear(DateOnly.FromDateTime(DateTime.Now));
        VonText = GermanDateInput.ToText(zeitraum.From);
        BisText = GermanDateInput.ToText(zeitraum.ToExclusive.AddDays(-1));
    }

    private void LadeKategorieAuswahl()
    {
        // Die Auswahl ueber den Neuaufbau retten - siehe die gleiche
        // Stelle in AusgabenlisteViewModel.
        var angehakteKategorien = new HashSet<int>();
        SammleAngehakte(KategorieWurzeln, angehakteKategorien);

        var angehakteZahler = ZahlerOptionen
            .Where(option => option.IstGewaehlt)
            .Select(option => option.Id)
            .ToHashSet();

        KategorieWurzeln.Clear();
        _kategorieNamen = new Dictionary<int, string>();

        var baum = _categoryRepository.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);
        foreach (var knoten in BaueFilterKnoten(baum, pfade, null))
        {
            KategorieWurzeln.Add(knoten);
        }

        StelleHaekchenWiederHer(KategorieWurzeln, angehakteKategorien);

        ZahlerOptionen.Clear();
        foreach (var person in _personRepository.GetAllActive())
        {
            var option = new ZahlerOption(person.Name, person.Id, person.IsSelf)
            {
                BeiAenderung = FilterAuswahlGeaendert,
            };

            if (angehakteZahler.Contains(person.Id))
            {
                option.SetzeStill(true);
            }

            ZahlerOptionen.Add(option);
        }

        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));
    }

    private static void SammleAngehakte(
        IEnumerable<KategorieFilterKnoten> knoten, HashSet<int> ziel)
    {
        foreach (var k in knoten)
        {
            if (k.IstGewaehlt)
            {
                ziel.Add(k.Id);
            }

            SammleAngehakte(k.Children, ziel);
        }
    }

    private static void StelleHaekchenWiederHer(
        IEnumerable<KategorieFilterKnoten> knoten, IReadOnlySet<int> angehakt)
    {
        foreach (var k in knoten)
        {
            k.SetzeStill(angehakt.Contains(k.Id));
            StelleHaekchenWiederHer(k.Children, angehakt);
        }
    }

    private List<KategorieFilterKnoten> BaueFilterKnoten(
        IReadOnlyList<CategoryNode> nodes,
        IReadOnlyDictionary<int, string> pfade,
        int? elternId)
    {
        var ergebnis = new List<KategorieFilterKnoten>();

        foreach (var node in nodes)
        {
            // Archivierte Kategorien bleiben waehlbar: ihre Ausgaben sind
            // Teil der Historie und muessen auswertbar bleiben.
            var knoten = new KategorieFilterKnoten(
                node.Category.Id,
                node.Category.Name,
                pfade[node.Category.Id],
                node.Category.IsArchived,
                elternId)
            {
                BeiAenderung = FilterAuswahlGeaendert,
            };

            _kategorieNamen[node.Category.Id] = node.Category.Name;

            knoten.Children.AddRange(
                BaueFilterKnoten(node.Children, pfade, node.Category.Id));
            ergebnis.Add(knoten);
        }

        return ergebnis;
    }

    private static CategoryNode? FindeKnoten(IReadOnlyList<CategoryNode> nodes, int id)
    {
        foreach (var node in nodes)
        {
            if (node.Category.Id == id)
            {
                return node;
            }

            var gefunden = FindeKnoten(node.Children, id);
            if (gefunden is not null)
            {
                return gefunden;
            }
        }

        return null;
    }
}
