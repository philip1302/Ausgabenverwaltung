using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
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
    // CSV-Text selbst entsteht in Core (Reports.ReportCsv), und der Text
    // einer Fehlermeldung ebenfalls (Errors.FileErrorText) - Regel 7.
    private async void CsvExportieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ReportViewModel viewModel)
        {
            return;
        }

        // Der Dateiname wird vor dem Versuch gemerkt: nach einem Fehler
        // soll die Meldung sagen, WELCHE Datei nicht ging, und dann steht
        // das Ziel unter Umstaenden nicht mehr zur Verfuegung.
        var dateiname = viewModel.CsvDateiname;

        try
        {
            dateiname = await ExportiereCsv(viewModel) ?? dateiname;
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagener Export darf die Anwendung nicht
            // beenden - der Hinweis steht neben der Schaltflaeche.
            //
            // Der mit Abstand haeufigste Fall ist die noch in Excel
            // geoeffnete Zieldatei. Er bekommt deshalb einen eigenen
            // Hinweis statt einer Meldung ueber "Sharing violation".
            AppLog.Current.Exception("Beim CSV-Export", ex);

            viewModel.MeldeExport(
                FileErrorText.ForCsvExport(StorageProblems.Classify(ex), dateiname));
        }
    }

    // Liefert den tatsaechlich gewaehlten Dateinamen, damit eine
    // Fehlermeldung ihn nennen kann - oder NULL, wenn abgebrochen wurde.
    private async Task<string?> ExportiereCsv(ReportViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return null;
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
            return null;
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

        return ziel.Name;
    }
}
