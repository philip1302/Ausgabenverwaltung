using System.Collections.Generic;
using System.Linq;

namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Welches Band gerade gezeigt wird, wenn mehrere anstehen.
/// </summary>
/// <param name="Index">
/// Stelle der gewaehlten Meldung in der UEBERGEBENEN Liste - nicht in der
/// sortierten. Der Aufrufer haelt neben jeder Meldung noch ihre Knoepfe,
/// und die muessen zur gezeigten Meldung passen. <c>-1</c>, wenn nichts
/// ansteht.
/// </param>
/// <param name="Nummer">Die wievielte von <paramref name="Gesamt"/>, 1-basiert. 0 bei nichts.</param>
/// <param name="Gesamt">Wie viele insgesamt anstehen.</param>
/// <param name="Zaehltext">
/// "2 von 3" - oder <c>null</c>, wenn es nur eine gibt. Bei genau einer
/// Meldung waere "1 von 1" reine Ziffernkosmetik.
/// </param>
public sealed record Bandlage(int Index, int Nummer, int Gesamt, string? Zaehltext)
{
    public static readonly Bandlage Leer = new(-1, 0, 0, null);

    public bool Sichtbar => Index >= 0;

    /// <summary>Ob sich das Durchblaettern ueberhaupt lohnt.</summary>
    public bool MehrereVorhanden => Gesamt > 1;
}

/// <summary>
/// Waehlt aus mehreren anstehenden Meldungen die eine aus, die das Band
/// zeigt.
///
/// Warum nur eine: die Anwendung hatte drei Baender, die sich oben
/// uebereinander stapeln konnten - Aktualisierung, Sicherungsfehler und
/// erzeugte wiederkehrende Buchungen. Alle drei zugleich schoben den
/// eigentlichen Inhalt um ein Vielfaches ihrer eigenen Hoehe nach unten,
/// und keines war dadurch wichtiger als das andere. Die gaengige
/// Empfehlung (Carbon, PatternFly) ist genau deshalb: ein Band, und wer
/// mehr hat, staffelt nach Dringlichkeit.
///
/// Verloren geht nichts. Die uebrigen stehen weiter an und lassen sich
/// durchblaettern (<see cref="Bandlage.Zaehltext"/>); geschlossen wird
/// immer nur die gerade gezeigte.
///
/// Reine Auswahllogik ohne Oberflaeche, deshalb hier in Core und
/// vollstaendig pruefbar (Regel 7).
/// </summary>
public static class Bandauswahl
{
    /// <summary>
    /// Sortiert nach Rang absteigend und liefert die Meldung an der
    /// gewuenschten Stelle.
    /// </summary>
    /// <param name="kandidaten">
    /// Alle anstehenden Meldungen in der Reihenfolge, in der sie
    /// aufgetreten sind.
    /// </param>
    /// <param name="wunschNummer">
    /// Die wievielte gezeigt werden soll, 1-basiert - der Stand des
    /// Durchblaetterns. Wird in den gueltigen Bereich gebracht, damit ein
    /// weggefallenes Band den Zaehler nicht ins Leere laufen laesst.
    /// </param>
    public static Bandlage Waehle(IReadOnlyList<Bandmeldung> kandidaten, int wunschNummer = 1)
    {
        if (kandidaten.Count == 0)
        {
            return Bandlage.Leer;
        }

        // OrderByDescending ist in LINQ stabil: bei gleichem Rang bleibt
        // die Reihenfolge des Auftretens erhalten. Das ist gewollt - unter
        // zwei gleich dringenden Meldungen ist die aeltere die, die der
        // Anwender schon einmal gesehen hat, und sie soll nicht durch eine
        // neue verdraengt werden.
        var sortiert = kandidaten
            .Select((meldung, stelle) => (meldung, stelle))
            .OrderByDescending(eintrag => eintrag.meldung.Rang)
            .ToList();

        var nummer = wunschNummer < 1 ? 1
            : wunschNummer > sortiert.Count ? sortiert.Count
            : wunschNummer;

        return new Bandlage(
            sortiert[nummer - 1].stelle,
            nummer,
            sortiert.Count,
            sortiert.Count > 1 ? $"{nummer} von {sortiert.Count}" : null);
    }
}
