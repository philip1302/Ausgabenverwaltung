# Was ist neu

Diese Datei ist der Text, den die Anwendung selbst anzeigt: nach einer
Aktualisierung erscheint einmalig der Abschnitt der neuen Fassung im
Bereich „Was ist neu". Sie wird in die Anwendung eingebaut und braucht
dafür keine Internetverbindung.

Sie richtet sich deshalb an den **Anwender**, nicht an den Entwickler.
Wie sie zu schreiben ist und wann, steht in `CLAUDE.md`, Regel 15. Kurz:
bei jeder Änderung, die jemand beim Benutzen merkt, im selben Commit, in
ganzen Sätzen.

Ganz oben sammelt „Unveröffentlicht", was noch in keiner Fassung steckt;
beim Anheben der Versionsnummer wird diese Überschrift zur Fassung.

## Unveröffentlicht

- Das Löschen einer Buchung fragt nicht mehr nach, sondern lässt sich
  zurücknehmen: die Zeile verschwindet sofort, darüber steht „Buchung
  gelöscht · Rückgängig". Ein Druck darauf legt alles wieder an — Betrag,
  Datum, Kategorie, Zahler, Bemerkung und auch die Zuordnung zu einer
  Vorlage. Das Angebot bleibt stehen, bis der Bereich gewechselt wird;
  eine Nachfrage, die man dreimal am Tag wegklickt, schützt ohnehin
  niemanden mehr. Vorlagen werden weiterhin erst nach Rückfrage gelöscht.
- Die Seite „Was ist neu" zeigt jetzt tatsächlich die Änderungen: sie
  liest sie aus der Änderungsliste, die die Anwendung selbst mitbringt.
  Vorher hing sie am Beschreibungstext der Veröffentlichung im Netz —
  und der bestand bei den letzten Fassungen nur aus einem Verweis.
- Wer eine Fassung überspringt, bekommt beim nächsten Start die
  Änderungen aller übersprungenen Fassungen zu sehen, nicht nur die der
  neuesten.

## 1.4.0 — 07.08.2026

### Weniger Tipparbeit beim Erfassen

- Das Betragsfeld rechnet jetzt. „12,50+3,20" wird zu 15,70 €, „40-2,50"
  zu 37,50 € — drei Kassenzettel lassen sich also zusammenzählen, ohne
  vorher zum Taschenrechner zu greifen.
- Das Datumsfeld versteht Kurzformen: „heute", „gestern", „vorgestern",
  „-3" für vor drei Tagen und „15." für den 15. des Monats. Nach dem
  Verlassen des Feldes steht das vollständige Datum da, damit sichtbar
  ist, was verstanden wurde.
- Wer mehrere Belege am Stück erfasst, hakt „Werte behalten" an: Kategorie,
  Zahler und Datum bleiben nach dem Speichern stehen, geleert werden nur
  Betrag und Bemerkung.
- Über dem Kategoriefeld stehen die fünf Kategorien, die zuletzt am
  häufigsten gebraucht wurden — ein Klick statt einer Auswahl im Baum.
- Wird eine Bemerkung getippt, die es schon einmal gab, bietet die Maske
  die Werte der letzten gleichlautenden Buchung an. Übernommen wird erst
  auf Klick, das Datum bleibt unberührt.

### Buchungen ändern und wiederverwenden

- Eine Buchung lässt sich duplizieren: gleiche Werte, heutiges Datum.
- Mehrere markierte Buchungen lassen sich gemeinsam ändern — Kategorie,
  Zahler oder „beglichen" in einem Rutsch statt Zeile für Zeile.
- Aus einer bestehenden Buchung lässt sich eine wiederkehrende Ausgabe
  anlegen („das kommt jeden Monat"). Der Rechtsklick auf die Zeile führt
  hin.

### Wiederkehrende Ausgaben

- Beim Löschen einer Vorlage wird gefragt, was mit den bereits daraus
  erzeugten Buchungen geschehen soll: behalten oder mitlöschen. Vorher
  wird automatisch gesichert.
- Eine geänderte Vorlage kann die Änderung auf Wunsch auch auf die schon
  erzeugten Buchungen übertragen — ausdrücklich anzuhaken, jedes Mal neu.
  Datum und Beglichen-Status bleiben dabei unangetastet.

### Startseite

- Die Kacheln führen jetzt dorthin, wo ihre Zahl herkommt: „Ausgaben
  diesen Monat" öffnet die passend gefilterte Liste, „Offene Posten" den
  gleichnamigen Bereich, „Nächste Fälligkeit" die zugehörige Vorlage.
- Ein Klick auf eine Zeile unter „Letzte Buchungen" zeigt genau diese eine
  Buchung.

### Datensicherung

- Die Seite beantwortet jetzt eine einzige Frage — „bin ich abgesichert?"
  — statt fünf Teilaussagen nebeneinanderzustellen.
- Jede Sicherung lässt sich prüfen: die Anwendung öffnet sie und meldet,
  ob sie lesbar ist und wie viele Buchungen darin stehen. Eine Sicherung
  ist nur so viel wert wie das, was sich aus ihr wiederherstellen lässt.
- Ein versehentlich entferntes zweites Sicherungsziel lässt sich sofort
  zurückholen.

### Sonstiges

- Nach einer Aktualisierung zeigt die Anwendung einmalig, was sich
  geändert hat.
