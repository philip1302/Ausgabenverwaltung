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
public sealed class VerwaltungViewModel : ViewModelBase
{
    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }
    public VorlagenViewModel Vorlagen { get; }
    public DatensicherungViewModel Datensicherung { get; }

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
}
