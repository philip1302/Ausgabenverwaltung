namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Eine Zeile im Erklaerfenster einer Karte: die Beschriftung und der
/// fertige Text daneben. Bewusst schon fertig formuliert und nicht als
/// Zahl - welcher Betrag wie erscheint, entscheidet <see cref="ReviewText"/>
/// und nicht die Ansicht (Regel 1 und Regel 7).
/// </summary>
public sealed record ReviewValue(string Beschriftung, string Wert);

/// <summary>
/// Was hinter einer Karte des Jahresrueckblicks steckt.
///
/// <b>Warum es das gibt.</b> Auf der Karte selbst stehen nur Ueberschrift,
/// Gegenstand und Betrag - drei einzeilige Angaben, damit alle Karten
/// gleich gross sind. Der ganze Satz und die Zahlen dahinter wuerden jede
/// Karte anders hoch machen und stehen deshalb hier: sie erscheinen erst,
/// wenn jemand das Fragezeichen an der Karte anklickt.
///
/// <see cref="Bedeutung"/> beantwortet die Frage, die sich beim Lesen
/// zuerst stellt - <i>wonach</i> wurde gesucht, dass ausgerechnet diese
/// Kategorie hier steht. Ohne sie bleibt der Rueckblick ein Orakel:
/// richtig gerechnet, aber nicht nachvollziehbar.
/// </summary>
public sealed record ReviewExplanation(
    string Ueberschrift,
    string Gegenstand,
    string Satz,
    string Bedeutung,
    IReadOnlyList<ReviewValue> Werte);
