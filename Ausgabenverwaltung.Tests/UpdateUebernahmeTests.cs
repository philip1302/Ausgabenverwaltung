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
}
