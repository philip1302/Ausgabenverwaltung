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

/// <summary>
/// Reine Verdrahtung: die Ansicht selbst ist vollstaendig deklarativ, hier
/// steht nur die Dateiauswahl fuer den Export. Wortgleich zu
/// <see cref="ReportView"/> aufgebaut - zwei Exporte, die sich verschieden
/// verhielten, waeren schlimmer als jeder der beiden fuer sich.
/// </summary>
public partial class JahresrueckblickView : UserControl
{
    public JahresrueckblickView()
    {
        InitializeComponent();
    }

    // Wie gross die Zeichenflaeche des Monatsverlaufs ist, weiss erst die
    // Oberflaeche - gerechnet wird damit aber in Core (Regel 7). Deshalb
    // wird die Groesse nur weitergereicht, genau wie in StartseiteView.
    private void Zeichenflaeche_Groesse(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is JahresrueckblickViewModel viewModel)
        {
            viewModel.ZeichenflaecheGeaendert(e.NewSize.Width, e.NewSize.Height);
        }
    }

    // Dateiauswahl und Schreiben sind Aufgabe der Oberflaeche; der
    // CSV-Text entsteht in Core (Reports.ReportCsv), der Text einer
    // Fehlermeldung ebenfalls (Errors.FileErrorText) - Regel 7.
    private async void CsvExportieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not JahresrueckblickViewModel viewModel)
        {
            return;
        }

        // Vorher gemerkt: nach einem Fehler soll die Meldung sagen, WELCHE
        // Datei nicht ging, und dann steht das Ziel unter Umstaenden nicht
        // mehr zur Verfuegung.
        var dateiname = viewModel.CsvDateiname;

        try
        {
            dateiname = await ExportiereCsv(viewModel) ?? dateiname;
        }
        catch (Exception ex)
        {
            // Ein fehlgeschlagener Export darf die Anwendung nicht
            // beenden - der Hinweis steht neben der Schaltflaeche. Der
            // haeufigste Fall ist die noch in Excel geoeffnete Zieldatei.
            AppLog.Current.Exception("Beim CSV-Export des Jahresrueckblicks", ex);

            viewModel.MeldeExport(
                FileErrorText.ForCsvExport(StorageProblems.Classify(ex), dateiname));
        }
    }

    // Liefert den tatsaechlich gewaehlten Dateinamen, damit eine
    // Fehlermeldung ihn nennen kann - oder NULL, wenn abgebrochen wurde.
    private async Task<string?> ExportiereCsv(JahresrueckblickViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return null;
        }

        var ziel = await fenster.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Jahresrückblick als CSV speichern",
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
