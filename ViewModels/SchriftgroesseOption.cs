using Ausgabenverwaltung.Core.Display;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine waehlbare Schriftgroessen-Stufe in der Darstellungs-Einstellung.
/// Beschriftung und Faktor kommen aus Core (<see cref="FontScales"/>);
/// hier kommt nur dazu, ob die Stufe gerade gewaehlt ist - das braucht
/// der Auswahlknopf in der Ansicht.
/// </summary>
public sealed partial class SchriftgroesseOption : ObservableObject
{
    public SchriftgroesseOption(FontScaleStep stufe, bool istAusgewaehlt)
    {
        Stufe = stufe;
        Bezeichnung = FontScales.Label(stufe);
        Faktor = FontScales.Factor(stufe);
        _istAusgewaehlt = istAusgewaehlt;
    }

    public FontScaleStep Stufe { get; }

    public string Bezeichnung { get; }

    public double Faktor { get; }

    /// <summary>
    /// Der Faktor als Prozentangabe - die Stufen sollen nicht nur einen
    /// Namen haben, sondern auch verraten, wie weit sie auseinanderliegen.
    /// </summary>
    public string FaktorText => $"{Faktor * 100:0}%";

    [ObservableProperty]
    private bool _istAusgewaehlt;
}
