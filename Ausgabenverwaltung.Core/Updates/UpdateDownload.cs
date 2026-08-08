using System.Formats.Tar;
using System.IO.Compression;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Holt die neue Fassung und legt sie neben der laufenden bereit -
/// uebernommen wird sie erst beim naechsten Start
/// (<see cref="UpdateInstaller"/>).
///
/// Der Netzzugriff kommt als Delegat herein und nicht als eigener Typ.
/// Das Vorhaben kennt keine Schnittstellen fuer solche Zwecke; testbar
/// wird hier ueberall dadurch, dass das Aeussere hereingereicht wird -
/// Pfade bei <c>StartupService</c>, ein Stichtag bei
/// <c>GenerateDueOccurrences</c>, und hier eben das Laden selbst. So
/// laesst sich der Ablauf ohne Netz pruefen, ohne dafuer eine Abstraktion
/// zu erfinden, die es sonst nirgends gibt.
/// </summary>
public static class UpdateDownload
{
    /// <summary>Laedt eine Adresse als Text (die Releases-Liste).</summary>
    public delegate Task<string> HoleText(Uri adresse, CancellationToken abbruch);

    /// <summary>Laedt eine Adresse in eine Datei (das Asset).</summary>
    public delegate Task HoleDatei(Uri adresse, string zielDatei, CancellationToken abbruch);

    /// <summary>
    /// Fragt nach, ob es etwas Neueres gibt. Kein Herunterladen, keine
    /// Nebenwirkung auf der Platte.
    ///
    /// Wirft nicht: ein nicht erreichbares GitHub, eine Zeitueberschreitung
    /// oder eine veraenderte Antwort ergeben
    /// <see cref="UpdateGrund.NichtsGefunden"/>. Dass es gerade nicht
    /// nachzusehen geht, ist nichts, was jemanden aufhalten sollte.
    /// </summary>
    public static async Task<UpdateDecision> PruefeAsync(
        string? aktuelleVersion,
        string? assetName,
        HoleText holeText,
        CancellationToken abbruch = default)
    {
        if (assetName is null)
        {
            return UpdateDecision.Ohne(UpdateGrund.PlattformOhneVeroeffentlichung);
        }

        try
        {
            var json = await holeText(GitHubReleases.ListenAdresse, abbruch)
                .ConfigureAwait(false);

            var releases = GitHubReleases.Lies(json);

            return UpdateEntscheidung.Treffe(aktuelleVersion, releases, assetName);
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Nachsehen nach einer neuen Fassung", ex);
            return UpdateDecision.Ohne(UpdateGrund.NichtsGefunden);
        }
    }

    /// <summary>
    /// Laedt das Asset, prueft es und legt es als vorbereitete Fassung
    /// bereit. Liefert <c>true</c>, wenn danach etwas bereitliegt, das
    /// beim naechsten Start uebernommen werden kann.
    ///
    /// Wirft nicht - jeder Fehlschlag raeumt die halbe Vorbereitung weg
    /// und wird protokolliert.
    /// </summary>
    public static async Task<bool> LadeUndLegeBereitAsync(
        UpdateDecision entscheidung,
        string zielPfad,
        HoleDatei holeDatei,
        CancellationToken abbruch = default)
    {
        if (!entscheidung.IstVerfuegbar
            || entscheidung.Asset is not { } asset
            || entscheidung.Release is not { } release)
        {
            return false;
        }

        var neu = UpdateStaging.NeuPfad(zielPfad);

        // Zuerst unter einem anderen Namen laden: waehrend des Ladens
        // darf nichts dastehen, das wie eine fertige Vorbereitung
        // aussieht. Erst nach bestandener Pruefung wird umbenannt.
        // Die Endung kommt aus UpdateStaging, damit sie beim Aufraeumen
        // mitgezaehlt wird - vorher stand sie nur hier und wurde beim
        // Start nie geraeumt.
        var teil = UpdateStaging.TeilPfad(zielPfad);

        try
        {
            UpdateStaging.Verwirf(zielPfad);
            UpdateStaging.LoescheStill(teil);

            await holeDatei(new Uri(asset.DownloadUrl), teil, abbruch).ConfigureAwait(false);

            var zettel = new UpdateStaging.Zettel
            {
                Version = release.TagName,
                Sha256 = asset.Sha256!,
                SizeBytes = asset.SizeBytes,
                AssetName = asset.Name,
            };

            // Erste Pruefung: stimmt das GELADENE ARCHIV mit dem ueberein,
            // was GitHub dazu angibt? Das ist die Stelle, an der ein
            // beschaedigter oder unterwegs veraenderter Download
            // auffaellt.
            if (!UpdateStaging.PasstZumZettel(teil, zettel))
            {
                AppLog.Current.Warning(LogEvents.UpdatePruefsummeAbweichend(release.TagName));
                UpdateStaging.LoescheStill(teil);
                return false;
            }

            // Das Geladene ist ein Archiv (ZIP unter Windows, TAR.GZ
            // unter macOS) - ausgetauscht wird aber die Programmdatei
            // bzw. das Bundle darin.
            Entpacke(teil, neu, asset.Name);
            UpdateStaging.LoescheStill(teil);

            // Ab hier beschreibt der Zettel das ENTPACKTE, nicht mehr das
            // Archiv: nur so laesst sich unmittelbar vor dem Austausch
            // noch einmal pruefen, was dann tatsaechlich ausgefuehrt wird.
            var pruefDatei = UpdateStaging.PruefDatei(neu);
            if (pruefDatei is null)
            {
                AppLog.Current.Warning(LogEvents.UpdateVerworfen(release.TagName));
                UpdateStaging.Verwirf(zielPfad);
                return false;
            }

            var entpacktZettel = zettel with
            {
                SizeBytes = new FileInfo(pruefDatei).Length,
                Sha256 = UpdateStaging.BerechnePruefsumme(pruefDatei),
            };

            // Der Zettel wird als LETZTES geschrieben: bis dahin gilt die
            // Vorbereitung als unvollstaendig, und ein Abbruch mittendrin
            // hinterlaesst nichts, das beim naechsten Start uebernommen
            // wuerde.
            UpdateStaging.SchreibeZettel(zielPfad, entpacktZettel);

            // Beide aus dem Blick nehmen. Sie liegen jetzt womoeglich
            // stunden- oder tagelang neben der Programmdatei - so lange,
            // bis der Anwender neu startet -, und in dieser Zeit hat
            // niemand einen Grund, sie zu sehen.
            UpdateStaging.Verstecke(neu);
            UpdateStaging.Verstecke(UpdateStaging.ZettelPfad(zielPfad));

            AppLog.Current.Info(
                LogEvents.UpdateBereitgelegt(release.TagName, asset.SizeBytes));

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Bereitlegen einer Aktualisierung", ex);
            UpdateStaging.LoescheStill(teil);
            UpdateStaging.Verwirf(zielPfad);

            return false;
        }
    }

    /// <summary>
    /// Holt aus dem Archiv das, was ausgetauscht wird: unter Windows die
    /// eine Programmdatei, unter macOS das ganze .app-Bundle.
    /// </summary>
    private static void Entpacke(string archiv, string ziel, string assetName)
    {
        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            EntpackeZip(archiv, ziel);
            return;
        }

        EntpackeTarGz(archiv, ziel);
    }

    private static void EntpackeZip(string archiv, string zielDatei)
    {
        using var zip = ZipFile.OpenRead(archiv);

        // Im ZIP liegt genau eine Programmdatei (siehe publish.ps1).
        var eintrag =
            zip.Entries.FirstOrDefault(
                e => e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Im geladenen Archiv steckt keine Programmdatei.");

        eintrag.ExtractToFile(zielDatei, overwrite: true);
    }

    private static void EntpackeTarGz(string archiv, string zielOrdner)
    {
        // Der Zielordner ist der ".neu"-Pfad; die Auspack-Endung haengt
        // deshalb daran und ist in UpdateStaging.AlleEndungen enthalten.
        var auspackOrdner = zielOrdner + ".auspacken";
        UpdateStaging.LoescheStill(auspackOrdner);
        Directory.CreateDirectory(auspackOrdner);

        try
        {
            using (var datei = File.OpenRead(archiv))
            using (var gzip = new GZipStream(datei, CompressionMode.Decompress))
            {
                // Das Ausfuehrungsrecht steht im TAR und wird hier
                // uebernommen - deshalb ueberhaupt TAR und nicht ZIP
                // (siehe PUBLISH.md).
                TarFile.ExtractToDirectory(gzip, auspackOrdner, overwriteFiles: true);
            }

            var bundle = Directory
                .EnumerateDirectories(auspackOrdner, "*.app", SearchOption.TopDirectoryOnly)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Im geladenen Archiv steckt kein Programmbuendel.");

            UpdateStaging.LoescheStill(zielOrdner);
            Directory.Move(bundle, zielOrdner);
        }
        finally
        {
            UpdateStaging.LoescheStill(auspackOrdner);
        }
    }
}
