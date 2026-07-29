using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Report" - Auswertungen ueber Kategorie-Aeste und Zeitraeume.
/// Noch ohne Fachfunktion, nur der Rahmen.
/// </summary>
public sealed class ReportViewModel : ViewModelBase
{
    private readonly ReportRepository _reportRepository;

    public ReportViewModel(ReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public string PlatzhalterText => "Report - hier entstehen spaeter die Auswertungen.";
}
