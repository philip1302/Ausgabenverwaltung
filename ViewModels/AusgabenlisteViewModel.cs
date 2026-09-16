using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Ausgabenliste": alle erfassten Ausgaben suchen, pruefen und
/// korrigieren. Gefiltert, sortiert und summiert wird komplett in SQL
/// (<see cref="ExpenseRepository.Query"/> und
/// <see cref="ExpenseRepository.Summarize"/>), die Zeitraumberechnung
/// steckt in <see cref="DateRangePresets"/> - hier bleiben nur Bindung,
/// Formatierung und Auswahlzustand (Regel 7). Das Filtermodell ist das
/// bestehende <see cref="ReportFilter"/>.
/// </summary>
public sealed partial class AusgabenlisteViewModel : ViewModelBase
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private readonly ExpenseRepository _expenseRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PersonRepository _personRepository;
    private readonly AppSettingsStore _settingsStore;
    private readonly IMessenger _messenger;

    // Unterdrueckt das Neuladen, solange mehrere Filterwerte auf einmal
    // gesetzt werden (Schnellwahl, Zuruecksetzen, Neuaufbau der
    // Auswahllisten) - sonst laeuft die Abfrage pro Eigenschaft erneut.
    private bool _ladenGesperrt;

    // Die zuletzt geladenen Buchungen, so wie sie gerade in der Liste
    // stehen. Gemerkt fuer den CSV-Export: exportiert werden soll genau das
    // Angezeigte, und die Anzeigezeilen (AusgabeZeile) tragen nur fertig
    // formatierte Texte, aus denen sich kein rechenbares CSV bauen laesst.
    private IReadOnlyList<ExpenseListItem> _angezeigteBuchungen = Array.Empty<ExpenseListItem>();

    // Der Weg zurueck aus dem letzten umkehrbaren Vorgang - das, was das
    // Rueckgaengig-Band gerade anbietet. NULL, solange es nichts
    // zurueckzunehmen gibt. Gehalten wird er bis zum Bereichswechsel (siehe
    // AktualisiereListe), bewusst ohne Zeitablauf: ein Band, das von selbst
    // verschwindet, nimmt dem Anwender die Entscheidung ab.
    private Ruecknahme? _ruecknahme;

    /// <summary>
    /// Ein umkehrbarer Vorgang, solange das Band steht. Es gibt ihn fuer
    /// das Loeschen (die Buchungen werden wieder angelegt) und fuer das
    /// Abhaken (der Beglichen-Stand wird zurueckgeschrieben) - das Band
    /// kennt den Unterschied nicht, es fuehrt nur aus.
    /// </summary>
    /// <param name="Anlass">
    /// Der Anlasstext fuer <see cref="Schreibvorgang.Versuche"/> - er steht
    /// in der Fehlermeldung, wenn die Ruecknahme selbst scheitert.
    /// </param>
    /// <param name="Tipp">
    /// Was der Knopf "Rückgängig" tut, als Kurzhinweis am Knopf.
    /// </param>
    /// <param name="Ausfuehren">
    /// Der Weg zurueck. Liefert den Satz, der danach im Erfolgsband steht.
    /// </param>
    private sealed record Ruecknahme(
        string Anlass,
        string Tipp,
        Func<string> Ausfuehren);

    /// <summary>
    /// Bitte um einen Wechsel in den Vorlagenbereich, mit einem aus dieser
    /// Buchung vorbelegten Formular. Die Liste kennt die Navigation nicht
    /// selbst - der <see cref="MainViewModel"/> hoert zu und setzt sie um
    /// (dasselbe Muster wie bei den Spruengen der Startseite).
    /// </summary>
    public event EventHandler<int>? VorlageAusBuchungAngefordert;

    /// <summary>
    /// Bitte um einen Wechsel in die Erfassungsmaske - aus dem
    /// Leerzustand, solange noch gar nichts erfasst ist. Gleiches Muster
    /// wie <see cref="StartseiteViewModel.ErfassenAngefordert"/>.
    /// </summary>
    public event EventHandler? ErfassenAngefordert;

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Bitte um den Tastaturfokus im Suchfeld (Strg+F). Welches
    /// Bedienelement das ist, weiss nur die Ansicht - dasselbe Muster wie
    /// <see cref="ErfassenViewModel.FokusBetragAngefordert"/>.
    /// </summary>
    public event EventHandler? FokusSucheAngefordert;

    /// <summary>
    /// Ein noch nicht eingeloester Fokuswunsch. Er ueberlebt bewusst den
    /// Bereichswechsel: kommt Strg+F aus einem anderen Bereich, wird die
    /// Ansicht erst im naechsten Layoutlauf gebaut, und das Ereignis
    /// darueber liefe ins Leere. Die Ansicht loest den Wunsch dann beim
    /// Laden ein (siehe Views/AusgabenlisteView.axaml.cs).
    /// </summary>
    public bool SucheFokusOffen { get; private set; }

    public void FokussiereSuche()
    {
        SucheFokusOffen = true;
        FokusSucheAngefordert?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Meldet, dass der Zeiger tatsaechlich im Feld steht.</summary>
    public void FokusSucheErledigt() => SucheFokusOffen = false;

    public ObservableCollection<AusgabeZeile> Zeilen { get; } = new();

    public ObservableCollection<KategorieFilterKnoten> KategorieWurzeln { get; } = new();

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
    public string ZahlerFilterText => FilterCaption.Payers(
        ZahlerOptionen.Where(option => option.IstGewaehlt)
                      .Select(option => option.Bezeichnung).ToList());

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

    private void AktualisiereAktiveFilter()
    {
        var chips = FilterChips.Bestimme(new FilterZustand
        {
            // Der Vorgabezeitraum ist kein Filter - nur eine ausdrueckliche
            // Abweichung bekommt einen Chip.
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

    // ---------------- Gespeicherte Filter ----------------

    /// <summary>
    /// Die benannten Filtereinstellungen hinter dem Knopf "Gespeichert"
    /// (siehe <see cref="SavedFilters"/>).
    ///
    /// Geteilt mit der Auswertung: es ist dieselbe Leiste und dieselbe
    /// Frage ("Auto, dieses Jahr"). Eine Liste, die je nach Bereich anders
    /// aussieht, muesste sich der Anwender zweimal merken.
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
    /// Datei ist die einzige Wahrheit, und ein in der Auswertung
    /// gespeicherter Filter soll hier ohne Neustart auftauchen.
    /// </summary>
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
    /// Ein schon vergebener Name ersetzt seinen Eintrag - so bessert man
    /// einen Filter nach, ohne den alten vorher loeschen zu muessen.
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
        // wurde. Schlaegt das Schreiben fehl, steht der Name noch da und
        // ein zweiter Versuch kostet keine Tipparbeit.
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
    /// Stellt die Leiste auf einen gespeicherten Filter um.
    ///
    /// Wie beim Zuruecksetzen wird vorher ALLES geraeumt - auch die
    /// Einschraenkung auf eine Vorlage oder eine einzelne Buchung. Bliebe
    /// eine davon stehen, waere die Liste hinterher raetselhaft leer,
    /// obwohl der gewaehlte Filter passt.
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
        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

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

        OnPropertyChanged(nameof(KategorieFilterText));
        OnPropertyChanged(nameof(ZahlerFilterText));

        _ladenGesperrt = false;

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

        var einstellungen = _settingsStore.Load();
        var liste = SavedFilters.Remove(einstellungen.SavedFilters, filter.Name);

        if (Schreibvorgang.Versuche(
                "Beim Loeschen eines Filters",
                () => _settingsStore.Save(einstellungen with { SavedFilters = liste })) is not null)
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
        };
    }

    // Namen der Kategorien fuer die Beschriftung oben - beim Aufbau des
    // Baums mitgefuellt, damit die Beschriftung nicht jedes Mal durch den
    // Baum laufen muss.
    private Dictionary<int, string> _kategorieNamen = new();

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

    // Einschraenkung auf eine Vorlage. Kommt nicht aus der Filterleiste,
    // sondern ueber den Sprung aus der Vorlagenverwaltung
    // (siehe ZeigeVorlagenBuchungen) - deshalb ein eigenes Feld statt einer
    // gebundenen Auswahl.
    private int? _vorlageFilterId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorlageFilterAktiv))]
    private string? _vorlageFilterText;

    public bool VorlageFilterAktiv => VorlageFilterText is not null;

    // Einschraenkung auf genau eine Buchung. Kommt wie der Vorlagenfilter
    // nicht aus der Filterleiste, sondern ueber den Sprung aus "Letzte
    // Buchungen" (siehe ZeigeEinzelneBuchung).
    private int? _buchungFilterId;

    /// <summary>
    /// Bewusst ein sichtbarer, wegklickbarer Hinweis und kein stilles
    /// Feld: eine Id tippt niemand ein, aber ein Filter, der wirkt, ohne
    /// sich zu zeigen, ist der haeufigste Grund fuer "meine Buchungen sind
    /// weg".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuchungFilterAktiv))]
    private string? _buchungFilterText;

    public bool BuchungFilterAktiv => BuchungFilterText is not null;

    // ---------------- Sortierung ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumHeaderText))]
    [NotifyPropertyChangedFor(nameof(KategorieHeaderText))]
    [NotifyPropertyChangedFor(nameof(BetragHeaderText))]
    [NotifyPropertyChangedFor(nameof(ZahlerHeaderText))]
    [NotifyPropertyChangedFor(nameof(StatusHeaderText))]
    [NotifyPropertyChangedFor(nameof(BemerkungHeaderText))]
    private ExpenseSortColumn _sortSpalte = ExpenseSortColumn.Datum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumHeaderText))]
    [NotifyPropertyChangedFor(nameof(KategorieHeaderText))]
    [NotifyPropertyChangedFor(nameof(BetragHeaderText))]
    [NotifyPropertyChangedFor(nameof(ZahlerHeaderText))]
    [NotifyPropertyChangedFor(nameof(StatusHeaderText))]
    [NotifyPropertyChangedFor(nameof(BemerkungHeaderText))]
    private bool _sortAufsteigend;

    // Erst wenn der Konstruktor durch ist, wird eine Aenderung der
    // Sortierung gemerkt. Sonst schriebe das Lesen aus den Einstellungen
    // den gerade gelesenen Wert sofort wieder zurueck.
    private bool _sortierungGemerkt;

    // Beide Aenderungswege laufen hier zusammen: der Klick auf einen
    // Spaltenkopf UND die Ruecksetzung auf Datum/absteigend in den
    // Sprungmethoden und in "Filter zuruecksetzen". Beides ist eine
    // Anwenderhandlung und soll den Neustart ueberstehen - deshalb an der
    // Eigenschaft und nicht in den fuenf Aufrufstellen.
    partial void OnSortSpalteChanged(ExpenseSortColumn value) => MerkeSortierung();

    partial void OnSortAufsteigendChanged(bool value) => MerkeSortierung();

    private void MerkeSortierung()
    {
        if (!_sortierungGemerkt)
        {
            return;
        }

        // Ausdruecklich still: ein Klick auf einen Spaltenkopf darf keinen
        // Fehlerdialog nach sich ziehen. Fuer diese Sitzung gilt die
        // Sortierung ohnehin; verloren geht hoechstens, dass sie den
        // Neustart uebersteht.
        try
        {
            _settingsStore.Save(_settingsStore.Load() with
            {
                ExpenseListSortColumn = SortSpalte,
                ExpenseListSortAscending = SortAufsteigend,
            });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Speichern der Sortierung", ex);
        }
    }

    public string DatumHeaderText => KopfText("Datum", ExpenseSortColumn.Datum);
    public string KategorieHeaderText => KopfText("Kategorie", ExpenseSortColumn.Kategorie);
    public string BetragHeaderText => KopfText("Betrag", ExpenseSortColumn.Betrag);
    public string ZahlerHeaderText => KopfText("Zahler", ExpenseSortColumn.Zahler);
    public string StatusHeaderText => KopfText("Status", ExpenseSortColumn.Status);
    public string BemerkungHeaderText => KopfText("Bemerkung", ExpenseSortColumn.Bemerkung);

    // ---------------- Ergebnis ----------------

    [ObservableProperty]
    private string _trefferText = string.Empty;

    [ObservableProperty]
    private string _summeText = string.Empty;

    /// <summary>Die Summe der Treffer ist positiv - Einnahmen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _summeIstEinnahme;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAuswahl))]
    [NotifyPropertyChangedFor(nameof(AuswahlText))]
    [NotifyCanExecuteChangedFor(nameof(AusgewaehlteLoeschenCommand))]
    private int _anzahlAusgewaehlt;

    public bool HatAuswahl => AnzahlAusgewaehlt > 0;

    /// <summary>Beschriftung der Aktionsleiste ueber der Tabelle.</summary>
    public string AuswahlText => AnzahlAusgewaehlt == 1
        ? "1 markiert"
        : $"{AnzahlAusgewaehlt} markiert";

    /// <summary>
    /// Die waehlbaren Ziele der Sammelaktion "Kategorie ändern" - nur
    /// Blattknoten, nicht archiviert, genau wie im Bearbeiten-Dialog. Eine
    /// eigene Liste neben <see cref="KategorieWurzeln"/>: die dient dem
    /// FILTER und enthaelt deshalb auch Ober- und Zwischenkategorien, auf
    /// die sich eine Buchung gar nicht buchen laesst.
    /// </summary>
    public ObservableCollection<CategoryOption> SammelKategorien { get; } = new();

    // ---------------- Overlays ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BearbeitenAktiv))]
    private AusgabeBearbeitenViewModel? _bearbeiten;

    public bool BearbeitenAktiv => Bearbeiten is not null;

    /// <summary>
    /// Das Rueckgaengig-Band nach einem umkehrbaren Vorgang ("3 Buchungen
    /// gelöscht · Rückgängig", "1 Buchung als beglichen markiert ·
    /// Rückgängig"). Es ersetzt die fruehere Sicherheitsabfrage: eine
    /// Aktion umkehrbar zu machen ist mehr wert als eine Nachfrage, die
    /// ohnehin weggeklickt wird. Das Vorlagen-Loeschen behaelt seine
    /// Nachfrage - dort ist das Schadensausmass groesser.
    ///
    /// Jeder Vorgang, der sich zuruecknehmen laesst, meldet sich HIER und
    /// nicht im Erfolgsband: zwei Baender uebereinander lesen sich wie zwei
    /// Vorgaenge, und ein Erfolgsband ohne Knopf verschweigt, dass es
    /// zurueck geht.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckgaengigSichtbar))]
    [NotifyPropertyChangedFor(nameof(RueckgaengigTipp))]
    private string? _rueckgaengigText;

    public bool RueckgaengigSichtbar => RueckgaengigText is not null;

    /// <summary>
    /// Der Kurzhinweis am Knopf "Rückgängig" - je Vorgang ein anderer Satz,
    /// weil "Legt die gelöschten Buchungen wieder an" nach einem Abhaken
    /// etwas anderes verspricht als es tut.
    /// </summary>
    public string RueckgaengigTipp => _ruecknahme?.Tipp ?? string.Empty;

    /// <summary>
    /// Ein Schreibfehler in der Liste selbst - beim Loeschen. Als Band
    /// ueber der Tabelle, damit sichtbar bleibt, WAS nicht geklappt hat,
    /// und die Liste unveraendert darunter stehen kann.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerAlsBand))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    /// <summary>
    /// Derselbe Text als Band ueber der Tabelle - aber nur, solange keine
    /// Nachfrage offen ist. Die zeigt ihn selbst, und zweimal derselbe
    /// Satz auf einem Bildschirm liest sich wie zwei Fehler.
    /// </summary>
    public bool SchreibFehlerAlsBand =>
        SchreibFehlerText is not null && SammelAnfrageText is null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    /// <summary>
    /// Ein geglueckter Schreibvorgang, der sich nicht von selbst erklaert -
    /// bisher nur das Duplizieren. Eine neue Zeile mit heutigem Datum
    /// taucht je nach Sortierung irgendwo in der Liste auf; ohne diesen
    /// Satz bliebe offen, ob ueberhaupt etwas passiert ist.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErfolgSichtbar))]
    private string? _erfolgText;

    public bool ErfolgSichtbar => ErfolgText is not null;

    [RelayCommand]
    private void ErfolgSchliessen() => ErfolgText = null;

    // ================= CSV-Export =================

    /// <summary>
    /// Der Hinweis neben dem Export-Knopf - "Gespeichert: …" oder der
    /// Grund, warum nicht. Wie in der Auswertung ein Text neben dem Knopf
    /// und kein Band: der Export aendert keine Daten, sein Ergebnis muss
    /// niemanden aufhalten.
    /// </summary>
    [ObservableProperty]
    private string? _exportHinweis;

    /// <summary>
    /// Vorschlag fuer den Dateinamen. Der Tag steht darin, damit sich
    /// mehrere Exporte nebeneinander unterscheiden lassen.
    /// </summary>
    public string CsvDateiname =>
        "Ausgabenliste_" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";

    /// <summary>
    /// Der CSV-Text zur gerade angezeigten Liste. Gebildet aus den beim
    /// Laden gemerkten Buchungen und NICHT aus einer zweiten Abfrage:
    /// exportiert werden soll genau das, was der Anwender vor sich sieht -
    /// eine erneute Abfrage koennte inzwischen etwas anderes liefern.
    /// </summary>
    public string BaueCsv() => ReportCsv.BuildExpenseList(_angezeigteBuchungen);

    public void MeldeExport(string hinweis) => ExportHinweis = hinweis;

    public AusgabenlisteViewModel(
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        AppSettingsStore settingsStore,
        IMessenger messenger)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _settingsStore = settingsStore;
        _messenger = messenger;

        _ladenGesperrt = true;

        // Direkte Feldzuweisung: ueber die Eigenschaften wuerde
        // MerkeSortierung den gerade gelesenen Wert sofort wieder
        // zurueckschreiben (dasselbe Muster wie bei WerteBehalten in
        // ErfassenViewModel).
        var einstellungen = _settingsStore.Load();
        _sortSpalte = einstellungen.ExpenseListSortColumn;
        _sortAufsteigend = einstellungen.ExpenseListSortAscending;

        LadeAuswahllisten();
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;
        _sortierungGemerkt = true;

        LadeGespeicherteFilter();

        LadeDaten();

        // Buchungsaenderungen aus anderen Bereichen (Anlegen in "Erfassen",
        // Abhaken in "Offene Posten", Zusammenfuehren von Kategorien) sollen
        // hier sofort sichtbar werden, nicht erst beim naechsten Navigieren
        // zur Ausgabenliste (Regel 14).
        _messenger.Register<AusgabenlisteViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.LadeDaten());
    }

    /// <summary>
    /// Laedt Auswahllisten und Treffer neu. Wird bei Navigation zu diesem
    /// Bereich aufgerufen (siehe <see cref="MainViewModel"/>), damit
    /// zwischenzeitlich in "Erfassen" angelegte Buchungen und in der
    /// Verwaltung geaenderte Kategorien/Personen ohne Neustart erscheinen -
    /// die Bereichs-ViewModels sind DI-Singletons.
    /// </summary>
    public void AktualisiereListe()
    {
        // Der Bereich wird gerade betreten - das Rueckgaengig-Angebot des
        // letzten Besuchs gilt nicht mehr. Es ausdruecklich hier zu raeumen
        // ist der Preis dafuer, dass es sonst NICHT von selbst verschwindet:
        // solange der Anwender in der Liste steht, soll er sich Zeit lassen
        // duerfen.
        VergissRuecknahme();

        _ladenGesperrt = true;
        LadeAuswahllisten();
        _ladenGesperrt = false;

        // Die gespeicherten Filter koennen sich in der Auswertung
        // geaendert haben - beide Bereiche fuehren dieselbe Liste.
        LadeGespeicherteFilter();

        LadeDaten();
    }

    /// <summary>
    /// Welcher Schnellwahl-Zeitraum zuletzt gewaehlt wurde (UI/UX-Redesign,
    /// Abschnitt 5.4: sichtbarer aktiver Zustand der Schnellwahl-Buttons).
    /// NULL, sobald Von/Bis von Hand veraendert werden.
    /// </summary>
    [ObservableProperty]
    private string? _aktiverZeitraumSchluessel;

    // Jede Filteraenderung laedt neu - die Summe in der Fusszeile muss
    // sich mit jedem Filter mitbewegen. _ladenGesperrt unterscheidet eine
    // Handeingabe (loescht die Schnellwahl-Markierung) von einer durch
    // Schnellwahl() selbst gesetzten Aenderung.
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
    partial void OnStatusOffenChanged(bool value) => LadeDaten();
    partial void OnStatusBeglichenChanged(bool value) => LadeDaten();
    partial void OnMeineKostenChanged(bool value) => LadeDaten();
    partial void OnNurEinnahmenChanged(bool value) => LadeDaten();
    partial void OnNurAusgabenChanged(bool value) => LadeDaten();

    /// <summary>
    /// Ein Haekchen im Kategorie-Baum oder in der Zahlerliste wurde
    /// umgestellt. Die beiden Beschriftungen haengen an einer Auswahl, die
    /// kein einzelnes beobachtbares Feld ist - sie muessen deshalb von
    /// Hand angestossen werden.
    /// </summary>
    private void FilterAuswahlGeaendert()
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
    partial void OnSuchtextChanged(string value) => LadeDaten();

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

    [RelayCommand]
    private void FilterZuruecksetzen()
    {
        _ladenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Nimmt die Einschraenkung auf eine einzelne Buchung zurueck. Steht
    /// als eigene Methode da, weil sie an jeder Sprungmethode dabei sein
    /// muss: ueberlebte der Einzelfilter einen anderen Sprung, bliebe die
    /// Liste danach raetselhaft leer.
    /// </summary>
    private void EinzelfilterLeeren()
    {
        _buchungFilterId = null;
        BuchungFilterText = null;
    }

    /// <summary>
    /// Zeigt genau die aus einer Vorlage erzeugten Buchungen. Wird aus der
    /// Vorlagenverwaltung heraus aufgerufen (siehe
    /// <see cref="MainViewModel"/>).
    ///
    /// Der Zeitraum wird dabei geoeffnet und die uebrigen Filter geleert:
    /// die Buchungen einer Vorlage reichen typischerweise weiter zurueck
    /// als die Vorgabe "dieses Jahr", und ein stehen gebliebener
    /// Kategoriefilter wuerde sie zusaetzlich ausduennen. Der Anwender
    /// erwartet nach dem Sprung genau diese Buchungen - alle davon.
    /// </summary>
    public void ZeigeVorlagenBuchungen(int vorlageId, string vorlageTitel)
    {
        _ladenGesperrt = true;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        VonText = string.Empty;
        BisText = string.Empty;
        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;

        EinzelfilterLeeren();
        _vorlageFilterId = vorlageId;
        VorlageFilterText = $"Nur Buchungen aus Vorlage: {vorlageTitel}";
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Zeigt genau EINE Buchung. Wird aus "Letzte Buchungen" (Startseite)
    /// und "Letzte Ausgaben" (Erfassen) heraus aufgerufen: ein Klick auf
    /// eine Zeile dort soll zu genau dieser Buchung fuehren.
    ///
    /// Der Zeitraum wird dabei geoeffnet - die Vorgabe "dieses Jahr" wuerde
    /// eine nachtraeglich datierte Buchung sonst gleich wieder
    /// herausfiltern, und die Liste bliebe unerklaerlich leer.
    /// </summary>
    public void ZeigeEinzelneBuchung(int id, string beschreibung)
    {
        _ladenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        VonText = string.Empty;
        BisText = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;

        _buchungFilterId = id;
        BuchungFilterText = $"Nur diese Buchung: {beschreibung}";
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Zeigt die Buchungen EINES Monats, wahlweise nur die Einnahmen oder
    /// nur die Ausgaben. Wird aus den beiden Monatskacheln der Startseite
    /// heraus aufgerufen - die Liste soll danach genau das enthalten,
    /// woraus die Zahl auf der Kachel besteht.
    /// </summary>
    public void ZeigeMonat(DateOnly monatsAnfang, bool nurEinnahmen, bool meineKosten)
    {
        _ladenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

        VonText = GermanDateInput.ToText(monatsAnfang);
        BisText = GermanDateInput.ToText(monatsAnfang.AddMonths(1).AddDays(-1));

        NurEinnahmen = nurEinnahmen;
        NurAusgaben = !nurEinnahmen;
        MeineKosten = meineKosten;

        // Die Einnahmenkachel zaehlt nur, was tatsaechlich zugeflossen ist
        // (siehe StartseiteViewModel.AktualisiereMonatsKacheln) - eine noch
        // offene Einnahme ist kein Geld. Ohne dieses Haekchen stuenden in
        // der Liste mehr Buchungen, als die Kachel zusammengezaehlt hat.
        StatusBeglichen = nurEinnahmen;

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Zeigt genau die Buchungen eines Zeitabschnitts. Wird aus dem
    /// Diagramm der Startseite heraus aufgerufen: ein Klick auf einen
    /// Balken soll zeigen, woraus er besteht.
    ///
    /// Wie beim Vorlagensprung werden die uebrigen Filter geleert - ein
    /// stehen gebliebener Kategorie- oder Zahlerfilter wuerde die Liste
    /// ausduennen, und der Anwender erwartet nach dem Klick genau die
    /// Buchungen dieses Balkens.
    ///
    /// <paramref name="bisEinschliesslich"/> ist der letzte Tag, der noch
    /// dazugehoert - die Filterleiste versteht ihre beiden Felder
    /// einschliessend (siehe DateRangePresets.FromInclusiveBounds).
    /// </summary>
    public void ZeigeZeitraum(DateOnly von, DateOnly bisEinschliesslich)
    {
        _ladenGesperrt = true;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

        VonText = GermanDateInput.ToText(von);
        BisText = GermanDateInput.ToText(bisEinschliesslich);

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Zeigt die Buchungen einer Kategorie in einem Zeitraum. Wird aus dem
    /// Jahresrueckblick heraus aufgerufen: hinter jeder Karte und jeder
    /// Tabellenzeile dort stehen genau diese Buchungen.
    ///
    /// Der Rueckblick schraenkt weder nach Zahler noch nach
    /// Begleichungsstand ein - deshalb werden hier auch nur Zeitraum und
    /// Kategorie gesetzt, und die Liste zeigt anschliessend dieselbe Menge,
    /// aus der die Karte gerechnet wurde.
    ///
    /// <paramref name="kategorieId"/> NULL bedeutet "alle Kategorien";
    /// dahinter stehen die Monatskarten, die zu keiner Kategorie gehoeren.
    /// Ein angehakter Knoten meint immer seinen ganzen Ast - genau wie die
    /// Zeile im Rueckblick, die ihre Unterkategorien mitzaehlt.
    /// </summary>
    public void ZeigeKategorieZeitraum(
        int? kategorieId, DateOnly von, DateOnly bisEinschliesslich)
    {
        _ladenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

        VonText = GermanDateInput.ToText(von);
        BisText = GermanDateInput.ToText(bisEinschliesslich);

        if (kategorieId is int id)
        {
            HakeKategorieAn(KategorieWurzeln, id);
        }

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;
        _ladenGesperrt = false;

        OnPropertyChanged(nameof(KategorieFilterText));
        LadeDaten();
    }

    private static bool HakeKategorieAn(
        IEnumerable<KategorieFilterKnoten> knoten, int kategorieId)
    {
        foreach (var k in knoten)
        {
            if (k.Id == kategorieId)
            {
                // Still, weil der Aufrufer anschliessend selbst laedt - ein
                // Haekchen, das den ganzen Ast mitzieht, loeste sonst je
                // Unterkategorie eine Neuladung aus.
                k.SetzeStill(true);
                return true;
            }

            if (HakeKategorieAn(k.Children, kategorieId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hebt die Einschraenkung auf eine Vorlage auf, ohne die uebrigen
    /// Filter anzufassen.
    /// </summary>
    [RelayCommand]
    private void VorlagenFilterEntfernen()
    {
        _vorlageFilterId = null;
        VorlageFilterText = null;
        LadeDaten();
    }

    /// <summary>
    /// Hebt die Einschraenkung auf eine einzelne Buchung auf, ohne die
    /// uebrigen Filter anzufassen - das Gegenstueck zu
    /// <see cref="VorlagenFilterEntfernen"/>.
    /// </summary>
    [RelayCommand]
    private void BuchungFilterAufheben()
    {
        EinzelfilterLeeren();
        LadeDaten();
    }

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
    private void SpalteSortieren(string? spalte)
    {
        var neueSpalte = spalte switch
        {
            "Datum" => ExpenseSortColumn.Datum,
            "Kategorie" => ExpenseSortColumn.Kategorie,
            "Betrag" => ExpenseSortColumn.Betrag,
            "Zahler" => ExpenseSortColumn.Zahler,
            "Status" => ExpenseSortColumn.Status,
            "Bemerkung" => ExpenseSortColumn.Bemerkung,
            _ => SortSpalte,
        };

        if (neueSpalte == SortSpalte)
        {
            SortAufsteigend = !SortAufsteigend;
        }
        else
        {
            SortSpalte = neueSpalte;
            SortAufsteigend = true;
        }

        LadeDaten();
    }

    // ---------------- Bearbeiten ----------------

    [RelayCommand]
    private void BearbeitenOeffnen(AusgabeZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        Bearbeiten = new AusgabeBearbeitenViewModel(
            zeile,
            _categoryRepository.GetSelectableLeaves(),
            _personRepository.GetAllActive());
    }

    [RelayCommand]
    private void BearbeitenSpeichern()
    {
        if (Bearbeiten is not { } dialog)
        {
            return;
        }

        if (!dialog.TryLeseWerte(out var amountCents, out var expenseDate, out var settledDate))
        {
            return;
        }

        // Update setzt ModifiedUtc. SettledDate kommt bei fremdem Zahler
        // aus dem neuen "Bezahlt am"-Feld (Regel 4); bei der eigenen
        // Person liefert TryLeseWerte dafuer immer NULL, unabhaengig vom
        // bisherigen Wert.
        dialog.SpeicherFehlerText = Schreibvorgang.Versuche(
            "Beim Speichern einer bearbeiteten Ausgabe",
            () => _expenseRepository.Update(
                dialog.ExpenseId,
                dialog.AusgewaehlteKategorie!.Id,
                amountCents,
                expenseDate,
                dialog.AusgewaehlterZahler.Id,
                dialog.BemerkungOderNull,
                settledDate,
                dialog.IstEinnahme));

        // Der Dialog bleibt bei einem Schreibfehler offen und gefuellt -
        // sonst waeren die Aenderungen weg, die gerade nicht gespeichert
        // werden konnten.
        if (dialog.SpeicherFehlerText is not null)
        {
            return;
        }

        Bearbeiten = null;
        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    [RelayCommand]
    private void BearbeitenAbbrechen() => Bearbeiten = null;

    // ---------------- Loeschen ----------------
    //
    // Geloescht wird sofort, ohne Nachfrage - dafuer laesst sich der
    // Vorgang zurueckholen, solange der Bereich nicht gewechselt wurde.
    // Eine Nachfrage vor jedem Loeschen wird nach dem dritten Mal blind
    // bestaetigt und schuetzt dann niemanden mehr; ein Rueckgaengig-Band
    // schuetzt auch den, der zu schnell geklickt hat.

    [RelayCommand]
    private void Loeschen(AusgabeZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        LoescheUndMerke(
            new[] { zeile.Id },
            $"Buchung gelöscht: {zeile.Beschreibung}");
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private void AusgewaehlteLoeschen()
    {
        var ausgewaehlte = Zeilen.Where(zeile => zeile.IstAusgewaehlt).ToList();
        if (ausgewaehlte.Count == 0)
        {
            return;
        }

        LoescheUndMerke(
            ausgewaehlte.Select(zeile => zeile.Id).ToList(),
            ausgewaehlte.Count == 1
                ? $"Buchung gelöscht: {ausgewaehlte[0].Beschreibung}"
                : $"{ausgewaehlte.Count} Buchungen gelöscht.");
    }

    /// <summary>
    /// Loescht die angegebenen Buchungen und legt sie zugleich fuer
    /// "Rueckgaengig" beiseite.
    ///
    /// Die Werte werden VOR dem Loeschen aus der Datenbank geholt und nicht
    /// aus den Anzeigezeilen genommen: dort stehen formatierte Texte, und
    /// aus "-1.234,56 €" zurueckzurechnen waere eine Fehlerquelle ohne Not.
    /// </summary>
    private void LoescheUndMerke(IReadOnlyList<int> ids, string bandText)
    {
        Bearbeiten = null;

        // Beide Baender von vorhin gehoerten zu einem anderen Vorgang -
        // auch das Rueckgaengig-Angebot, das dort noch stand.
        ErfolgText = null;
        VergissRuecknahme();

        var gesichert = _expenseRepository.GetByIds(ids);

        // DeleteMany laeuft in einer Transaktion: entweder alle Zeilen sind
        // weg oder keine. Ein Fehler mittendrin hinterlaesst also keine
        // halb geleerte Auswahl - und der gemerkte Vorrat passt dazu.
        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Loeschen von Ausgaben",
            () => _expenseRepository.DeleteMany(ids));

        if (SchreibFehlerText is not null)
        {
            // Regel 13: die Liste bleibt unveraendert stehen, der Fehler
            // erklaert als Band darueber, warum sich nichts bewegt hat.
            return;
        }

        // Steht die Liste gerade auf genau der Buchung, die eben geloescht
        // wurde, muss der Einzelfilter mit weg - sonst zeigt sie dauerhaft
        // nichts mehr an, und der Grund dafuer ist nicht mehr da.
        if (_buchungFilterId is int gefiltert && ids.Contains(gefiltert))
        {
            EinzelfilterLeeren();
        }

        // Die geloeschten Werte bleiben in der Ruecknahme liegen; sie legt
        // sie auf Wunsch wieder an. Die Zeilen bekommen dabei neue Ids
        // (siehe ExpenseRepository.RestoreMany) - alles uebrige steht danach
        // wieder so da wie vorher.
        BieteRuecknahmeAn(
            bandText,
            new Ruecknahme(
                "Beim Wiederherstellen geloeschter Ausgaben",
                "Legt die gelöschten Buchungen wieder an",
                () =>
                {
                    var anzahl = _expenseRepository.RestoreMany(gesichert);
                    return anzahl == 1
                        ? "Buchung wiederhergestellt."
                        : $"{anzahl} Buchungen wiederhergestellt.";
                }));

        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    // ---------------- Rueckgaengig-Band ----------------

    /// <summary>
    /// Stellt das Band auf, mit dem sich der eben ausgefuehrte Vorgang
    /// zuruecknehmen laesst. Das Erfolgsband wird dabei geraeumt: der Satz
    /// zum Vorgang steht im Band mit dem Knopf, und nur dort.
    /// </summary>
    private void BieteRuecknahmeAn(string bandText, Ruecknahme ruecknahme)
    {
        ErfolgText = null;

        // Erst die Ruecknahme, dann der Text: dessen Meldung zieht
        // RueckgaengigTipp mit, der aus der Ruecknahme kommt.
        _ruecknahme = ruecknahme;
        RueckgaengigText = bandText;
    }

    /// <summary>
    /// Nimmt den letzten umkehrbaren Vorgang zurueck - je nachdem, was das
    /// Band anbietet: die geloeschten Buchungen werden wieder angelegt, ein
    /// Abhaken wird auf den vorherigen Beglichen-Stand zurueckgesetzt.
    /// </summary>
    [RelayCommand]
    private void Rueckgaengig()
    {
        if (_ruecknahme is not { } ruecknahme)
        {
            return;
        }

        var erfolgstext = string.Empty;

        SchreibFehlerText = Schreibvorgang.Versuche(
            ruecknahme.Anlass,
            () => erfolgstext = ruecknahme.Ausfuehren());

        if (SchreibFehlerText is not null)
        {
            // Regel 13: das Band bleibt stehen, der Versuch laesst sich
            // gleich wiederholen.
            return;
        }

        VergissRuecknahme();
        ErfolgText = erfolgstext;

        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    /// <summary>
    /// Nimmt das Angebot an, ohne es anzunehmen: das Band verschwindet, der
    /// Vorgang bleibt. Raeumt zugleich den gemerkten Vorrat - was nicht
    /// mehr angeboten wird, muss auch nicht mehr vorgehalten werden.
    /// </summary>
    [RelayCommand]
    private void RueckgaengigSchliessen() => VergissRuecknahme();

    private void VergissRuecknahme()
    {
        _ruecknahme = null;
        RueckgaengigText = null;
    }

    // ---------------- Duplizieren ----------------

    /// <summary>
    /// Legt eine neue Buchung mit denselben Werten und dem heutigen Datum
    /// an ("das war wieder dasselbe wie letztes Mal").
    ///
    /// Die Werte kommen aus der Datenbank und nicht aus der Anzeigezeile:
    /// dort stehen formatierte Texte, und aus "-1.234,56 €"
    /// zurueckzurechnen waere eine Fehlerquelle ohne Not.
    ///
    /// Bewusst NICHT uebernommen werden das Beglichen-Datum (es gehoert
    /// der einzelnen Buchung, Regel 4) und der Vorlagenbezug (eine Kopie
    /// von Hand stammt aus keiner Vorlage).
    /// </summary>
    [RelayCommand]
    private void Duplizieren(AusgabeZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchreibFehlerText = null;
        ErfolgText = null;

        if (_expenseRepository.GetById(zeile.Id) is not { } vorlage)
        {
            // Zwischenzeitlich anderswo geloescht - die Liste ist dann
            // ohnehin veraltet.
            SchreibFehlerText =
                "Diese Buchung gibt es nicht mehr. Sie wurde inzwischen an anderer "
                + "Stelle gelöscht. Es wurde nichts angelegt. "
                + "„Filter zurücksetzen“ zeigt den aktuellen Stand.";
            return;
        }

        var heute = DateOnly.FromDateTime(DateTime.Now);
        Expense? kopie = null;

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Duplizieren einer Ausgabe",
            () => kopie = _expenseRepository.Create(
                vorlage.CategoryId,
                vorlage.AmountCents,
                heute,
                vorlage.PayerId,
                vorlage.Note,
                settledDate: null,
                recurringExpenseId: null,
                isIncome: vorlage.IsIncome));

        if (SchreibFehlerText is not null)
        {
            // Regel 13: die Liste bleibt unveraendert stehen.
            return;
        }

        AppLog.Current.Info(LogEvents.ExpenseDuplicated(vorlage.Id, kopie!.Id));

        ErfolgText = "Buchung dupliziert — Datum auf heute gesetzt.";
        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    // ---------------- Sammelaktionen ----------------
    //
    // Was fuer das Loeschen laengst geht, geht auch fuer das Aendern.
    // Gerechnet und geschrieben wird in Core (die drei ...Many-Methoden
    // von ExpenseRepository, jede in einer Transaktion) - hier bleibt die
    // Frage, ob vorher nachgefragt wird, und der Satz danach.

    /// <summary>
    /// Ab wie vielen betroffenen Zeilen vor der Sammelaktion nachgefragt
    /// wird. Darunter gibt es keinen Dialog: drei Zeilen umzubuchen ist in
    /// drei Klicks wieder geradegerueckt, und eine Nachfrage bei jedem
    /// Handgriff wird ohnehin weggeklickt. Reibung nach Schadensausmass.
    /// </summary>
    private const int SammelBestaetigungSchwelle = 20;

    /// <summary>
    /// Eine angestossene, noch nicht ausgefuehrte Sammelaktion. Steht
    /// zwischen Nachfrage und Bestaetigung.
    /// </summary>
    /// <param name="RuecknahmeDanach">
    /// Der Weg zurueck, wenn die Aktion umkehrbar ist - dann steht ihr Satz
    /// im Rueckgaengig-Band statt im Erfolgsband. NULL bei den Aktionen, die
    /// sich nicht zuruecknehmen lassen (Kategorie und Zahler umbuchen: dort
    /// waere der alte Stand je Zeile ein anderer, und die Auswahl ist nach
    /// dem Neuladen ohnehin weg). Wird erst NACH dem Schreiben aufgerufen,
    /// darf sich also auf den in <see cref="Sammelaktion.Ausfuehren"/>
    /// gemerkten Stand stuetzen.
    /// </param>
    private sealed record Sammelaktion(
        string Frage,
        string Anlass,
        Func<int> Ausfuehren,
        Func<int, string> Erfolgstext,
        Func<Ruecknahme>? RuecknahmeDanach = null);

    private Sammelaktion? _offeneSammelaktion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SammelAnfrageAktiv))]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerAlsBand))]
    private string? _sammelAnfrageText;

    public bool SammelAnfrageAktiv => SammelAnfrageText is not null;

    [RelayCommand]
    private void SammelKategorieSetzen(CategoryOption? ziel)
    {
        var ids = AusgewaehlteIds();
        if (ziel is null || ids.Count == 0)
        {
            return;
        }

        StosseSammelaktionAn(new Sammelaktion(
            $"{ZeilenText(ids.Count)} auf die Kategorie „{ziel.FullPath}“ umbuchen?",
            "Beim Aendern der Kategorie mehrerer Ausgaben",
            () => _expenseRepository.SetCategoryMany(ids, ziel.Id),
            anzahl => $"{ZeilenText(anzahl)} auf „{ziel.FullPath}“ umgebucht."),
            ids.Count);
    }

    [RelayCommand]
    private void SammelZahlerSetzen(ZahlerOption? ziel)
    {
        var ids = AusgewaehlteIds();
        if (ziel is null || ids.Count == 0)
        {
            return;
        }

        StosseSammelaktionAn(new Sammelaktion(
            $"{ZeilenText(ids.Count)} auf den Zahler „{ziel.Bezeichnung}“ umbuchen?",
            "Beim Aendern des Zahlers mehrerer Ausgaben",
            () => _expenseRepository.SetPayerMany(ids, ziel.Id),
            anzahl => $"{ZeilenText(anzahl)} auf „{ziel.Bezeichnung}“ umgebucht."),
            ids.Count);
    }

    /// <summary>
    /// Markiert die Auswahl als beglichen (mit dem heutigen Datum).
    ///
    /// Eigene Ausgaben bleiben dabei unberuehrt (Regel 4) - der
    /// Erfolgstext sagt das ausdruecklich, wenn weniger Zeilen gewandert
    /// sind als markiert waren. Ohne diesen Satz saehe es aus, als haette
    /// die Aktion die Haelfte vergessen.
    ///
    /// Umkehrbar: der Satz landet im Rueckgaengig-Band, und die Ruecknahme
    /// schreibt genau den Stand zurueck, der vorher in den Zeilen stand
    /// (siehe <see cref="MerkeBeglichenStand"/>) - auch ein aelteres
    /// Begleichungsdatum, das beim Abhaken ueberschrieben wurde.
    /// </summary>
    [RelayCommand]
    private void SammelAlsBeglichen()
    {
        var ids = AusgewaehlteIds();
        if (ids.Count == 0)
        {
            return;
        }

        var heute = DateOnly.FromDateTime(DateTime.Now);
        var markiert = ids.Count;
        IReadOnlyList<SettledState> vorher = Array.Empty<SettledState>();

        StosseSammelaktionAn(new Sammelaktion(
            $"{ZeilenText(markiert)} als beglichen markieren?",
            "Beim Markieren mehrerer Ausgaben als beglichen",
            () =>
            {
                // Der Stand VOR dem Schreiben, aus der Datenbank und nicht
                // aus den Anzeigezeilen: gelesen wird hier und nicht schon
                // beim Anstossen der Aktion, damit zwischen Nachfrage und
                // Bestaetigung nichts dazwischenkommt.
                vorher = MerkeBeglichenStand(ids);
                return _expenseRepository.SetSettledMany(ids, heute);
            },
            anzahl => anzahl == markiert
                ? $"{ZeilenText(anzahl)} als beglichen markiert."
                : $"{ZeilenText(anzahl)} als beglichen markiert. "
                  + $"{ZeilenText(markiert - anzahl)} blieben unverändert: "
                  + "bei eigenen Ausgaben gibt es keinen Beglichen-Status.",
            () => BeglichenRuecknahme(vorher)),
            markiert);
    }

    [RelayCommand]
    private void SammelBestaetigen()
    {
        if (_offeneSammelaktion is { } aktion)
        {
            FuehreSammelaktionAus(aktion);
        }
    }

    [RelayCommand]
    private void SammelAbbrechen()
    {
        _offeneSammelaktion = null;
        SammelAnfrageText = null;
    }

    private void StosseSammelaktionAn(Sammelaktion aktion, int betroffene)
    {
        Bearbeiten = null;
        ErfolgText = null;
        SchreibFehlerText = null;

        // Ein neuer Vorgang loest das Angebot des vorherigen ab: was das
        // Band anbietet, muss zu dem gehoeren, was gerade geschehen ist.
        VergissRuecknahme();

        if (betroffene >= SammelBestaetigungSchwelle)
        {
            _offeneSammelaktion = aktion;
            SammelAnfrageText = aktion.Frage;
            return;
        }

        FuehreSammelaktionAus(aktion);
    }

    private void FuehreSammelaktionAus(Sammelaktion aktion)
    {
        var geaendert = 0;

        SchreibFehlerText = Schreibvorgang.Versuche(
            aktion.Anlass, () => geaendert = aktion.Ausfuehren());

        if (SchreibFehlerText is not null)
        {
            // Regel 13: die Nachfrage bleibt stehen, die Liste unveraendert
            // darunter - der Versuch laesst sich gleich wiederholen, ohne
            // die Auswahl neu zu treffen.
            return;
        }

        _offeneSammelaktion = null;
        SammelAnfrageText = null;

        // Nur anbieten, wenn es etwas zurueckzunehmen gibt: hat die Aktion
        // keine einzige Zeile getroffen (etwa lauter eigene Ausgaben,
        // Regel 4), waere ein Knopf "Rückgängig" ein Angebot ohne Inhalt.
        if (aktion.RuecknahmeDanach is { } ruecknahme && geaendert > 0)
        {
            BieteRuecknahmeAn(aktion.Erfolgstext(geaendert), ruecknahme());
        }
        else
        {
            ErfolgText = aktion.Erfolgstext(geaendert);
        }

        // Betrag, Kategorie, Zahler oder Status mehrerer Buchungen haben
        // sich geaendert - jede Liste und jede Auswertung zeigt sonst
        // veraltete Werte (Regel 14). Die eigene Liste laedt darueber mit
        // neu, die Auswahl faellt dabei weg.
        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    private IReadOnlyList<int> AusgewaehlteIds() =>
        Zeilen.Where(zeile => zeile.IstAusgewaehlt).Select(zeile => zeile.Id).ToList();

    private static string ZeilenText(int anzahl) =>
        anzahl == 1 ? "1 Buchung" : $"{anzahl} Buchungen";

    // ---------------- Eine Zeile abhaken ----------------

    /// <summary>
    /// Markiert EINE Buchung als beglichen, mit dem heutigen Datum - der
    /// Weg fuer den Einzelfall aus dem Kontextmenue, ohne vorher zu
    /// markieren. Geschrieben wird ueber dieselbe Core-Methode wie bei der
    /// Sammelaktion, damit Regel 4 an genau einer Stelle steht.
    ///
    /// Bei einer eigenen Buchung passiert nichts: dort bedeutet
    /// SettledDate nichts. Der Menuepunkt ist deshalb erst gar nicht
    /// anklickbar (siehe <see cref="AusgabeZeile.IstOffen"/>) - hier steht
    /// die Pruefung noch einmal, weil sich ein Kommando nicht darauf
    /// verlassen darf, wer es aufruft.
    ///
    /// Wie die Sammelaktion ist der Handgriff umkehrbar: er meldet sich im
    /// Rueckgaengig-Band. Ein Fehlklick im Kontextmenue ist genau die Lage,
    /// in der das gebraucht wird - die Zeile steht danach nicht mehr in der
    /// Liste, wenn der Filter auf "offen" steht.
    /// </summary>
    [RelayCommand]
    private void AlsBeglichen(AusgabeZeile? zeile)
    {
        if (zeile is null || !zeile.IstOffen)
        {
            return;
        }

        ErfolgText = null;
        SchreibFehlerText = null;
        VergissRuecknahme();

        var heute = DateOnly.FromDateTime(DateTime.Now);
        var ids = new[] { zeile.Id };
        IReadOnlyList<SettledState> vorher = Array.Empty<SettledState>();

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Markieren einer Ausgabe als beglichen",
            () =>
            {
                vorher = MerkeBeglichenStand(ids);
                _expenseRepository.SetSettledMany(ids, heute);
            });

        if (SchreibFehlerText is not null)
        {
            // Regel 13: die Liste bleibt unveraendert stehen.
            return;
        }

        BieteRuecknahmeAn(
            $"Als beglichen markiert: {zeile.Beschreibung}",
            BeglichenRuecknahme(vorher));

        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    /// <summary>
    /// Haelt fest, welches Begleichungsdatum die Buchungen VOR dem Abhaken
    /// trugen. Gelesen wird aus der Datenbank und nicht aus den
    /// Anzeigezeilen: dort steht ein fertiger Satz ("beglichen am
    /// 05.08.2026"), und aus einem Text zurueckzurechnen waere eine
    /// Fehlerquelle ohne Not.
    /// </summary>
    private IReadOnlyList<SettledState> MerkeBeglichenStand(IReadOnlyList<int> ids) =>
        _expenseRepository.GetByIds(ids)
                          .Select(buchung => new SettledState(buchung.Id, buchung.SettledDate))
                          .ToList();

    /// <summary>
    /// Der Weg zurueck aus einem Abhaken: jede Buchung bekommt genau das
    /// Datum wieder, das vorher dort stand - meist "offen" (NULL), bei einer
    /// ueberschriebenen Zeile aber ihr altes Datum.
    /// </summary>
    private Ruecknahme BeglichenRuecknahme(IReadOnlyList<SettledState> vorher) =>
        new("Beim Zuruecknehmen des Abhakens",
            "Setzt den Beglichen-Status auf den Stand von vorher zurück",
            () =>
            {
                _expenseRepository.RestoreSettledDates(vorher);

                // Ohne Zahl: "3 Buchungen sind wieder offen" waere falsch,
                // sobald eine davon vorher schon ein aelteres
                // Begleichungsdatum trug oder eine eigene Ausgabe war, die
                // nie einen Beglichen-Status hatte (Regel 4).
                return "Abhaken zurückgenommen — der Beglichen-Status "
                       + "steht wieder wie vorher.";
            });

    // ---------------- Als Vorlage ----------------

    /// <summary>
    /// "Das kommt jeden Monat": oeffnet im Vorlagenbereich ein Formular,
    /// vorbelegt aus dieser Buchung. Gespeichert wird hier nichts - nur
    /// die Id wird weitergereicht, die Werte holt der Vorlagenbereich
    /// selbst aus der Datenbank (siehe
    /// <see cref="VorlagenViewModel.NeueVorlageAus"/>).
    /// </summary>
    [RelayCommand]
    private void AlsVorlage(AusgabeZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        VorlageAusBuchungAngefordert?.Invoke(this, zeile.Id);
    }

    // ---------------- Laden ----------------

    private void LadeDaten()
    {
        if (_ladenGesperrt)
        {
            return;
        }

        if (!TryBaueFilter(out var filter))
        {
            // Bei ungueltiger Zeitraumeingabe bleibt die letzte
            // Trefferliste stehen; der Fehlertext erklaert, warum sich
            // nichts bewegt.
            return;
        }

        foreach (var zeile in Zeilen)
        {
            zeile.PropertyChanged -= OnZeilePropertyChanged;
        }

        Zeilen.Clear();

        // Die Farben einmal je Ladevorgang aufloesen statt je Zeile: die
        // Vererbung laeuft ueber den ganzen Baum, und der aendert sich
        // waehrend eines Ladevorgangs nicht.
        var farben = _categoryRepository.GetResolvedColors();

        _angezeigteBuchungen = _expenseRepository.Query(filter, SortSpalte, SortAufsteigend);

        foreach (var item in _angezeigteBuchungen)
        {
            var zeile = new AusgabeZeile(item, CategoryColors.Of(farben, item.CategoryId));
            zeile.PropertyChanged += OnZeilePropertyChanged;
            Zeilen.Add(zeile);
        }

        var summary = _expenseRepository.Summarize(filter);
        TrefferText = summary.Count == 1
            ? "1 Treffer"
            : $"{summary.Count.ToString("N0", DeDe)} Treffer";
        SummeText = EuroText.Format(summary.SumCents);
        SummeIstEinnahme = EuroText.IsPositive(summary.SumCents);

        AnzahlAusgewaehlt = 0;
        KeineTreffer = summary.Count == 0;

        // Nur nachfragen, wenn die Liste leer ist: bei Treffern steht die
        // Antwort ohnehin fest, und die Abfrage liefe bei jedem Tastendruck
        // im Suchfeld mit.
        NochNichtsErfasst = KeineTreffer && !_expenseRepository.HasAny();

        // Der Export-Hinweis gehoert zu der Liste, die beim Speichern
        // dastand. Sobald sich der Filter bewegt, sagt "Gespeichert: …"
        // nichts mehr ueber das, was jetzt zu sehen ist.
        ExportHinweis = null;

        // Hier und nicht in jedem Filter-Setter: LadeDaten laeuft nach
        // JEDER Filteraenderung genau einmal, die Chips koennen also nicht
        // hinter dem Zustand zurueckbleiben.
        AktualisiereAktiveFilter();
    }

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

            RecurringExpenseId = _vorlageFilterId,
            ExpenseId = _buchungFilterId,
        };

        return true;
    }

    private void SetzeVorgabeZeitraum()
    {
        var zeitraum = DateRangePresets.ThisYear(DateOnly.FromDateTime(DateTime.Now));
        VonText = GermanDateInput.ToText(zeitraum.From);
        BisText = GermanDateInput.ToText(zeitraum.ToExclusive.AddDays(-1));
    }

    private void LadeAuswahllisten()
    {
        // Die Auswahl ueber den Neuaufbau retten: die Listen werden auch
        // waehrend der Sitzung neu geladen (neue Kategorie, neue Person),
        // und ein dabei stillschweigend geleerter Filter waere ein Raetsel.
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

        // Erst nach dem Aufbau anhaken: das Haekchen kaskadiert in den
        // Unterbaum, und beim Aufbau von oben nach unten wuerde es die
        // gerettete Auswahl der Kinder wieder ueberschreiben.
        StelleHaekchenWiederHer(KategorieWurzeln, angehakteKategorien);

        // Die Ziele der Sammelaktion "Kategorie ändern": nur Blattknoten,
        // nicht archiviert - genau die, auf die sich eine Buchung ueberhaupt
        // buchen laesst.
        SammelKategorien.Clear();
        foreach (var option in _categoryRepository.GetSelectableLeaves())
        {
            SammelKategorien.Add(option);
        }

        ZahlerOptionen.Clear();
        foreach (var person in _personRepository.GetAllActive())
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

    private static KategorieFilterKnoten? FindeKnoten(
        IEnumerable<KategorieFilterKnoten> knoten, int id)
    {
        foreach (var kandidat in knoten)
        {
            if (kandidat.Id == id)
            {
                return kandidat;
            }

            var gefunden = FindeKnoten(kandidat.Children, id);
            if (gefunden is not null)
            {
                return gefunden;
            }
        }

        return null;
    }

    private void OnZeilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AusgabeZeile.IstAusgewaehlt))
        {
            AnzahlAusgewaehlt = Zeilen.Count(zeile => zeile.IstAusgewaehlt);
        }
    }

    private string KopfText(string bezeichnung, ExpenseSortColumn spalte) =>
        SortSpalte == spalte ? $"{bezeichnung} {(SortAufsteigend ? "▲" : "▼")}" : bezeichnung;
}
