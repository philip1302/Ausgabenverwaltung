using System.Text.Json;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Liest die Antwort der GitHub-Schnittstelle
/// (<c>GET /repos/{eigner}/{name}/releases</c>) in
/// <see cref="ReleaseInfo"/>-Werte um.
///
/// Bewusst die LISTE und nicht <c>/releases/latest</c>: GitHub fuehrt
/// neben "Entwurf" und "Vorabversion" ein drittes, eigenes Merkmal
/// dafuer, welche Veroeffentlichung als die neueste gilt. Es wird beim
/// Anlegen gesetzt und zieht nicht automatisch nach, wenn eine
/// Vorabversion spaeter zur regulaeren erklaert wird - dieser Fall ist im
/// Projekt bereits eingetreten und lieferte tagelang die vorletzte
/// Fassung als "neueste". Aus der vollen Liste die hoechste Version zu
/// nehmen, haengt an keinem solchen Merkmal.
///
/// Reine Textverarbeitung ohne Netzzugriff, deshalb vollstaendig pruefbar
/// (Regel 7) - wer laedt, steht in <see cref="UpdateDownload"/>.
/// </summary>
public static class GitHubReleases
{
    /// <summary>
    /// Der Endpunkt fuer die Veroeffentlichungen dieses Vorhabens.
    /// </summary>
    public static readonly Uri ListenAdresse =
        new("https://api.github.com/repos/philip1302/Ausgabenverwaltung/releases");

    /// <summary>
    /// Zerlegt die Antwort. Entwuerfe und Vorabversionen fallen dabei
    /// heraus, ebenso alles, dessen Marke sich nicht als Version lesen
    /// laesst. Das Ergebnis ist absteigend sortiert, die hoechste Version
    /// steht also vorn.
    ///
    /// Wirft nicht: unlesbares JSON ergibt eine leere Liste. Eine
    /// veraenderte oder gestoerte Antwort soll nichts ausloesen, schon gar
    /// keinen Fehlerdialog wegen einer Nebensaechlichkeit wie der Frage,
    /// ob es etwas Neueres gibt.
    /// </summary>
    public static IReadOnlyList<ReleaseInfo> Lies(string json)
    {
        try
        {
            using var dokument = JsonDocument.Parse(json);

            if (dokument.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var ergebnis = new List<ReleaseInfo>();

            foreach (var eintrag in dokument.RootElement.EnumerateArray())
            {
                var release = LiesEintrag(eintrag);
                if (release is not null)
                {
                    ergebnis.Add(release);
                }
            }

            return ergebnis
                .OrderByDescending(release => release.Version)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ReleaseInfo? LiesEintrag(JsonElement eintrag)
    {
        if (eintrag.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Ein Entwurf ist noch nicht veroeffentlicht, eine Vorabversion
        // ist bewusst nicht fuer alle gedacht - beides kommt fuer eine
        // automatische Aktualisierung nicht in Frage.
        if (Flagge(eintrag, "draft") || Flagge(eintrag, "prerelease"))
        {
            return null;
        }

        var tagName = Text(eintrag, "tag_name");
        if (tagName is null || !AppVersion.TryParse(tagName, out var version))
        {
            return null;
        }

        return new ReleaseInfo
        {
            TagName = tagName,
            Version = version,
            HtmlUrl = Text(eintrag, "html_url") ?? string.Empty,

            // Der Beschreibungstext wird hier nur mitgenommen, nicht
            // ausgewertet - aufbereitet wird er erst dort, wo er
            // tatsaechlich gezeigt wird (siehe ReleaseNotes).
            Body = Text(eintrag, "body"),
            Assets = LiesAssets(eintrag),
        };
    }

    private static IReadOnlyList<ReleaseAsset> LiesAssets(JsonElement eintrag)
    {
        if (!eintrag.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ergebnis = new List<ReleaseAsset>();

        foreach (var asset in assets.EnumerateArray())
        {
            var name = Text(asset, "name");
            var url = Text(asset, "browser_download_url");

            if (name is null || url is null)
            {
                continue;
            }

            ergebnis.Add(new ReleaseAsset
            {
                Name = name,
                DownloadUrl = url,
                SizeBytes = Zahl(asset, "size"),
                Sha256 = LiesDigest(asset),
            });
        }

        return ergebnis;
    }

    /// <summary>
    /// Das Feld "digest" kommt in der Form "sha256:abc123..." herein. Ein
    /// anderes Verfahren als SHA256 wird nicht angenommen, sondern als
    /// "keine Pruefsumme" behandelt - lieber keine Aktualisierung als
    /// eine, deren Pruefung stillschweigend uebergangen wurde.
    /// </summary>
    private static string? LiesDigest(JsonElement asset)
    {
        var digest = Text(asset, "digest");

        const string vorzeichen = "sha256:";
        if (digest is null || !digest.StartsWith(vorzeichen, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hex = digest[vorzeichen.Length..].Trim();

        // 32 Byte als Hexziffern.
        return hex.Length == 64 ? hex.ToLowerInvariant() : null;
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString()
            : null;

    private static long Zahl(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert)
           && wert.ValueKind == JsonValueKind.Number
           && wert.TryGetInt64(out var zahl)
            ? zahl
            : 0;

    private static bool Flagge(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.True;
}
