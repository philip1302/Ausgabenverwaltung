using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Charts;
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
/// <see cref="ReportFilter"/> wie in der Ausgabenliste - und zwar
/// buchstaeblich dieselbe Leiste: sie steht in
/// <see cref="FilterleisteViewModel"/>. Hier bleibt, was es nur hier
/// gibt: Gruppierung, Kreuztabelle, Einfaerbung und der Blick auf die
/// Einzelbuchungen hinter einer Zelle.
/// </summary>
public sealed partial class ReportViewModel : FilterleisteViewModel
{

    private readonly ReportRepository _reportRepository;
    private readonly ExpenseRepository _expenseRepository;

    // Welche Kategorien aufgeklappt sind, ueberdauert das Neuladen:
    // sonst fiele die Tabelle bei jeder Filteraenderung wieder zu.
    private readonly HashSet<int> _aufgeklappteKategorien = new();

    private ReportMatrix? _matrix;

    // Die aufgeloesten Kategoriefarben zur angezeigten Tabelle. Sie
    // ueberdauern das Auf- und Zuklappen, weil dabei nur die Zeilen neu
    // gebaut werden und der Kategoriebaum nicht erneut geladen wird.
    private IReadOnlyDictionary<int, string> _farben = new Dictionary<int, string>();

    // Die Einfaerbungsstufen der Zellen, nachgeschlagen ueber Kategorie
    // und Zeitabschnitt. Wie die Farben ueberdauern sie das Auf- und
    // Zuklappen: sie haengen an der Auswertung, nicht an der Anzeige.
    private IReadOnlyDictionary<(int KategorieId, string PeriodenKey), int> _stufen =
        LeereStufen;

    private static readonly Dictionary<(int KategorieId, string PeriodenKey), int>
        LeereStufen = new();

    // Der Filter, aus dem die angezeigte Tabelle entstanden ist. Der
    // Sprung in die Einzelbuchungen setzt genau darauf auf - nur so kann
    // der Dialog nie etwas anderes zeigen, als die angeklickte Zelle
    // summiert.
    private ReportFilter? _angezeigterFilter;

    public ObservableCollection<ReportSpalte> Spalten { get; } = new();

    public ObservableCollection<ReportZeile> Zeilen { get; } = new();

    public IReadOnlyList<GruppierungOption> GruppierungOptionen { get; } = new[]
    {
        new GruppierungOption("Jahr", ReportGrouping.Year),
        new GruppierungOption("Quartal", ReportGrouping.Quarter),
        new GruppierungOption("Monat", ReportGrouping.Month),
    };

    // ---------------- Eigene Filterwerte ----------------
    //
    // Die Leiste selbst - Zeitraum, Kategorien, Zahler, Status, Suche,
    // Chips, gespeicherte Staende - steht in FilterleisteViewModel und
    // ist dieselbe wie in der Ausgabenliste. Hier stehen nur die Werte,
    // die es nur in der Auswertung gibt.

    [ObservableProperty]
    private GruppierungOption _ausgewaehlteGruppierung;

    /// <summary>
    /// Kategorien ohne jede Buchung im Zeitraum. Standardmaessig
    /// ausgeblendet, damit die Tabelle nicht aus leeren Zeilen besteht.
    /// Wirkt rein auf die Anzeige - die Auswertung laeuft dafuer nicht neu.
    /// </summary>
    [ObservableProperty]
    private bool _kategorienOhneAusgabenAnzeigen;

    /// <summary>
    /// Ob die Zellen nach der Hoehe ihres Betrags eingefaerbt werden.
    ///
    /// Die Zahl bleibt dabei immer stehen und lesbar - die Farbe sagt
    /// nichts, was nicht auch dastuende, sie macht nur auffindbar, wo in
    /// der Tabelle viel liegt (Regel 10).
    ///
    /// Der Anfangswert kommt aus den Einstellungen; jede Aenderung
    /// schreibt ihn zurueck (siehe
    /// <see cref="OnWerteEinfaerbenChanged"/>).
    /// </summary>
    [ObservableProperty]
    private bool _werteEinfaerben = true;

    // ---------------- Ergebnis ----------------

    [ObservableProperty]
    private ReportZeile? _summenZeile;

    [ObservableProperty]
    private string _trefferText = string.Empty;

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
        : base(categoryRepository, personRepository, settingsStore)
    {
        _reportRepository = reportRepository;
        _expenseRepository = expenseRepository;

        _ausgewaehlteGruppierung = GruppierungOptionen[2];

        // Vor dem ersten Auswerten setzen, sonst rechnete BerechneStufen
        // einmal mit der Vorgabe statt mit dem gemerkten Stand. Direkt ins
        // Feld, damit OnWerteEinfaerbenChanged nicht anspringt und den
        // gerade gelesenen Wert gleich wieder zurueckschreibt.
        _werteEinfaerben = settingsStore.Load().ReportHeatmap;

        LadenGesperrt = true;
        LadeAuswahllisten();
        SetzeVorgabeZeitraum();
        LadenGesperrt = false;

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
        LadenGesperrt = true;
        LadeAuswahllisten();
        LadenGesperrt = false;

        // Die gespeicherten Filter koennen sich in der Ausgabenliste
        // geaendert haben - beide Bereiche fuehren dieselbe Liste.
        LadeGespeicherteFilter();

        LadeDaten();
    }

    // ---------------- Was die Filterleiste hier anders macht ----------------

    /// <summary>
    /// Anders als die Ausgabenliste gruppiert die Auswertung - und zwar
    /// so, wie es oben eingestellt ist.
    /// </summary>
    protected override ReportGrouping FilterGruppierung => AusgewaehlteGruppierung.Wert;

    /// <summary>
    /// Die Gruppierung gehoert zum gespeicherten Stand: "Auto nach
    /// Jahren" ist eine andere Frage als "Auto nach Monaten".
    /// </summary>
    protected override ReportGrouping? GespeicherteGruppierung => AusgewaehlteGruppierung.Wert;

    /// <summary>
    /// Zuruecksetzen heisst hier auch: zurueck zur Vorgabegruppierung und
    /// wieder ohne die leeren Kategorien.
    /// </summary>
    protected override void SetzeEigeneVorgaben()
    {
        AusgewaehlteGruppierung = GruppierungOptionen[2];
        KategorienOhneAusgabenAnzeigen = false;
    }

    /// <summary>
    /// Ein in der Ausgabenliste gespeicherter Filter kennt keine
    /// Gruppierung; dann bleibt die eingestellte stehen, statt auf eine
    /// erfundene zu springen.
    /// </summary>
    protected override void UebernimmEigeneWerte(SavedFilter filter)
    {
        if (filter.Grouping is ReportGrouping gruppierung)
        {
            AusgewaehlteGruppierung = GruppierungOptionen
                .FirstOrDefault(option => option.Wert == gruppierung)
                ?? AusgewaehlteGruppierung;
        }
    }

    partial void OnAusgewaehlteGruppierungChanged(GruppierungOption value) => LadeDaten();
    // Der Schalter blendet nur aus, was schon ausgewertet ist.
    partial void OnKategorienOhneAusgabenAnzeigenChanged(bool value) => BaueZeilen();

    /// <summary>
    /// Der Schalter faerbt nur um, was schon ausgewertet ist - die Stufen
    /// muessen dafuer aber neu bestimmt werden, weil sie bei
    /// ausgeschalteter Einfaerbung gar nicht erst gerechnet werden.
    /// </summary>
    partial void OnWerteEinfaerbenChanged(bool value)
    {
        BerechneStufen();
        BaueZeilen();

        var einstellungen = SettingsStore.Load();
        SettingsStore.Save(einstellungen with { ReportHeatmap = value });
    }

    /// <summary>
    /// Bestimmt, wie schwer jede Zelle im Vergleich zu den uebrigen wiegt.
    ///
    /// Gerechnet wird ueber den GANZEN Kategoriebaum, nicht nur ueber die
    /// gerade sichtbaren Zeilen: sonst wechselten beim Auf- und Zuklappen
    /// die Farben von Zellen, an denen sich nichts geaendert hat, und der
    /// Anwender suchte nach einer Bedeutung darin.
    ///
    /// Eingefaerbt werden nur Zellen mit AUSGABENUEBERHANG. Eine Zelle, in
    /// der die Einnahmen ueberwiegen, traegt bereits ihre gruene
    /// Auszeichnung; beides uebereinanderzulegen macht beides unlesbar.
    /// Eine Zelle, die sich auf genau null summiert, ist weder das eine
    /// noch das andere und bleibt ebenfalls frei.
    /// </summary>
    private void BerechneStufen()
    {
        if (_matrix is null || !WerteEinfaerben)
        {
            _stufen = LeereStufen;
            return;
        }

        var werte = new Dictionary<(int, string), long>();
        SammleAusgabenzellen(_matrix.Rows, werte);

        _stufen = Intensity.Steps(werte);
    }

    private static void SammleAusgabenzellen(
        IReadOnlyList<ReportMatrixRow> rows, Dictionary<(int, string), long> ziel)
    {
        foreach (var row in rows)
        {
            // Cells enthaelt nur die BELEGTEN Zeitabschnitte - leere Zellen
            // kommen dadurch gar nicht erst in die Bewertung.
            foreach (var (periodenKey, betrag) in row.Cells)
            {
                if (betrag.SumCents < 0)
                {
                    ziel[(row.CategoryId, periodenKey)] = betrag.SumCents;
                }
            }

            SammleAusgabenzellen(row.Children, ziel);
        }
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

        var farben = CategoryRepository.GetResolvedColors();

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
            : $"{Kultur.Anzahl(summary.Count)} Buchungen";
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

    // ---------------- Laden ----------------

    protected override void LadeDaten()
    {
        if (LadenGesperrt)
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
        var baum = CategoryRepository.GetTree();
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

        // Die Stufen haengen an der Auswertung, nicht an der Anzeige -
        // deshalb hier und nicht in BaueZeilen, das bei jedem Aufklappen
        // laeuft.
        BerechneStufen();

        Spalten.Clear();
        foreach (var key in _matrix.PeriodKeys)
        {
            Spalten.Add(new ReportSpalte(key, ReportPeriods.Label(key, filter.Grouping)));
        }

        BaueZeilen();

        TrefferText = _matrix.Total.Count == 1
            ? "1 Buchung in 1 Zeitabschnitt"
            : $"{Kultur.Anzahl(_matrix.Total.Count)} Buchungen in " +
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
                row, Spalten, hatKinder, aufgeklappt,
                CategoryColors.Of(_farben, row.CategoryId), _stufen));

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
