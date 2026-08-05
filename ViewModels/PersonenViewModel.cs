using System;
using System.Collections.ObjectModel;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung -> Personen": Liste mit Anlegen, Umbenennen,
/// Archivieren/Wiederherstellen und Verschieben in der Reihenfolge (Pfeil
/// nach oben/unten). Die eigentliche Fachlogik (Duplikat-Pruefung,
/// "hoechstens ein IsSelf", Vertauschen der SortOrder) steckt komplett in
/// <see cref="PersonRepository"/> bzw. den DB-Constraints (Regel 7) - hier
/// wird nur die DB-Liste in UI-Zeilen (<see cref="PersonZeile"/>) uebersetzt
/// und auf Anwenderaktionen reagiert.
/// </summary>
public sealed partial class PersonenViewModel : ViewModelBase
{
    private readonly PersonRepository _personRepository;
    private readonly OpenItemsRepository _openItemsRepository;

    public ObservableCollection<PersonZeile> Personen { get; } = new();

    [ObservableProperty]
    private bool _archivierteAnzeigen;

    /// <summary>
    /// Ein Schreibfehler beim Archivieren oder Wiederherstellen. Als Band
    /// ueber der Liste; die Liste selbst bleibt unveraendert stehen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    public PersonenViewModel(PersonRepository personRepository, OpenItemsRepository openItemsRepository)
    {
        _personRepository = personRepository;
        _openItemsRepository = openItemsRepository;
        LadeListe();
    }

    partial void OnArchivierteAnzeigenChanged(bool value) => LadeListe();

    [RelayCommand]
    private void NeuePerson()
    {
        var zeile = new PersonZeile(
            id: null, name: string.Empty, isSelf: false, isArchived: false,
            ausgabenAnzahl: 0, offenePostenSummeCents: 0);

        Personen.Add(zeile);
        StarteBearbeitung(zeile);
    }

    [RelayCommand]
    private void BearbeitenStarten(PersonZeile? zeile)
    {
        if (zeile is null || zeile.WirdBearbeitet)
        {
            return;
        }

        StarteBearbeitung(zeile);
    }

    [RelayCommand]
    private void BearbeitenUebernehmen(PersonZeile? zeile)
    {
        if (zeile is null || !zeile.WirdBearbeitet)
        {
            return;
        }

        var name = zeile.BearbeitungsText.Trim();
        if (name.Length == 0)
        {
            zeile.BearbeitungsFehler = "Bitte einen Namen eingeben.";
            return;
        }

        try
        {
            if (zeile.IstNeuUndUnbestaetigt)
            {
                var erstellt = _personRepository.Create(name);
                zeile.UebernehmeErstellteId(erstellt.Id);
            }
            else
            {
                _personRepository.Rename(zeile.Id!.Value, name);
            }
        }
        catch (DuplicatePersonNameException)
        {
            zeile.BearbeitungsFehler =
                $"Es gibt bereits eine Person namens „{name}“. "
                + "Bitte einen anderen Namen wählen — sonst wäre bei einer offenen "
                + "Forderung nicht zu erkennen, wer gemeint ist.";
            return;
        }
        catch (Exception ex)
        {
            // Schreibfehler: der Name bleibt im Bearbeitungsfeld stehen.
            zeile.BearbeitungsFehler = Schreibvorgang.Beschreibe(
                "Beim Speichern eines Personennamens", ex);
            return;
        }

        zeile.WirdBearbeitet = false;
        zeile.BearbeitungsFehler = null;
        LadeListe();
    }

    [RelayCommand]
    private void BearbeitenAbbrechen(PersonZeile? zeile)
    {
        if (zeile is null || !zeile.WirdBearbeitet)
        {
            return;
        }

        if (zeile.IstNeuUndUnbestaetigt)
        {
            Personen.Remove(zeile);
        }
        else
        {
            zeile.WirdBearbeitet = false;
            zeile.BearbeitungsFehler = null;
        }
    }

    [RelayCommand]
    private void Archivieren(PersonZeile? zeile)
    {
        if (zeile?.Id is not int id || !zeile.KannArchiviertWerden)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Archivieren einer Person",
            () => _personRepository.Archive(id));

        LadeListe();
    }

    [RelayCommand]
    private void Wiederherstellen(PersonZeile? zeile)
    {
        if (zeile?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Wiederherstellen einer Person",
            () => _personRepository.Restore(id));

        LadeListe();
    }

    [RelayCommand]
    private void NachObenVerschieben(PersonZeile? zeile)
    {
        if (zeile?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Verschieben einer Person nach oben",
            () => _personRepository.MoveUp(id));

        LadeListe();
    }

    [RelayCommand]
    private void NachUntenVerschieben(PersonZeile? zeile)
    {
        if (zeile?.Id is not int id)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Verschieben einer Person nach unten",
            () => _personRepository.MoveDown(id));

        LadeListe();
    }

    private static void StarteBearbeitung(PersonZeile zeile)
    {
        zeile.BearbeitungsText = zeile.Name;
        zeile.BearbeitungsFehler = null;
        zeile.WirdBearbeitet = true;
    }

    private void LadeListe()
    {
        var personen = _personRepository.GetAll();
        var anzahlen = _personRepository.GetExpenseCounts();
        var summen = _openItemsRepository.GetOpenSumsByPayer();

        Personen.Clear();
        foreach (var person in personen)
        {
            if (person.IsArchived && !ArchivierteAnzeigen)
            {
                continue;
            }

            var anzahl = anzahlen.TryGetValue(person.Id, out var count) ? count : 0;
            var summeCents = summen.TryGetValue(person.Id, out var sum) ? sum : 0;

            Personen.Add(new PersonZeile(
                person.Id, person.Name, person.IsSelf, person.IsArchived, anzahl, summeCents));
        }
    }
}
