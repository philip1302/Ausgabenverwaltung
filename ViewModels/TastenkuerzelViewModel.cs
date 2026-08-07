using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Ueberschrift der Kuerzeluebersicht samt der Zeilen darunter. Ein
/// eigener Typ und keine Gruppierung aus LINQ: eine <c>IGrouping</c> laesst
/// sich in einer Vorlage nur ueber Umwege binden, und der Name der Gruppe
/// waere dort ein <c>Key</c>.
/// </summary>
public sealed class TastenkuerzelGruppe
{
    public TastenkuerzelGruppe(string name, IReadOnlyList<Tastenkuerzel> kuerzel)
    {
        Name = name;
        Kuerzel = kuerzel;
    }

    public string Name { get; }

    public IReadOnlyList<Tastenkuerzel> Kuerzel { get; }
}

/// <summary>
/// Die Seite "Tastenkürzel" (F1).
///
/// Sie erfindet nichts: ihre Zeilen sind genau die Eintraege aus
/// <see cref="Tastenkuerzel.Alle"/>, aus denen sich auch die Bindungen im
/// Hauptfenster und in den Ansichten aufbauen. Eine Hilfeseite, die ihren
/// eigenen Bestand pflegt, ist nach dem zweiten neuen Kuerzel falsch, ohne
/// dass es jemandem auffaellt.
///
/// Ein eigener Bereich ohne Sidebar-Platz, kein Dialogfenster - dasselbe
/// Muster wie "Darstellung" und "Was ist neu": eine Seite laesst sich
/// rollen und in der eingestellten Schriftgroesse lesen.
/// </summary>
public sealed partial class TastenkuerzelViewModel : ViewModelBase
{
    /// <summary>
    /// Wird ausgeloest, wenn der Anwender die Uebersicht wegklickt. Die
    /// Seite kennt die Navigation nicht - der <see cref="MainViewModel"/>
    /// hoert zu und kehrt dorthin zurueck, wo der Anwender vorher war
    /// (dasselbe Muster wie bei <see cref="WasIstNeuViewModel"/>).
    /// </summary>
    public event EventHandler? Geschlossen;

    public IReadOnlyList<TastenkuerzelGruppe> Gruppen { get; } =
        Tastenkuerzel.NachGruppe()
                     .Select(gruppe => new TastenkuerzelGruppe(gruppe.Key, gruppe.ToList()))
                     .ToList();

    /// <summary>
    /// Der Hinweis unter der Liste. Er steht hier und nicht in der Ansicht,
    /// weil die Zahl darin aus derselben Quelle kommt wie die Kuerzel
    /// selbst.
    /// </summary>
    public string Fussnote =>
        $"Die Ziffern 1 bis {Tastenkuerzel.BereicheMitZiffer} zählen die Einträge der "
        + "Seitenleiste von oben nach unten, die Startseite ist die 1.";

    [RelayCommand]
    private void Schliessen() => Geschlossen?.Invoke(this, EventArgs.Empty);
}
