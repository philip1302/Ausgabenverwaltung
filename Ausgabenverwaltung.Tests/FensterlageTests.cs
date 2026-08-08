using Ausgabenverwaltung.Core.Display;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Regeln zur gemerkten Fensterlage (<see cref="WindowPlacements"/>).
///
/// Der Fall, um den es hier eigentlich geht, laesst sich in Wirklichkeit
/// nur mit einem zweiten Bildschirm herstellen: Fenster auf den zweiten
/// Bildschirm schieben, Anwendung schliessen, Bildschirm abziehen, wieder
/// starten. Ohne Pruefung geht das Fenster dort auf, wo kein Bildschirm
/// mehr ist - unsichtbar und nicht zurueckzuholen, weil man dafuer die
/// Titelzeile greifen muesste. Genau deshalb steht die Entscheidung in
/// Core und nicht in der Ansicht.
/// </summary>
public class FensterlageTests
{
    // Ein ueblicher Aufbau: Hauptbildschirm 1920x1080 (Arbeitsflaeche ohne
    // Taskleiste 1040 hoch), zweiter Bildschirm RECHTS daneben.
    private static readonly ScreenArea Haupt = new(0, 0, 1920, 1040);
    private static readonly ScreenArea Rechts = new(1920, 0, 1920, 1040);

    // Und einer, den die naheliegende Pruefung "Koordinate kleiner null ist
    // falsch" kaputtmachen wuerde: zweiter Bildschirm LINKS vom
    // Hauptbildschirm. Dort sind die X-Werte negativ, und ein Fenster da
    // ist voellig in Ordnung.
    private static readonly ScreenArea Links = new(-1920, 0, 1920, 1040);

    private static WindowPlacement Lage(int left, int top, double breite = 1200, double hoehe = 800)
        => new() { Left = left, Top = top, Width = breite, Height = hoehe };

    // ---------------- Lage ----------------

    [Fact]
    public void Eine_Lage_auf_dem_Hauptbildschirm_gilt()
        => Assert.True(WindowPlacements.IsOnScreen(Lage(100, 100), [Haupt]));

    [Fact]
    public void Eine_Lage_auf_dem_zweiten_Bildschirm_gilt()
        => Assert.True(WindowPlacements.IsOnScreen(Lage(2200, 300), [Haupt, Rechts]));

    /// <summary>
    /// Der eigentliche Anlass: derselbe Wert, aber der zweite Bildschirm
    /// ist nicht mehr da.
    /// </summary>
    [Fact]
    public void Dieselbe_Lage_gilt_nicht_mehr_ohne_den_zweiten_Bildschirm()
        => Assert.False(WindowPlacements.IsOnScreen(Lage(2200, 300), [Haupt]));

    /// <summary>
    /// Negative Koordinaten sind KEIN Fehler. Ein Bildschirm links vom
    /// Hauptbildschirm hat sie, und eine Pruefung auf "grosser oder gleich
    /// null" waere der naheliegende und falsche Weg.
    /// </summary>
    [Fact]
    public void Negative_Koordinaten_gelten_wenn_dort_ein_Bildschirm_steht()
        => Assert.True(WindowPlacements.IsOnScreen(Lage(-1500, 200), [Links, Haupt]));

    [Fact]
    public void Negative_Koordinaten_gelten_nicht_ohne_Bildschirm_dort()
        => Assert.False(WindowPlacements.IsOnScreen(Lage(-1500, 200), [Haupt]));

    /// <summary>
    /// Ein Fenster, das links etwas heraushaengt, bleibt brauchbar: seine
    /// Titelzeile ist noch zu greifen.
    /// </summary>
    [Fact]
    public void Ein_wenig_ueber_den_Rand_hinaus_ist_noch_in_Ordnung()
        => Assert.True(WindowPlacements.IsOnScreen(Lage(-10, 5), [Haupt]));

    /// <summary>
    /// Ein Fenster, das nur noch mit seinem rechten unteren Zipfel auf den
    /// Bildschirm ragt, UEBERLAPPT ihn zwar - hat aber keine greifbare
    /// Titelzeile. Genau deshalb wird ein Punkt kurz hinter der linken
    /// oberen Ecke geprueft und nicht die Ueberlappung.
    /// </summary>
    [Fact]
    public void Nur_der_Zipfel_auf_dem_Bildschirm_genuegt_nicht()
        => Assert.False(WindowPlacements.IsOnScreen(Lage(1900, 1030), [Haupt]));

    /// <summary>
    /// Die Aufloesung ist kleiner geworden. Was vorher am rechten Rand
    /// stand, liegt jetzt draussen.
    /// </summary>
    [Fact]
    public void Eine_kleinere_Aufloesung_macht_eine_Lage_am_Rand_ungueltig()
    {
        var lage = Lage(1600, 800);

        Assert.True(WindowPlacements.IsOnScreen(lage, [Haupt]));
        Assert.False(WindowPlacements.IsOnScreen(lage, [new ScreenArea(0, 0, 1280, 720)]));
    }

    /// <summary>
    /// Liess sich nicht feststellen, welche Bildschirme es gibt, gilt die
    /// Lage als unbrauchbar: ein zentriert geoeffnetes Fenster ist immer
    /// richtig, ein unsichtbares nie.
    /// </summary>
    [Fact]
    public void Ohne_bekannte_Bildschirme_gilt_keine_Lage()
        => Assert.False(WindowPlacements.IsOnScreen(Lage(100, 100), []));

    /// <summary>
    /// Die Taskleiste gehoert nicht zur Arbeitsflaeche. Ein Fenster, dessen
    /// Titelzeile dahinter liegen wuerde, gilt nicht.
    /// </summary>
    [Fact]
    public void Die_Flaeche_hinter_der_Taskleiste_zaehlt_nicht()
        => Assert.False(WindowPlacements.IsOnScreen(Lage(500, 1050), [Haupt]));

    // ---------------- Groesse ----------------

    [Fact]
    public void Eine_uebliche_Groesse_bleibt_unveraendert()
    {
        var lage = WindowPlacements.Normalize(Lage(100, 100, 1200, 800));

        Assert.NotNull(lage);
        Assert.Equal(1200, lage!.Width);
        Assert.Equal(800, lage.Height);
        Assert.Equal(100, lage.Left);
        Assert.Equal(100, lage.Top);
    }

    /// <summary>
    /// Ein winziges Fenster kann nur aus einer von Hand veraenderten Datei
    /// oder aus einem halb geschriebenen Wert stammen. Es wird auf das
    /// Mindestmass gebracht statt verworfen - die LAGE ist ja brauchbar.
    /// </summary>
    [Fact]
    public void Eine_zu_kleine_Groesse_wird_auf_das_Mindestmass_gebracht()
    {
        var lage = WindowPlacements.Normalize(Lage(100, 100, 20, 10));

        Assert.Equal(WindowPlacements.MinWidth, lage!.Width);
        Assert.Equal(WindowPlacements.MinHeight, lage.Height);
    }

    [Fact]
    public void Eine_unsinnig_grosse_Groesse_wird_beschnitten()
    {
        var lage = WindowPlacements.Normalize(Lage(0, 0, 999999, 999999));

        Assert.Equal(WindowPlacements.MaxSize, lage!.Width);
        Assert.Equal(WindowPlacements.MaxSize, lage.Height);
    }

    /// <summary>
    /// Nicht darstellbare Zahlen zu beschneiden waere Raten - eine solche
    /// Lage gilt als nicht vorhanden, das Fenster oeffnet wie beim ersten
    /// Start.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 800)]
    [InlineData(1200, double.NaN)]
    [InlineData(double.PositiveInfinity, 800)]
    [InlineData(1200, double.NegativeInfinity)]
    public void Unbrauchbare_Zahlen_verwerfen_die_ganze_Lage(double breite, double hoehe)
        => Assert.Null(WindowPlacements.Normalize(Lage(100, 100, breite, hoehe)));

    [Fact]
    public void Nichts_Gemerktes_bleibt_nichts_Gemerktes()
        => Assert.Null(WindowPlacements.Normalize(null));

    /// <summary>
    /// Das Maximiert-Sein ueberlebt die Pruefung - es wird getrennt von
    /// Lage und Groesse gemerkt, weil ein maximiertes Fenster seine
    /// normale Lage nicht verraet.
    /// </summary>
    [Fact]
    public void Das_Merkmal_maximiert_bleibt_erhalten()
    {
        var lage = WindowPlacements.Normalize(
            Lage(100, 100) with { IsMaximized = true });

        Assert.True(lage!.IsMaximized);
    }
}
