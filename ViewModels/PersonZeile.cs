using System.Globalization;
using Ausgabenverwaltung.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Personenliste: Daten einer Person mit Anzeige- und
/// Bearbeitungszustand fuer die Liste - bewusst getrennt von
/// Ausgabenverwaltung.Core.Entities.Person, damit die Core-Entitaet
/// keinen UI-Zustand tragen muss (Regel 7).
/// </summary>
public sealed partial class PersonZeile : ObservableObject
{
    /// <summary>NULL, solange die Zeile neu angelegt und noch nicht
    /// bestaetigt (also noch nicht in der DB gespeichert) ist.</summary>
    public int? Id { get; private set; }

    public bool IsSelf { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KannArchiviertWerden))]
    private bool _isArchived;

    [ObservableProperty]
    private int _ausgabenAnzahl;

    public string OffenePostenSummeText { get; }

    [ObservableProperty]
    private bool _wirdBearbeitet;

    [ObservableProperty]
    private string _bearbeitungsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BearbeitungsFehlerSichtbar))]
    private string? _bearbeitungsFehler;

    public bool BearbeitungsFehlerSichtbar => !string.IsNullOrEmpty(BearbeitungsFehler);

    public bool IstNeuUndUnbestaetigt => Id is null;

    /// <summary>Die IsSelf-Person kann nie archiviert werden (siehe
    /// Aufgabenstellung), ansonsten nur, solange sie es nicht schon ist.</summary>
    public bool KannArchiviertWerden => !IsSelf && !IsArchived;

    public PersonZeile(int? id, string name, bool isSelf, bool isArchived, int ausgabenAnzahl, long offenePostenSummeCents)
    {
        Id = id;
        IsSelf = isSelf;
        _name = name;
        _isArchived = isArchived;
        _ausgabenAnzahl = ausgabenAnzahl;

        // Bei der eigenen Person wird "SettledDate IS NULL" nie als offen
        // ausgewertet (Regel 4) - deshalb hier bewusst kein Betrag statt
        // einer irrefuehrenden 0,00-Anzeige.
        OffenePostenSummeText = isSelf
            ? "–"
            : Money.ToDecimal(offenePostenSummeCents).ToString("N2", CultureInfo.GetCultureInfo("de-DE"));
    }

    public void UebernehmeErstellteId(int id) => Id = id;
}
