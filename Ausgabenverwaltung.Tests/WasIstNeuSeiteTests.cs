using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Updates;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Seite "Was ist neu" mit demselben ViewModel, das die Ansicht
/// bindet, aber ohne Fenster - und mit der ECHTEN, eingebetteten
/// Aenderungsliste. Damit prueft sich nebenbei mit, dass CHANGELOG.md
/// tatsaechlich in der Baugruppe steckt; faellt das Einbetten weg, faellt
/// dieser Test.
///
/// Die Entscheidung selbst steht in Core (siehe
/// <see cref="WasIstNeuTests"/>); hier geht es um das, was danach
/// geschrieben wird: die gesehene Fassung merken. Ohne das erschiene die
/// Seite bei jedem Start erneut.
/// </summary>
public class WasIstNeuSeiteTests : IDisposable
{
    // Eine Fassung, zu der die echte Aenderungsliste einen Abschnitt hat,
    // und ihre Vorgaengerin.
    private const string MitAbschnitt = "1.4.0";
    private const string Davor = "1.3.1";

    /// <summary>
    /// Die NEUESTE Fassung, zu der die echte Aenderungsliste einen
    /// Abschnitt hat - berechnet und nicht eingetragen.
    ///
    /// Der Unterschied ist wichtig: hier stand einmal die Zahl 1.4.0, weil
    /// das damals die neueste war. Mit der naechsten Veroeffentlichung war
    /// sie es nicht mehr, und der Test darunter fiel - nicht weil etwas
    /// kaputt war, sondern weil zwischen ihr und der Phantasiefassung
    /// plötzlich ein echter Abschnitt lag. Ein Test, der bei jeder
    /// Veroeffentlichung rot wird, wird irgendwann angepasst statt gelesen.
    /// </summary>
    private static string Neueste => Changelog
        .Lies(Changelog.Eingebettet())
        .Max(eintrag => eintrag.Version)!
        .ToString(3);

    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-wasistneu-");

    private AppSettingsStore Speicher => new(Path.Combine(_tempDir.FullName, "settings.json"));

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Nach_einer_Aktualisierung_steht_die_Seite_mit_Titel_und_Inhalt_bereit()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings { LastSeenVersion = Davor });

        var seite = new WasIstNeuViewModel(speicher, MitAbschnitt);

        Assert.True(seite.Sichtbar);
        Assert.Contains(MitAbschnitt, seite.Titel, StringComparison.Ordinal);
        Assert.Contains("unverändert", seite.Einleitung, StringComparison.Ordinal);

        Assert.NotEmpty(seite.Abschnitte);
        Assert.Contains(seite.Abschnitte, zeile => zeile.IstPunkt);
        Assert.All(seite.Abschnitte, zeile => Assert.NotEqual(string.Empty, zeile.Text));
    }

    // Gemerkt wird sofort und nicht erst beim Wegklicken: die Seite soll
    // auch dann nicht ein zweites Mal aufgehen, wenn die Anwendung
    // dazwischen ueber den Fensterknopf beendet wird.
    [Fact]
    public void Die_gesehene_Fassung_wird_sofort_gemerkt()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings { LastSeenVersion = Davor });

        _ = new WasIstNeuViewModel(speicher, MitAbschnitt);

        Assert.Equal(MitAbschnitt, speicher.Load().LastSeenVersion);

        // Und beim naechsten Start bleibt es still.
        Assert.False(new WasIstNeuViewModel(speicher, MitAbschnitt).Sichtbar);
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
            LastSeenVersion = Davor,
        });

        _ = new WasIstNeuViewModel(speicher, MitAbschnitt);

        var gespeichert = speicher.Load();

        Assert.Equal(@"D:\Sicherungen", gespeichert.ExternalFolderPath);
        Assert.False(gespeichert.AutoUpdate);
        Assert.True(gespeichert.KeepEntryValues);
    }

    [Fact]
    public void Die_erste_Ausfuehrung_merkt_nur_den_Stand()
    {
        var speicher = Speicher;

        var seite = new WasIstNeuViewModel(speicher, MitAbschnitt);

        Assert.False(seite.Sichtbar);
        Assert.Empty(seite.Abschnitte);
        Assert.Equal(MitAbschnitt, speicher.Load().LastSeenVersion);
    }

    // Version angehoben, aber kein Abschnitt geschrieben (CLAUDE.md,
    // Regel 15): still bleiben, nicht mit leerer Seite aufgehen.
    //
    // Ausgangspunkt ist die NEUESTE dokumentierte Fassung - nur dann liegt
    // zwischen ihr und der Phantasiefassung darunter wirklich kein
    // Abschnitt.
    [Fact]
    public void Eine_Fassung_ohne_Abschnitt_zeigt_keine_leere_Seite()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings { LastSeenVersion = Neueste });

        var seite = new WasIstNeuViewModel(speicher, "99.0.0");

        Assert.False(seite.Sichtbar);
        Assert.Empty(seite.Abschnitte);
        Assert.Equal("99.0.0", speicher.Load().LastSeenVersion);
    }

    [Fact]
    public void Der_Weiter_Knopf_meldet_sich_ab()
    {
        var speicher = Speicher;
        speicher.Save(new AppSettings { LastSeenVersion = Davor });

        var seite = new WasIstNeuViewModel(speicher, MitAbschnitt);

        var gemeldet = 0;
        seite.Geschlossen += (_, _) => gemeldet++;

        seite.WeiterCommand.Execute(null);

        Assert.Equal(1, gemeldet);
    }
}
