using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien, Vorlagen fuer
/// wiederkehrende Buchungen, Datensicherung und Darstellung pflegen.
/// Alle fuenf Unterbereiche sind eigene ViewModels (siehe
/// <see cref="PersonenViewModel"/>, <see cref="KategorienViewModel"/>,
/// <see cref="VorlagenViewModel"/>, <see cref="DatensicherungViewModel"/>
/// und <see cref="DarstellungViewModel"/>); hier findet nur die
/// Verdrahtung statt.
///
/// <see cref="AusgewaehlterTabIndex"/> ist neu (UI/UX-Redesign, Abschnitt
/// 3): die Sidebar springt von den fuenf Unterpunkten der Gruppe
/// "Verwaltung" direkt in den passenden Tab, statt immer bei Kategorien
/// zu landen (siehe MainViewModel und Views/VerwaltungView.axaml). Die
/// Tab-Leiste innerhalb der Seite bleibt zusaetzlich als Kontext-
/// Umschalter erhalten.
/// </summary>
public sealed partial class VerwaltungViewModel : ViewModelBase
{
    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }
    public VorlagenViewModel Vorlagen { get; }
    public DatensicherungViewModel Datensicherung { get; }
    public DarstellungViewModel Darstellung { get; }

    [ObservableProperty]
    private int _ausgewaehlterTabIndex;

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
