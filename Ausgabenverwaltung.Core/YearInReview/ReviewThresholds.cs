namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Schwellwerte des Jahresrueckblicks - an einer Stelle, weil sie
/// gemeinsam die Frage beantworten, was ueberhaupt eine Meldung wert ist.
///
/// Ohne sie fuehrt jede Rangliste dieselbe Sorte Befund an: eine Ausgabe,
/// die von 2 auf 20 Euro steigt, ist plus 900 Prozent und schlaegt damit
/// jede echte Verschiebung. Prozentzahlen auf kleiner Grundlage sind kein
/// Ereignis, sondern Streuung.
/// </summary>
public static class ReviewThresholds
{
    /// <summary>
    /// Unterschied, ab dem eine Veraenderung ueberhaupt gemeldet wird.
    /// Unter 50 Euro im JAHR traegt der Satz "das ist dir passiert" nicht.
    /// </summary>
    public const long MindestDeltaCents = 5_000;

    /// <summary>
    /// Jahresvolumen, ab dem eine Kategorie als Ausgabenposition gilt.
    /// Darunter ist sie eine Einzelbuchung und keine Position, ueber die
    /// man ein Jahr lang etwas sagen koennte.
    /// </summary>
    public const long MindestVolumenCents = 10_000;

    /// <summary>
    /// Anteil am Vorjahreswert, ab dem die Veraenderung eine ist. 50 Euro
    /// mehr auf 20.000 Euro Miete sind keine Verschiebung, sondern
    /// Rauschen im Grossen.
    /// </summary>
    public const decimal MindestRelativ = 0.10m;

    /// <summary>
    /// Deckel fuer das Verhaeltnis in der Punktzahl: eine Vervierfachung
    /// zaehlt wie eine Verzwanzigfachung. Sonst gewinnt eine Kategorie mit
    /// winzigem Vorjahreswert jeden Vergleich allein durch ihr Verhaeltnis.
    /// </summary>
    public const double RelativDeckel = 3.0;

    /// <summary>
    /// Anteil, ab dem ein Kind die Veraenderung seiner Oberkategorie
    /// "erklaert" und statt ihrer gemeldet wird. 70 Prozent und nicht 50:
    /// bei 50 koennten zwei Geschwister mit je 49 Prozent die
    /// Oberkategorie verdraengen, obwohl dort die Aussage liegt.
    /// </summary>
    public const decimal AnteilFuerAbstieg = 0.70m;

    /// <summary>
    /// Jahresausgaben, unter denen der Rueckblick gar nichts meldet. Bei
    /// unter 500 Euro im Jahr ist die Datenlage zu duenn fuer eine Aussage
    /// ueber Gewohnheiten.
    /// </summary>
    public const long MindestJahresausgabenCents = 50_000;

    /// <summary>Mehr Karten als das liest niemand mehr als Rueckblick.</summary>
    public const int HoechsteBefundzahl = 6;

    // --- "Viele kleine Buchungen" ---------------------------------------
    public const int KleinviehMindestAnzahl = 24;
    public const long KleinviehHoechstSchnittCents = 1_500;
    public const long KleinviehMindestSummeCents = 30_000;

    /// <summary>Unter einer Buchung im Monat ist "haeufig" kein Wort.</summary>
    public const int HaeufigsteMindestAnzahl = 12;

    /// <summary>
    /// Aus weniger Monaten laesst sich kein Durchschnitt bilden, gegen den
    /// ein einzelner Monat auffallen koennte.
    /// </summary>
    public const int MindestMonateFuerMonatsbefund = 3;

    // --- Gewichtung der Befundarten gegeneinander ------------------------
    // Ein Dauerzustand ("das ist dein groesster Kostenblock") ist weniger
    // wert als eine Veraenderung - er galt letztes Jahr auch schon. Die
    // Teiler druecken solche Befunde in der Rangfolge nach hinten, ohne sie
    // ganz auszuschliessen: bei einem ruhigen Jahr tragen sie die Seite.
    public const long TeilerKostenblock = 5;
    public const long TeilerKleinvieh = 3;
    public const long TeilerHaeufigste = 5;

    /// <summary>
    /// Gewicht der Abweichung eines Monats vom Monatsschnitt. Gemessen
    /// wird nur der UEBERSCHUSS, nicht die ganze Monatssumme - dass ein
    /// Monat Ausgaben hat, ist keine Nachricht.
    /// </summary>
    public const long FaktorMonatsabweichung = 2;
}
