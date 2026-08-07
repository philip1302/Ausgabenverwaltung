using System;
using Ausgabenverwaltung.Anzeige;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class AusgabenlisteView : UserControl
{
    public AusgabenlisteView()
    {
        InitializeComponent();

        // Strg+S / Strg+Enter speichern den Bearbeiten-Dialog, Esc
        // schliesst ihn. Beide Kommandos gehoeren dem ViewModel der
        // ANSICHT (nicht dem des Dialogs) und tun nichts, solange kein
        // Dialog offen ist. Die Gesten kommen aus
        // Anzeige/Tastenkuerzel.cs.
        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Speichern,
            nameof(AusgabenlisteViewModel.BearbeitenSpeichernCommand));

        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Schliessen,
            nameof(AusgabenlisteViewModel.BearbeitenAbbrechenCommand));

        DataContextChanged += OnDataContextChanged;

        // Ein Fokuswunsch aus einem anderen Bereich (Strg+F) trifft diese
        // Ansicht, bevor es sie gibt - das Ereignis unten liefe dann ins
        // Leere. Deshalb wird beim Laden nachgesehen, ob noch einer offen
        // ist.
        Loaded += (_, _) =>
        {
            if (DataContext is AusgabenlisteViewModel viewModel && viewModel.SucheFokusOffen)
            {
                FokussiereSuche(viewModel);
            }
        };
    }

    // Reine Fokus-Verdrahtung (Regel 7 betrifft Fachlogik, nicht
    // UI-Mechanik wie Tastaturfokus): das ViewModel meldet nur "Fokus
    // gewuenscht", welches Bedienelement das konkret ist, weiss nur die
    // Ansicht - dasselbe Muster wie in ErfassenView.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AusgabenlisteViewModel viewModel)
        {
            viewModel.FokusSucheAngefordert += (_, _) => FokussiereSuche(viewModel);
        }
    }

    private void FokussiereSuche(AusgabenlisteViewModel viewModel)
    {
        Suchfeld.Focus();
        viewModel.FokusSucheErledigt();
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
