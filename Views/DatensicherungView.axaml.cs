using System;
using System.Threading.Tasks;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Logging;
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
            AppLog.Current.Exception("Beim Auswaehlen des zweiten Sicherungsziels", ex);

            viewModel.MeldeFehler(
                "Der Ordnerdialog ließ sich nicht öffnen oder wurde unerwartet "
                + "beendet.\n\nEs wurde nichts verändert — das bisherige zweite Ziel "
                + "gilt unverändert weiter. Bitte versuchen Sie es noch einmal.");
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

        if (!Ordner.Oeffne(viewModel.SicherungsordnerPfad))
        {
            viewModel.MeldeFehler(
                "Der Sicherungsordner ließ sich nicht im Explorer öffnen.\n\n"
                + "Die Sicherungen selbst sind davon nicht betroffen — sie liegen "
                + "unverändert an ihrem Ort. Bitte rufen Sie diesen Pfad von Hand "
                + $"auf:\n{viewModel.SicherungsordnerPfad}");
        }
    }

    private void ProtokollordnerOeffnen_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatensicherungViewModel viewModel)
        {
            return;
        }

        if (!Ordner.Oeffne(viewModel.ProtokollordnerPfad))
        {
            viewModel.MeldeFehler(
                "Der Protokollordner ließ sich nicht im Explorer öffnen.\n\n"
                + "Bitte rufen Sie diesen Pfad von Hand auf:\n"
                + (viewModel.ProtokollordnerPfad ?? "(kein Protokollordner)"));
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
            AppLog.Current.Exception("Beim Kopieren des Datenbankpfads", ex);

            viewModel.MeldeFehler(
                "Der Pfad ließ sich nicht in die Zwischenablage legen. Möglicherweise "
                + "hält ein anderes Programm die Zwischenablage gerade besetzt.\n\n"
                + "Es wurde nichts verändert. Der Pfad steht hier zum Abtippen:\n"
                + viewModel.DatenbankPfad);
        }
    }
}
