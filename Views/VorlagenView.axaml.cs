using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.ViewModels;
using Avalonia.Controls;

namespace Ausgabenverwaltung.Views;

public partial class VorlagenView : UserControl
{
    public VorlagenView()
    {
        InitializeComponent();

        // Strg+S / Strg+Enter speichern das Vorlagenformular, Esc
        // schliesst es. Beide Kommandos tun nichts, solange kein Formular
        // offen ist. Die Gesten kommen aus Anzeige/Tastenkuerzel.cs -
        // dieselbe Quelle wie die Uebersichtsseite (F1).
        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Speichern,
            nameof(VorlagenViewModel.BearbeitenSpeichernCommand));

        Kuerzelbindung.Binde(
            this, TastenkuerzelAktion.Schliessen,
            nameof(VorlagenViewModel.BearbeitenAbbrechenCommand));
    }
}
