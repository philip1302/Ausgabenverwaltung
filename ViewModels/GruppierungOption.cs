using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Zeitgruppierung in der Filterleiste des Reports.
/// </summary>
public sealed class GruppierungOption
{
    public GruppierungOption(string bezeichnung, ReportGrouping wert)
    {
        Bezeichnung = bezeichnung;
        Wert = wert;
    }

    public string Bezeichnung { get; }
    public ReportGrouping Wert { get; }
}
