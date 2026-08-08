using System.Text.Json;
using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Die Ablage zwischen "Wiederherstellen" und dem tatsaechlichen Ersetzen:
/// wo die entpackte Sicherung liegt, woran man sie wiedererkennt und wie
/// geprueft wird, dass sie unterwegs nicht beschaedigt wurde.
///
/// Bereitgelegt wird IMMER neben der aktiven Datenbank. Zwei Gruende,
/// dieselben wie bei <see cref="UpdateStaging"/>: das Ersetzen ist dann ein
/// Umbenennen innerhalb desselben Datentraegers - unteilbar, ohne die
/// Zwischenstufe einer halb kopierten Datei; und ob der Ordner ueberhaupt
/// beschreibbar ist, faellt schon beim Bereitlegen auf und nicht erst,
/// wenn die alte Datei bereits zur Seite gelegt wurde.
/// </summary>
public static class RestoreStaging
{
    /// <summary>Endung der bereitgelegten Datenbank.</summary>
    public const string NeuEndung = ".wiederherstellung";

    /// <summary>Endung der beiseitegelegten bisherigen Datenbank.</summary>
    public const string AltEndung = ".vorher";

    /// <summary>Endung des Begleitzettels.</summary>
    public const string ZettelEndung = ".wiederherstellung.json";

    /// <summary>
    /// Endung der halb geschriebenen Datei beim Entpacken. Sie wird erst
    /// nach vollstaendigem Entpacken auf <see cref="NeuEndung"/> umbenannt,
    /// damit ein abgebrochenes Entpacken nie als bereitliegende Sicherung
    /// gilt.
    /// </summary>
    public const string TempEndung = ".wiederherstellung.tmp";

    /// <summary>
    /// Der Begleitzettel neben der bereitgelegten Datenbank. Ohne ihn waere
    /// eine halb geschriebene Datei nicht von einer vollstaendigen zu
    /// unterscheiden - und beim Ersetzen der Datenbank ist genau das der
    /// Unterschied zwischen einer Wiederherstellung und einem Datenverlust.
    /// </summary>
    public sealed record Zettel
    {
        /// <summary>Name der ZIP-Datei, aus der entpackt wurde.</summary>
        public required string Quelldatei { get; init; }

        /// <summary>
        /// Dateiname der Sicherheitskopie der bisherigen Datenbank. Sie ist
        /// der Rueckweg und wird in der Meldung nach dem Neustart genannt.
        /// </summary>
        public required string Sicherheitskopie { get; init; }

        public required string Sha256 { get; init; }

        public required long SizeBytes { get; init; }

        /// <summary>Nur fuer das Protokoll und zum Nachsehen von Hand.</summary>
        public int? SchemaVersion { get; init; }
    }

    public static string NeuPfad(string databaseFilePath) => databaseFilePath + NeuEndung;

    public static string AltPfad(string databaseFilePath) => databaseFilePath + AltEndung;

    public static string TempPfad(string databaseFilePath) => databaseFilePath + TempEndung;

    public static string ZettelPfad(string databaseFilePath) => databaseFilePath + ZettelEndung;

    /// <summary>
    /// Schreibt den Begleitzettel. Erst danach gilt die bereitgelegte
    /// Sicherung als vollstaendig - deshalb wird er als LETZTES geschrieben.
    /// </summary>
    public static void SchreibeZettel(string databaseFilePath, Zettel zettel)
    {
        var json = JsonSerializer.Serialize(
            zettel, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(ZettelPfad(databaseFilePath), json);
    }

    /// <summary>
    /// Liest den Begleitzettel. <c>null</c>, wenn er fehlt oder unlesbar
    /// ist - dann gilt die bereitgelegte Sicherung als nicht vorhanden.
    /// </summary>
    public static Zettel? LiesZettel(string databaseFilePath)
    {
        try
        {
            var pfad = ZettelPfad(databaseFilePath);
            if (!File.Exists(pfad))
            {
                return null;
            }

            var zettel = JsonSerializer.Deserialize<Zettel>(File.ReadAllText(pfad));

            // Ein Zettel ohne Pruefsumme ist wertlos - die Pruefung vor dem
            // Ersetzen haengt genau daran.
            return string.IsNullOrWhiteSpace(zettel?.Sha256) ? null : zettel;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Stimmt die bereitgelegte Datei mit dem ueberein, was der Zettel
    /// behauptet? Geprueft wird beides: Groesse (billig) und Pruefsumme
    /// (genau).
    /// </summary>
    public static bool PasstZumZettel(string databaseFilePath, Zettel zettel)
    {
        try
        {
            var datei = new FileInfo(NeuPfad(databaseFilePath));

            if (!datei.Exists || datei.Length != zettel.SizeBytes)
            {
                return false;
            }

            // Dieselbe Pruefsumme wie bei der Aktualisierung. Bewusst nicht
            // nachgebaut: es soll im Programm genau eine Stelle geben, die
            // eine Datei zu einer Pruefsumme macht.
            return string.Equals(
                UpdateStaging.BerechnePruefsumme(datei.FullName),
                zettel.Sha256,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Raeumt bereitgelegte Sicherung, halb entpackte Datei und
    /// Begleitzettel weg. Wird nach einem gescheiterten Versuch gerufen,
    /// damit derselbe kaputte Stand nicht bei jedem Start erneut probiert
    /// wird. Die SICHERHEITSKOPIE bleibt liegen - sie ist eine gueltige
    /// Sicherung und schadet nicht.
    /// </summary>
    public static void Verwirf(string databaseFilePath)
    {
        LoescheStill(NeuPfad(databaseFilePath));
        LoescheStill(TempPfad(databaseFilePath));
        LoescheStill(ZettelPfad(databaseFilePath));
    }

    internal static void LoescheStill(string pfad)
    {
        try
        {
            if (File.Exists(pfad))
            {
                File.Delete(pfad);
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
