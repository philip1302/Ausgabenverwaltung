using System.Collections.Generic;
using System.Linq;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>
/// Wozu ein Tastenkuerzel gehoert. Der Wert ist der Schluessel, ueber den
/// sich eine Bindungsstelle ihre Geste holt - Zeichenketten wie "Ctrl+N"
/// stehen deshalb an genau einer Stelle, naemlich in
/// <see cref="Tastenkuerzel.Alle"/>.
/// </summary>
public enum TastenkuerzelAktion
{
    NeueBuchung,
    Suche,
    BereichWechseln,
    Uebersicht,
    Speichern,
    Schliessen,
}

/// <summary>
/// Ein Tastenkuerzel, so wie es wirkt UND so wie es in der Uebersicht
/// steht.
///
/// Beides steht bewusst in EINEM Eintrag: eine Uebersichtsseite, die ihre
/// Zeilen von Hand pflegt, laeuft der Wirklichkeit nach dem zweiten
/// hinzugefuegten Kuerzel hinterher, und niemand merkt es, weil eine
/// falsche Hilfeseite nichts kaputtmacht - sie schickt nur jeden in die
/// Irre, der ihr glaubt.
///
/// <see cref="Gesten"/> sind Avalonia-Gesten ("Ctrl+N"), <see cref="Taste"/>
/// ist der deutsche Anzeigetext ("Strg + N"). Ein Eintrag kann mehrere
/// Gesten tragen: Speichern hoert auf Strg+S und Strg+Enter, der
/// Bereichswechsel auf Strg+1 bis Strg+9.
/// </summary>
/// <param name="Aktion">Schluessel fuer die Bindungsstellen.</param>
/// <param name="Gruppe">Ueberschrift in der Uebersicht.</param>
/// <param name="Taste">Anzeigetext, deutsch.</param>
/// <param name="Beschreibung">Was passiert - ein ganzer Satz.</param>
/// <param name="Gesten">Die Gesten, auf die tatsaechlich gehoert wird.</param>
public sealed record Tastenkuerzel(
    TastenkuerzelAktion Aktion,
    string Gruppe,
    string Taste,
    string Beschreibung,
    IReadOnlyList<string> Gesten)
{
    /// <summary>Kuerzel, die ueberall gelten - verdrahtet im Hauptfenster.</summary>
    public const string GruppeUeberall = "Überall";

    /// <summary>
    /// Kuerzel, die nur dort wirken, wo gerade ein Formular offen ist -
    /// verdrahtet an den betroffenen Ansichten.
    /// </summary>
    public const string GruppeFormulare = "In Formularen und Dialogen";

    /// <summary>
    /// Wie viele Bereiche sich ueber Strg+Ziffer erreichen lassen. Neun,
    /// weil Strg+0 auf keiner Tastatur neben der Neun liegt und ein
    /// zehnter Eintrag deshalb schlechter zu treffen waere als der Weg
    /// ueber die Maus.
    /// </summary>
    public const int BereicheMitZiffer = 9;

    /// <summary>
    /// Alle Kuerzel der Anwendung. Diese Liste ist die Quelle: das
    /// Hauptfenster und die einzelnen Ansichten bauen ihre Bindungen
    /// daraus, und die Uebersichtsseite (F1) zeigt genau sie.
    /// </summary>
    public static IReadOnlyList<Tastenkuerzel> Alle { get; } = new[]
    {
        new Tastenkuerzel(
            TastenkuerzelAktion.NeueBuchung,
            GruppeUeberall,
            "Strg + N",
            "Öffnet die Erfassungsmaske, der Zeiger steht im Betragsfeld.",
            new[] { "Ctrl+N" }),

        new Tastenkuerzel(
            TastenkuerzelAktion.Suche,
            GruppeUeberall,
            "Strg + F",
            "Springt in die Ausgabenliste und in ihr Suchfeld.",
            new[] { "Ctrl+F" }),

        new Tastenkuerzel(
            TastenkuerzelAktion.BereichWechseln,
            GruppeUeberall,
            "Strg + 1 … 9",
            "Wechselt in einen der ersten neun Bereiche der Seitenleiste, "
            + "von oben nach unten gezählt.",
            Enumerable.Range(1, BereicheMitZiffer)
                      .Select(ziffer => $"Ctrl+D{ziffer}")
                      .ToArray()),

        new Tastenkuerzel(
            TastenkuerzelAktion.Uebersicht,
            GruppeUeberall,
            "F1",
            "Zeigt diese Übersicht.",
            new[] { "F1" }),

        new Tastenkuerzel(
            TastenkuerzelAktion.Speichern,
            GruppeFormulare,
            "Strg + S  oder  Strg + Eingabe",
            "Speichert das offene Formular — dasselbe wie ein Druck auf "
            + "„Speichern“.",
            new[] { "Ctrl+S", "Ctrl+Enter" }),

        new Tastenkuerzel(
            TastenkuerzelAktion.Schliessen,
            GruppeFormulare,
            "Esc",
            "Schließt das offene Formular, ohne zu speichern.",
            new[] { "Escape" }),
    };

    /// <summary>
    /// Der Eintrag zu einer Aktion. Wirft, wenn es ihn nicht gibt - ein
    /// fehlender Eintrag ist ein Fehler im Programm und keine Lage, auf
    /// die sich eine Bindungsstelle sinnvoll einstellen koennte.
    /// </summary>
    public static Tastenkuerzel Fuer(TastenkuerzelAktion aktion) =>
        Alle.Single(kuerzel => kuerzel.Aktion == aktion);

    /// <summary>
    /// Die einzige Geste einer Aktion. Fuer die Stellen, die genau eine
    /// erwarten - wer mehrere binden will, liest <see cref="Gesten"/>.
    /// </summary>
    public static string Geste(TastenkuerzelAktion aktion) =>
        Fuer(aktion).Gesten.Single();

    /// <summary>
    /// Die Kuerzel nach Gruppe, in der Reihenfolge von
    /// <see cref="Alle"/> - genau so steht die Uebersicht auf der Seite.
    /// </summary>
    public static IReadOnlyList<IGrouping<string, Tastenkuerzel>> NachGruppe() =>
        Alle.GroupBy(kuerzel => kuerzel.Gruppe).ToList();
}
