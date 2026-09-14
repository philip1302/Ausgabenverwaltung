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
   Historie nicht ruckwirkend veraendern. Ausgenommen ist die
   ausdruecklich angehakte, einmalige Uebertragung beim Speichern
   einer Vorlage — sie ist eine Anwenderaktion, keine Referenz, und
   laesst Datum und Beglichen-Status unberuehrt.

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

11. **Das Protokoll ist teilbar.** `AppLog` schreibt eine Datei je
    Tag nach `%APPDATA%\Ausgabenverwaltung\Logs\`, 30 Tage werden
    behalten. Hinein kommen IDs, Anzahlen, Versionen, Dateinamen und
    Pfade — **keine Betraege, Bemerkungen oder Personennamen**. Jedes
    protokollierte Ereignis bekommt seinen Text in `LogEvents`, damit
    an einer Stelle nachlesbar bleibt, was das Protokoll preisgibt.
    Die Protokollierung wirft nie; schlaegt das Schreiben fehl, wird
    es stillschweigend uebersprungen.

12. **Keine rohe Ausnahme erreicht den Anwender.** Jeder Text kommt
    aus Core (`Errors/FileErrorText`, `Errors/DatabaseErrorText`,
    `Startup/StartupFailureText`, `Errors/UnexpectedErrorText`) und
    sagt drei Dinge: was passiert ist, was das fuer die Daten
    bedeutet, was der Anwender tun kann. Ausnahmenamen, Fehlernummern
    und Aufrufstapel gehoeren ausschliesslich in den aufklappbaren
    Bereich und ins Protokoll. Eine Meldung, die nur "Ein Fehler ist
    aufgetreten" sagt, gibt es nicht — `MeldungsGrundsaetzeTests`
    prueft das gegen alle Texte.

13. **Ein Schreibfehler raeumt nie ein Formular.** Schreibende
    Zugriffe laufen ueber `ViewModels/Schreibvorgang`; erst wenn er
    `null` liefert, darf der Aufrufer Felder leeren, Dialoge
    schliessen oder Listen neu laden. Feldfehler erscheinen am Feld,
    nicht als Dialog.

14. **Jedes ViewModel mit einer Buchungsliste registriert sich auf
    `BuchungenGeaendertNachricht`.** Wer Ausgaben/Einnahmen anzeigt
    (`ViewModels/BuchungenGeaendertNachricht.cs`), bekommt ein
    `CommunityToolkit.Mvvm.Messaging.IMessenger` per DI-Konstruktor
    injiziert (registriert in `App.axaml.cs`) und meldet sich damit im
    Konstruktor per `Register<TSelbst, BuchungenGeaendertNachricht>` auf
    seine eigene Lademethode an. Jede Stelle, die Buchungsdaten
    schreibt, sendet ueber denselben `IMessenger` nach einem
    erfolgreichen Schreibvorgang dieselbe Nachricht statt (nur) die
    eigene Liste neu zu laden — sonst zeigt die neue Ansicht veraltete
    Betraege oder Farben, bis der Anwender zufaellig dorthin navigiert.
    Bewusst **nicht** `WeakReferenceMessenger.Default` verwenden: der
    ist prozessweit geteilt, in Tests braucht jeder Testfall eine eigene
    `IMessenger`-Instanz, sonst wirken Registrierungen frueherer Tests
    in spaetere hinein.

15. **`CHANGELOG.md` wird bei JEDEM Commit gepflegt, nicht erst beim
    Veroeffentlichen.** Zwei Pflichten, die zusammengehoeren:

    **a) Jeder Commit, der etwas fuer den Anwender aendert, traegt es im
    selben Commit ein** — unter der obersten Ueberschrift
    `## Unveroeffentlicht`. Gibt es sie noch nicht, wird sie angelegt:

    ```
    ## Unveroeffentlicht

    ### Gruppe (optional)

    - Ganze Saetze, jeder eine Aenderung.
    ```

    Nachtraeglich aus dem Log zusammengesucht wird es sonst nie richtig:
    was eine Aenderung fuer den Anwender bedeutet, weiss man beim
    Schreiben und nicht drei Wochen spaeter. Ein Commit ohne Wirkung auf
    den Anwender (Umbau, Test, Doku, Werkzeug) traegt nichts ein — das
    ist der Normalfall und kein Versaeumnis.

    **b) Beim Anheben von `<Version>` wird `## Unveroeffentlicht` zur
    Fassung** — im selben Commit, ohne den Text neu zu erfinden:

    ```
    ## 1.5.0 — 12.08.2026
    ```

    Steht dann nichts darunter, ist die Anhebung fragwuerdig: eine neue
    Fassung, die dem Anwender nichts zu sagen hat, braucht er auch nicht.

    Diese Datei ist kein Entwicklertagebuch, sondern **der Text, den der
    Anwender zu sehen bekommt**: sie wird in die Baugruppe eingebettet
    (`Core/Updates/Changelog.cs`) und nach einer Aktualisierung einmalig
    als Seite "Was ist neu" gezeigt. Deshalb:
    - Nur, was jemand beim **Benutzen** merkt. Umbauten, Tests,
      Aufraeumarbeiten und Abhaengigkeiten kommen nicht vor — wen sie
      betreffen, der liest das Protokoll der Aenderungen.
    - **Kein Fachgesimpel**: keine Klassen-, Datei- oder Feldnamen, kein
      "ViewModel", "Repository", "SQL", "Commit", "Migration".
      `ChangelogTests` prueft eine Liste solcher Woerter mit.
    - Ganze Saetze mit echten Umlauten, aus Sicht des Anwenders ("Das
      Betragsfeld rechnet jetzt"), nicht aus Sicht des Codes.
    - Der Nutzen gehoert dazu, wo er nicht offensichtlich ist — ein Satz,
      warum es das jetzt gibt, ist mehr wert als drei Aufzaehlungspunkte.

    `## Unveroeffentlicht` traegt keine lesbare Versionsnummer und wird
    von der Anwendung deshalb uebergangen — ein noch nicht
    veroeffentlichter Eintrag kann niemandem angezeigt werden. Wird die
    Version angehoben, ohne die Ueberschrift umzubenennen, faellt das in
    `ChangelogTests` auf; die Seite bliebe sonst stillschweigend aus. Der
    Veroeffentlichungs-Workflow nimmt denselben Abschnitt als
    Release-Text (siehe PUBLISH.md) — er wird also genau einmal
    geschrieben.

## Stil
- Kommentare auf Deutsch, Bezeichner auf Englisch
- Ausfuehrliche Kommentare bei allem, was nicht offensichtlich ist
- Zeilen kurz halten, kein horizontales Scrollen
- SQL sichtbar und lesbar — keine Query-Generierung verstecken

## Build
- Zur Pruefung nur `dotnet build` und `dotnet test` ausfuehren.
  **Niemals `dotnet run` zur Pruefung** - die Avalonia-App blockiert das
  Terminal, weil sie auf die GUI-Ereignisschleife wartet.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
