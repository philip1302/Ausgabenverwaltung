namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Ausgaben eines Monats innerhalb eines der beiden Zeitraeume.
/// Ausgaben stehen positiv, wie ueberall im Rueckblick.
///
/// Die Folge ist LUECKENLOS: ein Monat ohne Buchung steht mit 0 und
/// <see cref="Count"/> 0 mit drin. Nur so stimmt der Monatsdurchschnitt,
/// und nur so kann ein auffaellig guenstiger Monat ueberhaupt auffallen.
/// </summary>
public sealed record ReviewMonth(string Key, string Label, long ExpenseCents, int Count);
