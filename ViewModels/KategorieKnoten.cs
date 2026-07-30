using System.Collections.ObjectModel;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Categories;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Knoten im UI-Kategoriebaum. Fasst die Daten einer Category mit
/// Anzeige- und Bearbeitungszustand fuer die Baumansicht zusammen -
/// bewusst getrennt von Ausgabenverwaltung.Core.Entities.Category, damit
/// die Core-Entitaet keinen UI-Zustand tragen muss (Regel 7).
/// </summary>
public sealed partial class KategorieKnoten : ObservableObject
{
    /// <summary>NULL, solange der Knoten neu angelegt und noch nicht
    /// bestaetigt (also noch nicht in der DB gespeichert) ist.</summary>
    public int? Id { get; private set; }

    public int? ParentId { get; }

    /// <summary>Nur zum Entfernen abgebrochener Neuanlagen aus dem
    /// Baum - reine UI-Mechanik, keine Fachlogik.</summary>
    public KategorieKnoten? Eltern { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Anzeigename))]
    private string _name;

    [ObservableProperty]
    private bool _isArchived;

    /// <summary>
    /// Die EIGENE Farbe der Kategorie ('#RRGGBB') oder NULL, wenn sie
    /// erbt. Getrennt von <see cref="Farbe"/> gehalten, weil die Auswahl
    /// wissen muss, ob hier tatsaechlich etwas gesetzt ist - "keine
    /// Farbe" ist eine eigene Auswahlmoeglichkeit.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatEigeneFarbe))]
    [NotifyPropertyChangedFor(nameof(FarbHinweis))]
    private string? _eigeneFarbe;

    /// <summary>
    /// Die tatsaechlich angezeigte Farbe - eigene oder geerbte (siehe
    /// <see cref="Ausgabenverwaltung.Core.Categories.CategoryColors"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FarbHinweis))]
    private IBrush _farbe = Farbpinsel.Fuer(null);

    public bool HatEigeneFarbe => EigeneFarbe is not null;

    /// <summary>
    /// Beschreibt die Farbe in Worten. Ohne diesen Text waere der Punkt
    /// die einzige Auskunft ueber die Farbe - und damit fuer jeden
    /// wertlos, der Farben nicht unterscheiden kann.
    /// </summary>
    public string FarbHinweis => EigeneFarbe is { } eigene
        ? $"Farbe: {CategoryColorPalette.NameOf(eigene) ?? eigene}"
        : "Farbe: geerbt";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Anzeigename))]
    private int _ausgabenAnzahl;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _wirdBearbeitet;

    [ObservableProperty]
    private string _bearbeitungsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BearbeitungsFehlerSichtbar))]
    private string? _bearbeitungsFehler;

    public bool BearbeitungsFehlerSichtbar => !string.IsNullOrEmpty(BearbeitungsFehler);

    public string Anzeigename => AusgabenAnzahl > 0 ? $"{Name} ({AusgabenAnzahl})" : Name;

    public bool IstNeuUndUnbestaetigt => Id is null;

    public ObservableCollection<KategorieKnoten> Children { get; } = new();

    public KategorieKnoten(int? id, int? parentId, string name, bool isArchived, int ausgabenAnzahl)
    {
        Id = id;
        ParentId = parentId;
        _name = name;
        _isArchived = isArchived;
        _ausgabenAnzahl = ausgabenAnzahl;
    }

    public void UebernehmeErstellteId(int id) => Id = id;
}
