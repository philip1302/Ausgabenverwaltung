namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Aufbewahrung der Protokolldateien: die letzten
/// <see cref="KeepDays"/> Kalendertage bleiben, alles Aeltere faellt weg.
///
/// Bewusst einfacher als die gestaffelte Aufbewahrung der Sicherungen
/// (<see cref="Backups.BackupRetention"/>): ein Protokoll hilft beim
/// Nachvollziehen eines Fehlers, der gerade aufgefallen ist. Ein
/// Protokolleintrag von vor einem Jahr beantwortet keine Frage mehr, die
/// heute jemand stellt - und anders als eine Sicherung laesst sich aus ihm
/// auch nichts wiederherstellen.
///
/// Reine Rechnung auf einer Liste, ohne Dateisystemzugriff - deshalb
/// vollstaendig pruefbar (Regel 7).
/// </summary>
public static class LogRetention
{
    public const int KeepDays = 30;

    /// <summary>
    /// Die Protokolldateien, die aelter als <see cref="KeepDays"/> Tage
    /// sind. Der heutige Tag zaehlt als erster der behaltenen Tage; bei 30
    /// Tagen bleibt also alles ab <c>heute - 29</c> stehen.
    ///
    /// Dateien mit einem Datum in der Zukunft bleiben ebenfalls stehen.
    /// Das kommt vor, wenn die Systemuhr zurueckgestellt wurde, und ein
    /// Protokoll wegzuwerfen, weil die Uhr falsch ging, waere die
    /// schlechtere der beiden Moeglichkeiten.
    /// </summary>
    public static IReadOnlyList<LogFile> SelectExpired(
        IEnumerable<LogFile> files, DateOnly today)
    {
        var oldestKept = today.AddDays(-(KeepDays - 1));

        return files
            .Where(file => file.Date < oldestKept)
            .OrderBy(file => file.Date)
            .ToList();
    }
}
