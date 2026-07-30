using System.Text.Json;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Settings;

/// <summary>
/// Liest und schreibt die Einstellungen als JSON-Datei (siehe
/// <see cref="Database.AppPaths.GetSettingsFilePath()"/>). Es gibt genau
/// diesen einen Speicherort - wer eine Einstellung ergaenzt, ergaenzt
/// <see cref="AppSettings"/> und diese Datei, keinen zweiten Ort.
///
/// Gespeichert wird immer der GANZE Satz. Wer nur ein Feld aendern will,
/// laedt vorher und schreibt mit "with" weiter, sonst faellt alles
/// Uebrige auf die Vorgabewerte zurueck.
///
/// Bewusst nachsichtig: fehlt die Datei oder ist ihr Inhalt beschaedigt,
/// gelten die Vorgabewerte. Eine unlesbare Einstellungsdatei darf den
/// Programmstart nicht verhindern - der Preis dafuer ist, dass ein
/// eingetragenes zweites Ziel in so einem Fall verloren geht und neu
/// gewaehlt werden muss.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public AppSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            var document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(_filePath), SerializerOptions);

            if (document is null)
            {
                return new AppSettings();
            }

            return new AppSettings
            {
                ExternalFolderPath = string.IsNullOrWhiteSpace(document.ExternalFolderPath)
                    ? null
                    : document.ExternalFolderPath,
                LastExternalBackupUtc = ParseOrNull(document.LastExternalBackupUtc),

                // Fehlt der Wert in einer aelteren Datei, steht hier 0 -
                // Normalize macht daraus die kleinste Stufe, deshalb wird
                // das Fehlen vorher abgefangen.
                FontScale = document.FontScale is double faktor
                    ? FontScales.Normalize(faktor)
                    : FontScales.DefaultFactor,
            };
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var document = new SettingsDocument
        {
            ExternalFolderPath = settings.ExternalFolderPath,
            LastExternalBackupUtc = settings.LastExternalBackupUtc is DateTime utc
                ? IsoDateTime.ToUtcText(utc)
                : null,
            FontScale = settings.FontScale,
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
    // nicht in der Serialisierung von System.Text.Json (Regel 3). Alle
    // Felder sind nullable, damit eine Datei aus einer aelteren Version
    // erkennbar "hat den Wert nicht" von "hat ihn auf 0" unterscheidet.
    private sealed class SettingsDocument
    {
        public string? ExternalFolderPath { get; set; }
        public string? LastExternalBackupUtc { get; set; }
        public double? FontScale { get; set; }
    }
}
