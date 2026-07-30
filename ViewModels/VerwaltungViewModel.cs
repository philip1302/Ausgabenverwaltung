namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien, Vorlagen fuer
/// wiederkehrende Buchungen, Datensicherung und Darstellung pflegen.
/// Alle fuenf Unterbereiche sind eigene ViewModels (siehe
/// <see cref="PersonenViewModel"/>, <see cref="KategorienViewModel"/>,
/// <see cref="VorlagenViewModel"/>, <see cref="DatensicherungViewModel"/>
/// und <see cref="DarstellungViewModel"/>); hier findet nur die
/// Verdrahtung statt.
/// </summary>
public sealed class VerwaltungViewModel : ViewModelBase
{
    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }
    public VorlagenViewModel Vorlagen { get; }
    public DatensicherungViewModel Datensicherung { get; }
    public DarstellungViewModel Darstellung { get; }

    public VerwaltungViewModel(
        PersonenViewModel personen,
        KategorienViewModel kategorien,
        VorlagenViewModel vorlagen,
        DatensicherungViewModel datensicherung,
        DarstellungViewModel darstellung)
    {
        Personen = personen;
        Kategorien = kategorien;
        Vorlagen = vorlagen;
        Datensicherung = datensicherung;
        Darstellung = darstellung;
    }
}
