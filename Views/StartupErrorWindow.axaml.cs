using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ausgabenverwaltung.Views;

// Reines Anzeigefenster fuer einen Startabbruch (z. B. zu neue
// SchemaVersion) - zeigt nur eine feste Meldung und beendet die
// Anwendung, enthaelt keine Fachlogik (Regel 7).
public partial class StartupErrorWindow : Window
{
    public StartupErrorWindow()
    {
        InitializeComponent();
    }

    public StartupErrorWindow(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnBeendenClick(object? sender, RoutedEventArgs e) => Close();
}
