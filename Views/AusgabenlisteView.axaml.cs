using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ausgabenverwaltung.Views;

public partial class AusgabenlisteView : UserControl
{
    public AusgabenlisteView()
    {
        InitializeComponent();
    }

    // Reine Bedienmechanik, keine Fachlogik (Regel 7): nach der Wahl einer
    // Kategorie schliesst sich das Flyout, damit die Trefferliste sichtbar
    // wird, deren Filter sich gerade geaendert hat. Die Auswahl selbst
    // laeuft ueber das Command am Knoten-Button, nicht ueber dieses
    // Ereignis.
    private void KategorieFlyout_Schliessen(object? sender, RoutedEventArgs e)
    {
        KategorieAuswahl.Flyout?.Hide();
    }
}
