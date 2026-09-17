using System.Text.Json;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.Reports;

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

                // Fehlt der Wert, gilt die Vorgabebreite - und nicht das,
                // was Normalize aus einer 0 machen wuerde.
                CategoryColumnWidth = document.CategoryColumnWidth is double breite
                    ? ColumnWidths.NormalizeCategory(breite)
                    : ColumnWidths.CategoryDefault,

                // Ein unbekannter oder fehlender Text (aeltere Datei) faellt
                // auf "System" zurueck - das bisherige, einzige Verhalten.
                ThemeMode = LiesAufzaehlung(document.ThemeMode, ThemeMode.System),

                // Fehlt der Wert (Datei aus einer aelteren Fassung), gilt
                // die Vorgabe "eingeschaltet". Wer die Suche abgeschaltet
                // hat, hat das ausdruecklich getan, und dann steht es auch
                // in der Datei.
                AutoUpdate = document.AutoUpdate ?? true,
                LastUpdateCheckUtc = ParseOrNull(document.LastUpdateCheckUtc),

                // Leerer Text und fehlender Eintrag sind dasselbe:
                // "nichts zurueckgestellt". Dann erscheint das Band zur
                // neuen Fassung wie gewohnt.
                DismissedUpdateVersion = LeerAlsNull(document.DismissedUpdateVersion),

                // Fehlt der Wert (Datei aus einer aelteren Fassung), gilt
                // die Vorgabe "aus" - ein leeres Formular nach dem
                // Speichern, wie bisher.
                KeepEntryValues = document.KeepEntryValues ?? false,

                // Leerer Text und fehlender Eintrag sind hier dasselbe:
                // "nichts gemerkt". Eine Datei aus einer aelteren Fassung
                // hat den Wert nicht, und dann bleibt die Seite "Was ist
                // neu" beim naechsten Start still (siehe Updates.WasIstNeu).
                LastSeenVersion = LeerAlsNull(document.LastSeenVersion),

                // Fehlt einer der vier Werte, gilt die Lage als nicht
                // gemerkt - eine halbe Lage ist keine.
                WindowPlacement = LiesFensterlage(document.WindowPlacement),

                // Ein unbekannter oder fehlender Name faellt auf die
                // Vorgabesortierung zurueck, genau wie beim Thema oben.
                ExpenseListSortColumn = LiesAufzaehlung(
                    document.ExpenseListSortColumn, ExpenseSortColumn.Datum),
                ExpenseListSortAscending = document.ExpenseListSortAscending ?? false,

                OpenItemsSortColumn = LiesAufzaehlung(
                    document.OpenItemsSortColumn, OpenItemsSortColumn.Datum),
                OpenItemsSortAscending = document.OpenItemsSortAscending ?? true,

                // Fehlt der Wert (Datei aus einer aelteren Fassung), gilt
                // die Vorgabe "eingefaerbt" - wer sie abgeschaltet hat, hat
                // das ausdruecklich getan, und dann steht es in der Datei.
                ReportHeatmap = document.ReportHeatmap ?? true,

                SavedFilters = LiesFilter(document.SavedFilters),
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
            CategoryColumnWidth = settings.CategoryColumnWidth,
            ThemeMode = settings.ThemeMode.ToString(),
            AutoUpdate = settings.AutoUpdate,
            LastUpdateCheckUtc = settings.LastUpdateCheckUtc is DateTime geprueft
                ? IsoDateTime.ToUtcText(geprueft)
                : null,
            DismissedUpdateVersion = settings.DismissedUpdateVersion,
            KeepEntryValues = settings.KeepEntryValues,
            LastSeenVersion = settings.LastSeenVersion,

            WindowPlacement = settings.WindowPlacement is { } lage
                ? new PlacementDocument
                {
                    Left = lage.Left,
                    Top = lage.Top,
                    Width = lage.Width,
                    Height = lage.Height,
                    IsMaximized = lage.IsMaximized,
                }
                : null,

            ExpenseListSortColumn = settings.ExpenseListSortColumn.ToString(),
            ExpenseListSortAscending = settings.ExpenseListSortAscending,
            OpenItemsSortColumn = settings.OpenItemsSortColumn.ToString(),
            OpenItemsSortAscending = settings.OpenItemsSortAscending,
            ReportHeatmap = settings.ReportHeatmap,

            SavedFilters = settings.SavedFilters.Select(filter => new SavedFilterDocument
            {
                Name = filter.Name,
                PeriodKey = filter.PeriodKey,

                // Als ISO-Datum und nicht in deutscher Schreibweise: die
                // Datei ist Ablage und keine Anzeige, und '2026-01-31'
                // laesst sich beim Hineinsehen von Hand nicht mit
                // '01.03.2026' verwechseln (dieselbe Ueberlegung wie bei
                // Regel 3 fuer die Datenbank).
                From = filter.From is DateOnly von ? IsoDate.ToDateText(von) : null,
                To = filter.ToInclusive is DateOnly bis ? IsoDate.ToDateText(bis) : null,

                CategoryIds = filter.CategoryIds.ToList(),
                PayerIds = filter.PayerIds.ToList(),
                StatusOpen = filter.StatusOpen,
                StatusSettled = filter.StatusSettled,
                IncomeOnly = filter.IncomeOnly,
                ExpensesOnly = filter.ExpensesOnly,
                MyCosts = filter.MyCosts,
                SearchText = filter.SearchText,
                Grouping = filter.Grouping?.ToString(),
            }).ToList(),
        };

        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(document, SerializerOptions));
    }

    private static string? LeerAlsNull(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text;

    // Ein unbekannter Name (Datei aus einer neueren Fassung, von Hand
    // verschrieben) faellt auf die Vorgabe zurueck, statt die ganze Datei
    // zu verwerfen - dieselbe Nachsicht wie ueberall hier.
    private static T LiesAufzaehlung<T>(string? text, T vorgabe) where T : struct, Enum
        => LiesAufzaehlungOderNull<T>(text) ?? vorgabe;

    // Gelesen wird ueber Aufzaehlung.NachName und nicht mit einem blossen
    // Enum.TryParse: das nimmt auch Zahlen an und machte aus einem von
    // Hand verschriebenen "99" eine Sortierspalte, die es nicht gibt -
    // aufgefallen waere sie erst weit spaeter in der ORDER-BY-Weissliste
    // von ExpenseRepository, als Ausnahme mitten im Laden der Liste. Genau
    // das soll die Nachsicht dieser Datei ja verhindern.
    //
    // NULL heisst hier "nichts Brauchbares gelesen"; was daraus wird,
    // entscheidet der Aufrufer - Vorgabe (siehe oben) oder ebenfalls NULL
    // (siehe die Gruppierung eines gespeicherten Filters).
    private static T? LiesAufzaehlungOderNull<T>(string? text) where T : struct, Enum
        => Aufzaehlung.NachName<T>(text);

    // Alle vier Zahlen muessen da sein. Fehlt eine, ist die Lage
    // unbrauchbar: ein Fenster mit Position aber ohne Groesse (oder
    // umgekehrt) waere schlechter als gar keine gemerkte Lage. Die
    // Groessen werden hier NICHT beschnitten - das tut
    // WindowPlacements.Normalize, damit dieselbe Pruefung auch fuer einen
    // zur Laufzeit gebildeten Wert gilt.
    private static WindowPlacement? LiesFensterlage(PlacementDocument? document)
    {
        if (document is null
            || document.Left is not int links
            || document.Top is not int oben
            || document.Width is not double breite
            || document.Height is not double hoehe)
        {
            return null;
        }

        return new WindowPlacement
        {
            Left = links,
            Top = oben,
            Width = breite,
            Height = hoehe,
            IsMaximized = document.IsMaximized ?? false,
        };
    }

    // Eine kaputte Zeile kostet ihren Filter und nicht die ganze Datei -
    // dieselbe Nachsicht wie ueberall hier. Was danach uebrig bleibt,
    // bringt Normalize in denselben Zustand, den das Speichern
    // hinterlaesst (namenlose Eintraege und Doppelgaenger fliegen raus).
    private static IReadOnlyList<SavedFilter> LiesFilter(
        List<SavedFilterDocument>? gelesen)
    {
        if (gelesen is null)
        {
            return [];
        }

        var filter = new List<SavedFilter>();

        foreach (var document in gelesen)
        {
            if (string.IsNullOrWhiteSpace(document.Name))
            {
                continue;
            }

            filter.Add(new SavedFilter
            {
                Name = document.Name,
                PeriodKey = LeerAlsNull(document.PeriodKey),
                From = LiesDatum(document.From),
                ToInclusive = LiesDatum(document.To),
                CategoryIds = document.CategoryIds ?? [],
                PayerIds = document.PayerIds ?? [],
                StatusOpen = document.StatusOpen ?? false,
                StatusSettled = document.StatusSettled ?? false,
                IncomeOnly = document.IncomeOnly ?? false,
                ExpensesOnly = document.ExpensesOnly ?? false,
                MyCosts = document.MyCosts ?? false,
                SearchText = LeerAlsNull(document.SearchText),

                // Ein unbekannter Name faellt auf NULL zurueck: "keine
                // Gruppierung gespeichert" laesst die eingestellte
                // stehen, eine erfundene wuerde sie umstellen.
                Grouping = LiesAufzaehlungOderNull<ReportGrouping>(document.Grouping),
            });
        }

        return SavedFilters.Normalize(filter);
    }

    private static DateOnly? LiesDatum(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return IsoDate.ParseDate(text);
        }
        catch (FormatException)
        {
            // Eine unlesbare Grenze wiegt weniger als der Filter: lieber
            // eine offene Grenze als kein Filter.
            return null;
        }
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
        public double? CategoryColumnWidth { get; set; }
        public string? ThemeMode { get; set; }
        public bool? AutoUpdate { get; set; }
        public string? LastUpdateCheckUtc { get; set; }
        public string? DismissedUpdateVersion { get; set; }
        public bool? KeepEntryValues { get; set; }
        public string? LastSeenVersion { get; set; }
        public PlacementDocument? WindowPlacement { get; set; }
        public string? ExpenseListSortColumn { get; set; }
        public bool? ExpenseListSortAscending { get; set; }
        public string? OpenItemsSortColumn { get; set; }
        public bool? OpenItemsSortAscending { get; set; }
        public bool? ReportHeatmap { get; set; }
        public List<SavedFilterDocument>? SavedFilters { get; set; }
    }

    // Eigener Abschnitt je gespeichertem Filter. Wieder alle Felder
    // nullable: eine Datei aus einer aelteren Fassung kennt keinen davon,
    // und ein fehlendes Haekchen ist "nicht gesetzt" und nicht "false aus
    // Versehen".
    private sealed class SavedFilterDocument
    {
        public string? Name { get; set; }
        public string? PeriodKey { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public List<int>? CategoryIds { get; set; }
        public List<int>? PayerIds { get; set; }
        public bool? StatusOpen { get; set; }
        public bool? StatusSettled { get; set; }
        public bool? IncomeOnly { get; set; }
        public bool? ExpensesOnly { get; set; }
        public bool? MyCosts { get; set; }
        public string? SearchText { get; set; }
        public string? Grouping { get; set; }
    }

    // Eigener Abschnitt in der Datei statt vier flacher Felder: die vier
    // Zahlen gelten nur zusammen, und als eigener Block ist auch beim
    // Hineinsehen von Hand zu erkennen, dass sie zusammengehoeren.
    private sealed class PlacementDocument
    {
        public int? Left { get; set; }
        public int? Top { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
        public bool? IsMaximized { get; set; }
    }
}
