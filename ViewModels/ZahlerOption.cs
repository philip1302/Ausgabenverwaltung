using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Zahler in der Filterleiste, einzeln an- und abwaehlbar.
///
/// Frueher war das eine Auswahlliste mit einem Eintrag "alle" ohne Id;
/// seit die Zahler kombinierbar sind, gibt es diesen Sonderfall nicht mehr:
/// <b>kein Haekchen heisst "alle"</b> - dieselbe Regel wie bei Kategorien
/// und Status (siehe Core.Reports.ReportFilter).
/// </summary>
public sealed class ZahlerOption : ObservableObject
{
    private bool _istGewaehlt;

    public ZahlerOption(string bezeichnung, int id, bool istSelbst)
    {
        Bezeichnung = bezeichnung;
        Id = id;
        IstSelbst = istSelbst;
    }

    public string Bezeichnung { get; }

    public int Id { get; }

    /// <summary>Ob das die eigene Person ist - siehe Regel 4.</summary>
    public bool IstSelbst { get; }

    /// <summary>Wird vom ViewModel gesetzt und meldet eine Anwenderaenderung.</summary>
    internal Action? BeiAenderung { get; set; }

    public bool IstGewaehlt
    {
        get => _istGewaehlt;
        set
        {
            if (SetProperty(ref _istGewaehlt, value))
            {
                BeiAenderung?.Invoke();
            }
        }
    }

    /// <summary>Setzt das Haekchen ohne Meldung - fuer das Zuruecksetzen.</summary>
    internal void SetzeStill(bool wert) => SetProperty(ref _istGewaehlt, wert, nameof(IstGewaehlt));
}
