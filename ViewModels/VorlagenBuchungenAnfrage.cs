namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// "Zeig mir die Buchungen dieser Vorlage" - die Nutzlast des Ereignisses,
/// mit dem der Vorlagenbereich einen Wechsel in die Ausgabenliste anfordert
/// (siehe <see cref="VorlagenViewModel.BuchungenAnzeigenAngefordert"/> und
/// <see cref="MainViewModel"/>).
///
/// Bewusst ein eigener kleiner Typ statt der Zeile selbst: der
/// <see cref="MainViewModel"/> soll nur wissen, WAS gezeigt werden soll,
/// nicht wie die Vorlagenliste ihre Zeilen aufbaut.
/// </summary>
/// <param name="VorlageId">Die Vorlage, deren Buchungen gezeigt werden sollen.</param>
/// <param name="Titel">Ihr Titel, fuer die Anzeige des aktiven Filters.</param>
public sealed record VorlagenBuchungenAnfrage(int VorlageId, string Titel);
