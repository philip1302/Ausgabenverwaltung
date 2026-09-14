using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Charts;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.YearInReview;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
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

    // Die Groesse der Zeichenflaeche, wie sie die Ansicht meldet. Vor der
    // ersten Meldung ist sie 0 und es wird nichts gezeichnet.
    private double _flaecheBreite;
    private double _flaecheHoehe;
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

        // Die Raender des Diagramms wachsen mit der Schriftgroesse; ohne
        // Neuzeichnen ueberdeckten sich Beschriftung und Flaeche auf der
        // Stufe "Sehr gross" (Regel 9).
        Skalierung.Aktuell.PropertyChanged += (_, _) => ZeichneDiagramm();

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
        ZeichneDiagramm();
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

    // ---------------- Monatsverlauf ----------------

    /// <summary>
    /// Die Balken beider Jahre. Das Vorjahr steht zuerst in der Liste und
    /// wird dadurch zuerst gezeichnet - es ist der Bezug und liegt hinten.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<RueckblickBalken> _diagrammBalken = [];

    [ObservableProperty]
    private IReadOnlyList<DiagrammLinie> _diagrammLinien = [];

    [ObservableProperty]
    private IReadOnlyList<DiagrammWertBeschriftung> _diagrammWertachse = [];

    [ObservableProperty]
    private IReadOnlyList<DiagrammZeitBeschriftung> _diagrammZeitachse = [];

    /// <summary>
    /// Ob ueberhaupt etwas zu zeigen ist. Ein leeres Diagramm bleibt ganz
    /// weg, statt eine leere Flaeche mit Achsen zu zeigen - die sieht aus,
    /// als waere etwas kaputt.
    /// </summary>
    [ObservableProperty]
    private bool _diagrammVorhanden;

    /// <summary>
    /// Meldung der Ansicht ueber die Groesse der Zeichenflaeche. Sie steht
    /// erst nach dem ersten Anzeigen fest und aendert sich bei jeder
    /// Fenstergroesse - deshalb wird von dort aus gezeichnet und nicht
    /// beim Laden.
    /// </summary>
    public void ZeichenflaecheGeaendert(double breite, double hoehe)
    {
        _flaecheBreite = breite;
        _flaecheHoehe = hoehe;
        ZeichneDiagramm();
    }

    /// <summary>
    /// Uebersetzt das Ergebnis von Core.Charts in zeichenbare Elemente.
    ///
    /// Aufgebaut wie <see cref="StartseiteViewModel"/>: der linke Rand
    /// traegt die Wertachse, der untere die Zeitachse, beide wachsen mit
    /// der eingestellten Schriftgroesse (Regel 9).
    /// </summary>
    private void ZeichneDiagramm()
    {
        var monate = _ergebnis?.Months ?? [];

        // Ohne eine einzige Buchung in beiden Jahren gibt es nichts zu
        // vergleichen. Das sagt die Seite bereits im Klartext ueber ihren
        // Leerzustand; ein zusaetzliches leeres Achsenkreuz hilft nicht.
        var etwasDa = monate.Any(m => m.PreviousCents != 0 || m.CurrentCents != 0);

        if (!etwasDa || _flaecheBreite <= 0 || _flaecheHoehe <= 0)
        {
            DiagrammVorhanden = etwasDa;
            DiagrammBalken = [];
            DiagrammLinien = [];
            DiagrammWertachse = [];
            DiagrammZeitachse = [];
            return;
        }

        var faktor = Skalierung.Aktuell.Faktor;
        var linkerRand = Math.Round(64 * faktor);
        var untererRand = Math.Round(24 * faktor);

        // Oben Luft lassen: die oberste Achsenbeschriftung sitzt mittig
        // auf ihrem Strich und ragte sonst zur Haelfte ueber den Rand.
        var obererRand = Math.Round(12 * faktor);

        var breite = _flaecheBreite - linkerRand;
        var hoehe = _flaecheHoehe - untererRand - obererRand;

        var abschnitte = monate
            .Select(m => new ComparePeriod(
                m.Key, m.Label, m.PreviousCents, m.CurrentCents))
            .ToList();

        var layout = SeriesBars.Compare(abschnitte, breite, hoehe);

        DiagrammVorhanden = !layout.IsEmpty;

        if (layout.IsEmpty)
        {
            DiagrammBalken = [];
            DiagrammLinien = [];
            DiagrammWertachse = [];
            DiagrammZeitachse = [];
            return;
        }

        var vorjahrKopf = _ergebnis!.Periods.PreviousLabel;
        var jahrKopf = _ergebnis.Periods.CurrentLabel;

        DiagrammBalken = layout.Bars
            .Select(balken => new RueckblickBalken(
                balken.Key,
                balken.X + linkerRand,
                balken.Y + obererRand,
                Math.Max(1, balken.Width),
                Math.Max(1, balken.Height),
                balken.IstVorjahr,
                Hinweistext(balken, vorjahrKopf, jahrKopf)))
            .ToList();

        DiagrammLinien = layout.Lines
            .Select(linie => new DiagrammLinie(
                new Point(linkerRand, linie.Y + obererRand),
                new Point(linkerRand + breite, linie.Y + obererRand),
                linie.Kind,
                null))
            .ToList();

        DiagrammWertachse = layout.Ticks
            .Select(strich => new DiagrammWertBeschriftung(
                strich.Y + obererRand - Math.Round(8 * faktor),
                linkerRand - Math.Round(8 * faktor),
                EuroText.Axis(strich.ValueCents)))
            .ToList();

        // Wie auf der Startseite wird bei Platzmangel nur jeder n-te Monat
        // beschriftet. Das Mass ist hier kuerzer, weil nur das Kuerzel
        // dasteht ("Mär") und nicht Monat und Jahr.
        var abschnittsBreite = breite / abschnitte.Count;
        var mindestBreite = Math.Round(36 * faktor);
        var passendeAnzahl = Math.Max(1, (int)(breite / mindestBreite));
        var schrittweite = Math.Max(
            1, (int)Math.Ceiling(abschnitte.Count / (double)passendeAnzahl));

        var textBreite = abschnittsBreite * schrittweite;
        var versatz = (textBreite - abschnittsBreite) / 2;

        DiagrammZeitachse = abschnitte
            .Select((abschnitt, i) => (abschnitt, i))
            // Von hinten zaehlen, damit der letzte Monat immer beschriftet
            // ist - er ist der, an dem der Vergleich endet.
            .Where(x => (abschnitte.Count - 1 - x.i) % schrittweite == 0)
            .Select(x => new DiagrammZeitBeschriftung(
                Math.Clamp(
                    linkerRand + abschnittsBreite * x.i - versatz,
                    0,
                    Math.Max(0, _flaecheBreite - textBreite)),
                hoehe + obererRand + Math.Round(4 * faktor),
                textBreite,
                x.abschnitt.Label,
                x.abschnitt.Key))
            .ToList();
    }

    private static string Hinweistext(CompareBar balken, string vorjahr, string jahr)
    {
        var jahrText = balken.IstVorjahr ? vorjahr : jahr;

        return $"{balken.Label} {jahrText}\n"
               + $"{EuroText.Format(balken.ValueCents)}\n\n"
               + "Klicken zeigt die Buchungen";
    }

    /// <summary>
    /// Ein Klick auf einen Balken fuehrt in die Buchungen seines Monats -
    /// dasselbe Ziel wie bei einer Monatskarte.
    ///
    /// Auch der Vorjahresbalken fuehrt in SEINEN Monat und nicht in den des
    /// laufenden Jahres: sonst zeigte ein Klick etwas anderes, als
    /// angeklickt wurde.
    /// </summary>
    [RelayCommand]
    private void MonatOeffnen(string? schluessel)
    {
        if (schluessel is null || _ergebnis is null)
        {
            return;
        }

        Springe(kategorieId: null, Monatsausschnitt(schluessel));
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

/// <summary>
/// Ein gezeichnetes Rechteck des Monatsverlaufs.
///
/// Die Farbe waehlt die Ansicht ueber <see cref="IstVorjahr"/> - das
/// ViewModel kennt keine Farbwerte, sonst waeren sie nicht mehr
/// themenabhaengig (dasselbe Muster wie bei DiagrammBalken).
/// </summary>
public sealed class RueckblickBalken
{
    public RueckblickBalken(
        string schluessel,
        double x,
        double y,
        double breite,
        double hoehe,
        bool istVorjahr,
        string hinweis)
    {
        Schluessel = schluessel;
        X = x;
        Y = y;
        Breite = breite;
        Hoehe = hoehe;
        IstVorjahr = istVorjahr;
        IstJahr = !istVorjahr;
        Hinweis = hinweis;
    }

    /// <summary>Der Monat, in dessen Buchungen ein Klick fuehrt.</summary>
    public string Schluessel { get; }

    public double X { get; }

    public double Y { get; }

    public double Breite { get; }

    public double Hoehe { get; }

    public bool IstVorjahr { get; }

    /// <summary>
    /// Das Gegenstueck zu <see cref="IstVorjahr"/>. Als eigenes Merkmal und
    /// nicht ueber eine Verneinung in der Ansicht, weil Classes.x genau ein
    /// bool erwartet.
    /// </summary>
    public bool IstJahr { get; }

    public string Hinweis { get; }
}
