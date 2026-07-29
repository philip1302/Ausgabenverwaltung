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

    public ObservableCollection<string> LetzteAusgaben { get; } = new();

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

    [RelayCommand]
    private async Task Speichern()
    {
        var betragGueltig = Money.TryParseEuroText(BetragText, out var amountCents);
        BetragFehler = betragGueltig ? null : "Ungueltiger Betrag (Beispiel: 12,50).";

        KategorieFehler = AusgewaehlteKategorie is null ? "Bitte eine Kategorie waehlen." : null;

        var datumGueltig = GermanDateInput.TryParse(DatumText, out var expenseDate);
        DatumFehler = datumGueltig ? null : "Ungueltiges Datum (TT.MM.JJJJ).";

        if (!betragGueltig || AusgewaehlteKategorie is null || !datumGueltig)
        {
            return;
        }

        _expenseRepository.Create(
            AusgewaehlteKategorie.Id,
            amountCents,
            expenseDate,
            AusgewaehlterZahler.Id,
            note: string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung);

        // Datum und Zahler bleiben absichtlich stehen (naechste Ausgabe
        // ist haeufig am selben Tag vom selben Zahler).
        BetragText = string.Empty;
        AusgewaehlteKategorie = null;
        Bemerkung = null;

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
            var betrag = Money.ToDecimal(expense.AmountCents).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
            var zeile = $"{GermanDateInput.ToText(expense.ExpenseDate)}  ·  {betrag} EUR  ·  {expense.CategoryName}  ·  {expense.PayerName}";
            if (!string.IsNullOrWhiteSpace(expense.Note))
            {
                zeile += $"  ·  {expense.Note}";
            }

            LetzteAusgaben.Add(zeile);
        }
    }
}
