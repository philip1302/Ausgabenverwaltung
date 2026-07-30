using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Ausgabenverwaltung.Views;

public partial class DatensicherungView : UserControl
{
    public DatensicherungView()
    {
        InitializeComponent();
    }

    // Ordnerdialog, Explorer und Zwischenablage sind Bedienmechanik der
    // Oberflaeche (Regel 7 betrifft Fachlogik): das Pruefen des gewaehlten
    // Ordners und alles Weitere passiert in Core bzw. im ViewModel.
    private async void ZweitesZielWaehlen_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatensicherungViewModel viewModel)
        {
            return;
        }

        try
        {
            await WaehleZweitesZiel(viewModel);
        }
        catch (Exception ex)
        {
            viewModel.MeldeFehler($"Der Ordner konnte nicht ausgewählt werden: {ex.Message}");
        }
    }

    private async Task WaehleZweitesZiel(DatensicherungViewModel viewModel)
    {
        var fenster = TopLevel.GetTopLevel(this);
        if (fenster is null)
        {
            return;
        }

        var ordner = await fenster.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Ordner für die zusätzliche Sicherung wählen",
            AllowMultiple = false,
        });

        if (ordner.Count == 0)
        {
            return;
        }

        // TryGetLocalPath liefert NULL fuer Orte ohne Dateisystempfad
        // (etwa virtuelle Ordner). Ohne Pfad kann nicht dorthin kopiert
        // werden - die Anwendung kennt ausschliesslich Pfade.
        var pfad = ordner[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(pfad))
        {
            viewModel.MeldeFehler(
                "Dieser Ort hat keinen Ordnerpfad im Dateisystem und kann nicht als Ziel dienen.");
            return;
        }

        viewModel.SetzeZweitesZiel(pfad);
    }

    private void SicherungsordnerOeffnen_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatensicherungViewModel viewModel)
        {
            return;
        }

        try
        {
            // UseShellExecute laesst das Betriebssystem den Ordner in
            // seinem Dateimanager oeffnen - unter Windows der Explorer.
            Process.Start(new ProcessStartInfo
            {
                FileName = viewModel.SicherungsordnerPfad,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            viewModel.MeldeFehler($"Der Sicherungsordner konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private async void DatenbankPfadKopieren_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatensicherungViewModel viewModel)
        {
            return;
        }

        var zwischenablage = TopLevel.GetTopLevel(this)?.Clipboard;
        if (zwischenablage is null)
        {
            return;
        }

        try
        {
            await zwischenablage.SetTextAsync(viewModel.DatenbankPfad);
            viewModel.MeldeErfolg("Der Pfad der aktiven Datenbank liegt in der Zwischenablage.");
        }
        catch (Exception ex)
        {
            viewModel.MeldeFehler($"Der Pfad konnte nicht kopiert werden: {ex.Message}");
        }
    }
}
