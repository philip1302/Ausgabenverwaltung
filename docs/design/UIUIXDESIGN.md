# UI/UX-Redesign — Ausgabenverwaltung

Dieses Dokument ist der verbindliche Entwurf für die komplette Oberfläche.
Es ist bewusst **nur ein Entwurf** — nichts hier ist bereits Code. Ziel ist,
dass eine spätere Umsetzungsrunde jede Entscheidung hier nachvollziehen und
1:1 in Avalonia übertragen kann, ohne selbst nochmal Designfragen klären zu
müssen.

Alle Mockups liegen als PNG unter [`mockups/`](mockups/), ihre HTML/CSS-
Quellen (echtes, im Browser gerendertes Markup, keine Zeichnung) unter
[`mockup-src/`](mockup-src/) — `tokens.css` und `components.css` dort sind
die Werte, die in diesem Dokument stehen, im Original.

---

## 1. Warum dieses Redesign

Die App ist funktional bereits vollständig: Erfassen, Offene Posten, Report,
Ausgabenliste, Verwaltung mit fünf Unterbereichen. Visuell ist sie aber ein
reines Werkzeug geblieben — schwarzweiße `FluentTheme`-Defaults, eine
textbasierte Sidebar ohne Icons, keine Startseite, hart codierte helle
Bannerfarben (`#FFE0E0`, `#DFF5DF`, `#FFF4CE` …), die im Dunkelmodus
brechen würden, 2px-Ecken, keine einheitliche Abstands- oder Farbsystematik.

### Recherchegrundlage

- **Sidebar-Navigation**: 240–300px Breite expandiert, 48–64px eingeklappt,
  Icon **und** Label kombiniert (nicht Icon allein), klare aktiv/hover-
  Zustände, Sidebar bleibt fixiert sichtbar.
  [ALF Design Group – Sidebar Design for Web Apps](https://www.alfdesigngroup.com/post/improve-your-sidebar-design-for-web-apps),
  [UX Planet – Best UX Practices for Designing a Sidebar](https://uxplanet.org/best-ux-practices-for-designing-a-sidebar-9174ee0ecaa2)
- **Finanz-App-Dashboards**: Monarch Money zeigt Nettovermögen und Budget
  gleichzeitig auf einer anpassbaren Startseite; Copilot setzt auf schnelle
  Übersicht "auf einen Blick"; YNAB bleibt funktional, aber kahl. Für eine
  Zwei-Personen-Haushaltskasse ist die Mitte davon richtig: eine Startseite
  mit KPI-Kacheln und Trend statt eines leeren Formulars als erste Seite.
  [EarnifyHub – YNAB vs Monarch vs Copilot 2026](https://earnifyhub.com/finance-money/ynab-vs-monarch-vs-copilot-2026)
- **Abstands-/Typografie-System**: Fluent 2 (dieselbe Familie wie das
  bereits verwendete `FluentTheme`) arbeitet mit einer 4px-Abstandsrampe
  (4/8/12/16/20/24/32) und tokenisierten statt hart codierten Werten — genau
  das Muster, das `anzeige:Breite`/`Raster.Spalten` im Code bereits verfolgt,
  hier auf Farbe und Abstand ausgeweitet.
  [Fluent 2 Design System – Layout](https://fluent2.microsoft.design/layout),
  [Fluent 2 Design System – Typography](https://fluent2.microsoft.design/typography)
- **Kontrast/Dark Mode**: WCAG AA verlangt 4,5:1 für Fließtext und 3:1 für
  große Schrift bzw. UI-Komponenten wie Feldrahmen — **unabhängig vom
  Farbschema**, Hell und Dunkel müssen das je eigenständig einhalten.
  Ein dunkler Hintergrund sollte ein gedämpftes `#121212` statt reinem
  Schwarz sein, um Halation (Leuchteffekt um hellen Text) zu vermeiden.
  [AllAccessible – Color Contrast Accessibility: WCAG 2025 Guide](https://www.allaccessible.org/blog/color-contrast-accessibility-wcag-guide-2025),
  [DubBot – Dark Mode: Best Practices for Accessibility](https://dubbot.com/dubblog/2023/dark-mode-a11y.html)

### Leitsätze für dieses Redesign

1. **Text und Icon zusammen, nie Icon allein** — jeder Navigationspunkt
   trägt beides, damit nichts erraten werden muss.
2. **Farbe ergänzt, ersetzt nie** — gilt unverändert weiter für
   Ausgabe/Einnahme (`+`/`-` bleibt immer vor dem Betrag) und wird auf alle
   neuen Status-Elemente ausgeweitet.
3. **Ein 4px-Raster für jeden Abstand** — keine Zufallswerte wie bisher
   (`Margin="0,0,0,12"` neben `Margin="0,8,0,0"` ohne erkennbares System).
4. **Jede Ansicht funktioniert bei 200% Schrift** — das bestehende
   Skalierungssystem (`Anzeige/Skalierung.cs`) ist eine harte Vorgabe, kein
   Detail, das später nachgezogen wird.
5. **Hell und Dunkel sind gleichberechtigt**, nicht Hell + nachträglicher
   Best-Effort-Dunkelmodus.
6. **Die Startseite ersetzt das leere Formular als erste Seite** — der erste
   Eindruck ist ein Überblick, nicht eine Aufforderung zur Eingabe.

---

## 2. Designsystem

Vollständige Referenzseite: **[`mockups/00-designsystem.png`](mockups/00-designsystem.png)**.
Alle Werte auch maschinenlesbar in [`mockup-src/tokens.css`](mockup-src/tokens.css).

### 2.1 Farben — Hell

| Token | Hex | Verwendung | Kontrast |
|---|---|---|---|
| `--canvas` | `#F7F8FA` | Hintergrund des Inhaltsbereichs | — |
| `--surface` | `#FFFFFF` | Karten, Sidebar, Tabellenkopf | — |
| `--text-primary` | `#1D2433` | Fließtext auf Surface | 15,5:1 |
| `--text-secondary` | `#5B6472` | Nebentext, Spalten-Untertitel | 5,98:1 |
| `--text-tertiary` | `#6B7280` | Kleinste Hilfstexte (ersetzt `Foreground="Gray"`) | 4,83:1 |
| `--border-subtle` | `#E4E7EC` | Trennlinien, rein dekorativ | — |
| `--border-strong` | `#7C8593` | Feld-/Buttonrahmen | 3,73:1 |
| `--accent` | `#2F5FD1` | Primärer Button, aktiver Navigationspunkt | 5,71:1 |
| `--accent-hover` | `#244DB0` | Hover/Pressed-Zustand des Akzents | 7,60:1 |
| `--accent-subtle-bg` | `#EAF0FE` | Fläche des aktiven Navigationspunkts | — |
| `--expense-text` | `#A03A30` | Ausgaben-Vorzeichen (**unverändert** aus `AusgabeFarbe`) | 6,68:1 |
| `--income-text` | `#187A40` | Einnahmen-Vorzeichen (**korrigiert**, siehe unten) | 5,39:1 |

**Fund während der Recherche:** Die bestehende `EinnahmeFarbe` in
`App.axaml` (`#66BB6A`) erreicht auf Weiß nur **2,36:1** und verfehlt WCAG AA
(4,5:1) damit deutlich — dieses Grün war für einen Signalpunkt gedacht,
wird aber als Text auf Beträgen verwendet. Das Redesign ersetzt sie durch
`#187A40` (5,39:1). Das vorangestellte `+`-Zeichen aus
`EuroText.FormatSigned` bleibt der eigentliche, farbunabhängige Träger der
Bedeutung (Regel 10) — der Fund ist trotzdem real und sollte in der
Umsetzung mitgezogen werden, unabhängig vom Rest des Redesigns.

Statusfarben (ersetzen die bisher **pro View einzeln** hart codierten
Banner-Hexwerte):

| Rolle | Text | Fläche | Rahmen | Kontrast |
|---|---|---|---|---|
| Erfolg | `#067647` | `#ECFDF3` | `#ABEFC6` | 5,40:1 |
| Warnung | `#B54708` | `#FFFAEB` | `#FEDF89` | 5,20:1 |
| Fehler | `#B42318` | `#FEF3F2` | `#FECDCA` | 6,05:1 |
| Hinweis | `#175CD3` | `#EFF8FF` | `#B2DDFF` | 5,57:1 |

Diese vier Paare ersetzen alle Fundstellen wie
`Background="#FFE0E0" BorderBrush="#D08080"` in `ErfassenView.axaml`,
`OffenePostenView.axaml`, `MainWindow.axaml` und `DarstellungView.axaml`
durch benannte, in Hell **und** Dunkel geprüfte Ressourcen statt Kopien des
gleichen Rottons mit leicht unterschiedlichen Werten.

### 2.2 Farben — Dunkel

| Token | Hex | Kontrast (auf `--surface`) |
|---|---|---|
| `--canvas` | `#121212` | — |
| `--surface` | `#1B1D22` | — |
| `--text-primary` | `#EAECEF` | 14,3:1 |
| `--text-secondary` | `#A6ADBB` | 7,48:1 |
| `--text-tertiary` | `#7A8290` | 4,35:1 |
| `--border-strong` | `#7C8593` | 4,52:1 |
| `--accent` | `#8AB0FF` | 7,82:1 |
| `--expense-text` | `#F1A79E` | 8,64:1 |
| `--income-text` | `#4ADE80` | 9,68:1 |

Auffällig: **`--border-strong` (`#7C8593`) ist ein einziges Token für Hell
und Dunkel** — es erreicht auf Weiß 3,73:1 und auf der dunklen Fläche
4,52:1, beides über der 3:1-Schwelle für UI-Komponenten. Ein Wert für Feld-
und Buttonrahmen in beiden Themes, keine zwei Paletten zu pflegen.

Hintergrund `#121212` statt reinem Schwarz vermeidet Halation (Leuchteffekt
um hellen Text herum, besonders bei Astigmatismus störend).

### 2.3 Typografie

Die drei bestehenden Stufen aus `App.axaml`
(`SchriftKlein`/`SchriftNormal`/`SchriftUeberschrift`, 11/14/16px bei
Skalierung "Normal") bleiben **unverändert** — sie sind an vielen Stellen
im Code verankert (Regel 9) und werden hier nur um eine vierte Stufe
ergänzt:

| Stufe | Größe (Normal) | Verwendung |
|---|---|---|
| Klein | 11px | Feldbeschriftungen, Tabellen-Spaltenköpfe |
| Normal | 14px | Fließtext, Tabellenzellen, Eingabefelder |
| Überschrift | 16px | Kartentitel, Dialogüberschriften |
| **Seitentitel** (neu) | 26px | Titel im Kopf jedes Bereichs — bisher hatte kein Screen einen |

Zusätzlich eine reine Anzeigegröße für KPI-Zahlen (30px), ohne eigene
Bedeutungsebene — sie folgt derselben Skalierung wie die anderen Stufen.
Schriftfamilie: **Inter**, bereits über `Avalonia.Fonts.Inter` eingebunden,
keine neue Abhängigkeit.

### 2.4 Abstände — 4px-Raster

`4 / 8 / 12 / 16 / 20 / 24 / 32 / 40` px. Ersetzt die bisher freihändig
gewählten Werte (`Margin="0,0,0,12"`, `Margin="0,8,0,0"`, `Padding="12"` …)
durch ein System mit sieben Stufen, an das sich jede neue Ansicht hält.

### 2.5 Radien

| Token | Wert | Verwendung |
|---|---|---|
| `--radius-sm` | 4px | Buttons, Eingabefelder, Zellen-Badges |
| `--radius-md` | 8px | Karten, Banner |
| `--radius-lg` | 12px | Dialoge, Flyouts |
| `--radius-full` | 999px | Farbpunkt (bleibt), Pills, Avatare |

Ersetzt die bisherige `CornerRadius="2"` (z. B. `DarstellungView.axaml`,
`Border.kasten`), die auf großen Flächen kantig statt weich wirkt.

### 2.6 Iconset

Ein selbst gezeichnetes, 20×20 großes Strich-Icon-Set, Strichstärke 1,75px,
runde Enden — keine externe Icon-Font, keine neue Abhängigkeit. Zehn Icons
für die zehn Navigationspunkte, alle im Übersichtsbild
[`mockups/00-designsystem.png`](mockups/00-designsystem.png) zu sehen und
als SVG-Pfade in [`mockup-src/gen-mockups.js`](mockup-src/) vorformuliert
(dort als Referenz für spätere `PathIcon`-Geometrien in Avalonia gedacht,
nicht selbst Teil der Auslieferung).

| Bereich | Icon-Idee |
|---|---|
| Startseite | Haus |
| Erfassen | Kreis mit Plus |
| Offene Posten | Klemmbrett mit Haken |
| Report | Balkendiagramm |
| Ausgabenliste | Liste mit Aufzählungspunkten |
| Verwaltung (Gruppe) | Zahnrad |
| Kategorien | Preisschild |
| Personen | Zwei Personen |
| Wiederkehrende Ausgaben | Kreispfeile |
| Datensicherung | Schild mit Haken |
| Darstellung | Schieberegler |

### 2.7 Komponenten

- **Button-Hierarchie**: `primary` (gefüllt, Akzentfarbe — genau eine
  Aktion pro Ansicht), `secondary` (Rahmen, `--border-strong`),
  `ghost` (nur Text, für zurückhaltende Aktionen wie "Filter
  zurücksetzen"), `danger` (für Löschen-Aktionen, roter Text/Rahmen, nie
  gefüllt — ein gefüllter roter Button wäre ein zu großes Gewicht für eine
  Aktion, die ohnehin meist hinter einer Bestätigung steht).
- **KPI-Kachel** (neu): Label mit kleinem Icon, große Zahl, kleine
  Zusatzzeile. Vier davon bilden die Kopfzeile der Startseite.
- **Status-Banner**: vereinheitlicht Info/Erfolg/Warnung/Fehler (siehe
  2.1), ersetzt die bisher wiederholt einzeln gebauten `Border`-Blöcke.
- **Leerer Zustand**: Icon + Text + Aktion statt des bisherigen grauen,
  kursiven Einzeilers ("Keine offenen Posten.", "Keine Ausgaben für diesen
  Filter.") — konsistent mit der übrigen Bildsprache.
- **Filterleiste**: eigene Karte mit `--border-subtle`-Rahmen statt der
  bisherigen reinen `Border BorderThickness="0,0,0,1"`-Trennlinie —
  grenzt den Filterbereich klarer vom Inhalt ab.
- **Tabellenzeile**: Hover-Zustand mit `--accent-subtle-bg` (bisher keine
  Rückmeldung beim Überfahren außer bei anklickbaren Report-Zellen).

---

## 3. Navigation neu gedacht

**Bleibt links** (`mockups/01-startseite.png` bis `10-*.png` zeigen sie
durchgängig): Konvention bei Desktop-Anwendungen (Windows Einstellungen,
VS Code, jede Fluent-Referenzanwendung), kein Grund für einen Bruch mit
dem, was Nutzer aus jeder anderen Windows-App kennen. Neu ist, **wie** sie
aussieht und was sie zeigt:

- **Icon + Label** statt reinem Text (siehe 2.6).
- **Klarer aktiver Zustand**: 3px-Balken links am Eintrag plus dezente
  Akzentfläche, statt der bisherigen reinen ListBox-Selektionsfarbe des
  Themes.
- **Gruppiert in vier Abschnitte** statt einer flachen Liste von fünf
  Einträgen:
  - *(ohne Kopfzeile)* Startseite
  - **Erfassen & verwalten**: Erfassen, Offene Posten (mit Zähler-Badge für
    offene Anzahl — bisher nirgends auf einen Blick sichtbar, nur nach dem
    Wechsel in den Bereich)
  - **Auswertung**: Report, Ausgabenliste
  - **Einstellungen**: Verwaltung (aufklappbar)
- **`Verwaltung` wird von einem `TabControl` zu einer aufklappbaren
  Sidebar-Gruppe** mit den fünf bisherigen Tabs als eigene Unterpunkte
  (Kategorien, Personen, Wiederkehrende Ausgaben, Datensicherung,
  Darstellung). Kürzerer Klickweg (ein Klick statt "Verwaltung öffnen, dann
  Tab wählen"), gleicher Datenzugriff über dieselben Bereichs-ViewModels.
  Die Tab-Leiste bleibt zusätzlich **innerhalb** der Verwaltungsseiten
  erhalten (siehe Mockups 06–10) — sie dient dort als Kontext-Umschalter
  zwischen den fünf Unterseiten, nicht mehr als einziger Navigationsweg
  dorthin.
- **Breite**: 240px expandiert, 64px eingeklappt (nur Icons) — beide Werte
  über `{anzeige:Breite}` skaliert wie jede andere feste Breite im Code,
  wachsen also mit der Schriftgröße mit.
- **Fußzeile der Sidebar**: zeigt kompakt Thema und Schriftstufe
  ("Hell · Normal") — Orientierung ohne erst in die Verwaltung wechseln zu
  müssen.

---

## 4. Neue Startseite

Bisher landet die App direkt auf "Erfassen" — ein leeres Formular als
erster Eindruck, ohne jede Rückmeldung, wie der Monat bisher aussieht.
Mockup: **[`mockups/01-startseite.png`](mockups/01-startseite.png)**.

Aufbau von oben nach unten:

1. **Kopfzeile**: "Guten Tag" + aktueller Monat, rechts der einzige primäre
   Button der Seite: "Ausgabe erfassen" (springt zu Erfassen).
2. **App-weite Hinweisbänder** (Sicherungsfehler, automatisch erzeugte
   wiederkehrende Buchungen) bleiben **oben andockbar über der ganzen App**
   wie bisher — nur im neuen Banner-Design (siehe 2.1/2.7). Sie erscheinen
   unabhängig davon, welcher Bereich gerade offen ist, nicht nur auf der
   Startseite.
3. **Vier KPI-Kacheln**: Ausgaben diesen Monat, Einnahmen diesen Monat,
   offene Posten (Betrag + "von wem"), nächste fällige wiederkehrende
   Buchung. Alle vier greifen auf bereits vorhandene Core-Abfragen zurück
   (Report-Aggregation, `OffenePostenViewModel`-Logik,
   `RecurringExpenseScheduler`) — keine neue Fachlogik, nur eine neue
   Zusammenstellung.
4. **Netto-Trend der letzten 6 Monate**: einfache Linie, Einnahmen minus
   Ausgaben der eigenen Buchungen je Monat — dieselbe Aggregation wie im
   Report, nur ohne Kreuztabelle.
5. **Letzte Buchungen**: identisches Muster zur bestehenden Liste in
   `ErfassenViewModel.LetzteAusgaben`, hier als eigene Karte.

---

## 5. Screen-für-Screen

Jeder Abschnitt: Zweck, was sich ändert, Mockup.

### 5.1 Erfassen — [`mockups/02-erfassen.html`](mockup-src/02-erfassen.html) · [PNG](mockups/02-erfassen.png)

Zweck unverändert: schnellstmöglich eine Buchung erfassen. Änderungen:
zweispaltiges Layout (Formular links in fester Kartenbreite, "Letzte
Buchungen" rechts statt darunter — beide gleichzeitig sichtbar ohne
Scrollen), Feldbeschriftungen als kleine Versalien über dem Feld statt
`TextBlock` + `TextBox` untereinander ohne visuellen Zusammenhang,
Erfolgs-/Fehlermeldung im neuen Banner-Stil. Die Betrag-Rückfrage bei
ungewöhnlichem Datum bleibt inhaltlich exakt wie heute (Regel: kein
Verbot, nur Rückfrage), nur im neuen Banner-Design.

### 5.2 Offene Posten — [PNG](mockups/03-offene-posten.png)

Gruppierung nach Person bleibt (Regel-relevant: `SettledDate IS NULL`
zusammen mit fremdem Zahler). Neu: Gesamtsumme prominent oben rechts statt
unten in der Werkzeugleiste, beglichene Zeilen deutlicher gedämpft
(Opacity statt nur Graufärbung), Aktionen als kleine Sekundär-Buttons statt
Text-Links.

### 5.3 Report — [PNG](mockups/04-report.png)

Die Kreuztabelle, ihr aufklappbarer Kategoriebaum und die anklickbaren
Zellen (öffnen die Einzelbuchungen im Overlay) bleiben strukturell exakt
wie heute — das ist die mit Abstand komplexeste Ansicht der App und
funktioniert. Neu: Filterleiste als eigene Karte, Schnellwahl-Buttons als
echte Button-Gruppe mit sichtbarem aktivem Zustand (heute nicht erkennbar,
welcher Zeitraum aktiv gewählt ist), Tabellenkopf mit `sticky`-Verhalten
optisch deutlicher abgesetzt.

### 5.4 Ausgabenliste — [PNG](mockups/05-ausgabenliste.png)

Virtualisierte Volltabelle bleibt. Neu: Filterleiste als Karte über der
Tabelle (heute keine eigene Filterleiste im View sichtbar dokumentiert,
wird hier ergänzt), Zeilen-Hover-Zustand, Kategorie immer mit Farbpunkt
+ Name (Regel 10).

### 5.5 Verwaltung ▸ Kategorien — [PNG](mockups/06-verwaltung-kategorien.png)

Baumstruktur mit Einzug bleibt. Aktionen (Farbe/Umbenennen/Archivieren)
als Icons/Text-Buttons in der Zeile statt Kontextmenü — sie werden häufig
genug gebraucht, um sichtbar zu sein statt versteckt. Der Hinweis zu
Löschen/Zusammenführen/automatischer Sicherung (Regel 8) bleibt als
Info-Banner direkt unter der Liste stehen, nicht nur im Bearbeiten-Dialog.

### 5.6 Verwaltung ▸ Personen — [PNG](mockups/07-verwaltung-personen.png)

Tabelle mit Ausgaben-/Offene-Posten-Summen je Person bleibt inhaltlich
gleich (Personen werden nie gelöscht, nur archiviert — Regel 8). Archivierte
Zeilen gedämpft mit "Wiederherstellen" statt "Löschen".

### 5.7 Verwaltung ▸ Wiederkehrende Ausgaben — [PNG](mockups/08-verwaltung-vorlagen.png)

Tabellenspalten unverändert (Titel, Kategorie, Betrag, Rhythmus, Zahler,
nächste Fälligkeit, Status). Status jetzt als farbiges Badge
(Aktiv/Pausiert) statt Text in eigener Spalte — auf einen Blick erkennbar,
welche Vorlagen gerade wirken.

### 5.8 Verwaltung ▸ Datensicherung — [PNG](mockups/09-verwaltung-datensicherung.png)

Status der externen Sicherung als Banner ganz oben (grün wenn aktuell,
sonst Warnung) statt als separate Textzeile — die wichtigste Information
der Seite ("bin ich gerade abgesichert?") ist jetzt die erste, die
auffällt.

### 5.9 Verwaltung ▸ Darstellung — [PNG](mockups/10-verwaltung-darstellung.png)

Bisher die einzige "Einstellungen"-Seite der App, aber nur mit
Schriftgrößenwahl. **Neu ergänzt: ein Thema-Umschalter (Hell/Dunkel/
System)** — heute gibt es zwar `RequestedThemeVariant="Default"` (folgt dem
System) im Code, aber keine Möglichkeit, das bewusst zu wählen. Die
Schriftgrößen-Auswahl bleibt strukturell exakt wie heute (vier
Radio-Buttons mit Live-Vorschau in der jeweiligen Größe).

---

## 6. Barrierefreiheit-Checkliste

- [x] Jedes Textfarben/Hintergrund-Paar aus 2.1/2.2 real berechnet
      (WCAG-Relativluminanz-Formel), nicht geschätzt — mindestens 4,5:1 für
      Fließtext, mindestens 3:1 für große Schrift und interaktive
      UI-Ränder, **in Hell und Dunkel unabhängig voneinander geprüft**.
- [x] Bestehender Fund korrigiert: `EinnahmeFarbe` (`#66BB6A`, 2,36:1) →
      `#187A40` (5,39:1).
- [x] Farbe ist nirgends alleiniger Bedeutungsträger: Ausgabe/Einnahme
      tragen weiterhin `-`/`+` vor dem Betrag, Status-Badges tragen
      weiterhin Text, nicht nur Farbe.
- [x] Jeder Screen wurde gedanklich bei 200% Schriftgröße durchgespielt
      (Skalierungsprobe in `mockups/00-designsystem.png`, unterster
      Abschnitt) — feste Breiten kommen ausschließlich aus
      `{anzeige:Breite}`/`Raster.Spalten`, nie als literale Pixelzahl.
- [x] Sidebar bleibt per Tastatur bedienbar (ListBox-Grundverhalten bleibt
      erhalten, nur die visuelle Darstellung ändert sich).
- [x] Icons sind immer von Text begleitet — kein Icon muss zum Verständnis
      erraten werden.

---

## 7. Was das für den Code bedeutet (Ausblick, keine Umsetzung)

Nicht Teil dieses Schritts, aber die logische Übergabe an eine spätere
Umsetzungsrunde:

- Neue `DynamicResource`-Ressourcen in `App.axaml`: `SchriftSeitentitel`
  (analog zu den bestehenden drei Stufen, inklusive Eintrag in
  `Anzeige/Skalierung.cs`/`FontSizes`), Status-Banner-Brushes je Theme,
  `AccentFarbe`/`AccentFarbeHover`.
- `App.axaml` bekommt `ThemeDictionaries` für Hell/Dunkel statt der
  bisherigen, immer hellen Hex-Werte in den einzelnen Views.
- `Views/MainWindow.axaml` referenziert noch `Assets/avalonia-logo.ico`
  (Platzhalter aus dem Projekt-Template) statt des vorhandenen
  `Assets/AppIcon.ico` — unabhängig vom Redesign ein Fund, der beim
  nächsten Kontakt mit dieser Datei mitgezogen werden sollte.
- `VerwaltungViewModel`/`VerwaltungView` wird von einem reinen
  `TabControl`-Container zu einer Struktur, die auch von der Sidebar aus
  direkt in einen der fünf Unterbereiche springen kann (die Tab-Leiste
  bleibt als Kontext-Umschalter erhalten, siehe 3).
- Ein neues `StartseiteViewModel` mit Lesezugriffen auf vorhandene
  Aggregationen (kein neuer Schreibpfad, keine neue Fachlogik).
- Ein Theme-Umschalter (Hell/Dunkel/System) in `DarstellungViewModel`,
  der `RequestedThemeVariant` zur Laufzeit setzt — analog zum bestehenden
  Muster von `Skalierung.Setze(...)`.
