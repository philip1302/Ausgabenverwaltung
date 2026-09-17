using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Startup;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die beiden Meldungen, die der Start selbst zu berichten hat:
/// wiederkehrende Buchungen, die gerade automatisch erzeugt wurden, und
/// eine misslungene Sicherung.
///
/// Beide waren frueher je ein eigenes, dauerhaft angedocktes Band. Seit
/// dem Umbau der Hinweisbaender stellen sie ihre Meldung in die
/// gemeinsame Bandzone (<see cref="BaenderViewModel"/>), in der
/// hoechstens eine zugleich sichtbar ist; die Texte kommen aus Core
/// (<see cref="Bandtexte"/>) und nicht mehr aus diesem ViewModel
/// (Regel 7 und 12).
///
/// Die Erzeugung laeuft nicht nur beim Programmstart, sondern - weil der
/// Start sonst darauf warten muesste - auch bei den Laeufen waehrend der
/// Sitzung (siehe <see cref="MainViewModel"/> und
/// Core.RecurringExpenses.RecurringExpenseScheduler).
/// </summary>
public sealed class StartupNoticeViewModel : ViewModelBase
{
    private const string BandSchluesselBuchungen = "erzeugte-buchungen";
    private const string BandSchluesselSicherung = "sicherung";

    private readonly BaenderViewModel _baender;

    /// <summary>
    /// Der Weg zur Datensicherung, wenn die Sicherung beim Start
    /// misslungen ist. Der Text nennt "Verwaltung › Datensicherung" - ein
    /// Knopf, der auch dorthin fuehrt, erspart das Suchen. Dieses
    /// ViewModel kennt die Navigation nicht, es meldet nur an; verdrahtet
    /// wird in <see cref="MainViewModel"/>, wie bei allen anderen
    /// Spruengen auch.
    /// </summary>
    public event EventHandler? SicherungAngefordert;

    public StartupNoticeViewModel(StartupResult startupResult, BaenderViewModel baender)
    {
        _baender = baender;

        Zeige(startupResult.GeneratedExpenses);

        if (startupResult.Backup is { NeedsAttention: true } backup)
        {
            _baender.Zeige(new Bandeintrag
            {
                Schluessel = BandSchluesselSicherung,
                Meldung = Bandtexte.Sicherungsfehler(backup.PrimaryProblem),
                AktionText = "Zur Datensicherung",
                AktionTipp = "Öffnet „Verwaltung › Datensicherung“, wo sich ein "
                    + "neuer Versuch starten lässt.",
                Aktion = () => SicherungAngefordert?.Invoke(this, EventArgs.Empty),
            });
        }
    }

    /// <summary>
    /// Zeigt einen Erzeugungslauf an. Bei null Buchungen bleibt es still -
    /// ein "es war nichts faellig" beim Bereichswechsel waere nur Rauschen.
    /// </summary>
    public void Zeige(IReadOnlyList<Expense> erzeugte)
    {
        if (erzeugte.Count == 0)
        {
            return;
        }

        var beschreibungen = erzeugte
            .Select(expense =>
                $"{IsoDate.ToDateText(expense.ExpenseDate)} - " +
                $"{EuroText.Format(expense.AmountCents, expense.IsIncome)}" +
                (string.IsNullOrEmpty(expense.Note) ? string.Empty : $" ({expense.Note})"))
            .ToList();

        _baender.Zeige(new Bandeintrag
        {
            Schluessel = BandSchluesselBuchungen,
            Meldung = Bandtexte.ErzeugteBuchungen(beschreibungen),
        });
    }
}
