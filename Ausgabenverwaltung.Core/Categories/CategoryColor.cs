namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Ein Eintrag der Farbpalette: der gespeicherte Wert und sein Name.
/// Der Name ist kein Schmuck - er ist die Beschriftung, ueber die die
/// Farbe auch ohne Farbwahrnehmung auswaehlbar bleibt.
/// </summary>
/// <param name="Hex">Der gespeicherte Wert im Format '#RRGGBB'.</param>
/// <param name="Name">Anzeigename, z. B. "Blau".</param>
public sealed record CategoryColor(string Hex, string Name);
