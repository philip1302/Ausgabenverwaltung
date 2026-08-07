using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile auf der Seite "Was ist neu". Uebersetzt die Art des
/// Abschnitts (Core.Updates.ReleaseNoteArt) in die zwei Merkmale, an
/// denen die Ansicht ihre Klassen umschaltet - eine Ansicht soll nicht
/// gegen einen Aufzaehlungstyp vergleichen muessen.
/// </summary>
public sealed class NeuerungZeile
{
    public NeuerungZeile(ReleaseNoteBlock abschnitt)
    {
        Text = abschnitt.Text;
        IstUeberschrift = abschnitt.Art == ReleaseNoteArt.Ueberschrift;
        IstPunkt = abschnitt.Art == ReleaseNoteArt.Punkt;
    }

    public string Text { get; }

    public bool IstUeberschrift { get; }

    /// <summary>Traegt das Aufzaehlungszeichen vor dem Text.</summary>
    public bool IstPunkt { get; }
}
