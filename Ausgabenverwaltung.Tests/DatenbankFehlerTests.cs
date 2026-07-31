using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Startup;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Faelle, in denen die Datenbank beim Start nicht zu gebrauchen ist:
/// gesperrt, beschaedigt, unvollstaendig.
///
/// Geprueft wird beides - dass der Start sauber abbricht statt irgendwo
/// mitten in der Anwendung zu scheitern, UND dass daraus ein Text
/// entsteht, den ein Anwender ohne Terminal versteht.
/// </summary>
public class DatenbankFehlerTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-dbfehler-");

    private string DbPfad => Path.Combine(_tempDir.FullName, "ausgaben.db");
    private string BackupOrdner => Path.Combine(_tempDir.FullName, "Backups");
    private string EinstellungenPfad => Path.Combine(_tempDir.FullName, "settings.json");

    private StartupResult Starte()
        => StartupService.Run(DbPfad, BackupOrdner, EinstellungenPfad);

    private StartupFailure Beschreibe(Exception ex)
        => StartupFailureText.Describe(ex, DbPfad, BackupOrdner, @"C:\Logs", "1.0.0-test");

    public void Dispose()
    {
        // Microsoft.Data.Sqlite haelt Dateihandles ueber sein natives
        // Verbindungspooling offen - ohne dieses Leeren schlaegt das
        // Loeschen des Temp-Verzeichnisses fehl.
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    // ================= Gesperrte Datei =================

    [Fact]
    public void Eine_gesperrte_Datenbank_bricht_den_Start_ab()
    {
        Starte(); // regulaer anlegen

        // Eine zweite Verbindung haelt eine ausschliessliche Sperre - so
        // sieht es aus, wenn ein anderes Programm die Datei offen haelt.
        using var sperre = new SqliteConnection($"Data Source={DbPfad}");
        sperre.Open();
        sperre.Execute("PRAGMA locking_mode = EXCLUSIVE");
        sperre.Execute("BEGIN EXCLUSIVE");

        var ausnahme = Assert.ThrowsAny<SqliteException>(() => Starte());

        Assert.Equal(StorageProblem.DatabaseLocked, StorageProblems.Classify(ausnahme));
    }

    [Fact]
    public void Eine_von_aussen_exklusiv_gesperrte_Datei_gilt_als_gesperrt_nicht_als_verschwunden()
    {
        // SQLite meldet beides als SQLITE_CANTOPEN. Ohne den Blick ins
        // Dateisystem bekaeme der Anwender "Der Datenträger wurde
        // abgezogen" zu lesen, waehrend die Datei direkt vor ihm liegt.
        Starte();
        SqliteConnection.ClearAllPools();

        using var sperre = File.Open(DbPfad, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ausnahme = Assert.ThrowsAny<SqliteException>(() => Starte());

        Assert.Equal(
            StorageProblem.DatabaseLocked,
            StorageProblems.ClassifyForFile(ausnahme, DbPfad));

        var fehler = Beschreibe(ausnahme);
        Assert.Contains("läuft", fehler.Title);
        Assert.Contains("Taskleiste", fehler.Message);
    }

    [Fact]
    public void Eine_wirklich_verschwundene_Datei_bleibt_verschwunden()
    {
        // Die Gegenprobe: existiert die Datei nicht, darf die
        // Verfeinerung nicht zuschlagen.
        var weg = Path.Combine(_tempDir.FullName, "gibtesnicht.db");

        Assert.Equal(
            StorageProblem.PathNotFound,
            StorageProblems.ClassifyForFile(new DirectoryNotFoundException("weg"), weg));
    }

    [Fact]
    public void Die_Meldung_zur_gesperrten_Datei_nennt_die_laufende_Anwendung_zuerst()
    {
        var fehler = Beschreibe(SqliteFehler(5));

        Assert.Contains("läuft", fehler.Title);
        Assert.Contains("Taskleiste", fehler.Message);
        Assert.Contains("unverändert", fehler.Message);

        // Ein zweiter Versuch hat hier Aussicht: das andere Fenster ist
        // gleich zu.
        Assert.True(fehler.RetryWorthwhile);
    }

    // ================= Beschaedigte Datei =================

    [Fact]
    public void Eine_beschaedigte_Datenbank_faellt_bei_der_Integritaetspruefung_auf()
    {
        // Erst eine echte Datenbank mit genug Inhalt fuer mehrere Seiten
        // anlegen - eine einseitige Datei laesst sich nicht sinnvoll
        // beschaedigen, ohne dass sie gar keine Datenbank mehr ist.
        Starte();

        using (var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}"))
        {
            connection.Execute("PRAGMA journal_mode = DELETE");

            for (var i = 0; i < 400; i++)
            {
                connection.Execute(
                    "INSERT INTO Category (ParentId, Name, SortOrder, IsArchived, CreatedUtc) "
                    + "VALUES (NULL, @Name, @Sort, 0, '2026-01-01T00:00:00Z')",
                    new { Name = $"Kategorie {i}", Sort = i });
            }
        }

        SqliteConnection.ClearAllPools();
        BeschaedigeSeiten(DbPfad);

        var ausnahme = Assert.ThrowsAny<Exception>(() => Starte());

        // Je nach Art des Schadens meldet SQLite ihn ueber quick_check
        // (dann DatabaseCorruptException) oder verweigert schon den
        // Zugriff (dann SqliteException). Beides muss als Beschaedigung
        // erkannt werden - der Anwender bekommt in beiden Faellen
        // denselben Text.
        var fehler = Beschreibe(ausnahme);

        Assert.Contains("beschädigt", fehler.Title);
        Assert.Contains("Sicherungsordner", fehler.Message);
        Assert.Equal(BackupOrdner, fehler.FolderPath);

        // Hier hilft ein zweiter Versuch nicht - erst muss jemand etwas
        // tun.
        Assert.False(fehler.RetryWorthwhile);
    }

    [Fact]
    public void Eine_Datei_die_gar_keine_Datenbank_ist_wird_als_beschaedigt_gemeldet()
    {
        File.WriteAllText(DbPfad, "Das hier ist ein Einkaufszettel, keine Datenbank.");

        var ausnahme = Assert.ThrowsAny<Exception>(() => Starte());
        var fehler = Beschreibe(ausnahme);

        Assert.Contains("beschädigt", fehler.Title);
        Assert.Contains("Sicherungsordner öffnen", fehler.FolderButtonText);
    }

    [Fact]
    public void Die_Meldung_zur_beschaedigten_Datei_erklaert_das_Wiederherstellen()
    {
        var fehler = Beschreibe(new DatabaseCorruptException(DbPfad, "page 3 is never used"));

        // Die Anleitung muss die Schritte nennen, sonst steht der Anwender
        // vor einem Ordner voller ZIP-Dateien und weiss nicht weiter.
        Assert.Contains("entpacken", fehler.Message);
        Assert.Contains("ausgaben.db", fehler.Message);

        // Erst umbenennen, dann ersetzen - die beschaedigte Datei soll
        // nicht ueberschrieben werden, vielleicht ist noch etwas daraus
        // zu holen.
        Assert.Contains("Benennen Sie die beschädigte Datei um", fehler.Message);

        // Der technische Befund gehoert in den aufklappbaren Bereich,
        // nicht in den Haupttext.
        Assert.Contains("page 3 is never used", fehler.Technical);
        Assert.DoesNotContain("page 3 is never used", fehler.Message);
    }

    // ================= Unvollstaendige Datei =================

    [Fact]
    public void Eine_leere_Datei_wird_zu_einer_frischen_Datenbank()
    {
        // Eine 0-Byte-Datei ist fuer SQLite eine neue, leere Datenbank -
        // das ist kein Fehler und soll auch keiner sein.
        File.WriteAllBytes(DbPfad, []);

        var ergebnis = Starte();

        Assert.True(ergebnis.IsFirstStart);
    }

    [Fact]
    public void Eine_SchemaVersion_ohne_Eintrag_bricht_den_Start_ab()
    {
        // So sieht eine Datei aus, deren erster Start mittendrin abbrach:
        // die Tabelle ist da, der Stand fehlt.
        using (var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}"))
        {
            connection.Execute(
                "CREATE TABLE SchemaVersion (Version INTEGER NOT NULL, AppliedUtc TEXT NOT NULL)");
        }

        SqliteConnection.ClearAllPools();

        var ausnahme = Assert.Throws<SchemaVersionUnreadableException>(() => Starte());
        var fehler = Beschreibe(ausnahme);

        Assert.Contains("unvollständig", fehler.Title);
        Assert.Contains("Sicherungsordner", fehler.Message);
    }

    [Fact]
    public void Ein_Stand_von_null_gilt_als_unlesbar()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        connection.Execute(
            "CREATE TABLE SchemaVersion (Version INTEGER NOT NULL, AppliedUtc TEXT NOT NULL)");
        connection.Execute(
            "INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES (0, '2026-01-01T00:00:00Z')");

        Assert.Null(DatabaseInitializer.ReadSchemaVersion(connection));
        Assert.Throws<SchemaVersionUnreadableException>(
            () => DatabaseInitializer.GetSchemaVersion(connection));
    }

    // ================= Integritaetspruefung =================

    [Fact]
    public void Eine_gesunde_Datenbank_besteht_die_Pruefung()
    {
        Starte();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");

        Assert.Null(DatabaseHealth.QuickCheck(connection));
    }

    // ================= Zu neues Schema =================

    [Fact]
    public void Eine_zu_neue_Datenbank_bekommt_eine_eigene_Erklaerung()
    {
        var fehler = Beschreibe(new SchemaVersionTooNewException(9999, 2));

        Assert.Contains("neueren Programmversion", fehler.Title);
        Assert.Contains("unverändert und vollständig", fehler.Message);

        // Kein Verweis auf den Sicherungsordner: hier ist nichts kaputt,
        // es fehlt nur die passende Programmversion.
        Assert.NotEqual(BackupOrdner, fehler.FolderPath);
    }

    // ================= Hilfsmittel =================

    // Ueberschreibt den Inhalt ab der zweiten Seite. Der Dateikopf bleibt
    // stehen, damit SQLite die Datei weiterhin oeffnet und der Schaden
    // erst bei der Pruefung auffaellt.
    private static void BeschaedigeSeiten(string pfad)
    {
        using var datei = new FileStream(pfad, FileMode.Open, FileAccess.ReadWrite);

        const int seitengroesse = 4096;
        datei.Seek(seitengroesse * 2, SeekOrigin.Begin);

        var muell = new byte[seitengroesse * 4];
        Array.Fill(muell, (byte)0x7E);
        datei.Write(muell);
    }

    // SqliteException laesst sich nicht mit einem Ergebniscode erzeugen -
    // der Konstruktor nimmt ihn aber entgegen.
    private static SqliteException SqliteFehler(int ergebniscode)
        => new("database is locked", ergebniscode, ergebniscode);
}
