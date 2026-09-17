using System.Collections.Generic;

namespace Ausgabenverwaltung.Core.Errors;

/// <summary>
/// Die Meldungen der app-weiten Hinweisbaender, die nicht zur
/// Selbstaktualisierung gehoeren (die stehen in
/// <see cref="UpdateText"/>).
///
/// Sie standen vorher als zusammengesetzte Zeichenketten im
/// StartupNoticeViewModel - also im ViewModel und damit ausserhalb jeder
/// Pruefung (Regel 7 und 12). Hier haben sie dieselbe Form wie alle
/// anderen Baender und laufen durch dieselben Grundsatz-Tests.
/// </summary>
public static class Bandtexte
{
    /// <summary>
    /// Die Sicherung beim Start ist misslungen. Sie darf den Start nicht
    /// verhindern, aber auch nicht unbemerkt bleiben - ein Band statt
    /// eines Dialogs. Ein nicht erreichbares ZWEITES Ziel erscheint hier
    /// bewusst nicht: das steht still in den Einstellungen (siehe
    /// Core.Backups.BackupResult.NeedsAttention).
    /// </summary>
    public static Bandmeldung Sicherungsfehler(StorageProblem problem)
    {
        return new Bandmeldung(
            Bandrang.Fehler,
            "Die Sicherung beim Start ist misslungen",
            "Ihre Buchungen sind unverändert — unter „Verwaltung › Datensicherung“ "
            + "lässt sich ein neuer Versuch starten.",
            FileErrorText.ForBackup(problem)
            + "\n\nDie Anwendung läuft normal weiter, und an Ihren Buchungen hat "
            + "sich nichts verändert — misslungen ist die Kopie, nicht der "
            + "Datenbestand. Unter „Verwaltung › Datensicherung“ lässt sich ein "
            + "neuer Versuch starten; dort steht der Hinweis auch dann noch, wenn "
            + "dieses Band hier weggeklickt ist.");
    }

    /// <summary>
    /// Wiederkehrende Buchungen, die gerade automatisch erzeugt wurden -
    /// beim Programmstart und bei den Laeufen waehrend der Sitzung.
    ///
    /// Die Aufzaehlung stand frueher in einem aufklappbaren Bereich IM
    /// Band. Das machte das Band zu einem Behaelter mit eigener Mechanik,
    /// obwohl es eine Statuszeile ist; die Liste steht deshalb jetzt in
    /// den Details, wie bei jedem anderen Band auch.
    /// </summary>
    /// <param name="beschreibungen">
    /// Je eine Zeile "Datum - Betrag (Bemerkung)". Fertig formatiert vom
    /// Aufrufer, weil Datums- und Betragsformat ohnehin nur ueber
    /// IsoDate/EuroText entstehen duerfen.
    /// </param>
    public static Bandmeldung ErzeugteBuchungen(IReadOnlyList<string> beschreibungen)
    {
        var titel = beschreibungen.Count == 1
            ? "1 wiederkehrende Buchung wurde erzeugt"
            : $"{beschreibungen.Count} wiederkehrende Buchungen wurden erzeugt";

        return new Bandmeldung(
            Bandrang.Hinweis,
            titel,
            "Sie sind bereits eingetragen — „Details“ zeigt, welche es sind.",
            "Diese Buchungen sind aus Ihren Vorlagen entstanden und stehen bereits "
            + "in der Liste. An den Vorlagen selbst hat sich nichts verändert:\n\n"
            + string.Join("\n", beschreibungen));
    }
}
