namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Alles, was der Fehlerdialog anzeigt - fertig formuliert und ohne
/// jeden Bezug zur Oberflaeche. Gebaut wird das in
/// <see cref="UnexpectedErrorText"/>; das Fenster setzt die Teile nur noch
/// an ihren Platz (Regel 7).
/// </summary>
public sealed record ErrorReport
{
    /// <summary>Ueberschrift des Dialogs. Ein ganzer Satz, kein Schlagwort.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Der Haupttext: was passiert ist, was das fuer die Daten bedeutet,
    /// was der Anwender tun kann. Ohne technische Begriffe.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Die technischen Angaben fuer den aufklappbaren Bereich - Typ,
    /// Meldung und vollstaendiger Aufrufstapel. Genau der Text, den ein
    /// Anwender kopiert und weitergibt.
    /// </summary>
    public required string Technical { get; init; }

    /// <summary>
    /// Der Protokollordner fuer die Schaltflaeche "Protokollordner
    /// oeffnen". NULL, wenn nicht protokolliert wird - dann faellt die
    /// Schaltflaeche weg, statt ins Leere zu fuehren.
    /// </summary>
    public string? LogFolderPath { get; init; }

    /// <summary>
    /// Was dem Anwender zu raten ist. Beide Schaltflaechen gibt es
    /// trotzdem immer - das hier entscheidet nur, welche vorausgewaehlt
    /// ist und wie der letzte Absatz endet.
    /// </summary>
    public required ErrorRecommendation Recommendation { get; init; }
}
