using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Reports;
using Dapper;

namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Anlegen, Aendern, Loeschen und Laden einzelner Ausgaben. Die
/// Offene-Posten-Liste (Regel 4) steckt in
/// Ausgabenverwaltung.Core.OpenItems.OpenItemsRepository, weil sie
/// zusaetzlich Personenname und vollen Kategoriepfad braucht. Betraege
/// sind immer long-Cent (Regel 1), Datumsangaben werden ueber
/// IsoDate/IsoDateTime als 'YYYY-MM-DD' bzw. 'YYYY-MM-DDTHH:MM:SSZ'-TEXT
/// gespeichert (Regel 3).
/// </summary>
public sealed class ExpenseRepository
{
    private readonly IDbConnection _connection;

    public ExpenseRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public Expense Create(
        int categoryId,
        long amountCents,
        DateOnly expenseDate,
        int payerId,
        string? note = null,
        DateOnly? settledDate = null,
        int? recurringExpenseId = null,
        bool isIncome = false)
    {
        var nowUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @AmountCents, @ExpenseDateText, @Note, @PayerId,
                 @SettledDateText, @RecurringExpenseId, @IsIncome, @NowUtcText, @NowUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDateText = IsoDate.ToDateText(expenseDate),
            Note = note,
            PayerId = payerId,
            SettledDateText = settledDate is DateOnly settled ? IsoDate.ToDateText(settled) : null,
            RecurringExpenseId = recurringExpenseId,
            IsIncome = isIncome,
            NowUtcText = IsoDateTime.ToUtcText(nowUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        AppLog.Current.Info(LogEvents.ExpenseCreated((int)id, isIncome));

        return new Expense
        {
            Id = (int)id,
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDate = expenseDate,
            Note = note,
            PayerId = payerId,
            SettledDate = settledDate,
            RecurringExpenseId = recurringExpenseId,
            IsIncome = isIncome,
            CreatedUtc = nowUtc,
            ModifiedUtc = nowUtc,
        };
    }

    public void Update(
        int id,
        int categoryId,
        long amountCents,
        DateOnly expenseDate,
        int payerId,
        string? note,
        DateOnly? settledDate,
        bool isIncome = false)
    {
        const string sql = """
            UPDATE Expense
            SET CategoryId = @CategoryId,
                AmountCents = @AmountCents,
                ExpenseDate = @ExpenseDateText,
                Note = @Note,
                PayerId = @PayerId,
                SettledDate = @SettledDateText,
                IsIncome = @IsIncome,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        _connection.Execute(sql, new
        {
            Id = id,
            CategoryId = categoryId,
            AmountCents = amountCents,
            ExpenseDateText = IsoDate.ToDateText(expenseDate),
            Note = note,
            PayerId = payerId,
            SettledDateText = settledDate is DateOnly settled ? IsoDate.ToDateText(settled) : null,
            IsIncome = isIncome,
            NowUtcText = IsoDateTime.ToUtcText(DateTime.UtcNow),
        });

        AppLog.Current.Info(LogEvents.ExpenseUpdated(id));
    }

    public void Delete(int id)
    {
        const string sql = "DELETE FROM Expense WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });

        AppLog.Current.Info(LogEvents.ExpenseDeleted(id));
    }

    /// <summary>
    /// Loescht mehrere Ausgaben in einer Anweisung - fuer das
    /// Sammel-Loeschen in der Ausgabenliste. Eine leere Liste ist
    /// zulaessig und veraendert nichts.
    /// </summary>
    public void DeleteMany(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        const string sql = "DELETE FROM Expense WHERE Id IN @Ids";
        _connection.Execute(sql, new { Ids = ids });

        AppLog.Current.Info(LogEvents.ExpensesDeleted(ids.Count));
    }

    /// <summary>
    /// Legt geloeschte Ausgaben wieder an - das Gegenstueck zu
    /// <see cref="DeleteMany"/> fuer das Rueckgaengig-Band der
    /// Ausgabenliste. Alles in EINER Transaktion: eine halb
    /// wiederhergestellte Auswahl waere schlimmer als eine gar nicht
    /// wiederhergestellte, weil niemand ihr ansieht, wo sie aufgehoert hat.
    ///
    /// Die Zeilen bekommen neue Ids - die alten sind mit dem Loeschen
    /// verfallen, und eine davon kann inzwischen an eine neu erfasste
    /// Buchung vergeben sein. Alles uebrige wird unveraendert
    /// zurueckgeschrieben, <b>einschliesslich CreatedUtc</b>: erfasst
    /// wurde die Buchung damals, und in "Letzte Buchungen" soll sie wieder
    /// dort auftauchen, wo sie vorher stand. Nur ModifiedUtc wandert auf
    /// jetzt - angefasst wurde die Zeile ja gerade.
    /// </summary>
    public int RestoreMany(IReadOnlyList<Expense> expenses)
    {
        if (expenses.Count == 0)
        {
            return 0;
        }

        const string sql = """
            INSERT INTO Expense
                (CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc)
            VALUES
                (@CategoryId, @AmountCents, @ExpenseDateText, @Note, @PayerId,
                 @SettledDateText, @RecurringExpenseId, @IsIncome, @CreatedUtcText, @NowUtcText)
            """;

        var nowUtcText = JetztUtcText();

        // Eine Parameterliste statt einer Schleife mit Einzelaufrufen:
        // Dapper fuehrt dieselbe Anweisung je Element aus, alle innerhalb
        // derselben Transaktion.
        var parameter = expenses.Select(expense => new
        {
            expense.CategoryId,
            expense.AmountCents,
            ExpenseDateText = IsoDate.ToDateText(expense.ExpenseDate),
            expense.Note,
            expense.PayerId,
            SettledDateText = expense.SettledDate is DateOnly settled
                ? IsoDate.ToDateText(settled)
                : null,
            expense.RecurringExpenseId,
            expense.IsIncome,
            CreatedUtcText = IsoDateTime.ToUtcText(expense.CreatedUtc),
            NowUtcText = nowUtcText,
        }).ToList();

        var wiederhergestellt = AendereInTransaktion(sql, parameter);

        AppLog.Current.Info(LogEvents.ExpensesRestored(wiederhergestellt));

        return wiederhergestellt;
    }

    // ---------------- Sammelaenderungen ----------------
    //
    // Was fuer das Loeschen laengst geht, geht auch fuer das Aendern:
    // mehrere markierte Zeilen auf einmal umbuchen. Jede der drei
    // Methoden laeuft in EINER Transaktion - eine halb umgebuchte Auswahl
    // waere schlimmer als eine gar nicht umgebuchte, weil niemand ihr
    // ansieht, wo sie aufgehoert hat.
    //
    // ModifiedUtc wird ueberall mitgezogen (wie beim Zusammenfuehren von
    // Kategorien): die Buchungen haben sich geaendert, auch wenn Betrag
    // und Datum gleich bleiben. CreatedUtc bleibt unberuehrt - erfasst
    // wurden sie damals.
    //
    // Alle drei liefern die Zahl der TATSAECHLICH geaenderten Zeilen. Sie
    // kann kleiner sein als die Auswahl (siehe SetSettledMany), und die
    // Oberflaeche muss das sagen koennen.

    /// <summary>
    /// Bucht mehrere Ausgaben auf eine andere Kategorie um. Eine leere
    /// Liste ist zulaessig und veraendert nichts.
    /// </summary>
    public int SetCategoryMany(IReadOnlyList<int> ids, int categoryId)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        const string sql = """
            UPDATE Expense
            SET CategoryId  = @CategoryId,
                ModifiedUtc = @NowUtcText
            WHERE Id IN @Ids
            """;

        var geaendert = AendereInTransaktion(
            sql, new { Ids = ids, CategoryId = categoryId, NowUtcText = JetztUtcText() });

        AppLog.Current.Info(LogEvents.ExpensesCategoryChanged(geaendert, categoryId));

        return geaendert;
    }

    /// <summary>
    /// Bucht mehrere Ausgaben auf einen anderen Zahler um. Eine leere
    /// Liste ist zulaessig und veraendert nichts.
    ///
    /// Ein gesetztes SettledDate bleibt dabei stehen. Es gehoert der
    /// einzelnen Buchung (Regel 4) - wandert die Buchung auf die eigene
    /// Person, wird es nur nicht mehr ausgewertet.
    /// </summary>
    public int SetPayerMany(IReadOnlyList<int> ids, int payerId)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        const string sql = """
            UPDATE Expense
            SET PayerId     = @PayerId,
                ModifiedUtc = @NowUtcText
            WHERE Id IN @Ids
            """;

        var geaendert = AendereInTransaktion(
            sql, new { Ids = ids, PayerId = payerId, NowUtcText = JetztUtcText() });

        AppLog.Current.Info(LogEvents.ExpensesPayerChanged(geaendert, payerId));

        return geaendert;
    }

    /// <summary>
    /// Setzt oder loescht den Beglichen-Status mehrerer Ausgaben
    /// (NULL = wieder offen).
    ///
    /// <b>Regel 4:</b> Bei einer eigenen Ausgabe bedeutet SettledDate
    /// nichts und wird nie ausgewertet. Solche Zeilen bleiben deshalb
    /// unberuehrt - sie werden im SQL uebersprungen, nicht vorher
    /// aussortiert, damit die Entscheidung an genau einer Stelle steht.
    /// Zurueck kommt die Zahl der tatsaechlich geaenderten Zeilen; sie ist
    /// kleiner als die Auswahl, sobald eigene Ausgaben darin waren, und
    /// die Oberflaeche sagt das dann auch.
    /// </summary>
    public int SetSettledMany(IReadOnlyList<int> ids, DateOnly? settledDate)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        const string sql = """
            UPDATE Expense
            SET SettledDate = @SettledDateText,
                ModifiedUtc = @NowUtcText
            WHERE Id IN @Ids
              AND PayerId IN (SELECT Id FROM Person WHERE IsSelf = 0)
            """;

        var geaendert = AendereInTransaktion(sql, new
        {
            Ids = ids,
            SettledDateText = settledDate is DateOnly datum ? IsoDate.ToDateText(datum) : null,
            NowUtcText = JetztUtcText(),
        });

        AppLog.Current.Info(LogEvents.ExpensesSettled(geaendert, settledDate is not null));

        return geaendert;
    }

    /// <summary>
    /// Schreibt den Beglichen-Stand mehrerer Buchungen zurueck, wie er in
    /// <see cref="SettledState"/> festgehalten wurde - der Weg zurueck aus
    /// einem Abhaken.
    ///
    /// Anders als ein <see cref="SetSettledMany"/> mit NULL trifft das
    /// auch den Fall, dass eine Zeile vorher schon ein (aelteres)
    /// Begleichungsdatum trug und beim Abhaken ueberschrieben wurde: sie
    /// bekommt genau dieses Datum wieder, nicht "offen".
    ///
    /// Zurueck kommt die Zahl der tatsaechlich geaenderten Zeilen. Sie ist
    /// kleiner als die Liste, wenn eine Buchung zwischenzeitlich anderswo
    /// geloescht wurde - die Ruecknahme holt sie nicht zurueck, das ist
    /// Sache des Loeschen-Rueckgaengig.
    /// </summary>
    public int RestoreSettledDates(IReadOnlyList<SettledState> states)
    {
        if (states.Count == 0)
        {
            return 0;
        }

        const string sql = """
            UPDATE Expense
            SET SettledDate = @SettledDateText,
                ModifiedUtc = @NowUtcText
            WHERE Id = @Id
            """;

        var nowUtcText = JetztUtcText();

        // Eine Parameterliste statt einer Schleife mit Einzelaufrufen (wie
        // in RestoreMany): Dapper fuehrt dieselbe Anweisung je Element aus,
        // alle innerhalb derselben Transaktion. Je Zeile ein eigenes Datum -
        // deshalb hier "Id = @Id" und kein "IN @Ids".
        var parameter = states.Select(state => new
        {
            state.Id,
            SettledDateText = state.SettledDate is DateOnly datum
                ? IsoDate.ToDateText(datum)
                : null,
            NowUtcText = nowUtcText,
        }).ToList();

        var zurueckgenommen = AendereInTransaktion(sql, parameter);

        AppLog.Current.Info(LogEvents.ExpensesSettlementUndone(zurueckgenommen));

        return zurueckgenommen;
    }

    // Der gemeinsame Rahmen der drei Sammelaenderungen. Dapper erweitert
    // "IN @Ids" selbst zu einer Parameterliste; die Transaktion muss dabei
    // mitgereicht werden, sonst laeuft die Anweisung ausserhalb.
    private int AendereInTransaktion(string sql, object parameter)
    {
        using var transaction = _connection.BeginTransaction();

        var geaendert = _connection.Execute(sql, parameter, transaction);
        transaction.Commit();

        return geaendert;
    }

    private static string JetztUtcText() => IsoDateTime.ToUtcText(DateTime.UtcNow);

    public Expense? GetById(int id)
    {
        const string sql = """
            SELECT Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                   SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc
            FROM Expense
            WHERE Id = @Id
            """;

        var row = _connection.QueryFirstOrDefault<ExpenseRow>(sql, new { Id = id });
        return row is null ? null : ToExpense(row);
    }

    /// <summary>
    /// Mehrere Ausgaben auf einmal - fuer das Rueckgaengig-Band der
    /// Ausgabenliste, das die Werte sichern muss, BEVOR geloescht wird.
    /// Eine leere Liste liefert eine leere Liste zurueck. Ids, zu denen es
    /// nichts (mehr) gibt, fehlen im Ergebnis, ohne dass es einen Fehler
    /// gibt - die Liste des Aufrufers kann veraltet sein.
    /// </summary>
    public IReadOnlyList<Expense> GetByIds(IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Expense>();
        }

        const string sql = """
            SELECT Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                   SettledDate, RecurringExpenseId, IsIncome, CreatedUtc, ModifiedUtc
            FROM Expense
            WHERE Id IN @Ids
            """;

        return _connection.Query<ExpenseRow>(sql, new { Ids = ids })
                          .Select(ToExpense)
                          .ToList();
    }

    /// <summary>
    /// Die zuletzt ERFASSTEN Ausgaben (nach CreatedUtc, nicht nach
    /// ExpenseDate - ein nachtraeglich datiertes Buchungsdatum soll den
    /// gerade eingegebenen Eintrag nicht nach unten schieben), mit
    /// Kategorie- und Zahlername fuer die Anzeige.
    /// </summary>
    public IReadOnlyList<ExpenseOverview> GetRecent(int count)
    {
        const string sql = """
            SELECT e.Id, e.ExpenseDate, e.AmountCents, c.Name AS CategoryName,
                   p.Name AS PayerName, e.Note, e.IsIncome, p.IsSelf AS PayerIsSelf,
                   e.SettledDate
            FROM   Expense e
            JOIN   Category c ON c.Id = e.CategoryId
            JOIN   Person   p ON p.Id = e.PayerId
            ORDER BY e.CreatedUtc DESC, e.Id DESC
            LIMIT @Count
            """;

        var rows = _connection.Query<ExpenseOverviewRow>(sql, new { Count = count });

        return rows.Select(row => new ExpenseOverview
        {
            Id = row.Id,
            ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
            AmountCents = row.AmountCents,
            CategoryName = row.CategoryName,
            PayerName = row.PayerName,
            Note = row.Note,
            IsIncome = row.IsIncome,
            PayerIsSelf = row.PayerIsSelf,
            SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
        }).ToList();
    }

    /// <summary>
    /// Die zuletzt gleichlautend bemerkte Buchung - Grundlage fuer das
    /// Angebot in der Erfassungsmaske ("Zuletzt: Lebensmittel · 42,90 € ·
    /// Paul"). NULL, wenn es keine gibt.
    ///
    /// Verglichen wird die GANZE Bemerkung, nicht ihr Anfang: "Aldi" und
    /// "Aldi Getränke" sind zwei verschiedene Einkaeufe, und ein Angebot
    /// aus dem falschen davon waere schlimmer als keines.
    ///
    /// COLLATE NOCASE, weil "aldi" und "Aldi" derselbe Laden sind. Die
    /// SQLite-Voreinstellung NOCASE gilt nur fuer ASCII-Buchstaben; ein
    /// "Café" bleibt deshalb von "café" unterschieden. Das ist die
    /// hinnehmbare Luecke - die Alternative waere eine eigene
    /// Vergleichsfunktion fuer eine Bequemlichkeit.
    ///
    /// Sortiert nach ExpenseDate: die juengste gleichlautende Buchung
    /// gewinnt. Bei zwei Buchungen am selben Tag entscheidet die hoehere
    /// Id, also die spaeter erfasste.
    /// </summary>
    public ExpenseSuggestion? SuggestFor(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        const string sql = """
            WITH RECURSIVE Pfad(Id, ParentId, FullPath) AS (
                SELECT Id, ParentId, Name FROM Category WHERE ParentId IS NULL
                UNION ALL
                SELECT c.Id, c.ParentId, Pfad.FullPath || ' › ' || c.Name
                FROM   Category c
                JOIN   Pfad ON c.ParentId = Pfad.Id
            )
            SELECT
                e.CategoryId,
                pfad.FullPath AS CategoryFullPath,
                e.AmountCents,
                e.PayerId,
                p.Name        AS PayerName,
                e.IsIncome,
                e.ExpenseDate
            FROM Expense e
            JOIN Person p    ON p.Id = e.PayerId
            JOIN Pfad   pfad ON pfad.Id = e.CategoryId
            WHERE e.Note = @Note COLLATE NOCASE
            ORDER BY e.ExpenseDate DESC, e.Id DESC
            LIMIT 1
            """;

        var row = _connection.QueryFirstOrDefault<SuggestionRow>(sql, new { Note = note.Trim() });

        return row is null
            ? null
            : new ExpenseSuggestion
            {
                CategoryId = row.CategoryId,
                CategoryFullPath = row.CategoryFullPath,
                AmountCents = row.AmountCents,
                PayerId = row.PayerId,
                PayerName = row.PayerName,
                IsIncome = row.IsIncome,
                ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
            };
    }

    /// <summary>
    /// Die Ausgabenliste: alle Buchungen, die auf den Filter passen, mit
    /// Zahlername, vollem Kategoriepfad und Vorlagentitel. Gefiltert UND
    /// sortiert wird in SQL, nicht im Speicher - die Liste kann ueber
    /// Jahre tausende Zeilen umfassen.
    /// </summary>
    public IReadOnlyList<ExpenseListItem> Query(
        ReportFilter filter, ExpenseSortColumn sort, bool ascending)
    {
        // Kopf und Filter stehen als Konstante fest, nur die ORDER-BY-
        // Klausel kommt hinzu - und die stammt aus einer Weissliste ueber
        // ExpenseSortColumn, nie aus Anwendereingabe.
        var sql = QuerySql + "\n" + OrderBySql(sort, ascending);

        var rows = _connection.Query<ExpenseListRow>(sql, ReportFilterSql.ToParameters(filter));

        return rows.Select(row => new ExpenseListItem
        {
            Id = row.Id,
            ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
            CategoryId = row.CategoryId,
            CategoryFullPath = row.CategoryFullPath,
            AmountCents = row.AmountCents,
            IsIncome = row.IsIncome,
            PayerId = row.PayerId,
            PayerName = row.PayerName,
            PayerIsSelf = row.PayerIsSelf,
            SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
            Note = row.Note,
            RecurringExpenseId = row.RecurringExpenseId,
            RecurringExpenseTitle = row.RecurringExpenseTitle,
        }).ToList();
    }

    /// <summary>
    /// Gibt es ueberhaupt eine Buchung - unabhaengig von jedem Filter?
    ///
    /// Nur dafuer da, eine leere Liste richtig zu erklaeren: "noch nichts
    /// erfasst" und "nichts passt zu diesem Filter" sehen gleich aus, sind
    /// aber verschiedene Lagen und brauchen verschiedene Angebote. Ohne
    /// diese Frage muesste die Anzeige raten, und sie raet falsch, sobald
    /// jemand einen Zeitraum waehlt, in dem nichts liegt.
    ///
    /// <c>EXISTS</c> statt <c>COUNT(*)</c>: die Datenbank hoert beim ersten
    /// Treffer auf zu suchen, und mehr als "ja oder nein" wird hier nie
    /// gebraucht.
    /// </summary>
    public bool HasAny()
        => _connection.ExecuteScalar<long>(
            "SELECT EXISTS (SELECT 1 FROM Expense)") != 0;

    /// <summary>
    /// Anzahl und Summe der Treffer DESSELBEN Filters wie
    /// <see cref="Query"/> - eigene Aggregatabfrage statt einer Summe ueber
    /// die geladenen Zeilen, damit die Fusszeile der Liste unabhaengig von
    /// der Anzeige stimmt.
    /// </summary>
    public ExpenseListSummary Summarize(ReportFilter filter)
    {
        // Das Vorzeichen kommt vom Buchungstyp: eine Ausgabe mindert die
        // Summe (negativ), eine Einnahme erhoeht sie (positiv) - aber
        // erst, sobald sie tatsaechlich eingegangen ist (SettledDate
        // gesetzt). Eine noch offene Einnahme ist noch nicht real
        // geflossenes Geld und geht deshalb weder erhoehend noch
        // mindernd ein (siehe Entities.Expense.IsIncome). ABS() macht die
        // Rechnung robust gegenueber etwaigen Alt-Datensaetzen, die noch
        // aus der Zeit vor dieser Regel einen negativen Betrag tragen
        // (frueher: Erstattung).
        const string sql = """
            SELECT COUNT(*)                                       AS Anzahl,
                   COALESCE(SUM(
                       CASE WHEN e.IsIncome = 1 AND e.SettledDate IS NOT NULL THEN  ABS(e.AmountCents)
                            WHEN e.IsIncome = 1                              THEN  0
                            ELSE                                                  -ABS(e.AmountCents)
                       END), 0)                                    AS SummeCents
            FROM   Expense e
            JOIN   Person  p ON p.Id = e.PayerId
            WHERE
            """ + ReportFilterSql.Where;

        var row = _connection.QueryFirst<SummaryRow>(sql, ReportFilterSql.ToParameters(filter));
        return new ExpenseListSummary(row.Anzahl, row.SummeCents);
    }

    // Der Kategoriepfad wird per rekursiver CTE gebildet - dasselbe Muster
    // wie in OpenItems.OpenItemsRepository (siehe docs/schema_v1.sql,
    // Abfrage 2). Das Trennzeichen entspricht
    // Categories.CategoryPaths.Separator.
    // Der LEFT JOIN auf RecurringExpense holt den Vorlagentitel; er bleibt
    // NULL bei handerfassten Buchungen und bei Buchungen, deren Vorlage
    // inzwischen geloescht wurde (ON DELETE SET NULL).
    private const string QuerySql = """
        WITH RECURSIVE Pfad(Id, ParentId, FullPath) AS (
            SELECT Id, ParentId, Name FROM Category WHERE ParentId IS NULL
            UNION ALL
            SELECT c.Id, c.ParentId, Pfad.FullPath || ' › ' || c.Name
            FROM   Category c
            JOIN   Pfad ON c.ParentId = Pfad.Id
        )
        SELECT
            e.Id, e.ExpenseDate, e.CategoryId,
            pfad.FullPath      AS CategoryFullPath,
            e.AmountCents, e.IsIncome, e.PayerId,
            p.Name             AS PayerName,
            p.IsSelf           AS PayerIsSelf,
            e.SettledDate, e.Note, e.RecurringExpenseId,
            r.Title            AS RecurringExpenseTitle
        FROM      Expense e
        JOIN      Person  p    ON p.Id = e.PayerId
        JOIN      Pfad    pfad ON pfad.Id = e.CategoryId
        LEFT JOIN RecurringExpense r ON r.Id = e.RecurringExpenseId
        WHERE
        """ + ReportFilterSql.Where;

    // Rangfolge der Statusspalte beim Sortieren. Eigene Ausgaben haben
    // keinen Status (Regel 4) und landen deshalb hinter offen und
    // beglichen, statt mit einem der beiden vermischt zu werden.
    private const string StatusRankSql = """
        CASE WHEN p.IsSelf = 1          THEN 2
             WHEN e.SettledDate IS NULL THEN 0
             ELSE                            1
        END
        """;

    /// <summary>
    /// ORDER-BY-Klausel zur gewaehlten Spalte. Textspalten werden
    /// gross-/kleinschreibungsunabhaengig sortiert (COLLATE NOCASE), sonst
    /// stuenden alle Kleinbuchstaben hinter allen Grossbuchstaben. Die Id
    /// als zweites Kriterium haelt die Reihenfolge bei gleichen Werten
    /// stabil - ohne sie waere sie in SQLite unbestimmt.
    /// </summary>
    private static string OrderBySql(ExpenseSortColumn sort, bool ascending)
    {
        var spalte = sort switch
        {
            ExpenseSortColumn.Datum => "e.ExpenseDate",
            ExpenseSortColumn.Kategorie => "pfad.FullPath COLLATE NOCASE",
            ExpenseSortColumn.Betrag => "e.AmountCents",
            ExpenseSortColumn.Zahler => "p.Name COLLATE NOCASE",
            ExpenseSortColumn.Status => StatusRankSql,
            ExpenseSortColumn.Bemerkung => "e.Note COLLATE NOCASE",
            _ => throw new ArgumentOutOfRangeException(nameof(sort)),
        };

        var richtung = ascending ? "ASC" : "DESC";
        return $"ORDER BY {spalte} {richtung}, e.Id DESC";
    }

    private static Expense ToExpense(ExpenseRow row) => new()
    {
        Id = row.Id,
        CategoryId = row.CategoryId,
        AmountCents = row.AmountCents,
        ExpenseDate = IsoDate.ParseDate(row.ExpenseDate),
        Note = row.Note,
        PayerId = row.PayerId,
        SettledDate = row.SettledDate is null ? null : IsoDate.ParseDate(row.SettledDate),
        RecurringExpenseId = row.RecurringExpenseId,
        IsIncome = row.IsIncome,
        CreatedUtc = IsoDateTime.ParseUtc(row.CreatedUtc),
        ModifiedUtc = IsoDateTime.ParseUtc(row.ModifiedUtc),
    };

    // Datums- und Zeitstempelspalten werden als reiner TEXT gelesen statt
    // ueber automatische Dapper-Konvertierung, damit das Parsen zentral
    // ueber IsoDate/IsoDateTime laeuft (siehe Regel 3).
    private sealed class ExpenseRow
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public long AmountCents { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public string? Note { get; set; }
        public int PayerId { get; set; }
        public string? SettledDate { get; set; }
        public int? RecurringExpenseId { get; set; }
        public bool IsIncome { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string ModifiedUtc { get; set; } = string.Empty;
    }

    private sealed class ExpenseOverviewRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string PayerName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public bool IsIncome { get; set; }
        public bool PayerIsSelf { get; set; }
        public string? SettledDate { get; set; }
    }

    private sealed class SuggestionRow
    {
        public int CategoryId { get; set; }
        public string CategoryFullPath { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public int PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public bool IsIncome { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
    }

    private sealed class ExpenseListRow
    {
        public int Id { get; set; }
        public string ExpenseDate { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryFullPath { get; set; } = string.Empty;
        public long AmountCents { get; set; }
        public bool IsIncome { get; set; }
        public int PayerId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public bool PayerIsSelf { get; set; }
        public string? SettledDate { get; set; }
        public string? Note { get; set; }
        public int? RecurringExpenseId { get; set; }
        public string? RecurringExpenseTitle { get; set; }
    }

    private sealed class SummaryRow
    {
        public int Anzahl { get; set; }
        public long SummeCents { get; set; }
    }
}
