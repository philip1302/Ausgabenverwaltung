using System.Text.Json;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Liest und schreibt die Einstellungen als JSON-Datei (siehe
/// <see cref="Database.AppPaths.GetSettingsFilePath()"/>).
///
/// Bewusst nachsichtig: fehlt die Datei oder ist ihr Inhalt beschaedigt,
/// gelten die Vorgabewerte. Eine unlesbare Einstellungsdatei darf den
/// Programmstart nicht verhindern - der Preis dafuer ist, dass ein
/// eingetragenes zweites Ziel in so einem Fall verloren geht und neu
/// gewaehlt werden muss.
/// </summary>
public sealed class BackupSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public BackupSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public BackupSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new BackupSettings();
            }

            var document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(_filePath), SerializerOptions);

            if (document is null)
            {
                return new BackupSettings();
            }

            return new BackupSettings
            {
                ExternalFolderPath = string.IsNullOrWhiteSpace(document.ExternalFolderPath)
                    ? null
                    : document.ExternalFolderPath,
                LastExternalBackupUtc = ParseOrNull(document.LastExternalBackupUtc),
            };
        }
        catch (Exception)
        {
            return new BackupSettings();
        }
    }

    public void Save(BackupSettings settings)
    {
        var document = new SettingsDocument
        {
            ExternalFolderPath = settings.ExternalFolderPath,
            LastExternalBackupUtc = settings.LastExternalBackupUtc is DateTime utc
                ? IsoDateTime.ToUtcText(utc)
                : null,
        };

        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
    }

    private static DateTime? ParseOrNull(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return IsoDateTime.ParseUtc(text);
        }
        catch (FormatException)
        {
            // Ein kaputter Zeitstempel wiegt weniger als der Pfad daneben:
            // lieber "noch nie extern gesichert" anzeigen als die ganze
            // Datei verwerfen.
            return null;
        }
    }

    // Eigener Typ fuer die Datei, damit der Zeitstempel als Text im
    // vorgeschriebenen Format 'YYYY-MM-DDTHH:MM:SSZ' abgelegt wird und
    // nicht in der Serialisierung von System.Text.Json (Regel 3).
    private sealed class SettingsDocument
    {
        public string? ExternalFolderPath { get; set; }
        public string? LastExternalBackupUtc { get; set; }
    }
}
