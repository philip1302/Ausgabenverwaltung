namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Eine vorhandene Sicherungsdatei. Der Zeitstempel stammt aus dem
/// Dateinamen und nicht aus den Dateisystem-Metadaten: die aendern sich
/// beim Kopieren in ein zweites Ziel, der Name nicht.
/// </summary>
public sealed record BackupFile(string FullPath, string FileName, DateTime Timestamp, long SizeBytes);
