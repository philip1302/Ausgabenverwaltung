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

- Die Ausgabenliste lässt sich als CSV-Datei speichern — „Als CSV
  exportieren" über der Tabelle. Exportiert wird genau das, was gerade in
  der Liste steht: der Filter gilt also mit. Die Datei öffnet sich in Excel
  mit einem Doppelklick, ohne Import-Assistent; Ausgaben stehen negativ,
  Einnahmen positiv, sodass sich über die Betragsspalte rechnen lässt.
  Neben Datum, Betrag, Kategorie und Zahler stehen auch Art, Beglichen-Datum,
  Bemerkung und die Vorlage, aus der eine Buchung stammt.
- Eine Sicherung lässt sich jetzt aus der Anwendung heraus wieder
  einspielen, ohne Umweg über den Datei-Explorer. „Wiederherstellen" an
  einer Sicherungszeile prüft zuerst die Datei und sagt dann, was das
  Einspielen kostet: „Die aktive Datenbank enthält 1.284 Buchungen, diese
  Sicherung 1.190 — 94 Buchungen wären danach weg." Bestätigt wird durch
  Abtippen des Dateinamens; das ist Absicht, denn es ist die einzige Stelle,
  an der der gesamte Datenbestand ausgetauscht wird. Die bisherige Datenbank
  wird vorher unter eigenem Namen gesichert und bleibt liegen, der Weg
  zurück bleibt also offen. Eingespielt wird beim anschließenden Neustart,
  weil die Datei im laufenden Betrieb geöffnet ist — ein Knopf im Band
  erledigt ihn. Eine beschädigte Sicherung und eine aus einer neueren
  Programmfassung werden gar nicht erst angeboten.
- Die Suche in der Ausgabenliste und in der Auswertung durchsucht jetzt
  nicht mehr nur die Bemerkung, sondern auch Kategorie und Zahler. „Strom"
  findet damit die Buchungen der Kategorie Strom, auch wenn niemand das
  Wort in die Bemerkung geschrieben hat, und „Anna" findet alles, was Anna
  bezahlt hat. Bei der Kategorie zählt der ganze Pfad: „Wohnen" findet auch
  die Buchung unter „Wohnen › Nebenkosten › Strom". Buchungen ohne
  Bemerkung fielen bei einer Suche bisher immer heraus — jetzt sind sie über
  Kategorie und Zahler zu finden.
- Ein Rechtsklick auf eine Zeile öffnet jetzt ein Menü mit allem, was sich
  mit ihr anstellen lässt — in der Ausgabenliste, bei den offenen Posten
  und bei den wiederkehrenden Ausgaben. In der Ausgabenliste kommt damit
  auch das Abhaken einer einzelnen Buchung dazu, ohne sie vorher zu
  markieren. Weil das Menü alles trägt, stehen in der Aktionsspalte nur
  noch „Bearbeiten" und „Löschen": „Duplizieren" ist ins Menü gewandert
  und die Spalte um 100 Pixel schmaler geworden — die gewinnt die
  Bemerkung dazu.
- Die Anwendung lässt sich jetzt weitgehend über die Tastatur bedienen:
  Strg + N öffnet die Erfassungsmaske, Strg + F springt in die Suche der
  Ausgabenliste, Strg + 1 bis Strg + 9 wechseln in den Bereich an dieser
  Stelle der Seitenleiste. In Formularen speichert Strg + S oder
  Strg + Eingabe, Esc schließt ohne zu speichern. Welche Taste was tut,
  zeigt F1 auf einer eigenen Seite — damit man es nicht raten muss.
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
