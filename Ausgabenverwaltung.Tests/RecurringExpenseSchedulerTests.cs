using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;

namespace Ausgabenverwaltung.Tests;

public class RecurringExpenseSchedulerTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly RecurringExpenseRepository _repository;
    private readonly int _categoryId;
    private readonly int _payerId;

    public RecurringExpenseSchedulerTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new RecurringExpenseRepository(_connection);

        _categoryId = new CategoryRepository(_connection).Create("Abos", null).Id;
        _payerId = new PersonRepository(_connection).Create("Ich", isSelf: true).Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void RunIfDue_laeuft_am_Tag_des_Programmstarts_nicht_noch_einmal()
    {
        // Der Startlauf hat heute bereits stattgefunden. Ohne diese Sperre
        // wuerde jeder Bereichswechsel eine Transaktion ausloesen.
        var heute = new DateOnly(2026, 7, 30);
        _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        var scheduler = new RecurringExpenseScheduler(_repository, startupRunDate: heute);

        Assert.Empty(scheduler.RunIfDue(heute));
    }

    [Fact]
    public void RunIfDue_laeuft_am_naechsten_Tag_wieder()
    {
        // Der eigentliche Zweck: bleibt die Anwendung ueber Mitternacht
        // offen, muessen faellige Buchungen trotzdem entstehen.
        var start = new DateOnly(2026, 7, 30);
        _repository.Create(
            _categoryId, _payerId, 1000, "Taeglich", "day", 1, null,
            new DateOnly(2026, 7, 30), null);

        // Der Lauf des Programmstarts, den der Scheduler als "heute schon
        // gelaufen" voraussetzt.
        _repository.GenerateDueOccurrences(start);

        var scheduler = new RecurringExpenseScheduler(_repository, startupRunDate: start);
        Assert.Empty(scheduler.RunIfDue(start));

        var erzeugt = scheduler.RunIfDue(start.AddDays(1));

        Assert.Single(erzeugt);
        Assert.Equal(start.AddDays(1), erzeugt[0].ExpenseDate);
    }

    [Fact]
    public void RunIfDue_laeuft_pro_Kalendertag_nur_einmal()
    {
        var start = new DateOnly(2026, 7, 30);
        _repository.Create(
            _categoryId, _payerId, 1000, "Taeglich", "day", 1, null,
            new DateOnly(2026, 7, 30), null);

        _repository.GenerateDueOccurrences(start);

        var scheduler = new RecurringExpenseScheduler(_repository, startupRunDate: start);
        var morgen = start.AddDays(1);

        Assert.Single(scheduler.RunIfDue(morgen));
        Assert.Empty(scheduler.RunIfDue(morgen));
    }

    [Fact]
    public void RunNow_ignoriert_die_Tagesgrenze()
    {
        // "Jetzt erzeugen" soll auch am Tag des Programmstarts etwas tun -
        // etwa direkt nach dem Anlegen einer Vorlage.
        var heute = new DateOnly(2026, 7, 30);
        var scheduler = new RecurringExpenseScheduler(_repository, startupRunDate: heute);

        _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 6, 1), null);

        var erzeugt = scheduler.RunNow(heute);

        Assert.Equal(2, erzeugt.Count);
    }

    [Fact]
    public void RunNow_erzeugt_beim_zweiten_Druck_nichts_mehr()
    {
        // Dafuer sorgt GeneratedThrough, nicht der Scheduler.
        var heute = new DateOnly(2026, 7, 30);
        var scheduler = new RecurringExpenseScheduler(_repository, startupRunDate: heute);

        _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 6, 1), null);

        Assert.Equal(2, scheduler.RunNow(heute).Count);
        Assert.Empty(scheduler.RunNow(heute));
    }
}
