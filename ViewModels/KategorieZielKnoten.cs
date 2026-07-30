using System.Collections.Generic;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Knoten der Zielauswahl beim Zusammenfuehren zweier Kategorien.
///
/// Bewusst weder <see cref="KategorieKnoten"/> (der traegt den
/// Bearbeitungszustand der Verwaltungsansicht) noch
/// <see cref="KategorieFilterKnoten"/> (dort ist JEDER Knoten waehlbar,
/// weil ueber den ganzen Ast gefiltert wird). Hier ist nur ein Blatt
/// waehlbar: Ausgaben lassen sich auch sonst nirgends anders zuordnen.
/// Die uebrigen Knoten bleiben trotzdem sichtbar - ohne sie waere der
/// Baum nicht begehbar.
/// </summary>
public sealed class KategorieZielKnoten
{
    public KategorieZielKnoten(int id, string name, string fullPath, bool istArchiviert, bool istWaehlbar)
    {
        Id = id;
        Name = name;
        FullPath = fullPath;
        IstArchiviert = istArchiviert;
        IstWaehlbar = istWaehlbar;
    }

    public int Id { get; }

    public string Name { get; }

    /// <summary>Voller Pfad ab der Wurzel - er steht in der Vorschau und
    /// in der Bestaetigung, wo der blosse Name mehrdeutig waere.</summary>
    public string FullPath { get; }

    public bool IstArchiviert { get; }

    /// <summary>
    /// Blattknoten, nicht archiviert und nicht die Quelle selbst. Alles
    /// andere steht nur zum Aufklappen da.
    /// </summary>
    public bool IstWaehlbar { get; }

    public List<KategorieZielKnoten> Children { get; } = new();
}
