namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien und Vorlagen fuer
/// wiederkehrende Buchungen pflegen. Alle drei Unterbereiche sind eigene
/// ViewModels (siehe <see cref="PersonenViewModel"/>,
/// <see cref="KategorienViewModel"/> und <see cref="VorlagenViewModel"/>);
/// hier findet nur die Verdrahtung statt.
/// </summary>
public sealed class VerwaltungViewModel : ViewModelBase
{
    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }
    public VorlagenViewModel Vorlagen { get; }

    public VerwaltungViewModel(
        PersonenViewModel personen,
        KategorienViewModel kategorien,
        VorlagenViewModel vorlagen)
    {
        Personen = personen;
        Kategorien = kategorien;
        Vorlagen = vorlagen;
    }
}
