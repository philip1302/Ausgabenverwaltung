using System.Collections.Generic;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Knoten der Kategorie-Auswahl in der Filterleiste der Ausgabenliste.
/// Bewusst nicht <see cref="KategorieKnoten"/> wiederverwendet: der traegt
/// den Bearbeitungszustand der Verwaltungsansicht (Umbenennen, Neuanlage),
/// den es hier nicht gibt. Gefiltert wird immer auf den ganzen Ast, der
/// Knoten muss also kein Blatt sein.
/// </summary>
public sealed class KategorieFilterKnoten
{
    public KategorieFilterKnoten(int id, string name, string fullPath, bool isArchived)
    {
        Id = id;
        Name = name;
        FullPath = fullPath;
        IsArchived = isArchived;
    }

    public int Id { get; }
    public string Name { get; }

    /// <summary>Voller Pfad ab der Wurzel, fuer die Anzeige des gewaehlten Filters.</summary>
    public string FullPath { get; }

    public bool IsArchived { get; }

    public List<KategorieFilterKnoten> Children { get; } = new();
}
