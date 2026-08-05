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

    public event EventHandler? FokusBetragAngefordert;

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

    // ObservableCollection statt einmalig geladener Liste, damit in der
    // Verwaltung archivierte Personen aus der Auswahl verschwinden, ohne
    // die Anwendung neu zu starten (siehe AktualisiereZahlerOptionen,
    // aufgerufen bei Navigation zu diesem Bereich - analog zu
    // KategorieVorschlaege).
    public ObservableCollection<Person> ZahlerOptionen { get; } = new();

    [ObservableProperty]
    private Person _ausgewaehlterZahler;

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
        PersonRepository personRepository)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;

        AktualisiereKategorieVorschlaege();

        foreach (var person in _personRepository.GetAllActive())
        {
            ZahlerOptionen.Add(person);
        }
        _ausgewaehlterZahler = ZahlerOptionen.First(p => p.IsSelf);

        _datumText = GermanDateInput.ToText(DateOnly.FromDateTime(DateTime.Now));

        LadeLetzteAusgaben();
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
    /// die IsSelf-Person zurueck, da diese nie archiviert werden kann.
    /// </summary>
    public void AktualisiereZahlerOptionen()
    {
        var ausgewaehlteId = AusgewaehlterZahler.Id;

        ZahlerOptionen.Clear();
        foreach (var person in _personRepository.GetAllActive())
        {
            ZahlerOptionen.Add(person);
        }

        AusgewaehlterZahler = ZahlerOptionen.FirstOrDefault(p => p.Id == ausgewaehlteId)
            ?? ZahlerOptionen.First(p => p.IsSelf);
    }

    // Eine Aenderung am Datum nimmt eine erteilte Bestaetigung zurueck:
    // sie galt fuer das Datum, das dabei stand, nicht fuer jedes weitere.
    partial void OnDatumTextChanged(string value)
    {
        _datumBestaetigt = false;
        DatumRueckfrageText = null;
    }

    // Beim Ankreuzen auf die Ich-Person vorbelegen, sofern gerade ein
    // fremder Zahler gewaehlt ist - das Zahler-Feld bleibt trotzdem
    // bestehen und aenderbar, eine Einnahme ist nicht zwingend an "ich"
    // gebunden.
    partial void OnIstEinnahmeChanged(bool value)
    {
        if (value && !AusgewaehlterZahler.IsSelf)
        {
            AusgewaehlterZahler = ZahlerOptionen.First(p => p.IsSelf);
        }
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
            DateText = DatumText,
            Today = DateOnly.FromDateTime(DateTime.Now),
        });

        BetragFehler = pruefung.AmountError;
        KategorieFehler = pruefung.CategoryError;
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

        LadeLetzteAusgaben();
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
