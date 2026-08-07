using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Updates;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die Seite "Was ist neu" - sie erscheint einmalig beim ersten Start
/// nach einem Austausch der Programmdatei und zeigt die Abschnitte der
/// Aenderungsliste (CHANGELOG.md), die seither dazugekommen sind.
///
/// Entschieden wird nichts hier, sondern in Core.Updates.WasIstNeu
/// (Regel 7); formuliert wird in Core.Errors.UpdateText. Dieses
/// ViewModel fragt nur einmal beim Erzeugen nach und traegt das Ergebnis
/// an die Ansicht.
///
/// Die gesehene Fassung wird SOFORT gemerkt und nicht erst beim
/// Wegklicken: die Seite soll auch dann nicht ein zweites Mal aufgehen,
/// wenn die Anwendung dazwischen abstuerzt oder ueber den Fensterknopf
/// beendet wird. Der Preis waere, sie zu sehen, ohne sie gelesen zu
/// haben - nachlesbar bleibt der Text in der Aenderungsliste des
/// Vorhabens.
/// </summary>
public sealed partial class WasIstNeuViewModel : ViewModelBase
{
    /// <summary>
    /// Wird ausgeloest, wenn der Anwender weiterklickt. Die Seite kennt
    /// die Navigation nicht - dasselbe Muster wie
    /// StartseiteViewModel.ErfassenAngefordert.
    /// </summary>
    public event EventHandler? Geschlossen;

    /// <param name="laufendeVersion">
    /// Die Version der laufenden Baugruppe. Sie wird hereingereicht und
    /// nicht selbst gelesen - dieselbe Ueberlegung wie beim Stichtag des
    /// Erzeugungslaufs: nur so laesst sich der Ablauf pruefen, ohne die
    /// Baugruppe selbst neu zu bauen (verdrahtet in App.axaml.cs).
    /// </param>
    public WasIstNeuViewModel(AppSettingsStore einstellungen, string? laufendeVersion)
    {
        var gespeichert = einstellungen.Load();

        var entscheidung = WasIstNeu.Treffe(
            laufendeVersion,
            gespeichert.LastSeenVersion,
            Changelog.Eingebettet());

        Sichtbar = entscheidung.Zeigen;
        Titel = $"Was ist neu in Fassung {entscheidung.VersionText}";
        Einleitung = UpdateText.WasIstNeuEinleitung(entscheidung.VersionText);
        Abschnitte = entscheidung.Abschnitte.Select(a => new NeuerungZeile(a)).ToList();

        if (entscheidung.Zeigen)
        {
            AppLog.Current.Info(LogEvents.UpdateNeuerungenGezeigt(
                entscheidung.VersionText, Abschnitte.Count));
        }
        else if (entscheidung.MerkeVersion is not null
                 && gespeichert.LastSeenVersion is not null)
        {
            // Die Fassung ist gewechselt, es gab aber nichts zu zeigen -
            // zu ihr steht kein Abschnitt in der Aenderungsliste. Kein
            // Fall fuer den Anwender, wohl aber einer, den man im
            // Protokoll wiederfinden will (CLAUDE.md, Regel 15).
            AppLog.Current.Info(
                LogEvents.UpdateNeuerungenUebersprungen(entscheidung.VersionText));
        }

        Merke(einstellungen, entscheidung);
    }

    /// <summary>Ob die Seite beim Start ueberhaupt angezeigt wird.</summary>
    public bool Sichtbar { get; }

    public string Titel { get; }

    public string Einleitung { get; }

    public IReadOnlyList<NeuerungZeile> Abschnitte { get; }

    /// <summary>
    /// Haelt fest, dass diese Fassung gesehen wurde.
    ///
    /// Ausdruecklich still: dass sich eine Einstellung nicht schreiben
    /// laesst, ist kein Grund, den Start mit einem Dialog zu unterbrechen.
    /// Der Preis ist, dass die Seite beim naechsten Mal erneut erscheint.
    /// </summary>
    private static void Merke(AppSettingsStore einstellungen, WasIstNeuEntscheidung entscheidung)
    {
        if (entscheidung.MerkeVersion is null)
        {
            return;
        }

        try
        {
            einstellungen.Save(einstellungen.Load() with
            {
                LastSeenVersion = entscheidung.MerkeVersion,
            });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Merken der gesehenen Fassung", ex);
        }
    }

    [RelayCommand]
    private void Weiter() => Geschlossen?.Invoke(this, EventArgs.Empty);
}
