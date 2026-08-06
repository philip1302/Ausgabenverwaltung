using System.ComponentModel;
using Ausgabenverwaltung.Core.Display;
using Avalonia;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Die eine Stelle, an der die globale Schriftgroesse haengt.
///
/// Zwei Wege fuehren von hier in die Oberflaeche:
/// 1. Die Schriftgroessen-Ressourcen der Anwendung (siehe App.axaml).
///    Alle Ansichten beziehen ihre Schriftgroessen ueber DynamicResource
///    daraus - deshalb wirkt eine Aenderung sofort und ohne Neustart.
/// 2. <see cref="Faktor"/> als Bindungsquelle fuer alles, was in Pixeln
///    festgelegt ist und mitwachsen muss: feste Feldbreiten und die
///    Spaltenraster der Tabellen (siehe <see cref="BreiteExtension"/> und
///    <see cref="Raster"/>). Ohne das bliebe eine 90 Pixel breite
///    Datumsspalte auch bei "Sehr groß" 90 Pixel breit und schnitte den
///    Text ab.
///
/// Bewusst ein Singleton und kein ViewModel: die Schriftgroesse gehoert
/// keinem Bereich, und die Bindungen in den Ansichten brauchen eine
/// Quelle, die unabhaengig vom DataContext ist.
/// </summary>
public sealed class Skalierung : INotifyPropertyChanged
{
    /// <summary>Schluessel der Schriftgroessen-Ressourcen (siehe App.axaml).</summary>
    public const string SchriftKleinKey = "SchriftKlein";
    public const string SchriftNormalKey = "SchriftNormal";
    public const string SchriftUeberschriftKey = "SchriftUeberschrift";

    /// <summary>Seitentitel im Kopf jedes Bereichs (UI/UX-Redesign, neue vierte Stufe).</summary>
    public const string SchriftSeitentitelKey = "SchriftSeitentitel";

    /// <summary>KPI-Zahlen auf der Startseite (UI/UX-Redesign).</summary>
    public const string SchriftKpiKey = "SchriftKpi";

    public static Skalierung Aktuell { get; } = new();

    private Skalierung()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double Faktor { get; private set; } = FontScales.DefaultFactor;

    /// <summary>
    /// Uebernimmt eine Stufe. Der Wert wird auf eine bekannte Stufe
    /// gerundet (siehe <see cref="FontScales.Normalize"/>), damit auch
    /// ein von Hand veraenderter Wert aus der Einstellungsdatei nie zu
    /// einer Groesse fuehrt, die niemand mehr zuruecknehmen kann.
    /// </summary>
    public void Setze(double faktor)
    {
        Faktor = FontScales.Normalize(faktor);

        SchreibeRessourcen();

        // Auch dann melden, wenn der Faktor derselbe geblieben ist: beim
        // Start ist das die erste Meldung ueberhaupt, und die Bindungen
        // sollen den Wert einmal holen.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Faktor)));
    }

    private void SchreibeRessourcen()
    {
        // Beim Start ist die Anwendung noch nicht aufgebaut, wenn der
        // gespeicherte Wert gelesen wird - dann trifft App.axaml die
        // Vorgabe, und die Ressourcen werden gleich danach ueberschrieben.
        if (Application.Current is not { } anwendung)
        {
            return;
        }

        var groessen = FontSizes.For(Faktor);

        anwendung.Resources[SchriftKleinKey] = groessen.Small;
        anwendung.Resources[SchriftNormalKey] = groessen.Normal;
        anwendung.Resources[SchriftUeberschriftKey] = groessen.Heading;
        anwendung.Resources[SchriftSeitentitelKey] = groessen.PageTitle;
        anwendung.Resources[SchriftKpiKey] = groessen.Kpi;
    }
}
