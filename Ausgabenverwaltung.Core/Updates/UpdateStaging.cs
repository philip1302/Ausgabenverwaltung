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
    /// Endung der noch unvollstaendig geladenen Datei. Waehrend des Ladens
    /// darf nichts dastehen, das wie eine fertige Vorbereitung aussieht.
    /// </summary>
    public const string TeilEndung = ".neu.teil";

    /// <summary>Endung des Ordners, in den ein tar.gz entpackt wird.</summary>
    public const string AuspackEndung = ".neu.auspacken";

    /// <summary>
    /// ALLE Endungen, die neben der Programmdatei entstehen koennen.
    ///
    /// Sie stehen hier und nicht verteilt in den Aufrufstellen, weil genau
    /// das der Fehler war: <c>.teil</c> und <c>.auspacken</c> kannte nur
    /// der Lauf, der sie angelegt hat. Bricht die Anwendung waehrend des
    /// Ladens ab, blieben sie fuer immer liegen, denn beim Start sah
    /// niemand nach ihnen.
    ///
    /// Diese Liste ist die Zusicherung fuer die Tests ("nach einem
    /// gelungenen Austausch liegt nichts davon mehr da"), NICHT die Liste,
    /// gegen die beim Start geraeumt wird - siehe
    /// <see cref="RestEndungen"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> AlleEndungen =
        [NeuEndung, ZettelEndung, TeilEndung, AuspackEndung, AltEndung];

    /// <summary>
    /// Die Endungen, die beim Start bedenkenlos weggeraeumt werden duerfen:
    /// sie gehoeren alle zu einem Vorgang, der sicher nicht mehr laeuft.
    ///
    /// <b><c>.neu</c> und <c>.neu.json</c> sind ausdruecklich NICHT dabei.</b>
    /// Sie koennen eine gueltige, wartende Vorbereitung sein - genau die,
    /// die beim Start uebernommen werden soll. Wer beim Aufraeumen die
    /// ganze Liste nimmt, loescht die geladene Fassung, bevor
    /// <see cref="UpdateInstaller.TryUebernehmen"/> sie einspielen kann,
    /// und die Aktualisierung findet nie statt. Um diese beiden kuemmert
    /// sich der Austausch selbst.
    /// </summary>
    public static readonly IReadOnlyList<string> RestEndungen =
        [AltEndung, TeilEndung, AuspackEndung];

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

    public static string TeilPfad(string zielPfad) => zielPfad + TeilEndung;

    public static string AuspackPfad(string zielPfad) => zielPfad + AuspackEndung;

    /// <summary>
    /// Nimmt eine Datei oder einen Ordner aus dem Blick des Anwenders.
    ///
    /// Der Grund ist keine Kosmetik: neben der Programmdatei liegen
    /// waehrend einer Aktualisierung bis zu drei Eintraege mit demselben
    /// Namensanfang, und keiner erklaert sich. Wer in den Ordner sieht,
    /// weiss nicht, welcher davon das Programm ist und ob er die anderen
    /// loeschen darf. Versteckt sind sie kein Aergernis mehr - aufgeraeumt
    /// gehoeren sie trotzdem (siehe <see cref="RaeumeAlleReste"/>), das
    /// Verstecken ist nur das Netz fuer den Fall, dass das Raeumen einmal
    /// nicht durchgeht.
    ///
    /// Wirft nie. Unter Linux und macOS kennt das Dateisystem dieses
    /// Merkmal nicht in dieser Form - dort bleibt es wirkungslos, und der
    /// Ordner wird ueber das Raeumen sauber.
    /// </summary>
    public static void Verstecke(string pfad)
    {
        try
        {
            if (File.Exists(pfad) || Directory.Exists(pfad))
            {
                File.SetAttributes(pfad, File.GetAttributes(pfad) | FileAttributes.Hidden);
            }
        }
        catch (Exception)
        {
            // Auch das Merkmal auf einer LAUFENDEN Programmdatei zu setzen
            // kann scheitern - sie ist noch abgebildet. Das darf den
            // Austausch nicht aufhalten: sichtbar zu bleiben ist ein
            // Schoenheitsfehler, ein abgebrochener Austausch ein Schaden.
        }
    }

    /// <summary>
    /// Nimmt das Versteckt-Merkmal wieder weg - fuer die Datei, die nach
    /// dem Austausch die neue Programmdatei ist.
    /// </summary>
    public static void Zeige(string pfad)
    {
        try
        {
            if (File.Exists(pfad) || Directory.Exists(pfad))
            {
                File.SetAttributes(pfad, File.GetAttributes(pfad) & ~FileAttributes.Hidden);
            }
        }
        catch (Exception)
        {
            // Bliebe die Programmdatei versteckt, waere das schlimmer als
            // ein Rest zu viel - deshalb steht dieser Aufruf im
            // Austausch an letzter Stelle und wird zusaetzlich bei jedem
            // Start wiederholt (siehe UpdateInstaller).
        }
    }

    /// <summary>
    /// Raeumt die Reste weg, die niemand mehr braucht
    /// (<see cref="RestEndungen"/>). Gehoert an den Anfang jedes Starts:
    /// nur dort ist sicher, dass kein Vorgang mehr laeuft, der eine davon
    /// noch benutzt.
    /// </summary>
    public static void RaeumeReste(string zielPfad)
    {
        foreach (var endung in RestEndungen)
        {
            LoescheHartnaeckig(zielPfad + endung);
        }
    }

    /// <summary>
    /// Loeschen mit Wiederholung.
    ///
    /// Ein einziger Versuch genuegt hier nicht, und das war der eigentliche
    /// Fehler: die beiseitegelegte alte Programmdatei wird geraeumt,
    /// unmittelbar nachdem der Vorgaenger endete - und ein gerade beendeter
    /// Prozess gibt sein Abbild nicht im selben Augenblick frei. Der eine
    /// Versuch traf genau in diese fluechtige Sperre, schlug fehl, wurde
    /// verschluckt, und die Datei blieb sichtbar liegen. Mit dem
    /// Selbstneustart (der Vorgaenger endet Millisekunden vorher) waere das
    /// noch wahrscheinlicher geworden.
    ///
    /// Die Pausen sind kurz und die Anzahl klein: das hier laeuft im
    /// Programmstart, und ein Rest zu viel wiegt weniger als ein Start, der
    /// eine Sekunde spaeter kommt.
    /// </summary>
    public static void LoescheHartnaeckig(string pfad)
    {
        for (var versuch = 0; versuch < 5; versuch++)
        {
            LoescheStill(pfad);

            if (!File.Exists(pfad) && !Directory.Exists(pfad))
            {
                return;
            }

            Thread.Sleep(60);
        }
    }

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
