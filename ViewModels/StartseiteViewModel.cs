using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Charts;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die neue Startseite (UI/UX-Redesign, Abschnitt 4): Überblick statt
/// leerem Formular als erster Eindruck. Reiner Lesezugriff auf bereits
/// vorhandene Core-Abfragen - keine neue Fachlogik, nur eine neue
/// Zusammenstellung (Regel 7):
///
/// - KPI-Kacheln: <see cref="ExpenseRepository.Query"/> mit demselben
///   Zahler-Filter <see cref="PayerScope.SelfAndOpen"/>, den auch die
///   Voreinstellung des Reports benutzt ("Ich + offene Posten &amp;
///   Einnahmen"), <see cref="OpenItemsRepository.GetOpen"/> und
///   <see cref="RecurringExpenseRepository.GetAllActive"/> mit
///   <see cref="RecurrenceGenerator"/>.
/// - Netto-Trend: dieselbe Aggregation je Monat der letzten sechs Monate.
/// - Letzte Buchungen: dasselbe Muster wie ErfassenViewModel.LetzteAusgaben.
/// </summary>
public sealed partial class StartseiteViewModel : ViewModelBase
{
    private readonly ExpenseRepository _expenseRepository;
    private readonly OpenItemsRepository _openItemsRepository;
    private readonly RecurringExpenseRepository _recurringExpenseRepository;
    private readonly ReportRepository _reportRepository;

    /// <summary>Wird ausgeloest, wenn "Ausgabe erfassen" gewaehlt wird - siehe MainViewModel.</summary>
    public event EventHandler? ErfassenAngefordert;

    /// <summary>Wird ausgeloest, wenn "Alle Buchungen ansehen" gewaehlt wird - siehe MainViewModel.</summary>
    public event EventHandler? AusgabenlisteAngefordert;

    // ---- Spruenge aus den vier KPI-Kacheln ----
    //
    // Jede Kachel meldet nur an, WOHIN es gehen soll; die Startseite kennt
    // die Navigation nicht (dasselbe Muster wie ZeitraumAngefordert und
    // ErfassenAngefordert - der MainViewModel hoert zu und setzt um).

    /// <summary>Anfang des Monats, dessen Ausgaben gezeigt werden sollen.</summary>
    public event EventHandler<DateOnly>? AusgabenMonatAngefordert;

    /// <summary>Anfang des Monats, dessen Einnahmen gezeigt werden sollen.</summary>
    public event EventHandler<DateOnly>? EinnahmenMonatAngefordert;

    public event EventHandler? OffenePostenAngefordert;

    /// <summary>Id der Vorlage, die als naechste faellig ist.</summary>
    public event EventHandler<int>? NaechsteFaelligkeitAngefordert;

    /// <summary>Wird ausgeloest, wenn eine Zeile unter "Letzte Buchungen"
    /// angeklickt wird - siehe MainViewModel.</summary>
    public event EventHandler<LetzteAusgabeZeile>? BuchungAngefordert;

    [ObservableProperty] private string _monatUeberschrift = string.Empty;

    [ObservableProperty] private string _ausgabenMonatText = string.Empty;
    [ObservableProperty] private string _ausgabenMonatHinweis = string.Empty;

    /// <summary>
    /// Ob die Kachel ueberhaupt etwas zu zeigen hat. Ist sie leer, bleibt
    /// ihr Knopf ausgegraut - ein Sprung ins Nichts ist schlimmer als kein
    /// Sprung.
    /// </summary>
    [ObservableProperty] private bool _ausgabenMonatVorhanden;

    [ObservableProperty] private string _einnahmenMonatText = string.Empty;
    [ObservableProperty] private string _einnahmenMonatHinweis = string.Empty;
    [ObservableProperty] private bool _einnahmenMonatVorhanden;

    [ObservableProperty] private string _offenePostenText = string.Empty;
    [ObservableProperty] private string _offenePostenHinweis = string.Empty;
    [ObservableProperty] private bool _offenePostenVorhanden;

    [ObservableProperty] private string _naechsteFaelligkeitTitel = string.Empty;
    [ObservableProperty] private string _naechsteFaelligkeitHinweis = string.Empty;
    [ObservableProperty] private bool _naechsteFaelligkeitVorhanden;

    // Die Vorlage hinter der vierten Kachel - fuer den Sprung in den
    // Vorlagenbereich, der genau diese Zeile hervorheben soll.
    private int? _naechsteVorlageId;

    // Der Monat, aus dem die beiden Monatskacheln gerade gerechnet sind.
    // Gemerkt statt beim Klick neu bestimmt: laeuft die Anwendung ueber
    // Mitternacht des Monatsersten hinweg, zeigte die Liste sonst einen
    // anderen Monat als die Kachel darueber.
    private DateOnly _kachelMonat;

    public ObservableCollection<LetzteAusgabeZeile> LetzteBuchungen { get; } = new();

    // ================= Diagramm =================
    //
    // Gezeichnet wird auf EINER Zeichenflaeche, deren Groesse die Ansicht
    // meldet (siehe ZeichenflaecheGeaendert). Die gesamte Geometrie
    // rechnet Core (Charts.BarChart) - hier wird nur uebersetzt, was
    // dort herauskommt, und um den Rand fuer die Achsenbeschriftung
    // verschoben.

    [ObservableProperty] private IReadOnlyList<DiagrammBalken> _diagrammBalken = [];
    [ObservableProperty] private IReadOnlyList<DiagrammLinie> _diagrammLinien = [];
    [ObservableProperty] private IReadOnlyList<DiagrammWertBeschriftung> _diagrammWertachse = [];
    [ObservableProperty] private IReadOnlyList<DiagrammZeitBeschriftung> _diagrammZeitachse = [];

    /// <summary>Kein Balken zu zeichnen - dann steht ein Satz statt einer
    /// leeren Flaeche.</summary>
    [ObservableProperty] private bool _diagrammLeer = true;

    // ================= Verlauf in den Kacheln =================
    //
    // Die kleinen Linien unter den beiden Monatszahlen. Sie stehen FEST
    // auf zwoelf Monaten und haengen bewusst NICHT am Umschalter des
    // Diagramms darunter: eine Kachel, deren Verlauf beim Umschalten von
    // 6 auf 24 Monate seine Bedeutung wechselt, ist schlimmer als keine.

    private IReadOnlyList<long> _verlaufAusgaben = [];
    private IReadOnlyList<long> _verlaufEinnahmen = [];

    // Beide Kacheln sind gleich breit - sie stehen in einem UniformGrid.
    // Deshalb reicht EINE gemeldete Groesse fuer beide Linien.
    private double _verlaufBreite;
    private double _verlaufHoehe;

    [ObservableProperty] private IReadOnlyList<Point> _ausgabenVerlauf = [];
    [ObservableProperty] private IReadOnlyList<Point> _einnahmenVerlauf = [];

    /// <summary>
    /// Ob es etwas zu zeigen gibt - haengt NUR an den Daten und NICHT
    /// daran, ob schon eine Groesse gemeldet wurde (siehe
    /// <see cref="ZeichneVerlauf"/>). Bei weniger als zwei Monaten mit
    /// Buchungen bleibt die Linie weg: eine waagerechte Linie behauptete
    /// sonst eine Ruhe, die nie gemessen wurde.
    /// </summary>
    [ObservableProperty] private bool _ausgabenVerlaufVorhanden;

    [ObservableProperty] private bool _einnahmenVerlaufVorhanden;

    [ObservableProperty] private string _diagrammUeberschrift = string.Empty;
    [ObservableProperty] private string _diagrammZusammenfassung = string.Empty;

    /// <summary>Ob die Detailansicht laeuft - steuert die Legende.</summary>
    [ObservableProperty] private bool _detailansicht = true;

    /// <summary>Fuer die Hervorhebung der aktiven Schaltflaechen (siehe
    /// TextGleich, dasselbe Muster wie die Zeitraum-Schnellwahl der
    /// Auswertung).</summary>
    [ObservableProperty] private string _aktiveAnsicht = AnsichtDetail;
    [ObservableProperty] private string _aktiverZeitraum = "12";

    public const string AnsichtDetail = "Detail";
    public const string AnsichtNetto = "Netto";

    // Zuletzt gemeldete Groesse der Zeichenflaeche und die zuletzt
    // geladenen Werte - beide werden gebraucht, sobald sich eines von
    // beiden aendert.
    private double _flaecheBreite;
    private double _flaecheHoehe;
    private IReadOnlyList<PeriodValue> _abschnitte = [];

    /// <summary>Wird ausgeloest, wenn ein Balken angeklickt wird - siehe MainViewModel.</summary>
    public event EventHandler<DateRange>? ZeitraumAngefordert;

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AlleBuchungenAnsehen() => AusgabenlisteAngefordert?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AusgabenMonatOeffnen()
        => AusgabenMonatAngefordert?.Invoke(this, _kachelMonat);

    [RelayCommand]
    private void EinnahmenMonatOeffnen()
        => EinnahmenMonatAngefordert?.Invoke(this, _kachelMonat);

    [RelayCommand]
    private void OffenePostenOeffnen()
        => OffenePostenAngefordert?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void NaechsteFaelligkeitOeffnen()
    {
        if (_naechsteVorlageId is int id)
        {
            NaechsteFaelligkeitAngefordert?.Invoke(this, id);
        }
    }

    /// <summary>
    /// Klick auf eine Zeile unter "Letzte Buchungen": zeigt genau diese
    /// eine Buchung in der Ausgabenliste.
    /// </summary>
    [RelayCommand]
    private void BuchungOeffnen(LetzteAusgabeZeile? zeile)
    {
        if (zeile is not null)
        {
            BuchungAngefordert?.Invoke(this, zeile);
        }
    }

    public StartseiteViewModel(
        ExpenseRepository expenseRepository,
        OpenItemsRepository openItemsRepository,
        RecurringExpenseRepository recurringExpenseRepository,
        ReportRepository reportRepository,
        IMessenger messenger)
    {
        _expenseRepository = expenseRepository;
        _openItemsRepository = openItemsRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _reportRepository = reportRepository;

        // Die Randbreiten haengen an der eingestellten Schriftgroesse -
        // wird sie verstellt, muss das Diagramm neu vermessen werden.
        Skalierung.Aktuell.PropertyChanged += (_, _) =>
        {
            ZeichneDiagramm();
            ZeichneVerlauf();
        };

        Aktualisiere();

        // Buchungsaenderungen aus anderen Bereichen sollen Kacheln,
        // Diagramm und "Letzte Buchungen" sofort aktualisieren, nicht erst
        // beim naechsten Navigieren zur Startseite (Regel 14).
        messenger.Register<StartseiteViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.Aktualisiere());
    }

    /// <summary>
    /// Meldet die Groesse der Zeichenflaeche. Kommt aus dem Code-Behind
    /// (SizeChanged) - das ist Verdrahtung, keine Fachlogik: gerechnet
    /// wird in Core.Charts.BarChart (Regel 7).
    /// </summary>
    public void ZeichenflaecheGeaendert(double breite, double hoehe)
    {
        _flaecheBreite = breite;
        _flaecheHoehe = hoehe;
        ZeichneDiagramm();
    }

    [RelayCommand]
    private void AnsichtWaehlen(string? ansicht)
    {
        AktiveAnsicht = ansicht ?? AnsichtDetail;
        Detailansicht = AktiveAnsicht != AnsichtNetto;
        ZeichneDiagramm();
    }

    [RelayCommand]
    private void ZeitraumWaehlen(string? monate)
    {
        AktiverZeitraum = monate ?? "12";
        LadeAbschnitte();
        ZeichneDiagramm();
    }

    /// <summary>
    /// Klick auf einen Balken: zeigt die Buchungen genau dieses
    /// Zeitabschnitts. Den Zeitraum zum Schluessel liefert
    /// <see cref="ReportPeriods.Range"/> - dieselbe Mechanik wie beim
    /// Sprung aus einer Zelle der Auswertung.
    /// </summary>
    [RelayCommand]
    private void MonatOeffnen(string? schluessel)
    {
        if (string.IsNullOrEmpty(schluessel))
        {
            return;
        }

        ZeitraumAngefordert?.Invoke(
            this, ReportPeriods.Range(schluessel, ReportGrouping.Month));
    }

    /// <summary>
    /// Laedt alle Kacheln neu. Wird bei jedem Wechsel zur Startseite
    /// aufgerufen (siehe MainViewModel), damit zwischenzeitlich in anderen
    /// Bereichen erfasste Buchungen ohne Neustart sichtbar werden -
    /// StartseiteViewModel ist ein DI-Singleton wie die uebrigen
    /// Bereichs-ViewModels.
    /// </summary>
    public void Aktualisiere()
    {
        var heute = DateOnly.FromDateTime(DateTime.Now);
        var de = CultureInfo.GetCultureInfo("de-DE");

        MonatUeberschrift = heute.ToString("MMMM yyyy", de) + " · hier ist der Überblick über eure Finanzen";

        AktualisiereMonatsKacheln(heute);
        AktualisiereOffenePosten();
        AktualisiereNaechsteFaelligkeit(heute);
        LadeAbschnitte();
        ZeichneDiagramm();
        LadeVerlauf(heute);
        ZeichneVerlauf();
        AktualisiereLetzteBuchungen();
    }

    private void AktualisiereMonatsKacheln(DateOnly heute)
    {
        var monatsAnfang = new DateOnly(heute.Year, heute.Month, 1);
        var naechsterMonat = monatsAnfang.AddMonths(1);
        _kachelMonat = monatsAnfang;

        var buchungen = _expenseRepository.Query(
            new ReportFilter
            {
                From = monatsAnfang,
                To = naechsterMonat,
                PayerScope = PayerScope.SelfAndOpen,
            },
            ExpenseSortColumn.Datum,
            ascending: true);

        var ausgaben = buchungen.Where(b => !b.IsIncome).ToList();

        // Wie ueberall sonst (siehe AusgabeZeile.IstBeglicheneEinnahme):
        // eine Einnahme zaehlt erst, wenn sie abgehakt ist. Eine noch
        // offene Einnahme eines fremden Zahlers ist noch nicht
        // zugeflossenes Geld (Regel 4) und darf die Kachel nicht erhoehen.
        var einnahmen = buchungen
            .Where(b => b.IsIncome && !b.PayerIsSelf && b.SettledDate is not null)
            .ToList();

        var ausgabenSumme = ausgaben.Sum(b => b.AmountCents);
        AusgabenMonatVorhanden = ausgaben.Count > 0;
        AusgabenMonatText = EuroText.Format(ausgabenSumme);
        AusgabenMonatHinweis = ausgaben.Count == 0
            ? "Noch keine Ausgabe diesen Monat"
            : $"{ausgaben.Count} {(ausgaben.Count == 1 ? "Buchung" : "Buchungen")} · Ø {EuroText.Format(ausgabenSumme / ausgaben.Count)}";

        var einnahmenSumme = einnahmen.Sum(b => b.AmountCents);
        EinnahmenMonatVorhanden = einnahmen.Count > 0;
        EinnahmenMonatText = EuroText.Format(einnahmenSumme);
        EinnahmenMonatHinweis = einnahmen.Count == 0
            ? "Noch keine Einnahme diesen Monat"
            : $"{einnahmen.Count} {(einnahmen.Count == 1 ? "Buchung" : "Buchungen")}";
    }

    private void AktualisiereOffenePosten()
    {
        var offen = _openItemsRepository.GetOpen();
        var summeCents = offen.Sum(o => o.AmountCents);
        OffenePostenText = EuroText.Format(summeCents);
        OffenePostenVorhanden = offen.Count > 0;

        if (offen.Count == 0)
        {
            OffenePostenHinweis = "Keine offenen Posten";
            return;
        }

        var zahler = offen.Select(o => o.PayerName).Distinct().ToList();
        var vonWem = zahler.Count == 1 ? $" von {zahler[0]}" : string.Empty;
        OffenePostenHinweis = $"{offen.Count} {(offen.Count == 1 ? "unbeglichene Buchung" : "unbeglichene Buchungen")}{vonWem}";
    }

    private void AktualisiereNaechsteFaelligkeit(DateOnly heute)
    {
        var naechste = _recurringExpenseRepository.GetAllActive()
            .Select(vorlage => new
            {
                Vorlage = vorlage,
                Naechste = RecurrenceGenerator.GetNextDueDate(
                    vorlage.StartDate, vorlage.EndDate, vorlage.IntervalUnit,
                    vorlage.IntervalCount, vorlage.AnchorDay, heute),
            })
            .Where(x => x.Naechste is not null)
            .OrderBy(x => x.Naechste)
            .FirstOrDefault();

        if (naechste is null)
        {
            _naechsteVorlageId = null;
            NaechsteFaelligkeitVorhanden = false;
            NaechsteFaelligkeitTitel = "Keine aktive Vorlage";
            NaechsteFaelligkeitHinweis = string.Empty;
            return;
        }

        _naechsteVorlageId = naechste.Vorlage.Id;
        NaechsteFaelligkeitVorhanden = true;
        NaechsteFaelligkeitTitel = naechste.Vorlage.Title;
        NaechsteFaelligkeitHinweis =
            $"{EuroText.FormatSigned(naechste.Vorlage.AmountCents, naechste.Vorlage.IsIncome)} · fällig {GermanDateInput.ToText(naechste.Naechste!.Value)}";
    }

    /// <summary>
    /// Holt die Werte je Monat. EINE Abfrage fuer den ganzen Zeitraum
    /// (frueher: eine je Monat), und die Rechnung, was als Ausgabe und
    /// was als Einnahme zaehlt, steckt vollstaendig in dieser Abfrage -
    /// nicht mehr hier (Regel 7).
    ///
    /// Fruehere Fassungen zaehlten hier clientseitig ALLE Einnahmen,
    /// auch noch offene. Damit stand auf der Startseite Geld als
    /// vorhanden, das noch aussteht, und die Zahl wich von der
    /// Auswertung ab.
    /// </summary>
    private void LadeAbschnitte()
    {
        var heute = DateOnly.FromDateTime(DateTime.Now);
        var monate = int.TryParse(AktiverZeitraum, out var gewaehlt) ? gewaehlt : 12;

        var erster = new DateOnly(heute.Year, heute.Month, 1).AddMonths(-(monate - 1));
        var zeitraum = new DateRange(erster, new DateOnly(heute.Year, heute.Month, 1).AddMonths(1));

        var zeilen = _reportRepository
            .EvaluateTrend(zeitraum, ReportGrouping.Month)
            .ToDictionary(zeile => zeile.GroupKey);

        // Ueber die lueckenlose Abschnittsfolge laufen, nicht ueber das
        // Abfrageergebnis: ein Monat ganz ohne Buchungen muss als Luecke
        // stehen bleiben, sonst ruecken die uebrigen zusammen und die
        // Zeitachse stimmt nicht mehr.
        var ersterSchluessel = ReportPeriods.Key(erster, ReportGrouping.Month);
        var letzterSchluessel = ReportPeriods.Key(heute, ReportGrouping.Month);

        _abschnitte = ReportPeriods
            .Enumerate(ersterSchluessel, letzterSchluessel, ReportGrouping.Month)
            .Select(schluessel =>
            {
                zeilen.TryGetValue(schluessel, out var zeile);

                return new PeriodValue(
                    schluessel,
                    ReportPeriods.Label(schluessel, ReportGrouping.Month),
                    zeile?.OwnExpenseCents ?? 0,
                    zeile?.ForeignOpenExpenseCents ?? 0,
                    zeile?.IncomeCents ?? 0);
            })
            .ToList();
    }

    /// <summary>
    /// Die zwoelf Monatswerte fuer die Linien in den Kacheln.
    ///
    /// Eine eigene Abfrage und nicht der Ausschnitt aus
    /// <see cref="LadeAbschnitte"/>: dessen Zeitraum haengt am Umschalter
    /// des Diagramms, dieser hier darf es nicht. Zwei Abfragen gegen eine
    /// oertliche Datei kosten nichts - dieselbe Ueberlegung wie im
    /// Jahresrueckblick.
    /// </summary>
    private void LadeVerlauf(DateOnly heute)
    {
        const int monate = 12;

        var monatsAnfang = new DateOnly(heute.Year, heute.Month, 1);
        var erster = monatsAnfang.AddMonths(-(monate - 1));
        var zeitraum = new DateRange(erster, monatsAnfang.AddMonths(1));

        var zeilen = _reportRepository
            .EvaluateTrend(zeitraum, ReportGrouping.Month)
            .ToDictionary(zeile => zeile.GroupKey);

        // Wie beim Diagramm ueber die lueckenlose Folge laufen: ein Monat
        // ohne Buchungen ist eine 0 und keine Luecke, sonst ruecken die
        // uebrigen zusammen und die Linie behauptet einen Verlauf, den es
        // nicht gab.
        var schluessel = ReportPeriods.Enumerate(
            ReportPeriods.Key(erster, ReportGrouping.Month),
            ReportPeriods.Key(heute, ReportGrouping.Month),
            ReportGrouping.Month);

        var ausgaben = new List<long>(monate);
        var einnahmen = new List<long>(monate);

        foreach (var key in schluessel)
        {
            zeilen.TryGetValue(key, out var zeile);

            ausgaben.Add(
                (zeile?.OwnExpenseCents ?? 0) + (zeile?.ForeignOpenExpenseCents ?? 0));
            einnahmen.Add(zeile?.IncomeCents ?? 0);
        }

        _verlaufAusgaben = ausgaben;
        _verlaufEinnahmen = einnahmen;
    }

    /// <summary>
    /// Meldung der Ansicht ueber die Groesse der Linienflaeche. Eine
    /// Meldung genuegt fuer beide Kacheln, siehe
    /// <see cref="_verlaufBreite"/>.
    /// </summary>
    public void VerlaufflaecheGeaendert(double breite, double hoehe)
    {
        _verlaufBreite = breite;
        _verlaufHoehe = hoehe;
        ZeichneVerlauf();
    }

    private void ZeichneVerlauf()
    {
        // ZWEI getrennte Fragen, und das ist hier kein Feinschliff:
        //
        //   "Gibt es etwas zu zeigen?"  haengt NUR an den Daten.
        //   "Wie sieht die Linie aus?"  haengt an der gemeldeten Groesse.
        //
        // Beides in einem Merkmal zu fuehren hat die Sparkline schon
        // einmal vollstaendig verschwinden lassen: die Flaeche meldet ihre
        // Groesse ueber SizeChanged, ein unsichtbares Element wird aber gar
        // nicht erst vermessen. Haengt seine Sichtbarkeit am Ergebnis der
        // Groessenrechnung, wird sie nie wahr - und es meldet nie.
        AusgabenVerlaufVorhanden = HatVerlauf(_verlaufAusgaben);
        EinnahmenVerlaufVorhanden = HatVerlauf(_verlaufEinnahmen);

        AusgabenVerlauf = Linie(_verlaufAusgaben);
        EinnahmenVerlauf = Linie(_verlaufEinnahmen);
    }

    /// <summary>
    /// Ob die Reihe ueberhaupt etwas aussagt. Ein einziger Monat mit einer
    /// Buchung ergibt eine Linie, die elf Monate auf der Nulllinie liegt
    /// und dann hochschnellt - das sieht nach einem Ausbruch aus und ist
    /// doch nur "hier faengt es an".
    /// </summary>
    private static bool HatVerlauf(IReadOnlyList<long> werte)
        => werte.Count(wert => wert != 0) >= 2;

    /// <summary>
    /// Die Punkte der Linie. Leer, solange die Ansicht ihre Groesse noch
    /// nicht gemeldet hat - das ist der Zustand unmittelbar nach dem
    /// Aufbau und kein Fehler.
    /// </summary>
    private IReadOnlyList<Point> Linie(IReadOnlyList<long> werte)
    {
        if (!HatVerlauf(werte))
        {
            return [];
        }

        var layout = Sparkline.Compute(werte, _verlaufBreite, _verlaufHoehe);

        return layout.IsEmpty
            ? []
            : layout.Points.Select(p => new Point(p.X, p.Y)).ToList();
    }

    /// <summary>
    /// Uebersetzt das Ergebnis von Core.Charts in zeichenbare Elemente.
    ///
    /// Der linke Rand traegt die Wertachse, der untere die Zeitachse -
    /// beide wachsen mit der eingestellten Schriftgroesse, sonst
    /// ueberdeckten sich Beschriftung und Zeichenflaeche bei "Sehr
    /// gross" (Regel 9).
    /// </summary>
    private void ZeichneDiagramm()
    {
        var faktor = Skalierung.Aktuell.Faktor;
        var linkerRand = Math.Round(64 * faktor);
        var untererRand = Math.Round(24 * faktor);

        // Oben Luft lassen: die oberste Achsenbeschriftung sitzt mittig
        // auf ihrem Strich und ragte sonst zur Haelfte ueber den Rand
        // hinaus - sie war dadurch abgeschnitten.
        var obererRand = Math.Round(12 * faktor);

        var breite = _flaecheBreite - linkerRand;
        var hoehe = _flaecheHoehe - untererRand - obererRand;

        var layout = Detailansicht
            ? BarChart.Detailed(_abschnitte, breite, hoehe)
            : BarChart.Net(_abschnitte, breite, hoehe);

        DiagrammLeer = layout.IsEmpty;
        AktualisiereUeberschrift();

        if (layout.IsEmpty)
        {
            DiagrammBalken = [];
            DiagrammLinien = [];
            DiagrammWertachse = [];
            DiagrammZeitachse = [];
            return;
        }

        DiagrammBalken = layout.Bars
            .Select(balken => new DiagrammBalken(
                balken.Key,
                balken.X + linkerRand,
                balken.Y + obererRand,
                Math.Max(1, balken.Width),
                Math.Max(1, balken.Height),
                balken.Kind,
                Hinweistext(balken)))
            .ToList();

        DiagrammLinien = layout.Lines
            .Select(linie => new DiagrammLinie(
                new Point(linkerRand, linie.Y + obererRand),
                new Point(linkerRand + breite, linie.Y + obererRand),
                linie.Kind,
                linie.Kind is ChartLineKind.Grid or ChartLineKind.Zero
                    ? null
                    : Durchschnittstext(linie)))
            .ToList();

        DiagrammWertachse = layout.Ticks
            .Select(strich => new DiagrammWertBeschriftung(
                // Die Beschriftung sitzt mittig auf ihrem Strich; die
                // halbe Zeilenhoehe schaetzt sich aus der Schriftgroesse.
                strich.Y + obererRand - Math.Round(8 * faktor),
                linkerRand - Math.Round(8 * faktor),
                EuroText.Axis(strich.ValueCents)))
            .ToList();

        // Bei vielen Abschnitten wird nur jeder n-te beschriftet. Sonst
        // stehen die Monatsnamen so dicht, dass sie ineinanderlaufen -
        // und dann ist gar keiner mehr lesbar.
        //
        // Wie viele hineinpassen, haengt an der tatsaechlichen Breite und
        // an der Schriftgroesse, nicht an einer festen Zahl: dieselben 24
        // Monate brauchen in einem schmalen Fenster oder bei Stufe "Sehr
        // gross" deutlich mehr Platz. "Mär 2026" ist die laengste
        // vorkommende Beschriftung und dient als Mass.
        var abschnittsBreite = _abschnitte.Count == 0 ? 0 : breite / _abschnitte.Count;
        var mindestBreite = Math.Round(72 * faktor);
        var passendeAnzahl = Math.Max(1, (int)(breite / mindestBreite));
        var schrittweite = Math.Max(
            1, (int)Math.Ceiling(_abschnitte.Count / (double)passendeAnzahl));

        // Wird nur jeder n-te Monat beschriftet, steht dem Text auch der
        // Platz der uebersprungenen zur Verfuegung - sonst bliebe von
        // "Okt 2025" nur "Okt 2..." uebrig, und das Jahr ist gerade bei
        // langen Zeitraeumen die wichtigere Haelfte.
        var textBreite = abschnittsBreite * schrittweite;
        var versatz = (textBreite - abschnittsBreite) / 2;

        DiagrammZeitachse = _abschnitte
            .Select((abschnitt, i) => (abschnitt, i))
            // Von hinten zaehlen, damit der juengste Monat immer
            // beschriftet ist - er ist der, den man zuerst sucht.
            .Where(x => (_abschnitte.Count - 1 - x.i) % schrittweite == 0)
            .Select(x => new DiagrammZeitBeschriftung(
                // In die Flaeche einpassen: das breite Textfeld des
                // ersten und des letzten Monats ragte sonst ueber den
                // Rand hinaus und wurde dort abgeschnitten.
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

    private void AktualisiereUeberschrift()
    {
        var monate = _abschnitte.Count;

        DiagrammUeberschrift = Detailansicht
            ? $"Ausgaben und Einnahmen, letzte {monate} Monate"
            : $"Netto, letzte {monate} Monate";

        if (_abschnitte.Count == 0)
        {
            DiagrammZusammenfassung = string.Empty;
            return;
        }

        // Die Zahl unter der Ueberschrift beantwortet die Frage, die das
        // Diagramm sonst nur zeigt, aber nicht sagt.
        if (Detailansicht)
        {
            var getragen = _abschnitte.Sum(a => a.BorneExpenseCents) / _abschnitte.Count;
            var eingenommen = _abschnitte.Sum(a => a.IncomeCents) / _abschnitte.Count;

            DiagrammZusammenfassung =
                $"Im Schnitt {EuroText.Format(getragen)} getragen, "
                + $"{EuroText.Format(eingenommen)} eingenommen";
        }
        else
        {
            var netto = _abschnitte.Sum(a => a.NetCents) / _abschnitte.Count;
            DiagrammZusammenfassung = $"Im Schnitt {EuroText.Format(netto)} je Monat";
        }
    }

    private string Hinweistext(ChartBar balken)
    {
        var abschnitt = _abschnitte.FirstOrDefault(a => a.Key == balken.Key);
        var monat = abschnitt?.Label ?? balken.Key;

        var was = balken.Kind switch
        {
            BarKind.OwnExpenses => "Selbst gezahlt",
            BarKind.ForeignOpenExpenses => "Ausgelegt, noch offen",
            BarKind.Income => "Eingenommen",
            _ => "Netto",
        };

        return $"{monat}\n{was}: {EuroText.Format(balken.ValueCents)}\n\nKlicken zeigt die Buchungen";
    }

    private static string Durchschnittstext(ChartLine linie) => linie.Kind switch
    {
        ChartLineKind.AverageExpenses =>
            $"Durchschnittlich getragen: {EuroText.Format(linie.ValueCents)}",
        ChartLineKind.AverageIncome =>
            $"Durchschnittlich eingenommen: {EuroText.Format(linie.ValueCents)}",
        _ => $"Durchschnittliches Netto: {EuroText.Format(linie.ValueCents)}",
    };

    private void AktualisiereLetzteBuchungen()
    {
        LetzteBuchungen.Clear();
        foreach (var buchung in _expenseRepository.GetRecent(5))
        {
            LetzteBuchungen.Add(new LetzteAusgabeZeile(buchung));
        }
    }
}

/// <summary>
/// Ein Rechteck im Diagramm, fertig platziert. Die Farbe waehlt die
/// Ansicht ueber die Klassenmerkmale - das ViewModel kennt keine
/// Farbwerte, sonst waeren sie nicht mehr themenabhaengig.
/// </summary>
public sealed class DiagrammBalken
{
    public DiagrammBalken(
        string schluessel, double x, double y, double breite, double hoehe,
        BarKind art, string hinweis)
    {
        Schluessel = schluessel;
        X = x;
        Y = y;
        Breite = breite;
        Hoehe = hoehe;
        Hinweis = hinweis;

        IstEigeneAusgabe = art == BarKind.OwnExpenses;
        IstOffeneFremdausgabe = art == BarKind.ForeignOpenExpenses;
        IstEinnahme = art == BarKind.Income;
        IstNettoPositiv = art == BarKind.NetPositive;
        IstNettoNegativ = art == BarKind.NetNegative;
    }

    public string Schluessel { get; }
    public double X { get; }
    public double Y { get; }
    public double Breite { get; }
    public double Hoehe { get; }
    public string Hinweis { get; }

    public bool IstEigeneAusgabe { get; }
    public bool IstOffeneFremdausgabe { get; }
    public bool IstEinnahme { get; }
    public bool IstNettoPositiv { get; }
    public bool IstNettoNegativ { get; }
}

/// <summary>Eine waagerechte Linie: Gitternetz, Nulllinie oder Durchschnitt.</summary>
public sealed class DiagrammLinie
{
    public DiagrammLinie(Point von, Point bis, ChartLineKind art, string? hinweis)
    {
        Von = von;
        Bis = bis;
        Hinweis = hinweis;

        IstGitter = art == ChartLineKind.Grid;
        IstNulllinie = art == ChartLineKind.Zero;
        IstDurchschnittAusgaben = art == ChartLineKind.AverageExpenses;
        IstDurchschnittEinnahmen = art == ChartLineKind.AverageIncome;
        IstDurchschnittNetto = art == ChartLineKind.AverageNet;
        IstDurchschnitt = IstDurchschnittAusgaben || IstDurchschnittEinnahmen || IstDurchschnittNetto;
    }

    public Point Von { get; }
    public Point Bis { get; }
    public string? Hinweis { get; }

    public bool IstGitter { get; }
    public bool IstNulllinie { get; }
    public bool IstDurchschnitt { get; }
    public bool IstDurchschnittAusgaben { get; }
    public bool IstDurchschnittEinnahmen { get; }
    public bool IstDurchschnittNetto { get; }
}

/// <summary>Eine Beschriftung an der Wertachse (links).</summary>
public sealed record DiagrammWertBeschriftung(double Y, double Breite, string Text);

/// <summary>Eine Beschriftung an der Zeitachse (unten).</summary>
public sealed record DiagrammZeitBeschriftung(
    double X, double Y, double Breite, string Text, string Schluessel);
