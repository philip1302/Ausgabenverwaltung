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
/// Macht aus dem Beschreibungstext einer Veroeffentlichung
/// (<see cref="ReleaseInfo.Body"/>) etwas Anzeigbares.
///
/// Der Text kommt als Markdown herein, und zwar aus
/// <c>gh release create --generate-notes</c> (siehe PUBLISH.md). Er sieht
/// deshalb immer aehnlich aus:
///
/// <code>
/// ## What's Changed
/// * Betragsfeld rechnet, Datumsfeld versteht Kurzformen by @philip1302 in https://…
///
/// **Full Changelog**: https://github.com/…/compare/v1.1.0...v1.2.0
/// </code>
///
/// Bewusst KEIN Markdown-Darsteller: gezeigt werden Ueberschrift,
/// Aufzaehlungspunkt und Absatz, mehr nicht. Ein vollstaendiger Darsteller
/// waere eine Abhaengigkeit und eine Angriffsflaeche fuer einen Text, der
/// von aussen kommt - und er wuerde genau das anzeigen, was hier stoert:
/// Anmeldenamen und Adressen hinter jeder Zeile.
///
/// Reine Textverarbeitung, deshalb vollstaendig pruefbar (Regel 7).
/// Wirft nicht: unbrauchbarer Text ergibt eine leere Liste, und dann
/// unterbleibt die Seite (siehe <see cref="WasIstNeu"/>).
/// </summary>
public static partial class ReleaseNotes
{
    /// <summary>
    /// Mehr Abschnitte zeigt die Seite nicht. Eine Veroeffentlichung mit
    /// hunderten Zeilen gibt es hier nicht; die Grenze schuetzt die
    /// Anzeige vor einem Text, der aus welchem Grund auch immer ausufert.
    /// </summary>
    public const int Hoechstzahl = 200;

    /// <summary>
    /// Der Anhang, den GitHub an jede erzeugte Zeile haengt:
    /// " by @philip1302 in https://github.com/…". Fuer den Anwender sagt
    /// er nichts - in einem Vorhaben mit genau einem Verfasser schon gar
    /// nicht.
    /// </summary>
    [GeneratedRegex(@"\s+by\s+@[\w.\-]+(\s+in\s+\S+)?\s*$")]
    private static partial Regex GitHubAnhang();

    /// <summary>Markdown-Verweis "[Text](Adresse)" - der Text bleibt.</summary>
    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Verweis();

    /// <summary>Eine nackte Adresse mitten im Text.</summary>
    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Adresse();

    /// <summary>Aufzaehlungszeichen am Zeilenanfang, auch "1." und "2)".</summary>
    [GeneratedRegex(@"^([-*+]|\d+[.)])\s+")]
    private static partial Regex Aufzaehlung();

    private static readonly char[] Zeilenende = ['\n'];

    /// <summary>
    /// Zerlegt den Beschreibungstext in Abschnitte. Leerer oder
    /// unbrauchbarer Text ergibt eine leere Liste.
    /// </summary>
    public static IReadOnlyList<ReleaseNoteBlock> Lies(string? koerper)
    {
        if (string.IsNullOrWhiteSpace(koerper))
        {
            return [];
        }

        var abschnitte = new List<ReleaseNoteBlock>();

        // Fliesstext kann ueber mehrere Zeilen laufen und wird erst an der
        // naechsten Leerzeile (oder Ueberschrift oder Aufzaehlung)
        // abgeschlossen.
        var absatz = new StringBuilder();

        void SchliesseAbsatz()
        {
            Nimm(abschnitte, ReleaseNoteArt.Absatz, absatz.ToString());
            absatz.Clear();
        }

        foreach (var rohzeile in koerper.ReplaceLineEndings("\n").Split(Zeilenende))
        {
            var zeile = rohzeile.Trim();

            if (zeile.Length == 0 || IstTrennlinie(zeile) || zeile.StartsWith("```", StringComparison.Ordinal))
            {
                SchliesseAbsatz();
                continue;
            }

            if (zeile.StartsWith('#'))
            {
                SchliesseAbsatz();
                Nimm(abschnitte, ReleaseNoteArt.Ueberschrift, zeile.TrimStart('#').Trim());
                continue;
            }

            var aufzaehlung = Aufzaehlung().Match(zeile);
            if (aufzaehlung.Success)
            {
                SchliesseAbsatz();
                Nimm(abschnitte, ReleaseNoteArt.Punkt, zeile[aufzaehlung.Length..]);
                continue;
            }

            if (absatz.Length > 0)
            {
                absatz.Append(' ');
            }

            absatz.Append(zeile);
        }

        SchliesseAbsatz();

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

        var text = Bereinige(roh, out var enthieltAdresse);

        if (text.Length == 0)
        {
            return;
        }

        // "**Full Changelog**: https://…" bleibt nach dem Entfernen der
        // Adresse als blosse Beschriftung uebrig. Eine Zeile, die nur noch
        // ankuendigt, was nicht mehr dasteht, ist schlechter als keine.
        if (enthieltAdresse && text.EndsWith(':'))
        {
            return;
        }

        abschnitte.Add(new ReleaseNoteBlock { Art = art, Text = text });
    }

    /// <summary>
    /// Nimmt einer Zeile die Auszeichnungen und den GitHub-Anhang.
    /// </summary>
    private static string Bereinige(string roh, out bool enthieltAdresse)
    {
        var text = roh.Trim();

        text = Verweis().Replace(text, "$1");
        text = GitHubAnhang().Replace(text, string.Empty);

        enthieltAdresse = Adresse().IsMatch(text);
        text = Adresse().Replace(text, string.Empty);

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
