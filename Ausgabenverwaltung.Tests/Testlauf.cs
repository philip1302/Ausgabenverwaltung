using Xunit;

// Die Testklassen laufen NACHEINANDER statt nebeneinander.
//
// Grund ist die Protokollierung: AppLog.Current ist bewusst eine
// statische Ausfuehrung, weil Ausnahmen an Stellen entstehen, an die kein
// Konstruktorparameter reicht (globales Auffangnetz, Hintergrundaufgaben,
// Ereignishandler). Genau das macht sie im Test aber zu gemeinsamem
// Zustand: waehrend ProtokollTests die Protokollierung auf sein
// Temp-Verzeichnis richtet, wuerde jede parallel laufende Testklasse ihre
// Ausnahmen dorthin schreiben - und in das Verzeichnis, das gleich
// geloescht wird.
//
// Der Testlauf dauert dadurch etwa doppelt so lange (rund fuenf statt
// zweieinhalb Sekunden). Das ist der Preis dafuer, dass ein roter Test
// tatsaechlich einen Fehler bedeutet und nicht einen ungluecklichen
// Moment.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
