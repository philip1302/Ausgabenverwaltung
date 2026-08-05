-- ============================================================
-- Migration Schema v3 -> v4
--
-- Zieht eine bestehende Datenbank der Version 3 auf Version 4.
-- Laeuft beim Programmstart vor allem anderen und erst,
-- nachdem eine Sicherung geschrieben wurde (siehe
-- StartupService). Ausgefuehrt wird sie in EINER Transaktion:
-- SQLite kann auch DDL zuruecknehmen, eine abgebrochene
-- Migration hinterlaesst also keinen halben Stand.
--
-- Neu: Expense.IsIncome und RecurringExpense.IsIncome. Beide
-- ALTER TABLE ... ADD COLUMN mit DEFAULT 0 - jede bestehende
-- Buchung und Vorlage bleibt also eine ganz normale Ausgabe,
-- kein UPDATE noetig.
--
-- Neue Datenbanken entstehen nicht ueber diesen Weg, sondern
-- direkt aus docs/schema_v4.sql. Beide muessen zum selben
-- Ergebnis fuehren (siehe DatabaseMigratorTests).
-- ============================================================

ALTER TABLE Expense          ADD COLUMN IsIncome INTEGER NOT NULL DEFAULT 0;
ALTER TABLE RecurringExpense ADD COLUMN IsIncome INTEGER NOT NULL DEFAULT 0;

-- Fortschreiben statt Ueberschreiben: die Zeile der Version 3
-- bleibt stehen, die Historie der angewandten Staende bleibt
-- damit lesbar. Ausgewertet wird ueber MAX(Version).
INSERT INTO SchemaVersion
VALUES (4, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
