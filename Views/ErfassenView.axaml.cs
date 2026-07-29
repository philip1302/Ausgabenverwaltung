using System;
using Avalonia.Controls;
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
}
