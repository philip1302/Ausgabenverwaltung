-- ============================================================
-- Migration Schema v1 -> v2
--
-- Zieht eine bestehende Datenbank der Version 1 auf Version 2.
-- Laeuft beim Programmstart vor allem anderen und erst,
-- nachdem eine Sicherung geschrieben wurde (siehe
-- StartupService). Ausgefuehrt wird sie in EINER Transaktion:
-- SQLite kann auch DDL zuruecknehmen, eine abgebrochene
-- Migration hinterlaesst also keinen halben Stand.
--
-- ALTER TABLE ... ADD COLUMN fasst die vorhandenen Zeilen
-- nicht an; alle bestehenden Kategorien bekommen Color = NULL
-- und damit "keine eigene Farbe" - genau der Zustand, den eine
-- Datenbank ohne diese Spalte hatte.
--
-- Neue Datenbanken entstehen nicht ueber diesen Weg, sondern
-- direkt aus docs/schema_v2.sql. Beide muessen zum selben
-- Ergebnis fuehren (siehe DatabaseMigratorTests).
-- ============================================================

ALTER TABLE Category ADD COLUMN Color TEXT NULL;

-- Fortschreiben statt Ueberschreiben: die Zeile der Version 1
-- bleibt stehen, die Historie der angewandten Staende bleibt
-- damit lesbar. Ausgewertet wird ueber MAX(Version).
INSERT INTO SchemaVersion
VALUES (2, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
