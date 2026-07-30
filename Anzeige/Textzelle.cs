using System;
using System.Globalization;
using Ausgabenverwaltung.Core.Categories;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Ausgabenverwaltung.Anzeige;

/// <summary>Auf welcher Seite eine zu lange Zelle gekuerzt wird.</summary>
public enum Kuerzungsseite
{
    /// <summary>
    /// Hinten - der Normalfall fuer freien Text (Bemerkung, Titel, Name):
    /// dort steht das Wichtige am Anfang.
    /// </summary>
    Hinten,

    /// <summary>
    /// Vorne, entlang der Pfadstufen - fuer Kategoriepfade: dort steht das
    /// Wichtige am Ende (siehe <see cref="CategoryPathShortening"/>).
    /// </summary>
    Vorne,
}

/// <summary>
/// Die Textzelle einer Tabelle: immer einzeilig, immer genau so breit wie
/// die Spalte, mit Hilfetext nur dort, wo tatsaechlich etwas fehlt.
///
/// Sie loest drei Dinge, die ein blosser TextBlock nicht kann:
///
/// 1. Sie verlangt NIE mehr Breite, als die Spalte ihr anbietet. Ein
///    gewoehnlicher TextBlock meldet die Breite seines vollen Textes als
///    Wunsch an; in einem Raster mit Sternspalten - und erst recht, wenn
///    die Tabelle in einem waagerechten Bildlauf ohne Breitenbegrenzung
///    gemessen wird - schiebt ein langer Text dadurch alle folgenden
///    Spalten nach rechts, und die Zeilen stehen nicht mehr untereinander.
///
/// 2. Sie kuerzt Kategoriepfade VORNE statt hinten. TextTrimming kann das
///    nur zeichenweise; hier fallen ganze Pfadstufen weg, damit der Name
///    der Kategorie selbst vollstaendig stehen bleibt.
///
/// 3. Der Hilfetext erscheint nur, wenn wirklich gekuerzt wurde. Ein
///    Hilfetext ueber einem ohnehin vollstaendig sichtbaren Text ist
///    nichts als Rauschen.
///
/// Kein Umbruch und keine Hoehe aus dem Inhalt: alle Zeilen einer Tabelle
/// bleiben dadurch gleich hoch, und die Hoehe waechst weiterhin allein mit
/// der eingestellten Schriftgroesse.
/// </summary>
public sealed class Textzelle : TextBlock
{
    /// <summary>
    /// Der volle Text. Bewusst nicht <see cref="TextBlock.Text"/>: dort
    /// steht die gekuerzte Fassung, die diese Klasse selbst setzt.
    /// </summary>
    public static readonly StyledProperty<string?> InhaltProperty =
        AvaloniaProperty.Register<Textzelle, string?>(nameof(Inhalt));

    public static readonly StyledProperty<Kuerzungsseite> KuerzungProperty =
        AvaloniaProperty.Register<Textzelle, Kuerzungsseite>(nameof(Kuerzung));

    // Ein Pixel Luft beim Vergleichen: die Messung hier und der Textsatz
    // des TextBlock kommen auf minimal verschiedene Breiten. Ohne diesen
    // Abstand wuerde ein gerade eben passender Vorschlag am Ende doch noch
    // abgeschnitten - und ausgerechnet hinten.
    private const double Sicherheitsabstand = 1.0;

    // Wofuer der angezeigte Text zuletzt gerechnet wurde. Ohne dieses
    // Gedaechtnis liefe die Suche nach den passenden Pfadstufen bei jeder
    // Messung neu, also mehrmals je Zeile und Bildlaufschritt.
    private string? _gerechnetFuerInhalt;
    private double _gerechnetFuerBreite = double.NaN;
    private double _gerechnetFuerSchrift = double.NaN;

    static Textzelle()
    {
        AffectsMeasure<Textzelle>(InhaltProperty, KuerzungProperty);
    }

    public Textzelle()
    {
        TextWrapping = TextWrapping.NoWrap;
        TextTrimming = TextTrimming.CharacterEllipsis;
    }

    public string? Inhalt
    {
        get => GetValue(InhaltProperty);
        set => SetValue(InhaltProperty, value);
    }

    public Kuerzungsseite Kuerzung
    {
        get => GetValue(KuerzungProperty);
        set => SetValue(KuerzungProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        BestimmeAnzeigetext(availableSize.Width);

        var eigene = base.MeasureOverride(availableSize);

        // Der eigentliche Punkt: der gemeldete Platzbedarf bleibt in dem,
        // was die Spalte hergibt. Ohne Begrenzung (Auto-Spalte, waagerechter
        // Bildlauf) gilt weiterhin die natuerliche Breite - dort soll die
        // Zelle ja gerade den Platz bestimmen.
        var breite = double.IsInfinity(availableSize.Width)
            ? eigene.Width
            : Math.Min(eigene.Width, availableSize.Width);

        return new Size(breite, eigene.Height);
    }

    private void BestimmeAnzeigetext(double verfuegbar)
    {
        var inhalt = Inhalt;

        // Der Innenabstand geht vom Platz fuer den Text ab.
        var platz = double.IsInfinity(verfuegbar)
            ? double.PositiveInfinity
            : Math.Max(0, verfuegbar - Padding.Left - Padding.Right);

        if (Gleich(_gerechnetFuerInhalt, inhalt)
            && Gleich(_gerechnetFuerBreite, platz)
            && Gleich(_gerechnetFuerSchrift, FontSize))
        {
            return;
        }

        _gerechnetFuerInhalt = inhalt;
        _gerechnetFuerBreite = platz;
        _gerechnetFuerSchrift = FontSize;

        if (string.IsNullOrEmpty(inhalt))
        {
            Setze(inhalt, gekuerzt: false);
            return;
        }

        if (Passt(inhalt, platz))
        {
            Setze(inhalt, gekuerzt: false);
            return;
        }

        // Hinten kuerzt der TextBlock selbst - der volle Text darf stehen
        // bleiben, TextTrimming schneidet ihn beim Setzen ab. Vorne muss
        // der Text dagegen vorher zurechtgelegt werden.
        var anzeige = Kuerzung == Kuerzungsseite.Vorne
            ? CategoryPathShortening.Shorten(inhalt, vorschlag => Passt(vorschlag, platz))
            : inhalt;

        Setze(anzeige, gekuerzt: true);
    }

    private void Setze(string? anzeige, bool gekuerzt)
    {
        Text = anzeige;

        // Nur wo etwas fehlt, gibt es den vollen Text zum Nachsehen.
        ToolTip.SetTip(this, gekuerzt ? Inhalt : null);
    }

    private bool Passt(string text, double platz) =>
        double.IsInfinity(platz) || Gemessen(text) <= platz - Sicherheitsabstand;

    private double Gemessen(string text) =>
        new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground).Width;

    private static bool Gleich(string? links, string? rechts) =>
        string.Equals(links, rechts, StringComparison.Ordinal);

    // Auch fuer "beide unendlich" und "beide noch nie gerechnet" (NaN):
    // die Differenz waere in beiden Faellen NaN und der Vergleich falsch.
    private static bool Gleich(double links, double rechts) =>
        Math.Abs(links - rechts) < 0.5
        || (double.IsInfinity(links) && double.IsInfinity(rechts));
}
