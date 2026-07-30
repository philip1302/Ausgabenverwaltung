namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeitabschnitt-Spalte der Kreuztabelle: der technische Schluessel
/// ("2026-03") und der Text im Spaltenkopf ("Mär 2026").
/// </summary>
public sealed class ReportSpalte
{
    public ReportSpalte(string key, string beschriftung)
    {
        Key = key;
        Beschriftung = beschriftung;
    }

    public string Key { get; }
    public string Beschriftung { get; }
}
