using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class PersonenView : UserControl
{
    public PersonenView()
    {
        InitializeComponent();
    }

    // Reine Tastatur-/Maus-Verdrahtung (Regel 7 betrifft Fachlogik, nicht
    // UI-Mechanik): was Doppelklick/Enter/Escape/Fokusverlust bewirken,
    // steckt in den ViewModel-Commands, hier wird nur das jeweilige
    // Rohereignis auf den passenden Command-Aufruf abgebildet.
    private void Zeile_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not PersonZeile zeile) return;
        if (DataContext is not PersonenViewModel viewModel) return;

        viewModel.BearbeitenStartenCommand.Execute(zeile);
        e.Handled = true;
    }

    private void BearbeitungsTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not PersonZeile zeile) return;
        if (DataContext is not PersonenViewModel viewModel) return;

        if (e.Key == Key.Enter)
        {
            viewModel.BearbeitenUebernehmenCommand.Execute(zeile);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.BearbeitenAbbrechenCommand.Execute(zeile);
            e.Handled = true;
        }
    }

    private void BearbeitungsTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not PersonZeile zeile) return;
        if (DataContext is not PersonenViewModel viewModel) return;

        viewModel.BearbeitenUebernehmenCommand.Execute(zeile);
    }
}
