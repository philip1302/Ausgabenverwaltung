namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Signalisiert, dass sich Buchungsdaten irgendwo in der Anwendung
/// geaendert haben (angelegt, abgehakt, bearbeitet, geloescht, Kategorie
/// zusammengefuehrt). Traegt bewusst keine Nutzdaten - jeder Empfaenger
/// laedt seine eigene Liste ohnehin vollstaendig aus der Datenbank neu,
/// genau wie beim bisherigen Navigations-Refresh (Regel 7).
///
/// Verteilt wird sie ueber ein per DI eingebundenes
/// CommunityToolkit.Mvvm.Messaging.IMessenger (Regel 14 in CLAUDE.md) -
/// bewusst nicht ueber WeakReferenceMessenger.Default: der ist
/// prozessweit geteilt, und in Tests wuerden Registrierungen frueherer
/// ViewModel-Instanzen sonst in spaetere Testfaelle hineinwirken. Jedes
/// ViewModel mit einer Buchungsliste registriert sich im Konstruktor
/// darauf, jede Stelle, die Buchungsdaten schreibt, sendet sie nach
/// einem erfolgreichen Schreibvorgang.
/// </summary>
public sealed class BuchungenGeaendertNachricht
{
}
