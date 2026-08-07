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

### [ ] 11. Sammelaktionen in der Ausgabenliste

**Ziel:** Was heute nur fürs Löschen geht, geht auch fürs Ändern.

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

### [ ] 12. Rückgängig statt Löschbestätigung
Das Bestätigungsband beim Löschen von Buchungen durch ein Undo-Band
ersetzen („3 Buchungen gelöscht · Rückgängig"). Gelöschte Zeilen bis zum
Bereichswechsel im Speicher halten und bei „Rückgängig" über
`Create` neu anlegen. Passt zum recherchierten Muster: umkehrbar machen
schlägt nachfragen. Vorlagen-Löschen (Punkt 4) behält seinen Dialog — das
Schadensausmaß ist größer.

### [ ] 13. Tastaturkürzel und eine Übersicht dazu
`Strg+N` erfassen, `Strg+S`/`Strg+Enter` speichern, `Esc` Dialog
schließen, `Strg+F` Suchfeld, `Strg+1..9` Bereichswechsel, `F1`
Kürzelübersicht. Definition an **einer** Stelle
(`Anzeige/Tastenkuerzel.cs`), damit die Übersichtsseite sich daraus
erzeugt und nicht auseinanderläuft.

### [ ] 14. Kontextmenü auf Listenzeilen
Rechtsklick auf eine Zeile in Ausgabenliste, Offene Posten und Vorlagen:
Bearbeiten, Duplizieren, Als Vorlage, Als beglichen, Löschen. Nimmt Druck
von der Aktionsspalte, die sonst mit jedem neuen Punkt breiter wird.

### [ ] 15. Suche über Bemerkung, Kategorie und Zahler
Heute durchsucht `ReportFilter.SearchText` nur `Note`. Auf
Kategoriepfad und Zahlername ausweiten — in `ReportFilterSql`, mit
sichtbarem `OR`. Der Platzhaltertext im Suchfeld muss das sagen.

### [ ] 16. Wiederherstellen aus der Anwendung
Der Nachfolger von Punkt 6. Geführter Ablauf: Sicherung wählen → prüfen
(`BackupVerification`) → **Folgen beziffern** („Die aktive Datenbank
enthält 1 284 Buchungen, diese Sicherung 1 190 — 94 Buchungen wären
danach weg") → aktive Datenbank vorher unter neuem Namen sichern →
ersetzen → Neustart anfordern. Eingabebestätigung des Dateinamens, wie
bei Aktionen mit weiterem Wirkungskreis üblich. Erst umsetzen, wenn
Punkt 6 steht.

### [ ] 17. CSV-Export der Ausgabenliste
`Core/Reports/ReportCsv.cs` hat das Muster bereits (`de-DE`, Semikolon,
`EuroText.Plain`). Zweite Methode für die flache Buchungsliste, Knopf in
der Filterleiste. Exportiert wird, was gefiltert ist — nicht alles.

### [ ] 18. Fenstergröße, -position und Sortierung merken
Nach `AppSettings` (dort steht `CategoryColumnWidth` schon). Beim Start
prüfen, ob die Position noch auf einem vorhandenen Bildschirm liegt —
sonst zentriert öffnen.

### [ ] 19. Leerzustände mit Handlungsangebot
Überall dort, wo heute nur ein grauer Satz steht („Noch keine Sicherung
vorhanden", „Keine offenen Posten", leere Ausgabenliste): Satz plus der
Knopf, der den Zustand auflöst. Die Datensicherung macht das an einer
Stelle schon vor.

### [ ] 20. „Was ist neu" nach einem Update
`AktualisierungViewModel` weiß, wann eine neue Fassung übernommen wurde.
Beim ersten Start danach eine Seite mit den Änderungen zeigen, gespeist
aus dem Text des GitHub-Releases, den `GitHubReleases` ohnehin schon
liest.
