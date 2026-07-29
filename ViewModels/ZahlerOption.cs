namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Zahler-Auswahl in der Filterleiste. Der erste Eintrag
/// steht fuer "alle Zahler" und hat deshalb keine Id.
/// </summary>
public sealed class ZahlerOption
{
    public ZahlerOption(string bezeichnung, int? id)
    {
        Bezeichnung = bezeichnung;
        Id = id;
    }

    public string Bezeichnung { get; }

    /// <summary>NULL = keine Einschraenkung auf einen Zahler.</summary>
    public int? Id { get; }
}
