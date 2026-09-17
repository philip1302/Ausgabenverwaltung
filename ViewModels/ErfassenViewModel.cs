using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Erfassen" - schnelle Erfassung einzelner Ausgaben. Bindet nur:
/// Umrechnung (<see cref="Money"/>), Datumsparsen (<see cref="GermanDateInput"/>)
/// und das Ermitteln waehlbarer Kategorien (<see cref="CategoryRepository.GetSelectableLeaves"/>)
/// stecken in Core, hier passiert keine Fachlogik (Regel 7).
/// </summary>
public sealed partial class ErfassenViewModel : ViewModelBase
{
    /// <summary>
    /// So viele Kategorien passen als Schnellwahl ueber das Feld, ohne
    /// dass die Reihe zur zweiten Vorschlagsliste wird. Reine
    /// Anzeigegroesse - die Auswahl selbst trifft
    /// <see cref="CategoryRepository.GetMostUsed"/>.
    /// </summary>
    private const int SchnellwahlAnzahl = 5;

    /// <summary>
    /// Wie weit die Schnellwahl zurueckschaut. Ein Vierteljahr ist lang
    /// genug, dass auch monatliche Buchungen mehrfach vorkommen, und kurz
    /// genug, dass eine aufgegebene Gewohnheit wieder aus der Reihe
    /// verschwindet.
    /// </summary>
    private const int SchnellwahlTage = 90;

    private readonly ExpenseRepository _expenseRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PersonRepository _personRepository;
    private readonly AppSettingsStore _settingsStore;
    private readonly IMessenger _messenger;

    /// <summary>
    /// Wohin die Bestaetigung nach dem Speichern geht. Sie stand frueher
    /// als Band im Formular und schob beim Erscheinen alles darunter nach
    /// unten - genau waehrend der naechste Betrag getippt wird.
    /// </summary>
    private readonly ToastViewModel _toast;

    public event EventHandler? FokusBetragAngefordert;

    /// <summary>
    /// Bitte um einen Wechsel in die Ausgabenliste, eingeschraenkt auf
    /// genau die angeklickte Buchung. Der Bereich kennt die Navigation
    /// nicht selbst - der <see cref="MainViewModel"/> hoert zu und setzt
    /// sie um (dasselbe Muster wie in <see cref="StartseiteViewModel"/>).
    /// </summary>
    public event EventHandler<LetzteAusgabeZeile>? BuchungAngefordert;

    [ObservableProperty]
    private string _betragText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BetragFehlerSichtbar))]
    private string? _betragFehler;

    public bool BetragFehlerSichtbar => !string.IsNullOrEmpty(BetragFehler);

    /// <summary>
    /// Ob dieser Betrag eine allgemeine Einnahme ist statt einer Ausgabe
    /// (siehe Entities.Expense.IsIncome) - mindert die Summen in Liste und
    /// Auswertung, statt sie zu erhoehen.
    /// </summary>
    [ObservableProperty]
    private bool _istEinnahme;

    /// <summary>
    /// Erklaert, warum das Haekchen bei "Einnahme" gerade automatisch
    /// zurueckgesetzt wurde: es gibt ausser der Ich-Person niemanden, dem
    /// sich die Einnahme zuordnen liesse (siehe AktualisiereZahlerAuswahl).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinnahmeHinweisSichtbar))]
    private string? _einnahmeHinweisText;

    public bool EinnahmeHinweisSichtbar => EinnahmeHinweisText is not null;

    // ObservableCollection statt einmalig geladener Liste, damit in der
    // Verwaltung angelegte/umbenannte/archivierte Kategorien sichtbar
    // werden, ohne die Anwendung neu zu starten (siehe
    // AktualisiereKategorieVorschlaege, aufgerufen bei Navigation zu
    // diesem Bereich - ErfassenViewModel ist ein DI-Singleton und laedt
    // sonst nur einmal beim Start).
    public ObservableCollection<CategoryOption> KategorieVorschlaege { get; } = new();

    /// <summary>
    /// Die Kategorien, die zuletzt am haeufigsten gebraucht wurden - als
    /// Knopfreihe ueber dem Feld. Ein Klick spart das Tippen und Suchen;
    /// das Feld darunter bleibt der vollstaendige Weg und wird nicht
    /// ersetzt.
    /// </summary>
    public ObservableCollection<CategoryOption> Schnellwahl { get; } = new();

    public bool SchnellwahlSichtbar => Schnellwahl.Count > 0;

    [ObservableProperty]
    private CategoryOption? _ausgewaehlteKategorie;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KategorieFehlerSichtbar))]
    private string? _kategorieFehler;

    public bool KategorieFehlerSichtbar => !string.IsNullOrEmpty(KategorieFehler);

    [ObservableProperty]
    private string _datumText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumFehlerSichtbar))]
    private string? _datumFehler;

    public bool DatumFehlerSichtbar => !string.IsNullOrEmpty(DatumFehler);

    [ObservableProperty]
    private string? _bemerkung;

    // ---------------- Vorschlag aus der Historie ----------------

    /// <summary>
    /// Ab wie vielen Zeichen ueberhaupt nachgesehen wird. Bei ein, zwei
    /// Buchstaben passt fast nichts, und was passt, ist Zufall.
    /// </summary>
    private const int VorschlagMindestlaenge = 3;

    /// <summary>
    /// Wie lange nach dem letzten Tastendruck gewartet wird. Lang genug,
    /// dass beim Durchtippen einer Bemerkung nicht bei jedem Buchstaben
    /// eine Abfrage laeuft, kurz genug, dass das Angebot noch waehrend des
    /// Tippens erscheint.
    /// </summary>
    private static readonly TimeSpan VorschlagVerzoegerung = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _vorschlagCts;

    /// <summary>
    /// Die Werte hinter dem Angebotsband - was <see cref="VorschlagUebernehmen"/>
    /// einsetzen wuerde.
    /// </summary>
    private ExpenseSuggestion? _vorschlag;

    /// <summary>
    /// Das Angebot als Text ("Zuletzt: Lebensmittel · -42,90 € · Paul").
    /// NULL = kein Angebot.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VorschlagSichtbar))]
    private string? _vorschlagText;

    public bool VorschlagSichtbar => VorschlagText is not null;

    // Alle aktiven Personen, ungefiltert - die Quelle, aus der
    // AktualisiereZahlerAuswahl das tatsaechlich waehlbare ZahlerOptionen
    // aufbaut. Bei einer Einnahme faellt die Ich-Person dort heraus
    // (siehe AktualisiereZahlerAuswahl): eine Einnahme kommt immer von
    // jemand anderem, sonst liesse sich nie verfolgen, ob sie ueber die
    // Offene-Posten-Liste tatsaechlich eingegangen ist (Regel 4).
    private readonly List<Person> _allePersonen = new();

    // ObservableCollection statt einmalig geladener Liste, damit in der
    // Verwaltung angelegte/umbenannte/archivierte Kategorien sichtbar
    // werden, ohne die Anwendung neu zu starten (siehe
    // AktualisiereKategorieVorschlaege, aufgerufen bei Navigation zu
    // diesem Bereich - ErfassenViewModel ist ein DI-Singleton und laedt
    // sonst nur einmal beim Start).
    public ObservableCollection<Person> ZahlerOptionen { get; } = new();

    [ObservableProperty]
    private Person _ausgewaehlterZahler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZahlerFehlerSichtbar))]
    private string? _zahlerFehler;

    public bool ZahlerFehlerSichtbar => !string.IsNullOrEmpty(ZahlerFehler);

    /// <summary>
    /// Die Rueckfrage bei einem ungewoehnlichen, aber moeglichen Datum
    /// (siehe <see cref="DatePlausibility"/>). Ein Band am Formular, kein
    /// Dialog - Meldungen zu Feldern gehoeren an das Feld.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumRueckfrageSichtbar))]
    private string? _datumRueckfrageText;

    public bool DatumRueckfrageSichtbar => DatumRueckfrageText is not null;

    // Der Anwender hat das ungewoehnliche Datum bestaetigt. Verfaellt bei
    // jeder Aenderung des Datums, damit die Bestaetigung nicht fuer ein
    // anderes Datum gilt als das gezeigte.
    private bool _datumBestaetigt;

    /// <summary>
    /// Ein Fehler beim Schreiben in die Datenbank. Steht als Band ueber
    /// dem Formular, und - das ist der Punkt - das Formular bleibt dabei
    /// vollstaendig gefuellt. Nichts ist aergerlicher als eine geleerte
    /// Maske nach einem Fehler, den der Anwender nicht zu verantworten
    /// hat.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeicherFehlerSichtbar))]
    private string? _speicherFehlerText;

    public bool SpeicherFehlerSichtbar => SpeicherFehlerText is not null;

    public ObservableCollection<LetzteAusgabeZeile> LetzteAusgaben { get; } = new();

    /// <summary>
    /// Serienerfassung: Kategorie, Zahler und Datum bleiben nach dem
    /// Speichern stehen, geleert werden nur Betrag und Bemerkung. Fuer
    /// den Stapel Belege, der am Monatsende auf dem Tisch liegt.
    ///
    /// Die Einstellung uebersteht den Neustart (siehe
    /// <see cref="AppSettings.KeepEntryValues"/>) - sie beschreibt eine
    /// Arbeitsweise, keine Laune eines Nachmittags.
    /// </summary>
    [ObservableProperty]
    private bool _werteBehalten;

    public ErfassenViewModel(
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        AppSettingsStore settingsStore,
        IMessenger messenger,
        ToastViewModel toast)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _settingsStore = settingsStore;
        _messenger = messenger;
        _toast = toast;

        // Direkte Feldzuweisung: ueber die Eigenschaft wuerde
        // OnWerteBehaltenChanged den gerade gelesenen Wert sofort wieder
        // zurueckschreiben.
        _werteBehalten = _settingsStore.Load().KeepEntryValues;

        AktualisiereKategorieVorschlaege();

        _allePersonen.AddRange(_personRepository.GetAllActive());
        _ausgewaehlterZahler = _allePersonen.First(p => p.IsSelf);
        AktualisiereZahlerAuswahl();

        _datumText = GermanDateInput.ToText(DateOnly.FromDateTime(DateTime.Now));

        LadeLetzteAusgaben();

        // Buchungsaenderungen aus anderen Bereichen (Abhaken in "Offene
        // Posten", Bearbeiten/Loeschen in der Ausgabenliste, Zusammenfuehren
        // von Kategorien) sollen hier sofort sichtbar werden, nicht erst
        // beim naechsten Navigieren zu "Erfassen" (Regel 14). Die
        // Schnellwahl haengt an denselben Daten und wandert mit.
        _messenger.Register<ErfassenViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) =>
            {
                empfaenger.LadeLetzteAusgaben();
                empfaenger.AktualisiereSchnellwahl();
            });
    }

    // Die Einstellung wird sofort gespeichert, nicht erst beim Beenden -
    // ein Absturz dazwischen darf sie nicht verschlucken. Immer mit "with"
    // auf dem gerade gelesenen Stand, sonst faellt alles Uebrige auf die
    // Vorgabewerte zurueck (siehe AppSettingsStore).
    partial void OnWerteBehaltenChanged(bool value)
    {
        // Ausdruecklich still: das hier ist ein Haekchen an einem
        // Formular. Ein Fehlerdialog, nur weil sich eine Bequemlichkeit
        // nicht merken laesst, waere unverhaeltnismaessig - fuer diese
        // Sitzung gilt die Einstellung ohnehin.
        try
        {
            _settingsStore.Save(_settingsStore.Load() with { KeepEntryValues = value });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Speichern der Serienerfassung", ex);
        }
    }

    /// <summary>
    /// Laedt die waehlbaren Kategorien neu. Wird bei Navigation zu diesem
    /// Bereich aufgerufen (siehe MainViewModel), damit Aenderungen aus der
    /// Verwaltung ohne Neustart sichtbar werden. Eine bereits ausgewaehlte
    /// Kategorie bleibt erhalten, sofern sie weiterhin waehlbar ist.
    /// </summary>
    public void AktualisiereKategorieVorschlaege()
    {
        var ausgewaehlteId = AusgewaehlteKategorie?.Id;

        KategorieVorschlaege.Clear();
        foreach (var option in _categoryRepository.GetSelectableLeaves())
        {
            KategorieVorschlaege.Add(option);
        }

        AusgewaehlteKategorie = ausgewaehlteId is int id
            ? KategorieVorschlaege.FirstOrDefault(o => o.Id == id)
            : null;

        AktualisiereSchnellwahl();
    }

    /// <summary>
    /// Baut die Schnellwahl neu auf. Laeuft mit den Vorschlaegen mit und
    /// nach jeder Buchungsaenderung: was gerade oft gebraucht wird, aendert
    /// sich mit jedem Beleg.
    /// </summary>
    private void AktualisiereSchnellwahl()
    {
        var seit = DateOnly.FromDateTime(DateTime.Now).AddDays(-SchnellwahlTage);

        Schnellwahl.Clear();
        foreach (var option in _categoryRepository.GetMostUsed(SchnellwahlAnzahl, seit))
        {
            Schnellwahl.Add(option);
        }

        OnPropertyChanged(nameof(SchnellwahlSichtbar));
    }

    /// <summary>
    /// Klick auf einen Schnellwahl-Knopf. Gesetzt wird die Kategorie aus
    /// den Vorschlaegen und nicht die angeklickte selbst: das Kategoriefeld
    /// vergleicht seine Auswahl ueber die Objektgleichheit, und zwei
    /// getrennt geladene <see cref="CategoryOption"/> mit derselben Id sind
    /// fuer es zwei verschiedene Kategorien.
    /// </summary>
    [RelayCommand]
    private void SchnellwahlWaehlen(CategoryOption? option)
    {
        if (option is null)
        {
            return;
        }

        AusgewaehlteKategorie = KategorieVorschlaege.FirstOrDefault(o => o.Id == option.Id);
    }

    /// <summary>
    /// Laedt die waehlbaren Zahler neu. Wird bei Navigation zu diesem
    /// Bereich aufgerufen (siehe MainViewModel), damit in der Verwaltung
    /// archivierte Personen ohne Neustart aus der Auswahl verschwinden. Ein
    /// bereits ausgewaehlter Zahler bleibt erhalten, sofern er weiterhin
    /// waehlbar ist - wird er inzwischen archiviert, faellt die Auswahl auf
    /// die IsSelf-Person zurueck (bzw. bei einer Einnahme auf die erste
    /// verbleibende fremde Person), siehe AktualisiereZahlerAuswahl.
    /// </summary>
    public void AktualisiereZahlerOptionen()
    {
        _allePersonen.Clear();
        _allePersonen.AddRange(_personRepository.GetAllActive());
        AktualisiereZahlerAuswahl();
    }

    // Eine Aenderung am Datum nimmt eine erteilte Bestaetigung zurueck:
    // sie galt fuer das Datum, das dabei stand, nicht fuer jedes weitere.
    partial void OnDatumTextChanged(string value)
    {
        _datumBestaetigt = false;
        DatumRueckfrageText = null;
    }

    partial void OnIstEinnahmeChanged(bool value) => AktualisiereZahlerAuswahl();

    // Eine neue Bemerkung loest ein neues Angebot aus - entprellt, damit
    // beim Durchtippen nicht bei jedem Buchstaben eine Abfrage laeuft.
    partial void OnBemerkungChanged(string? value) => StosseVorschlagAn(value);

    /// <summary>
    /// Sucht nach der zuletzt gleichlautend bemerkten Buchung und baut
    /// daraus das Angebotsband. Bewusst ohne await beim Aufrufer: das
    /// Angebot ist eine Zugabe, niemand wartet darauf.
    ///
    /// Der vorherige Durchlauf wird abgebrochen - sonst ueberholte das
    /// Angebot zu "Le" das zu "Lebensmittel".
    ///
    /// Nach dem Warten geht es auf dem Oberflaechen-Thread weiter: das
    /// await uebernimmt den SynchronizationContext, unter dem die
    /// Eigenschaftsaenderung gelaufen ist. Wichtig, weil die ganze
    /// Anwendung sich EINE Datenbankverbindung teilt - die Abfrage darf
    /// nicht nebenher auf einem zweiten Thread laufen.
    /// </summary>
    private async void StosseVorschlagAn(string? bemerkung)
    {
        _vorschlagCts?.Cancel();

        _vorschlag = null;
        VorschlagText = null;

        var text = bemerkung?.Trim() ?? string.Empty;
        if (text.Length < VorschlagMindestlaenge)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _vorschlagCts = cts;

        try
        {
            await Task.Delay(VorschlagVerzoegerung, cts.Token);

            if (_expenseRepository.SuggestFor(text) is not { } vorschlag)
            {
                return;
            }

            // Zwischen Abfrage und Anzeige kann weitergetippt worden sein.
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _vorschlag = vorschlag;
            VorschlagText =
                $"Zuletzt: {vorschlag.CategoryFullPath} · "
                + $"{EuroText.FormatSigned(vorschlag.AmountCents, vorschlag.IsIncome)} · "
                + $"{vorschlag.PayerName} ({GermanDateInput.ToText(vorschlag.ExpenseDate)})";
        }
        catch (TaskCanceledException)
        {
            // Es wurde weitergetippt - dieser Durchlauf ist ueberholt.
        }
        catch (Exception ex)
        {
            // Ein Vorschlag ist eine Bequemlichkeit. Scheitert die Abfrage,
            // bleibt das Band einfach weg - ein Fehlerdialog waehrend des
            // Tippens waere voellig unverhaeltnismaessig.
            AppLog.Current.Exception("Beim Suchen eines Vorschlags zur Bemerkung", ex);
        }
    }

    /// <summary>
    /// Setzt Kategorie, Betrag und Zahler des Angebots ein. Das Datum
    /// bleibt stehen - es gehoert dem Beleg, der gerade vor einem liegt,
    /// nicht dem von damals.
    ///
    /// Die Buchungsart wandert mit, obwohl sie kein Feld ist, das der
    /// Anwender gesucht hat: aus einer Einnahme von Anna wuerde sonst
    /// still eine Ausgabe an Anna.
    /// </summary>
    [RelayCommand]
    private void VorschlagUebernehmen()
    {
        if (_vorschlag is not { } vorschlag)
        {
            return;
        }

        // Erst die Art, dann der Zahler: die Art baut die Zahlerauswahl neu
        // auf (bei einer Einnahme faellt die Ich-Person heraus).
        IstEinnahme = vorschlag.IsIncome;

        BetragText = EuroText.Plain(vorschlag.AmountCents);
        AusgewaehlteKategorie = KategorieVorschlaege.FirstOrDefault(o => o.Id == vorschlag.CategoryId);

        if (ZahlerOptionen.FirstOrDefault(p => p.Id == vorschlag.PayerId) is { } zahler)
        {
            AusgewaehlterZahler = zahler;
        }

        // Das Angebot ist angenommen und hat sich damit erledigt.
        _vorschlag = null;
        VorschlagText = null;
    }

    [RelayCommand]
    private void VorschlagVerwerfen()
    {
        _vorschlag = null;
        VorschlagText = null;
    }

    /// <summary>
    /// Schreibt den ausgerechneten Betrag in Normalform zurueck, sobald
    /// das Feld den Fokus verliert ("12,50+3,20" wird zu "15,70",
    /// "12.5" zu "12,50"). Der Anwender sieht damit vor dem Speichern,
    /// was verstanden wurde. Ist die Eingabe nicht auswertbar, bleibt sie
    /// unangetastet stehen - die Pruefung beim Speichern erklaert warum.
    /// Gerechnet wird in Core (<see cref="BetragsAusdruck"/>, Regel 7).
    /// </summary>
    public void BetragNormalisieren()
    {
        if (BetragsAusdruck.Normalform(BetragText) is string normalform)
        {
            BetragText = normalform;
        }
    }

    /// <summary>
    /// Dasselbe fuer das Datumsfeld: "heute" wird zu "07.08.2026". Siehe
    /// <see cref="GermanDateInput.Normalize"/>.
    /// </summary>
    public void DatumNormalisieren()
    {
        if (GermanDateInput.Normalize(DatumText, DateOnly.FromDateTime(DateTime.Now)) is string normalform)
        {
            DatumText = normalform;
        }
    }

    /// <summary>
    /// Baut ZahlerOptionen aus _allePersonen neu auf: bei einer Einnahme
    /// faellt die Ich-Person heraus, eine Einnahme kommt immer von jemand
    /// anderem (siehe Feldkommentar bei _allePersonen). Eine bereits
    /// getroffene Auswahl bleibt erhalten, sofern sie weiterhin waehlbar
    /// ist; sonst faellt sie auf "ich" zurueck (Ausgabe) bzw. die erste
    /// verbleibende Person (Einnahme, "ich" ist dort schon herausgefiltert).
    ///
    /// Gibt es ausser der Ich-Person niemanden, laesst sich eine Einnahme
    /// gar nicht zuordnen - das Haekchen wird dann sofort zurueckgesetzt
    /// (loest diese Methode rekursiv nochmal aus) und ein Hinweistext
    /// erklaert warum. Der Hinweistext wird bewusst ERST NACH dem
    /// rekursiven Aufruf gesetzt, sonst raeumt der rekursive Durchlauf
    /// (der unten im regulaeren Zweig landet) ihn sofort wieder weg.
    /// </summary>
    private void AktualisiereZahlerAuswahl()
    {
        if (IstEinnahme && _allePersonen.All(p => p.IsSelf))
        {
            IstEinnahme = false;
            EinnahmeHinweisText = "Für eine Einnahme wird zunächst eine weitere Person benötigt (siehe Verwaltung › Personen).";
            return;
        }

        EinnahmeHinweisText = null;

        var ausgewaehlteId = AusgewaehlterZahler.Id;

        ZahlerOptionen.Clear();
        foreach (var person in _allePersonen.Where(p => !IstEinnahme || !p.IsSelf))
        {
            ZahlerOptionen.Add(person);
        }

        AusgewaehlterZahler = ZahlerOptionen.FirstOrDefault(p => p.Id == ausgewaehlteId)
            ?? ZahlerOptionen.FirstOrDefault(p => !IstEinnahme && p.IsSelf)
            ?? ZahlerOptionen.First();
    }

    /// <summary>Der Anwender bestaetigt das ungewoehnliche Datum.</summary>
    [RelayCommand]
    private void DatumBestaetigen()
    {
        _datumBestaetigt = true;
        DatumRueckfrageText = null;
    }

    [RelayCommand]
    private void DatumRueckfrageAbbrechen() => DatumRueckfrageText = null;

    [RelayCommand]
    private void SpeicherFehlerSchliessen() => SpeicherFehlerText = null;

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

    // Gibt eine Task zurueck, obwohl nichts mehr darin abgewartet wird:
    // das einzige Asynchrone war die Wartezeit des Bestaetigungsbands, und
    // die ist mit dem Toast weggefallen. Die Signatur bleibt, damit
    // SpeichernCommand ein AsyncRelayCommand bleibt - Bindung und
    // Aufrufstellen haengen daran.
    [RelayCommand]
    private Task Speichern()
    {
        // Die gesamte Pruefung liegt in Core (Regel 7) - hier werden die
        // Meldungen nur auf ihre Felder verteilt.
        var pruefung = ExpenseValidator.Validate(new ExpenseInput
        {
            AmountText = BetragText,
            IsIncome = IstEinnahme,
            CategoryId = AusgewaehlteKategorie?.Id,
            PayerId = AusgewaehlterZahler.Id,
            PayerIsSelf = AusgewaehlterZahler.IsSelf,
            DateText = DatumText,
            Today = DateOnly.FromDateTime(DateTime.Now),
        });

        BetragFehler = pruefung.AmountError;
        KategorieFehler = pruefung.CategoryError;
        ZahlerFehler = pruefung.PayerError;
        DatumFehler = pruefung.DateError;

        if (!pruefung.IsValid)
        {
            return Task.CompletedTask;
        }

        // Ungewoehnliches, aber moegliches Datum: einmal nachfragen und
        // erst beim naechsten Speichern durchlassen. Verboten wird nichts -
        // wer alte Belege nachtraegt, hat gute Gruende dafuer.
        if (pruefung.NeedsConfirmation && !_datumBestaetigt)
        {
            DatumRueckfrageText = pruefung.DateConfirmation;
            return Task.CompletedTask;
        }

        // Platte voll, Berechtigung entzogen, Laufwerk getrennt: dann
        // steht hier ein Text und darunter das unveraenderte Formular.
        // Geraeumt wird erst nach einem erfolgreichen Schreiben.
        SpeicherFehlerText = Schreibvorgang.Versuche(
            "Beim Speichern einer Ausgabe",
            () => _expenseRepository.Create(
                AusgewaehlteKategorie!.Id,
                pruefung.AmountCents,
                pruefung.Date,
                AusgewaehlterZahler.Id,
                note: string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung,
                isIncome: IstEinnahme));

        if (SpeicherFehlerText is not null)
        {
            return Task.CompletedTask;
        }

        _datumBestaetigt = false;

        // Datum und Zahler bleiben absichtlich stehen (naechste Ausgabe
        // ist haeufig am selben Tag vom selben Zahler). IstEinnahme wird
        // dagegen wie Betrag und Kategorie geraeumt: die naechste Buchung
        // ist im Regelfall wieder eine normale Ausgabe, und ein stehen
        // gebliebenes Haekchen wuerde sie sonst unbemerkt zur Einnahme
        // machen.
        //
        // Bei angehakter Serienerfassung gilt das Gegenteil: dann liegt
        // ein Stapel gleichartiger Belege auf dem Tisch, und geleert
        // werden nur Betrag und Bemerkung. Auch das Einnahme-Haekchen
        // bleibt dann stehen - wer eine Reihe Einnahmen erfasst, will es
        // nicht fuenfmal setzen. Unbemerkt ist es dabei nicht: das
        // Haekchen steht sichtbar im Formular, direkt ueber dem Feld.
        BetragText = string.Empty;
        Bemerkung = null;

        if (!WerteBehalten)
        {
            AusgewaehlteKategorie = null;
            IstEinnahme = false;
        }

        // Statt nur der eigenen Liste (LadeLetzteAusgaben) wird die
        // Nachricht gesendet - die eigene Registrierung oben ladet dadurch
        // auch neu, zusaetzlich aber jeder andere Bereich mit (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());
        FokusBetragAngefordert?.Invoke(this, EventArgs.Empty);

        _toast.Zeige("Gespeichert.");

        return Task.CompletedTask;
    }

    private void LadeLetzteAusgaben()
    {
        LetzteAusgaben.Clear();

        foreach (var expense in _expenseRepository.GetRecent(10))
        {
            LetzteAusgaben.Add(new LetzteAusgabeZeile(expense));
        }
    }
}
