using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien, Vorlagen fuer
/// wiederkehrende Buchungen und Datensicherung pflegen. Alle vier
/// Unterbereiche sind eigene ViewModels (siehe
/// <see cref="PersonenViewModel"/>, <see cref="KategorienViewModel"/>,
/// <see cref="VorlagenViewModel"/> und
/// <see cref="DatensicherungViewModel"/>); hier findet nur die
/// Verdrahtung statt.
/// </summary>
public sealed partial class VerwaltungViewModel : ViewModelBase
{
    // Reihenfolge der Reiter in VerwaltungView.axaml.
    private const int DatensicherungTabIndex = 3;

    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }
    public VorlagenViewModel Vorlagen { get; }
    public DatensicherungViewModel Datensicherung { get; }

    /// <summary>
    /// Der offene Reiter. Gebunden, damit der Menuepunkt "Sicherung jetzt"
    /// nicht nur sichert, sondern auch dorthin fuehrt, wo das Ergebnis und
    /// die Liste der Sicherungen stehen.
    /// </summary>
    [ObservableProperty]
    private int _ausgewaehlterTabIndex;

    public VerwaltungViewModel(
        PersonenViewModel personen,
        KategorienViewModel kategorien,
        VorlagenViewModel vorlagen,
        DatensicherungViewModel datensicherung)
    {
        Personen = personen;
        Kategorien = kategorien;
        Vorlagen = vorlagen;
        Datensicherung = datensicherung;
    }

    public void ZeigeDatensicherung() => AusgewaehlterTabIndex = DatensicherungTabIndex;
}
