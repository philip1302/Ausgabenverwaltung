using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Zielliste im Bereich "Datensicherung": ein Name, ein
/// Statuspunkt, ein Zeitpunkt, ein gekuerzter Pfad und - nur wenn es
/// etwas zu melden gibt - ein Problemtext.
///
/// Sie ersetzt die vorher je Ziel eigenen Status- und Fehlertexte samt
/// zweier verschieden gebauter Fehlerkaesten. Der Inhalt ist derselbe
/// geblieben, er hat nur noch EINE Bauform: der Gesamtzustand steht in
/// der Karte darueber, hier steht, welches Ziel ihn verursacht.
///
/// Der Statuspunkt benutzt dieselbe Einteilung wie der Gesamtzustand
/// (<see cref="BackupHealthLevel"/>) - zwei Skalen nebeneinander waeren
/// genau der Fehler, den dieser Umbau beseitigt.
/// </summary>
public sealed record ZielZeile
{
    public required string Name { get; init; }

    public required BackupHealthLevel Punkt { get; init; }

    /// <summary>Wann zuletzt hierher gesichert wurde, als fertiger Text.</summary>
    public required string ZeitpunktText { get; init; }

    /// <summary>Der volle Pfad - fuer Hilfetext, "Pfad kopieren" und
    /// "Ordner öffnen". NULL, wenn nicht eingerichtet.</summary>
    public string? Pfad { get; init; }

    /// <summary>Der letzte Fehlschlag dieses Ziels, sonst NULL. Bleibt
    /// stehen, bis eine Sicherung gelingt.</summary>
    public string? ProblemText { get; init; }

    /// <summary>Ziel 1 ist immer eingerichtet, Ziel 2 nur nach Wahl.</summary>
    public bool IstEingerichtet { get; init; }

    public bool PfadVorhanden => !string.IsNullOrWhiteSpace(Pfad);

    /// <summary>Der Pfad, wie er in die Zeile passt - vollstaendig steht
    /// er im Hilfetext daneben.</summary>
    public string PfadKurz => PfadKuerzung.Kuerze(Pfad);

    public bool ProblemSichtbar => ProblemText is not null;

    // Drei Bindungen statt einer Umwandlung in der Ansicht: Avalonia
    // vergleicht in Classes.x-Bindungen nur auf Wahrheitswerte.
    public bool PunktGut => Punkt == BackupHealthLevel.Gesichert;
    public bool PunktEingeschraenkt => Punkt == BackupHealthLevel.Eingeschraenkt;
    public bool PunktSchlecht => Punkt == BackupHealthLevel.NichtGesichert;
}
