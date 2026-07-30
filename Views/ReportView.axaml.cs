using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Ausgabenverwaltung.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
    }

    // Reine Bedienmechanik, keine Fachlogik (Regel 7): nach der Wahl eines
    // Kategorie-Astes schliesst sich das Flyout, damit die Tabelle sichtbar
    // wird, deren Filter sich gerade geaendert hat. Die Auswahl selbst
    // laeuft ueber das Command am Knoten-Button.
    private void KategorieFlyout_Schliessen(object? sender, RoutedEventArgs e)
    {
        KategorieAuswahl.Flyout?.Hide();
    }

    // Dateiauswahl und Schreiben sind Aufgabe der Oberflaeche; der
    // CSV-Text selbst entsteht in Core (Reports.ReportCsv).
    private async void CsvExportieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportViewModel viewModel)
        {
            return;
        }

        try
        {
            await ExportiereCsv(viewModel);
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagener Export darf die Anwendung nicht
            // beenden - der Hinweis steht neben der Schaltflaeche.
            viewModel.MeldeExport($"Export fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task ExportiereCsv(ReportViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return;
        }

        var ziel = await fenster.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Kreuztabelle als CSV speichern",
            SuggestedFileName = viewModel.CsvDateiname,
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV-Datei")
                {
                    Patterns = new[] { "*.csv" },
                },
            },
        });

        if (ziel is null)
        {
            return;
        }

        var text = viewModel.BaueCsv();

        await using var strom = await ziel.OpenWriteAsync();

        // Eine vorhandene Datei wird ueberschrieben, nicht ueberlagert -
        // sonst blieben Reste einer laengeren Vorgaengerdatei stehen.
        strom.SetLength(0);

        // UTF-8 MIT Signatur: ohne sie zeigt Excel im deutschen
        // Gebietsschema Umlaute falsch an.
        await using var schreiber = new StreamWriter(
            strom, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await schreiber.WriteAsync(text);
        await schreiber.FlushAsync();

        viewModel.MeldeExport($"Gespeichert: {ziel.Name}");
    }
}
