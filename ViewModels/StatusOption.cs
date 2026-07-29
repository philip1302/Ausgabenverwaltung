using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Status-Auswahl in der Filterleiste - deutsche
/// Beschriftung fuer einen <see cref="SettlementStatus"/>.
/// </summary>
public sealed class StatusOption
{
    public StatusOption(string bezeichnung, SettlementStatus wert)
    {
        Bezeichnung = bezeichnung;
        Wert = wert;
    }

    public string Bezeichnung { get; }
    public SettlementStatus Wert { get; }
}
