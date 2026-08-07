using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Ergebnis der Pruefung einer Sicherungsdatei
/// (<see cref="BackupVerification.Verify"/>).
///
/// Rein berichtend - die Pruefung wirft nicht. Eine Sicherung, die sich
/// nicht oeffnen laesst, ist ein Befund und kein Programmfehler.
/// </summary>
public sealed record BackupVerificationResult
{
    /// <summary>
    /// Ob die Sicherung eine oeffenbare, unbeschaedigte Datenbank
    /// enthaelt. Nur das beantwortet die eigentliche Frage: taugt diese
    /// Datei zum Wiederherstellen?
    /// </summary>
    public required bool IsReadable { get; init; }

    /// <summary>Schema-Stand der Datenbank im ZIP - NULL, wenn nicht
    /// lesbar.</summary>
    public int? SchemaVersion { get; init; }

    /// <summary>Anzahl der Buchungen darin - NULL, wenn nicht lesbar.</summary>
    public int? ExpenseCount { get; init; }

    /// <summary>
    /// Die Sicherung ist lesbar, stammt aber aus einer neueren
    /// Programmversion. Sie liesse sich mit dieser Fassung nicht
    /// zurueckspielen - deshalb ein eigener Befund und nicht einfach
    /// "in Ordnung".
    /// </summary>
    public bool IsSchemaTooNew { get; init; }

    /// <summary>Art des Problems - Grundlage des Meldungstextes
    /// (<see cref="FileErrorText.ForBackupVerification"/>).</summary>
    public StorageProblem Problem { get; init; }

    /// <summary>
    /// Der technische Befund: SQLite-Meldung oder Ausnahmetext. Fuer
    /// Protokoll und aufklappbaren Bereich, nie fuer den Haupttext
    /// (Regel 12). NULL, wenn nichts zu beanstanden war.
    /// </summary>
    public string? Finding { get; init; }
}
