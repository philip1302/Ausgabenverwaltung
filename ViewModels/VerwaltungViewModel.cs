using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Kategorien, Personen und Datensicherung
/// pflegen. Alle Unterbereiche sind eigene ViewModels (siehe
/// <see cref="PersonenViewModel"/>, <see cref="KategorienViewModel"/> und
/// <see cref="DatensicherungViewModel"/>); hier findet nur die
/// Verdrahtung statt.
///
/// <see cref="AusgewaehlterTabIndex"/> ist neu (UI/UX-Redesign, Abschnitt
/// 3): die Sidebar springt von den drei Unterpunkten der Gruppe
/// "Verwaltung" direkt in den passenden Tab, statt immer bei Kategorien
/// zu landen (siehe MainViewModel und Views/VerwaltungView.axaml). Die
/// Tab-Leiste innerhalb der Seite bleibt zusaetzlich als Kontext-
/// Umschalter erhalten.
///
/// <see cref="Vorlagen"/> und <see cref="Darstellung"/> stehen weiterhin
/// hier, WERDEN ABER NICHT MEHR ALS TAB ANGEZEIGT: beide sind
/// eigenstaendige Bereiche mit eigenem Navigationseintrag (siehe
/// MainViewModel). Sie bleiben Eigentum dieser Klasse, weil sie ueber
/// dieselbe DI-Instanz laufen und die Sidebar-Fusszeile den Zustand von
/// <see cref="Darstellung"/> anzeigt.
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
