using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.YearInReview;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Wohin ein Klick im Rueckblick fuehrt: in die Ausgabenliste, eingestellt
/// auf genau die Buchungen hinter der angeklickten Karte oder Zeile.
/// <see cref="KategorieId"/> NULL steht fuer die Monatskarten, die zu
/// keiner Kategorie gehoeren.
/// </summary>
public sealed record RueckblickSprung(
    int? KategorieId, DateOnly Von, DateOnly BisEinschliesslich);

/// <summary>
/// Der Jahresrueckblick: zwei Jahre nebeneinander, die auffaelligsten
/// Punkte als Karten und darunter die ganze Gegenueberstellung.
///
/// Enthaelt keine Fachlogik (Regel 7) - was auffaellig ist, welcher
/// Zeitraum verglichen wird und wie die Saetze lauten, steht vollstaendig
/// in <c>Core/YearInReview</c>. Hier wird nur verdrahtet und angezeigt.
/// </summary>
public sealed partial class JahresrueckblickViewModel : ViewModelBase
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private readonly YearInReviewService _service;
    private readonly CategoryRepository _categoryRepository;
    private readonly Func<DateOnly> _heute;

    // Welche Kategorien aufgeklappt sind, ueberdauert das Neuladen und den
    // Jahreswechsel - wer einen Ast geoeffnet hat, will ihn im anderen Jahr
    // ebenfalls offen sehen.
    private readonly HashSet<int> _aufgeklappteKategorien = new();

    private YearInReviewResult? _ergebnis;
    private IReadOnlyDictionary<int, string> _farben = new Dictionary<int, string>();
    private bool _ladenGesperrt;

    /// <param name="heute">
    /// Woher der heutige Tag kommt. Von aussen hereingereicht, damit sich
    /// die Jahresauswahl und der Umschalter pruefen lassen, ohne die
    /// Systemuhr zu stellen.
    /// </param>
    public JahresrueckblickViewModel(
        YearInReviewService service,
        CategoryRepository categoryRepository,
        IMessenger messenger,
        Func<DateOnly>? heute = null)
    {
        _service = service;
        _categoryRepository = categoryRepository;
        _heute = heute ?? (() => DateOnly.FromDateTime(DateTime.Now));

        Aktualisiere();

        // Buchungsaenderungen aus anderen Bereichen sollen den Rueckblick
        // sofort mitziehen und nicht erst beim naechsten Navigieren
        // hierher (Regel 14).
        messenger.Register<JahresrueckblickViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.Aktualisiere());
    }

    // ---------------- Auswahl ----------------

    /// <summary>Die Jahre mit Buchungen, das juengste zuerst.</summary>
    public ObservableCollection<int> JahrOptionen { get; } = new();

    [ObservableProperty]
    private int _ausgewaehltesJahr;

    /// <summary>
    /// Ob ganze Kalenderjahre verglichen werden. Vorgabe ist der gleiche
    /// Zeitraum bis heute - siehe <see cref="ReviewSpan"/>.
    /// </summary>
    [ObservableProperty]
    private bool _ganzeKalenderjahre;

    /// <summary>
    /// Nur beim laufenden Jahr gibt es etwas umzuschalten; ein
    /// abgeschlossenes Jahr wird immer ganz verglichen.
    /// </summary>
    public bool ZeitraumUmschaltbar =>
        ReviewPeriods.IstLaufendesJahr(AusgewaehltesJahr, _heute());

    [ObservableProperty]
    private string _zeitraumHinweis = string.Empty;

    /// <summary>
    /// Zusatzwarnung, wenn ganze Kalenderjahre gewaehlt sind, obwohl das
    /// Jahr noch laeuft - leer, wenn es nichts einzuordnen gibt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatGanzjahresWarnung))]
    private string _ganzjahresWarnung = string.Empty;

    public bool HatGanzjahresWarnung => GanzjahresWarnung.Length > 0;

    // ---------------- Kennzahlen ----------------

    [ObservableProperty]
    private RueckblickKennzahl? _ausgabenKennzahl;

    [ObservableProperty]
    private RueckblickKennzahl? _einnahmenKennzahl;

    [ObservableProperty]
    private RueckblickKennzahl? _nettoKennzahl;

    // ---------------- Karten und Tabelle ----------------

    public ObservableCollection<RueckblickKarte> Karten { get; } = new();

    public ObservableCollection<RueckblickZeile> Zeilen { get; } = new();

    [ObservableProperty]
    private RueckblickZeile? _summenZeile;

    [ObservableProperty]
    private string _vorjahrKopf = string.Empty;

    [ObservableProperty]
    private string _jahrKopf = string.Empty;

    // ---------------- Lage ----------------

    /// <summary>Die Datenbank ist leer - der einzige Fall mit einem Knopf.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZeigtInhalt))]
    private bool _nochNichtsErfasst;

    /// <summary>
    /// Kein Vorjahr zum Vergleichen. Kennzahlen und Tabelle bleiben
    /// trotzdem stehen - sie gelten fuer sich.
    /// </summary>
    [ObservableProperty]
    private bool _vorjahrOhneBuchung;

    /// <summary>
    /// Nichts hat sich deutlich verschoben. Ein ERGEBNIS und kein
    /// Leerzustand: es gibt nichts aufzuloesen, deshalb steht daneben auch
    /// kein Knopf.
    /// </summary>
    [ObservableProperty]
    private bool _keineAuffaelligkeiten;

    [ObservableProperty]
    private string _lageHinweis = string.Empty;

    /// <summary>Ob Kennzahlen und Tabelle ueberhaupt etwas zu zeigen haben.</summary>
    public bool ZeigtInhalt => !NochNichtsErfasst;

    [ObservableProperty]
    private string? _exportHinweis;

    // ---------------- Ereignisse ----------------

    /// <summary>Der Weg aus dem leeren Rueckblick in die Erfassung.</summary>
    public event EventHandler? ErfassenAngefordert;

    /// <summary>Ein Klick auf eine Karte oder eine Zeile.</summary>
    public event EventHandler<RueckblickSprung>? AusgabenlisteAngefordert;

    // ---------------- Laden ----------------

    /// <summary>
    /// Laedt Jahresauswahl und Rueckblick neu. Wird bei Navigation in
    /// diesen Bereich aufgerufen (siehe <see cref="MainViewModel"/>) und
    /// bei jeder Buchungsaenderung - die Bereichs-ViewModels sind
    /// DI-Singletons und wuerden sonst einmal Geladenes stehen lassen.
    /// </summary>
    public void Aktualisiere()
    {
        var jahre = _service.JahreMitBuchungen();

        _ladenGesperrt = true;

        var bisher = AusgewaehltesJahr;
        JahrOptionen.Clear();
        foreach (var jahr in jahre)
        {
            JahrOptionen.Add(jahr);
        }

        // Das zuletzt gewaehlte Jahr behalten, solange es noch Buchungen
        // hat - sonst das juengste. Ohne jede Buchung bleibt das laufende
        // Jahr stehen, damit die Seite einen Zeitraum benennen kann.
        AusgewaehltesJahr = jahre.Contains(bisher)
            ? bisher
            : jahre.Count > 0 ? jahre[0] : _heute().Year;

        _ladenGesperrt = false;

        LadeDaten();
    }

    partial void OnAusgewaehltesJahrChanged(int value)
    {
        OnPropertyChanged(nameof(ZeitraumUmschaltbar));
        LadeDaten();
    }

    partial void OnGanzeKalenderjahreChanged(bool value) => LadeDaten();

    private void LadeDaten()
    {
        if (_ladenGesperrt)
        {
            return;
        }

        var heute = _heute();
        var spanne = GanzeKalenderjahre
            ? ReviewSpan.GanzeKalenderjahre
            : ReviewSpan.GleicherZeitraum;

        _ergebnis = _service.Build(AusgewaehltesJahr, spanne, heute);
        _farben = _categoryRepository.GetResolvedColors();
        ExportHinweis = null;

        var zeitraeume = _ergebnis.Periods;
        ZeitraumHinweis = ReviewText.ZeitraumHinweis(zeitraeume);
        GanzjahresWarnung = ReviewText.GanzjahresWarnung(zeitraeume, heute);
        VorjahrKopf = zeitraeume.PreviousLabel;
        JahrKopf = zeitraeume.CurrentLabel;

        AusgabenKennzahl = RueckblickKennzahl.Aus(
            _ergebnis.Headline.Ausgaben, zeitraeume.PreviousLabel);
        EinnahmenKennzahl = RueckblickKennzahl.Aus(
            _ergebnis.Headline.Einnahmen, zeitraeume.PreviousLabel);
        NettoKennzahl = RueckblickKennzahl.Aus(
            _ergebnis.Headline.Netto, zeitraeume.PreviousLabel);

        NochNichtsErfasst = _ergebnis.State == ReviewDataState.NochNichtsErfasst;
        VorjahrOhneBuchung = _ergebnis.State == ReviewDataState.VorjahrOhneBuchung;
        KeineAuffaelligkeiten = _ergebnis.State == ReviewDataState.KeineAuffaelligkeiten;
        LageHinweis = ReviewText.LageHinweis(_ergebnis.State, zeitraeume);

        BaueKarten();
        BaueZeilen();
    }

    private void BaueKarten()
    {
        Karten.Clear();
        if (_ergebnis is null)
        {
            return;
        }

        foreach (var befund in _ergebnis.Findings)
        {
            Karten.Add(RueckblickKarte.Aus(
                befund,
                befund.CategoryId is int id ? CategoryColors.Of(_farben, id) : null,
                _ergebnis.Periods));
        }
    }

    /// <summary>
    /// Flacht den Zeilenbaum auf die gerade sichtbaren Zeilen ab - laeuft
    /// bei jedem Auf- und Zuklappen, ohne erneute Abfrage.
    /// </summary>
    private void BaueZeilen()
    {
        Zeilen.Clear();
        if (_ergebnis is null)
        {
            return;
        }

        FuegeZeilenEin(_ergebnis.Comparison.Roots);
        SummenZeile = _ergebnis.Comparison.JahrHatBuchungen
                      || _ergebnis.Comparison.VorjahrHatBuchungen
            ? RueckblickZeile.FuerSumme(_ergebnis.Comparison)
            : null;
    }

    private void FuegeZeilenEin(IReadOnlyList<ReviewCategoryChange> knoten)
    {
        // Der groesste Posten oben: ein Rueckblick beantwortet zuerst die
        // Frage "wofuer ging das meiste weg" und nicht "wie heisst die
        // Kategorie alphabetisch". Bei gleichem Betrag entscheidet der Name,
        // damit die Reihenfolge nicht von Abfrage zu Abfrage springt.
        foreach (var k in knoten
                     .OrderByDescending(k => k.CurrentCents)
                     .ThenByDescending(k => k.PreviousCents)
                     .ThenBy(k => k.Name, StringComparer.Create(DeDe, ignoreCase: false)))
        {
            var aufgeklappt = _aufgeklappteKategorien.Contains(k.CategoryId);

            Zeilen.Add(RueckblickZeile.FuerKategorie(
                k,
                _ergebnis!.Comparison.CurrentTotalCents,
                hatKinder: k.Children.Count > 0,
                istAufgeklappt: aufgeklappt,
                farbe: CategoryColors.Of(_farben, k.CategoryId)));

            if (aufgeklappt)
            {
                FuegeZeilenEin(k.Children);
            }
        }
    }

    // ---------------- Bedienung ----------------

    [RelayCommand]
    private void ZeileUmschalten(RueckblickZeile? zeile)
    {
        if (zeile?.Quelle is not ReviewCategoryChange quelle || !zeile.HatKinder)
        {
            return;
        }

        if (!_aufgeklappteKategorien.Add(quelle.CategoryId))
        {
            _aufgeklappteKategorien.Remove(quelle.CategoryId);
        }

        BaueZeilen();
    }

    [RelayCommand]
    private void KarteOeffnen(RueckblickKarte? karte)
    {
        if (karte is null || _ergebnis is null)
        {
            return;
        }

        // Eine Monatskarte fuehrt in ihren Monat, eine Kategoriekarte in
        // den ganzen verglichenen Zeitraum.
        var zeitraum = karte.Quelle.PeriodKey is string key
            ? Monatsausschnitt(key)
            : _ergebnis.Periods.Current;

        Springe(karte.KategorieId, zeitraum);
    }

    [RelayCommand]
    private void ZeileOeffnen(RueckblickZeile? zeile)
    {
        if (zeile?.Quelle is not ReviewCategoryChange quelle || _ergebnis is null)
        {
            return;
        }

        Springe(quelle.CategoryId, _ergebnis.Periods.Current);
    }

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    // Der Monat eines Monatsbefunds, begrenzt auf den verglichenen
    // Zeitraum: im laufenden Jahr endet der letzte Monat heute und nicht
    // an seinem Monatsende, sonst zeigte die Liste mehr Buchungen, als in
    // die Karte eingerechnet wurden.
    private DateRange Monatsausschnitt(string periodenKey)
    {
        var monat = ReportPeriods.Range(periodenKey, ReportGrouping.Month);
        var ende = _ergebnis!.Periods.Current.ToExclusive;

        return new DateRange(monat.From, monat.ToExclusive < ende ? monat.ToExclusive : ende);
    }

    private void Springe(int? kategorieId, DateRange zeitraum) =>
        AusgabenlisteAngefordert?.Invoke(this, new RueckblickSprung(
            kategorieId, zeitraum.From, zeitraum.ToExclusive.AddDays(-1)));

    // ---------------- Export ----------------

    public string CsvDateiname =>
        "Jahresrueckblick_" + AusgewaehltesJahr.ToString("D4", CultureInfo.InvariantCulture)
        + ".csv";

    /// <summary>
    /// Der CSV-Text zur gerade ANGEZEIGTEN Tabelle, samt der aufgeklappten
    /// Struktur. Geschrieben wird die Datei in der Ansicht - die
    /// Dateiauswahl ist reine Bedienmechanik.
    /// </summary>
    public string BaueCsv() =>
        _ergebnis is null
            ? string.Empty
            : ReportCsv.BuildYearComparison(
                _ergebnis.Comparison,
                Zeilen.Where(zeile => zeile.Quelle is not null)
                      .Select(zeile => zeile.Quelle!)
                      .ToList());

    public void MeldeExport(string hinweis) => ExportHinweis = hinweis;
}
