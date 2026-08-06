using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    // Unterdrueckt das Neuladen, solange mehrere Filterwerte auf einmal
    // gesetzt werden (Schnellwahl, Zuruecksetzen, Neuaufbau der
    // Auswahllisten) - sonst laeuft die Abfrage pro Eigenschaft erneut.
    private bool _ladenGesperrt;

    private IReadOnlyList<int> _zuLoeschendeIds = Array.Empty<int>();

    public ObservableCollection<AusgabeZeile> Zeilen { get; } = new();

    public ObservableCollection<KategorieFilterKnoten> KategorieWurzeln { get; } = new();

    public ObservableCollection<ZahlerOption> ZahlerOptionen { get; } = new();

    public IReadOnlyList<StatusOption> StatusOptionen { get; } = new[]
    {
        new StatusOption("alle", SettlementStatus.Alle),
        new StatusOption("nur offene", SettlementStatus.NurOffene),
        new StatusOption("nur beglichene", SettlementStatus.NurBeglichene),
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

    // Bewusst nullbar: die ComboBox schreibt beim Neuaufbau von
    // ZahlerOptionen kurzzeitig null zurueck, weil ihr bisher gewaehltes
    // Element aus der Liste verschwindet. NULL bedeutet hier "alle".
    [ObservableProperty]
    private ZahlerOption? _ausgewaehlterZahler;

    [ObservableProperty]
    private StatusOption _ausgewaehlterStatus;

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

    /// <summary>Die Summe der Treffer ist negativ - Ausgaben ueberwiegen.</summary>
    [ObservableProperty]
    private bool _summeIstAusgabe;

    /// <summary>Die Summe der Treffer ist positiv - Einnahmen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _summeIstEinnahme;

    [ObservableProperty]
    private bool _keineTreffer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAuswahl))]
    [NotifyCanExecuteChangedFor(nameof(AusgewaehlteLoeschenCommand))]
    private int _anzahlAusgewaehlt;

    public bool HatAuswahl => AnzahlAusgewaehlt > 0;

    // ---------------- Overlays ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BearbeitenAktiv))]
    private AusgabeBearbeitenViewModel? _bearbeiten;

    public bool BearbeitenAktiv => Bearbeiten is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschAnfrageAktiv))]
    private string? _loeschAnfrageText;

    public bool LoeschAnfrageAktiv => LoeschAnfrageText is not null;

    /// <summary>
    /// Ein Schreibfehler in der Liste selbst - beim Loeschen. Als Band
    /// ueber der Tabelle, damit sichtbar bleibt, WAS nicht geklappt hat,
    /// und die Liste unveraendert darunter stehen kann.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    public AusgabenlisteViewModel(
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;

        _ausgewaehlterStatus = StatusOptionen[0];

        _ladenGesperrt = true;
        LadeAuswahllisten();
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
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
        _ladenGesperrt = true;
        LadeAuswahllisten();
        _ladenGesperrt = false;

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
    partial void OnAusgewaehlteFilterKategorieChanged(KategorieFilterKnoten? value) => LadeDaten();
    partial void OnAusgewaehlterZahlerChanged(ZahlerOption? value) => LadeDaten();
    partial void OnAusgewaehlterStatusChanged(StatusOption value) => LadeDaten();
    partial void OnSuchtextChanged(string value) => LadeDaten();

    [RelayCommand]
    private void Schnellwahl(string? bereich)
    {
        AktiverZeitraumSchluessel = bereich;

        var heute = DateOnly.FromDateTime(DateTime.Now);

        // "alles" laesst beide Felder leer - eine leere Grenze ist die
        // natuerliche Schreibweise fuer "unbegrenzt" und vermeidet, dass
        // dort 01.01.0001 bzw. 31.12.9999 steht.
        DateRange? bereichWerte;
        switch (bereich)
        {
            case "DieserMonat":
                bereichWerte = DateRangePresets.ThisMonth(heute);
                break;
            case "DiesesJahr":
                bereichWerte = DateRangePresets.ThisYear(heute);
                break;
            case "Letzte12Monate":
                bereichWerte = DateRangePresets.LastTwelveMonths(heute);
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
        AktiverZeitraumSchluessel = null;
        AusgewaehlteFilterKategorie = null;
        AusgewaehlterZahler = ZahlerOptionen[0];
        AusgewaehlterStatus = StatusOptionen[0];
        Suchtext = string.Empty;
        _vorlageFilterId = null;
        VorlageFilterText = null;
        SetzeVorgabeZeitraum();
        _ladenGesperrt = false;

        LadeDaten();
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
        AusgewaehlteFilterKategorie = null;
        AusgewaehlterZahler = ZahlerOptionen[0];
        AusgewaehlterStatus = StatusOptionen[0];
        Suchtext = string.Empty;
        VonText = string.Empty;
        BisText = string.Empty;
        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;

        _vorlageFilterId = vorlageId;
        VorlageFilterText = $"Nur Buchungen aus Vorlage: {vorlageTitel}";
        _ladenGesperrt = false;

        LadeDaten();
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
    /// Waehlt einen Kategorie-Ast als Filter. Bewusst ein Command statt
    /// einer Bindung an TreeView.SelectedItem: der Baum steckt in einem
    /// Flyout, und beim Schliessen des Popups setzt der TreeView seine
    /// Auswahl zurueck - das wuerde ueber eine TwoWay-Bindung sofort
    /// wieder NULL in den Filter schreiben.
    /// </summary>
    [RelayCommand]
    private void KategorieWaehlen(KategorieFilterKnoten? knoten) =>
        AusgewaehlteFilterKategorie = knoten;

    [RelayCommand]
    private void AlleKategorien() => AusgewaehlteFilterKategorie = null;

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

        LoeschAnfrageText = null;
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
        LadeDaten();
    }

    [RelayCommand]
    private void BearbeitenAbbrechen() => Bearbeiten = null;

    // ---------------- Loeschen ----------------

    [RelayCommand]
    private void Loeschen(AusgabeZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        Bearbeiten = null;
        _zuLoeschendeIds = new[] { zeile.Id };
        LoeschAnfrageText =
            $"Diese Ausgabe wirklich loeschen?\n{zeile.LoeschBeschreibung}";
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private void AusgewaehlteLoeschen()
    {
        var ausgewaehlte = Zeilen.Where(zeile => zeile.IstAusgewaehlt).ToList();
        if (ausgewaehlte.Count == 0)
        {
            return;
        }

        Bearbeiten = null;
        _zuLoeschendeIds = ausgewaehlte.Select(zeile => zeile.Id).ToList();
        LoeschAnfrageText = ausgewaehlte.Count == 1
            ? $"Diese Ausgabe wirklich loeschen?\n{ausgewaehlte[0].LoeschBeschreibung}"
            : $"{ausgewaehlte.Count} Ausgaben wirklich loeschen?";
    }

    [RelayCommand]
    private void LoeschenBestaetigen()
    {
        // DeleteMany laeuft in einer Transaktion: entweder alle
        // ausgewaehlten Zeilen sind weg oder keine. Ein Fehler mittendrin
        // hinterlaesst also keine halb geleerte Auswahl.
        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Loeschen von Ausgaben",
            () => _expenseRepository.DeleteMany(_zuLoeschendeIds));

        if (SchreibFehlerText is not null)
        {
            // Die Nachfrage bleibt stehen: der Anwender kann es gleich
            // noch einmal versuchen, ohne die Auswahl neu zu treffen.
            return;
        }

        _zuLoeschendeIds = Array.Empty<int>();
        LoeschAnfrageText = null;
        LadeDaten();
    }

    [RelayCommand]
    private void LoeschenAbbrechen()
    {
        _zuLoeschendeIds = Array.Empty<int>();
        LoeschAnfrageText = null;
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

        foreach (var item in _expenseRepository.Query(filter, SortSpalte, SortAufsteigend))
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
        SummeIstAusgabe = EuroText.IsNegative(summary.SumCents);
        SummeIstEinnahme = EuroText.IsPositive(summary.SumCents);

        AnzahlAusgewaehlt = 0;
        KeineTreffer = summary.Count == 0;
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

        filter = new ReportFilter
        {
            From = zeitraum.From,
            To = zeitraum.ToExclusive,
            CategoryRootId = AusgewaehlteFilterKategorie?.Id,
            PayerId = AusgewaehlterZahler?.Id,
            Status = AusgewaehlterStatus.Wert,
            SearchText = string.IsNullOrWhiteSpace(Suchtext) ? null : Suchtext.Trim(),
            RecurringExpenseId = _vorlageFilterId,
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
        var gewaehlteKategorieId = AusgewaehlteFilterKategorie?.Id;
        var gewaehlteZahlerId = AusgewaehlterZahler?.Id;

        KategorieWurzeln.Clear();
        var baum = _categoryRepository.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);
        foreach (var knoten in BaueFilterKnoten(baum, pfade))
        {
            KategorieWurzeln.Add(knoten);
        }

        AusgewaehlteFilterKategorie = gewaehlteKategorieId is int kategorieId
            ? FindeKnoten(KategorieWurzeln, kategorieId)
            : null;

        ZahlerOptionen.Clear();
        ZahlerOptionen.Add(new ZahlerOption("alle", null));
        foreach (var person in _personRepository.GetAllActive())
        {
            ZahlerOptionen.Add(new ZahlerOption(person.Name, person.Id));
        }

        // Ein inzwischen archivierter Zahler faellt aus der Auswahl - der
        // Filter geht dann auf "alle" zurueck, statt auf einen Eintrag zu
        // zeigen, den es nicht mehr gibt.
        AusgewaehlterZahler =
            ZahlerOptionen.FirstOrDefault(option => option.Id == gewaehlteZahlerId)
            ?? ZahlerOptionen[0];
    }

    private static List<KategorieFilterKnoten> BaueFilterKnoten(
        IReadOnlyList<CategoryNode> nodes, IReadOnlyDictionary<int, string> pfade)
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
                node.Category.IsArchived);

            knoten.Children.AddRange(BaueFilterKnoten(node.Children, pfade));
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
