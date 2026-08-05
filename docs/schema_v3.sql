-- ============================================================
-- Ausgabenverwaltung – Schema Version 3
--
-- Massgebliches Schema fuer NEUE Datenbanken. Bestehende
-- Datenbanken der Version 2 werden nicht mit dieser Datei
-- angelegt, sondern mit docs/migration_v2_to_v3.sql
-- hochgezogen. Beide Wege muessen zum selben Ergebnis fuehren
-- (siehe DatabaseMigratorTests).
--
-- Unterschied zu Version 2: Person.SortOrder.
--
-- Bei JEDER neu geoeffneten Verbindung noetig, sonst prueft
-- SQLite keine Fremdschluessel:
-- ============================================================
PRAGMA foreign_keys = ON;


-- ============================================================
-- PERSON
-- Genau eine Zeile hat IsSelf = 1 (der Anwender selbst).
-- Alle anderen sind moegliche Zahlungsverantwortliche.
-- ============================================================
CREATE TABLE Person (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL UNIQUE,

    -- 0/1, weil SQLite keinen BOOLEAN-Typ kennt
    IsSelf      INTEGER NOT NULL DEFAULT 0,
    IsArchived  INTEGER NOT NULL DEFAULT 0,
    CreatedUtc  TEXT    NOT NULL,

    -- Neu in Version 3: Anzeigereihenfolge, von Hand ueber
    -- Pfeil-nach-oben/-unten verschiebbar (siehe
    -- PersonRepository.MoveUp/MoveDown). Steht hinter CreatedUtc
    -- und nicht an der thematisch passenderen Stelle weiter oben:
    -- ALTER TABLE ADD COLUMN haengt neue Spalten immer hinten an,
    -- und eine migrierte Datenbank soll sich von einer neu
    -- angelegten in nichts unterscheiden (siehe DatabaseMigratorTests).
    SortOrder   INTEGER NOT NULL DEFAULT 0
);

-- Stellt sicher, dass es hoechstens EIN "Ich" gibt.
-- Ein partieller Index (mit WHERE) indiziert nur die Zeilen,
-- die die Bedingung erfuellen – hier also nur die IsSelf-Zeile.
CREATE UNIQUE INDEX UX_Person_Self
    ON Person(IsSelf) WHERE IsSelf = 1;


-- ============================================================
-- CATEGORY – selbstreferenzierender Baum
-- ============================================================
CREATE TABLE Category (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,

    -- NULL = Oberkategorie, sonst Verweis auf die Eltern-Id
    ParentId    INTEGER NULL,
    Name        TEXT    NOT NULL,
    SortOrder   INTEGER NOT NULL DEFAULT 0,
    IsArchived  INTEGER NOT NULL DEFAULT 0,
    CreatedUtc  TEXT    NOT NULL,

    -- Neu in Version 2: Anzeigefarbe als '#RRGGBB'.
    -- NULL = keine eigene Farbe; die Kategorie erbt dann die
    -- Farbe des naechsten Vorfahren, der eine hat. Deshalb
    -- ausdruecklich NULL-faehig und ohne Vorgabewert: ein
    -- Vorgabewert waere eine eigene Farbe und wuerde jede
    -- Vererbung unterbinden. Ausgewertet wird die Spalte in
    -- CategoryColors.
    --
    -- Steht hinter CreatedUtc und nicht an der thematisch
    -- passenderen Stelle weiter oben: ALTER TABLE ADD COLUMN
    -- haengt neue Spalten immer hinten an, und eine migrierte
    -- Datenbank soll sich von einer neu angelegten in nichts
    -- unterscheiden (siehe DatabaseMigratorTests).
    Color       TEXT    NULL,

    FOREIGN KEY (ParentId) REFERENCES Category(Id)
        ON DELETE RESTRICT,

    UNIQUE (ParentId, Name)
);

-- Das UNIQUE oben greift nicht fuer Oberkategorien, weil zwei
-- NULL-Werte in SQL als verschieden gelten. Diese Luecke
-- schliesst der partielle Index:
CREATE UNIQUE INDEX UX_Category_RootName
    ON Category(Name) WHERE ParentId IS NULL;


-- ============================================================
-- RECURRING_EXPENSE – Vorlage fuer wiederkehrende Ausgaben
-- ============================================================
CREATE TABLE RecurringExpense (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,

    -- Was erzeugt werden soll (Vorlagenwerte):
    CategoryId    INTEGER NOT NULL,
    PayerId       INTEGER NOT NULL,
    AmountCents   INTEGER NOT NULL,
    Note          TEXT    NULL,

    -- Eigener Name fuer die Verwaltungsliste,
    -- z. B. "Stallmiete Ponyhof Weber"
    Title         TEXT    NOT NULL,

    -- --- Rhythmus -------------------------------------------
    -- Zusammen ergeben Unit und Count das Intervall:
    --   month/1  = monatlich
    --   month/3  = quartalsweise
    --   year/1   = jaehrlich
    --   week/2   = alle zwei Wochen
    IntervalUnit  TEXT    NOT NULL,
    IntervalCount INTEGER NOT NULL DEFAULT 1,

    -- Faelligkeitstag im Monat (1..31), nur bei month/year.
    -- Gibt es den Tag im Zielmonat nicht, wird auf den
    -- letzten Tag des Monats gekuerzt.
    AnchorDay     INTEGER NULL,

    -- --- Laufzeit -------------------------------------------
    -- Erstes Vorkommen. ACHTUNG: Ein weit zurueckliegendes
    -- StartDate erzeugt beim ersten Lauf die gesamte
    -- Historie – in der Oberflaeche abfragen.
    StartDate     TEXT    NOT NULL,   -- 'YYYY-MM-DD'
    EndDate       TEXT    NULL,       -- NULL = unbefristet

    -- Bis zu diesem Datum wurde bereits erzeugt.
    -- Verhindert Doppelerzeugung UND das Wiederauferstehen
    -- geloeschter Buchungen. NULL = noch nie gelaufen.
    GeneratedThrough TEXT NULL,

    IsActive      INTEGER NOT NULL DEFAULT 1,
    CreatedUtc    TEXT    NOT NULL,
    ModifiedUtc   TEXT    NOT NULL,

    FOREIGN KEY (CategoryId) REFERENCES Category(Id)
        ON DELETE RESTRICT,
    FOREIGN KEY (PayerId)    REFERENCES Person(Id)
        ON DELETE RESTRICT,

    CHECK (IntervalUnit IN ('day','week','month','year')),
    CHECK (IntervalCount > 0),
    CHECK (AnchorDay IS NULL OR AnchorDay BETWEEN 1 AND 31)
);


-- ============================================================
-- EXPENSE – die eigentlichen Buchungen
-- ============================================================
CREATE TABLE Expense (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,

    CategoryId      INTEGER NOT NULL,

    -- Betrag in Cent: 4.200,00 EUR -> 420000
    -- Niemals Gleitkomma. Negative Werte = Erstattung.
    AmountCents     INTEGER NOT NULL,

    -- Belegdatum 'YYYY-MM-DD'. Dieses Format sortiert
    -- alphabetisch = chronologisch, deshalb funktionieren
    -- BETWEEN und ORDER BY direkt auf dem Text.
    ExpenseDate     TEXT    NOT NULL,

    Note            TEXT    NULL,

    -- --- Zahlungsverantwortung ------------------------------
    -- Wer muss das begleichen. Standard: die IsSelf-Person.
    PayerId         INTEGER NOT NULL,

    -- Wann wurde tatsaechlich gezahlt.
    -- NULL = offen, ABER nur relevant, wenn PayerId nicht
    -- die eigene Person ist. Bei eigenen Ausgaben bleibt
    -- das Feld leer und wird nie ausgewertet.
    SettledDate     TEXT    NULL,

    -- --- Herkunft -------------------------------------------
    -- NULL = von Hand erfasst,
    -- gefuellt = aus dieser Vorlage automatisch erzeugt
    RecurringExpenseId INTEGER NULL,

    CreatedUtc      TEXT    NOT NULL,
    ModifiedUtc     TEXT    NOT NULL,

    FOREIGN KEY (CategoryId) REFERENCES Category(Id)
        ON DELETE RESTRICT,
    FOREIGN KEY (PayerId)    REFERENCES Person(Id)
        ON DELETE RESTRICT,
    FOREIGN KEY (RecurringExpenseId) REFERENCES RecurringExpense(Id)
        ON DELETE SET NULL   -- Vorlage weg, Buchung bleibt
);

-- Zweite Absicherung gegen Doppelerzeugung: pro Vorlage kann
-- es je Datum nur eine Buchung geben. Handerfasste Zeilen
-- haben hier NULL und kollidieren nie miteinander – wieder
-- die NULL-Eigenheit, diesmal als Vorteil.
CREATE UNIQUE INDEX UX_Expense_Occurrence
    ON Expense(RecurringExpenseId, ExpenseDate)
    WHERE RecurringExpenseId IS NOT NULL;

CREATE INDEX IX_Expense_Date     ON Expense(ExpenseDate);
CREATE INDEX IX_Expense_Category ON Expense(CategoryId);

-- Partieller Index fuer die Offene-Posten-Liste: indiziert
-- nur die unbeglichenen Zeilen, bleibt also winzig.
CREATE INDEX IX_Expense_Open
    ON Expense(PayerId, ExpenseDate)
    WHERE SettledDate IS NULL;


-- ============================================================
-- SCHEMA-VERSION
-- Zeitstempel im vorgeschriebenen Format (Regel 3); das
-- datetime('now') aus Version 1 lieferte ein Leerzeichen
-- statt 'T' und kein 'Z'.
-- ============================================================
CREATE TABLE SchemaVersion (
    Version    INTEGER NOT NULL,
    AppliedUtc TEXT    NOT NULL
);
INSERT INTO SchemaVersion
VALUES (3, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));


-- ============================================================
-- ABFRAGE 1: Offene Posten
-- Alle nicht beglichenen Ausgaben, fuer die jemand anderes
-- zustaendig ist – gruppiert nach Person.
-- ============================================================
SELECT
    p.Name                        AS Person,
    e.ExpenseDate,
    c.Name                        AS Kategorie,
    e.AmountCents,
    e.Note,

    -- julianday() liefert die fortlaufende Tageszahl;
    -- die Differenz ergibt das Alter der Forderung in Tagen
    CAST(julianday('now') - julianday(e.ExpenseDate) AS INTEGER)
                                  AS TageOffen
FROM   Expense e
JOIN   Person   p ON p.Id = e.PayerId
JOIN   Category c ON c.Id = e.CategoryId
WHERE  e.SettledDate IS NULL
  AND  p.IsSelf = 0
ORDER  BY p.Name, e.ExpenseDate;


-- ============================================================
-- ABFRAGE 2: Auswertung ueber einen Kategorie-Ast
-- "Wieviel habe ich fuer Heizkosten in den letzten drei
--  Jahren bezahlt?" – inklusive aller Unterkategorien.
-- ============================================================
WITH RECURSIVE Subtree(Id) AS (
    -- Ankerteil: der gewaehlte Knoten selbst
    SELECT Id FROM Category WHERE Id = @RootId
    UNION ALL
    -- Rekursionsteil: dessen Kinder, deren Kinder, ...
    SELECT c.Id
    FROM   Category c
    JOIN   Subtree  s ON c.ParentId = s.Id
)
SELECT
    strftime('%Y', e.ExpenseDate) AS Jahr,
    SUM(e.AmountCents)            AS SummeCents,
    COUNT(*)                      AS Anzahl
FROM   Expense e
JOIN   Person  p ON p.Id = e.PayerId
WHERE  e.CategoryId IN (SELECT Id FROM Subtree)
  AND  e.ExpenseDate >= @Von
  AND  e.ExpenseDate <  @Bis
  -- @PayerFilter: 'self' | 'others' | 'all'
  AND  (@PayerFilter = 'all'
        OR (@PayerFilter = 'self'   AND p.IsSelf = 1)
        OR (@PayerFilter = 'others' AND p.IsSelf = 0))
GROUP  BY Jahr
ORDER  BY Jahr;
