using Ausgabenverwaltung.Core.Display;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine waehlbare Themenvariante in der Darstellungs-Einstellung
/// (UI/UX-Redesign, Verwaltung ▸ Darstellung, Thema-Umschalter Hell/
/// Dunkel/System). Dasselbe Muster wie <see cref="SchriftgroesseOption"/>:
/// Beschriftung und Modus stehen fest, nur die Auswahl ist beobachtbar.
/// </summary>
public sealed partial class ThemaOption : ObservableObject
{
    public ThemaOption(ThemeMode modus, string bezeichnung, bool istAusgewaehlt)
    {
        Modus = modus;
        Bezeichnung = bezeichnung;
        _istAusgewaehlt = istAusgewaehlt;
    }

    public ThemeMode Modus { get; }

    public string Bezeichnung { get; }

    [ObservableProperty]
    private bool _istAusgewaehlt;
}
