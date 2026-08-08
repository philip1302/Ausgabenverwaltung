namespace Ausgabenverwaltung.Core.Display;

/// <summary>
/// Wann die Filterleiste aufgeklappt bleibt und wann sie einklappt.
///
/// Der Anlass: aufgeklappt stehen acht Filtergruppen nebeneinander. Wird
/// das Fenster schmaler, brechen sie um - und weil die Gruppen sehr
/// verschieden breit und verschieden hoch sind (ein Datumsfeld gegen vier
/// Schnellwahlknoepfe), entsteht dabei eine ausgefranste Treppe, die
/// obendrein die halbe Seite einnimmt. Die Liste, um die es eigentlich
/// geht, rutscht nach unten aus dem Blick.
///
/// Unterhalb der Schwelle klappt die Leiste deshalb zu einer Zeile
/// zusammen. Sichtbar bleiben Suche und ein Knopf mit der Anzahl der
/// aktiven Filter; WORAUF gefiltert wird, sagen die Chips darunter
/// (<see cref="Reports.FilterChips"/>) - ohne sie waere das Einklappen
/// ein Verstecken.
///
/// Die Schwelle ist bewusst nicht die Breite, ab der es UEBERHAUPT
/// umbricht: zwei, drei ruhige Reihen sind in Ordnung, und wer ein
/// mittelbreites Fenster hat, soll seine Filter sehen. Sie liegt dort, wo
/// aus dem Umbruch eine Treppe wird.
/// </summary>
public static class Filterleiste
{
    /// <summary>
    /// Ab dieser verfuegbaren Breite (in geraeteunabhaengigen Punkten,
    /// Schriftstufe "Normal") bleibt die Leiste aufgeklappt.
    ///
    /// Der Wert ist die Summe der breitesten Reihe in ihrer schmalsten
    /// noch ruhigen Anordnung: Kategorien (260) und Suche (220) plus
    /// Abstand passen nebeneinander, alles Weitere darunter. Darunter
    /// blieben nur noch ein bis zwei Gruppen je Reihe uebrig, und die
    /// Leiste waere hoeher als die Liste darunter.
    /// </summary>
    public const double MindestbreiteAufgeklappt = 520;

    /// <summary>
    /// Reicht die Breite fuer die aufgeklappte Leiste?
    ///
    /// Eine nicht gemessene Breite (0 oder kleiner, wie vor dem ersten
    /// Messen) gilt als ausreichend: der Anfangszustand ist aufgeklappt,
    /// und eine Leiste, die beim Aufgehen des Fensters erst zuklappt und
    /// gleich wieder aufspringt, sieht nach einem Fehler aus.
    /// </summary>
    public static bool PasstAufgeklappt(double verfuegbareBreite)
        => !double.IsFinite(verfuegbareBreite)
           || verfuegbareBreite <= 0
           || verfuegbareBreite >= MindestbreiteAufgeklappt;
}
