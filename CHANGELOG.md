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

### Auswertung

- Die Auswertung hinterlegt ihre Werte jetzt nach Höhe: je größer der Betrag,
  desto kräftiger die Fläche hinter der Zahl. Damit lässt sich eine volle
  Kreuztabelle überfliegen, statt sie Zeile für Zeile lesen zu müssen — wo viel
  liegt, sieht man, bevor man eine einzige Zahl gelesen hat. Die Beträge stehen
  unverändert da; die Farbe sagt nichts, was nicht auch dastünde.
- Eingefärbt wird nach der Reihenfolge der Beträge und nicht nach ihrem
  Abstand. Sonst bekäme in einem Haushalt mit einer Jahresmiete diese eine Zelle
  den kräftigsten Ton und alle übrigen denselben blassen — die Einfärbung sähe
  aus wie ein Fehler und sagte nichts mehr.
- Zellen, in denen die Einnahmen überwiegen, bleiben wie bisher grün und ohne
  Hinterlegung, ebenso die Summenzeile und die Summenspalte: sie sind
  Rechnungen über die übrigen Zellen und stünden sonst immer ganz oben.
- Wem das zu unruhig ist, schaltet es über „Werte einfärben“ über der Tabelle
  ab. Die Anwendung merkt sich das.

### Jahresrückblick

- Über den Karten steht jetzt der Monatsverlauf beider Jahre: für jeden Monat
  ein Balken für das laufende Jahr, davor der des Vorjahres in Grau. Damit ist
  auf einen Blick zu sehen, in welchen Monaten es teurer wurde und in welchen
  nicht — bisher stand die Veränderung nur als eine Zahl für das ganze Jahr da,
  und die verschweigt, ob sich etwas dauerhaft verschoben hat oder ein einziger
  Monat aus der Reihe fällt.
- Die beiden Jahre teilen sich eine Skala, ein doppelt so hoher Balken ist also
  auch der doppelte Betrag. Ein Klick auf einen Balken führt in die Buchungen
  genau dieses Monats.
- Unter „Auswertung“ gibt es jetzt den Jahresrückblick. Er stellt zwei Jahre
  nebeneinander und rechnet selbst aus, was sich am stärksten verändert hat.
  Ganz oben stehen Ausgaben, Einnahmen und was unterm Strich übrig blieb,
  jeweils mit dem Vorjahreswert und dem Unterschied daneben.
- Darunter benennt der Rückblick die auffälligsten Punkte auf je einer
  Karte: wofür deutlich mehr oder weniger ausgegeben wurde, was neu
  hinzugekommen oder ganz weggefallen ist, welcher Posten der größte war, wo
  sich viele kleine Beträge summieren und welcher Monat der teuerste und
  welcher der ruhigste war.
- Auf der Karte stehen nur Überschrift, Name und Betrag — dafür sind alle
  Karten gleich groß und lassen sich nebeneinander überfliegen. Das kleine
  Fragezeichen an der Karte öffnet den Rest: den ganzen Satz dazu, die Zahlen
  im Einzelnen (beide Jahre, der Unterschied in Euro und Prozent, der Anteil
  am Jahr, die Zahl der Buchungen und der Schnitt je Buchung) und zum Schluss
  einen Satz dazu, wonach für diese Karte überhaupt gesucht wurde. Ohne den
  liest sich ein Rückblick wie ein Orakel: richtig gerechnet, aber nicht
  nachvollziehbar.
- Kleine Ausschläge auf kleiner Grundlage bleiben dabei außen vor. Eine
  Ausgabe, die von 2 € auf 20 € steigt, sieht prozentual gewaltig aus, ist
  aber kein Jahresereignis — solche Zahlen würden sonst jede Liste anführen
  und das Wesentliche verdecken.
- Steckt derselbe Anstieg zugleich in einer Ober- und einer Unterkategorie,
  nennt der Rückblick nur die genauere von beiden. Sonst stünde dieselbe
  Veränderung zwei- oder dreimal untereinander.
- Läuft das Jahr noch, vergleicht der Rückblick von sich aus nur den gleichen
  Zeitraum des Vorjahres — im September also Januar bis September gegen
  Januar bis September. Wie weit gerechnet wurde, steht über den Zahlen; auf
  ganze Kalenderjahre lässt sich umschalten, und dann steht dabei, dass das
  laufende Jahr noch nicht zu Ende ist.
- Ganz unten steht die vollständige Gegenüberstellung nach Kategorie zum
  Aufklappen, der größte Posten oben, mit einer eigenen Spalte für den
  Unterschied und eine für den Anteil am Jahr. Ein Klick auf eine Karte oder
  eine Zeile führt in die Ausgabenliste mit genau den Buchungen dahinter,
  und die Tabelle lässt sich als CSV-Datei speichern.
- Gibt es für das Vorjahr noch gar keine Buchung, sagt die Seite das und
  lässt oben ein anderes Jahr wählen, statt eine leere Gegenüberstellung zu
  zeigen. Hat sich zwischen zwei Jahren wirklich nichts Nennenswertes
  verschoben, steht auch das da — als Ergebnis und nicht als Fehlanzeige.

### Auswertung

- Die Filterleiste der Auswertung hat jetzt wie die Ausgabenliste die
  Häkchen „Ausgaben“ und „Einnahmen“. Ohne Häkchen zählen wie bisher beide
  zusammen; ein einzelnes Häkchen rechnet die Auswertung nur noch über
  Ausgaben oder nur noch über Einnahmen. Der gesetzte Filter steht auch bei
  eingeklappter Leiste als Merkmal darunter und lässt sich dort wegklicken.

### Gespeicherte Filter

- Ausgabenliste und Auswertung haben jetzt in der Filterzeile den Knopf
  „Filter ▾“. Er legt die gerade eingestellten Filter unter einem Namen ab und
  stellt sie später mit einem Klick wieder her. Wer immer wieder dieselbe Frage
  stellt — „was kostet das Auto dieses Jahr“ —, setzt dafür nicht mehr jedes Mal
  dieselben sechs Häkchen.
- Beide Bereiche teilen sich eine Liste: was in der Ausgabenliste gespeichert
  wurde, steht auch in der Auswertung bereit und umgekehrt. In der Auswertung
  gehört die Zeiteinteilung (Jahr, Quartal, Monat) mit zum gespeicherten Stand.
- War der Zeitraum über einen der Knöpfe „Dieser Monat“, „Dieses Jahr“ oder
  „Letzte 12 Monate“ eingestellt, wird er beim Anwenden neu ausgerechnet:
  „Dieses Jahr“ meint auch im nächsten Jahr das laufende. Ein von Hand
  eingetragener Zeitraum bleibt dagegen stehen, wie er war.
- Ein Name, den es schon gibt, ersetzt den bisherigen Stand — so lässt sich ein
  Filter nachbessern, ohne ihn vorher zu löschen. Weg kommt er über das Kreuz
  neben seinem Namen; eine Rückfrage gibt es dafür nicht, denn an den Buchungen
  ändert das nichts.

### Filterleiste

- Die Filterfelder klappt jetzt ein kleiner Pfeil am rechten Ende der Zeile auf
  und zu, statt eines Knopfes mitten in der Reihe. Aufgeklappt zeigt er nach
  oben, eingeklappt nach unten — er sagt also, was ein Klick tut. Wie viele
  Filter gesetzt sind, steht in seinem Hinweis; worauf gefiltert wird, sagen wie
  bisher die Merkmale darunter.

### Sonstiges

- Das Ändern einer Kategoriefarbe endete bisher jedes Mal mit einer
  Fehlermeldung, obwohl die Farbe längst gesetzt und gespeichert war — die
  Auswahl blieb dabei offen stehen. Sie schließt sich jetzt nach der Wahl, und
  die Meldung bleibt aus.
- Bei großer Schrift werden aufklappende Erklärungen und Auswahllisten jetzt so
  breit, wie ihr Inhalt sie braucht. Vorher stießen sie an eine feste Grenze und
  bekamen eine Bildlaufleiste, mit der man den Text seitlich schieben musste —
  am deutlichsten bei der Erklärung hinter dem Fragezeichen im Jahresrückblick.

- Weil in der Seitenleiste ein Bereich dazugekommen ist, führen Strg + 1 bis
  Strg + 9 jetzt jeweils eine Stelle weiter nach unten. Die Datensicherung
  bleibt über die Seitenleiste erreichbar, hat aber kein Zifferkürzel mehr.

## 1.5.0 — 24.08.2026

- Die Filterleiste in Ausgabenliste und Auswertung lässt sich jetzt
  einklappen und tut das bei schmalem Fenster von selbst. Übrig bleibt
  eine Zeile mit dem Suchfeld und einem Knopf, der sagt, wie viele Filter
  gesetzt sind. Vorher liefen die hinteren Filter aus ihrer Karte heraus
  über den rechten Rand hinweg und waren dort auch nicht erreichbar; wer
  das Fenster kleiner zog, bekam eine ausgefranste Treppe, die die halbe
  Seite einnahm.
- Jeder gesetzte Filter steht jetzt als kleiner Chip unter der Leiste und
  lässt sich dort einzeln mit einem Klick aufheben — auch eingeklappt.
  Damit bleibt immer sichtbar, worauf gerade gefiltert wird; ein Filter,
  der wirkt, ohne sich zu zeigen, ist der häufigste Grund für „meine
  Buchungen sind weg". Das Suchfeld ist aus dem Filterblock nach oben in
  die immer sichtbare Zeile gewandert, weil es das am häufigsten benutzte
  ist.
- Auf einem Mac steht im Menü oben links jetzt „Ausgabenverwaltung" statt
  des Namens des verwendeten Baukastens.
- Der Hinweis auf eine neue Fassung sagt jetzt, was zu tun ist, und der
  Knopf tut es auch: „Jetzt neu starten" schließt die Anwendung und öffnet
  sie sofort wieder. Vorher hieß er „Jetzt beenden" und beendete nur — wer
  danach vor einem geschlossenen Programm saß, hielt das für einen Fehler.
  Im Hinweis steht außerdem nur noch ein hervorgehobener Knopf statt drei
  gleich wichtig aussehender.
- Lässt sich eine neue Fassung nicht selbst einspielen, nennt der Hinweis
  jetzt die Datei beim Namen und führt in vier Schritten durch das
  Austauschen von Hand — statt nur „ersetzt die Programmdatei von Hand" zu
  sagen und die Frage offen zu lassen, welche der Dateien auf der Seite
  denn gemeint ist.
- Im Programmordner bleibt nach einer Aktualisierung nichts mehr liegen.
  Vorher standen dort plötzlich mehrere Dateien mit fast demselben Namen,
  und keine erklärte sich. Während der Aktualisierung sind die
  Zwischendateien jetzt unsichtbar, und aufgeräumt wird verlässlich statt
  auf einen einzigen Versuch hin. Auch ein abgebrochener Download
  hinterlässt nichts mehr.
- Wo eine Liste leer ist, steht jetzt der Knopf daneben, der den Zustand
  auflöst: „Jetzt sichern" bei der Datensicherung, „Neue Vorlage" bei den
  wiederkehrenden Ausgaben, „Ausgabe erfassen" in Ausgabenliste und
  Auswertung. Bisher stand dort ein grauer Satz, der die Lösung zwar
  nannte, aber suchen ließ.
- Die leere Ausgabenliste und die leere Auswertung unterscheiden jetzt
  zwei Fälle: Liegt es am Filter, wird „Filter zurücksetzen" angeboten;
  ist noch gar nichts erfasst, führt der Knopf in die Erfassungsmaske.
  Vorher hieß es in beiden Fällen „Keine Ausgaben für diesen Filter" —
  was nach einem Filterproblem klang, obwohl schlicht noch nichts da war.
- Bei den offenen Posten heißt es jetzt „Nichts offen" statt „Keine
  offenen Posten": Dass nichts aussteht, ist eine gute Nachricht und
  keine Fehlanzeige. Einen Knopf gibt es hier bewusst nicht — es ist
  nichts aufzulösen.

## 1.4.1 — 08.08.2026

- Das Fenster öffnet wieder dort, wo es zuletzt stand, und in der Größe,
  die es zuletzt hatte — maximiert bleibt maximiert. Steht die gemerkte
  Stelle auf keinem angeschlossenen Bildschirm mehr, etwa weil der zweite
  Monitor abgezogen wurde, öffnet das Fenster mittig auf dem vorhandenen.
  Sonst wäre es unsichtbar und ließe sich nicht einmal zurückholen.
- Auch die Sortierung bleibt erhalten: wer die Ausgabenliste nach Betrag
  sortiert oder die offenen Posten nach der Zahl der Tage, findet sie beim
  nächsten Start so wieder. Beide Listen merken sich das getrennt.
  „Filter zurücksetzen" lässt die Sortierung bewusst stehen — sie ist kein
  Filter, sondern die Leserichtung.
- Die Knöpfe am Zeilenende stehen jetzt in jeder Zeile an derselben Stelle.
  Vorher richteten sie sich nach der Länge des Textes davor und wanderten
  dadurch von Zeile zu Zeile um einige Pixel: bei den offenen Posten
  verschoben „Abhaken" und „Erhalten" den Knopf dahinter, bei den
  wiederkehrenden Ausgaben taten „Deaktivieren" und „Aktivieren" dasselbe
  mit „Löschen", und bei den Kategorien verschob „Wiederherstellen" sogar
  die Spalte mit der Anzahl davor. Wer eine Liste von oben nach unten
  durcharbeitet, klickt jetzt nicht mehr daneben.
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
