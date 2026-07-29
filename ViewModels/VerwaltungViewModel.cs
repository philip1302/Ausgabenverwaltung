namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien und Vorlagen fuer
/// wiederkehrende Buchungen pflegen. Der Kategorien-Unterbereich ist
/// bereits fachlich fertig (siehe <see cref="KategorienViewModel"/>),
/// Personen und Vorlagen sind noch Platzhalter.
/// </summary>
public sealed class VerwaltungViewModel : ViewModelBase
{
    public KategorienViewModel Kategorien { get; }

    public VerwaltungViewModel(KategorienViewModel kategorien)
    {
        Kategorien = kategorien;
    }

    public string PersonenPlatzhalterText => "Personen - hier entsteht spaeter die Pflege der Personen.";

    public string VorlagenPlatzhalterText =>
        "Vorlagen - hier entsteht spaeter die Pflege wiederkehrender Buchungen.";
}
