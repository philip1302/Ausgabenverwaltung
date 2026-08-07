using System.Reflection;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>Ein Abschnitt der Aenderungsliste - eine Fassung.</summary>
public sealed record ChangelogEintrag
{
    public required Version Version { get; init; }

    /// <summary>Die Ueberschrift, wie sie dasteht: "1.4.0 — 07.08.2026".</summary>
    public required string Titel { get; init; }

    /// <summary>Der aufbereitete Inhalt (siehe <see cref="ReleaseNotes"/>).</summary>
    public required IReadOnlyList<ReleaseNoteBlock> Inhalt { get; init; }
}

/// <summary>
/// Liest die Aenderungsliste (CHANGELOG.md im Wurzelverzeichnis), aus der
/// die Seite "Was ist neu" gespeist wird.
///
/// Die Datei wird in die Baugruppe EINGEBETTET (siehe csproj) und nicht
/// zur Laufzeit von irgendwoher geladen. Das ist der Kern der Sache: die
/// Anwendung bringt ihren eigenen Text mit, braucht dafuer weder
/// Netzverbindung noch eine Datei neben der Programmdatei, und der Text
/// gehoert unweigerlich zu genau der Fassung, die gerade laeuft. Ein
/// nachtraeglich geaenderter Text auf GitHub kann hier nichts mehr
/// verschieben.
///
/// Aufbau der Datei: alles vor der ersten Ueberschrift zweiter Ordnung
/// ist Vorwort und wird uebergangen. Danach beginnt mit jedem
/// <c>## &lt;Version&gt; — &lt;Datum&gt;</c> ein neuer Eintrag.
///
/// Reine Textverarbeitung, deshalb vollstaendig pruefbar (Regel 7).
/// Wirft nicht: eine fehlende oder unbrauchbare Datei ergibt eine leere
/// Liste, und dann unterbleibt die Seite (siehe <see cref="WasIstNeu"/>).
/// </summary>
public static class Changelog
{
    /// <summary>Der Name, unter dem die Datei eingebettet ist.</summary>
    public const string Dateiname = "CHANGELOG.md";

    private static readonly char[] Zeilenende = ['\n'];

    /// <summary>
    /// Die eingebettete Aenderungsliste. <c>null</c>, wenn sie fehlt -
    /// das ist kein Fehlerfall, der jemanden aufhalten sollte, sondern
    /// bedeutet schlicht "es gibt nichts zu zeigen".
    /// </summary>
    public static string? Eingebettet()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(Dateiname, StringComparison.Ordinal));

            if (name is null)
            {
                return null;
            }

            using var strom = assembly.GetManifestResourceStream(name);
            if (strom is null)
            {
                return null;
            }

            using var leser = new StreamReader(strom);
            return leser.ReadToEnd();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Zerlegt die Aenderungsliste in ihre Fassungen, die hoechste zuerst.
    /// Ein Abschnitt ohne lesbare Version oder ohne Inhalt faellt weg.
    /// </summary>
    public static IReadOnlyList<ChangelogEintrag> Lies(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var eintraege = new List<ChangelogEintrag>();

        // Der laufende Abschnitt: Ueberschrift und die Zeilen darunter,
        // bis die naechste Ueberschrift zweiter Ordnung beginnt.
        Version? version = null;
        var titel = string.Empty;
        var zeilen = new List<string>();

        void Schliesse()
        {
            if (version is null)
            {
                return;
            }

            var inhalt = ReleaseNotes.Lies(string.Join('\n', zeilen));

            // Eine Fassung ohne Inhalt wird nicht gezeigt - eine
            // Ueberschrift allein sagt dem Anwender nichts.
            if (inhalt.Count > 0)
            {
                eintraege.Add(new ChangelogEintrag
                {
                    Version = version,
                    Titel = titel,
                    Inhalt = inhalt,
                });
            }

            version = null;
            titel = string.Empty;
            zeilen.Clear();
        }

        foreach (var rohzeile in text.ReplaceLineEndings("\n").Split(Zeilenende))
        {
            var zeile = rohzeile.TrimEnd();

            // Genau zwei Rautezeichen beginnen eine Fassung. Die eine
            // Raute ist der Titel der Datei, drei und mehr gliedern den
            // Inhalt eines Abschnitts.
            if (zeile.StartsWith("## ", StringComparison.Ordinal))
            {
                Schliesse();

                var kopf = zeile[3..].Trim();

                // Die Version steht vorn, dahinter darf stehen, was mag -
                // ueblicherweise das Datum.
                var erstesWort = kopf.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();

                if (AppVersion.TryParse(erstesWort, out var gelesen))
                {
                    version = gelesen;
                    titel = kopf;
                }

                continue;
            }

            // Alles vor der ersten Fassungsueberschrift ist Vorwort.
            if (version is not null)
            {
                zeilen.Add(zeile);
            }
        }

        Schliesse();

        return eintraege
            .OrderByDescending(eintrag => eintrag.Version)
            .ToList();
    }
}
