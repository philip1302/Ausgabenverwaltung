using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Categories;
using Avalonia.Media;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Ein Eintrag der Farbauswahl in der Kategorieverwaltung: entweder eine
/// Farbe der Palette oder "keine Farbe" (<see cref="Hex"/> = NULL), bei
/// der die Kategorie wieder erbt.
///
/// Jeder Eintrag traegt seinen Namen. Eine Auswahl, die nur aus farbigen
/// Flaechen besteht, waere ohne Farbwahrnehmung nicht bedienbar.
/// </summary>
public sealed class FarbOption
{
    private FarbOption(string? hex, string bezeichnung)
    {
        Hex = hex;
        Bezeichnung = bezeichnung;
        Pinsel = Farbpinsel.Fuer(hex);
    }

    /// <summary>Der gespeicherte Wert, NULL = keine eigene Farbe.</summary>
    public string? Hex { get; }

    public string Bezeichnung { get; }

    public IBrush Pinsel { get; }

    public bool IstKeineFarbe => Hex is null;

    public static FarbOption Aus(CategoryColor farbe) => new(farbe.Hex, farbe.Name);

    public static FarbOption Keine() => new(null, "Keine eigene Farbe (erbt)");
}
