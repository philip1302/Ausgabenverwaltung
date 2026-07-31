using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Ankertag im Vorlagenformular. Er wird jetzt bemaengelt, wenn er
/// nicht zum Rhythmus passt - das darf aber keine Falle werden, denn bei
/// Tag und Woche ist das Feld gar nicht sichtbar.
/// </summary>
public class VorlageAnkertagTests : IDisposable
{
    private readonly IDbConnection _connection;

    public VorlageAnkertagTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        new PersonRepository(_connection).Create("Ich", isSelf: true);
        new CategoryRepository(_connection).Create("Sonstiges", parentId: null);
    }

    public void Dispose() => _connection.Dispose();

    private VorlageBearbeitenViewModel NeuesFormular()
        => new(
            vorlage: null,
            kategoriePfad: null,
            new CategoryRepository(_connection).GetSelectableLeaves(),
            new PersonRepository(_connection).GetAllActive(),
            erzeugteAnzahl: 0,
            heute: new DateOnly(2026, 7, 31));

    private void SetzeEinheit(VorlageBearbeitenViewModel formular, string wert)
        => formular.AusgewaehlteIntervallEinheit =
            formular.IntervallOptionen.First(option => option.Wert == wert);

    [Fact]
    public void Beim_Wechsel_auf_Woche_wird_der_Ankertag_geraeumt()
    {
        var formular = NeuesFormular();
        formular.AnkertagText = "15";

        SetzeEinheit(formular, "week");

        Assert.False(formular.AnkertagSichtbar);
        Assert.Equal(string.Empty, formular.AnkertagText);
    }

    [Fact]
    public void Beim_Zurueckwechseln_auf_Monat_ist_der_Ankertag_wieder_da()
    {
        var formular = NeuesFormular();
        formular.AnkertagText = "15";

        SetzeEinheit(formular, "week");
        SetzeEinheit(formular, "month");

        Assert.True(formular.AnkertagSichtbar);
        Assert.Equal("15", formular.AnkertagText);
    }

    [Fact]
    public void Aus_dem_Formular_heraus_entsteht_die_unpassende_Kombination_nie()
    {
        // Das ist der Kern: die Pruefung lehnt "Woche + Ankertag" ab, aber
        // aus der Bedienung heraus kann dieser Zustand gar nicht
        // entstehen. Eine Meldung an einem ausgeblendeten Feld waere sonst
        // eine Sackgasse.
        var formular = NeuesFormular();
        formular.Titel = "Zeitung";
        formular.BetragText = "5,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];
        formular.AnkertagText = "31";

        SetzeEinheit(formular, "week");

        var ergebnis = formular.Pruefe();

        Assert.True(ergebnis.IsValid);
        Assert.False(formular.AnkertagFehlerSichtbar);
    }

    [Fact]
    public void Bei_Monatsrhythmus_bleibt_der_Ankertag_wirksam()
    {
        var formular = NeuesFormular();
        formular.Titel = "Miete";
        formular.BetragText = "800,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];

        SetzeEinheit(formular, "month");
        formular.AnkertagText = "3";

        var ergebnis = formular.Pruefe();

        Assert.True(ergebnis.IsValid);
        Assert.Equal(3, ergebnis.AnchorDay);
    }

    [Fact]
    public void Ein_Ankertag_ueber_28_loest_den_Hinweis_zur_Kuerzung_aus()
    {
        // Regel 5: existiert der Ankertag im Zielmonat nicht, wird auf den
        // letzten Tag gekuerzt. Das gehoert vorher gesagt.
        var formular = NeuesFormular();

        SetzeEinheit(formular, "month");
        formular.AnkertagText = "31";

        Assert.True(formular.AnkertagHinweisSichtbar);
    }
}
