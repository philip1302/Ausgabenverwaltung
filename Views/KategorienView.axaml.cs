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

    // "+ Unter" sitzt jetzt in der Zeile selbst (UI/UX-Redesign, Abschnitt
    // 5.5) statt an einer Werkzeugleiste, die eine vorherige Baumauswahl
    // voraussetzt. NeueUnterkategorieCommand haengt weiterhin an
    // AusgewaehlterKnoten (Regel 7: die Fachlogik bleibt unveraendert) -
    // hier wird nur diese eine Auswahl aus der Zeile heraus gesetzt, bevor
    // der bestehende Befehl laeuft.
    private void NeueUnterkategorieFuerZeile(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.AusgewaehlterKnoten = knoten;
        if (viewModel.NeueUnterkategorieCommand.CanExecute(null))
        {
            viewModel.NeueUnterkategorieCommand.Execute(null);
        }
    }

    // Beim Oeffnen der Farbwahl haelt das ViewModel fest, um welche
    // Kategorie es geht - der Farbe-Knopf sitzt in der Zeile selbst, sein
    // DataContext ist deshalb bereits der betroffene Knoten.
    private void Farbwahl_Oeffnen(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.OeffneFarbwahlFuer(knoten);
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

    // Nach der Wahl der Zielkategorie schliesst sich das Aufklappfenster,
    // damit die Vorschau darunter sichtbar wird. Die Auswahl selbst laeuft
    // ueber das Command am Knoten-Button, nicht ueber dieses Ereignis.
    private void ZielFlyout_Schliessen(object? sender, RoutedEventArgs e)
    {
        ZielAuswahl.Flyout?.Hide();
    }

    private void BearbeitungsTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not KategorieKnoten knoten) return;
        if (DataContext is not KategorienViewModel viewModel) return;

        viewModel.BearbeitenUebernehmenCommand.Execute(knoten);
    }
}
