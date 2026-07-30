using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public IReadOnlyList<ZahlerBereichOption> ZahlerBereichOptionen { get; } = new[]
    {
        new ZahlerBereichOption("alle", PayerScope.All),
        new ZahlerBereichOption("nur ich", PayerScope.Self),
        new ZahlerBereichOption("nur andere", PayerScope.Others),
    };

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KategorieFilterText))]
    private KategorieFilterKnoten? _ausgewaehlteFilterKategorie;

    /// <summary>Beschriftung der Kategorie-Auswahl in der Filterleiste.</summary>
    public string KategorieFilterText =>
        AusgewaehlteFilterKategorie?.FullPath ?? "Alle Kategorien";

    [ObservableProperty]
    private GruppierungOption _ausgewaehlteGruppierung;

    [ObservableProperty]
    private ZahlerBereichOption _ausgewaehlterZahlerBereich;

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
    private bool _keineTreffer;

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

    /// <summary>Die Summe des Dialogs ist negativ - Erstattungen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _detailSummeIstErstattung;

    public ReportViewModel(
        ReportRepository reportRepository,
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository)
    {
        _reportRepository = reportRepository;
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;

        _ausgewaehlteGruppierung = GruppierungOptionen[2];
        _ausgewaehlterZahlerBereich = ZahlerBereichOptionen[0];

        _ladenGesperrt = true;
        LadeKategorieAuswahl();
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
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

        LadeDaten();
    }

    // Jede Filteraenderung wertet neu aus.
    partial void OnVonTextChanged(string value) => LadeDaten();
    partial void OnBisTextChanged(string value) => LadeDaten();
    partial void OnAusgewaehlteFilterKategorieChanged(KategorieFilterKnoten? value) => LadeDaten();
    partial void OnAusgewaehlteGruppierungChanged(GruppierungOption value) => LadeDaten();
    partial void OnAusgewaehlterZahlerBereichChanged(ZahlerBereichOption value) => LadeDaten();
    partial void OnSuchtextChanged(string value) => LadeDaten();

    // Der Schalter blendet nur aus, was schon ausgewertet ist.
    partial void OnKategorienOhneAusgabenAnzeigenChanged(bool value) => BaueZeilen();

    [RelayCommand]
    private void Schnellwahl(string? bereich)
    {
        var heute = DateOnly.FromDateTime(DateTime.Now);

        // "alles" laesst beide Felder leer - eine leere Grenze ist die
        // natuerliche Schreibweise fuer "unbegrenzt" und vermeidet, dass
        // dort 01.01.0001 bzw. 31.12.9999 steht.
        DateRange? bereichWerte;
        switch (bereich)
        {
            case "DiesesJahr":
                bereichWerte = DateRangePresets.ThisYear(heute);
                break;
            case "LetztesJahr":
                bereichWerte = DateRangePresets.LastYear(heute);
                break;
            case "Letzte12Monate":
                bereichWerte = DateRangePresets.LastTwelveMonths(heute);
                break;
            case "Letzte3Jahre":
                bereichWerte = DateRangePresets.LastThreeYears(heute);
                break;
            case "Alles":
                bereichWerte = null;
                break;
            default:
                return;
        }

        _ladenGesperrt = true;
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
        _ladenGesperrt = false;

        LadeDaten();
    }

    [RelayCommand]
    private void FilterZuruecksetzen()
    {
        _ladenGesperrt = true;
        AusgewaehlteFilterKategorie = null;
        AusgewaehlteGruppierung = GruppierungOptionen[2];
        AusgewaehlterZahlerBereich = ZahlerBereichOptionen[0];
        Suchtext = string.Empty;
        KategorienOhneAusgabenAnzeigen = false;
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Waehlt einen Kategorie-Ast als Filter. Bewusst ein Command statt
    /// einer Bindung an TreeView.SelectedItem - siehe die gleiche Stelle
    /// in <see cref="AusgabenlisteViewModel.KategorieWaehlen"/>.
    /// </summary>
    [RelayCommand]
    private void KategorieWaehlen(KategorieFilterKnoten? knoten) =>
        AusgewaehlteFilterKategorie = knoten;

    [RelayCommand]
    private void AlleKategorien() => AusgewaehlteFilterKategorie = null;

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
        DetailSummeIstErstattung = EuroText.IsNegative(summary.SumCents);

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
            // NULL bei Summenzeile und Summenspalte: dort bleibt es beim
            // Kategoriefilter der Filterleiste.
            CategoryRootId = zelle.KategorieId ?? basis.CategoryRootId,
            Grouping = basis.Grouping,
            PayerScope = basis.PayerScope,
            PayerId = basis.PayerId,
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

        // Ist ein Kategorie-Ast gewaehlt, beginnt die Tabelle bei diesem
        // Knoten. Sonst waere die oberste Zeile immer die Oberkategorie mit
        // exakt derselben Summe - eine Zeile ohne Aussage.
        var wurzeln = filter.CategoryRootId is int astId
            ? FindeKnoten(baum, astId) is { } ast
                ? new List<CategoryNode> { ast }
                : new List<CategoryNode>()
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
            return;
        }

        FuegeZeilenEin(_matrix.Rows);

        SummenZeile = ReportZeile.FuerSumme(_matrix, Spalten);
        KeineTreffer = _matrix.Total.Count == 0;
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

        filter = new ReportFilter
        {
            From = zeitraum.From,
            To = zeitraum.ToExclusive,
            CategoryRootId = AusgewaehlteFilterKategorie?.Id,
            Grouping = AusgewaehlteGruppierung.Wert,
            PayerScope = AusgewaehlterZahlerBereich.Wert,
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
        var gewaehlteId = AusgewaehlteFilterKategorie?.Id;

        KategorieWurzeln.Clear();
        var baum = _categoryRepository.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);
        foreach (var knoten in BaueFilterKnoten(baum, pfade))
        {
            KategorieWurzeln.Add(knoten);
        }

        AusgewaehlteFilterKategorie = gewaehlteId is int id
            ? FindeFilterKnoten(KategorieWurzeln, id)
            : null;
    }

    private static List<KategorieFilterKnoten> BaueFilterKnoten(
        IReadOnlyList<CategoryNode> nodes, IReadOnlyDictionary<int, string> pfade)
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
                node.Category.IsArchived);

            knoten.Children.AddRange(BaueFilterKnoten(node.Children, pfade));
            ergebnis.Add(knoten);
        }

        return ergebnis;
    }

    private static KategorieFilterKnoten? FindeFilterKnoten(
        IEnumerable<KategorieFilterKnoten> knoten, int id)
    {
        foreach (var kandidat in knoten)
        {
            if (kandidat.Id == id)
            {
                return kandidat;
            }

            var gefunden = FindeFilterKnoten(kandidat.Children, id);
            if (gefunden is not null)
            {
                return gefunden;
            }
        }

        return null;
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
