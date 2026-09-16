using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Charts;
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
///
/// Die Filterleiste selbst - Zeitraum, Kategorien, Zahler, Status,
/// Buchungsart, Suche, die Chips darunter und die gespeicherten Staende -
/// steht in <see cref="FilterleisteViewModel"/> und ist dieselbe wie in
/// der Auswertung. Hier bleibt, was es nur hier gibt: Sortierung, die
/// beiden Sprungfilter, Bearbeiten, Loeschen, Duplizieren, die
/// Sammelaktionen und das Rueckgaengig-Band.
/// </summary>
public sealed partial class AusgabenlisteViewModel : FilterleisteViewModel
{

    private readonly ExpenseRepository _expenseRepository;
    private readonly IMessenger _messenger;

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

    // ---------------- Sprungfilter ----------------
    //
    // Zwei Einschraenkungen, die NICHT aus der Filterleiste kommen
    // (die steht in FilterleisteViewModel), sondern ueber einen Sprung
    // aus einem anderen Bereich.

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
            SettingsStore.Save(SettingsStore.Load() with
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

    public AusgabenlisteViewModel(
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        AppSettingsStore settingsStore,
        IMessenger messenger)
        : base(categoryRepository, personRepository, settingsStore)
    {
        _expenseRepository = expenseRepository;
        _messenger = messenger;

        LadenGesperrt = true;

        // Direkte Feldzuweisung: ueber die Eigenschaften wuerde
        // MerkeSortierung den gerade gelesenen Wert sofort wieder
        // zurueckschreiben (dasselbe Muster wie bei WerteBehalten in
        // ErfassenViewModel).
        var einstellungen = SettingsStore.Load();
        _sortSpalte = einstellungen.ExpenseListSortColumn;
        _sortAufsteigend = einstellungen.ExpenseListSortAscending;

        LadeAuswahllisten();
        SetzeVorgabeZeitraum();
        LadenGesperrt = false;
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

        LadenGesperrt = true;
        LadeAuswahllisten();
        LadenGesperrt = false;

        // Die gespeicherten Filter koennen sich in der Auswertung
        // geaendert haben - beide Bereiche fuehren dieselbe Liste.
        LadeGespeicherteFilter();

        LadeDaten();
    }

    /// <summary>
    /// Nimmt die Einschraenkung auf eine einzelne Buchung zurueck. Steht
    /// als eigene Methode da, weil sie an jeder Sprungmethode dabei sein
    /// muss: ueberlebte der Einzelfilter einen anderen Sprung, bliebe die
    /// Liste danach raetselhaft leer.
    /// </summary>
    // ---------------- Was die Filterleiste hier anders macht ----------------

    /// <summary>
    /// Die Einschraenkung auf eine Vorlage geht in den fertigen Filter
    /// ein (siehe <see cref="ZeigeVorlagenBuchungen"/>).
    /// </summary>
    protected override int? FilterVorlageId => _vorlageFilterId;

    /// <summary>Die Einschraenkung auf eine einzelne Buchung.</summary>
    protected override int? FilterBuchungId => _buchungFilterId;

    /// <summary>
    /// Beim Zuruecksetzen fallen auch die beiden Sprungfilter weg - sie
    /// stehen zwar nicht in der Leiste, schraenken die Liste aber genauso
    /// ein, und ein "Filter zuruecksetzen", das sie stehen liesse, waere
    /// eine Luege.
    /// </summary>
    protected override void SetzeEigeneVorgaben() => LeereSprungfilter();

    /// <summary>
    /// Vor einem gespeicherten Filter ebenso: bliebe die Einschraenkung
    /// auf eine Vorlage stehen, waere die Liste hinterher raetselhaft
    /// leer, obwohl der gewaehlte Filter passt.
    /// </summary>
    protected override void LeereEigeneEinschraenkungen() => LeereSprungfilter();

    /// <summary>
    /// Die Ziele der Sammelaktion "Kategorie ändern": nur Blattknoten,
    /// nicht archiviert - genau die, auf die sich eine Buchung ueberhaupt
    /// buchen laesst. Haengen am selben Neuaufbau wie der Filterbaum.
    /// </summary>
    protected override void LadeWeitereAuswahllisten()
    {
        SammelKategorien.Clear();
        foreach (var option in CategoryRepository.GetSelectableLeaves())
        {
            SammelKategorien.Add(option);
        }
    }

    // Beide Sprungfilter zusammen - sie gehoeren zusammen aufgehoben.
    private void LeereSprungfilter()
    {
        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();
    }

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
        LadenGesperrt = true;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;
        VonText = string.Empty;
        BisText = string.Empty;
        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;

        EinzelfilterLeeren();
        _vorlageFilterId = vorlageId;
        VorlageFilterText = $"Nur Buchungen aus Vorlage: {vorlageTitel}";
        LadenGesperrt = false;

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
        LadenGesperrt = true;
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
        LadenGesperrt = false;

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
        LadenGesperrt = true;
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
        LadenGesperrt = false;

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
    /// <paramref name="balkenArt"/> sagt, WELCHER Teil des Monats gemeint
    /// ist. Eine Saeule der Detailansicht ist gestapelt; wer auf
    /// "Ausgelegt, noch offen" klickt, will diese Auslagen sehen und
    /// nicht den ganzen Monat. Die drei Faelle bilden genau die drei
    /// Spalten der Trendabfrage nach (siehe
    /// ReportRepository.EvaluateTrend) - Liste und Balken zeigen so
    /// dieselbe Menge:
    ///
    /// - eigene Ausgaben: Ausgaben, Zahler bin ich;
    /// - ausgelegt und noch offen: Ausgaben mit dem Haekchen "offen" -
    ///   das bedeutet in der Abfrage bereits "fremder Zahler UND kein
    ///   Begleichungsdatum" (Regel 4, siehe ReportFilterSql.Where);
    /// - Einnahmen: Einnahmen mit dem Haekchen "beglichen" - erst dann
    ///   ist das Geld tatsaechlich geflossen.
    ///
    /// Ein Netto-Balken ist die Summe von allem und schraenkt deshalb
    /// nicht weiter ein.
    ///
    /// <paramref name="bisEinschliesslich"/> ist der letzte Tag, der noch
    /// dazugehoert - die Filterleiste versteht ihre beiden Felder
    /// einschliessend (siehe DateRangePresets.FromInclusiveBounds).
    /// </summary>
    public void ZeigeZeitraum(
        DateOnly von, DateOnly bisEinschliesslich, BarKind balkenArt)
    {
        LadenGesperrt = true;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

        VonText = GermanDateInput.ToText(von);
        BisText = GermanDateInput.ToText(bisEinschliesslich);

        switch (balkenArt)
        {
            case BarKind.OwnExpenses:
                NurAusgaben = true;

                // "Zahler bin ich" steht in der Zahlerliste und nicht in
                // "Meine Kosten": letzteres zaehlt die offenen Auslagen
                // anderer mit (PayerScope.SelfAndOpen) und waere damit die
                // ganze Saeule statt ihres unteren Abschnitts.
                ZahlerOptionen
                    .FirstOrDefault(option => option.IstSelbst)
                    ?.SetzeStill(true);
                break;

            case BarKind.ForeignOpenExpenses:
                NurAusgaben = true;
                StatusOffen = true;
                break;

            case BarKind.Income:
                NurEinnahmen = true;
                StatusBeglichen = true;
                break;
        }

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;
        LadenGesperrt = false;

        LadeDaten();
    }

    /// <summary>
    /// Zeigt die AUSGABEN einer Kategorie in einem Monat. Wird aus der
    /// Karte "Wofuer diesen Monat" auf der Startseite heraus aufgerufen.
    ///
    /// Anders als <see cref="ZeigeKategorieZeitraum"/> schraenkt diese
    /// Fassung zusaetzlich ein - genau so, wie die Karte gerechnet ist:
    /// nur Ausgaben, und davon nur die selbst getragenen (eigene plus noch
    /// offene fremde, also "Meine Kosten"). Ohne diese beiden Haekchen
    /// stuenden in der Liste Buchungen, die zur Zahl auf der Karte gar
    /// nichts beigetragen haben - eine schon zurueckgezahlte Auslage etwa.
    /// </summary>
    public void ZeigeKategorieAusgabenImMonat(
        int kategorieId, DateOnly von, DateOnly bisEinschliesslich)
    {
        LadenGesperrt = true;
        AktiverZeitraumSchluessel = null;
        FilterAuswahlLeeren();
        Suchtext = string.Empty;

        _vorlageFilterId = null;
        VorlageFilterText = null;
        EinzelfilterLeeren();

        HakeKategorieAn(KategorieWurzeln, kategorieId);

        VonText = GermanDateInput.ToText(von);
        BisText = GermanDateInput.ToText(bisEinschliesslich);

        NurAusgaben = true;
        MeineKosten = true;

        SortSpalte = ExpenseSortColumn.Datum;
        SortAufsteigend = false;
        LadenGesperrt = false;

        OnPropertyChanged(nameof(KategorieFilterText));
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
        LadenGesperrt = true;
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
        LadenGesperrt = false;

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

    [RelayCommand]
    private void SpalteSortieren(string? spalte)
    {
        if (Sortierung.Spalte<ExpenseSortColumn>(spalte) is not { } geklickt)
        {
            return;
        }

        (SortSpalte, SortAufsteigend) =
            Sortierung.NaechsteRichtung(geklickt, SortSpalte, SortAufsteigend);

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
            CategoryRepository.GetSelectableLeaves(),
            PersonRepository.GetAllActive());
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

    protected override void LadeDaten()
    {
        if (LadenGesperrt)
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
        var farben = CategoryRepository.GetResolvedColors();

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
            : $"{Kultur.Anzahl(summary.Count)} Treffer";
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

    private void OnZeilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AusgabeZeile.IstAusgewaehlt))
        {
            AnzahlAusgewaehlt = Zeilen.Count(zeile => zeile.IstAusgewaehlt);
        }
    }

    private string KopfText(string bezeichnung, ExpenseSortColumn spalte) =>
        Sortierung.KopfText(bezeichnung, spalte, SortSpalte, SortAufsteigend);
}
