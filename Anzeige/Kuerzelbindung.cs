using System;
using System.Windows.Input;
using Avalonia.Controls;
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
    /// denselben Befehl mit einem anderen Parameter ausloest.
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
            Command = new Weitergereicht(ziel, kommandoName),
        };

        // Nur setzen, wenn es einen gibt: die Eigenschaft ist nicht als
        // "darf null sein" ausgewiesen, und die meisten Kuerzel brauchen
        // keinen Parameter.
        if (parameter is not null)
        {
            bindung.CommandParameter = parameter;
        }

        ziel.KeyBindings.Add(bindung);
    }

    /// <summary>
    /// Reicht den Tastendruck an das gleichnamige Kommando des
    /// DataContext weiter - und zwar erst im Moment des Drucks.
    ///
    /// Der Umweg ist noetig, weil die Ansichten ihre Kuerzel im Konstruktor
    /// anhaengen und der DataContext da noch nicht steht. Ein zugewiesenes
    /// Kommando waere fuer immer leer, und eine KeyBinding ohne Kommando
    /// tut einfach nichts - ohne Fehlermeldung, ohne Spur im Protokoll.
    ///
    /// Bewusst kein <c>Binding</c> auf "DataContext.…": das laeuft ueber
    /// die Dispatcher-Maschinerie von Avalonia und liesse sich ausserhalb
    /// eines laufenden Fensters nicht mehr pruefen.
    /// </summary>
    private sealed class Weitergereicht : ICommand
    {
        private readonly InputElement _ziel;
        private readonly string _kommandoName;

        public Weitergereicht(InputElement ziel, string kommandoName)
        {
            _ziel = ziel;
            _kommandoName = kommandoName;
        }

        // Die Tastenbindung fragt bei jedem Druck neu nach; ein Ereignis
        // haette also nichts zu melden, worauf sich jemand verlassen
        // muesste.
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            Kommando() is { } kommando && kommando.CanExecute(parameter);

        public void Execute(object? parameter) => Kommando()?.Execute(parameter);

        private ICommand? Kommando()
        {
            var datenkontext = (_ziel as Control)?.DataContext;

            return datenkontext?
                .GetType()
                .GetProperty(_kommandoName)?
                .GetValue(datenkontext) as ICommand;
        }
    }
}
