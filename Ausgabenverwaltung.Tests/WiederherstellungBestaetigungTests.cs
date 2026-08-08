using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Eingabebestaetigung vor dem Einspielen einer Sicherung
/// (<see cref="RestoreConfirmation"/>) und die bezifferten Folgen
/// (<see cref="RestoreText.Folgen"/>).
///
/// Beides steht in Core, weil es pruefbar ist (Regel 7): dass der Knopf
/// erst freigibt, wenn der richtige Name dasteht, ist die einzige Huerde
/// vor der einzigen Aktion, die den ganzen Datenbestand austauscht.
/// </summary>
public class WiederherstellungBestaetigungTests
{
    private const string Dateiname = "ausgaben-2026-08-07-0814.zip";

    [Fact]
    public void Der_richtige_Dateiname_bestaetigt()
        => Assert.True(RestoreConfirmation.Matches(Dateiname, Dateiname));

    /// <summary>
    /// Gross-/Kleinschreibung und umgebende Leerzeichen bestrafen nur das
    /// Abtippen selbst und schuetzen niemanden.
    /// </summary>
    [Theory]
    [InlineData("AUSGABEN-2026-08-07-0814.ZIP")]
    [InlineData("  ausgaben-2026-08-07-0814.zip  ")]
    [InlineData("\tausgaben-2026-08-07-0814.zip\n")]
    public void Schreibweise_und_Leerzeichen_sind_egal(string getippt)
        => Assert.True(RestoreConfirmation.Matches(getippt, Dateiname));

    /// <summary>
    /// Der Zeitstempel im Namen ist der Unterschied zwischen der Sicherung
    /// von heute und der von vor drei Wochen - er muss stimmen.
    /// </summary>
    [Theory]
    [InlineData("ausgaben-2026-08-06-0814.zip")]
    [InlineData("ausgaben-2026-08-07-0815.zip")]
    [InlineData("ausgaben-2026-08-07-0814")]
    [InlineData("ausgaben")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Alles_andere_bestaetigt_nicht(string? getippt)
        => Assert.False(RestoreConfirmation.Matches(getippt, Dateiname));

    // ---------------- Die bezifferten Folgen ----------------

    /// <summary>
    /// Der eigentliche Zweck des Textes: "94 Buchungen waeren danach weg"
    /// ist eine Frage, die jemand beantworten kann - "Wiederherstellen?"
    /// nicht.
    /// </summary>
    [Fact]
    public void Die_Folgen_nennen_beide_Anzahlen_und_den_Unterschied()
    {
        var text = RestoreText.Folgen(Dateiname, activeCount: 1284, backupCount: 1190);

        Assert.Contains("1.284", text);
        Assert.Contains("1.190", text);
        Assert.Contains("94 Buchungen wären danach weg", text);
        Assert.Contains(Dateiname, text);
    }

    /// <summary>
    /// Gleiche Anzahl ist KEINE Entwarnung: geaendert, verschoben und
    /// umbenannt kann trotzdem alles sein. Der Text muss das sagen, sonst
    /// liest sich "beide 1.284" wie "es passiert nichts".
    /// </summary>
    [Fact]
    public void Gleiche_Anzahl_wird_nicht_als_Entwarnung_formuliert()
    {
        var text = RestoreText.Folgen(Dateiname, activeCount: 1284, backupCount: 1284);

        Assert.Contains("nicht gleicher Inhalt", text);
        Assert.DoesNotContain("wären danach weg", text);
    }

    [Fact]
    public void Eine_umfangreichere_Sicherung_wird_als_solche_benannt()
    {
        var text = RestoreText.Folgen(Dateiname, activeCount: 10, backupCount: 40);

        Assert.Contains("umfangreichere", text);
        Assert.DoesNotContain("wären danach weg", text);
    }

    [Fact]
    public void Eine_einzelne_Buchung_steht_in_der_Einzahl()
    {
        var text = RestoreText.Folgen(Dateiname, activeCount: 2, backupCount: 1);

        Assert.Contains("1 Buchung wäre danach weg", text);
    }

    /// <summary>
    /// Jeder Text des Ablaufs muss den Rueckweg nennen. Wer nicht weiss,
    /// dass die bisherige Datenbank gesichert wird, bestaetigt entweder
    /// nicht oder bestaetigt blind.
    /// </summary>
    [Theory]
    [InlineData("Folgen")]
    [InlineData("Bereitgelegt")]
    [InlineData("Uebernommen")]
    public void Jeder_Text_nennt_den_Weg_zurueck(string welcher)
    {
        var text = welcher switch
        {
            "Folgen" => RestoreText.Folgen(Dateiname, 10, 5),
            "Bereitgelegt" => RestoreText.Bereitgelegt(Dateiname, "ausgaben-vor-x.db"),
            _ => RestoreText.Uebernommen(Dateiname, "ausgaben-vor-x.db"),
        };

        Assert.Contains("Sicherungsordner", text);
        Assert.Contains("vollständig", text);
    }

    /// <summary>
    /// Das Band nach dem Bereitlegen darf nicht "erledigt" sagen: es fehlt
    /// der Neustart, und bis dahin gelten die bisherigen Daten.
    /// </summary>
    [Fact]
    public void Das_Band_nach_dem_Bereitlegen_sagt_dass_noch_nichts_eingespielt_ist()
    {
        var text = RestoreText.Bereitgelegt(Dateiname, "ausgaben-vor-x.db");

        Assert.Contains("beim nächsten Start", text);
        Assert.Contains("unverändert", text);
    }

    [Fact]
    public void Die_Aufforderung_nennt_den_abzutippenden_Namen()
        => Assert.Contains(Dateiname, RestoreText.Abtippen(Dateiname));
}
