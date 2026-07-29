# Ausgabenverwaltung

Desktop-App zur Erfassung privater Ausgaben mit hierarchischen
Kategorien, wiederkehrenden Buchungen und Auswertungen.

## Stack
- .NET 10, C#
- Avalonia (MVVM) fuer die Oberflaeche
- SQLite via Dapper — KEIN Entity Framework
- xUnit fuer Tests

## Projektstruktur
- `Ausgabenverwaltung.Core`  — Domaene, Datenzugriff, Reportlogik.
                               Enthaelt KEINEN UI-Code.
- `Ausgabenverwaltung.Tests` — Tests gegen Core
- `Ausgabenverwaltung.App`   — Avalonia-Oberflaeche
- `docs/schema_v1.sql`       — massgebliches DB-Schema

## Harte Regeln

1. **Geld ist IMMER `long` in Cent.** Niemals `double`, `float`
   oder `decimal` in der Datenbank. In C# an der Rechengrenze
   `decimal`, Umwandlung nur in einer zentralen Helper-Klasse.

2. **`PRAGMA foreign_keys = ON`** bei JEDER neuen Verbindung.
   SQLite prueft sonst keine Fremdschluessel.

3. **Datumsformat**: `'YYYY-MM-DD'` als TEXT, UTC-Zeitstempel
   als `'YYYY-MM-DDTHH:MM:SSZ'`. Nie lokalisierte Formate in
   der Datenbank.

4. **`SettledDate IS NULL` bedeutet nur in Kombination mit
   einem fremden Zahler "offen".** Bei eigenen Ausgaben bleibt
   das Feld leer und wird nie ausgewertet.

5. **Wiederkehrende Buchungen**: Vorkommen werden als
   `StartDate + n * Intervall` berechnet, NIE als
   `letztes Vorkommen + Intervall` (sonst Drift).
   Existiert der Ankertag im Zielmonat nicht, auf den letzten
   Tag des Monats kuerzen.

6. **Betraege werden beim Erzeugen aus der Vorlage KOPIERT**,
   nicht referenziert. Vorlagenaenderungen duerfen die
   Historie nicht ruckwirkend veraendern.

7. **Keine Geschaeftslogik in Code-Behind oder ViewModels.**
   Alles Pruefbare gehoert nach Core.

8. **Kategorien und Personen werden archiviert, nie geloescht.**
   Fremdschluessel stehen auf `ON DELETE RESTRICT`.

## Stil
- Kommentare auf Deutsch, Bezeichner auf Englisch
- Ausfuehrliche Kommentare bei allem, was nicht offensichtlich ist
- Zeilen kurz halten, kein horizontales Scrollen
- SQL sichtbar und lesbar — keine Query-Generierung verstecken

## Build