using Avalonia.Data;
using Avalonia.Input;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Haengt ein Tastenkuerzel aus <see cref="Tastenkuerzel"/> an ein
/// Bedienelement.
///
/// Der Umweg ueber diese Klasse statt einer <c>KeyBinding</c> in der .axaml
/// hat genau einen Grund: die Geste steht dann nur EINMAL im Programm,
/// naemlich in <see cref="Tastenkuerzel.Alle"/>. Die Uebersichtsseite (F1)
/// zeigt dieselbe Liste und kann deshalb nicht von der Wirklichkeit
/// abweichen.
///
/// Reine Bedienmechanik (Regel 7 betrifft Fachlogik) - was beim Druck
/// passiert, steht im jeweiligen ViewModel.
/// </summary>
public static class Kuerzelbindung
{
    /// <summary>
    /// Bindet ALLE Gesten einer Aktion (Speichern hoert auf zwei) an ein
    /// Kommando des DataContext.
    /// </summary>
    public static void Binde(
        InputElement ziel,
        TastenkuerzelAktion aktion,
        string kommandoName,
        object? parameter = null)
    {
        foreach (var geste in Tastenkuerzel.Fuer(aktion).Gesten)
        {
            BindeGeste(ziel, geste, kommandoName, parameter);
        }
    }

    /// <summary>
    /// Bindet eine einzelne Geste - fuer Strg+1 bis Strg+9, wo jede Ziffer
    /// denselben Befehl mit einem anderen Parameter auslöst.
    /// </summary>
    public static void BindeGeste(
        InputElement ziel,
        string geste,
        string kommandoName,
        object? parameter = null)
    {
        var bindung = new KeyBinding
        {
            Gesture = KeyGesture.Parse(geste),
        };

        // Nur setzen, wenn es einen gibt: die Eigenschaft ist nicht als
        // "darf null sein" ausgewiesen, und die meisten Kuerzel brauchen
        // keinen Parameter.
        if (parameter is not null)
        {
            bindung.CommandParameter = parameter;
        }

        // Das Kommando wird GEBUNDEN und nicht zugewiesen: der DataContext
        // steht beim Erzeugen der Ansicht noch nicht, eine Bindung findet
        // ihn spaeter von selbst. Ueber "DataContext.…" mit dem Element als
        // Quelle, weil eine KeyBinding selbst keinen DataContext erbt.
        bindung.Bind(
            KeyBinding.CommandProperty,
            new Binding($"DataContext.{kommandoName}") { Source = ziel });

        ziel.KeyBindings.Add(bindung);
    }
}
