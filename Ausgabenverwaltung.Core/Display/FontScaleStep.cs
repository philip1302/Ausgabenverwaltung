namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Die waehlbaren Stufen der globalen Schriftgroesse. Bewusst Stufen und
/// keine freie Zahl: der Anwender waehlt "wie gross", nicht "wie viele
/// Punkte" - und nur so bleibt pruefbar, dass alle Ansichten bei jeder
/// Stufe vollstaendig lesbar sind.
///
/// Zu jeder Stufe gehoert ein Skalierungsfaktor, keine absolute
/// Punktzahl (siehe <see cref="FontScales"/>): jede Schriftgroesse der
/// Oberflaeche wird mit demselben Faktor multipliziert, dadurch bleiben
/// die Groessenverhaeltnisse zwischen Hinweistext, Fliesstext und
/// Ueberschrift auf jeder Stufe gleich.
/// </summary>
public enum FontScaleStep
{
    Small,
    Normal,
    Large,
    ExtraLarge,
}
