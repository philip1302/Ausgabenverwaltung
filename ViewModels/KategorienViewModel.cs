using System.Collections.Generic;
using System.Collections.ObjectModel;
using Ausgabenverwaltung.Core.Categories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Verwaltung -> Kategorien": Baumansicht mit Anlegen,
/// Umbenennen, Archivieren/Wiederherstellen und Verschieben. Die
/// eigentliche Fachlogik (Duplikat-Pruefung, Kaskade, Sortierung) steckt
/// komplett in <see cref="CategoryRepository"/> (Regel 7) - hier wird nur
/// der DB-Baum in UI-Knoten (<see cref="KategorieKnoten"/>) uebersetzt
/// und auf Anwenderaktionen reagiert.
/// </summary>
public sealed partial class KategorienViewModel : ViewModelBase
{
    private readonly CategoryRepository _categoryRepository;

    public ObservableCollection<KategorieKnoten> Wurzelknoten { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KnotenBestaetigtAusgewaehlt))]
    [NotifyPropertyChangedFor(nameof(AusgewaehlterKnotenIstArchiviert))]
    [NotifyCanExecuteChangedFor(nameof(NeueUnterkategorieCommand))]
    private KategorieKnoten? _ausgewaehlterKnoten;

    [ObservableProperty]
    private bool _archivierteAnzeigen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageAktiv))]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageText))]
    private KategorieKnoten? _archivierungAnfrageKnoten;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArchivierungAnfrageText))]
    private int _archivierungAnfrageAnzahlUnterkategorien;

    /// <summary>Ob ein bereits gespeicherter (nicht gerade erst per
    /// "Neue ..." angelegter, noch unbestaetigter) Knoten ausgewaehlt
    /// ist - Voraussetzung fuer Umbenennen/Archivieren/Verschieben.</summary>
    public bool KnotenBestaetigtAusgewaehlt => AusgewaehlterKnoten is { IstNeuUndUnbestaetigt: false };

    public bool AusgewaehlterKnotenIstArchiviert => AusgewaehlterKnoten?.IsArchived ?? false;

    public bool ArchivierungAnfrageAktiv => ArchivierungAnfrageKnoten is not null;

    public string ArchivierungAnfrageText => ArchivierungAnfrageKnoten is null
        ? string.Empty
        : $"\"{ArchivierungAnfrageKnoten.Name}\" hat {ArchivierungAnfrageAnzahlUnterkategorien} " +
          (ArchivierungAnfrageAnzahlUnterkategorien == 1 ? "Unterkategorie" : "Unterkategorien") +
          ". Sollen diese mit archiviert werden?";

    public KategorienViewModel(CategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
        LadeBaum();
    }

    partial void OnArchivierteAnzeigenChanged(bool value) => LadeBaum();

    [RelayCommand]
    private void NeueOberkategorie()
    {
        var knoten = new KategorieKnoten(id: null, parentId: null, name: string.Empty, isArchived: false, ausgabenAnzahl: 0);
        Wurzelknoten.Add(knoten);
        AusgewaehlterKnoten = knoten;
        StarteBearbeitung(knoten);
    }

    [RelayCommand(CanExecute = nameof(KnotenBestaetigtAusgewaehlt))]
    private void NeueUnterkategorie()
    {
        var eltern = AusgewaehlterKnoten;
        if (eltern is null)
        {
            return;
        }

        var knoten = new KategorieKnoten(id: null, parentId: eltern.Id, name: string.Empty, isArchived: false, ausgabenAnzahl: 0)
        {
            Eltern = eltern,
        };

        eltern.IsExpanded = true;
        eltern.Children.Add(knoten);
        AusgewaehlterKnoten = knoten;
        StarteBearbeitung(knoten);
    }

    [RelayCommand]
    private void BearbeitenStarten(KategorieKnoten? knoten)
    {
        if (knoten is null || knoten.WirdBearbeitet)
        {
            return;
        }

        StarteBearbeitung(knoten);
    }

    [RelayCommand]
    private void BearbeitenUebernehmen(KategorieKnoten? knoten)
    {
        if (knoten is null || !knoten.WirdBearbeitet)
        {
            return;
        }

        var name = knoten.BearbeitungsText.Trim();
        if (name.Length == 0)
        {
            knoten.BearbeitungsFehler = "Bitte einen Namen eingeben.";
            return;
        }

        try
        {
            if (knoten.IstNeuUndUnbestaetigt)
            {
                var erstellt = _categoryRepository.Create(name, knoten.ParentId);
                knoten.UebernehmeErstellteId(erstellt.Id);
            }
            else
            {
                _categoryRepository.Rename(knoten.Id!.Value, name);
            }
        }
        catch (DuplicateCategoryNameException)
        {
            knoten.BearbeitungsFehler = $"Es gibt hier bereits eine Kategorie namens \"{name}\".";
            return;
        }

        knoten.Name = name;
        knoten.WirdBearbeitet = false;
        knoten.BearbeitungsFehler = null;
        LadeBaum();
    }

    [RelayCommand]
    private void BearbeitenAbbrechen(KategorieKnoten? knoten)
    {
        if (knoten is null || !knoten.WirdBearbeitet)
        {
            return;
        }

        if (knoten.IstNeuUndUnbestaetigt)
        {
            EntferneKnoten(knoten);
        }
        else
        {
            knoten.WirdBearbeitet = false;
            knoten.BearbeitungsFehler = null;
        }
    }

    [RelayCommand]
    private void Archivieren(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        var unterkategorien = _categoryRepository.GetDescendantIds(id);
        if (unterkategorien.Count > 0)
        {
            ArchivierungAnfrageKnoten = knoten;
            ArchivierungAnfrageAnzahlUnterkategorien = unterkategorien.Count;
            return;
        }

        _categoryRepository.Archive(id, includeDescendants: false);
        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungNurDieseBestaetigen()
    {
        if (ArchivierungAnfrageKnoten?.Id is not int id)
        {
            return;
        }

        _categoryRepository.Archive(id, includeDescendants: false);
        ArchivierungAnfrageKnoten = null;
        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungMitUnterkategorienBestaetigen()
    {
        if (ArchivierungAnfrageKnoten?.Id is not int id)
        {
            return;
        }

        _categoryRepository.Archive(id, includeDescendants: true);
        ArchivierungAnfrageKnoten = null;
        LadeBaum();
    }

    [RelayCommand]
    private void ArchivierungAbbrechen()
    {
        ArchivierungAnfrageKnoten = null;
    }

    [RelayCommand]
    private void Wiederherstellen(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        _categoryRepository.Restore(id);
        LadeBaum();
    }

    [RelayCommand]
    private void NachObenVerschieben(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        _categoryRepository.MoveUp(id);
        LadeBaum();
    }

    [RelayCommand]
    private void NachUntenVerschieben(KategorieKnoten? knoten)
    {
        if (knoten?.Id is not int id)
        {
            return;
        }

        _categoryRepository.MoveDown(id);
        LadeBaum();
    }

    private static void StarteBearbeitung(KategorieKnoten knoten)
    {
        knoten.BearbeitungsText = knoten.Name;
        knoten.BearbeitungsFehler = null;
        knoten.WirdBearbeitet = true;
    }

    private void EntferneKnoten(KategorieKnoten knoten)
    {
        var geschwister = knoten.Eltern?.Children ?? Wurzelknoten;
        geschwister.Remove(knoten);

        if (ReferenceEquals(AusgewaehlterKnoten, knoten))
        {
            AusgewaehlterKnoten = knoten.Eltern;
        }
    }

    private void LadeBaum()
    {
        var ausgewaehlteId = AusgewaehlterKnoten?.Id;

        var baum = _categoryRepository.GetTree();
        var anzahlen = _categoryRepository.GetExpenseCounts();

        Wurzelknoten.Clear();
        foreach (var knoten in BaueKnoten(baum, anzahlen, eltern: null))
        {
            Wurzelknoten.Add(knoten);
        }

        AusgewaehlterKnoten = ausgewaehlteId is int id ? FindeKnoten(Wurzelknoten, id) : null;
    }

    private List<KategorieKnoten> BaueKnoten(
        IReadOnlyList<CategoryNode> nodes, IReadOnlyDictionary<int, int> anzahlen, KategorieKnoten? eltern)
    {
        var ergebnis = new List<KategorieKnoten>();

        foreach (var node in nodes)
        {
            var anzahl = anzahlen.TryGetValue(node.Category.Id, out var count) ? count : 0;
            var knoten = new KategorieKnoten(
                node.Category.Id, node.Category.ParentId, node.Category.Name, node.Category.IsArchived, anzahl)
            {
                Eltern = eltern,
            };

            var kinder = BaueKnoten(node.Children, anzahlen, knoten);

            // Ein komplett archivierter Ast (Knoten selbst archiviert und
            // keine sichtbaren Kinder uebrig) wird ausgeblendet, solange
            // der Schalter "Archivierte anzeigen" aus ist.
            if (node.Category.IsArchived && !ArchivierteAnzeigen && kinder.Count == 0)
            {
                continue;
            }

            foreach (var kind in kinder)
            {
                knoten.Children.Add(kind);
            }

            ergebnis.Add(knoten);
        }

        return ergebnis;
    }

    private static KategorieKnoten? FindeKnoten(IEnumerable<KategorieKnoten> knoten, int id)
    {
        foreach (var kandidat in knoten)
        {
            if (kandidat.Id == id)
            {
                return kandidat;
            }

            var gefunden = FindeKnoten(kandidat.Children, id);
            if (gefunden is not null)
            {
                return gefunden;
            }
        }

        return null;
    }
}
