using System.Data;
using System.Globalization;
using System.IO.Compression;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Updates;
using Dapper;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Das Wiederherstellen einer Sicherung - in zwei Haelften, die zu
/// verschiedenen Zeitpunkten laufen.
///
/// <b>Warum zweigeteilt.</b> Die aktive Datenbank ist waehrend der ganzen
/// Laufzeit offen (eine Verbindung, dazu die -wal/-shm-Dateien daneben).
/// Sie im Betrieb zu ersetzen ist unter Windows nicht moeglich und
/// anderswo unklug. Genau dieselbe Lage hat die Aktualisierung bei der
/// Programmdatei, und sie loest sie genauso (siehe
/// <see cref="UpdateInstaller"/>): bereitlegen, wenn nichts daran haengt
/// ersetzen.
///
/// 1. <see cref="Vorbereiten"/> laeuft im Betrieb: es sichert die bisherige
///    Datenbank, entpackt die gewaehlte Sicherung daneben und legt einen
///    Begleitzettel dazu. Danach wird die Anwendung neu gestartet.
/// 2. <see cref="TryUebernehmen"/> laeuft GANZ FRUEH im naechsten Start,
///    vor Datenbank, Einzelinstanz-Sperre und Fenster. Dort ist das
///    Ersetzen ein Umbenennen und nichts haengt daran.
///
/// Beide Haelften werfen nie. Scheitert das Bereitlegen, bleibt die aktive
/// Datenbank unangetastet; scheitert die Uebernahme, wird sie verworfen und
/// die Anwendung startet mit der bisherigen Datenbank weiter. Das ist
/// immer die bessere von beiden Moeglichkeiten - eine nicht
/// wiederhergestellte Sicherung ist ein Aergernis, eine halb ersetzte
/// Datenbank ein Schaden.
/// </summary>
public static class BackupRestore
{
    /// <summary>
    /// Das Ergebnis eines Uebernahmeversuchs beim Start.
    /// </summary>
    public enum Ergebnis
    {
        /// <summary>Nichts lag bereit - der Normalfall.</summary>
        NichtsVorbereitet,

        /// <summary>Ersetzt; die Anwendung laeuft ab jetzt auf der
        /// wiederhergestellten Datenbank.</summary>
        Uebernommen,

        /// <summary>Es lag etwas bereit, liess sich aber nicht uebernehmen.
        /// Es wurde verworfen, der Start laeuft mit der bisherigen
        /// Datenbank weiter.</summary>
        Gescheitert,
    }

    /// <summary>
    /// Was beim Bereitlegen herauskam. <see cref="Erfolgreich"/> heisst:
    /// die Sicherung liegt bereit und wird beim naechsten Start uebernommen.
    /// </summary>
    public sealed record Vorbereitung
    {
        public required bool Erfolgreich { get; init; }

        /// <summary>
        /// Dateiname der Sicherheitskopie der bisherigen Datenbank - der
        /// Rueckweg, der in der Meldung genannt wird. NULL, wenn es nicht
        /// einmal dazu kam.
        /// </summary>
        public string? Sicherheitskopie { get; init; }

        /// <summary>
        /// Bei welchem Schritt es scheiterte. Entscheidet den Fehlertext:
        /// eine gescheiterte Sicherheitskopie ist etwas anderes als ein
        /// gescheitertes Entpacken.
        /// </summary>
        public bool SicherheitskopieGescheitert { get; init; }

        public StorageProblem Problem { get; init; }
    }

    /// <summary>
    /// Namensbestandteil der Sicherheitskopie. Bewusst ausserhalb des
    /// Rasters von <see cref="BackupFileName"/>: so wird sie nie als
    /// Sicherung gezaehlt und faellt nie der Aufbewahrungsgrenze zum Opfer
    /// (<see cref="BackupRetention"/>) - eine Kopie, die man vielleicht
    /// zurueckholen will, darf nicht nach zehn Sicherungen verschwinden.
    /// </summary>
    private const string SicherheitskopieMuster = "ausgaben-vor-wiederherstellung-";

    /// <summary>
    /// Der Name der Sicherheitskopie zu einem Zeitpunkt, etwa
    /// "ausgaben-vor-wiederherstellung-2026-08-08_1432.db". Lokale Zeit,
    /// weil der Name zum Kalendertag des Anwenders passen soll (Regel 3
    /// betrifft gespeicherte Zeitstempel).
    /// </summary>
    public static string SicherheitskopieName(DateTime nowLocal)
        => SicherheitskopieMuster
           + nowLocal.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture)
           + ".db";

    /// <summary>
    /// Legt die gewaehlte Sicherung zur Uebernahme bereit. Vorher wird die
    /// bisherige Datenbank gesichert; scheitert das, wird NICHTS
    /// vorbereitet - eine Wiederherstellung ohne Rueckweg gibt es nicht.
    /// </summary>
    /// <param name="connection">
    /// Die laufende Verbindung. Die Sicherheitskopie entsteht per
    /// VACUUM INTO darueber und nicht als Dateikopie: nur so ist sie in
    /// sich stimmig und enthaelt auch, was noch im Schreibprotokoll
    /// (-wal) steht.
    /// </param>
    public static Vorbereitung Vorbereiten(
        IDbConnection connection,
        string zipPath,
        string databaseFilePath,
        string backupFolderPath,
        int? schemaVersion,
        DateTime nowLocal)
    {
        // Reste eines abgebrochenen Versuchs. Ohne dieses Aufraeumen
        // scheiterte gleich das Umbenennen unten.
        RestoreStaging.Verwirf(databaseFilePath);

        var sicherheitskopie = SicherheitskopieName(nowLocal);

        try
        {
            Directory.CreateDirectory(backupFolderPath);

            var kopiePfad = Path.Combine(backupFolderPath, sicherheitskopie);

            // VACUUM INTO will eine nicht vorhandene Zieldatei. Ein zweiter
            // Versuch in derselben Minute traegt denselben Namen.
            RestoreStaging.LoescheStill(kopiePfad);

            connection.Execute("VACUUM INTO @TargetPath", new { TargetPath = kopiePfad });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Sichern der bisherigen Datenbank", ex);

            return new Vorbereitung
            {
                Erfolgreich = false,
                SicherheitskopieGescheitert = true,
                Problem = StorageProblems.Classify(ex),
            };
        }

        try
        {
            var tempPfad = RestoreStaging.TempPfad(databaseFilePath);
            var neuPfad = RestoreStaging.NeuPfad(databaseFilePath);

            // Erst vollstaendig entpacken, dann umbenennen: eine halb
            // entpackte Datei darf nie unter dem Namen liegen, unter dem
            // der naechste Start sie uebernimmt.
            EntpackeDatenbank(zipPath, tempPfad);
            File.Move(tempPfad, neuPfad);

            var datei = new FileInfo(neuPfad);

            RestoreStaging.SchreibeZettel(databaseFilePath, new RestoreStaging.Zettel
            {
                Quelldatei = Path.GetFileName(zipPath),
                Sicherheitskopie = sicherheitskopie,
                Sha256 = UpdateStaging.BerechnePruefsumme(neuPfad),
                SizeBytes = datei.Length,
                SchemaVersion = schemaVersion,
            });

            AppLog.Current.Info(
                LogEvents.RestorePrepared(Path.GetFileName(zipPath), schemaVersion));

            return new Vorbereitung
            {
                Erfolgreich = true,
                Sicherheitskopie = sicherheitskopie,
            };
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Bereitlegen der Wiederherstellung", ex);

            // Nichts Halbes liegen lassen. Die Sicherheitskopie bleibt -
            // sie ist eine gueltige Sicherung und schadet nicht.
            RestoreStaging.Verwirf(databaseFilePath);

            return new Vorbereitung
            {
                Erfolgreich = false,
                Sicherheitskopie = sicherheitskopie,
                Problem = StorageProblems.Classify(ex),
            };
        }
    }

    /// <summary>
    /// Uebernimmt eine bereitliegende Sicherung, falls eine bereitliegt und
    /// sie der Pruefung standhaelt. Gehoert GANZ FRUEH in den Start, vor
    /// allem, was die Datenbank anfasst.
    ///
    /// Liefert neben dem Ergebnis den Begleitzettel, damit die Anwendung
    /// nach dem Neustart sagen kann, was geschehen ist - nach der
    /// Uebernahme ist der Zettel weg.
    /// </summary>
    public static (Ergebnis Ergebnis, RestoreStaging.Zettel? Zettel) TryUebernehmen(
        string databaseFilePath)
    {
        var neu = RestoreStaging.NeuPfad(databaseFilePath);

        try
        {
            var zettel = RestoreStaging.LiesZettel(databaseFilePath);

            // Ohne Begleitzettel gilt eine daliegende Datei als
            // unvollstaendig - etwa als Rest eines abgebrochenen Versuchs.
            if (zettel is null)
            {
                if (File.Exists(neu))
                {
                    RestoreStaging.Verwirf(databaseFilePath);
                }

                return (Ergebnis.NichtsVorbereitet, null);
            }

            if (!File.Exists(neu))
            {
                RestoreStaging.Verwirf(databaseFilePath);
                return (Ergebnis.NichtsVorbereitet, null);
            }

            // Die Pruefsumme wird hier ein ZWEITES Mal geprueft, obwohl sie
            // beim Bereitlegen schon stimmte. Zwischen damals und jetzt
            // liegt mindestens ein Programmende, womoeglich ein Absturz -
            // und was gleich die Datenbank ersetzt, prueft man unmittelbar
            // davor.
            if (!RestoreStaging.PasstZumZettel(databaseFilePath, zettel))
            {
                AppLog.Current.Warning(LogEvents.RestoreDiscarded(zettel.Quelldatei));
                RestoreStaging.Verwirf(databaseFilePath);
                return (Ergebnis.Gescheitert, null);
            }

            Ersetze(databaseFilePath, neu);

            RestoreStaging.LoescheStill(RestoreStaging.ZettelPfad(databaseFilePath));

            AppLog.Current.Info(
                LogEvents.RestoreTakenOver(zettel.Quelldatei, zettel.SchemaVersion));

            return (Ergebnis.Uebernommen, zettel);
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Uebernehmen einer Wiederherstellung", ex);
            RestoreStaging.Verwirf(databaseFilePath);

            return (Ergebnis.Gescheitert, null);
        }
    }

    /// <summary>
    /// Der eigentliche Austausch: zwei Umbenennungen innerhalb desselben
    /// Ordners. Zwischen ihnen liegt der einzige heikle Augenblick - faellt
    /// hier der Strom aus, fehlt die Datenbank. Deshalb der Umweg ueber
    /// ".vorher" statt eines Loeschens: die bisherige Datei ist bis zuletzt
    /// vorhanden und liesse sich von Hand zurueckbenennen.
    /// </summary>
    private static void Ersetze(string databaseFilePath, string neu)
    {
        var alt = RestoreStaging.AltPfad(databaseFilePath);

        // Ein Rest vom vorletzten Mal wuerde das Umbenennen scheitern lassen.
        RestoreStaging.LoescheStill(alt);

        if (File.Exists(databaseFilePath))
        {
            File.Move(databaseFilePath, alt);
        }

        try
        {
            File.Move(neu, databaseFilePath);
        }
        catch (Exception)
        {
            // Der zweite Schritt ging schief - die bisherige Datenbank
            // zurueckholen, sonst startet gar nichts mehr. Ihre -wal/-shm
            // stehen noch unberuehrt daneben, sie ist also vollstaendig.
            if (File.Exists(alt))
            {
                File.Move(alt, databaseFilePath);
            }

            throw;
        }

        // JETZT muessen das Schreibprotokoll und die geteilte Datei der
        // BISHERIGEN Datenbank weg. File.Move nimmt sie nicht mit, sie
        // liegen also nach dem Austausch neben der NEUEN Datei - und ein
        // fremdes -wal auf eine Datenbank anzuwenden, zu der es nicht
        // gehoert, ist genau der Datenverlust, den diese Funktion
        // verhindern soll. Verloren geht dabei nichts: die
        // Sicherheitskopie entstand per VACUUM INTO und enthaelt, was im
        // -wal stand.
        RestoreStaging.LoescheStill(databaseFilePath + "-wal");
        RestoreStaging.LoescheStill(databaseFilePath + "-shm");

        RestoreStaging.LoescheStill(alt);
    }

    /// <summary>
    /// Holt die Datenbankdatei aus dem ZIP. Der Eintrag heisst wie die
    /// aktive Datenbank (siehe <see cref="AppPaths.DatabaseFileName"/>) -
    /// dass er da ist, hat <see cref="BackupVerification"/> vorher schon
    /// festgestellt; hier wird es trotzdem geprueft, weil zwischen Pruefung
    /// und Entpacken die Datei gewechselt haben kann.
    /// </summary>
    private static void EntpackeDatenbank(string zipPath, string zielPfad)
    {
        using var archiv = ZipFile.OpenRead(zipPath);

        var eintrag = archiv.GetEntry(AppPaths.DatabaseFileName)
            ?? throw new InvalidDataException(
                $"Die Sicherung enthaelt keine Datei namens {AppPaths.DatabaseFileName}.");

        eintrag.ExtractToFile(zielPfad, overwrite: true);
    }
}
