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

    // Beim Oeffnen der Farbwahl haelt das ViewModel fest, um welche
    // Kategorie es geht - danach darf der Baum seine Auswahl verlieren,
    // ohne dass die Farbwahl ins Leere greift.
    private void Farbwahl_Oeffnen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.OeffneFarbwahl();
    }

    // Wahl einer Farbe: Command des ViewModels ausfuehren und die Auswahl
    // schliessen, damit der umgefaerbte Baum dahinter sichtbar wird.
    private void Farbe_Gewaehlt(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not FarbOption option) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.FarbeSetzenCommand.Execute(option);
        FarbAuswahl.Flyout?.Hide();
    }

    private void BearbeitungsTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.BearbeitenUebernehmenCommand.Execute(knoten);
    }
}
