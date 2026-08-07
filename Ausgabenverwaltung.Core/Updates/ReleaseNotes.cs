using System.Text;
using System.Text.RegularExpressions;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Was ein Abschnitt der Neuerungen ist. Mehr Formen gibt es bewusst
/// nicht: die Seite "Was ist neu" soll eine Liste von Aenderungen zeigen
/// und keine Markdown-Anzeige sein.
/// </summary>
public enum ReleaseNoteArt
{
    /// <summary>Eine Zwischenueberschrift ("## Was ist neu").</summary>
    Ueberschrift,

    /// <summary>Ein Aufzaehlungspunkt - der Regelfall.</summary>
    Punkt,

    /// <summary>Fliesstext zwischen den Aufzaehlungen.</summary>
    Absatz,
}

/// <summary>Ein Abschnitt der aufbereiteten Neuerungen.</summary>
public sealed record ReleaseNoteBlock
{
    public required ReleaseNoteArt Art { get; init; }

    public required string Text { get; init; }
}

/// <summary>
/// Macht aus einem Abschnitt der Aenderungsliste
/// (<see cref="Changelog"/>, CHANGELOG.md) etwas Anzeigbares.
///
/// Bewusst KEIN Markdown-Darsteller: gezeigt werden Ueberschrift,
/// Aufzaehlungspunkt und Absatz, mehr nicht. Genau so viel Form braucht
/// eine Liste von Aenderungen, und ein vollstaendiger Darsteller waere
/// eine Abhaengigkeit fuer nichts.
///
/// Reine Textverarbeitung, deshalb vollstaendig pruefbar (Regel 7).
/// Wirft nicht: unbrauchbarer Text ergibt eine leere Liste, und dann
/// unterbleibt die Seite (siehe <see cref="WasIstNeu"/>).
/// </summary>
public static partial class ReleaseNotes
{
    /// <summary>
    /// Mehr Abschnitte zeigt die Seite nicht. Eine Fassung mit hunderten
    /// Zeilen gibt es hier nicht; die Grenze schuetzt die Anzeige vor
    /// einem Text, der aus welchem Grund auch immer ausufert.
    /// </summary>
    public const int Hoechstzahl = 200;

    /// <summary>Markdown-Verweis "[Text](Adresse)" - der Text bleibt.</summary>
    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Verweis();

    /// <summary>Aufzaehlungszeichen am Zeilenanfang, auch "1." und "2)".</summary>
    [GeneratedRegex(@"^([-*+]|\d+[.)])\s+")]
    private static partial Regex Aufzaehlung();

    private static readonly char[] Zeilenende = ['\n'];

    /// <summary>
    /// Zerlegt den Text in Abschnitte. Leerer oder unbrauchbarer Text
    /// ergibt eine leere Liste.
    /// </summary>
    public static IReadOnlyList<ReleaseNoteBlock> Lies(string? koerper)
    {
        if (string.IsNullOrWhiteSpace(koerper))
        {
            return [];
        }

        var abschnitte = new List<ReleaseNoteBlock>();

        // Der gerade offene Abschnitt. Ein Aufzaehlungspunkt wie ein
        // Absatz darf sich ueber mehrere Zeilen ziehen - in einer Datei
        // mit kurzen Zeilen (Stilvorgabe) ist das der Normalfall und
        // nicht die Ausnahme. Geschlossen wird an der naechsten
        // Leerzeile, Ueberschrift oder Aufzaehlung.
        var offen = new StringBuilder();
        var offeneArt = ReleaseNoteArt.Absatz;

        void Schliesse()
        {
            Nimm(abschnitte, offeneArt, offen.ToString());
            offen.Clear();
            offeneArt = ReleaseNoteArt.Absatz;
        }

        foreach (var rohzeile in koerper.ReplaceLineEndings("\n").Split(Zeilenende))
        {
            var zeile = rohzeile.Trim();

            if (zeile.Length == 0 || IstTrennlinie(zeile) || zeile.StartsWith("```", StringComparison.Ordinal))
            {
                Schliesse();
                continue;
            }

            if (zeile.StartsWith('#'))
            {
                Schliesse();
                Nimm(abschnitte, ReleaseNoteArt.Ueberschrift, zeile.TrimStart('#').Trim());
                continue;
            }

            var aufzaehlung = Aufzaehlung().Match(zeile);
            if (aufzaehlung.Success)
            {
                Schliesse();
                offeneArt = ReleaseNoteArt.Punkt;
                offen.Append(zeile[aufzaehlung.Length..]);
                continue;
            }

            // Fortsetzung dessen, was offen ist - ob eingerueckt oder
            // nicht, spielt keine Rolle.
            if (offen.Length > 0)
            {
                offen.Append(' ');
            }

            offen.Append(zeile);
        }

        Schliesse();

        return abschnitte.Count > Hoechstzahl
            ? abschnitte.GetRange(0, Hoechstzahl)
            : abschnitte;
    }

    private static void Nimm(List<ReleaseNoteBlock> abschnitte, ReleaseNoteArt art, string roh)
    {
        if (abschnitte.Count >= Hoechstzahl)
        {
            return;
        }

        var text = Bereinige(roh);

        if (text.Length == 0)
        {
            return;
        }

        abschnitte.Add(new ReleaseNoteBlock { Art = art, Text = text });
    }

    /// <summary>
    /// Nimmt einer Zeile ihre Auszeichnungen. Aus einem Verweis bleibt
    /// sein Text: eine Adresse laesst sich in einer Anwendung ohnehin
    /// nicht anklicken, die davon nichts weiss.
    /// </summary>
    private static string Bereinige(string roh)
    {
        var text = roh.Trim();

        text = Verweis().Replace(text, "$1");

        // Nur die doppelten Zeichen fuer fett und kursiv fallen weg.
        // Einzelne Sterne und Unterstriche bleiben stehen: sie kommen in
        // Dateinamen und Bezeichnern vor, und ein Text, dem man Zeichen
        // herausschneidet, ist schlimmer als einer mit einem Sternchen zu
        // viel.
        text = text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // "---", "***" oder "___" als waagerechter Strich.
    private static bool IstTrennlinie(string zeile)
        => zeile.Length >= 3
           && (zeile.All(z => z == '-') || zeile.All(z => z == '*') || zeile.All(z => z == '_'));
}
