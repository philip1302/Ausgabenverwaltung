namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Intervalleinheit-Auswahl: deutsche Beschriftung plus der
/// in der Datenbank gespeicherte Wert ('day' | 'week' | 'month' | 'year',
/// siehe CHECK-Constraint in docs/schema_v1.sql). Rein fuer die Bindung.
/// </summary>
public sealed class IntervallOption
{
    public IntervallOption(string bezeichnung, string wert)
    {
        Bezeichnung = bezeichnung;
        Wert = wert;
    }

    public string Bezeichnung { get; }

    public string Wert { get; }

    /// <summary>Nur bei Monat und Jahr ist ein Ankertag ueberhaupt gemeint.</summary>
    public bool HatAnkertag => Wert is "month" or "year";
}
