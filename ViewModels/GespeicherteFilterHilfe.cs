using System;
using System.Collections.Generic;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Das bisschen Umrechnung zwischen der Filterleiste und einem
/// gespeicherten Filter, das Ausgabenliste und Auswertung gemeinsam
/// brauchen: Haekchen einsammeln, Haekchen wiederherstellen, eine
/// Datumsgrenze lesen.
///
/// Bewusst KEINE Fachlogik (Regel 7) - die Regeln ueber der Liste
/// (Namenspruefung, Ersetzen, Sortierung) stehen in
/// <see cref="Ausgabenverwaltung.Core.Reports.SavedFilters"/> und sind
/// dort gepruefet. Hier steht nur, was ohne die Baumknoten der
/// Oberflaeche gar nicht formulierbar waere.
/// </summary>
internal static class GespeicherteFilterHilfe
{
    /// <summary>
    /// Die Ids aller angehakten Knoten - roh, wie angeklickt, und nicht
    /// als Aeste und Ausschluesse. Genau so lassen sich die Haekchen
    /// spaeter wieder hinlegen.
    /// </summary>
    public static List<int> Angehakt(IEnumerable<KategorieFilterKnoten> wurzeln)
    {
        var ids = new List<int>();

        void Sammle(IEnumerable<KategorieFilterKnoten> knoten)
        {
            foreach (var k in knoten)
            {
                if (k.IstGewaehlt)
                {
                    ids.Add(k.Id);
                }

                Sammle(k.Children);
            }
        }

        Sammle(wurzeln);

        return ids;
    }

    /// <summary>
    /// Setzt die Haekchen des Baums auf genau diese Ids.
    ///
    /// <see cref="KategorieFilterKnoten.SetzeStill"/> zieht den Unterbaum
    /// mit; weil hier JEDER Knoten von oben nach unten seinen eigenen
    /// Wert bekommt, wird das Mitgezogene gleich darauf ueberschrieben.
    /// Der Baum steht danach genau so, wie er beim Speichern stand - eine
    /// inzwischen geloeschte Kategorie faellt weg, eine neue bleibt leer.
    /// </summary>
    public static void SetzeHaken(
        IEnumerable<KategorieFilterKnoten> knoten, HashSet<int> gewaehlt)
    {
        foreach (var k in knoten)
        {
            k.SetzeStill(gewaehlt.Contains(k.Id));
            SetzeHaken(k.Children, gewaehlt);
        }
    }

    /// <summary>
    /// Eine Datumsgrenze aus dem Feld. Eine leere oder unleserliche
    /// Eingabe wird zur offenen Grenze - genau das bedeutet ein leeres
    /// Feld in der Leiste auch.
    /// </summary>
    public static DateOnly? LiesGrenze(string text)
        => GermanDateInput.TryParse(text, out var datum) ? datum : null;
}
