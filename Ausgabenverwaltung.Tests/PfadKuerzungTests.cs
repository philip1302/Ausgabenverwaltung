using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Das Kuerzen von Pfaden fuer eine Zeile.
///
/// Gekuerzt wird an Trennzeichen, nie mittendrin in einem Ordnernamen:
/// Anfang und Ende tragen die Aussage ("welches Laufwerk", "welcher
/// Ordner"), die Ebenen dazwischen nicht.
/// </summary>
public class PfadKuerzungTests
{
    [Fact]
    public void Ein_kurzer_Pfad_bleibt_unveraendert()
    {
        Assert.Equal(@"D:\Sicherungen", PfadKuerzung.Kuerze(@"D:\Sicherungen"));
    }

    [Fact]
    public void Ein_leerer_Pfad_ergibt_einen_leeren_Text()
    {
        Assert.Equal(string.Empty, PfadKuerzung.Kuerze(null));
        Assert.Equal(string.Empty, PfadKuerzung.Kuerze("   "));
    }

    [Fact]
    public void Ein_langer_Pfad_behaelt_Anfang_und_Ende()
    {
        var gekuerzt = PfadKuerzung.Kuerze(
            @"C:\Users\Paul\AppData\Roaming\Ausgabenverwaltung\Backups");

        Assert.StartsWith(@"C:\", gekuerzt);
        Assert.EndsWith(@"\Backups", gekuerzt);
        Assert.Contains("…", gekuerzt);

        // Und die Zwischenebenen sind wirklich weg.
        Assert.DoesNotContain("AppData", gekuerzt);
    }

    [Fact]
    public void Der_Trenner_richtet_sich_nach_dem_Pfad()
    {
        var gekuerzt = PfadKuerzung.Kuerze(
            "/home/paul/.local/share/Ausgabenverwaltung/Sicherungen");

        Assert.DoesNotContain(@"\", gekuerzt);
        Assert.EndsWith("/Sicherungen", gekuerzt);
    }

    [Fact]
    public void Ein_ueberlanger_letzter_Ordnername_verdraengt_den_Anfang()
    {
        // Passt selbst die gekuerzte Form nicht, zaehlt der Name am Ende:
        // er beantwortet "welcher Ordner?", der Laufwerksbuchstabe nicht.
        var gekuerzt = PfadKuerzung.Kuerze(
            @"C:\Zwischen\Ein-ausgesprochen-langer-Ordnername-fuer-die-Sicherungen",
            hoechstlaenge: 40);

        Assert.StartsWith(@"…\", gekuerzt);
        Assert.EndsWith("Sicherungen", gekuerzt);
    }

    [Fact]
    public void Ein_Pfad_ohne_Trennzeichen_wird_am_Anfang_beschnitten()
    {
        var gekuerzt = PfadKuerzung.Kuerze(new string('x', 80), hoechstlaenge: 20);

        Assert.Equal(20, gekuerzt.Length);
        Assert.StartsWith("…", gekuerzt);
    }
}
