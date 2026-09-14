# Nächste Aufgaben

Arbeitsanweisung an mich selbst. Jeder Punkt ist so beschrieben, dass er
ohne Rückfrage begonnen werden kann.

**Aufruf:** „Arbeite die ersten N Punkte in der NächsteAufgaben.md ab."
Dann werden die Punkte **in der hier stehenden Reihenfolge** abgearbeitet,
jeder vollständig (Core → Tests → Oberfläche → `dotnet build` +
`dotnet test`), bevor der nächste beginnt.

Erledigte Punkte werden nicht gelöscht, sondern mit `[x]` markiert und
bekommen eine Zeile „Erledigt in: <Commit-Betreff>".

---

## Für jeden Punkt gilt

- **Prüfen ausschließlich mit `dotnet build` und `dotnet test`.**
  Niemals `dotnet run` — die Avalonia-App blockiert das Terminal.
- **Fachlogik nach Core**, ViewModel bleibt Verdrahtung + Anzeigetexte
  (Regel 7). Faustregel: alles, wofür sich ein Test schreiben lässt,
  gehört nach `Ausgabenverwaltung.Core`.
- **Jeder schreibende Zugriff läuft über `ViewModels/Schreibvorgang`**
  (Regel 13). Formular/Dialog wird erst geräumt, wenn `null` zurückkommt.
- **Nach erfolgreichem Schreiben von Buchungsdaten:
  `BuchungenGeaendertNachricht` über den injizierten `IMessenger`
  senden** (Regel 14). Nicht `WeakReferenceMessenger.Default`.
- **Beträge**: `long` in Cent, Anzeige nur über `EuroText.Format` /
  `EuroText.Plain` (Regel 1).
- **Keine festen Schriftgrößen, keine festen Pixelbreiten** in `.axaml`
  (Regel 9). Breiten über `{anzeige:Breite N}`, Raster über
  `anzeige:Raster.Spalten`, Schrift über `{DynamicResource Schrift…}`.
- **Neue Protokollereignisse bekommen ihren Text in
  `Core/Logging/LogEvents.cs`** und enthalten keine Beträge, Bemerkungen
  oder Personennamen (Regel 11).
- **Neue Fehlertexte** kommen aus Core (`Errors/*Text.cs`) und sagen drei
  Dinge: was passiert ist, was das für die Daten bedeutet, was der
  Anwender tun kann (Regel 12). `MeldungsGrundsaetzeTests` prüft das mit.
- **Stil**: Kommentare deutsch in ASCII-Umschrift (`ue`, `ae`, `oe`),
  Bezeichner englisch in Core / deutsch in ViewModels+Views, wie bisher.
  Anwendersichtbare Texte mit echten Umlauten.
- **Commit-Betreff** wie im bisherigen Log: deutscher Aussagesatz in
  ASCII-Umschrift, z. B. `Kacheln der Startseite fuehren in die
  gefilterte Ausgabenliste`.

---

## Batch 1 — Sprünge und Vorlagen-Kopplung (Punkte 1–5)

### [x] 1. Kacheln der Startseite sind anklickbar

**Erledigt in:** Kacheln und letzte Buchungen fuehren in die Ausgabenliste, Buchungen lassen sich duplizieren

**Ziel:** Jede KPI-Kachel führt dorthin, wo die Zahl herkommt.

**Zielverhalten**

| Kachel | Sprungziel | Filter |
|---|---|---|
| Ausgaben diesen Monat | Ausgabenliste | Zeitraum = laufender Monat, `MeineKosten` an, nur Ausgaben |
| Einnahmen diesen Monat | Ausgabenliste | Zeitraum = laufender Monat, nur Einnahmen, Status „beglichen" |
| Offene Posten | Bereich „Offene Posten" | — |
| Nächste Fälligkeit | Bereich „Wiederkehrende Ausgaben" | Zeile der Vorlage vorausgewählt |

**Vorgehen**

1. `Core/Reports/ReportFilter.cs`: Feld
   `public bool? IsIncome { get; init; }` ergänzen (NULL = keine
   Einschränkung). In `Core/Reports/ReportFilterSql.cs` als
   `AND e.IsIncome = @IsIncome` einhängen, nur wenn gesetzt.
   Kommentar dazu: leerer Wert schränkt nicht ein — dieselbe Regel wie
   bei allen Listenfiltern in dieser Klasse.
2. `ViewModels/AusgabenlisteViewModel.cs`: Sichtbare Umschaltung
   `[ObservableProperty] private bool _nurEinnahmen;` und
   `_nurAusgaben;` in die Filterleiste aufnehmen (zwei Häkchen, beide
   aus = alles — analog `StatusOffen`/`StatusBeglichen`). In
   `BaueFilter` auf `IsIncome` abbilden, in `FilterAuswahlLeeren`
   zurücksetzen.
3. Neue öffentliche Methode
   `ZeigeMonat(DateOnly monatsAnfang, bool nurEinnahmen, bool meineKosten)`
   nach dem Muster der vorhandenen `ZeigeZeitraum` (dort:
   `_ladenGesperrt` setzen, Filter leeren, Werte setzen, entsperren,
   `LadeDaten()`).
4. `ViewModels/StartseiteViewModel.cs`: vier neue
   `[RelayCommand]`-Methoden, die je ein Event auslösen —
   `AusgabenMonatAngefordert`, `EinnahmenMonatAngefordert`,
   `OffenePostenAngefordert`, `NaechsteFaelligkeitAngefordert(int VorlageId)`.
   Kein direkter Zugriff auf andere ViewModels: die Startseite kennt die
   Navigation nicht, das ist genau das Muster von
   `ZeitraumAngefordert`/`ErfassenAngefordert`.
   Für die vierte Kachel muss die Vorlagen-Id gemerkt werden —
   `AktualisiereNaechsteFaelligkeit` hält sie schon in `naechste.Vorlage`,
   also zusätzlich in ein privates Feld `_naechsteVorlageId` schreiben.
5. `ViewModels/MainViewModel.cs`: die vier Events im Konstruktor
   verdrahten, exakt nach dem Vorbild des vorhandenen
   `startseite.ZeitraumAngefordert`-Blocks. Für die Vorlage:
   `verwaltung.Vorlagen.WaehleVorlage(id)` (neue Methode im
   `VorlagenViewModel`, markiert die Zeile) und dann auf den
   Navigationseintrag „Wiederkehrende Ausgaben" wechseln.
6. `Views/StartseiteView.axaml`: die drei/vier `Border.kpi` in einen
   `Button` mit `Classes="kpi"` verwandeln oder das `Border` in einen
   Button einbetten. Neuer Style in `App.axaml` neben
   `Selector="Border.kpi"`: `Button.kpi` mit gleicher Optik plus
   `:pointerover`-Hervorhebung, damit die Klickbarkeit sichtbar ist.
   `ToolTip.Tip` je Kachel: „Zeigt die Buchungen dieses Monats" usw.
   `Cursor="Hand"` setzen.
   **Wichtig:** Ist die Kachel leer (`NaechsteFaelligkeitVorhanden` =
   false, „Keine offenen Posten"), muss der Button `IsEnabled="False"`
   sein — ein Sprung ins Nichts ist schlimmer als kein Sprung.

**Tests** (`Ausgabenverwaltung.Tests/ExpenseListQueryTests.cs`)
- `IsIncome = true` liefert nur Einnahmen, `false` nur Ausgaben,
  `null` alles.
- Kombination mit `Status`/`PayerScope` schränkt zusätzlich ein
  (UND-Verknüpfung), nicht ersetzend.

**Fertig, wenn:** Build und Tests grün, alle vier Kacheln haben ein
Kommando, leere Kacheln sind ausgegraut, und die Zahl in der Kachel
stimmt mit der Summe unter der Liste überein, in die sie springt.

---

### [x] 2. „Letzte Buchungen" führen zu genau dieser Buchung

**Erledigt in:** Kacheln und letzte Buchungen fuehren in die Ausgabenliste, Buchungen lassen sich duplizieren

**Ziel:** Ein Klick auf eine Zeile unter „Letzte Ausgaben" (Erfassen) und
„Letzte Buchungen" (Startseite) öffnet die Ausgabenliste, in der genau
diese eine Buchung steht.

**Vorgehen**

1. `Core/Reports/ReportFilter.cs`: Feld
   `public int? ExpenseId { get; init; }` ergänzen — Kommentar analog zum
   vorhandenen `RecurringExpenseId`: Einschränkung auf genau eine
   Buchung, für den Sprung aus den Übersichtslisten. NULL = keine
   Einschränkung. In `ReportFilterSql` als `AND e.Id = @ExpenseId`
   einhängen.
2. `ViewModels/LetzteAusgabeZeile.cs`: `public int Id { get; }` ergänzen
   und im Konstruktor aus `expense.Id` füllen (steht in
   `ExpenseOverview` bereits zur Verfügung).
3. `ViewModels/AusgabenlisteViewModel.cs`:
   - privates Feld `private int? _buchungFilterId;`
   - `[ObservableProperty] [NotifyPropertyChangedFor(nameof(BuchungFilterAktiv))] private string? _buchungFilterText;`
     plus `public bool BuchungFilterAktiv => BuchungFilterText is not null;`
   - `public void ZeigeEinzelneBuchung(int id, string beschreibung)` —
     nach dem Muster von `ZeigeVorlagenBuchungen`: alle übrigen Filter
     leeren, Zeitraum öffnen (`VonText`/`BisText` leer), `_vorlageFilterId`
     auf `null`, dann `_buchungFilterId = id` und
     `BuchungFilterText = $"Nur diese Buchung: {beschreibung}"`.
   - `[RelayCommand] private void BuchungFilterAufheben()` — hebt nur
     diesen Filter auf, wie das vorhandene Gegenstück zum Vorlagenfilter.
   - In `BaueFilter`: `ExpenseId = _buchungFilterId`.
   - In `FilterZuruecksetzen`: **beide** zurücksetzen
     (`_buchungFilterId = null; BuchungFilterText = null;`).
   - In `ZeigeVorlagenBuchungen` und `ZeigeZeitraum` ebenfalls
     zurücksetzen — sonst überlebt der Einzelfilter einen anderen Sprung
     und die Liste bleibt rätselhaft leer.
   - **Nach dem Löschen einer Buchung**: in `LoeschenBestaetigen` prüfen,
     ob `_buchungFilterId` in `_zuLoeschendeIds` steckt; wenn ja, den
     Filter mit aufheben. Sonst zeigt die Liste dauerhaft nichts an.

   **Abweichung von der Vorgabe, bewusst:** Der Filter ist *kein
   sichtbares Eingabefeld* in der Filterleiste — die Id tippt niemand
   ein. Er bekommt aber wie der Vorlagenfilter einen sichtbaren,
   wegklickbaren Hinweis („Nur diese Buchung: 07.08.2026 · 42,90 € ·
   Lebensmittel · Filter aufheben"). Ein Filter, der wirkt, ohne sich zu
   zeigen, ist der häufigste Grund für „meine Buchungen sind weg".
4. `ViewModels/StartseiteViewModel.cs` und
   `ViewModels/ErfassenViewModel.cs`: je ein
   `[RelayCommand] private void BuchungOeffnen(LetzteAusgabeZeile? zeile)`,
   das ein Event `BuchungAngefordert` mit Id und einer kurzen
   Beschreibung auslöst. Beschreibung = `VorText` + `BetragText` ohne die
   Trennpunkte; dafür in `LetzteAusgabeZeile` eine Eigenschaft
   `Beschreibung` ergänzen, damit die Zusammensetzung an einer Stelle
   steht.
5. `ViewModels/MainViewModel.cs`: beide Events verdrahten →
   `_ausgabenliste.ZeigeEinzelneBuchung(...)` + Wechsel auf den
   Navigationseintrag der Ausgabenliste.
6. `Views/StartseiteView.axaml` und `Views/ErfassenView.axaml`: die
   Zeilen-`DataTemplate`s klickbar machen (Button mit `Classes="ghost"`
   über die ganze Zeilenbreite, `HorizontalContentAlignment="Stretch"`,
   `Command`/`CommandParameter` an das jeweilige ViewModel binden — der
   Bindungspfad braucht `$parent[UserControl].DataContext`, weil der
   DataContext im Template die Zeile ist).
7. `Views/AusgabenlisteView.axaml`: den Hinweis-Chip analog zum
   vorhandenen Vorlagenfilter-Chip ergänzen (`IsVisible` an
   `BuchungFilterAktiv`, Aufheben-Knopf an `BuchungFilterAufhebenCommand`).

**Tests** (`ExpenseListQueryTests`)
- `ExpenseId` liefert genau eine Zeile.
- `ExpenseId` in Kombination mit einem Zeitraum, der die Buchung nicht
  enthält, liefert leer — dokumentiert, dass der Zeitraum beim Sprung
  geöffnet werden muss.

**Fertig, wenn:** Klick auf eine Zeile beider Listen landet auf genau
dieser Buchung, „Filter zurücksetzen" und das Löschen der Buchung heben
den Filter auf, und kein anderer Sprung schleppt ihn mit.

---

### [x] 3. Buchung duplizieren

**Erledigt in:** Kacheln und letzte Buchungen fuehren in die Ausgabenliste, Buchungen lassen sich duplizieren

**Ziel:** Aus einer bestehenden Zeile eine neue Buchung mit denselben
Werten und dem heutigen Datum anlegen.

**Vorgehen**

1. `ViewModels/AusgabenlisteViewModel.cs`:
   `[RelayCommand] private void Duplizieren(AusgabeZeile? zeile)`.
   - Werte aus `_expenseRepository.GetById(zeile.Id)` holen (nicht aus
     der Anzeigezeile — dort stehen formatierte Texte).
   - Neue Buchung über `Schreibvorgang.Versuche("Beim Duplizieren einer
     Ausgabe", () => _expenseRepository.Create(...))` mit
     `ExpenseDate = heute`, `SettledDate = null`,
     `RecurringExpenseId = null` (eine Kopie stammt aus keiner Vorlage),
     alle übrigen Felder übernommen.
   - Bei `null` (Erfolg): `_messenger.Send(new BuchungenGeaendertNachricht())`
     und ein Erfolgs-Band „Buchung dupliziert — Datum auf heute gesetzt."
   - Bei Fehler: Band mit dem Fehlertext, Liste unverändert (Regel 13).
2. `Views/AusgabenlisteView.axaml`: Knopf in der Aktionsspalte der Zeile,
   `Classes="ghost klein"`, Beschriftung „Duplizieren".
3. `Core/Logging/LogEvents.cs`: Ereignis `ExpenseDuplicated(int quelleId,
   int neueId)` — nur Ids, keine Beträge (Regel 11).

**Tests** (`ExpenseRepositoryTests`) — nur falls Core berührt wird; ist
hier reines ViewModel, dann genügt der bestehende Test von `Create`.

**Fertig, wenn:** Duplizieren erzeugt eine Zeile mit heutigem Datum,
gleichem Betrag/Kategorie/Zahler, ohne Vorlagenbezug und ohne
Beglichen-Datum, und alle Bereiche aktualisieren sich sofort.

---

### [x] 4. Vorlage löschen — mit den erzeugten Buchungen?

**Erledigt in:** Vorlagen lassen sich samt ihren Buchungen loeschen

**Ziel:** Beim Löschen einer Vorlage wird gefragt, was mit den daraus
erzeugten Buchungen geschehen soll.

**Ausgangslage:** `Expense.RecurringExpenseId` steht auf
`ON DELETE SET NULL` — heute bleiben die Buchungen stehen und verlieren
still ihre Herkunft. Das bleibt die **Voreinstellung**, weil es die
Historie erhält.

**Zielverhalten:** Der Bestätigungsdialog nennt die Anzahl der erzeugten
Buchungen und bietet zwei Wege:
- „Vorlage löschen, Buchungen behalten" (Voreinstellung, vorausgewählt)
- „Vorlage und alle N Buchungen löschen" (`Classes="danger"`)

Ist N = 0, entfällt die Auswahl und es bleibt beim heutigen Dialog.

**Vorgehen**

1. `Core/RecurringExpenses/RecurringExpenseRepository.cs`:
   - `public int CountGeneratedExpenses(int templateId)` — einfaches
     `SELECT COUNT(*) FROM Expense WHERE RecurringExpenseId = @Id`.
     (`GetGeneratedExpenseCounts()` gibt es schon für die Liste; hier
     genügt eine Zahl, keine Neuberechnung aller Vorlagen.)
   - `public void DeleteWithExpenses(int templateId)` — **eine**
     Transaktion: erst `DELETE FROM Expense WHERE RecurringExpenseId = @Id`,
     dann `DELETE FROM RecurringExpense WHERE Id = @Id`. Reihenfolge ist
     Pflicht, sonst greift `ON DELETE SET NULL` vorher und die Buchungen
     sind nicht mehr auffindbar. Sichtbares SQL, kein generiertes.
   - `PRAGMA foreign_keys = ON` kommt aus der Verbindung
     (`SqliteConnectionFactory`), hier nichts zusätzlich nötig — im Test
     aber prüfen, dass sie an ist.
2. **Vorher automatisch sichern.** Nicht rückgängig zu machen, also
   dieselbe Behandlung wie das Zusammenführen von Kategorien (Regel 8):
   `BackupService.RunNow(DateTime.Now)` vor dem Löschen, und bei
   gescheiterter Sicherung wird **nicht** gelöscht, sondern gemeldet.
3. `ViewModels/VorlagenViewModel.cs`:
   - In `Loeschen(VorlageZeile?)` zusätzlich die Anzahl ermitteln und in
     einer neuen Eigenschaft `LoeschenBuchungenAnzahl` ablegen, dazu
     `LoeschenFrageText` („Diese Vorlage hat bisher N Buchungen
     erzeugt."). Bei N = 0 bleibt der Text wie heute.
   - `[ObservableProperty] private bool _auchBuchungenLoeschen;` —
     Voreinstellung `false`, wird bei jedem neuen Löschversuch
     zurückgesetzt (in `Loeschen` explizit auf `false`, nicht darauf
     verlassen, dass es noch stimmt).
   - `LoeschenBestaetigen` verzweigt auf `Delete` bzw.
     `DeleteWithExpenses`, beides über `Schreibvorgang`.
   - Nach Erfolg mit gelöschten Buchungen:
     `BuchungenGeaendertNachricht` senden (Regel 14) — sonst zeigen
     Startseite und Ausgabenliste weiter die verschwundenen Beträge.
4. `Views/VorlagenView.axaml`: im vorhandenen Bestätigungsband die
   Anzahl-Zeile und ein `CheckBox` „Auch die N erzeugten Buchungen
   löschen" ergänzen. Ist die Box an, wechselt der Bestätigungsknopf auf
   `Classes="danger"` und die Beschriftung auf „Vorlage und N Buchungen
   löschen".
5. `Core/Logging/LogEvents.cs`: `TemplateDeletedWithExpenses(int
   vorlageId, int anzahl)`.

**Tests** (`RecurringExpenseRepositoryTests`)
- `DeleteWithExpenses` löscht Vorlage **und** genau deren Buchungen;
  handerfasste Buchungen (`RecurringExpenseId IS NULL`) bleiben stehen;
  Buchungen anderer Vorlagen bleiben stehen.
- `Delete` (alt) lässt die Buchungen stehen und setzt deren
  `RecurringExpenseId` auf NULL — Absicherung, dass sich das Verhalten
  nicht nebenbei ändert.
- Schlägt der zweite Schritt fehl, ist auch der erste zurückgerollt
  (Transaktion) — mit einer künstlich verletzten Bedingung prüfen.

**Fertig, wenn:** Beide Wege funktionieren, vorher wird gesichert, die
Zahl im Dialog stimmt, und alle Bereiche aktualisieren sich.

---

### [x] 5. Vorlagenänderung auf erzeugte Buchungen übertragen

**Erledigt in:** Vorlagenaenderungen lassen sich auf erzeugte Buchungen uebertragen

**Ziel:** Beim Speichern einer geänderten Vorlage kann der Anwender
**ausdrücklich** verlangen, dass die Änderung auch für die bereits
erzeugten Buchungen gilt.

> **Achtung — berührt Regel 6.**
> CLAUDE.md, Regel 6: „Beträge werden beim Erzeugen aus der Vorlage
> KOPIERT, nicht referenziert. Vorlagenänderungen dürfen die Historie
> nicht rückwirkend verändern."
>
> Diese Aufgabe hebt die Regel **nicht** auf. Das automatische Verhalten
> bleibt unverändert: Speichern rührt die Historie nie an. Was dazukommt,
> ist ein **getrennter, jedes Mal neu anzuhakender Vorgang**, den der
> Anwender bewusst auslöst — fachlich dasselbe wie ein
> Sammel-Bearbeiten in der Ausgabenliste, nur vom Vorlagendialog aus
> bequemer erreichbar.
>
> **Von Paul freigegeben am 07.08.2026.** Die Ergänzung von Regel 6 ist
> Schritt 0 dieser Aufgabe (siehe unten) — sie wird zusammen mit dem Code
> committet, nicht vorher: CLAUDE.md soll nie ein Verhalten beschreiben,
> das es noch nicht gibt.

**Umfang der Übertragung — bewusst begrenzt**

| Feld | wird übertragen | Grund |
|---|---|---|
| `AmountCents` | ja | der Hauptfall („Miete ist gestiegen") |
| `CategoryId` | ja | Umsortieren im Kategoriebaum |
| `PayerId` | ja | |
| `Note` | ja | |
| `IsIncome` | ja | |
| `ExpenseDate` | **nein** | ergibt sich aus dem Rhythmus, nicht aus der Vorlage |
| `SettledDate` | **nein** | gehört der einzelnen Buchung (Regel 4) |
| Rhythmus, Start-/Enddatum | **nein** | ändern, welche Vorkommen es gibt, nicht wie sie aussehen |

`ModifiedUtc` wird auf jeder berührten Zeile mitgezogen — wie beim
Zusammenführen von Kategorien.

**Vorgehen**

0. **`CLAUDE.md`, Regel 6** um einen Satz ergänzen, direkt hinter
   „…duerfen die Historie nicht ruckwirkend veraendern.":
   *„Ausgenommen ist die ausdruecklich angehakte, einmalige Uebertragung
   beim Speichern einer Vorlage — sie ist eine Anwenderaktion, keine
   Referenz, und laesst Datum und Beglichen-Status unberuehrt."*
   Im selben Commit wie der Code.
1. `Core/RecurringExpenses/RecurringExpenseRepository.cs`:
   `public int ApplyToGeneratedExpenses(int templateId)` — eine
   Transaktion, ein sichtbares `UPDATE Expense SET CategoryId = …,
   PayerId = …, AmountCents = …, Note = …, IsIncome = …,
   ModifiedUtc = @Jetzt FROM (SELECT …) WHERE RecurringExpenseId = @Id`.
   Liefert die Anzahl geänderter Zeilen zurück.
   Zeitstempel im Format `'YYYY-MM-DDTHH:MM:SSZ'` (Regel 3).
2. **Vorher automatisch sichern** — dieselbe Begründung wie bei Punkt 4:
   nicht rückgängig zu machen.
3. `ViewModels/VorlageBearbeitenViewModel.cs`: Die Mechanik für eine
   bestätigungspflichtige Zusatzaktion ist bereits da
   (`RueckwirkendAbfrageAngefordert` / `RueckwirkendBestaetigt` /
   `FordereRueckwirkendBestaetigung`) — **nicht wiederverwenden**, das
   betrifft die rückwirkende *Erzeugung* neuer Vorkommen und ist etwas
   anderes. Neu, klar getrennt benannt:
   - `[ObservableProperty] private bool _aenderungUebertragen;` (immer
     `false` beim Öffnen des Dialogs)
   - `public int UebertragbareAnzahl { get; }` — beim Öffnen aus
     `CountGeneratedExpenses` gefüllt
   - `public bool UebertragungMoeglich => IstBestehend && UebertragbareAnzahl > 0;`
   - `public string UebertragungText` — „Diese Änderung auch auf die N
     bereits erzeugten Buchungen anwenden. Betrifft Betrag, Kategorie,
     Zahler, Bemerkung und Art. Datum und Beglichen-Status bleiben
     unverändert. Nicht rückgängig zu machen — vorher wird automatisch
     gesichert."
   - Der vorhandene Hinweis `BetragHinweisSichtbar` (Regel 6) muss
     umformuliert werden: er sagt heute pauschal, dass nichts rückwirkend
     wirkt. Neuer Text: „…wirkt sich nicht auf bereits erzeugte Buchungen
     aus, es sei denn, die Übertragung unten wird angehakt."
4. `ViewModels/VorlagenViewModel.cs`: In `Speichern` nach erfolgreichem
   `Update` und **nur** bei `AenderungUebertragen`:
   sichern → `ApplyToGeneratedExpenses` über `Schreibvorgang` →
   `BuchungenGeaendertNachricht` senden → Erfolgsband mit der Anzahl.
   Scheitert die Übertragung, bleibt die gespeicherte Vorlage bestehen
   (das ist korrekt) und der Fehlertext nennt das ausdrücklich: „Die
   Vorlage wurde gespeichert, die Übertragung auf die bestehenden
   Buchungen jedoch nicht ausgeführt."
5. `Views/VorlageBearbeitenView` bzw. der Vorlagendialog in
   `Views/VorlagenView.axaml`: `CheckBox` unterhalb der Felder,
   `IsVisible="{Binding UebertragungMoeglich}"`, Beschriftung aus
   `UebertragungText`, in einem `Border.banner.hinweis`.
6. `Core/Logging/LogEvents.cs`:
   `TemplateChangeApplied(int vorlageId, int anzahl)`.

**Tests** (`RecurringExpenseRepositoryTests`, neuer Testfall in
`VorlageAnkertagTests` passt nicht — eigene Datei
`VorlageUebertragungTests.cs`)
- Übertragung ändert Betrag/Kategorie/Zahler/Bemerkung/`IsIncome` aller
  erzeugten Buchungen.
- `ExpenseDate` und `SettledDate` bleiben unangetastet — auch bei einer
  bereits beglichenen Buchung.
- Handerfasste Buchungen und Buchungen anderer Vorlagen bleiben
  unberührt.
- `ModifiedUtc` ist danach neuer als vorher und hat das Format aus
  Regel 3.
- Ohne Anhaken passiert nichts (Regel 6 im Normalfall unverändert).

**Fertig, wenn:** Speichern ohne Haken verhält sich exakt wie bisher,
mit Haken werden genau die erlaubten Felder übertragen, vorher wird
gesichert, und Regel 6 in CLAUDE.md ist entsprechend ergänzt.

---

## Batch 2 — Datensicherung (Punkt 6)

### [x] 6. Bereich „Datensicherung" neu bauen

**Erledigt in:** Datensicherung zeigt einen Zustand, Sicherungen lassen sich pruefen

**Ziel:** Die Seite beantwortet auf einen Blick eine einzige Frage — „bin
ich abgesichert?" — und stellt alles andere dahinter.

**Was heute nicht stimmt** (`Views/DatensicherungView.axaml`,
`ViewModels/DatensicherungViewModel.cs`)

1. **Fünf Elemente konkurrieren um dieselbe Aussage:** Banner oben,
   Meldungsband, `Ziel1StatusText`, `Ziel2StatusText`, plus zwei separate
   Fehlerkästen. Der Anwender muss sie selbst zu einem Gesamtbild
   zusammenrechnen.
2. **Der Banner nennt nur Ziel 2.** Schlägt Ziel 1 fehl — der ernstere
   Fall — steht oben trotzdem „Kein zweites Sicherungsziel".
3. **Drei verschiedene Fehlerdarstellungen** (Banner-Klassen, roher
   `Background="{DynamicResource FehlerFlaeche}"` in zwei Inline-Borders,
   Meldungsband) für dieselbe Sache.
4. **Rohe Pfade als Dauerinhalt** in Monospace — Rauschen, das die
   wichtigen Zeilen verdrängt.
5. **„Wiederherstellen" ist eine Textwand**, die als eigene Karte
   dauerhaft Platz belegt, obwohl sie im Normalfall nie gebraucht wird.
6. **Die Sicherungsliste kann nichts.** Man sieht Dateinamen, kann aber
   weder eine Sicherung prüfen noch etwas mit ihr anfangen.
7. **Der Nutzen ist unbelegt.** Nirgends steht, ob eine Sicherung
   überhaupt lesbar ist.

**Recherchierte Muster, die angewendet werden**

- **Status zuerst, genau eine Aussage.** Backup-Oberflächen zeigen einen
  Gesamtzustand plus Zeitstempel der letzten Sicherung, nicht je Ziel
  eine Teilaussage.
  ([IBM: Backup/Restore-Statusanzeige](https://www.ibm.com/docs/en/noi/1.6.4?topic=restore-backing-up-restoring-ui-configuration-data-ocp))
- **Reibung nach Schadensausmaß bemessen.** Harmlos und umkehrbar →
  Undo-Band statt Dialog; schwer umkehrbar → Modal mit Folgenanzeige.
  ([LogRocket: UX for reversible actions](https://blog.logrocket.com/ux-design/ux-reversible-actions-framework/))
- **Folgen beziffern, gefährliche Aktion als `danger` kennzeichnen,
  bei Aktionen mit weiterem Wirkungskreis eine Eingabebestätigung
  verlangen.**
  ([GitLab Pajamas: Destructive actions](https://design.gitlab.com/patterns/destructive-actions/))
- **Progressive disclosure in Einstellungsseiten**: Voreinstellungen und
  Status sichtbar, Details und selten Gebrauchtes eingeklappt.
  ([Uxcel: Designing useful settings pages](https://uxcel.com/lessons/settings-best-practices-572))
- **Sicherungen sind nur so viel wert wie ihre Wiederherstellbarkeit** —
  eine Prüffunktion gehört dazu. („Backups are only useful if they work,
  so regularly testing restoration ensures data can be recovered.",
  [3x-ui: Backup/Restore-Praxis](https://3x-ui.com/how-to-backup-restore-3x-ui-settings/))
- **Zwei Kopien, eine davon außerhalb des Rechners** — die bestehende
  Ziel-1/Ziel-2-Anlage ist richtig, sie muss nur als *ein* Zustand
  dargestellt werden.
  ([Microsoft: Windows app restore](https://learn.microsoft.com/en-us/windows/apps/develop/windows-app-restore))

**Neuer Aufbau der Seite**

```
┌────────────────────────────────────────────────────────┐
│  ●  Gesichert                                          │  ← EIN Zustand,
│     Zuletzt heute 08:14 auf diesem Rechner             │    aus beiden
│     und am 05.08. im Ordner „D:\Sicherungen".          │    Zielen berechnet
│                                    [ Jetzt sichern ]   │
└────────────────────────────────────────────────────────┘

  Ziele                                          (Karte)
  ● Dieser Rechner        heute 08:14      ⋯
  ● Zusätzlicher Ordner   05.08.2026       [Ordner wählen…] ⋯
    ▸ Kein zweites Ziel? Dann liegt alles auf einer Platte.

  Sicherungen (12)                               (Karte)
  ausgaben-2026-08-07.zip   07.08. 08:14   1,2 MB   [Prüfen] ⋯
  ausgaben-2026-08-06.zip   06.08. 09:02   1,2 MB   [Prüfen] ⋯
  …

  ▸ Hilfe: Wie stelle ich eine Sicherung wieder her?   (Expander)
  ▸ Wo liegen meine Dateien?                          (Expander)
```

**Vorgehen**

1. **Neu in Core: `Core/Backups/BackupHealth.cs`** — der Gesamtzustand,
   damit die Ampel testbar ist und nicht im ViewModel entsteht (Regel 7).
   ```
   public enum BackupHealthLevel { Gesichert, Eingeschraenkt, NichtGesichert }
   ```
   Regeln (als Kommentar in der Datei festhalten):
   - `NichtGesichert`: Ziel 1 ist fehlgeschlagen **oder** es gibt gar
     keine Sicherung. Das ist der einzige Zustand, der eine Warnfarbe
     verdient.
   - `Eingeschraenkt`: Ziel 1 in Ordnung, aber Ziel 2 fehlt, ist
     fehlgeschlagen oder älter als die Schwelle aus
     `ExternalBackupAge` (14 Tage).
   - `Gesichert`: beide aktuell.
   Eingang: `BackupResult?`, Anzahl vorhandener Sicherungen, Zeitpunkt
   der letzten lokalen Sicherung, `LastExternalBackupUtc`, `jetzt`.
2. **Neu: `Core/Backups/BackupHealthText.cs`** — Überschrift und
   Erläuterung je Zustand, jeweils drei Aussagen (Regel 12). Wird von
   `MeldungsGrundsaetzeTests` mit erfasst; die Testliste dort ergänzen.
3. **Neu: `Core/Backups/BackupVerification.cs`** — prüft eine
   Sicherungsdatei:
   - ZIP in einen temporären Ordner entpacken,
   - `ausgaben.db` mit `PRAGMA foreign_keys = ON` öffnen,
   - `PRAGMA integrity_check` auswerten,
   - `SELECT Version FROM SchemaVersion` lesen,
   - `SELECT COUNT(*) FROM Expense` lesen,
   - temporären Ordner wieder aufräumen (auch im Fehlerfall, `try/finally`).
   Ergebnistyp `BackupVerificationResult` mit
   `IsReadable`, `SchemaVersion`, `ExpenseCount`, `Problem`.
   Wirft nicht — Probleme kommen als `StorageProblem` zurück, Text aus
   `FileErrorText`. `DatabaseHealth` ist als Vorbild da und wird, wo es
   passt, wiederverwendet statt nachgebaut.
4. **`ViewModels/DatensicherungViewModel.cs` aufräumen:**
   - `Ziel1FehlerText`, `Ziel2FehlerText`, `Ziel1StatusText`,
     `Ziel2StatusText`, `ExterneSicherungIstAktuell/IstVeraltet/Fehlt`,
     `ExterneSicherungBannerText` **ersetzen** durch
     `Zustand` (aus `BackupHealth`), `ZustandUeberschrift`,
     `ZustandText` sowie je Ziel eine schlanke Zeile
     (`ZielZeile`-Record: Name, Statuspunkt, Zeitpunkt, Pfad, Problemtext).
   - „Ziel entfernen" verliert die Sofortwirkung und bekommt ein
     **Undo-Band**: Pfad merken, Band „Zweites Ziel entfernt ·
     Rückgängig" für die Dauer der Sitzung. Reibung passend zum
     Schadensausmaß — nichts geht verloren, ein Dialog wäre zu viel.
   - Neu: `[RelayCommand] private void SicherungPruefen(SicherungZeile?)`
     → `BackupVerification`, Ergebnis als Band und als Merkmal an der
     Zeile („geprüft ✓ · Schema 4 · 1 284 Buchungen").
   - `WiederherstellungHinweis` bleibt inhaltlich, wandert aber in einen
     Expander unter „Hilfe".
   - `MeldungText`/`MeldungIstFehler` bleiben als **einziger**
     Meldungsweg. Die beiden Inline-Fehlerkästen entfallen ersatzlos —
     ihr Inhalt geht in die Zustandskarte bzw. die Zielzeile.
5. **`Views/DatensicherungView.axaml` neu schreiben** nach der Skizze
   oben.
   - Alle Farben über `Classes` + `DynamicResource`, keine
     `Background="{DynamicResource …}"`-Attribute direkt am Element (der
     bestehende Kommentar in der Datei erklärt, warum — die Regel wird
     an zwei Stellen selbst gebrochen).
   - Statuspunkt als kleines `Ellipse` mit Klassen
     `.gut`/`.eingeschraenkt`/`.schlecht`; Farbe ist **Zusatz zum Text**,
     nie sein Ersatz (Regel 10 sinngemäß).
   - Pfade nicht mehr als Dauerinhalt: gekürzt in der Zielzeile, voll im
     `ToolTip` und im ⋯-Menü („Pfad kopieren", „Ordner öffnen").
   - Spaltenraster weiterhin über `anzeige:Raster.Spalten`, Breiten über
     `{anzeige:Breite N}` (Regel 9).
   - Neue Styles (`Ellipse.statuspunkt…`, `Border.zustandskarte`) in
     `App.axaml` zu den bestehenden `Border.karte`/`Border.banner`
     stellen, nicht lokal in der View — sie werden in Punkt 6b und in
     künftigen Bereichen wiederverwendet.
6. **`Core/Logging/LogEvents.cs`**: `BackupVerified(string dateiname,
   bool lesbar, int schemaVersion)`. Dateiname ist erlaubt, Beträge
   nicht (Regel 11).

**Tests**
- Neue Datei `Ausgabenverwaltung.Tests/BackupHealthTests.cs`: alle drei
  Zustände, inklusive der Grenzfälle „Ziel 1 fehlgeschlagen, Ziel 2
  aktuell" (→ `NichtGesichert`) und „keine Sicherung vorhanden, kein
  Fehler" (→ `NichtGesichert`).
- Neue Datei `Ausgabenverwaltung.Tests/BackupVerificationTests.cs`:
  gültige ZIP, ZIP ohne `ausgaben.db`, beschädigte Datenbank, ZIP mit
  neuerer Schemaversion. Temporäre Ordner werden in allen vier Fällen
  aufgeräumt.
- `MeldungsGrundsaetzeTests` um `BackupHealthText` erweitern.
- **`SicherungsfehlerTests` mitziehen.** Der Umbau entfernt
  `Ziel1FehlerText`/`Ziel2FehlerText` und die beiden dauerhaft sichtbaren
  Fehlerkästen je Ziel. Der Inhalt geht nicht verloren, er wandert in die
  Zustandskarte bzw. die Zielzeile — die Testfälle müssen also auf die
  neuen Eigenschaften umgeschrieben, nicht gelöscht werden. Jeder heute
  geprüfte Fehlerfall muss danach weiterhin einen Test haben.

**Nicht Teil dieser Aufgabe:** Wiederherstellen aus der Anwendung heraus.
Das ist Punkt 16 und ein eigener Vorgang.

**Fertig, wenn:** Die Seite hat genau eine Statusaussage, jede
Fehlerdarstellung benutzt dieselbe Bauform, „Prüfen" funktioniert an
jeder Sicherungszeile, das Entfernen von Ziel 2 ist rückgängig zu machen,
und Build und Tests sind grün.

---

## Batch 3 — Erfassen mit weniger Tipparbeit (Punkte 7–11)

### [x] 7. Betragsfeld rechnet, Datumsfeld versteht Kurzformen

**Erledigt in:** Betragsfeld rechnet, Datumsfeld versteht Kurzformen

**Ziel:** Weniger Kopfrechnen und weniger Tippen an den beiden Feldern,
die bei jeder Erfassung angefasst werden.

**Abweichungen von der Vorgabe, bewusst:**

- **Der Punkt gilt jetzt als Dezimaltrennzeichen** (`12.50` → 12,50 €),
  aber nur, wo er kein Tausendertrennzeichen sein *kann*: `1.500` und
  `1.234,56` bleiben abgelehnt. Möglich wird das erst durch das
  Zurückschreiben der Normalform — der Anwender sieht sofort, was
  verstanden wurde. `Money.TryParseEuroText` bleibt unverändert streng,
  weil dort (Vorlagenbetrag) kein Zurückschreiben stattfindet.
- **Negative Ergebnisse liefert `BetragsAusdruck` als negativen Wert
  zurück**, nicht als `null`. Die Regel „Beträge sind immer positiv"
  steht weiterhin nur an einer Stelle, im `ExpenseValidator` — nur von
  dort kommt die Meldung, die den Grund nennt.
- Die Kurzformen gelten **nur dort, wo es einen Bezugstag gibt** (das
  Datumsfeld von Erfassungsmaske und Bearbeiten-Dialog, beide über
  `ExpenseValidator`). Im Von/Bis eines Zeitraums bleibt es beim
  vollständigen Datum: eine zweite `TryParse`-Überladung mit Bezugstag
  trennt beides.

**Vorgehen**

1. **Neu: `Core/Formatting/BetragsAusdruck.cs`** — wertet einfache
   Additionen und Subtraktionen aus: `12,50+3,20` → `1570` Cent,
   `40-2,50` → `3750`. Nur `+` und `-`, keine Klammern, keine
   Multiplikation — es geht um „drei Kassenzettel zusammenzählen", nicht
   um einen Taschenrechner. Rechnung in `decimal`, Umwandlung in Cent
   ausschließlich über `Money` (Regel 1). Ungültiges liefert `null`, kein
   Wurf.
2. `Core/Expenses/ExpenseValidator.cs` bzw. die Stelle, die den Betrag
   liest, nutzt `BetragsAusdruck` vor der bisherigen Prüfung.
3. **`Core/Formatting/GermanDateInput.cs` erweitern**: versteht
   zusätzlich `heute`, `gestern`, `vorgestern`, `-3` (vor drei Tagen) und
   `15.` (der 15. des laufenden Monats; liegt er in der Zukunft, der 15.
   des Vormonats — das ist beim Nacherfassen von Belegen fast immer
   gemeint). Die vorhandene Ausgabefunktion `ToText` bleibt unverändert.
4. Beim Verlassen des Feldes wird der erkannte Wert in die
   Normalform zurückgeschrieben (`12.5` → `12,50`, `heute` →
   `07.08.2026`), damit sichtbar ist, was verstanden wurde.

**Tests**
- Neue Datei `BetragsAusdruckTests.cs`: Summen, Differenzen, negatives
  Ergebnis (→ ungültig, Beträge sind immer positiv), Leerzeichen,
  Punkt statt Komma, Unsinn.
- `GermanDateInputTests.cs` erweitern: alle neuen Kurzformen, plus
  Nachweis, dass die bisherigen Formate unverändert funktionieren.

---

### [x] 8. Serienerfassung: Werte behalten und Schnellwahl

**Erledigt in:** Erfassungsmaske behaelt Werte und bietet eine Schnellwahl

**Ziel:** Fünf Belege hintereinander erfassen, ohne fünfmal dieselbe
Kategorie zu wählen.

**Ergänzung:** Bei angehakter Serienerfassung bleibt auch das
Einnahme-Häkchen stehen („geleert werden nur Betrag und Bemerkung") —
im Regelfall wird es weiterhin zurückgesetzt. Die Schnellwahl filtert
gegen `GetSelectableLeaves`, damit sie nichts anbieten kann, was sich
im Kategoriefeld daneben nicht auswählen lässt.

**Vorgehen**

1. `ViewModels/ErfassenViewModel.cs`:
   `[ObservableProperty] private bool _werteBehalten;` (in
   `AppSettings` merken, damit die Einstellung den Neustart übersteht).
   Ist sie an, bleiben nach dem Speichern Kategorie, Zahler und Datum
   stehen; geleert werden nur Betrag und Bemerkung. Der Fokus springt
   auf das Betragsfeld.
2. **Schnellwahl-Chips**: `Core/Categories/CategoryRepository.cs` bekommt
   `GetMostUsed(int count, DateOnly seit)` — sichtbares SQL,
   `GROUP BY CategoryId ORDER BY COUNT(*) DESC`. Die fünf häufigsten
   Kategorien der letzten 90 Tage erscheinen als Knöpfe über dem
   Kategoriefeld.
3. `Views/ErfassenView.axaml`: `ItemsControl` mit
   `Classes="schnellwahl"`, `WrapPanel` als Panel — Chips müssen bei
   Schriftstufe „Sehr groß" umbrechen dürfen (Regel 9).

**Tests**: `CategoryRepositoryTests` — Reihenfolge nach Häufigkeit,
Zeitraumgrenze wirkt, archivierte Kategorien tauchen nicht auf.

---

### [x] 9. Vorlage aus einer bestehenden Buchung erzeugen

**Erledigt in:** Aus einer Buchung laesst sich eine Vorlage anlegen

**Ziel:** „Das kommt jeden Monat" — ein Klick statt Neuanlage im
Vorlagenbereich.

**Abweichung von der Vorgabe, bewusst:** Der Einstieg ist ein
**Kontextmenü auf der Zeile**, kein vierter Knopf in der Aktionsspalte.
Die steht laut dem Kommentar am Kopf von `AusgabenlisteView.axaml`
bereits an ihrer Breitengrenze — „wächst sie weiter, gehört das in ein
Kontextmenü". Punkt 14 zieht die übrigen Aktionen dorthin nach. Der
Spaltenkopf „Aktionen" nennt den Rechtsklick im Hilfetext, damit die
Funktion auffindbar bleibt.

**Vorgehen**

1. `ViewModels/AusgabenlisteViewModel.cs`: `[RelayCommand] private void
   AlsVorlage(AusgabeZeile? zeile)` — löst ein Event
   `VorlageAusBuchungAngefordert(int expenseId)` aus.
2. `ViewModels/MainViewModel.cs`: verdrahtet auf
   `verwaltung.Vorlagen.NeueVorlageAus(expenseId)` und wechselt in den
   Bereich „Wiederkehrende Ausgaben".
3. `ViewModels/VorlagenViewModel.cs` / `VorlageBearbeitenViewModel`:
   neuer Dialog, vorbelegt mit Kategorie, Zahler, Betrag, Bemerkung und
   `IsIncome` der Buchung; `Title` = Bemerkung oder Kategoriename;
   `StartDate` = Datum der Buchung; `IntervalUnit` = `month`,
   `IntervalCount` = 1; `AnchorDay` = Tag des Buchungsdatums.
   **`GeneratedThrough` wird auf das Buchungsdatum gesetzt**, damit die
   Vorlage nicht sofort das Vorkommen nacherzeugt, das es schon gibt.
4. Nichts wird automatisch gespeichert — der Dialog geht auf, der
   Anwender bestätigt.

**Tests**: `RecurringExpenseRepositoryTests` — eine so angelegte Vorlage
erzeugt beim nächsten Lauf **kein** Duplikat der Ausgangsbuchung.

---

### [x] 10. Vorschläge aus der Historie

**Erledigt in:** Die Erfassungsmaske bietet die Werte der letzten gleichen Buchung an

**Ziel:** Bemerkung getippt → Kategorie, Betrag und Zahler der letzten
gleichlautenden Buchung stehen bereit.

**Ergänzung:** Übernommen wird zusätzlich die **Buchungsart**
(`IsIncome`) — sonst würde aus einer Einnahme von Anna still eine
Ausgabe an Anna, weil der Zahler ja mitwandert.

**Vorgehen**

1. **Neu: `Core/Expenses/ExpenseSuggestion.cs`** +
   `ExpenseRepository.SuggestFor(string note)` — sichtbares SQL,
   `WHERE Note = @Note COLLATE NOCASE ORDER BY ExpenseDate DESC LIMIT 1`.
   Liefert `null`, wenn nichts passt.
2. `ErfassenViewModel`: auf `OnBemerkungChanged` (entprellt, mindestens
   3 Zeichen) den Vorschlag holen und als **Angebot** anzeigen —
   nicht automatisch einsetzen. Ein Band mit „Zuletzt: Lebensmittel ·
   42,90 € · Paul — [übernehmen]".
3. Übernehmen füllt Kategorie, Betrag und Zahler; das Datum bleibt.

**Tests**: `ExpenseRepositoryTests` — Groß-/Kleinschreibung egal, die
jüngste Buchung gewinnt, keine Treffer → `null`.

---

### [x] 11. Sammelaktionen in der Ausgabenliste

**Erledigt in:** Markierte Buchungen lassen sich gemeinsam aendern

**Ziel:** Was heute nur fürs Löschen geht, geht auch fürs Ändern.

**Ergänzung:** Alle drei Methoden liefern die Zahl der tatsächlich
geänderten Zeilen (nicht nur `SetSettledMany`) — die Aktionsleiste
formuliert ihren Erfolgstext daraus und benennt ausdrücklich, wenn
weniger Zeilen gewandert sind als markiert waren.

**Vorgehen**

1. `Core/Expenses/ExpenseRepository.cs`, je eine Transaktion, sichtbares
   SQL, `ModifiedUtc` mitziehen:
   - `SetCategoryMany(IReadOnlyList<int> ids, int categoryId)`
   - `SetPayerMany(IReadOnlyList<int> ids, int payerId)`
   - `SetSettledMany(IReadOnlyList<int> ids, DateOnly? settledDate)` —
     **beachtet Regel 4**: bei eigenen Buchungen wird `SettledDate` nicht
     gesetzt, diese Ids werden übersprungen und die Anzahl der
     tatsächlich geänderten Zeilen zurückgegeben.
2. `AusgabenlisteViewModel`: eine Aktionsleiste, die erscheint, sobald
   mindestens eine Zeile markiert ist („3 markiert · Kategorie ändern ·
   Zahler ändern · Als beglichen markieren · Löschen"). Alles über
   `Schreibvorgang`, danach `BuchungenGeaendertNachricht`.
3. Ab 20 betroffenen Zeilen: Bestätigung mit Anzahl, darunter kein
   Dialog — Reibung nach Schadensausmaß.

**Tests**: `ExpenseRepositoryTests` — alle drei Methoden, jeweils mit
einer nicht betroffenen Kontrollzeile; für `SetSettledMany` zusätzlich
der Regel-4-Fall.

---

## Batch 4 — Weitere Quality-of-Life-Punkte (12–20)

### [x] 12. Rückgängig statt Löschbestätigung

**Erledigt in:** Geloeschte Buchungen lassen sich zurueckholen

Das Bestätigungsband beim Löschen von Buchungen durch ein Undo-Band
ersetzen („3 Buchungen gelöscht · Rückgängig"). Gelöschte Zeilen bis zum
Bereichswechsel im Speicher halten und bei „Rückgängig" über
`Create` neu anlegen. Passt zum recherchierten Muster: umkehrbar machen
schlägt nachfragen. Vorlagen-Löschen (Punkt 4) behält seinen Dialog — das
Schadensausmaß ist größer.

**Abweichung von der Vorgabe, bewusst:** Wiederhergestellt wird über eine
eigene Methode `ExpenseRepository.RestoreMany` statt über `Create` — sie
läuft in EINER Transaktion (eine halb zurückgeholte Auswahl wäre schlimmer
als eine gar nicht zurückgeholte) und schreibt `CreatedUtc`, `SettledDate`
und den Vorlagenbezug unverändert zurück, was `Create` nicht kann. Die
Zeilen bekommen dabei neue Ids; die alten sind mit dem Löschen verfallen
und können inzwischen an eine neu erfasste Buchung vergeben sein.

### [x] 13. Tastaturkürzel und eine Übersicht dazu

**Erledigt in:** Die Anwendung laesst sich ueber die Tastatur bedienen

`Strg+N` erfassen, `Strg+S`/`Strg+Enter` speichern, `Esc` Dialog
schließen, `Strg+F` Suchfeld, `Strg+1..9` Bereichswechsel, `F1`
Kürzelübersicht. Definition an **einer** Stelle
(`Anzeige/Tastenkuerzel.cs`), damit die Übersichtsseite sich daraus
erzeugt und nicht auseinanderläuft.

**Umsetzung, über die Vorgabe hinaus:** Nicht nur die Übersicht, auch die
**Bindungen** entstehen aus derselben Liste — über
`Anzeige/Kuerzelbindung.cs`, im Code-Behind statt als `KeyBinding` in der
`.axaml`. Sonst stünde jede Geste zweimal im Programm und die Hilfeseite
wäre spätestens beim nächsten neuen Kürzel falsch, ohne dass es jemandem
auffällt. `TastenkuerzelTests` prüft beides mit: dass jede Geste gültig und
eindeutig ist, dass die Seite genau die Liste zeigt, und dass eine Bindung
ihr Kommando auch dann findet, wenn der DataContext erst nach dem
Konstruktor gesetzt wird.

`Strg+1..9` zählt die Einträge der Seitenleiste von oben nach unten; die
Zuordnung leitet sich in `MainViewModel.BereicheMitZiffer` aus
`NavigationItems` ab, statt als zweite Liste gepflegt zu werden.

### [x] 14. Kontextmenü auf Listenzeilen

**Erledigt in:** Ein Rechtsklick auf eine Zeile zeigt ihre Aktionen

Rechtsklick auf eine Zeile in Ausgabenliste, Offene Posten und Vorlagen:
Bearbeiten, Duplizieren, Als Vorlage, Als beglichen, Löschen. Nimmt Druck
von der Aktionsspalte, die sonst mit jedem neuen Punkt breiter wird.

**Umsetzung:**

- Das Menü führt **alle** Aktionen der Zeile, auch die, die daneben als
  Knopf stehen. Ein Menü, in dem die Hälfte fehlt, zwingt zum Wechseln
  zwischen zwei Wegen.
- Was für eine Zeile nicht gilt (Abhaken einer eigenen Ausgabe, Regel 4;
  „Rückgängig" bei einem noch offenen Posten; „Aktivieren" bei einer
  aktiven Vorlage), ist **ausgegraut statt versteckt** — sonst springen
  die Einträge je nach getroffener Zeile ihren Platz.
- Neu dabei: `AlsBeglichen` für eine einzelne Zeile der Ausgabenliste. Es
  schreibt über dasselbe `SetSettledMany` wie die Sammelaktion, damit
  Regel 4 an genau einer Stelle steht.
- Die Aktionsspalte der Ausgabenliste schrumpft von 300 auf 200 Pixel:
  „Duplizieren" ist ins Menü gewandert, „Bearbeiten" und „Löschen" bleiben
  sichtbar. Der Hilfetext am Spaltenkopf nennt den Rechtsklick, damit die
  übrigen Aktionen auffindbar bleiben. Die `MinWidth` des waagerechten
  Bildlaufs sinkt entsprechend von 1100 auf 1000.

### [x] 15. Suche über Bemerkung, Kategorie und Zahler

**Erledigt in:** Die Suche findet auch Kategorie und Zahler

Heute durchsucht `ReportFilter.SearchText` nur `Note`. Auf
Kategoriepfad und Zahlername ausweiten — in `ReportFilterSql`, mit
sichtbarem `OR`. Der Platzhaltertext im Suchfeld muss das sagen.

**Umsetzung:**

- Der Kategoriepfad wird **in `ReportFilterSql` selbst** per rekursiver
  CTE gebildet, statt ihn vom Aufrufer zu verlangen. `Summarize` und die
  beiden Auswertungsabfragen joinen die Kategorie überhaupt nicht — der
  Vertrag der Klasse bleibt so bei `Expense e` und `Person p`, und Liste,
  Trefferzahl, Summe und Auswertung sehen zwangsläufig dieselbe
  Treffermenge. Die Unterabfrage entscheidet pro Buchung, was sie muss:
  `EvaluateMatrix` vervielfacht jede Buchung über ihre Ahnen.
- Durchsucht wird der **ganze Pfad**, nicht nur der Name der gebuchten
  Kategorie — „Wohnen" findet auch die Buchung unter
  „Wohnen › Nebenkosten › Strom".
- Nebenwirkung, ausdrücklich gewollt: Buchungen **ohne** Bemerkung fielen
  bei gesetzter Suche bisher immer heraus (`NULL LIKE …` ist NULL). Über
  Kategorie und Zahler sind sie jetzt zu finden.
- Die Beschriftung wechselt von „Suche in der Bemerkung" auf „Suche", der
  Platzhalter auf „Bemerkung, Kategorie, Zahler…" — in **beiden** Ansichten,
  weil beide denselben Filter bauen.

### [x] 16. Wiederherstellen aus der Anwendung

**Erledigt in:** Eine Sicherung laesst sich aus der Anwendung einspielen

Der Nachfolger von Punkt 6. Geführter Ablauf: Sicherung wählen → prüfen
(`BackupVerification`) → **Folgen beziffern** („Die aktive Datenbank
enthält 1 284 Buchungen, diese Sicherung 1 190 — 94 Buchungen wären
danach weg") → aktive Datenbank vorher unter neuem Namen sichern →
ersetzen → Neustart anfordern. Eingabebestätigung des Dateinamens, wie
bei Aktionen mit weiterem Wirkungskreis üblich. Erst umsetzen, wenn
Punkt 6 steht.

**Umsetzung — „ersetzen" geht nicht im Betrieb:**

Die aktive `ausgaben.db` ist die ganze Laufzeit über geöffnet (eine
Verbindung als DI-Singleton, dazu `-wal`/`-shm` daneben). Sie im laufenden
Betrieb zu ersetzen ist unter Windows nicht möglich. Genau dieselbe Lage
hat die Selbstaktualisierung bei der Programmdatei, und der Ablauf folgt
deshalb demselben, schon bewährten Muster (`Updates/UpdateInstaller`):

1. **Im Betrieb** (`Core/Backups/BackupRestore.Vorbereiten`): bisherige
   Datenbank per `VACUUM INTO` als
   `ausgaben-vor-wiederherstellung-<Zeitpunkt>.db` in den Sicherungsordner —
   der Name fällt bewusst durch das Raster von `BackupFileName`, sonst
   würde die Kopie als Sicherung gezählt und irgendwann von
   `BackupRetention` gelöscht. Dann die Sicherung neben die aktive
   Datenbank entpacken (`.wiederherstellung.tmp` → umbenennen) und als
   **Letztes** einen Begleitzettel mit Prüfsumme dazu
   (`Core/Backups/RestoreStaging`). Scheitert die Sicherheitskopie, wird
   gar nichts vorbereitet — ohne Weg zurück keine Wiederherstellung.
2. **Beim nächsten Start** (`BackupRestore.TryUebernehmen`, gerufen in
   `Program.Main` direkt nach dem Ermitteln des Datenbankpfads und **vor**
   der Einzelinstanz-Sperre): Prüfsumme ein zweites Mal prüfen, dann zwei
   Umbenennungen über `.vorher` — die bisherige Datei ist bis zuletzt
   vorhanden. **Danach müssen `-wal` und `-shm` der alten Datenbank weg**:
   `File.Move` nimmt sie nicht mit, sie lägen also neben der neuen Datei,
   und ein fremdes Schreibprotokoll ist genau der Datenverlust, den der
   Ablauf verhindern soll. Verloren geht dabei nichts, die
   Sicherheitskopie aus `VACUUM INTO` enthält deren Inhalt.
3. Der Neustart läuft über `Core/Startup/Neustart` — dorthin sind
   `WarteMerkmal`/`WarteAufVorgaenger` aus `UpdateInstaller` gewandert, weil
   es jetzt **zwei** Anlässe für einen Selbstneustart gibt und zwei
   Fassungen desselben Aufrufmerkmals auseinanderliefen. Erst den
   Nachfolger starten, dann selbst enden; lässt sich kein Nachfolger
   starten, endet nichts und die Sicherung wird beim nächsten Start von
   Hand übernommen.

**Weitere Entscheidungen:**

- **Eine zu neue Sicherung kommt gar nicht in den Ablauf.** Sie ließe den
  nächsten Start an `SchemaVersionTooNewException` scheitern — die
  Anwendung wäre nicht mehr zu öffnen. Eine ältere ist unbedenklich: der
  Start migriert sie und sichert davor selbst noch einmal.
- **Die Eingabebestätigung ist neu** — im Programm gab es dafür kein
  Muster. `Core/Backups/RestoreConfirmation.Matches` ist die prüfbare
  Hälfte (Regel 7), Groß-/Kleinschreibung und Leerzeichen sind egal, alles
  andere muss stimmen. Der Knopf ist **ausgegraut statt versteckt**.
- **Ein gescheitertes Bereitlegen räumt den Ablauf nicht** (Regel 13): die
  Sicherung ist in Ordnung, nur der Datenträger war im Weg — wer Platz
  geschaffen hat, drückt einen Knopf statt neu anzufangen. Eine gescheiterte
  *Prüfung* räumt dagegen, dort ist die Datei selbst untauglich.
- **Das Band nach dem Bereitlegen sagt nicht „erledigt"**, denn es fehlt
  der Neustart, und bis dahin gelten die bisherigen Daten. Es hat auch kein
  „Schließen" — ein offener Vorgang lässt sich nicht wegklicken.
- **Nach dem Neustart wird gemeldet, was geschah** — übernommen oder
  verworfen. Der Text kommt über `Program` in den Bereich
  „Datensicherung", weil die Übernahme vor Datenbank und Fenster läuft;
  ohne diese Meldung bliebe offen, auf welchem Stand die Anwendung gerade
  arbeitet.
- `WiederherstellungHinweis` beschrieb bisher, dass es diese Funktion
  bewusst *nicht* gibt. Der Text beschreibt jetzt den geführten Ablauf und
  behält die Anleitung von Hand — sie wird gebraucht, wenn die Anwendung
  gar nicht mehr startet.

### [x] 17. CSV-Export der Ausgabenliste

**Erledigt in:** Die Ausgabenliste laesst sich als CSV speichern

`Core/Reports/ReportCsv.cs` hat das Muster bereits (`de-DE`, Semikolon,
`EuroText.Plain`). Zweite Methode für die flache Buchungsliste, Knopf in
der Filterleiste. Exportiert wird, was gefiltert ist — nicht alles.

**Umsetzung:**

- `ReportCsv.BuildExpenseList(IReadOnlyList<ExpenseListItem>)` — dieselbe
  Datei wie der Export der Kreuztabelle, damit Trennzeichen, Zeilenende und
  Maskierung nicht auseinanderlaufen. Spalten: Datum, Art, Betrag,
  Kategorie, Zahler, Beglichen am, Bemerkung, Vorlage.
- **Das Vorzeichen kommt vom Buchungstyp**, nicht vom gespeicherten Wert:
  Ausgabe negativ, Einnahme positiv, wie `EuroText.FormatSigned` für eine
  einzelne Buchung. Damit lässt sich in Excel über die Spalte rechnen.
  Bewusst in Kauf genommen: die Spaltensumme kann von der Summe unter der
  Liste abweichen, weil die eine noch **offene** Einnahme mit null zählt
  (das Geld ist nicht geflossen). Im Export steht stattdessen ihr Betrag —
  eine Zeile, die ihren eigenen Betrag verschweigt, wäre wertlos; welche
  offen sind, sagt die Spalte „Beglichen am". Der Grund steht als Kommentar
  an der Methode.
- **Der Knopf sitzt in der Werkzeugleiste, nicht in der Filterleiste** —
  dort, wo „Filter zurücksetzen" schon steht, also bei den Aktionen der
  ganzen Ansicht. Als `WrapPanel`, damit die Reihe bei großer Schriftstufe
  umbricht (Regel 9). Der Hinweistext daneben statt als Band: der Export
  ändert keine Daten, sein Ergebnis muss niemanden aufhalten.
- **`LadeDaten` merkt sich die geladenen Buchungen** (`_angezeigteBuchungen`)
  statt für den Export erneut abzufragen — exportiert werden soll genau
  das, was der Anwender vor sich sieht. Der Hinweistext wird bei jedem
  Ladevorgang gelöscht: „Gespeichert: …" sagt nach einer Filteränderung
  nichts mehr über das, was jetzt zu sehen ist.
- Dateiauswahl, Schreiben und die UTF-8-Signatur bleiben im Code-Behind
  (Regel 7), wortgleich zum Export der Auswertung — derselbe Vorgang soll
  sich nicht an zwei Stellen anders verhalten.

### [x] 18. Fenstergröße, -position und Sortierung merken

**Erledigt in:** Fenster und Sortierung bleiben, wie man sie verlassen hat

Nach `AppSettings` (dort steht `CategoryColumnWidth` schon). Beim Start
prüfen, ob die Position noch auf einem vorhandenen Bildschirm liegt —
sonst zentriert öffnen.

**Umsetzung:**

- **Die Entscheidung steckt in Core**, nicht in der Ansicht:
  `Core/Display/WindowPlacement.cs` (Lage + `ScreenArea`) und
  `WindowPlacements.cs` (`Normalize`, `IsOnScreen`). Der Fall, um den es
  geht — Fenster auf den zweiten Bildschirm geschoben, Bildschirm
  abgezogen — lässt sich in der Oberfläche nicht herstellen, in
  `FensterlageTests` dagegen in einer Zeile. Eigener `ScreenArea`-Typ,
  damit Core ohne die Oberflächen-Baugruppe auskommt.
- **Geprüft wird ein Punkt kurz hinter der linken oberen Ecke**
  (`GrabInset = 24`), nicht die Überlappung mit einem Bildschirm. Ein
  Fenster, das nur mit dem rechten unteren Zipfel hereinragt, überlappt
  zwar — hat aber keine greifbare Titelzeile und ist damit genauso
  verloren wie eines, das ganz daneben liegt.
- **Negative Koordinaten sind kein Ausschlussgrund.** Ein Bildschirm links
  vom Hauptbildschirm hat negative X-Werte; die naheliegende Prüfung
  „kleiner null ist falsch" hätte diesen Aufbau kaputt gemacht. Dafür gibt
  es zwei Tests.
- **Zwei Einheiten, bewusst getrennt:** Lage in physischen Pixeln (nur so
  ist sie mit den Arbeitsflächen vergleichbar), Größe in
  geräteunabhängigen Punkten. Wer beides in eine Einheit rechnen wollte,
  bräuchte die Skalierung des Bildschirms, auf dem das Fenster steht — und
  die kann beim nächsten Start eine andere sein.
- **Maximiert wird getrennt gemerkt**, und der Normalzustand wird
  fortlaufend nachgehalten (`Anzeige/Fensterzustand.cs`). Ein maximiertes
  Fenster verrät seine normale Lage nicht: dort stehen die Maße des
  Vollbilds. Wer erst beim Schließen liest, merkt sich ein
  bildschirmgroßes Fenster, das sich nicht wiederherstellen lässt.
- Nachgemessen, dass `Bounds` und `Width`/`Height` in Avalonia dieselbe
  Größe sind — ein Unterschied von der Rahmenbreite hätte bedeutet, dass
  das Fenster bei jedem Start ein paar Pixel schrumpft.
- **Sortierung beider Listen**, getrennt gemerkt. Dafür ist
  `OffenePostenSortSpalte` nach `Core/OpenItems/OpenItemsSortColumn.cs`
  gewandert: eine Einstellung, deren Typ in der Oberfläche liegt, lässt
  sich in `AppSettings` nicht ablegen, und die nachsichtige Umwandlung aus
  der Datei gehört an die eine Stelle, die das für alle Einstellungen tut.
- **„Filter zurücksetzen" lässt die Sortierung stehen** — sie ist kein
  Filter, sondern die Leserichtung. Ein *Sprung* in die Liste setzt sie
  dagegen auf Datum/absteigend zurück (bestehendes Verhalten), und auch
  das wird gemerkt. Beides ist mit einem Test festgehalten, weil das
  Merken die Frage erst aufwirft.
- **Neu für die Tests:** `TestEinstellungen` — ein `AppSettingsStore` auf
  einer wegwerfbaren Datei. Ohne ihn schrieben die ViewModel-Tests in die
  echte `settings.json` des Rechners und hingen voneinander ab.

**Nicht Teil der Umsetzung:** die Größe auf den neuen Bildschirm
beschneiden. Wer von einem 4K-Bildschirm auf ein kleines Notebook wechselt,
bekommt ein Fenster, das größer als der Bildschirm ist — unschön, aber
bedienbar, weil es zentriert öffnet und sich ziehen lässt. Das ist ein
eigener Punkt, wenn es auffällt.

### [x] 19. Leerzustände mit Handlungsangebot

**Erledigt in:** Leere Listen bieten den Weg heraus an

Überall dort, wo heute nur ein grauer Satz steht („Noch keine Sicherung
vorhanden", „Keine offenen Posten", leere Ausgabenliste): Satz plus der
Knopf, der den Zustand auflöst. Die Datensicherung macht das an einer
Stelle schon vor.

**Umsetzung:**

- **Fünf Leerzustände in drei Bauformen** waren es vorher: grauer
  Kursiv-Einzeiler (Ausgabenliste, Vorlagen, Datensicherung), Symbol mit
  Text (Offene Posten, Auswertung) — und in keiner davon ein Knopf. Die
  Klasse `empty-state` stand schon in einer Ansicht, hatte aber **nie
  einen Stil dazu** und wirkte deshalb gar nicht. Jetzt gibt es den Stil
  in `App.axaml`, und alle fünf benutzen ihn.
- **Der eigentliche Gewinn ist eine Unterscheidung, die es vorher nicht
  gab:** Ausgabenliste und Auswertung sagten beide „Keine Ausgaben für
  diesen Filter" — auch dann, wenn überhaupt noch nichts erfasst war. Das
  klingt nach einem Filterproblem und schickt den Anwender im Kreis.
  Neu `ExpenseRepository.HasAny()` (sichtbares `SELECT EXISTS`, hört beim
  ersten Treffer auf) und daraus `NochNichtsErfasst` /
  `KeinTrefferTrotzDaten` in beiden ViewModels. Gefragt wird nur, wenn die
  Liste leer ist — sonst liefe die Abfrage bei jedem Tastendruck im
  Suchfeld mit.
- **Offene Posten bekommt bewusst KEINEN Knopf.** Dort ist leer das Ziel
  und kein Mangel; ein Angebot, wo nichts fehlt, macht aus einer guten
  Nachricht eine Aufgabe. Stattdessen sagt der Satz jetzt, dass alles in
  Ordnung ist („Nichts offen — …"), statt nur das Fehlen zu melden. Das
  ist die einzige Abweichung von „Satz plus Knopf" und die einzige, die
  sich rechtfertigen lässt.
- Für die Sprünge aus Ausgabenliste und Auswertung in die
  Erfassungsmaske je ein `ErfassenAngefordert`-Ereignis, verdrahtet im
  `MainViewModel` — dasselbe Muster wie bei den Kacheln der Startseite,
  die Listen kennen die Navigation nicht selbst.
- Bei der Datensicherung steht „Jetzt sichern" jetzt **zweimal**: oben in
  der Zustandskarte und im leeren Kasten. Bewusst, weil der Blick beim
  leeren Kasten steht und nicht am Kartenrand; beide lösen dasselbe
  Kommando aus.

**Nicht abgedeckt:** Ob die Bauform in allen Schriftstufen und
Fensterbreiten sitzt, ist reines Layout und nur in der laufenden
Anwendung zu sehen. Die Entscheidung, WELCHES Angebot erscheint, ist
dagegen in `LeerzustandTests` festgehalten.

### [x] 20. „Was ist neu" nach einem Update

**Erledigt in:** Nach einer Aktualisierung zeigt die Anwendung, was neu ist

`AktualisierungViewModel` weiß, wann eine neue Fassung übernommen wurde.
Beim ersten Start danach eine Seite mit den Änderungen zeigen, gespeist
aus dem Text des GitHub-Releases, den `GitHubReleases` ohnehin schon
liest.

**Abweichung von der Vorgabe, bewusst — die Quelle ist eine andere:**

Die Vorgabe wollte den Text „aus dem GitHub-Release, den `GitHubReleases`
ohnehin schon liest". Das trägt nicht: `--generate-notes` fasst *Pull
Requests* zusammen, nicht Commits, und weil hier direkt auf `master`
gepusht wird, bestand der Text von v1.2.1 bis v1.3.1 nur aus dem
Vergleichs-Verweis. Die Seite hätte also nie etwas zu zeigen gehabt.

Stattdessen:

- Eine **`CHANGELOG.md`** im Wurzelverzeichnis, geschrieben für den
  Anwender, vor jeder Anhebung der Versionsnummer (neue **Regel 15** in
  CLAUDE.md). `ChangelogTests` prüft, dass die ausgelieferte Fassung
  einen Abschnitt hat und dass darin kein Fachgesimpel steht.
- Die Datei wird in die Baugruppe **eingebettet**. Die Anwendung bringt
  ihren Text also selbst mit: keine Netzverbindung, keine Datei neben der
  Programmdatei, und der Text gehört unweigerlich zu der Fassung, die
  gerade läuft. `GitHubReleases` bleibt unangetastet.
- Der Veröffentlichungs-Workflow nimmt **denselben** Abschnitt als
  Release-Text (`--notes-file`) — geschrieben wird er genau einmal.
- Wer eine Fassung überspringt, bekommt **alle** dazwischenliegenden
  Abschnitte, jeder mit seiner Überschrift.
- **Kein Markdown-Darsteller**: gezeigt werden Überschrift,
  Aufzählungspunkt und Absatz, mehr nicht.
- Die Seite bleibt in zwei Fällen bewusst **aus**: bei der ersten
  Ausführung überhaupt und bei einer Fassung ohne eigenen Abschnitt. Eine
  leere Seite „Was ist neu" ist schlechter als keine.
- Die gesehene Fassung wird **sofort** gemerkt, nicht erst beim
  Wegklicken — sonst ginge die Seite nach einem Absturz erneut auf.
- Ein eigener Bereich ohne Sidebar-Platz (`NavigationGruppe.Keine`, wie
  „Darstellung"), kein Dialogfenster: eine Seite lässt sich rollen und in
  der eingestellten Schriftgröße lesen, ohne den Start aufzuhalten.

---

## Batch 5 — Was beim Benutzen aufgefallen ist (Punkte 21–23)

### [x] 21. Der Aktualisierungsvorgang ist verwirrend — Band und Dateireste

**Erledigt in:** Die Aktualisierung sagt was zu tun ist und raeumt hinter sich auf

**Abweichungen und Funde bei der Umsetzung:**

- **Ein Fehler wäre fast entstanden:** „beim Start alle Endungen räumen"
  hätte eine **gültige wartende** Vorbereitung (`.neu` + `.neu.json`)
  gelöscht, bevor sie eingespielt werden kann — die Aktualisierung hätte
  dann nie stattgefunden, egal wie oft der Anwender neu startet. Deshalb
  jetzt zwei Listen: `AlleEndungen` (die Zusicherung für die Tests) und
  `RestEndungen` (was beim Start weg darf: `.alt`, `.teil`,
  `.auspacken`). Um `.neu` kümmert sich der Austausch selbst. Ein Test
  hält das fest.
- `.teil` und `.auspacken` hießen vorher nur lokal in `UpdateDownload` so;
  sie stehen jetzt als Endungen in `UpdateStaging` und werden dadurch
  überhaupt erst beim Start geräumt.
- **Das Verstecken ist nicht nachweisbar in dieser Umgebung.** Die beiden
  Tests dazu (`Waehrend_des_Austauschs_ist_hoechstens_eine_Datei_sichtbar`,
  `Die_neue_Programmdatei_ist_nach_dem_Austausch_sichtbar`) tragen
  `[WindowsOnlyFact]` und werden hier übersprungen — sie laufen im
  Release-Workflow, der auf `windows-latest` baut. Ob
  `SetFileAttributes` auf der **laufenden** Programmdatei durchgeht, ist
  damit weiterhin offen; scheitert es, bleibt die Datei sichtbar, der
  Austausch läuft aber unverändert durch (`Verstecke` wirft nie). Der
  Ausweichweg über einen versteckten Unterordner steht weiter unten als
  Notiz.
- Der Test prüft „**höchstens** eine sichtbare Datei", nicht „genau eine":
  dass es zwischendurch keine gibt, ist der bewusst in Kauf genommene
  Zustand und darf nicht versehentlich verboten werden.

**Ziel:** Wer das Band liest, weiß danach genau, was er drücken soll und
was dann passiert — und im Programmordner liegt hinterher genau eine
Datei, nicht drei. Heute muss er beides raten.

**Was heute nicht stimmt** (`Views/MainWindow.axaml`,
`ViewModels/AktualisierungViewModel.cs`, `Core/Errors/UpdateText.cs`)

1. **Text und Knopf sagen Verschiedenes.** `UpdateText.Bereitgelegt`
   endet mit „Wer nicht warten möchte, startet die Anwendung gleich
   neu." — der Knopf daneben heißt aber **„Jetzt beenden"** und tut auch
   nur das. Die Anwendung ist danach weg, und der Anwender sitzt vor
   einem geschlossenen Programm, das er selbst wieder starten muss.
   Wer „startet gleich neu" liest und „beenden" gedrückt bekommt, hält
   das für einen Fehler.
2. **Drei Knöpfe ohne Rangfolge:** „Veröffentlichungsseite" (`secondary`),
   „Jetzt beenden" (`primary`), „Schließen" (`ghost`). Der auffälligste
   ist damit der, der die Anwendung beendet — auf einem Band, das
   gleichzeitig beteuert, es laufe alles unverändert weiter.
3. **Beim reinen Hinweis fehlt die Anleitung.** `UpdateText.NurHinweis`
   sagt „lädt die neue Fassung von der Veröffentlichungsseite und
   ersetzt die Programmdatei von Hand". Offen bleibt: *welche* der dort
   liegenden Dateien gilt für dieses System, wohin damit, und muss die
   Anwendung dafür geschlossen sein? Auf macOS ist es zudem kein
   Programm**datei**, sondern ein Bundle.
4. **„Was ist neu" wird nicht angekündigt.** Dass nach dem Neustart
   einmalig eine Seite mit den Änderungen erscheint, weiß nur, wer es
   schon erlebt hat.
5. **Der Programmordner füllt sich mit Resten, die der Anwender sieht.**
   Nach einer Aktualisierung liegen dort plötzlich mehrere Dateien mit
   fast demselben Namen, und keine davon erklärt sich. Von Paul am
   08.08.2026 gemeldet: „das ist alles verwirrend."

   Diese Dateien können entstehen (`Core/Updates/UpdateStaging.cs`,
   `UpdateDownload.cs`) — alle **neben** der Programmdatei, weil der
   Austausch ein Umbenennen auf demselben Datenträger sein muss:

   | Datei | wann | wird geräumt |
   |---|---|---|
   | `Ausgabenverwaltung.exe.teil` | während des Ladens | nach Prüfsumme bzw. im Fehlerzweig — **nie beim Start** |
   | `Ausgabenverwaltung.exe.auspacken` (Ordner) | beim Entpacken | direkt danach — **nie beim Start** |
   | `Ausgabenverwaltung.exe.neu` | geladen, geprüft, wartet | beim Austausch (umbenannt) |
   | `Ausgabenverwaltung.exe.neu.json` | Begleitzettel dazu | nach dem Austausch |
   | `Ausgabenverwaltung.exe.alt` | die bisherige Fassung | **erst beim NÄCHSTEN Start** |

   Der Hauptübeltäter ist `.alt`. Es überlebt die ganze Sitzung, in der
   ausgetauscht wurde: geräumt wird es allein von
   `UpdateInstaller.RaeumeAlteAuf`, und das ruft nur `Program.Main` beim
   *nächsten* Start (`Program.cs`, in
   `UebernehmeAktualisierungFalls`). Der Anwender arbeitet also die ganze
   Sitzung neben einer zweiten, fast gleich heißenden Datei.

   Zur Genauigkeit: die Endung liegt **hinter** `.exe`
   (`Ausgabenverwaltung.exe.alt`), die Datei lässt sich also nicht
   versehentlich per Doppelklick starten — Windows kennt `.alt` nicht. Der
   Schaden ist keine falsch gestartete Fassung, sondern Ratlosigkeit: drei
   Einträge mit demselben Namensanfang, und keiner sagt, welcher das
   Programm ist und ob man die anderen löschen darf.

   Erschwerend: `RaeumeAlteAuf` macht **einen** stillen Versuch. Der
   Nachfolger räumt unmittelbar nachdem der Vorgänger endete, und Windows
   hält das Abbild einer gerade beendeten Programmdatei noch einen
   Augenblick — der Versuch scheitert dann, `LoescheStill` schluckt es,
   und die Datei bleibt liegen. Genau das erklärt, warum die Reste nicht
   einfach nach dem nächsten Start weg sind.

   `.teil` und `.auspacken` haben **gar keine** Aufräumung beim Start: ein
   während des Ladens abgebrochenes Programm hinterlässt sie für immer.

**Vorgehen**

1. **Der Knopf startet wirklich neu.** Seit Punkt 16 gibt es
   `Core/Startup/Neustart.StarteSichSelbst()`: Nachfolger starten (er
   wartet über `--warte-auf-prozess` auf das Ende des Vorgängers), dann
   `Shutdown()`. Genau so übernimmt `UpdateInstaller` schon heute nach
   einem gelungenen Austausch. Aus „Jetzt beenden" wird damit
   **„Jetzt neu starten"**, und `JetztBeenden` wird zu `JetztNeuStarten`
   — nach dem Vorbild des gleichnamigen Kommandos in
   `DatensicherungViewModel`.
   Der Kommentar an `JetztBeenden` („Bewusst kein Selbst-Neustart …")
   fällt weg: seine Begründung war, dass der Austausch einen Prozess
   braucht, der die Programmdatei nicht mehr benutzt — und genau das
   leistet das Warten auf den Vorgänger.
   Lässt sich kein Nachfolger starten, wird **nicht** beendet, sondern
   gemeldet (dieselbe Reihenfolge wie in `DatensicherungViewModel`:
   erst starten, dann enden — sonst ist die Anwendung weg).
2. `Core/Errors/UpdateText.Bereitgelegt` umschreiben: der letzte Satz
   benennt den Knopf wörtlich und sagt, was danach passiert — „Mit
   „Jetzt neu starten" wird sie sofort übernommen; die Anwendung
   schließt sich und öffnet sich gleich wieder." Dazu der Hinweis, dass
   danach einmalig die Seite „Was ist neu" erscheint.
3. `Core/Errors/UpdateText.NurHinweis` bekommt **nummerierte Schritte**
   statt eines Satzes, und sie nennen die Datei für das laufende System.
   Dafür ist eine Angabe nötig, die es in Core schon gibt
   (`Updates/UpdateAssets` bzw. die Stelle, die das Merkmal für die
   Plattform wählt) — der Text soll „Ausgabenverwaltung-win-x64.zip"
   heißen können und nicht „die passende Datei".
   Auf macOS heißt der Schritt „Bundle ersetzen", nicht
   „Programmdatei" — die Unterscheidung steckt bereits in
   `UpdateInstaller.ZielPfad()`.
4. `Views/MainWindow.axaml`: nur **ein** hervorgehobener Knopf je
   Zustand. Bereitgelegt → „Jetzt neu starten" (`primary`), „Später"
   (`ghost`). Nur Hinweis → „Veröffentlichungsseite" (`primary`),
   „Schließen" (`ghost`). „Veröffentlichungsseite" und „Jetzt neu
   starten" tauchen nie zusammen auf: sie gehören zu verschiedenen
   Zuständen, und beide gleichzeitig zu zeigen ist der Grund, warum das
   Band wie eine Auswahl unter drei gleichwertigen Wegen aussieht.

5. **Die Reste verschwinden, bevor der Anwender sie sieht.** Vier Teile,
   die zusammengehören:

   a) **Unsichtbar machen, und zwar in dieser Reihenfolge.** Jede der fünf
      Dateien bekommt beim Anlegen das Merkmal „versteckt"
      (`File.SetAttributes` mit `FileAttributes.Hidden`) — es gibt im
      Programm bisher keine einzige Stelle, die das tut, das ist also neu.

      Entscheidend ist nicht das Verstecken allein, sondern **wann** beim
      Austausch umgeschaltet wird. `UpdateInstaller.TryUebernehmen`
      bekommt deshalb diese Abfolge (von Paul am 08.08.2026 so
      vorgegeben):

      ```
      1. exe  →  exe.alt          (umbenennen)
      2. exe.alt  versteckt       (Merkmal setzen)
      3. exe.neu  →  exe          (umbenennen; noch versteckt)
      4. exe      sichtbar        (Merkmal entfernen)
      ```

      Die Reihenfolge ist der ganze Punkt. Wer erst die neue Datei
      sichtbar macht und dann die alte versteckt, zeigt genau dazwischen
      **zwei** fast gleich heißende Programmdateien — der Zustand, den es
      abzuschaffen gilt. Andersherum entsteht ein kurzer Moment, in dem
      **gar keine** Programmdatei zu sehen ist. Das ist die bessere von
      beiden Möglichkeiten und ausdrücklich in Kauf genommen: der Moment
      dauert zwei Umbenennungen lang, liegt im Programmstart vor dem
      ersten Fenster, und niemand sieht dabei in den Ordner. „Kurz nichts"
      ist verständlich, „drei fast gleiche" nicht.

      **Zu prüfen, bevor darauf gebaut wird:** Schritt 2 setzt das Merkmal
      auf der Datei, aus der der eigene Prozess GERADE LÄUFT — das Abbild
      ist noch abgebildet. Windows lässt eine laufende Programmdatei
      umbenennen (darauf beruht der ganze Austausch), ob es auch
      `SetFileAttributes` darauf zulässt, ist eine andere Frage und hier
      nicht nachprüfbar. Schlägt es fehl, darf es den Austausch **nicht**
      abbrechen: still übergehen und auf (b)/(c) verlassen.

      **Ausweichweg, falls Schritt 2 nicht geht:** alles Vorbereitete in
      einen versteckten Unterordner neben der Programmdatei legen (etwa
      `.aktualisierung\`). Umbenennen über Ordnergrenzen hinweg bleibt auf
      demselben Datenträger unteilbar, der Vorteil aus dem Kommentar in
      `UpdateStaging` geht also nicht verloren — und ein versteckter
      Ordner löst das Problem an der Wurzel, weil dann gar nichts mehr
      neben der Programmdatei liegt. Dafür wandern die Pfade in
      `UpdateStaging` und `UpdateDownload` mit.

      **macOS:** dort greift das Merkmal nicht, das Ziel ist ein
      Bundle-Ordner, und ein führender Punkt im Namen ginge nur um den
      Preis, dass `UpdateStaging.PruefDatei` ihn nicht mehr findet. Also:
      Windows über das Merkmal, macOS über das schnelle Räumen unten.

   b) **Räumen mit Wiederholung statt einem stillen Versuch.**
      `RaeumeAlteAuf` versucht es mehrmals mit kurzer Pause (etwa fünf
      Versuche über eine Sekunde). Die Sperre nach einem Prozessende ist
      flüchtig; ein einziger Versuch trifft genau in sie hinein. Danach
      immer noch still — aber dann liegt der Rest wirklich nur im
      Ausnahmefall da, und wegen (a) sieht ihn niemand.

   c) **Alles räumen, nicht nur `.alt`.** Beim Start einmal über alle fünf
      Endungen gehen, damit auch ein abgebrochenes Laden (`.teil`,
      `.auspacken`) nicht für immer liegen bleibt. Eine Methode
      `UpdateStaging.RaeumeAlleReste(zielPfad)` statt der heutigen
      Einzelfall-Aufrufe — dann fällt beim Hinzufügen einer sechsten
      Endung auf, dass sie dort hineingehört.

   d) **Der Knopf im Band übernimmt das Räumen — soweit er kann.** Wichtig
      und nicht offensichtlich: **beim Druck auf „Jetzt neu starten" gibt
      es `.alt` noch gar nicht.** Es entsteht erst im Nachfolger, beim
      Austausch selbst. Wer die Aufräumung dort sucht, sucht an der
      falschen Stelle. Zu tun ist:
      - im **Nachfolger**, direkt nach dem Austausch und **vor** dem
        Öffnen des Fensters, räumen (b) + (c);
      - der Nachfolger wartet über `--warte-auf-prozess` ohnehin auf das
        Ende des Vorgängers (`Core/Startup/Neustart`), das Räumen liegt
        also nach dem einzigen Zeitpunkt, an dem es überhaupt gehen kann.

      Der Selbstneustart aus Schritt 1 **verschärft das**: Vorgänger und
      Nachfolger folgen jetzt in Millisekunden aufeinander statt in der
      Zeit, die ein Anwender zum Neustarten braucht. Die flüchtige Sperre
      wird damit wahrscheinlicher, nicht seltener — beide Änderungen
      gehören deshalb in denselben Punkt.

**Tests**
- `MeldungsGrundsaetzeTests` erfasst die geänderten Texte weiterhin
  (`Update/Bereitgelegt`, `Update/Hinweis/*`) — die neuen Fassungen
  müssen die vier Theories bestehen.
- `UpdateUebernahmeTests` erweitern: nach einem gelungenen Austausch
  liegt **keine** der fünf Endungen mehr im Ordner. Der Test soll die
  Endungen aus einer Liste in `UpdateStaging` beziehen und nicht selbst
  aufzählen, sonst prüft er beim Hinzufügen einer sechsten nichts mehr.
- Ein Test für die Wiederholung: eine Datei, die beim ersten Versuch
  gesperrt ist (unter Windows über einen offenen `FileStream` ohne
  `FileShare.Delete` herstellbar) und danach freigegeben wird, ist
  hinterher weg. `WindowsOnlyFactAttribute` gibt es dafür schon.
- Das Merkmal „versteckt" ebenfalls nur unter Windows prüfen — und dabei
  ausdrücklich die **Reihenfolge**: zu keinem Zeitpunkt des Austauschs
  dürfen zwei sichtbare Programmdateien nebeneinander liegen. Prüfbar,
  indem `TryUebernehmen` in Schritten aufgerufen bzw. nach jedem Schritt
  gezählt wird, wie viele nicht versteckte Einträge mit dem Namensanfang
  im Ordner stehen: nie mehr als eins. Dass es zwischendurch **null**
  sein darf, gehört mit in die Zusicherung — sonst nagelt der Test
  versehentlich das Gegenteil fest.
- Neu in `UpdateEntscheidungTests` oder einer eigenen Datei: der Text zu
  `UpdateHindernis.KeineDateiFuerDiesesSystem` nennt keinen Dateinamen
  (es gibt keinen), die beiden übrigen Hindernisse nennen einen.
- Ein Test, der Band-Text und Knopfbeschriftung gegeneinander hält, ist
  nicht möglich (die Beschriftung steht in `.axaml`). Stattdessen prüft
  ein Test, dass `Bereitgelegt` die Wörter „Jetzt neu starten" enthält —
  wird der Knopf umbenannt, fällt der Text auf.

**Fertig, wenn:** Das Band nennt genau einen empfohlenen Schritt, der
Knopf tut, was der Text ankündigt, der Neustart bringt die Anwendung von
selbst zurück, und die Anleitung von Hand nennt Dateinamen und
Reihenfolge. **Und:** nach einer Aktualisierung liegt im Programmordner
genau eine Programmdatei — kein `.alt`, kein `.neu`, kein `.neu.json`,
kein `.teil`, kein `.auspacken`. Nachzusehen ist das im Ordner selbst,
mit eingeschalteter Anzeige versteckter Dateien: „nicht zu sehen" genügt
nicht, es soll wirklich weg sein. Das Verstecken ist das Netz für den
Ausnahmefall, nicht die Lösung — wer es zur Lösung macht, sammelt
unsichtbare Reste an, und die sind schlimmer als sichtbare, weil sie
niemand mehr findet.

---

### [x] 22. Auf macOS heißt die Anwendung „Avalonia"

**Erledigt in:** Der Anwendungsname stimmt auf macOS, die Filterleiste bricht um

**Umsetzung:** `Name="Ausgabenverwaltung"` am `<Application>`-Element, mit
Kommentar. Der vorgeschlagene Test, der `Application.Current.Name` nach
dem Laden liest, **geht nicht**: dafür müsste App.axaml geladen werden,
und das verlangt eine Fensterplattform (`Unable to locate
'Avalonia.Platform.ICursorFactory'`); auch der Umweg über
`AssetLoader` scheitert daran. Möglich wäre es nur mit
`Avalonia.Headless` als zusätzlicher Abhängigkeit der Testbaugruppe — für
eine einzelne Angabe zu viel. `AnwendungsnameTests` prüft deshalb das
**Attribut am Wurzelelement der XAML-Quelle** (nicht bloß einen Textfund),
und ein zweiter Test hält daneben fest, dass der Fenstertitel etwas
anderes ist und nie falsch war.

**Ziel:** Die Anwendung heißt überall „Ausgabenverwaltung" — auch im
Menü oben links neben dem Apfel.

**Ausgangslage:** Auf macOS steht dort heute der Name des
Oberflächen-Baukastens statt der des Programms. Es ist **nicht** der
Fenstertitel und **nicht** das Bundle: `Views/MainWindow.axaml` trägt
`Title="Ausgabenverwaltung"`, und `publish.ps1` schreibt eine korrekte
`Info.plist` mit `CFBundleName`/`CFBundleDisplayName`. Was fehlt, ist
`Avalonia.Application.Name` — ohne diese Angabe setzt Avalonia den
macOS-Anwendungsnamen auf seinen eigenen Vorgabewert.

**Vorgehen**

1. `App.axaml`: am `<Application>`-Element `Name="Ausgabenverwaltung"`
   ergänzen (die Eigenschaft heißt `Application.Name`, gestützt in
   Avalonia 12.1). Ein Kommentar dazu, wofür sie gilt — sie ist auf
   Windows und Linux unsichtbar, und ohne Notiz entfernt sie später
   jemand als scheinbar wirkungslos.
2. Prüfen, ob dieselbe Vorgabe auch im **Fehlerfenster** greift:
   `Views/StartupErrorWindow.axaml` läuft, bevor irgendetwas anderes
   steht, benutzt aber dieselbe `Application`.
3. Nicht Teil des Punktes: Symbol und Bundle-Angaben. Die stimmen.

**Prüfen:** Auf macOS nicht mit `dotnet build` zu sehen — die Angabe
wirkt erst im laufenden Fenster. Nachweisbar ist sie deshalb über einen
Test, der `Application.Current.Name` nach dem Laden von `App.axaml`
liest, oder schlicht über den nächsten Veröffentlichungslauf. Der Test
ist vorzuziehen: er hält die Angabe fest, ohne einen Mac zu brauchen.

**Fertig, wenn:** `Application.Name` ist gesetzt, ein Test hält den Wert
fest, und der Kommentar erklärt, warum die Zeile auf dem
Entwicklungsrechner nichts zu tun scheint.

---

### [x] 23. Die Filterleiste bricht nicht um und läuft aus ihrer Karte heraus

**Erledigt in:** Der Anwendungsname stimmt auf macOS, die Filterleiste bricht um
**Nachgebessert in:** Die Filterleiste klappt ein und zeigt ihre Filter als Chips

**Nachtrag vom 08.08.2026 — Umbrechen allein genügte nicht.** Paul: „Das
sieht schrecklich aus, wenn man es kleinzieht." Zu Recht: acht Gruppen sehr
unterschiedlicher Breite (110 bis 330) und Höhe (ein Datumsfeld gegen vier
Schnellwahlknöpfe) ergeben beim Umbrechen eine ausgefranste Treppe — und
acht Gruppen untereinander nehmen die halbe Seite ein, die Liste rutscht
aus dem Blick. Ein reiner Layout-Umbau löst das zweite Problem nicht.

Recherchierte Muster:

- **Filterleiste + Überlauf hinter einem Knopf + Chips der angewandten
  Filter darunter** — HashiCorps Helios beschreibt genau diese
  Dreiteilung ([Filter patterns](https://helios.hashicorp.design/patterns/filter-patterns))
- **Priority+** — was hineinpasst bleibt, der Rest wandert in ein
  „Mehr"-Menü, statt umzubrechen ([CSS-Tricks](https://css-tricks.com/the-priority-navigation-pattern/))
- **Angewandte Filter über den Ergebnissen zeigen**, statt sie beim
  Einklappen verschwinden zu lassen
  ([Smart Interface Design Patterns](https://smart-interface-design-patterns.com/articles/filtering-ux/))

Umgesetzt ist das erste, von Paul gewählt:

- `Core/Display/Filterleiste.cs` entscheidet anhand der Breite, ob
  aufgeklappt bleibt (Schwelle 520). Die Ansicht meldet nur die Messung
  (Regel 7). **Sobald der Anwender selbst umschaltet, gilt seine
  Entscheidung** — ein Umschalten, das gleich wieder zurückspringt, ist
  schlimmer als gar keines.
- `Core/Reports/FilterChips.cs` baut aus dem Filterzustand die Chips. In
  Core, weil beide Leisten dieselben Zustände gleich benennen müssen —
  dieselbe Begründung wie bei `FilterCaption`.
- **Die Chips sind die Bedingung dafür, dass die Leiste verschwinden
  darf.** Ohne sie wäre das Einklappen ein Verstecken, und genau das ist
  der in Punkt 2 benannte häufigste Grund für „meine Buchungen sind weg".
  Jeder Chip hebt genau seinen Filter auf.
- Zwei Feinheiten: beide Status-Häkchen zusammen ergeben **keinen** Chip
  (sie schränken nicht ein), und der Vorgabezeitraum ebenfalls nicht (er
  ist der Ausgangspunkt, kein Filter) — sonst trüge der Knopf nie die
  Zahl null.
- Die **Gruppierung** der Auswertung bekommt bewusst keinen Chip: sie
  schränkt nichts ein, und „Gruppierung aufheben" ergibt keinen Sinn.
- Das **Suchfeld** ist in die immer sichtbare Zeile gewandert — das am
  häufigsten benutzte Feld soll nicht hinter einem Klick liegen. Strg+F
  trifft es dort unverändert.

**Weiterhin nur in der laufenden Anwendung zu beurteilen:** wie es
aussieht. Die Schwelle 520 ist gerechnet, nicht gesehen.

**Ursprüngliche Umsetzung:**

- Beide Reihen in beiden Ansichten sind jetzt `WrapPanel` mit
  `ItemSpacing`/`LineSpacing` — die gibt es in Avalonia 12.1, nachgesehen
  statt vermutet, sonst hätte es Ränder an jedem Kind gebraucht.
- **Zwei Reihen bleiben zwei Reihen.** Der Punkt schlug vor zu prüfen, ob
  ein einziges `WrapPanel` über alle Gruppen ruhiger wäre — ist es nicht:
  bei breitem Fenster stünden Zeitraum und Sachfilter in einer langen
  Reihe durcheinander, und die gewachsene Aufteilung ginge verloren. Der
  Überlauf entsteht *innerhalb* einer Reihe, also genügt es, wenn jede für
  sich umbricht.
- **Die festen Breiten bleiben.** Der Vorschlag, `Width` der
  Kategorienauswahl zu einer `MaxWidth` zu machen, wäre ein Rückschritt:
  ohne feste Breite bemisst sich der Knopf am aktuellen Text („Alle
  Kategorien" gegen „3 Kategorien") und änderte seine Breite bei jeder
  Auswahl. Nötig ist es auch nicht — die breiteste Gruppe misst 260, und
  das Fenster ist mindestens 800 breit (Punkt 18).
- Ein Kommentar, der das Höhenverhalten des waagerechten StackPanels
  erklärte, ist mitgezogen worden: er beschrieb ein Steuerelement, das
  dort nicht mehr steht. Das Verhalten selbst bleibt, `WrapPanel` dehnt
  seine Kinder ebenso auf die Höhe der höchsten je Reihe.

**Nicht nachweisbar mit `dotnet test`:** reines Layoutverhalten. Zu prüfen
in der laufenden Anwendung, in drei Fensterbreiten und zusätzlich bei
Schriftstufe „Sehr groß".

**Ziel:** Die Filter bleiben in ihrer Karte, egal wie schmal das Fenster
ist — und wenn sie nicht mehr nebeneinander passen, stehen sie
untereinander.

**Was heute nicht stimmt** (`Views/AusgabenlisteView.axaml`,
`Views/ReportView.axaml`)

Beide Ansichten tragen ihre Filter in einem `Border Classes="filterleiste"`
— einer Karte mit Rahmen, Hintergrund und abgerundeten Ecken. Darin
liegen die Filter in **waagerechten `StackPanel`s** (Zeitraum in der
ersten Reihe, Kategorie/Zahler/Status/Art/Sicht/Suche in der zweiten).

Ein waagerechtes `StackPanel` bricht nicht um und beschneidet nichts: es
misst seine Kinder mit unbegrenzter Breite und stellt sie in eine Reihe.
Wird das Fenster schmaler, wandern die hinteren Filter deshalb **über
den rechten Rand der Karte hinaus** ins Nichts — der Rahmen endet, die
Bedienelemente laufen weiter. `Border.filterleiste` setzt kein
`ClipToBounds`, das Überstehende bleibt also sichtbar und sieht aus wie
ein Anzeigefehler.

Dazu kommt die Ungleichheit zur Tabelle darunter: **die** liegt in einem
waagerechten Bildlauf (`x:Name="Waagerecht"`), lässt sich also nach
rechts rollen, bis alles zu sehen war. Für die Filterleiste gibt es
diesen Weg nicht — was rechts heraushängt, ist nicht erreichbar.

**Vorgehen**

1. **`WrapPanel` statt waagerechtem `StackPanel`** für die Filterreihen.
   Das ist im Programm die etablierte Antwort auf genau diese Frage: die
   Schnellwahl-Chips der Erfassungsmaske (Punkt 8) und die
   Sammelaktionsleiste der Ausgabenliste benutzen sie schon, dort mit
   derselben Begründung — bei großer Schriftstufe muss umgebrochen
   werden dürfen (Regel 9). Ein schmales Fenster ist derselbe Fall,
   nur aus der anderen Richtung.
2. Die zusammengehörenden Teile dürfen dabei **nicht auseinanderreißen**:
   „Von"+„Bis" gehören zusammen, die vier Schnellwahl-Knöpfe gehören
   zusammen. Also bleibt jede Gruppe ein eigener Behälter mit
   Beschriftung, und umgebrochen wird zwischen den Gruppen, nicht in
   ihnen. Die Raster „Beschriftung, Rest" der Zeitraumspalten bleiben
   unverändert — der Kommentar dort erklärt, warum sie kein
   `StackPanel` sind, und dieser Grund gilt weiter.
3. **Beide Reihen können zu einer werden.** Wenn ohnehin umgebrochen
   wird, ist die Trennung in zwei feste Reihen keine Hilfe mehr, sondern
   verhindert nur, dass der Platz einer halb leeren Reihe genutzt wird.
   Prüfen, ob ein einziges `WrapPanel` über alle Filtergruppen das
   ruhigere Ergebnis ist — dann fällt auch die Frage weg, in welcher
   Reihe ein neuer Filter landet.
4. **Mindestbreite statt Überlauf.** Auch ein `WrapPanel` hat eine
   Untergrenze: die breiteste einzelne Gruppe (die Kategorienauswahl mit
   `{anzeige:Breite 260}`). Darunter muss die Karte selbst in den
   waagerechten Bildlauf, damit nichts unerreichbar wird — oder die
   feste Breite dieser Gruppe wird zu einer `MaxWidth`, damit sie
   mitschrumpfen kann. Der zweite Weg ist der bessere: ein Fenster, das
   man schmaler zieht, soll nicht plötzlich zwei Bildlaufleisten
   bekommen.
5. **Beide Ansichten gleich behandeln.** Die Filterleiste der Auswertung
   ist bis auf Kleinigkeiten dieselbe wie die der Ausgabenliste; was
   hier gilt, gilt dort. Wenn dabei auffällt, dass beide dieselbe
   Bauform mit denselben Bindungsnamen tragen, ist ein gemeinsames
   Steuerelement in `Anzeige/` der nächste Schritt — aber nur, wenn sich
   die Unterschiede tatsächlich auf Beschriftungen beschränken. Sonst
   bleibt es bei zwei Dateien.

**Prüfen:** Mit `dotnet build`/`dotnet test` **nicht** nachweisbar — das
ist reines Layoutverhalten. Nachzusehen ist es in der laufenden
Anwendung, und zwar in drei Breiten (breit, mittel, so schmal wie
möglich) und zusätzlich bei Schriftstufe „Sehr groß": beide Größen
wirken auf denselben Umbruch, und ein Fehler zeigt sich oft nur in einer
der beiden Richtungen.

**Fertig, wenn:** In jeder Fensterbreite und jeder Schriftstufe steht
kein Filter außerhalb seiner Karte, zusammengehörende Filter bleiben
beieinander, und es gibt keinen Zustand, in dem ein Filter zwar da, aber
nicht erreichbar ist.

---

## Batch 6 — Gespeicherte Filter (Punkt 24)

Nicht vorab geplant, sondern aus einem Vorschlag entstanden: nach Punkt 23
gab es Filterchips, aber keine Möglichkeit, eine wiederkehrende Frage
festzuhalten.

### [x] 24. Filter benennen und wieder aufrufen

**Erledigt in:** Filter lassen sich benennen und wieder aufrufen

Paul: „Ich stelle mir das so vor, dass ich irgendwo Speichern drücken kann
in der Filterliste und dann einen Namen vergebe und es ein kleines Dropdown
mit meinen Filtern gibt, die ich dann anklicken kann."

Umgesetzt:

- `Core/Reports/SavedFilter.cs` hält den Stand der Leiste **roh** — die
  angehakten Kategorie-Ids, nicht die daraus abgeleiteten Äste und
  Ausschlüsse. Nur so lassen sich die Häkchen wieder hinlegen.
- `Core/Reports/SavedFilters.cs` trägt die Regeln: Namensprüfung,
  Ersetzen bei gleichem Namen, Löschen, Sortierung nach de-DE, Obergrenzen
  (20 Filter, 40 Zeichen) und `Normalize` für eine von Hand verbogene
  Einstellungsdatei.
- **Der Zeitraum wird als Schlüssel gespeichert**, wenn er über die
  Schnellwahl kam (`PeriodKey`), sonst als festes Datum. „Dieses Jahr"
  meint sonst ab Januar plötzlich das falsche Jahr. Damit ein in der
  Auswertung gespeicherter Filter („Letzte 3 Jahre") in der Ausgabenliste
  nicht stillschweigend zu „alles" wird, löst `DateRangePresets.TryByKey`
  die Schlüssel **beider** Leisten auf.
- Die Liste liegt in `settings.json` und wird von beiden Bereichen geteilt.
  Bewusst nicht in der Datenbank: ein gespeicherter Filter ist eine
  Gewohnheit und kein Datenbestand.
- Oberfläche: Knopf „Filter ▾" in der immer sichtbaren Zeile, dahinter die
  Liste zum Anklicken, das ✕ zum Löschen und darunter das Namensfeld.

**Nachgebessert am selben Tag**, nachdem Paul es gesehen hat:

- Der Knopf hieß erst „Gespeichert" — jetzt „Filter ▾".
- Der Umschaltknopf „Filter (3)" ist ein **Pfeil am rechten Ende** der
  Zeile: aufgeklappt nach oben, eingeklappt nach unten (Carbon Design
  System, [Accordion](https://carbondesignsystem.com/components/accordion/usage/);
  ebenso GitLab und eBay). Die Anzahl gesetzter Filter steht in seinem
  Hinweis, damit sie nicht ersatzlos verschwindet.
- Aufklappfenster stießen bei großer Schrift an den festen Deckel des
  Fluent-Themas (`FlyoutThemeMaxWidth` = 456) und bekamen eine waagerechte
  Bildlaufleiste — am deutlichsten die Erklärung im Jahresrückblick
  (340 × 1,4 = 476). Der Deckel wächst jetzt mit (`App.axaml`).

**Nebenbei gefunden und behoben:** `KategorienView.Farbe_Gewaehlt` rief
`FarbAuswahl.Flyout?.Hide()`. Der Name steht in der **Zeilenvorlage** und
gehört damit deren Namensraum — das Feld war immer `null`, jeder
Farbwechsel endete in einer `NullReferenceException`, nachdem die Farbe
längst gespeichert war. Der Weg führt jetzt vom angeklickten Farbfeld nach
oben aus dem Popup heraus.

**Geprüft:** `dotnet build`/`dotnet test` (21 neue Tests in
`GespeicherteFilterTests`, dazu der Pfeil in `LeerzustandTests`) und drei
Durchläufe durch Paul: Speichern und Löschen sind in `settings.json`
nachweisbar, der Farbwechsel im Protokoll ohne Ausnahme.
