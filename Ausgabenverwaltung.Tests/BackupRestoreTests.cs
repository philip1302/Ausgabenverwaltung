using System.IO.Compression;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Das Wiederherstellen einer Sicherung - beide Haelften, die im Betrieb
/// (<see cref="BackupRestore.Vorbereiten"/>) und die beim naechsten Start
/// (<see cref="BackupRestore.TryUebernehmen"/>).
///
/// Die Tests fahren den Ablauf durchgehend mit ECHTEN Dateien: die
/// Wiederherstellung besteht praktisch nur aus Dateibewegungen, und ein
/// Test, der die wegabstrahiert, prueft genau das nicht, was schiefgehen
/// kann.
/// </summary>
public class BackupRestoreTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-wiederherstellung-");

    private string DbPfad => Path.Combine(_tempDir.FullName, AppPaths.DatabaseFileName);
    private string SicherungsordnerPfad => Path.Combine(_tempDir.FullName, "Backups");

    private static readonly DateTime Jetzt = new(2026, 8, 8, 14, 32, 0, DateTimeKind.Local);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    // ---------------- Bereitlegen ----------------

    [Fact]
    public void Bereitlegen_sichert_die_bisherige_Datenbank_und_legt_die_Sicherung_bereit()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        using var connection = OeffneAktiveDatenbank(buchungen: 7);

        var vorbereitung = BackupRestore.Vorbereiten(
            connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);

        Assert.True(vorbereitung.Erfolgreich);
        Assert.Equal("ausgaben-vor-wiederherstellung-2026-08-08_1432.db", vorbereitung.Sicherheitskopie);

        // Die Sicherheitskopie liegt im Sicherungsordner und ist die
        // bisherige Datenbank, nicht die Sicherung.
        var kopie = Path.Combine(SicherungsordnerPfad, vorbereitung.Sicherheitskopie!);
        Assert.True(File.Exists(kopie));
        Assert.Equal(7, ZaehleBuchungen(kopie));

        // Bereitgelegt, aber NICHT eingespielt: die aktive Datenbank hat
        // noch ihre sieben Buchungen.
        Assert.True(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.NotNull(RestoreStaging.LiesZettel(DbPfad));
        Assert.Equal(7, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Expense"));
    }

    /// <summary>
    /// Der Zettel wird als LETZTES geschrieben. Ohne ihn gilt eine
    /// daliegende Datei als unvollstaendig - genau das trennt eine
    /// Wiederherstellung von einem Datenverlust.
    /// </summary>
    [Fact]
    public void Der_Begleitzettel_nennt_Quelldatei_Sicherheitskopie_und_Pruefsumme()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        using var connection = OeffneAktiveDatenbank(buchungen: 1);

        BackupRestore.Vorbereiten(
            connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);

        var zettel = RestoreStaging.LiesZettel(DbPfad);

        Assert.NotNull(zettel);
        Assert.Equal("ausgaben-2026-08-01-0800.zip", zettel!.Quelldatei);
        Assert.Equal("ausgaben-vor-wiederherstellung-2026-08-08_1432.db", zettel.Sicherheitskopie);
        Assert.Equal(4, zettel.SchemaVersion);
        Assert.NotEmpty(zettel.Sha256);
        Assert.True(RestoreStaging.PasstZumZettel(DbPfad, zettel));
    }

    /// <summary>
    /// Ein ZIP ohne Datenbankdatei kommt aus der Pruefung normalerweise nicht
    /// heraus - zwischen Pruefung und Entpacken kann die Datei aber
    /// gewechselt haben. Dann darf nichts halb Fertiges liegen bleiben.
    /// </summary>
    [Fact]
    public void Ein_ZIP_ohne_Datenbankdatei_legt_nichts_bereit()
    {
        var zip = Path.Combine(_tempDir.FullName, "leer.zip");
        using (var archiv = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archiv.CreateEntry("liesmich.txt");
        }

        using var connection = OeffneAktiveDatenbank(buchungen: 2);

        var vorbereitung = BackupRestore.Vorbereiten(
            connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: null, Jetzt);

        Assert.False(vorbereitung.Erfolgreich);
        Assert.False(vorbereitung.SicherheitskopieGescheitert);

        Assert.False(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.False(File.Exists(RestoreStaging.TempPfad(DbPfad)));
        Assert.Null(RestoreStaging.LiesZettel(DbPfad));

        // Die Sicherheitskopie stand schon und bleibt liegen - sie ist eine
        // gueltige Sicherung und schadet nicht.
        Assert.True(File.Exists(
            Path.Combine(SicherungsordnerPfad, vorbereitung.Sicherheitskopie!)));
    }

    /// <summary>
    /// Ohne Weg zurueck wird nichts eingespielt. Der Sicherungsordner ist
    /// hier nicht anlegbar, weil sein Elternteil eine Datei ist.
    /// </summary>
    [Fact]
    public void Ohne_Sicherheitskopie_wird_nichts_bereitgelegt()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        using var connection = OeffneAktiveDatenbank(buchungen: 2);

        var vorbereitung = BackupRestore.Vorbereiten(
            connection, zip, DbPfad, Path.Combine(blockierer, "Backups"),
            schemaVersion: 4, Jetzt);

        Assert.False(vorbereitung.Erfolgreich);
        Assert.True(vorbereitung.SicherheitskopieGescheitert);
        Assert.False(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.Null(RestoreStaging.LiesZettel(DbPfad));
    }

    // ---------------- Uebernehmen ----------------

    [Fact]
    public void Uebernehmen_ersetzt_die_aktive_Datenbank()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        using (var connection = OeffneAktiveDatenbank(buchungen: 7))
        {
            BackupRestore.Vorbereiten(
                connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);
        }

        // Wie beim echten Start: der Vorgaenger ist beendet, nichts haelt
        // die Datei mehr offen.
        SqliteConnection.ClearAllPools();

        var (ergebnis, zettel) = BackupRestore.TryUebernehmen(DbPfad);

        Assert.Equal(BackupRestore.Ergebnis.Uebernommen, ergebnis);
        Assert.Equal("ausgaben-2026-08-01-0800.zip", zettel!.Quelldatei);

        // Der Stand aus der Sicherung gilt.
        Assert.Equal(3, ZaehleBuchungen(DbPfad));

        // Nichts bleibt liegen: weder bereitgelegte Datei noch Zettel noch
        // die beiseitegelegte bisherige Datenbank.
        Assert.False(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.False(File.Exists(RestoreStaging.AltPfad(DbPfad)));
        Assert.Null(RestoreStaging.LiesZettel(DbPfad));
    }

    /// <summary>
    /// Der gefaehrlichste Rest: ein Schreibprotokoll (-wal) der BISHERIGEN
    /// Datenbank neben der neuen Datei. File.Move nimmt es nicht mit, und
    /// ein fremdes -wal auf eine Datenbank anzuwenden, zu der es nicht
    /// gehoert, ist genau der Datenverlust, den der ganze Ablauf verhindern
    /// soll.
    /// </summary>
    [Fact]
    public void Uebernehmen_raeumt_Schreibprotokoll_und_geteilte_Datei_der_alten_Datenbank_weg()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        using (var connection = OeffneAktiveDatenbank(buchungen: 7))
        {
            BackupRestore.Vorbereiten(
                connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);
        }

        SqliteConnection.ClearAllPools();

        // Reste, wie sie ein abrupt beendeter Prozess hinterlaesst.
        File.WriteAllText(DbPfad + "-wal", "Rest eines alten Schreibprotokolls.");
        File.WriteAllText(DbPfad + "-shm", "Rest einer alten geteilten Datei.");

        Assert.Equal(
            BackupRestore.Ergebnis.Uebernommen,
            BackupRestore.TryUebernehmen(DbPfad).Ergebnis);

        Assert.False(File.Exists(DbPfad + "-wal"));
        Assert.False(File.Exists(DbPfad + "-shm"));

        // Und die eingespielte Datenbank ist danach zu oeffnen.
        Assert.Equal(3, ZaehleBuchungen(DbPfad));
    }

    [Fact]
    public void Ohne_Vorbereitung_wird_nichts_uebernommen()
    {
        using (OeffneAktiveDatenbank(buchungen: 7)) { }
        SqliteConnection.ClearAllPools();

        var (ergebnis, zettel) = BackupRestore.TryUebernehmen(DbPfad);

        Assert.Equal(BackupRestore.Ergebnis.NichtsVorbereitet, ergebnis);
        Assert.Null(zettel);
        Assert.Equal(7, ZaehleBuchungen(DbPfad));
    }

    /// <summary>
    /// Eine bereitliegende Datei OHNE Begleitzettel ist der Rest eines
    /// abgebrochenen Versuchs. Sie wird verworfen und nicht eingespielt -
    /// niemand weiss, ob sie vollstaendig ist.
    /// </summary>
    [Fact]
    public void Eine_bereitliegende_Datei_ohne_Begleitzettel_wird_verworfen()
    {
        using (OeffneAktiveDatenbank(buchungen: 7)) { }
        SqliteConnection.ClearAllPools();

        File.WriteAllText(RestoreStaging.NeuPfad(DbPfad), "Halb entpackt.");

        var (ergebnis, _) = BackupRestore.TryUebernehmen(DbPfad);

        Assert.Equal(BackupRestore.Ergebnis.NichtsVorbereitet, ergebnis);
        Assert.False(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.Equal(7, ZaehleBuchungen(DbPfad));
    }

    /// <summary>
    /// Die Pruefsumme wird beim Uebernehmen ein zweites Mal geprueft.
    /// Zwischen Bereitlegen und Start liegt mindestens ein Programmende,
    /// womoeglich ein Absturz - und was gleich die Datenbank ersetzt, prueft
    /// man unmittelbar davor.
    /// </summary>
    [Fact]
    public void Eine_nachtraeglich_veraenderte_Datei_wird_verworfen_und_nichts_ersetzt()
    {
        var zip = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);

        using (var connection = OeffneAktiveDatenbank(buchungen: 7))
        {
            BackupRestore.Vorbereiten(
                connection, zip, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);
        }

        SqliteConnection.ClearAllPools();

        File.WriteAllText(RestoreStaging.NeuPfad(DbPfad), "Nicht mehr das, was der Zettel sagt.");

        var (ergebnis, zettel) = BackupRestore.TryUebernehmen(DbPfad);

        Assert.Equal(BackupRestore.Ergebnis.Gescheitert, ergebnis);
        Assert.Null(zettel);

        // Die aktive Datenbank ist unberuehrt - der ganze Sinn des zweiten
        // Pruefens.
        Assert.Equal(7, ZaehleBuchungen(DbPfad));

        // Und verworfen, damit derselbe kaputte Stand nicht bei jedem Start
        // erneut probiert wird.
        Assert.False(File.Exists(RestoreStaging.NeuPfad(DbPfad)));
        Assert.Null(RestoreStaging.LiesZettel(DbPfad));
    }

    /// <summary>
    /// Ein zweiter Ablauf raeumt den ersten weg. Sonst scheiterte das
    /// Umbenennen an der schon vorhandenen Datei, oder - schlimmer - es
    /// laege ein Zettel neben einer Datei aus einem anderen Versuch.
    /// </summary>
    [Fact]
    public void Ein_zweites_Bereitlegen_ersetzt_das_erste()
    {
        var alt = ErzeugeSicherung("ausgaben-2026-08-01-0800.zip", buchungen: 3);
        var neu = ErzeugeSicherung("ausgaben-2026-08-05-0900.zip", buchungen: 5);

        using (var connection = OeffneAktiveDatenbank(buchungen: 7))
        {
            BackupRestore.Vorbereiten(
                connection, alt, DbPfad, SicherungsordnerPfad, schemaVersion: 4, Jetzt);

            BackupRestore.Vorbereiten(
                connection, neu, DbPfad, SicherungsordnerPfad, schemaVersion: 4,
                Jetzt.AddMinutes(1));
        }

        SqliteConnection.ClearAllPools();

        var (ergebnis, zettel) = BackupRestore.TryUebernehmen(DbPfad);

        Assert.Equal(BackupRestore.Ergebnis.Uebernommen, ergebnis);
        Assert.Equal("ausgaben-2026-08-05-0900.zip", zettel!.Quelldatei);
        Assert.Equal(5, ZaehleBuchungen(DbPfad));
    }

    /// <summary>
    /// Der Name der Sicherheitskopie faellt bewusst durch das Raster von
    /// <see cref="BackupFileName"/>: sonst wuerde sie als Sicherung gezaehlt
    /// und irgendwann von der Aufbewahrungsgrenze geloescht - ausgerechnet
    /// die Kopie, die man vielleicht zurueckholen will.
    /// </summary>
    [Fact]
    public void Der_Name_der_Sicherheitskopie_gilt_nicht_als_Sicherung()
    {
        var name = BackupRestore.SicherheitskopieName(Jetzt);

        Assert.Equal("ausgaben-vor-wiederherstellung-2026-08-08_1432.db", name);
        Assert.False(BackupFileName.TryParseTimestamp(name, out _));
    }

    // ---------------- Helfer ----------------

    // Die "aktive" Datenbank: angelegt, gefuellt, und die Verbindung bleibt
    // offen - genau wie im Betrieb.
    private SqliteConnection OeffneAktiveDatenbank(int buchungen)
    {
        var connection = (SqliteConnection)SqliteConnectionFactory.OpenConnection(
            $"Data Source={DbPfad};Pooling=False");

        DatabaseInitializer.Initialize(connection);
        FuelleBuchungen(connection, buchungen);

        return connection;
    }

    private static int ZaehleBuchungen(string dateiPfad)
    {
        using var connection = SqliteConnectionFactory.OpenConnection(
            $"Data Source={dateiPfad};Pooling=False");

        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Expense");
    }

    // Baut eine echte Sicherung: dieselbe Datei im Archiv unter demselben
    // Namen, den BackupService verwendet.
    private string ErzeugeSicherung(string zipName, int buchungen)
    {
        var dbPfad = Path.Combine(
            _tempDir.FullName, Path.GetFileNameWithoutExtension(zipName) + ".db");

        using (var connection = SqliteConnectionFactory.OpenConnection(
                   $"Data Source={dbPfad};Pooling=False"))
        {
            DatabaseInitializer.Initialize(connection);
            FuelleBuchungen(connection, buchungen);
        }

        var zipPfad = Path.Combine(_tempDir.FullName, zipName);
        using (var archiv = ZipFile.Open(zipPfad, ZipArchiveMode.Create))
        {
            archiv.CreateEntryFromFile(
                dbPfad, AppPaths.DatabaseFileName, CompressionLevel.Optimal);
        }

        return zipPfad;
    }

    // Initialize legt nur das Schema an, keine Grunddaten - fuer die
    // Zaehlung reichen eine Kategorie und eine Person.
    private static void FuelleBuchungen(System.Data.IDbConnection connection, int anzahl)
    {
        if (anzahl <= 0)
        {
            return;
        }

        connection.Execute(
            """
            INSERT INTO Person (Name, IsSelf, CreatedUtc)
            VALUES ('Ich', 1, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));

            INSERT INTO Category (ParentId, Name, CreatedUtc)
            VALUES (NULL, 'Allgemein', strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
            """);

        for (var i = 0; i < anzahl; i++)
        {
            connection.Execute(
                """
                INSERT INTO Expense (CategoryId, PayerId, ExpenseDate, AmountCents,
                                     IsIncome, CreatedUtc, ModifiedUtc)
                SELECT
                    (SELECT MIN(Id) FROM Category),
                    (SELECT MIN(Id) FROM Person),
                    '2026-08-07', 1000, 0,
                    strftime('%Y-%m-%dT%H:%M:%SZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%SZ', 'now')
                """);
        }
    }
}
