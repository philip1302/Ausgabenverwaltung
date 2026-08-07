using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class ErfassenView : UserControl
{
    public ErfassenView()
    {
        InitializeComponent();
        Loaded += (_, _) => BetragBox.Focus();
        DataContextChanged += OnDataContextChanged;
    }

    // Reine Fokus-Verdrahtung (Regel 7 betrifft Fachlogik, nicht UI-
    // Mechanik wie Tastaturfokus): das ViewModel meldet nur "Fokus
    // gewuenscht", welcher Control das konkret ist, weiss nur die View.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ErfassenViewModel viewModel)
        {
            viewModel.FokusBetragAngefordert += (_, _) => BetragBox.Focus();
        }
    }

    // Verlaesst der Anwender das Feld, steht dort die ausgerechnete
    // Normalform ("12,50+3,20" wird zu "15,70", "heute" zum Datum). Auch
    // das ist reine Verdrahtung: gerechnet wird in Core, das ViewModel
    // schreibt nur zurueck (Regel 7).
    private void BetragLostFocus(object? sender, RoutedEventArgs e) =>
        (DataContext as ErfassenViewModel)?.BetragNormalisieren();

    private void DatumLostFocus(object? sender, RoutedEventArgs e) =>
        (DataContext as ErfassenViewModel)?.DatumNormalisieren();
}
