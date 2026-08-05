-- ============================================================
-- Migration Schema v2 -> v3
--
-- Zieht eine bestehende Datenbank der Version 2 auf Version 3.
-- Laeuft beim Programmstart vor allem anderen und erst,
-- nachdem eine Sicherung geschrieben wurde (siehe
-- StartupService). Ausgefuehrt wird sie in EINER Transaktion:
-- SQLite kann auch DDL zuruecknehmen, eine abgebrochene
-- Migration hinterlaesst also keinen halben Stand.
--
-- ALTER TABLE ... ADD COLUMN fasst die vorhandenen Zeilen nicht
-- an; alle bestehenden Personen bekommen zunaechst SortOrder = 0
-- und wuerden damit ununterscheidbar am Anfang landen. Die
-- folgende UPDATE-Anweisung vergibt stattdessen die bisherige
-- alphabetische Reihenfolge als Startwert - wer die Liste schon
-- kennt, findet sie beim ersten Start unveraendert vor. Ueber
-- eine korrelierte Unterabfrage statt eines Fensterfunktions-
-- RANK, weil der Name UNIQUE ist und es damit keine Gleichstaende
-- gibt, die eine Reihenfolge unter sich brauchen wuerden.
--
-- Neue Datenbanken entstehen nicht ueber diesen Weg, sondern
-- direkt aus docs/schema_v3.sql. Beide muessen zum selben
-- Ergebnis fuehren (siehe DatabaseMigratorTests).
-- ============================================================

ALTER TABLE Person ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;

UPDATE Person
SET SortOrder = (
    SELECT COUNT(*)
    FROM Person AS Andere
    WHERE Andere.Name < Person.Name
);

-- Fortschreiben statt Ueberschreiben: die Zeile der Version 2
-- bleibt stehen, die Historie der angewandten Staende bleibt
-- damit lesbar. Ausgewertet wird ueber MAX(Version).
INSERT INTO SchemaVersion
VALUES (3, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
