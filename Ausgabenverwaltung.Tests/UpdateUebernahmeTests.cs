using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Austausch der Programmdatei beim Start - der Teil, bei dem am
/// meisten kaputtgehen kann. Geprueft wird hier alles ausser dem
/// Prozesswechsel selbst: statt einer echten Programmdatei liegt eine
/// gewoehnliche Datei im Temp-Verzeichnis, und ob umbenannt wurde, sieht
/// man ihrem Inhalt an.
///
/// Die Leitlinie, gegen die hier geprueft wird, lautet: ein misslungener
/// Austausch darf NIE dazu fuehren, dass keine Programmdatei mehr
/// dasteht. Eine veraltete Anwendung ist ein Aergernis, eine nicht mehr
/// startende ein Schaden.
/// </summary>
public class UpdateUebernahmeTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-update-");

    private string ZielPfad => Path.Combine(_tempDir.FullName, "Ausgabenverwaltung.exe");

    public void Dispose() => _tempDir.Delete(recursive: true);

    private void LegeLaufendeFassungAn(string inhalt = "alte Fassung")
        => File.WriteAllText(ZielPfad, inhalt);

    /// <summary>
    /// Legt eine vollstaendige Vorbereitung an: Datei plus Begleitzettel
    /// mit passender Groesse und Pruefsumme.
    /// </summary>
    private void LegeVorbereitungAn(string inhalt = "neue Fassung", string version = "v1.2.0")
    {
        var neu = UpdateStaging.NeuPfad(ZielPfad);
        File.WriteAllText(neu, inhalt);

        UpdateStaging.SchreibeZettel(ZielPfad, new UpdateStaging.Zettel
        {
            Version = version,
            Sha256 = UpdateStaging.BerechnePruefsumme(neu),
            SizeBytes = new FileInfo(neu).Length,
            AssetName = "Ausgabenverwaltung-win-x64.zip",
        });
    }

    [Fact]
    public void Ohne_Vorbereitung_passiert_nichts()
    {
        LegeLaufendeFassungAn();

        Assert.Equal(
            UpdateInstaller.Ergebnis.NichtsVorbereitet,
            UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal("alte Fassung", File.ReadAllText(ZielPfad));
    }

    [Fact]
    public void Eine_gepruefte_Vorbereitung_wird_uebernommen()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        Assert.Equal(
            UpdateInstaller.Ergebnis.Uebernommen,
            UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal("neue Fassung", File.ReadAllText(ZielPfad));
    }

    /// <summary>
    /// Die alte Fassung wird beiseitegelegt und nicht geloescht - sie
    /// liesse sich damit von Hand zurueckbenennen, falls die neue nicht
    /// startet.
    /// </summary>
    [Fact]
    public void Die_alte_Fassung_bleibt_zunaechst_daneben_liegen()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        UpdateInstaller.TryUebernehmen(ZielPfad);

        Assert.True(File.Exists(UpdateStaging.AltPfad(ZielPfad)));
        Assert.Equal("alte Fassung", File.ReadAllText(UpdateStaging.AltPfad(ZielPfad)));
    }

    [Fact]
    public void Nach_der_Uebernahme_liegt_keine_Vorbereitung_mehr_da()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        UpdateInstaller.TryUebernehmen(ZielPfad);

        Assert.False(File.Exists(UpdateStaging.NeuPfad(ZielPfad)));
        Assert.False(File.Exists(UpdateStaging.ZettelPfad(ZielPfad)));
    }

    /// <summary>
    /// Der eigentliche Sinn der zweiten Pruefung: zwischen Herunterladen
    /// und Uebernehmen liegt mindestens ein Programmende, womoeglich ein
    /// Absturz oder ein Virenscanner.
    /// </summary>
    [Fact]
    public void Eine_nachtraeglich_veraenderte_Vorbereitung_wird_verworfen()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        // Nach dem Schreiben des Zettels veraendert - die Pruefsumme
        // stimmt jetzt nicht mehr.
        File.WriteAllText(UpdateStaging.NeuPfad(ZielPfad), "etwas ganz anderes");

        Assert.Equal(
            UpdateInstaller.Ergebnis.Gescheitert,
            UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal("alte Fassung", File.ReadAllText(ZielPfad));
        Assert.False(File.Exists(UpdateStaging.NeuPfad(ZielPfad)));
    }

    /// <summary>
    /// Ohne Begleitzettel ist nicht zu unterscheiden, ob da eine
    /// vollstaendige Fassung liegt oder der Rest eines abgebrochenen
    /// Downloads.
    /// </summary>
    [Fact]
    public void Eine_Vorbereitung_ohne_Begleitzettel_wird_nicht_uebernommen()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(UpdateStaging.NeuPfad(ZielPfad), "halb geladen");

        Assert.Equal(
            UpdateInstaller.Ergebnis.NichtsVorbereitet,
            UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal("alte Fassung", File.ReadAllText(ZielPfad));
        Assert.False(File.Exists(UpdateStaging.NeuPfad(ZielPfad)));
    }

    [Fact]
    public void Ein_Begleitzettel_ohne_Datei_wird_weggeraeumt()
    {
        LegeLaufendeFassungAn();

        UpdateStaging.SchreibeZettel(ZielPfad, new UpdateStaging.Zettel
        {
            Version = "v1.2.0",
            Sha256 = new string('a', 64),
            SizeBytes = 123,
        });

        Assert.Equal(
            UpdateInstaller.Ergebnis.NichtsVorbereitet,
            UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.False(File.Exists(UpdateStaging.ZettelPfad(ZielPfad)));
    }

    /// <summary>
    /// Damit nicht bei jedem Start derselbe kaputte Stand erneut
    /// probiert wird.
    /// </summary>
    [Fact]
    public void Eine_verworfene_Vorbereitung_wird_kein_zweites_Mal_versucht()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();
        File.WriteAllText(UpdateStaging.NeuPfad(ZielPfad), "kaputt");

        Assert.Equal(
            UpdateInstaller.Ergebnis.Gescheitert, UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal(
            UpdateInstaller.Ergebnis.NichtsVorbereitet, UpdateInstaller.TryUebernehmen(ZielPfad));
    }

    [Fact]
    public void Ein_Rest_der_vorletzten_Fassung_verhindert_die_Uebernahme_nicht()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(UpdateStaging.AltPfad(ZielPfad), "vorvorige Fassung");
        LegeVorbereitungAn();

        Assert.Equal(
            UpdateInstaller.Ergebnis.Uebernommen, UpdateInstaller.TryUebernehmen(ZielPfad));

        Assert.Equal("neue Fassung", File.ReadAllText(ZielPfad));
    }

    [Fact]
    public void Aufraeumen_entfernt_die_beiseitegelegte_Fassung()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(UpdateStaging.AltPfad(ZielPfad), "vorige Fassung");

        UpdateInstaller.RaeumeAlteAuf(ZielPfad);

        Assert.False(File.Exists(UpdateStaging.AltPfad(ZielPfad)));
        Assert.True(File.Exists(ZielPfad));
    }

    /// <summary>
    /// Aufraeumen laeuft bei JEDEM Start, auch wenn nichts da ist.
    /// </summary>
    [Fact]
    public void Aufraeumen_ohne_etwas_zum_Aufraeumen_wirft_nicht()
    {
        LegeLaufendeFassungAn();

        UpdateInstaller.RaeumeAlteAuf(ZielPfad);

        Assert.True(File.Exists(ZielPfad));
    }

    [Fact]
    public void Ein_Ordner_ohne_Schreibrecht_faellt_vor_dem_Laden_auf()
    {
        // Der beschreibbare Fall - der Gegenprobe fehlt hier bewusst ein
        // Ort, den es plattformuebergreifend zuverlaessig gibt.
        Assert.True(UpdateStaging.OrdnerBeschreibbar(_tempDir.FullName));

        Assert.False(UpdateStaging.OrdnerBeschreibbar(
            Path.Combine(_tempDir.FullName, "gibt-es-nicht")));
    }

    [Fact]
    public void Die_Pruefsumme_wird_als_Hexziffern_in_Kleinschreibung_gebildet()
    {
        var datei = Path.Combine(_tempDir.FullName, "probe.txt");
        File.WriteAllText(datei, "abc");

        // SHA256("abc"), allgemein bekannter Wert.
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            UpdateStaging.BerechnePruefsumme(datei));
    }

    [Fact]
    public void Ein_unlesbarer_Begleitzettel_gilt_als_nicht_vorhanden()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(UpdateStaging.ZettelPfad(ZielPfad), "kein JSON");

        Assert.Null(UpdateStaging.LiesZettel(ZielPfad));
    }

    /// <summary>
    /// Ein Zettel ohne Pruefsumme ist wertlos - die Pruefung vor dem
    /// Austausch haengt genau daran.
    /// </summary>
    [Fact]
    public void Ein_Begleitzettel_ohne_Pruefsumme_gilt_als_nicht_vorhanden()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(
            UpdateStaging.ZettelPfad(ZielPfad),
            """{ "Version": "v1.2.0", "Sha256": "", "SizeBytes": 12 }""");

        Assert.Null(UpdateStaging.LiesZettel(ZielPfad));
    }

    // ---------------- Was danach im Ordner liegt ----------------

    /// <summary>
    /// Der Ordner ist nach Austausch UND Aufraeumen leer bis auf die
    /// Programmdatei. Das ist die eigentliche Zusicherung an den Anwender:
    /// er soll nach einer Aktualisierung nicht vor drei Eintraegen mit
    /// demselben Namensanfang stehen und raten, welcher das Programm ist.
    ///
    /// Die Endungen kommen aus <see cref="UpdateStaging.AlleEndungen"/>
    /// und werden hier NICHT aufgezaehlt - sonst prueft dieser Test beim
    /// Hinzufuegen einer sechsten nichts mehr.
    /// </summary>
    [Fact]
    public void Nach_Austausch_und_Aufraeumen_liegt_nur_noch_die_Programmdatei_da()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        // Reste, wie sie ein abgebrochenes Laden hinterlaesst.
        File.WriteAllText(UpdateStaging.TeilPfad(ZielPfad), "halb geladen");
        Directory.CreateDirectory(UpdateStaging.AuspackPfad(ZielPfad));

        UpdateInstaller.TryUebernehmen(ZielPfad);
        UpdateInstaller.RaeumeAlteAuf(ZielPfad);

        foreach (var endung in UpdateStaging.AlleEndungen)
        {
            var pfad = ZielPfad + endung;

            Assert.False(File.Exists(pfad), $"„{endung}“ liegt noch da.");
            Assert.False(Directory.Exists(pfad), $"„{endung}“ liegt noch da.");
        }

        Assert.Equal("neue Fassung", File.ReadAllText(ZielPfad));
    }

    /// <summary>
    /// Der Fehler, der beim Bauen dieser Aufraeumung fast entstanden waere:
    /// wer beim Start ALLE Endungen wegraeumt, loescht die geladene
    /// Fassung, bevor sie eingespielt werden kann - und die Aktualisierung
    /// findet nie statt, egal wie oft der Anwender neu startet.
    /// </summary>
    [Fact]
    public void Das_Aufraeumen_laesst_eine_wartende_Vorbereitung_stehen()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        UpdateInstaller.RaeumeAlteAuf(ZielPfad);

        Assert.True(File.Exists(UpdateStaging.NeuPfad(ZielPfad)));
        Assert.NotNull(UpdateStaging.LiesZettel(ZielPfad));

        // Und sie laesst sich danach noch uebernehmen.
        Assert.Equal(
            UpdateInstaller.Ergebnis.Uebernommen,
            UpdateInstaller.TryUebernehmen(ZielPfad));
    }

    /// <summary>
    /// Reste eines abgebrochenen Ladens hatten vorher gar keine
    /// Aufraeumung beim Start: sie kannte nur der Lauf, der sie angelegt
    /// hat, und blieben nach einem Absturz fuer immer liegen.
    /// </summary>
    [Fact]
    public void Reste_eines_abgebrochenen_Ladens_werden_beim_Start_geraeumt()
    {
        LegeLaufendeFassungAn();
        File.WriteAllText(UpdateStaging.TeilPfad(ZielPfad), "halb geladen");
        Directory.CreateDirectory(UpdateStaging.AuspackPfad(ZielPfad));
        File.WriteAllText(UpdateStaging.AltPfad(ZielPfad), "vorletzte Fassung");

        UpdateInstaller.RaeumeAlteAuf(ZielPfad);

        Assert.False(File.Exists(UpdateStaging.TeilPfad(ZielPfad)));
        Assert.False(Directory.Exists(UpdateStaging.AuspackPfad(ZielPfad)));
        Assert.False(File.Exists(UpdateStaging.AltPfad(ZielPfad)));
        Assert.True(File.Exists(ZielPfad));
    }

    /// <summary>
    /// Zu keinem Zeitpunkt des Austauschs duerfen ZWEI sichtbare Dateien
    /// mit demselben Namensanfang nebeneinander liegen - das ist der
    /// Zustand, der den Anwender ratlos macht. Dass es zwischendurch
    /// KEINE sichtbare gibt, ist dagegen erlaubt und ausdruecklich in Kauf
    /// genommen; deshalb prueft der Test auf "hoechstens eine" und nicht
    /// auf "genau eine".
    ///
    /// Nur unter Windows: anderswo kennt das Dateisystem dieses Merkmal
    /// nicht in dieser Form, dort sorgt allein das Raeumen fuer Ordnung.
    /// </summary>
    [WindowsOnlyFact]
    public void Waehrend_des_Austauschs_ist_hoechstens_eine_Datei_sichtbar()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();

        // So, wie das Laden sie hinterlaesst.
        UpdateStaging.Verstecke(UpdateStaging.NeuPfad(ZielPfad));
        UpdateStaging.Verstecke(UpdateStaging.ZettelPfad(ZielPfad));

        Assert.Equal(1, SichtbareEintraege());

        UpdateInstaller.TryUebernehmen(ZielPfad);

        Assert.Equal(1, SichtbareEintraege());
        Assert.Equal("neue Fassung", File.ReadAllText(ZielPfad));
    }

    /// <summary>
    /// Nach dem Austausch ist die neue Programmdatei wieder sichtbar -
    /// sie kam versteckt aus dem Laden, und eine unsichtbare Programmdatei
    /// waere schlimmer als jeder Rest.
    /// </summary>
    [WindowsOnlyFact]
    public void Die_neue_Programmdatei_ist_nach_dem_Austausch_sichtbar()
    {
        LegeLaufendeFassungAn();
        LegeVorbereitungAn();
        UpdateStaging.Verstecke(UpdateStaging.NeuPfad(ZielPfad));

        UpdateInstaller.TryUebernehmen(ZielPfad);

        Assert.False(File.GetAttributes(ZielPfad).HasFlag(FileAttributes.Hidden));
    }

    private int SichtbareEintraege()
        => Directory
            .EnumerateFileSystemEntries(_tempDir.FullName, "Ausgabenverwaltung.exe*")
            .Count(pfad => !File.GetAttributes(pfad).HasFlag(FileAttributes.Hidden));
}
