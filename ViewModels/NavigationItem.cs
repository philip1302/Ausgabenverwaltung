using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Gruppe der Hauptnavigation (UI/UX-Redesign, Abschnitt 3): die
/// Sidebar ist nicht mehr eine flache Liste, sondern in Abschnitte
/// gegliedert. <see cref="Keine"/> steht für die Startseite, die bewusst
/// ohne Kopfzeile über den übrigen Gruppen steht.
/// </summary>
public enum NavigationGruppe
{
    Keine,
    ErfassenUndVerwalten,
    Auswertung,
    Einstellungen,
}

/// <summary>
/// Ein Eintrag der Hauptnavigation: Anzeigename, Icon-Ressourcenschlüssel
/// (siehe App.axaml) und das ViewModel des zugehörigen Bereichs.
///
/// <see cref="IstAktiv"/> ist ein eigenes, beobachtbares Feld statt eines
/// Vergleichs in der Ansicht: MainViewModel setzt es fuer genau ein
/// Element auf true, sobald sich SelectedNavigationItem aendert (siehe
/// MainViewModel.AktualisiereAktivStatus) - so bindet Views/MainWindow.axaml
/// direkt "IstAktiv" statt eines Konverters, der zwei Objektreferenzen
/// vergleicht.
///
/// <see cref="VerwaltungsTabIndex"/> bildet die Unterpunkte der Gruppe
/// "Verwaltung" ab (UI/UX-Redesign, Abschnitt 3): die fünf Unterpunkte
/// teilen sich dasselbe <see cref="VerwaltungViewModel"/> als
/// <see cref="ViewModel"/> (dieselbe Instanz, mehrere Navigationseintraege)
/// und tragen zusaetzlich den Index des Tabs, der beim Auswaehlen aktiviert
/// werden soll.
/// </summary>
public sealed partial class NavigationItem : ObservableObject
{
    public NavigationItem(
        string title,
        ViewModelBase viewModel,
        string iconKey,
        NavigationGruppe gruppe = NavigationGruppe.Keine,
        bool istUnterpunkt = false,
        int? verwaltungsTabIndex = null)
    {
        Title = title;
        ViewModel = viewModel;
        IconKey = iconKey;
        Gruppe = gruppe;
        IstUnterpunkt = istUnterpunkt;
        VerwaltungsTabIndex = verwaltungsTabIndex;
    }

    public string Title { get; }
    public ViewModelBase ViewModel { get; }

    /// <summary>Schlüssel der StreamGeometry-Ressource in App.axaml (z. B. "IconStartseite").</summary>
    public string IconKey { get; }

    public NavigationGruppe Gruppe { get; }

    /// <summary>Ob dies ein eingerückter Unterpunkt der Gruppe "Verwaltung" ist.</summary>
    public bool IstUnterpunkt { get; }

    /// <summary>Bei einem Verwaltungs-Unterpunkt der Index des zu aktivierenden Tabs.</summary>
    public int? VerwaltungsTabIndex { get; }

    [ObservableProperty]
    private bool _istAktiv;

    /// <summary>Zaehler-Badge (z. B. Anzahl offener Posten), NULL = kein Badge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeSichtbar))]
    private int? _badgeAnzahl;

    public bool BadgeSichtbar => BadgeAnzahl is > 0;
}
