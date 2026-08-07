using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Was die Seite "Was ist neu" beim Erzeugen mit den Einstellungen macht -
/// mit demselben ViewModel, das die Ansicht bindet, aber ohne Fenster.
///
/// Die Entscheidung selbst steht in Core (siehe
/// <see cref="WasIstNeuTests"/>); hier geht es um das, was danach
/// geschrieben wird: die gesehene Fassung merken und den gezeigten
/// Beschreibungstext wegraeumen. Ohne das erschiene die Seite bei jedem
/// Start erneut.
/// </summary>
public class WasIstNeuSeiteTests : IDisposable
{
    private const string Notizen = """
        ## What's Changed
        * Markierte Buchungen lassen sich gemeinsam aendern
        """;

    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-wasistneu-");

    private AppSettingsStore Speicher => new(Path.Combine(_tempDir.FullName, "settings.json"));

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Nach_einer_Aktualisierung_steht_die_Seite_mit_Titel_und_Abschnitten_bereit()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings
        {
            LastSeenVersion = "1.1.0",
            PendingReleaseNotesVersion = "v1.2.0",
            PendingReleaseNotes = Notizen,
        });

        var seite = new WasIstNeuViewModel(speicher, "1.2.0");

        Assert.True(seite.Sichtbar);
        Assert.Contains("1.2.0", seite.Titel, StringComparison.Ordinal);
        Assert.Contains("unverändert", seite.Einleitung, StringComparison.Ordinal);

        Assert.Collection(
            seite.Abschnitte,
            zeile =>
            {
                Assert.True(zeile.IstUeberschrift);
                Assert.False(zeile.IstPunkt);
            },
            zeile =>
            {
                Assert.False(zeile.IstUeberschrift);
                Assert.True(zeile.IstPunkt);
                Assert.Equal("Markierte Buchungen lassen sich gemeinsam aendern", zeile.Text);
            });
    }

    // Gemerkt wird sofort und nicht erst beim Wegklicken: die Seite soll
    // auch dann nicht ein zweites Mal aufgehen, wenn die Anwendung
    // dazwischen ueber den Fensterknopf beendet wird.
    [Fact]
    public void Die_gesehene_Fassung_wird_sofort_gemerkt_und_der_Text_geraeumt()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings
        {
            LastSeenVersion = "1.1.0",
            PendingReleaseNotesVersion = "v1.2.0",
            PendingReleaseNotes = Notizen,
        });

        _ = new WasIstNeuViewModel(speicher, "1.2.0");

        var gespeichert = speicher.Load();

        Assert.Equal("1.2.0", gespeichert.LastSeenVersion);
        Assert.Null(gespeichert.PendingReleaseNotesVersion);
        Assert.Null(gespeichert.PendingReleaseNotes);

        // Und beim naechsten Start bleibt es still.
        Assert.False(new WasIstNeuViewModel(speicher, "1.2.0").Sichtbar);
    }

    // Die uebrigen Einstellungen stehen in derselben Datei - wer eine
    // aendert, darf die anderen nicht auf ihre Vorgabewerte zuruecksetzen.
    [Fact]
    public void Andere_Einstellungen_bleiben_beim_Merken_unberuehrt()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings
        {
            ExternalFolderPath = @"D:\Sicherungen",
            AutoUpdate = false,
            KeepEntryValues = true,
            LastSeenVersion = "1.1.0",
            PendingReleaseNotesVersion = "v1.2.0",
            PendingReleaseNotes = Notizen,
        });

        _ = new WasIstNeuViewModel(speicher, "1.2.0");

        var gespeichert = speicher.Load();

        Assert.Equal(@"D:\Sicherungen", gespeichert.ExternalFolderPath);
        Assert.False(gespeichert.AutoUpdate);
        Assert.True(gespeichert.KeepEntryValues);
    }

    [Fact]
    public void Ohne_Aktualisierung_wird_nichts_gezeigt_und_nichts_geschrieben()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings
        {
            LastSeenVersion = "1.2.0",
            PendingReleaseNotesVersion = "v1.3.0",
            PendingReleaseNotes = Notizen,
        });

        var seite = new WasIstNeuViewModel(speicher, "1.2.0");

        Assert.False(seite.Sichtbar);
        Assert.Empty(seite.Abschnitte);

        // Der bereitliegende Text gehoert zur naechsten Fassung und muss
        // den Start unangetastet ueberstehen - sonst waere die Seite nach
        // dem Austausch leer.
        var gespeichert = speicher.Load();
        Assert.Equal("v1.3.0", gespeichert.PendingReleaseNotesVersion);
        Assert.Equal(Notizen, gespeichert.PendingReleaseNotes);
    }

    [Fact]
    public void Die_erste_Ausfuehrung_merkt_nur_den_Stand()
    {
        var speicher = Speicher;

        var seite = new WasIstNeuViewModel(speicher, "1.2.0");

        Assert.False(seite.Sichtbar);
        Assert.Equal("1.2.0", speicher.Load().LastSeenVersion);
    }

    [Fact]
    public void Der_Weiter_Knopf_meldet_sich_ab()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings
        {
            LastSeenVersion = "1.1.0",
            PendingReleaseNotesVersion = "v1.2.0",
            PendingReleaseNotes = Notizen,
        });

        var seite = new WasIstNeuViewModel(speicher, "1.2.0");

        var gemeldet = 0;
        seite.Geschlossen += (_, _) => gemeldet++;

        seite.WeiterCommand.Execute(null);

        Assert.Equal(1, gemeldet);
    }
}
