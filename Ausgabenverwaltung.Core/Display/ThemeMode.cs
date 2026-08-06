namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Die vom Anwender bewusst wählbare Themenvariante (UI/UX-Redesign,
/// Verwaltung ▸ Darstellung). "System" folgt weiterhin dem Hell/Dunkel-
/// Modus des Betriebssystems (bisheriges, einziges Verhalten der
/// Anwendung) - Hell und Dunkel erzwingen die jeweilige Variante
/// unabhängig vom System.
/// </summary>
public enum ThemeMode
{
    System,
    Light,
    Dark,
}
