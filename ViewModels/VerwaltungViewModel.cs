namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung" - Personen, Kategorien und Vorlagen fuer
/// wiederkehrende Buchungen pflegen. Personen- und Kategorien-Unterbereich
/// sind bereits fachlich fertig (siehe <see cref="PersonenViewModel"/> und
/// <see cref="KategorienViewModel"/>), Vorlagen ist noch ein Platzhalter.
/// </summary>
public sealed class VerwaltungViewModel : ViewModelBase
{
    public PersonenViewModel Personen { get; }
    public KategorienViewModel Kategorien { get; }

    public VerwaltungViewModel(PersonenViewModel personen, KategorienViewModel kategorien)
    {
        Personen = personen;
        Kategorien = kategorien;
    }

    public string VorlagenPlatzhalterText =>
        "Vorlagen - hier entsteht spaeter die Pflege wiederkehrender Buchungen.";
}
