namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Gestaffelte Aufbewahrung: welche Sicherungen bleiben, welche gehen.
///
/// Zwei Regeln, die sich ueberlagern:
/// 1. die letzten <see cref="KeepLatestCount"/> Sicherungen,
/// 2. zusaetzlich die jeweils letzte Sicherung jedes Kalendermonats -
///    dauerhaft.
///
/// Der Grund fuer die zweite Regel: zehn taegliche Sicherungen reichen nur
/// zurueck, solange der Schaden binnen zehn Tagen auffaellt. Faellt eine
/// versehentlich geloeschte Kategorie erst nach Wochen auf, ist der
/// Monatsstand der einzige Weg zurueck auf einen Stand VOR dem Schaden.
///
/// Reine Rechnung auf einer Liste, ohne Dateisystemzugriff - deshalb
/// vollstaendig pruefbar (Regel 7).
/// </summary>
public static class BackupRetention
{
    public const int KeepLatestCount = 10;

    /// <summary>
    /// Die Sicherungen, die keine der beiden Regeln haelt. Die Reihenfolge
    /// der Eingabe ist egal.
    /// </summary>
    public static IReadOnlyList<BackupFile> SelectExpired(IEnumerable<BackupFile> backups)
    {
        var byDateDescending = backups
            .OrderByDescending(backup => backup.Timestamp)
            .ToList();

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var backup in byDateDescending.Take(KeepLatestCount))
        {
            keep.Add(backup.FullPath);
        }

        // GroupBy erhaelt die Reihenfolge innerhalb einer Gruppe. Da die
        // Liste absteigend sortiert ist, ist das erste Element jeder Gruppe
        // die letzte Sicherung dieses Monats.
        foreach (var month in byDateDescending.GroupBy(
                     backup => (backup.Timestamp.Year, backup.Timestamp.Month)))
        {
            keep.Add(month.First().FullPath);
        }

        return byDateDescending
            .Where(backup => !keep.Contains(backup.FullPath))
            .ToList();
    }
}
