using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
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
    private readonly ExpenseRepository _expenseRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PersonRepository _personRepository;
    private readonly IMessenger _messenger;

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

    [ObservableProperty]
    private bool _bestaetigungSichtbar;

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

    public ErfassenViewModel(
        ExpenseRepository expenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        IMessenger messenger)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _messenger = messenger;

        AktualisiereKategorieVorschlaege();

        _allePersonen.AddRange(_personRepository.GetAllActive());
        _ausgewaehlterZahler = _allePersonen.First(p => p.IsSelf);
        AktualisiereZahlerAuswahl();

        _datumText = GermanDateInput.ToText(DateOnly.FromDateTime(DateTime.Now));

        LadeLetzteAusgaben();

        // Buchungsaenderungen aus anderen Bereichen (Abhaken in "Offene
        // Posten", Bearbeiten/Loeschen in der Ausgabenliste, Zusammenfuehren
        // von Kategorien) sollen hier sofort sichtbar werden, nicht erst
        // beim naechsten Navigieren zu "Erfassen" (Regel 14).
        _messenger.Register<ErfassenViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.LadeLetzteAusgaben());
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

    [RelayCommand]
    private async Task Speichern()
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
            return;
        }

        // Ungewoehnliches, aber moegliches Datum: einmal nachfragen und
        // erst beim naechsten Speichern durchlassen. Verboten wird nichts -
        // wer alte Belege nachtraegt, hat gute Gruende dafuer.
        if (pruefung.NeedsConfirmation && !_datumBestaetigt)
        {
            DatumRueckfrageText = pruefung.DateConfirmation;
            return;
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
            return;
        }

        _datumBestaetigt = false;

        // Datum und Zahler bleiben absichtlich stehen (naechste Ausgabe
        // ist haeufig am selben Tag vom selben Zahler). IstEinnahme wird
        // dagegen wie Betrag und Kategorie geraeumt: die naechste Buchung
        // ist im Regelfall wieder eine normale Ausgabe, und ein stehen
        // gebliebenes Haekchen wuerde sie sonst unbemerkt zur Einnahme
        // machen.
        BetragText = string.Empty;
        AusgewaehlteKategorie = null;
        Bemerkung = null;
        IstEinnahme = false;

        // Statt nur der eigenen Liste (LadeLetzteAusgaben) wird die
        // Nachricht gesendet - die eigene Registrierung oben ladet dadurch
        // auch neu, zusaetzlich aber jeder andere Bereich mit (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());
        FokusBetragAngefordert?.Invoke(this, EventArgs.Empty);

        BestaetigungSichtbar = true;
        await Task.Delay(TimeSpan.FromSeconds(2));
        BestaetigungSichtbar = false;
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
