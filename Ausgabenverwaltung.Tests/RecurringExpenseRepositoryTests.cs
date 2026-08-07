using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Dapper;

namespace Ausgabenverwaltung.Tests;

public class RecurringExpenseRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly RecurringExpenseRepository _repository;
    private readonly ExpenseRepository _expenseRepository;
    private readonly int _categoryId;
    private readonly int _payerId;

    public RecurringExpenseRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new RecurringExpenseRepository(_connection);
        _expenseRepository = new ExpenseRepository(_connection);

        var categories = new CategoryRepository(_connection);
        var people = new PersonRepository(_connection);

        _categoryId = categories.Create("Abos", null).Id;
        _payerId = people.Create("Ich", isSelf: true).Id;
    }

    public void Dispose() => _connection.Dispose();

    // ---------------------------------------------------------------
    // CRUD
    // ---------------------------------------------------------------

    [Fact]
    public void Create_legt_Vorlage_mit_allen_Feldern_an()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 5000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "Fixkosten");

        Assert.True(template.Id > 0);
        Assert.Equal(5000, template.AmountCents);
        Assert.Equal("Miete", template.Title);
        Assert.Equal("month", template.IntervalUnit);
        Assert.Equal(1, template.IntervalCount);
        Assert.Equal(1, template.AnchorDay);
        Assert.Equal(new DateOnly(2026, 1, 1), template.StartDate);
        Assert.Null(template.EndDate);
        Assert.Null(template.GeneratedThrough);
        Assert.True(template.IsActive);
        Assert.Equal("Fixkosten", template.Note);
    }

    [Fact]
    public void GetById_liefert_null_fuer_unbekannte_Id()
    {
        Assert.Null(_repository.GetById(99999));
    }

    [Fact]
    public void GetById_laedt_die_gespeicherte_Vorlage_unveraendert()
    {
        var created = _repository.Create(
            _categoryId, _payerId, 1234, "Zeitung", "week", 1, null,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 12, 31));

        var loaded = _repository.GetById(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal(created.Title, loaded!.Title);
        Assert.Equal(created.AmountCents, loaded.AmountCents);
        Assert.Equal(created.IntervalUnit, loaded.IntervalUnit);
        Assert.Equal(created.StartDate, loaded.StartDate);
        Assert.Equal(created.EndDate, loaded.EndDate);
    }

    [Fact]
    public void Update_aendert_die_Felder_der_Vorlage()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Alt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.Update(
            template.Id, _categoryId, _payerId, 2000, "Neu", "year", 2, 15,
            new DateOnly(2026, 2, 1), new DateOnly(2027, 2, 1), note: "geaendert");

        var loaded = _repository.GetById(template.Id)!;
        Assert.Equal(2000, loaded.AmountCents);
        Assert.Equal("Neu", loaded.Title);
        Assert.Equal("year", loaded.IntervalUnit);
        Assert.Equal(2, loaded.IntervalCount);
        Assert.Equal(15, loaded.AnchorDay);
        Assert.Equal(new DateOnly(2026, 2, 1), loaded.StartDate);
        Assert.Equal(new DateOnly(2027, 2, 1), loaded.EndDate);
        Assert.Equal("geaendert", loaded.Note);
    }

    [Fact]
    public void Deactivate_entfernt_die_Vorlage_aus_GetAllActive_ohne_sie_zu_loeschen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Wird deaktiviert", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.Deactivate(template.Id);

        Assert.DoesNotContain(_repository.GetAllActive(), t => t.Id == template.Id);
        var loaded = _repository.GetById(template.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded!.IsActive);
    }

    [Fact]
    public void Create_ohne_IsIncome_ist_eine_gewoehnliche_Vorlage()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        Assert.False(template.IsIncome);
        Assert.False(_repository.GetById(template.Id)!.IsIncome);
    }

    [Fact]
    public void Create_und_Update_speichern_IsIncome()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 300000, "Gehalt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, isIncome: true);

        Assert.True(template.IsIncome);
        Assert.True(_repository.GetById(template.Id)!.IsIncome);

        _repository.Update(
            template.Id, _categoryId, _payerId, 300000, "Gehalt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: null, isIncome: false);

        Assert.False(_repository.GetById(template.Id)!.IsIncome);
    }

    [Fact]
    public void GetAllActive_liefert_nur_aktive_Vorlagen_alphabetisch_sortiert()
    {
        _repository.Create(_categoryId, _payerId, 1000, "Bernd-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Create(_categoryId, _payerId, 1000, "Anna-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var inaktiv = _repository.Create(_categoryId, _payerId, 1000, "Zzz-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Deactivate(inaktiv.Id);

        var titel = _repository.GetAllActive().Select(t => t.Title).ToList();

        Assert.Equal(new[] { "Anna-Abo", "Bernd-Abo" }, titel);
    }

    // ---------------------------------------------------------------
    // Erzeugung
    // ---------------------------------------------------------------

    [Fact]
    public void GenerateDueOccurrences_erzeugt_faellige_Buchungen_mit_kopierten_Vorlagenwerten()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1200, "Streamingdienst", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "Familientarif");

        var created = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        Assert.Equal(3, created.Count); // Januar, Februar, Maerz
        Assert.Equal(1200, created[0].AmountCents);
        Assert.Equal("Familientarif", created[0].Note);
        Assert.Equal(template.Id, created[0].RecurringExpenseId);
        var buchungen = GetGeneratedExpenses(template.Id);
        Assert.Equal(
            new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1) },
            buchungen.Select(b => b.ExpenseDate));

        var erste = _expenseRepository.GetById(buchungen[0].Id)!;
        Assert.Equal(1200, erste.AmountCents);
        Assert.Equal("Familientarif", erste.Note);
        Assert.Equal(_payerId, erste.PayerId);
        Assert.Equal(template.Id, erste.RecurringExpenseId);
    }

    [Fact]
    public void GenerateDueOccurrences_kopiert_IsIncome_aus_der_Vorlage_Regel_6()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 300000, "Gehalt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, isIncome: true);

        var created = _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1));

        var erzeugteBuchung = Assert.Single(created);
        Assert.True(erzeugteBuchung.IsIncome);
        Assert.True(_expenseRepository.GetById(erzeugteBuchung.Id)!.IsIncome);

        // Regel 6: eine spaetere Aenderung an der Vorlage darf die bereits
        // erzeugte Buchung nicht rueckwirkend veraendern.
        _repository.Update(
            template.Id, _categoryId, _payerId, 300000, "Gehalt", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: null, isIncome: false);

        Assert.True(_expenseRepository.GetById(erzeugteBuchung.Id)!.IsIncome);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_bei_zweitem_Aufruf_am_selben_Tag_nichts_doppelt()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1500, "Fitnessstudio", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        var ersterLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 15));
        var zweiterLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 15));

        Assert.Equal(3, ersterLauf.Count);
        Assert.Empty(zweiterLauf);
        Assert.Equal(3, GetGeneratedExpenses(template.Id).Count);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_nach_Ablauf_des_EndDate_keine_weiteren_Buchungen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 800, "Zeitschriftenabo", "month", 1, 1,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1));

        var vollstaendig = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(3, vollstaendig.Count);

        // EndDate liegt jetzt in der Vergangenheit - ein spaeterer Lauf
        // (das Abo ist laengst ausgelaufen) darf nichts mehr erzeugen.
        var weitererLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 12, 1));

        Assert.Empty(weitererLauf);
        Assert.Equal(3, GetGeneratedExpenses(template.Id).Count);
    }

    [Fact]
    public void GenerateDueOccurrences_erzeugt_eine_geloeschte_Buchung_nicht_erneut()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 3000, "Vereinsbeitrag", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1)); // Jan, Feb, Maerz

        var februarBuchung = GetGeneratedExpenses(template.Id).Single(e => e.ExpenseDate == new DateOnly(2026, 2, 1));
        _expenseRepository.Delete(februarBuchung.Id);

        // Weiterer Lauf mit spaeterem Cutoff - Februar darf nicht
        // zurueckkommen, GeneratedThrough steht bereits dahinter.
        var weitererLauf = _repository.GenerateDueOccurrences(new DateOnly(2026, 5, 1));

        Assert.Equal(2, weitererLauf.Count); // nur April, Mai
        var verbleibendeDaten = GetGeneratedExpenses(template.Id).Select(e => e.ExpenseDate).ToList();
        Assert.DoesNotContain(new DateOnly(2026, 2, 1), verbleibendeDaten);
    }

    [Fact]
    public void GenerateDueOccurrences_laeuft_in_einer_Transaktion_und_rollt_bei_Fehler_alles_zurueck()
    {
        var vorlageA = _repository.Create(_categoryId, _payerId, 1000, "A - Erste Vorlage", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);
        var vorlageB = _repository.Create(_categoryId, _payerId, 2000, "B - Zweite Vorlage", "month", 1, 1,
            new DateOnly(2026, 1, 1), null);

        // Sabotage: fuer Vorlage B existiert das erste faellige Vorkommen
        // bereits als Buchung, ohne dass GeneratedThrough davon weiss.
        // Der Generator versucht es trotzdem anzulegen und verletzt damit
        // UX_Expense_Occurrence.
        InsertRawExpense(vorlageB.Id, new DateOnly(2026, 1, 1));

        Assert.ThrowsAny<Exception>(() => _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1)));

        // Vorlage A wurde alphabetisch zuerst verarbeitet und haette ohne
        // Transaktion bereits eine Buchung erhalten - das muss
        // zurueckgerollt worden sein.
        Assert.Empty(GetGeneratedExpenses(vorlageA.Id));
        Assert.Null(_repository.GetById(vorlageA.Id)!.GeneratedThrough);
    }

    [Fact]
    public void Update_der_Vorlage_veraendert_bereits_erzeugte_Buchungen_nicht_rueckwirkend()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Internet", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "alt");

        _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1));

        _repository.Update(
            template.Id, _categoryId, _payerId, 5000, "Internet", "month", 1, 1,
            new DateOnly(2026, 1, 1), null, note: "neu, teurer Tarif");

        var januarBuchung = GetGeneratedExpenses(template.Id).Single();
        var geladeneBuchung = _expenseRepository.GetById(januarBuchung.Id)!;

        Assert.Equal(1000, geladeneBuchung.AmountCents);
        Assert.Equal("alt", geladeneBuchung.Note);
    }

    // ---------------------------------------------------------------
    // Verwaltung: alle laden, aktivieren, loeschen, zaehlen
    // ---------------------------------------------------------------

    [Fact]
    public void GetAll_liefert_auch_inaktive_Vorlagen_aktive_zuerst()
    {
        _repository.Create(_categoryId, _payerId, 1000, "Bernd-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var inaktiv = _repository.Create(_categoryId, _payerId, 1000, "Anna-Abo", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Deactivate(inaktiv.Id);

        var titel = _repository.GetAll().Select(t => t.Title).ToList();

        // Aktive zuerst, deshalb steht Bernd trotz des Alphabets vorn.
        Assert.Equal(new[] { "Bernd-Abo", "Anna-Abo" }, titel);
    }

    [Fact]
    public void Activate_macht_eine_deaktivierte_Vorlage_wieder_aktiv()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.Deactivate(template.Id);
        Assert.False(_repository.GetById(template.Id)!.IsActive);

        _repository.Activate(template.Id);

        Assert.True(_repository.GetById(template.Id)!.IsActive);
    }

    [Fact]
    public void SetGeneratedThrough_unterdrueckt_den_Rueckstand_ohne_etwas_zu_erzeugen()
    {
        // Der Fall "Vorlage nach langer Pause reaktivieren, aber erst ab
        // heute weiterlaufen".
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.SetGeneratedThrough(template.Id, new DateOnly(2026, 7, 30));

        Assert.Empty(GetGeneratedExpenses(template.Id));
        Assert.Equal(new DateOnly(2026, 7, 30), _repository.GetById(template.Id)!.GeneratedThrough);

        // Der naechste Lauf holt jetzt nichts mehr nach.
        Assert.Empty(_repository.GenerateDueOccurrences(new DateOnly(2026, 7, 30)));
    }

    [Fact]
    public void Eine_aus_einer_Buchung_erzeugte_Vorlage_legt_kein_Duplikat_an()
    {
        // Der Fall "das kommt jeden Monat": aus einer bestehenden Buchung
        // wird eine Vorlage mit demselben Startdatum. Sie darf genau diese
        // Buchung nicht ein zweites Mal anlegen - dafuer wandert
        // GeneratedThrough beim Anlegen auf das Buchungsdatum (siehe
        // VorlagenViewModel.NeueVorlageAus).
        var buchungsdatum = new DateOnly(2026, 5, 15);
        _expenseRepository.Create(_categoryId, 5000, buchungsdatum, _payerId, note: "Miete");

        var template = _repository.Create(
            _categoryId, _payerId, 5000, "Miete", "month", 1, buchungsdatum.Day,
            buchungsdatum, null, note: "Miete");
        _repository.SetGeneratedThrough(template.Id, buchungsdatum);

        // Derselbe Tag: es gibt nichts nachzuholen.
        Assert.Empty(_repository.GenerateDueOccurrences(template.Id, buchungsdatum));
        Assert.Empty(GetGeneratedExpenses(template.Id));

        // Einen Monat spaeter entsteht das naechste Vorkommen - und nur
        // dieses eine.
        var naechste = _repository.GenerateDueOccurrences(template.Id, new DateOnly(2026, 6, 15));

        Assert.Equal(new DateOnly(2026, 6, 15), Assert.Single(naechste).ExpenseDate);
    }

    [Fact]
    public void Delete_entfernt_die_Vorlage_und_loest_nur_die_Zuordnung_der_Buchungen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(3, erzeugt.Count);

        _repository.Delete(template.Id);

        Assert.Null(_repository.GetById(template.Id));

        // ON DELETE SET NULL: die Buchungen bleiben, gelten aber danach als
        // handerfasst.
        foreach (var buchung in erzeugt)
        {
            var geladen = _expenseRepository.GetById(buchung.Id);
            Assert.NotNull(geladen);
            Assert.Null(geladen!.RecurringExpenseId);
        }
    }

    [Fact]
    public void DeleteWithExpenses_laeuft_auf_einer_Verbindung_mit_Fremdschluesselpruefung()
    {
        // Regel 2. Ohne sie greift ON DELETE SET NULL nicht, und die
        // Reihenfolge in DeleteWithExpenses (erst Buchungen, dann Vorlage)
        // waere Zierde statt Notwendigkeit.
        Assert.Equal(1, _connection.ExecuteScalar<long>("PRAGMA foreign_keys"));
    }

    [Fact]
    public void DeleteWithExpenses_loescht_die_Vorlage_und_genau_deren_Buchungen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var andereVorlage = _repository.Create(
            _categoryId, _payerId, 500, "Zeitung", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(6, erzeugt.Count);

        var handerfasst = _expenseRepository.Create(_categoryId, 700, new DateOnly(2026, 2, 2), _payerId);

        _repository.DeleteWithExpenses(template.Id);

        Assert.Null(_repository.GetById(template.Id));
        Assert.Empty(GetGeneratedExpenses(template.Id));

        // Die Buchungen der anderen Vorlage bleiben unangetastet, ...
        Assert.Equal(3, GetGeneratedExpenses(andereVorlage.Id).Count);
        Assert.NotNull(_repository.GetById(andereVorlage.Id));

        // ... und die handerfasste erst recht: sie hat mit der Vorlage nie
        // etwas zu tun gehabt.
        Assert.NotNull(_expenseRepository.GetById(handerfasst.Id));
    }

    [Fact]
    public void DeleteWithExpenses_rollt_bei_einem_Fehler_im_zweiten_Schritt_alles_zurueck()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        Assert.Equal(3, erzeugt.Count);

        // Kuenstlich verletzte Bedingung: das Loeschen der Vorlage - der
        // ZWEITE Schritt - schlaegt fehl, nachdem die Buchungen im selben
        // Vorgang bereits weg waren.
        _connection.Execute("""
            CREATE TRIGGER Vorlagenloeschung_verbieten
            BEFORE DELETE ON RecurringExpense
            BEGIN
                SELECT RAISE(ABORT, 'Loeschen im Test unterbunden');
            END
            """);

        try
        {
            Assert.ThrowsAny<Exception>(() => _repository.DeleteWithExpenses(template.Id));
        }
        finally
        {
            _connection.Execute("DROP TRIGGER Vorlagenloeschung_verbieten");
        }

        // Beides muss noch da sein - halb geloescht waere der schlimmste
        // aller Ausgaenge: die Buchungen weg, die Vorlage erzeugt weiter.
        Assert.NotNull(_repository.GetById(template.Id));
        Assert.Equal(3, GetGeneratedExpenses(template.Id).Count);
    }

    [Fact]
    public void CountGeneratedExpenses_zaehlt_nur_die_Buchungen_dieser_Vorlage()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var andereVorlage = _repository.Create(
            _categoryId, _payerId, 500, "Zeitung", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));
        _expenseRepository.Create(_categoryId, 700, new DateOnly(2026, 2, 2), _payerId);

        Assert.Equal(3, _repository.CountGeneratedExpenses(template.Id));
        Assert.Equal(3, _repository.CountGeneratedExpenses(andereVorlage.Id));
    }

    [Fact]
    public void CountGeneratedExpenses_liefert_null_fuer_eine_Vorlage_ohne_Buchungen()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Kuenftig", "month", 1, 1, new DateOnly(2027, 1, 1), null);

        Assert.Equal(0, _repository.CountGeneratedExpenses(template.Id));
        Assert.Equal(0, _repository.CountGeneratedExpenses(99999));
    }

    [Fact]
    public void GetGeneratedExpenseCounts_zaehlt_je_Vorlage()
    {
        var mitBuchungen = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var ohneBuchungen = _repository.Create(
            _categoryId, _payerId, 1000, "Kuenftig", "month", 1, 1, new DateOnly(2027, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 3, 1));

        var anzahlen = _repository.GetGeneratedExpenseCounts();

        Assert.Equal(3, anzahlen[mitBuchungen.Id]);

        // Vorlagen ohne Buchung fehlen im Ergebnis - gleiches Muster wie
        // bei Kategorien und Personen.
        Assert.False(anzahlen.ContainsKey(ohneBuchungen.Id));
    }

    [Fact]
    public void GetGeneratedExpenseCounts_zaehlt_handerfasste_Buchungen_nicht_mit()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(new DateOnly(2026, 1, 1));
        _expenseRepository.Create(_categoryId, 500, new DateOnly(2026, 1, 5), _payerId);

        Assert.Equal(1, _repository.GetGeneratedExpenseCounts()[template.Id]);
    }

    // ---------------------------------------------------------------
    // Einzelerzeugung
    // ---------------------------------------------------------------

    [Fact]
    public void Einzelerzeugung_laesst_andere_Vorlagen_unberuehrt()
    {
        // Beim Speichern einer neuen Vorlage darf nicht nebenbei die
        // Historie aller anderen Vorlagen entstehen.
        var gespeicherte = _repository.Create(
            _categoryId, _payerId, 1000, "Neu angelegt", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        var andere = _repository.Create(
            _categoryId, _payerId, 2000, "Laeuft schon", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        var erzeugt = _repository.GenerateDueOccurrences(gespeicherte.Id, new DateOnly(2026, 3, 1));

        Assert.Equal(3, erzeugt.Count);
        Assert.All(erzeugt, e => Assert.Equal(gespeicherte.Id, e.RecurringExpenseId));

        Assert.Empty(GetGeneratedExpenses(andere.Id));
        Assert.Null(_repository.GetById(andere.Id)!.GeneratedThrough);
    }

    [Fact]
    public void Einzelerzeugung_schreibt_GeneratedThrough_fort()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Miete", "month", 1, 1, new DateOnly(2026, 1, 1), null);

        _repository.GenerateDueOccurrences(template.Id, new DateOnly(2026, 3, 1));

        Assert.Equal(new DateOnly(2026, 3, 1), _repository.GetById(template.Id)!.GeneratedThrough);
        Assert.Empty(_repository.GenerateDueOccurrences(template.Id, new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Einzelerzeugung_erzeugt_fuer_eine_inaktive_Vorlage_nichts()
    {
        var template = _repository.Create(
            _categoryId, _payerId, 1000, "Stillgelegt", "month", 1, 1, new DateOnly(2026, 1, 1), null);
        _repository.Deactivate(template.Id);

        Assert.Empty(_repository.GenerateDueOccurrences(template.Id, new DateOnly(2026, 3, 1)));

        // Auch GeneratedThrough darf sich dabei nicht bewegen - sonst waere
        // der Rueckstand beim Reaktivieren stillschweigend verschwunden.
        Assert.Null(_repository.GetById(template.Id)!.GeneratedThrough);
    }

    [Fact]
    public void Einzelerzeugung_fuer_eine_unbekannte_Vorlage_erzeugt_nichts()
    {
        Assert.Empty(_repository.GenerateDueOccurrences(99999, new DateOnly(2026, 3, 1)));
    }

    private void InsertRawExpense(int recurringExpenseId, DateOnly date)
    {
        var nowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow);

        _connection.Execute("""
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, PayerId, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, 1, @ExpenseDateText, @PayerId, @RecurringExpenseId, @NowUtcText, @NowUtcText)
            """, new
        {
            CategoryId = _categoryId,
            ExpenseDateText = IsoDate.ToDateText(date),
            PayerId = _payerId,
            RecurringExpenseId = recurringExpenseId,
            NowUtcText = nowUtcText,
        });
    }

    private List<GeneratedExpenseRow> GetGeneratedExpenses(int recurringExpenseId)
    {
        var rows = _connection.Query<ExpenseDateRow>("""
            SELECT Id, ExpenseDate
            FROM Expense
            WHERE RecurringExpenseId = @Id
            ORDER BY ExpenseDate
            """, new { Id = recurringExpenseId });

        return rows.Select(r => new GeneratedExpenseRow(r.Id, IsoDate.ParseDate(r.ExpenseDate))).ToList();
    }

    private sealed record GeneratedExpenseRow(int Id, DateOnly ExpenseDate);

    private sealed class ExpenseDateRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
    }
}
