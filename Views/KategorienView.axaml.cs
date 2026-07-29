using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class KategorienView : UserControl
{
    public KategorienView()
    {
        InitializeComponent();
    }

    // Reine Tastatur-/Maus-Verdrahtung (Regel 7 betrifft Fachlogik, nicht
    // UI-Mechanik): was F2/Doppelklick/Enter/Escape/Fokusverlust bewirken,
    // steckt in den ViewModel-Commands, hier wird nur das jeweilige
    // Rohereignis auf den passenden Command-Aufruf abgebildet.
    private void Baum_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2) return;
        if (DataContext is not KategorienViewModel viewModel) return;
        if (viewModel.AusgewaehlterKnoten is null) return;

        viewModel.BearbeitenStartenCommand.Execute(viewModel.AusgewaehlterKnoten);
        e.Handled = true;
    }

    private void Knoten_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.BearbeitenStartenCommand.Execute(knoten);
        e.Handled = true;
    }

    private void BearbeitungsTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        if (e.Key == Key.Enter)
        {
            viewModel.BearbeitenUebernehmenCommand.Execute(knoten);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.BearbeitenAbbrechenCommand.Execute(knoten);
            e.Handled = true;
        }
    }

    private void BearbeitungsTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.BearbeitenUebernehmenCommand.Execute(knoten);
    }
}
