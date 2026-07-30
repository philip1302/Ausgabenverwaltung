using Ausgabenverwaltung.Core.Entities;

namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Stoesst die Erzeugung faelliger Buchungen an, aber hoechstens einmal pro
/// Kalendertag.
///
/// Noetig, weil die Erzeugung sonst allein am Programmstart haengt
/// (Startup.StartupService): bleibt die Anwendung tagelang offen, geht der
/// Monatserste vorbei, ohne dass die Stallmiete entsteht. Die Oberflaeche
/// ruft deshalb bei jedem Bereichswechsel <see cref="RunIfDue"/> auf - der
/// Zaehler hier sorgt dafuer, dass daraus trotzdem nur ein Lauf pro Tag
/// wird und nicht bei jedem Klick eine Datenbanktransaktion.
///
/// Bewusst kein Zeitgeber im Hintergrund: die Erzeugung liefe sonst
/// mitten in eine laufende Eingabe hinein, auf derselben einen
/// SQLite-Verbindung.
/// </summary>
public sealed class RecurringExpenseScheduler
{
    private readonly RecurringExpenseRepository _repository;

    private DateOnly _lastRunDate;

    /// <param name="startupRunDate">
    /// Der Tag, an dem der Lauf beim Programmstart bereits stattgefunden
    /// hat. Er zaehlt als der heutige Lauf, sonst wuerde der erste
    /// Bereichswechsel sofort ein zweites Mal erzeugen.
    /// </param>
    public RecurringExpenseScheduler(RecurringExpenseRepository repository, DateOnly startupRunDate)
    {
        _repository = repository;
        _lastRunDate = startupRunDate;
    }

    /// <summary>
    /// Erzeugt die faelligen Buchungen, falls heute noch nicht gelaufen
    /// wurde. Sonst eine leere Liste, ohne Datenbankzugriff.
    /// </summary>
    public IReadOnlyList<Expense> RunIfDue(DateOnly today)
    {
        if (today <= _lastRunDate)
        {
            return Array.Empty<Expense>();
        }

        return RunNow(today);
    }

    /// <summary>
    /// Erzeugt die faelligen Buchungen unabhaengig davon, ob heute schon
    /// gelaufen wurde - fuer die Schaltflaeche "Jetzt erzeugen". Ein
    /// zweiter Aufruf erzeugt trotzdem nichts mehr, dafuer sorgt
    /// GeneratedThrough.
    /// </summary>
    public IReadOnlyList<Expense> RunNow(DateOnly today)
    {
        var created = _repository.GenerateDueOccurrences(today);

        // Erst nach dem erfolgreichen Lauf fortschreiben: wirft die
        // Erzeugung, soll der naechste Versuch es erneut probieren duerfen.
        if (today > _lastRunDate)
        {
            _lastRunDate = today;
        }

        return created;
    }
}
