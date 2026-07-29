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

    public event EventHandler? FokusBetragAngefordert;

    [ObservableProperty]
    private string _betragText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BetragFehlerSichtbar))]
    private string? _betragFehler;

    public bool BetragFehlerSichtbar => !string.IsNullOrEmpty(BetragFehler);

    public IReadOnlyList<CategoryOption> KategorieVorschlaege { get; }

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

    public IReadOnlyList<Person> ZahlerOptionen { get; }

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

        KategorieVorschlaege = categoryRepository.GetSelectableLeaves();

        ZahlerOptionen = personRepository.GetAllActive();
        _ausgewaehlterZahler = ZahlerOptionen.First(p => p.IsSelf);

        _datumText = GermanDateInput.ToText(DateOnly.FromDateTime(DateTime.Now));

        LadeLetzteAusgaben();
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
