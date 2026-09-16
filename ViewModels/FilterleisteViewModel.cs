using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die Filterleiste, die sich Ausgabenliste und Auswertung teilen:
/// Zeitraum, Kategorien, Zahler, Status, Buchungsart, Suche, die Chips
/// darunter und die gespeicherten Filterstaende.
///
/// Warum eine gemeinsame Oberklasse und nicht zwei Leisten: es IST
/// dieselbe Leiste und dieselbe Frage ("Auto, dieses Jahr"). Sie teilen
/// sich bereits die gespeicherten Filter (<see cref="SavedFilters"/>) -
/// ein in der Auswertung abgelegter Filter muss in der Ausgabenliste
/// dasselbe bedeuten, sonst waere die geteilte Liste eine Falle. Vorher
/// standen dafuer rund 500 Zeilen zweimal da, an mehreren Stellen mit dem
/// Kommentar "siehe die gleiche Stelle in AusgabenlisteViewModel"; jede
/// Aenderung an der Leiste war damit eine Aenderung an zwei Dateien, und
/// die zweite wurde vergessen.
///
/// Was die beiden Bereiche NICHT teilen, bleibt bei ihnen: die Sortierung
/// und die Sprungfilter der Ausgabenliste, Gruppierung und Kreuztabelle
/// der Auswertung. Dafuer gibt es die wenigen ueberschreibbaren Stellen
/// weiter unten - und zwar bewusst wenige: eine Oberklasse, die fuer
/// jeden Unterschied einen Haken anbietet, ist wieder zwei Klassen in
/// einer.
///
/// Die pruefbare Logik steckt nach wie vor in Core (Regel 7) -
/// <see cref="DateRangePresets"/>, <see cref="FilterChips"/>,
/// <see cref="FilterCaption"/>, <see cref="CategoryFilterSelection"/>,
/// <see cref="SavedFilters"/>. Hier stehen nur Bindung und
/// Auswahlzustand.
/// </summary>
public abstract partial class FilterleisteViewModel : ViewModelBase
{
    protected readonly CategoryRepository CategoryRepository;
    protected readonly PersonRepository PersonRepository;
    protected readonly AppSettingsStore SettingsStore;

    /// <summary>
    /// Unterdrueckt das Neuladen, solange mehrere Filterwerte auf einmal
    /// gesetzt werden (Schnellwahl, Zuruecksetzen, Neuaufbau der
    /// Auswahllisten) - sonst laeuft die Abfrage pro Eigenschaft erneut.
    /// </summary>
    protected bool LadenGesperrt;

    // Namen der Kategorien fuer die Beschriftung oben - beim Aufbau des
    // Baums mitgefuellt, damit die Beschriftung nicht jedes Mal durch den
    // Baum laufen muss.
    private Dictionary<int, string> _kategorieNamen = new();

    protected FilterleisteViewModel(
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        AppSettingsStore settingsStore)
    {
        CategoryRepository = categoryRepository;
        PersonRepository = personRepository;
        SettingsStore = settingsStore;
    }

    // ---------------- Was die Unterklassen beisteuern ----------------

    /// <summary>
    /// Traeger und Summe neu holen. Wird nach jeder Filteraenderung
    /// gerufen - was dabei geladen wird, weiss nur der Bereich.
    /// </summary>
    protected abstract void LadeDaten();

    /// <summary>
    /// Die Gruppierung, die in den fertigen <see cref="ReportFilter"/>
    /// geht. Die Ausgabenliste gruppiert nicht und bleibt bei der Vorgabe.
    /// </summary>
    protected virtual ReportGrouping FilterGruppierung => ReportGrouping.Month;

    /// <summary>
    /// Einschraenkung auf eine Vorlage, falls der Bereich so etwas kennt
    /// (Ausgabenliste: Sprung aus der Vorlagenverwaltung).
    /// </summary>
    protected virtual int? FilterVorlageId => null;

    /// <summary>Einschraenkung auf eine einzelne Buchung.</summary>
    protected virtual int? FilterBuchungId => null;

    /// <summary>
    /// Die Gruppierung, die MIT GESPEICHERT wird. NULL heisst "dieser
    /// Bereich kennt keine" - ein so abgelegter Filter laesst die
    /// Gruppierung der Auswertung dann in Ruhe.
    /// </summary>
    protected virtual ReportGrouping? GespeicherteGruppierung => null;

    /// <summary>
    /// Die bereichseigenen Filter auf ihren Vorgabewert zuruecksetzen -
    /// gerufen aus <see cref="FilterZuruecksetzen"/>, innerhalb der
    /// Ladesperre.
    /// </summary>
    protected virtual void SetzeEigeneVorgaben()
    {
    }

    /// <summary>
    /// Die bereichseigenen EINSCHRAENKUNGEN aufheben, bevor ein
    /// gespeicherter Filter angewendet wird. Bliebe etwa die
    /// Einschraenkung auf eine Vorlage stehen, waere die Liste hinterher
    /// raetselhaft leer, obwohl der gewaehlte Filter passt.
    /// </summary>
    protected virtual void LeereEigeneEinschraenkungen()
    {
    }

    /// <summary>
    /// Die bereichseigenen Werte aus einem gespeicherten Filter
    /// uebernehmen (Auswertung: die Gruppierung).
    /// </summary>
    protected virtual void UebernimmEigeneWerte(SavedFilter filter)
    {
    }

    /// <summary>
    /// Weitere Auswahllisten mitladen, wenn der Bereich welche hat
    /// (Ausgabenliste: die Zielkategorien der Sammelaktion).
    /// </summary>
    protected virtual void LadeWeitereAuswahllisten()
    {
    }

    // ---------------- Auswahllisten ----------------

    public ObservableCollection<KategorieFilterKnoten> KategorieWurzeln { get; } = new();

    public ObservableCollection<ZahlerOption> ZahlerOptionen { get; } = new();

    // ---------------- Zeitraum ----------------

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

    /// <summary>
    /// Welcher Schnellwahl-Zeitraum zuletzt gewaehlt wurde (sichtbarer
    /// aktiver Zustand der Schnellwahl-Knoepfe). NULL, sobald Von/Bis von
    /// Hand veraendert werden.
    /// </summary>
    [ObservableProperty]
    private string? _aktiverZeitraumSchluessel;

    // Jede Filteraenderung laedt neu - die Summe in der Fusszeile muss
    // sich mit jedem Filter mitbewegen. LadenGesperrt unterscheidet eine
    // Handeingabe (loescht die Schnellwahl-Markierung) von einer durch
    // Schnellwahl() selbst gesetzten Aenderung.
    partial void OnVonTextChanged(string value)
    {
        if (!LadenGesperrt)
        {
            AktiverZeitraumSchluessel = null;
        }

        LadeDaten();
    }

    partial void OnBisTextChanged(string value)
    {
        if (!LadenGesperrt)
        {
            AktiverZeitraumSchluessel = null;
        }

        LadeDaten();
    }

    protected void SetzeVorgabeZeitraum()
    {
        var zeitraum = DateRangePresets.ThisYear(DateOnly.FromDateTime(DateTime.Now));
        VonText = GermanDateInput.ToText(zeitraum.From);
        BisText = GermanDateInput.ToText(zeitraum.ToExclusive.AddDays(-1));
    }

    [RelayCommand]
    private void Schnellwahl(string? bereich)
    {
        LadenGesperrt = true;
        var gesetzt = WendeSchnellwahlAn(bereich);
        LadenGesperrt = false;

        if (gesetzt)
        {
            LadeDaten();
        }
    }

    /// <summary>
    /// Legt Von/Bis auf einen Schnellwahl-Zeitraum. Liefert false bei
    /// einem unbekannten Schluessel - dann bleibt alles stehen.
    ///
    /// Laedt bewusst NICHT neu und schliesst die Ladesperre nicht selbst:
    /// beim Anwenden eines gespeicherten Filters ist der Zeitraum nur
    /// einer von mehreren Werten, und die Liste soll danach einmal
    /// geladen werden und nicht je Feld.
    /// </summary>
    private bool WendeSchnellwahlAn(string? bereich)
    {
        var heute = DateOnly.FromDateTime(DateTime.Now);

        // Welcher Schluessel welchen Zeitraum meint, steht in Core
        // (Regel 7) - und zwar fuer beide Filterleisten gemeinsam, damit
        // ein in der Auswertung gespeicherter Filter hier dasselbe
        // bedeutet.
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

    // ---------------- Filterwerte ----------------

    /// <summary>
    /// Status-Haekchen, kombinierbar. Beide aus = keine Einschraenkung;
    /// beide an ist NICHT dasselbe (Regel 4, siehe SettlementStatus).
    /// </summary>
    [ObservableProperty]
    private bool _statusOffen;

    [ObservableProperty]
    private bool _statusBeglichen;

    /// <summary>
    /// "Meine Kosten": eigene Buchungen plus alles, was von anderen noch
    /// offen ist (<see cref="PayerScope.SelfAndOpen"/>). Bewusst ein
    /// eigener Schalter und keine Zahler-Haekchen - die Bedingung mischt
    /// Zahler, Status und Buchungstyp und laesst sich aus einzelnen
    /// Personen nicht zusammensetzen.
    /// </summary>
    [ObservableProperty]
    private bool _meineKosten;

    /// <summary>
    /// Einschraenkung auf einen Buchungstyp. Zwei unabhaengige Haekchen
    /// nach dem Muster von <see cref="StatusOffen"/>/<see cref="StatusBeglichen"/>:
    /// beide aus (und ebenso beide an) heisst "alles", weil eine Buchung
    /// nicht zugleich Einnahme und Ausgabe sein kann.
    /// </summary>
    [ObservableProperty]
    private bool _nurEinnahmen;

    [ObservableProperty]
    private bool _nurAusgaben;

    [ObservableProperty]
    private string _suchtext = string.Empty;

    partial void OnStatusOffenChanged(bool value) => LadeDaten();
    partial void OnStatusBeglichenChanged(bool value) => LadeDaten();
    partial void OnMeineKostenChanged(bool value) => LadeDaten();
    partial void OnNurEinnahmenChanged(bool value) => LadeDaten();
    partial void OnNurAusgabenChanged(bool value) => LadeDaten();
    partial void OnSuchtextChanged(string value) => LadeDaten();

    /// <summary>Beschriftung der Kategorie-Auswahl in der Filterleiste.</summary>
    public string KategorieFilterText =>
        FilterCaption.Categories(KategorieAuswahl(), _kategorieNamen);

    /// <summary>Beschriftung der Zahler-Auswahl in der Filterleiste.</summary>
    public string ZahlerFilterText => FilterCaption.Payers(
        ZahlerOptionen.Where(option => option.IstGewaehlt)
                      .Select(option => option.Bezeichnung).ToList());

    /// <summary>
    /// Ein Haekchen im Kategorie-Baum oder in der Zahlerliste wurde
    /// umgestellt. Die beiden Beschriftungen haengen an einer Auswahl, die
    /// kein einzelnes beobachtbares Feld ist - sie muessen deshalb von
    /// Hand angestossen werden.
    /// </summary>
    protected void FilterAuswahlGeaendert()
    {
        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));
        LadeDaten();
    }

    /// <summary>
    /// Die angehakten Kategorien als Aeste und Ausschluesse. Die
    /// Umrechnung selbst steht in Core (Regel 7), hier wird nur
    /// eingesammelt, was im Baum angehakt ist.
    /// </summary>
    protected CategoryFilterChoice KategorieAuswahl()
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

    // ---------------- Ein- und Ausklappen ----------------

    /// <summary>
    /// Ob die Filterleiste ihre Felder zeigt. Eingeklappt bleibt eine
    /// Zeile mit Suche, den gespeicherten Filtern und dem Klapp-Pfeil
    /// stehen; worauf gefiltert wird, sagen die Chips darunter.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterKlappZeichen))]
    [NotifyPropertyChangedFor(nameof(FilterKlappHinweis))]
    private bool _filterAufgeklappt = true;

    /// <summary>
    /// Der Pfeil am Klappknopf: aufgeklappt zeigt er nach oben,
    /// eingeklappt nach unten.
    ///
    /// Das ist die uebliche Leserichtung solcher Knoepfe (Carbon Design
    /// System, "Accordion": chevron down = zu, chevron up = offen). Der
    /// Pfeil sagt damit zugleich, was ein Klick tut - nach oben
    /// zusammenschieben, nach unten aufziehen.
    /// </summary>
    public string FilterKlappZeichen => FilterAufgeklappt ? "▲" : "▼";

    // Sobald der Anwender selbst auf- oder zugeklappt hat, entscheidet er
    // und nicht mehr die Fensterbreite. Ein Umschalten, das gleich wieder
    // von selbst zurueckspringt, ist schlimmer als gar keines.
    private bool _klappzustandVonHand;

    [RelayCommand]
    private void FilterUmschalten()
    {
        _klappzustandVonHand = true;
        FilterAufgeklappt = !FilterAufgeklappt;
    }

    /// <summary>
    /// Meldet die verfuegbare Breite der Filterleiste. Wird von der
    /// Ansicht beim Groessenwechsel gerufen - wie breit ein Bereich
    /// tatsaechlich ist, weiss nur sie; ob das reicht, entscheidet Core
    /// (<see cref="Filterleiste"/>).
    /// </summary>
    public void PasseAnBreiteAn(double breite)
    {
        if (_klappzustandVonHand)
        {
            return;
        }

        FilterAufgeklappt = Filterleiste.PasstAufgeklappt(breite);
    }

    // ---------------- Aktive Filter als Chips ----------------

    /// <summary>
    /// Jeder gesetzte Filter als wegklickbarer Chip. Steht IMMER unter der
    /// Leiste, auch eingeklappt - das ist die Bedingung dafuer, dass die
    /// Leiste ueberhaupt verschwinden darf.
    /// </summary>
    public ObservableCollection<FilterChip> AktiveFilter { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAktiveFilter))]
    [NotifyPropertyChangedFor(nameof(FilterKlappHinweis))]
    private int _aktiveFilterAnzahl;

    public bool HatAktiveFilter => AktiveFilterAnzahl > 0;

    /// <summary>
    /// Beschriftung des Klappknopfs - er traegt nur einen Pfeil und
    /// braucht deshalb einen Satz, der sagt, was er tut.
    ///
    /// Die Anzahl der gesetzten Filter steht mit darin: sie stand vorher
    /// als "Filter (3)" auf dem Knopf und darf nicht ersatzlos
    /// verschwinden. WORAUF gefiltert wird, sagen weiterhin die Chips -
    /// die bleiben auch eingeklappt stehen.
    /// </summary>
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

    protected void AktualisiereAktiveFilter()
    {
        var auswahl = KategorieAuswahl();

        var chips = FilterChips.Bestimme(new FilterZustand
        {
            // Der Vorgabezeitraum ist kein Filter - nur eine ausdrueckliche
            // Abweichung bekommt einen Chip.
            Zeitraum = ZeitraumWeichtAb() ? $"{VonText} – {BisText}" : null,
            Kategorien = auswahl.RootIds.Count == 0 && auswahl.ExcludedIds.Count == 0
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

    // Weicht der eingestellte Zeitraum vom Vorgabezeitraum (laufendes
    // Jahr) ab? Verglichen wird der TEXT und nicht das Datum: genau der
    // steht in den beiden Feldern, und genau er wuerde im Chip stehen.
    private bool ZeitraumWeichtAb()
    {
        var vorgabe = DateRangePresets.ThisYear(DateOnly.FromDateTime(DateTime.Now));

        return VonText != GermanDateInput.ToText(vorgabe.From)
            || BisText != GermanDateInput.ToText(vorgabe.ToExclusive.AddDays(-1));
    }

    /// <summary>
    /// Hebt genau einen Filter auf. Der Weg vom Chip zurueck - ohne ihn
    /// muesste der Anwender die Leiste aufklappen und das Feld suchen,
    /// und dann waere das Einklappen ein Rueckschritt.
    /// </summary>
    [RelayCommand]
    private void FilterAufheben(FilterChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        LadenGesperrt = true;

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

        LadenGesperrt = false;

        LadeDaten();
    }

    // ---------------- Auswahl leeren und zuruecksetzen ----------------

    /// <summary>
    /// Hebt die Kategorie-Auswahl auf: kein Haekchen mehr, also wieder
    /// alle Kategorien. Ein Command statt einer Bindung, weil der Baum in
    /// einem Flyout steckt und der Knopf daneben steht.
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

    /// <summary>
    /// Leert die gesamte Auswahl der Filterleiste, ohne dabei je Haekchen
    /// neu zu laden - die Aufrufer laden anschliessend selbst.
    /// </summary>
    protected void FilterAuswahlLeeren()
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
    private void FilterZuruecksetzen()
    {
        LadenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        SetzeEigeneVorgaben();
        SetzeVorgabeZeitraum();
        LadenGesperrt = false;

        LadeDaten();
    }

    // ---------------- Gespeicherte Filter ----------------

    /// <summary>
    /// Die benannten Filtereinstellungen hinter dem Knopf "Gespeichert"
    /// (siehe <see cref="SavedFilters"/>).
    ///
    /// Geteilt von Ausgabenliste und Auswertung: es ist dieselbe Leiste
    /// und dieselbe Frage ("Auto, dieses Jahr"). Eine Liste, die je nach
    /// Bereich anders aussieht, muesste sich der Anwender zweimal merken.
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

    /// <summary>
    /// Liest die Liste aus der Einstellungsdatei. Beim Aufbau und nach
    /// jeder Aenderung, und ausserdem beim Betreten des Bereichs: die
    /// Datei ist die einzige Wahrheit, und ein im anderen Bereich
    /// gespeicherter Filter soll hier ohne Neustart auftauchen.
    /// </summary>
    protected void LadeGespeicherteFilter()
    {
        GespeicherteFilter.Clear();

        foreach (var filter in SettingsStore.Load().SavedFilters)
        {
            GespeicherteFilter.Add(filter);
        }

        GespeicherteFilterAnzahl = GespeicherteFilter.Count;
    }

    /// <summary>
    /// Legt den aktuellen Filterstand unter dem eingegebenen Namen ab.
    /// Ein schon vergebener Name ersetzt seinen Eintrag - so bessert man
    /// einen Filter nach, ohne den alten vorher loeschen zu muessen.
    /// </summary>
    [RelayCommand]
    private void FilterSpeichern()
    {
        var einstellungen = SettingsStore.Load();

        FilterNameFehler = SavedFilters.Validate(FilterName, einstellungen.SavedFilters);
        if (FilterNameFehler is not null)
        {
            return;
        }

        var liste = SavedFilters.Save(einstellungen.SavedFilters, SammleFilter(FilterName));

        // Regel 13: das Namensfeld wird erst geraeumt, wenn geschrieben
        // wurde. Schlaegt das Schreiben fehl, steht der Name noch da und
        // ein zweiter Versuch kostet keine Tipparbeit.
        FilterNameFehler = Schreibvorgang.Versuche(
            "Beim Speichern eines Filters",
            () => SettingsStore.Save(einstellungen with { SavedFilters = liste })) is null
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
    /// Stellt die Leiste auf einen gespeicherten Filter um.
    ///
    /// Wie beim Zuruecksetzen wird vorher ALLES geraeumt - auch die
    /// bereichseigenen Einschraenkungen. Bliebe eine davon stehen, waere
    /// die Liste hinterher raetselhaft leer, obwohl der gewaehlte Filter
    /// passt.
    /// </summary>
    [RelayCommand]
    private void GespeichertenFilterAnwenden(SavedFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        LadenGesperrt = true;

        FilterAuswahlLeeren();
        LeereEigeneEinschraenkungen();

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

        UebernimmEigeneWerte(filter);

        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));

        LadenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Loescht einen gespeicherten Filter. Ohne Rueckfrage: an den Daten
    /// aendert das nichts, und der Filter ist mit denselben Haekchen in
    /// einem Augenblick wieder angelegt.
    /// </summary>
    [RelayCommand]
    private void GespeichertenFilterLoeschen(SavedFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        var einstellungen = SettingsStore.Load();
        var liste = SavedFilters.Remove(einstellungen.SavedFilters, filter.Name);

        if (Schreibvorgang.Versuche(
                "Beim Loeschen eines Filters",
                () => SettingsStore.Save(einstellungen with { SavedFilters = liste })) is not null)
        {
            // Der Eintrag bleibt stehen - er ist ja noch da. Das
            // Uebrige steht im Protokoll.
            return;
        }

        LadeGespeicherteFilter();
    }

    /// <summary>
    /// Der Filterstand, so wie er in der Leiste steht. Roh und nicht als
    /// fertiger <see cref="ReportFilter"/> - siehe
    /// <see cref="SavedFilter"/>.
    /// </summary>
    private SavedFilter SammleFilter(string name)
    {
        var ueberSchnellwahl = AktiverZeitraumSchluessel is not null;

        return new SavedFilter
        {
            Name = name,
            PeriodKey = AktiverZeitraumSchluessel,

            // Von Hand eingetragene Grenzen nur dann, wenn keine
            // Schnellwahl aktiv ist: sonst stuenden beide Angaben in der
            // Datei und die zweite waere schon morgen falsch.
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

            // Die Gruppierung gehoert zum Stand, wo es eine gibt: "Auto
            // nach Jahren" ist eine andere Frage als "Auto nach Monaten",
            // und sie wieder von Hand umzustellen waere der halbe Weg.
            Grouping = GespeicherteGruppierung,
        };
    }

    // ---------------- Der fertige Filter ----------------

    /// <summary>
    /// Baut aus der Leiste den Filter fuer die Abfrage. Liefert false bei
    /// einer unlesbaren Datumsangabe - dann steht der Grund in
    /// <see cref="ZeitraumFehler"/> und die Aufrufer lassen die zuletzt
    /// geladenen Traeger stehen.
    /// </summary>
    protected bool TryBaueFilter(out ReportFilter filter)
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
            Grouping = FilterGruppierung,

            // "Meine Kosten" ist der einzige Grund, den PayerScope noch
            // anzufassen - die Zahler selbst kommen als Liste.
            PayerScope = MeineKosten ? PayerScope.SelfAndOpen : PayerScope.All,
            PayerIds = ZahlerOptionen.Where(option => option.IstGewaehlt)
                                     .Select(option => option.Id).ToList(),

            Status = (StatusOffen ? SettlementStatus.Offene : SettlementStatus.Alle)
                   | (StatusBeglichen ? SettlementStatus.Beglichene : SettlementStatus.Alle),

            SearchText = string.IsNullOrWhiteSpace(Suchtext) ? null : Suchtext.Trim(),

            // Beide Haekchen zusammen sind dasselbe wie keines: eine
            // Buchung ist entweder Einnahme oder Ausgabe, nie beides.
            IsIncome = NurEinnahmen == NurAusgaben ? null : NurEinnahmen,

            RecurringExpenseId = FilterVorlageId,
            ExpenseId = FilterBuchungId,
        };

        return true;
    }

    // ---------------- Auswahllisten aufbauen ----------------

    /// <summary>
    /// Baut Kategoriebaum und Zahlerliste neu auf. Auch waehrend der
    /// Sitzung noetig (neue Kategorie, neue Person) - die angehakte
    /// Auswahl wird dabei ueber den Neuaufbau gerettet, denn ein
    /// stillschweigend geleerter Filter waere ein Raetsel.
    /// </summary>
    protected void LadeAuswahllisten()
    {
        var angehakteKategorien = new HashSet<int>();
        SammleAngehakte(KategorieWurzeln, angehakteKategorien);

        var angehakteZahler = ZahlerOptionen
            .Where(option => option.IstGewaehlt)
            .Select(option => option.Id)
            .ToHashSet();

        KategorieWurzeln.Clear();
        _kategorieNamen = new Dictionary<int, string>();

        var baum = CategoryRepository.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);
        foreach (var knoten in BaueFilterKnoten(baum, pfade, null))
        {
            KategorieWurzeln.Add(knoten);
        }

        // Erst nach dem Aufbau anhaken: das Haekchen kaskadiert in den
        // Unterbaum, und beim Aufbau von oben nach unten wuerde es die
        // gerettete Auswahl der Kinder wieder ueberschreiben.
        StelleHaekchenWiederHer(KategorieWurzeln, angehakteKategorien);

        LadeWeitereAuswahllisten();

        ZahlerOptionen.Clear();
        foreach (var person in PersonRepository.GetAllActive())
        {
            var option = new ZahlerOption(person.Name, person.Id, person.IsSelf)
            {
                BeiAenderung = FilterAuswahlGeaendert,
            };

            // Ein inzwischen archivierter Zahler faellt aus der Auswahl -
            // sein Haekchen verschwindet mit ihm, statt auf einen Eintrag
            // zu zeigen, den es nicht mehr gibt.
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
            // Von oben nach unten, aber jeweils still und einzeln: so
            // bleibt ein abgewaehltes Kind unter einem angehakten
            // Elternteil abgewaehlt.
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
            // Teil der Historie und muessen auffindbar bleiben.
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

    // ---------------- Leerzustand, Erfassen, Export ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTrefferTrotzDaten))]
    private bool _keineTreffer;

    /// <summary>
    /// Die Liste ist leer, WEIL es noch gar keine Buchung gibt - nicht,
    /// weil der Filter zu eng steht. Der Unterschied entscheidet, was der
    /// Leerzustand anbietet: die Erfassungsmaske oder das Zuruecksetzen
    /// des Filters. Beides sieht gleich leer aus, ist aber nicht dasselbe,
    /// und ein Angebot, das an der Lage vorbeigeht, ist schlimmer als
    /// keines.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeinTrefferTrotzDaten))]
    private bool _nochNichtsErfasst;

    /// <summary>Leer, obwohl es Buchungen gibt - dann liegt es am Filter.</summary>
    public bool KeinTrefferTrotzDaten => KeineTreffer && !NochNichtsErfasst;

    /// <summary>
    /// Bitte um den Wechsel in die Erfassungsmaske. Der Leerzustand bietet
    /// ihn an; den Bereichswechsel kann nur das Hauptfenster.
    /// </summary>
    public event EventHandler? ErfassenAngefordert;

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Rueckmeldung nach einem CSV-Export, von der Ansicht gesetzt - nur
    /// sie weiss, ob der Anwender den Dateidialog abgebrochen hat.
    /// </summary>
    [ObservableProperty]
    private string? _exportHinweis;

    public void MeldeExport(string hinweis) => ExportHinweis = hinweis;
}
