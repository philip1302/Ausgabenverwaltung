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
- `Ausgabenverwaltung`      — Avalonia-Oberflaeche
- `docs/schema_v2.sql`       — massgebliches DB-Schema (neue Datenbanken)
- `docs/migration_*.sql`     — je ein Schritt zwischen zwei Schema-Staenden;
                               `schema_v1.sql` bleibt unveraendert liegen

## Harte Regeln

1. **Geld ist IMMER `long` in Cent.** Niemals `double`, `float`
   oder `decimal` in der Datenbank. In C# an der Rechengrenze
   `decimal`, Umwandlung nur in einer zentralen Helper-Klasse
   (`Money`). Angezeigt wird ausschliesslich ueber `EuroText`, fest
   auf `de-DE`: `EuroText.Format` mit €-Zeichen ueberall, wo ein
   Betrag gelesen wird, `EuroText.Plain` ohne Zeichen in
   Eingabefeldern und im CSV-Export. Keine Formatierung in einzelnen
   Views oder ViewModels.

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

8. **Archivieren ist der Normalfall.** Fremdschluessel stehen auf
   `ON DELETE RESTRICT`.
   - **Personen** werden ausschliesslich archiviert, nie geloescht.
   - **Kategorien** duerfen geloescht werden, aber nur wenn sie
     vollstaendig unbenutzt sind: keine Ausgaben, keine
     Unterkategorien, keine verweisende Vorlage.
   - Eine benutzte Kategorie wird archiviert oder **zusammengefuehrt**:
     alle Ausgaben und Vorlagen wandern in EINER Transaktion zu einer
     Zielkategorie (nur Blattknoten), `ModifiedUtc` wird mitgezogen,
     danach faellt die leere Quelle weg. Eine Quelle mit
     Unterkategorien wird abgelehnt — kein rekursives Verschieben
     ganzer Aeste. Nicht rueckgaengig zu machen, deshalb vorher
     automatisch eine Sicherung.

9. **Keine festen Schriftgroessen und keine festen Pixelbreiten
   in den Ansichten.** Schriftgroessen kommen als
   `{DynamicResource SchriftKlein|SchriftNormal|SchriftUeberschrift}`,
   Breiten als `{anzeige:Breite 110}`, Spaltenraster als
   `anzeige:Raster.Spalten="..."`. Sonst waechst das Layout bei
   der eingestellten Schriftgroesse nicht mit und schneidet Text ab.

10. **Kategoriefarben nur aus `CategoryColorPalette`.** Eine
    Kategorie ohne eigene Farbe erbt die des naechsten Vorfahren
    (`CategoryColors`). Farbe ist immer ein Zusatz zum Text, nie
    sein Ersatz.

## Stil
- Kommentare auf Deutsch, Bezeichner auf Englisch
- Ausfuehrliche Kommentare bei allem, was nicht offensichtlich ist
- Zeilen kurz halten, kein horizontales Scrollen
- SQL sichtbar und lesbar — keine Query-Generierung verstecken

## Build
- Zur Pruefung nur `dotnet build` und `dotnet test` ausfuehren.
  **Niemals `dotnet run` zur Pruefung** - die Avalonia-App blockiert das
  Terminal, weil sie auf die GUI-Ereignisschleife wartet.