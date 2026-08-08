using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class AusgabenlisteView : UserControl
{
    public AusgabenlisteView()
    {
        InitializeComponent();

        // Strg+S / Strg+Enter speichern den Bearbeiten-Dialog, Esc
        // schliesst ihn. Beide Kommandos gehoeren dem ViewModel der
        // ANSICHT (nicht dem des Dialogs) und tun nichts, solange kein
        // Dialog offen ist. Die Gesten kommen aus
        // Anzeige/Tastenkuerzel.cs.
        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Speichern,
            nameof(AusgabenlisteViewModel.BearbeitenSpeichernCommand));

        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Schliessen,
            nameof(AusgabenlisteViewModel.BearbeitenAbbrechenCommand));

        DataContextChanged += OnDataContextChanged;

        // Ein Fokuswunsch aus einem anderen Bereich (Strg+F) trifft diese
        // Ansicht, bevor es sie gibt - das Ereignis unten liefe dann ins
        // Leere. Deshalb wird beim Laden nachgesehen, ob noch einer offen
        // ist.
        Loaded += (_, _) =>
        {
            if (DataContext is AusgabenlisteViewModel viewModel && viewModel.SucheFokusOffen)
            {
                FokussiereSuche(viewModel);
            }
        };
    }

    // Reine Fokus-Verdrahtung (Regel 7 betrifft Fachlogik, nicht
    // UI-Mechanik wie Tastaturfokus): das ViewModel meldet nur "Fokus
    // gewuenscht", welches Bedienelement das konkret ist, weiss nur die
    // Ansicht - dasselbe Muster wie in ErfassenView.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AusgabenlisteViewModel viewModel)
        {
            viewModel.FokusSucheAngefordert += (_, _) => FokussiereSuche(viewModel);
        }
    }

    private void FokussiereSuche(AusgabenlisteViewModel viewModel)
    {
        Suchfeld.Focus();
        viewModel.FokusSucheErledigt();
    }

    // Verlaesst der Anwender das Feld, steht dort die ausgerechnete
    // Normalform ("12,50+3,20" wird zu "15,70", "heute" zum Datum) -
    // dieselbe Verdrahtung wie in der Erfassungsmaske. Der DataContext
    // haengt hier am Feld selbst, weil der Bearbeiten-Dialog sein eigenes
    // ViewModel mitbringt und nicht das der Ansicht.
    private void BetragLostFocus(object? sender, RoutedEventArgs e) =>
        ((sender as Control)?.DataContext as AusgabeBearbeitenViewModel)?.BetragNormalisieren();

    private void DatumLostFocus(object? sender, RoutedEventArgs e) =>
        ((sender as Control)?.DataContext as AusgabeBearbeitenViewModel)?.DatumNormalisieren();

    // Dateiauswahl und Schreiben sind Aufgabe der Oberflaeche; der
    // CSV-Text selbst entsteht in Core (Reports.ReportCsv), und der Text
    // einer Fehlermeldung ebenfalls (Errors.FileErrorText) - Regel 7.
    // Wortgleich zum Export der Auswertung (ReportView.axaml.cs): derselbe
    // Vorgang soll sich nicht an zwei Stellen anders verhalten.
    private async void CsvExportieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AusgabenlisteViewModel viewModel)
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
            // beenden - der Hinweis steht neben der Schaltflaeche. Der mit
            // Abstand haeufigste Fall ist die noch in Excel geoeffnete
            // Zieldatei, und der bekommt einen eigenen Hinweis.
            AppLog.Current.Exception("Beim CSV-Export der Ausgabenliste", ex);

            viewModel.MeldeExport(
                FileErrorText.ForCsvExport(StorageProblems.Classify(ex), dateiname));
        }
    }

    // Liefert den tatsaechlich gewaehlten Dateinamen, damit eine
    // Fehlermeldung ihn nennen kann - oder NULL, wenn abgebrochen wurde.
    private async Task<string?> ExportiereCsv(AusgabenlisteViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return null;
        }

        var ziel = await fenster.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Ausgabenliste als CSV speichern",
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
