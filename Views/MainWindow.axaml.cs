using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Controls;

namespace Ausgabenverwaltung.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VerdrahteTastenkuerzel();
    }

    /// <summary>
    /// Die Kuerzel, die ueberall gelten. Sie haengen am Fenster und nicht
    /// an einer einzelnen Ansicht - erreichbar sind sie damit aus jedem
    /// Bereich, solange kein Eingabefeld die Taste selbst beansprucht.
    /// </summary>
    private void VerdrahteTastenkuerzel()
    {
        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.NeueBuchung,
            nameof(MainViewModel.NeueBuchungCommand));

        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Suche,
            nameof(MainViewModel.SucheFokussierenCommand));

        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Uebersicht,
            nameof(MainViewModel.OeffneTastenkuerzelCommand));

        // Strg+1 bis Strg+9: eine Bindung je Ziffer, alle auf dasselbe
        // Kommando, unterschieden ueber den Parameter. Welcher Bereich
        // dahintersteht, entscheidet der MainViewModel (BereicheMitZiffer).
        var bereiche = Tastenkuerzel.Fuer(TastenkuerzelAktion.BereichWechseln);
        foreach (var (geste, nummer) in bereiche.Gesten.Select((g, i) => (g, i + 1)))
        {
            Kuerzelbindung.BindeGeste(
                this, geste, nameof(MainViewModel.WaehleNummerCommand), nummer);
        }
    }
}
