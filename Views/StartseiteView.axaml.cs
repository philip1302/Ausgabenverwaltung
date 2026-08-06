using Avalonia.Controls;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Views;

public partial class StartseiteView : UserControl
{
    public StartseiteView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Meldet dem ViewModel die Groesse der Zeichenflaeche, sobald sie
    /// feststeht oder sich aendert.
    ///
    /// Das ist Verdrahtung und keine Fachlogik (Regel 7): ein Diagramm
    /// braucht echte Bildpunkte, und die kennt erst das Layout. Gerechnet
    /// wird daraus hier nichts - das erledigt Core.Charts.BarChart ueber
    /// StartseiteViewModel.ZeichenflaecheGeaendert.
    /// </summary>
    private void Zeichenflaeche_Groesse(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is StartseiteViewModel viewModel)
        {
            viewModel.ZeichenflaecheGeaendert(e.NewSize.Width, e.NewSize.Height);
        }
    }
}
