using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    /// <summary>Wird ausgeloest, wenn "Ausgabe erfassen" gewaehlt wird - siehe MainViewModel.</summary>
    public event EventHandler? ErfassenAngefordert;

    /// <summary>Wird ausgeloest, wenn "Alle Buchungen ansehen" gewaehlt wird - siehe MainViewModel.</summary>
    public event EventHandler? AusgabenlisteAngefordert;

    [ObservableProperty] private string _monatUeberschrift = string.Empty;

    [ObservableProperty] private string _ausgabenMonatText = string.Empty;
    [ObservableProperty] private string _ausgabenMonatHinweis = string.Empty;

    [ObservableProperty] private string _einnahmenMonatText = string.Empty;
    [ObservableProperty] private string _einnahmenMonatHinweis = string.Empty;

    [ObservableProperty] private string _offenePostenText = string.Empty;
    [ObservableProperty] private string _offenePostenHinweis = string.Empty;

    [ObservableProperty] private string _naechsteFaelligkeitTitel = string.Empty;
    [ObservableProperty] private string _naechsteFaelligkeitHinweis = string.Empty;
    [ObservableProperty] private bool _naechsteFaelligkeitVorhanden;

    public ObservableCollection<LetzteAusgabeZeile> LetzteBuchungen { get; } = new();

    /// <summary>
    /// Die "einfache Linie" des Netto-Trends (UI/UX-Redesign, Abschnitt 4)
    /// als Folge von Liniensegmenten in einem festen, gedachten
    /// Koordinatenraum (0..600 × 0..160) - die Ansicht skaliert sie ueber
    /// ein Viewbox auf die tatsaechliche Kartenbreite, statt fest in
    /// Pixeln zu rechnen. Segmente statt einer Punktliste, damit die
    /// Ansicht ohne Points-Typkonvertierung direkt an Line.StartPoint/
    /// EndPoint binden kann.
    /// </summary>
    [ObservableProperty] private IReadOnlyList<TrendSegment> _trendLinien = Array.Empty<TrendSegment>();

    public ObservableCollection<string> TrendMonatsBeschriftungen { get; } = new();

    [RelayCommand]
    private void AusgabeErfassen() => ErfassenAngefordert?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AlleBuchungenAnsehen() => AusgabenlisteAngefordert?.Invoke(this, EventArgs.Empty);

    public StartseiteViewModel(
        ExpenseRepository expenseRepository,
        OpenItemsRepository openItemsRepository,
        RecurringExpenseRepository recurringExpenseRepository)
    {
        _expenseRepository = expenseRepository;
        _openItemsRepository = openItemsRepository;
        _recurringExpenseRepository = recurringExpenseRepository;

        Aktualisiere();
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
        AktualisiereTrend(heute, de);
        AktualisiereLetzteBuchungen();
    }

    private void AktualisiereMonatsKacheln(DateOnly heute)
    {
        var monatsAnfang = new DateOnly(heute.Year, heute.Month, 1);
        var naechsterMonat = monatsAnfang.AddMonths(1);

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
        var einnahmen = buchungen.Where(b => b.IsIncome).ToList();

        var ausgabenSumme = ausgaben.Sum(b => b.AmountCents);
        AusgabenMonatText = EuroText.Format(ausgabenSumme);
        AusgabenMonatHinweis = ausgaben.Count == 0
            ? "Noch keine Ausgabe diesen Monat"
            : $"{ausgaben.Count} {(ausgaben.Count == 1 ? "Buchung" : "Buchungen")} · Ø {EuroText.Format(ausgabenSumme / ausgaben.Count)}";

        var einnahmenSumme = einnahmen.Sum(b => b.AmountCents);
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
            NaechsteFaelligkeitVorhanden = false;
            NaechsteFaelligkeitTitel = "Keine aktive Vorlage";
            NaechsteFaelligkeitHinweis = string.Empty;
            return;
        }

        NaechsteFaelligkeitVorhanden = true;
        NaechsteFaelligkeitTitel = naechste.Vorlage.Title;
        NaechsteFaelligkeitHinweis =
            $"{EuroText.FormatSigned(naechste.Vorlage.AmountCents, naechste.Vorlage.IsIncome)} · fällig {GermanDateInput.ToText(naechste.Naechste!.Value)}";
    }

    private void AktualisiereTrend(DateOnly heute, CultureInfo de)
    {
        const int monatsAnzahl = 6;
        var monatsWerte = new List<long>(monatsAnzahl);

        TrendMonatsBeschriftungen.Clear();

        var ersterMonat = new DateOnly(heute.Year, heute.Month, 1).AddMonths(-(monatsAnzahl - 1));
        for (var i = 0; i < monatsAnzahl; i++)
        {
            var monatsAnfang = ersterMonat.AddMonths(i);
            var naechsterMonat = monatsAnfang.AddMonths(1);

            var buchungen = _expenseRepository.Query(
                new ReportFilter
                {
                    From = monatsAnfang,
                    To = naechsterMonat,
                    PayerScope = PayerScope.SelfAndOpen,
                },
                ExpenseSortColumn.Datum,
                ascending: true);

            var netto = buchungen.Sum(b => b.IsIncome ? b.AmountCents : -b.AmountCents);
            monatsWerte.Add(netto);

            var beschriftung = monatsAnfang.ToDateTime(TimeOnly.MinValue).ToString("MMM", de);
            TrendMonatsBeschriftungen.Add(i == monatsAnzahl - 1 && monatsAnfang.Month == heute.Month
                ? beschriftung + "*"
                : beschriftung);
        }

        var punkte = BerechneTrendPunkte(monatsWerte);
        var segmente = new List<TrendSegment>(Math.Max(0, punkte.Count - 1));
        for (var i = 1; i < punkte.Count; i++)
        {
            segmente.Add(new TrendSegment(punkte[i - 1], punkte[i]));
        }

        TrendLinien = segmente;
    }

    // Fester gedachter Koordinatenraum (siehe Feldkommentar TrendLinien).
    private const double TrendBreite = 600, TrendHoehe = 160, TrendRand = 12;

    private static IReadOnlyList<Point> BerechneTrendPunkte(IReadOnlyList<long> werte)
    {
        if (werte.Count < 2)
        {
            return Array.Empty<Point>();
        }

        var min = werte.Min();
        var max = werte.Max();
        var spanne = max - min;
        if (spanne == 0)
        {
            spanne = 1;
        }

        var schrittX = (TrendBreite - 2 * TrendRand) / (werte.Count - 1);

        var punkte = new Point[werte.Count];
        for (var i = 0; i < werte.Count; i++)
        {
            var x = TrendRand + i * schrittX;
            var anteil = (werte[i] - min) / (double)spanne;
            var y = (TrendHoehe - TrendRand) - anteil * (TrendHoehe - 2 * TrendRand);
            punkte[i] = new Point(x, y);
        }

        return punkte;
    }

    private void AktualisiereLetzteBuchungen()
    {
        LetzteBuchungen.Clear();
        foreach (var buchung in _expenseRepository.GetRecent(5))
        {
            LetzteBuchungen.Add(new LetzteAusgabeZeile(buchung));
        }
    }
}

/// <summary>Ein Liniensegment des Netto-Trends - siehe StartseiteViewModel.TrendLinien.</summary>
public sealed record TrendSegment(Point Von, Point Bis);
