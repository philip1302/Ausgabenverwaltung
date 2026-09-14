using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.Settings;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Regeln ueber der Liste der gespeicherten Filter
/// (<see cref="SavedFilters"/>) und ihr Weg durch die
/// Einstellungsdatei.
/// </summary>
public class GespeicherteFilterTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-filter-");

    private string SettingsPath => Path.Combine(_tempDir.FullName, "settings.json");

    public void Dispose() => _tempDir.Delete(recursive: true);

    private static SavedFilter Filter(string name) => new() { Name = name };

    // ---------------- Name ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Ohne_Namen_wird_nicht_gespeichert(string? name)
    {
        Assert.NotNull(SavedFilters.Validate(name, []));
    }

    [Fact]
    public void Ein_zu_langer_Name_wird_abgelehnt()
    {
        var name = new string('x', SavedFilters.MaxNameLength + 1);

        Assert.NotNull(SavedFilters.Validate(name, []));
    }

    [Fact]
    public void Ein_Name_am_Rand_der_Laenge_geht_noch()
    {
        var name = new string('x', SavedFilters.MaxNameLength);

        Assert.Null(SavedFilters.Validate(name, []));
    }

    [Fact]
    public void Eine_volle_Liste_nimmt_keinen_neuen_Filter_mehr_auf()
    {
        var voll = Enumerable.Range(1, SavedFilters.MaxCount)
            .Select(i => Filter($"Filter {i}"))
            .ToList();

        Assert.NotNull(SavedFilters.Validate("Noch einer", voll));
    }

    // Sonst waere der letzte freie Platz eine Falle: nachbessern liesse
    // sich danach kein einziger Filter mehr.
    [Fact]
    public void Eine_volle_Liste_laesst_einen_vorhandenen_Namen_trotzdem_zu()
    {
        var voll = Enumerable.Range(1, SavedFilters.MaxCount)
            .Select(i => Filter($"Filter {i}"))
            .ToList();

        Assert.Null(SavedFilters.Validate("filter 3", voll));
    }

    // ---------------- Ablegen ----------------

    [Fact]
    public void Derselbe_Name_ersetzt_den_Eintrag_statt_ihn_zu_verdoppeln()
    {
        var liste = SavedFilters.Save([], Filter("Auto") with { MyCosts = false });

        liste = SavedFilters.Save(liste, Filter("auto") with { MyCosts = true });

        var eintrag = Assert.Single(liste);
        Assert.True(eintrag.MyCosts);

        // Der zuletzt vergebene Name gewinnt - auch in der Schreibweise.
        Assert.Equal("auto", eintrag.Name);
    }

    [Fact]
    public void Der_Name_wird_getrimmt_abgelegt()
    {
        var liste = SavedFilters.Save([], Filter("  Auto  "));

        Assert.Equal("Auto", Assert.Single(liste).Name);
    }

    [Fact]
    public void Die_Liste_steht_alphabetisch_nach_deutschen_Regeln()
    {
        var liste = SavedFilters.Save([], Filter("Zahnarzt"));
        liste = SavedFilters.Save(liste, Filter("Ärzte"));
        liste = SavedFilters.Save(liste, Filter("Auto"));

        Assert.Equal(["Ärzte", "Auto", "Zahnarzt"], liste.Select(f => f.Name));
    }

    [Fact]
    public void Loeschen_nimmt_genau_einen_Eintrag_heraus()
    {
        var liste = SavedFilters.Save([], Filter("Auto"));
        liste = SavedFilters.Save(liste, Filter("Wohnen"));

        liste = SavedFilters.Remove(liste, "AUTO");

        Assert.Equal("Wohnen", Assert.Single(liste).Name);
    }

    [Fact]
    public void Ein_unbekannter_Name_loescht_nichts()
    {
        var liste = SavedFilters.Save([], Filter("Auto"));

        Assert.Single(SavedFilters.Remove(liste, "Wohnen"));
    }

    // ---------------- Eingelesene Liste ----------------

    [Fact]
    public void Namenlose_Eintraege_und_Doppelgaenger_fallen_beim_Einlesen_weg()
    {
        var liste = SavedFilters.Normalize(
        [
            Filter("Auto"),
            Filter("  "),
            Filter("auto"),
            Filter("Wohnen"),
        ]);

        Assert.Equal(["Auto", "Wohnen"], liste.Select(f => f.Name));
    }

    [Fact]
    public void Mehr_als_die_Obergrenze_wird_beim_Einlesen_gekuerzt()
    {
        var zuviele = Enumerable.Range(1, SavedFilters.MaxCount + 5)
            .Select(i => Filter($"Filter {i:00}"))
            .ToList();

        Assert.Equal(SavedFilters.MaxCount, SavedFilters.Normalize(zuviele).Count);
    }

    // ---------------- Weg durch die Einstellungsdatei ----------------

    [Fact]
    public void Ein_gespeicherter_Filter_kommt_unveraendert_zurueck()
    {
        var speicher = new AppSettingsStore(SettingsPath);

        speicher.Save(new AppSettings
        {
            SavedFilters =
            [
                new SavedFilter
                {
                    Name = "Auto, fester Zeitraum",
                    From = new DateOnly(2026, 2, 1),
                    ToInclusive = new DateOnly(2026, 2, 28),
                    CategoryIds = [4, 7],
                    PayerIds = [2],
                    StatusOpen = true,
                    ExpensesOnly = true,
                    MyCosts = true,
                    SearchText = "Werkstatt",
                    Grouping = ReportGrouping.Quarter,
                },
            ],
        });

        var gelesen = Assert.Single(speicher.Load().SavedFilters);

        Assert.Equal("Auto, fester Zeitraum", gelesen.Name);
        Assert.Equal(new DateOnly(2026, 2, 1), gelesen.From);
        Assert.Equal(new DateOnly(2026, 2, 28), gelesen.ToInclusive);
        Assert.Equal([4, 7], gelesen.CategoryIds);
        Assert.Equal([2], gelesen.PayerIds);
        Assert.True(gelesen.StatusOpen);
        Assert.False(gelesen.StatusSettled);
        Assert.True(gelesen.ExpensesOnly);
        Assert.True(gelesen.MyCosts);
        Assert.Equal("Werkstatt", gelesen.SearchText);
        Assert.Equal(ReportGrouping.Quarter, gelesen.Grouping);
    }

    // Der Schnellwahl-Zeitraum wird als Schluessel abgelegt und nicht als
    // Datum: "dieses Jahr" soll auch im naechsten Jahr das laufende
    // meinen.
    [Fact]
    public void Ein_Schnellwahl_Zeitraum_bleibt_ein_Schluessel()
    {
        var speicher = new AppSettingsStore(SettingsPath);

        speicher.Save(new AppSettings
        {
            SavedFilters = [new SavedFilter { Name = "Dieses Jahr", PeriodKey = "DiesesJahr" }],
        });

        var gelesen = Assert.Single(speicher.Load().SavedFilters);

        Assert.Equal("DiesesJahr", gelesen.PeriodKey);
        Assert.Null(gelesen.From);
        Assert.Null(gelesen.ToInclusive);
    }

    [Fact]
    public void Eine_Datei_ohne_Filter_liefert_eine_leere_Liste()
    {
        File.WriteAllText(SettingsPath, """{ "FontScale": 1.0 }""");

        Assert.Empty(new AppSettingsStore(SettingsPath).Load().SavedFilters);
    }

    // Nachsichtig wie ueberall in der Einstellungsdatei: die kaputte Zeile
    // kostet ihren Filter, nicht die ganze Datei.
    [Fact]
    public void Kaputte_Eintraege_kosten_nur_sich_selbst()
    {
        File.WriteAllText(SettingsPath, """
        {
          "SavedFilters": [
            { "Name": "Ohne Datum", "From": "kein Datum", "Grouping": "Jahrhundert" },
            { "Name": "" },
            { "Name": "Wohnen", "CategoryIds": [3] }
          ]
        }
        """);

        var gelesen = new AppSettingsStore(SettingsPath).Load().SavedFilters;

        Assert.Equal(["Ohne Datum", "Wohnen"], gelesen.Select(f => f.Name));
        Assert.Null(gelesen[0].From);
        Assert.Null(gelesen[0].Grouping);
        Assert.Equal([3], gelesen[1].CategoryIds);
    }

    // ---------------- Schnellwahl-Schluessel ----------------

    [Theory]
    [InlineData("DieserMonat")]
    [InlineData("DiesesJahr")]
    [InlineData("LetztesJahr")]
    [InlineData("Letzte12Monate")]
    [InlineData("Letzte3Jahre")]
    public void Jeder_Schnellwahl_Schluessel_beider_Leisten_wird_verstanden(string schluessel)
    {
        var heute = new DateOnly(2026, 9, 14);

        Assert.True(DateRangePresets.TryByKey(schluessel, heute, out var bereich));
        Assert.NotNull(bereich);
    }

    [Fact]
    public void Alles_ist_ein_Ergebnis_und_kein_Fehlschlag()
    {
        Assert.True(DateRangePresets.TryByKey("Alles", new DateOnly(2026, 9, 14), out var bereich));
        Assert.Null(bereich);
    }

    [Fact]
    public void Ein_unbekannter_Schluessel_setzt_nichts()
    {
        Assert.False(DateRangePresets.TryByKey("Mondphase", new DateOnly(2026, 9, 14), out _));
    }
}
