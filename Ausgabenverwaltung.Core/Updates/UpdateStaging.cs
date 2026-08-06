using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ausgabenverwaltung.Core.Updates;

/// <summary>
/// Die Ablage zwischen Herunterladen und Uebernehmen: wo die
/// vorbereitete Fassung liegt, woran man sie wiedererkennt und wie
/// geprueft wird, dass sie unterwegs nicht beschaedigt wurde.
///
/// Vorbereitet wird IMMER neben der laufenden Programmdatei und nicht im
/// Anwendungsdatenordner. Zwei Gruende: der Austausch ist dann ein
/// Umbenennen innerhalb desselben Datentraegers - unteilbar, ohne die
/// Zwischenstufe einer halb kopierten Datei; und ob der Programmordner
/// ueberhaupt beschreibbar ist, faellt so schon vor dem Herunterladen auf
/// und nicht erst danach.
/// </summary>
public static class UpdateStaging
{
    /// <summary>Endung der vorbereiteten Fassung.</summary>
    public const string NeuEndung = ".neu";

    /// <summary>Endung der beiseitegelegten alten Fassung.</summary>
    public const string AltEndung = ".alt";

    /// <summary>Endung des Begleitzettels.</summary>
    public const string ZettelEndung = ".neu.json";

    /// <summary>
    /// Der Begleitzettel neben der vorbereiteten Fassung. Er haelt fest,
    /// was da liegt - ohne ihn waere eine halb geschriebene Datei nicht
    /// von einer vollstaendigen zu unterscheiden.
    /// </summary>
    public sealed record Zettel
    {
        public required string Version { get; init; }

        public required string Sha256 { get; init; }

        public required long SizeBytes { get; init; }

        /// <summary>Nur fuer das Protokoll und zum Nachsehen von Hand.</summary>
        public string? AssetName { get; init; }
    }

    public static string NeuPfad(string zielPfad) => zielPfad + NeuEndung;

    public static string AltPfad(string zielPfad) => zielPfad + AltEndung;

    public static string ZettelPfad(string zielPfad) => zielPfad + ZettelEndung;

    /// <summary>
    /// Schreibt den Begleitzettel. Erst danach gilt die vorbereitete
    /// Fassung als vollstaendig - deshalb wird er als LETZTES geschrieben.
    /// </summary>
    public static void SchreibeZettel(string zielPfad, Zettel zettel)
    {
        var json = JsonSerializer.Serialize(zettel, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ZettelPfad(zielPfad), json);
    }

    /// <summary>
    /// Liest den Begleitzettel. <c>null</c>, wenn er fehlt oder unlesbar
    /// ist - dann gilt die vorbereitete Fassung als nicht vorhanden.
    /// </summary>
    public static Zettel? LiesZettel(string zielPfad)
    {
        try
        {
            var pfad = ZettelPfad(zielPfad);
            if (!File.Exists(pfad))
            {
                return null;
            }

            var zettel = JsonSerializer.Deserialize<Zettel>(File.ReadAllText(pfad));

            // Ein Zettel ohne Pruefsumme ist wertlos - die Pruefung vor
            // dem Austausch haengt genau daran.
            return string.IsNullOrWhiteSpace(zettel?.Sha256) ? null : zettel;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Raeumt vorbereitete Fassung und Begleitzettel weg. Wird nach einem
    /// gescheiterten Versuch gerufen, damit derselbe kaputte Stand nicht
    /// bei jedem Start erneut probiert wird.
    /// </summary>
    public static void Verwirf(string zielPfad)
    {
        LoescheStill(NeuPfad(zielPfad));
        LoescheStill(ZettelPfad(zielPfad));
    }

    /// <summary>
    /// Die SHA256-Pruefsumme einer Datei als Hexziffern in
    /// Kleinschreibung - dieselbe Form, in der GitHub sie meldet.
    /// </summary>
    public static string BerechnePruefsumme(string dateiPfad)
    {
        using var strom = File.OpenRead(dateiPfad);
        var hash = SHA256.HashData(strom);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Was von einer vorbereiteten Fassung tatsaechlich geprueft wird.
    ///
    /// Unter Windows ist das die Programmdatei selbst. Unter macOS ist
    /// die Fassung ein ganzes Verzeichnis (.app-Bundle) - ein Verzeichnis
    /// hat keine Pruefsumme, also wird die ausfuehrbare Datei darin
    /// geprueft. Sie ist ohnehin die einzige, auf die es ankommt: alles
    /// andere im Bundle ist Beiwerk, das nichts ausfuehrt.
    ///
    /// Gesucht wird ueber den Aufbau (Contents/MacOS) und nicht ueber den
    /// Namen - der Ordner heisst waehrend der Vorbereitung
    /// "Ausgabenverwaltung.app.neu" und spaeter "Ausgabenverwaltung.app",
    /// und aus beidem den Programmnamen zurueckzurechnen waere unnoetig
    /// zerbrechlich.
    /// </summary>
    public static string? PruefDatei(string pfad)
    {
        if (File.Exists(pfad))
        {
            return pfad;
        }

        if (!Directory.Exists(pfad))
        {
            return null;
        }

        var macOsOrdner = Path.Combine(pfad, "Contents", "MacOS");
        if (!Directory.Exists(macOsOrdner))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(macOsOrdner, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(datei => datei, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Stimmt die vorbereitete Fassung mit dem ueberein, was der Zettel
    /// behauptet? Geprueft wird beides: Groesse (billig) und Pruefsumme
    /// (genau).
    /// </summary>
    public static bool PasstZumZettel(string pfad, Zettel zettel)
    {
        try
        {
            var pruefDatei = PruefDatei(pfad);
            if (pruefDatei is null)
            {
                return false;
            }

            var datei = new FileInfo(pruefDatei);

            if (!datei.Exists || datei.Length != zettel.SizeBytes)
            {
                return false;
            }

            return string.Equals(
                BerechnePruefsumme(pruefDatei), zettel.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Laesst sich in diesem Ordner ueberhaupt schreiben? Liegt die
    /// Anwendung etwa unter "C:\Program Files", ist die Antwort nein und
    /// es gibt nichts zu automatisieren - das faellt hier auf, bevor
    /// Daten geladen werden, die anschliessend niemand ablegen kann.
    ///
    /// Geprueft wird durch einen echten Schreibversuch, nicht ueber
    /// Berechtigungen: die tatsaechliche Antwort haengt an Gruppen,
    /// Vererbung und Virtualisierung, und die einzige verlaessliche Probe
    /// ist der Versuch selbst.
    /// </summary>
    public static bool OrdnerBeschreibbar(string ordnerPfad)
    {
        var probe = Path.Combine(
            ordnerPfad,
            "schreibprobe-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp");

        try
        {
            using (var strom = File.Create(probe))
            {
                strom.WriteByte(0);
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            LoescheStill(probe);
            return false;
        }
    }

    internal static void LoescheStill(string pfad)
    {
        try
        {
            if (File.Exists(pfad))
            {
                File.Delete(pfad);
            }
            else if (Directory.Exists(pfad))
            {
                Directory.Delete(pfad, recursive: true);
            }
        }
        catch (Exception)
        {
            // Bleibt eben liegen und wird beim naechsten Start erneut
            // versucht. Ein nicht geraeumter Rest ist kein Grund,
            // irgendetwas abzubrechen.
        }
    }
}
