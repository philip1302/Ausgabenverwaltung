using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Knoten der Kategorie-Auswahl in der Filterleiste von Report und
/// Ausgabenliste. Bewusst nicht <see cref="KategorieKnoten"/>
/// wiederverwendet: der traegt den Bearbeitungszustand der
/// Verwaltungsansicht (Umbenennen, Neuanlage), den es hier nicht gibt.
///
/// Jeder Knoten traegt ein Haekchen. Ein angehakter Knoten meint immer
/// seinen ganzen Ast; ein abgewaehltes Kind darunter nimmt seinen
/// Unter-Ast wieder heraus. Uebersetzt wird das von
/// <see cref="Ausgabenverwaltung.Core.Categories.CategoryFilterSelection"/>
/// in die zwei Listen, die die Abfrage kennt - hier steht bewusst keine
/// Ableitungslogik (Regel 7).
///
/// Das Haekchen kaskadiert in beide Richtungen nach unten: Anhaken haakt
/// den ganzen Unterbaum an, Abwaehlen waehlt ihn ab. Das ist nicht nur
/// bequem, sondern noetig, damit die Anzeige nicht luegt - ein angehaktes
/// Enkelkind unter einem abgewaehlten Kind waere in der Abfrage trotzdem
/// draussen (der Ausschluss sticht), im Baum saehe es aber drin aus.
/// </summary>
public sealed class KategorieFilterKnoten : ObservableObject
{
    private bool _istGewaehlt;

    public KategorieFilterKnoten(
        int id, string name, string fullPath, bool isArchived, int? parentId)
    {
        Id = id;
        Name = name;
        FullPath = fullPath;
        IsArchived = isArchived;
        ParentId = parentId;
    }

    public int Id { get; }
    public string Name { get; }

    /// <summary>Voller Pfad ab der Wurzel, fuer die Anzeige des gewaehlten Filters.</summary>
    public string FullPath { get; }

    public bool IsArchived { get; }

    /// <summary>NULL bei einer Oberkategorie.</summary>
    public int? ParentId { get; }

    public List<KategorieFilterKnoten> Children { get; } = new();

    /// <summary>
    /// Wird vom ViewModel an JEDEN Knoten des Baums gehaengt und meldet
    /// eine vom Anwender ausgeloeste Aenderung - genau einmal je Klick,
    /// nicht einmal je mitgezogenem Kind (siehe <see cref="Setze"/>).
    /// </summary>
    internal Action? BeiAenderung { get; set; }

    public bool IstGewaehlt
    {
        get => _istGewaehlt;
        set => Setze(value, melden: true);
    }

    /// <summary>
    /// Setzt das Haekchen und zieht den Unterbaum mit. Nur der Knoten, den
    /// der Anwender tatsaechlich angeklickt hat, meldet die Aenderung
    /// weiter - sonst loeste ein Klick auf eine Oberkategorie mit zwanzig
    /// Unterkategorien einundzwanzig Neuladungen der Liste aus.
    /// </summary>
    private void Setze(bool wert, bool melden)
    {
        if (_istGewaehlt != wert)
        {
            _istGewaehlt = wert;
            OnPropertyChanged(nameof(IstGewaehlt));

            foreach (var kind in Children)
            {
                kind.Setze(wert, melden: false);
            }
        }

        if (melden)
        {
            BeiAenderung?.Invoke();
        }
    }

    /// <summary>
    /// Setzt das Haekchen ohne jede Meldung - fuer das Zuruecksetzen der
    /// Filterleiste, das die Liste ohnehin selbst neu laedt.
    /// </summary>
    internal void SetzeStill(bool wert) => Setze(wert, melden: false);
}
