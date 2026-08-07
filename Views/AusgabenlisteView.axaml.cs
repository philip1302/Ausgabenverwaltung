using Avalonia.Controls;
using Avalonia.Interactivity;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class AusgabenlisteView : UserControl
{
    public AusgabenlisteView()
    {
        InitializeComponent();
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
}
