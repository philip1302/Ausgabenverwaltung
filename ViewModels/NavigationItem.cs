namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Hauptnavigation: Anzeigename plus das ViewModel des
/// zugehoerigen Bereichs. Rein fuer die Bindung der Navigationsliste,
/// keine eigene Logik.
/// </summary>
public sealed class NavigationItem
{
    public NavigationItem(string title, ViewModelBase viewModel)
    {
        Title = title;
        ViewModel = viewModel;
    }

    public string Title { get; }
    public ViewModelBase ViewModel { get; }
}
