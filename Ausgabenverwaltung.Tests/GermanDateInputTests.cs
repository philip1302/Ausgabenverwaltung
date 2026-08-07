using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

public class GermanDateInputTests
{
    [Theory]
    [InlineData("29.07.2026", 2026, 7, 29)]
    [InlineData("1.1.2026", 2026, 1, 1)] // ohne fuehrende Nullen
    [InlineData("01.01.2026", 2026, 1, 1)]
    public void TryParse_erkennt_gueltige_Datumsangaben(string text, int jahr, int monat, int tag)
    {
        var erfolgreich = GermanDateInput.TryParse(text, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2026-07-29")] // ISO-Format wird hier nicht akzeptiert
    [InlineData("32.01.2026")] // Tag existiert nicht
    [InlineData("29.13.2026")] // Monat existiert nicht
    public void TryParse_lehnt_ungueltige_Eingabe_ab(string text)
    {
        var erfolgreich = GermanDateInput.TryParse(text, out _);

        Assert.False(erfolgreich);
    }

    [Fact]
    public void ToText_formatiert_mit_fuehrenden_Nullen()
    {
        var text = GermanDateInput.ToText(new DateOnly(2026, 1, 5));

        Assert.Equal("05.01.2026", text);
    }

    [Fact]
    public void Rundtrip_ist_stabil()
    {
        var date = new DateOnly(2026, 7, 29);

        var erfolgreich = GermanDateInput.TryParse(GermanDateInput.ToText(date), out var geparst);

        Assert.True(erfolgreich);
        Assert.Equal(date, geparst);
    }

    // ================= Kurzformen =================

    // Ein Freitag mitten im Monat: der laufende Monat hat noch Tage vor
    // sich (fuer "15." unten) und genug hinter sich (fuer "-3").
    private static readonly DateOnly Heute = new(2026, 8, 7);

    [Theory]
    [InlineData("heute", 2026, 8, 7)]
    [InlineData("gestern", 2026, 8, 6)]
    [InlineData("vorgestern", 2026, 8, 5)]
    [InlineData("Heute", 2026, 8, 7)]    // Grossschreibung ist egal
    [InlineData("GESTERN", 2026, 8, 6)]
    [InlineData(" heute ", 2026, 8, 7)]  // Leerzeichen ebenso
    public void TryParse_versteht_die_Wortformen(string text, int jahr, int monat, int tag)
    {
        var erfolgreich = GermanDateInput.TryParse(text, Heute, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Theory]
    [InlineData("-1", 2026, 8, 6)]
    [InlineData("-3", 2026, 8, 4)]
    [InlineData("-10", 2026, 7, 28)]   // ueber die Monatsgrenze hinweg
    [InlineData("-0", 2026, 8, 7)]
    public void TryParse_versteht_Tage_in_der_Vergangenheit(string text, int jahr, int monat, int tag)
    {
        var erfolgreich = GermanDateInput.TryParse(text, Heute, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Fact]
    public void Eine_unsinnig_grosse_Tageszahl_wird_abgelehnt_statt_zu_werfen()
    {
        Assert.False(GermanDateInput.TryParse("-999999999", Heute, out _));
    }

    [Theory]
    [InlineData("5.", 2026, 8, 5)]   // schon vorbei: dieser Monat
    [InlineData("7.", 2026, 8, 7)]   // heute selbst zaehlt noch dazu
    [InlineData("1.", 2026, 8, 1)]
    public void Ein_Monatstag_meint_den_laufenden_Monat(string text, int jahr, int monat, int tag)
    {
        var erfolgreich = GermanDateInput.TryParse(text, Heute, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Theory]
    [InlineData("15.", 2026, 7, 15)]
    [InlineData("31.", 2026, 7, 31)]
    public void Ein_Monatstag_in_der_Zukunft_meint_den_Vormonat(string text, int jahr, int monat, int tag)
    {
        // Beim Nacherfassen eines Belegs ist fast immer der letzte
        // gemeinte Tag der richtige - ein Datum in der Zukunft waere hier
        // so gut wie nie gewollt.
        var erfolgreich = GermanDateInput.TryParse(text, Heute, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(jahr, monat, tag), date);
    }

    [Fact]
    public void Ein_Monatstag_den_es_in_keinem_der_beiden_Monate_gibt_wird_abgelehnt()
    {
        // Der 30. am 7. Maerz: im Maerz noch in der Zukunft, im Februar
        // gibt es ihn nicht. Ein stillschweigend verschobenes Datum waere
        // schlimmer als eine Meldung.
        var erfolgreich = GermanDateInput.TryParse("30.", new DateOnly(2026, 3, 7), out _);

        Assert.False(erfolgreich);
    }

    [Theory]
    [InlineData("0.")]
    [InlineData("32.")]
    [InlineData("morgen")]     // bewusst nicht: Buchungen liegen in der Vergangenheit
    [InlineData("uebermorgen")]
    [InlineData("15.8")]       // halbes Datum, kein Monatstag
    [InlineData("- 3")]
    [InlineData("abc")]
    [InlineData("")]
    public void Unbekannte_Kurzformen_werden_abgelehnt(string text)
    {
        Assert.False(GermanDateInput.TryParse(text, Heute, out _));
    }

    [Fact]
    public void Ein_vollstaendiges_Datum_hat_Vorrang_vor_der_Kurzform()
    {
        // "01.01.2026" endet auf keine Ziffer-Punkt-Form, aber der Test
        // sichert die Reihenfolge ab: erst das exakte Format, dann die
        // Kurzformen.
        var erfolgreich = GermanDateInput.TryParse("01.01.2026", Heute, out var date);

        Assert.True(erfolgreich);
        Assert.Equal(new DateOnly(2026, 1, 1), date);
    }

    [Theory]
    [InlineData("29.07.2026")]
    [InlineData("1.1.2026")]
    public void Die_bisherigen_Formate_gelten_in_beiden_Ueberladungen(string text)
    {
        Assert.True(GermanDateInput.TryParse(text, out var ohneBezug));
        Assert.True(GermanDateInput.TryParse(text, Heute, out var mitBezug));
        Assert.Equal(ohneBezug, mitBezug);
    }

    [Fact]
    public void Die_Ueberladung_ohne_Bezugstag_kennt_keine_Kurzformen()
    {
        // Im Von/Bis eines Zeitraums gibt es keinen sinnvollen Bezugstag,
        // dort bleibt es beim vollstaendigen Datum.
        Assert.False(GermanDateInput.TryParse("heute", out _));
    }

    [Theory]
    [InlineData("heute", "07.08.2026")]
    [InlineData("-3", "04.08.2026")]
    [InlineData("1.1.2026", "01.01.2026")]  // auch die fuehrenden Nullen kommen dazu
    public void Normalize_schreibt_das_erkannte_Datum_in_voller_Form(string text, string erwartet)
    {
        Assert.Equal(erwartet, GermanDateInput.Normalize(text, Heute));
    }

    [Theory]
    [InlineData("mor")]
    [InlineData("")]
    [InlineData(null)]
    public void Normalize_liefert_null_wenn_nichts_zurueckzuschreiben_ist(string? text)
    {
        // Dann bleibt stehen, was der Anwender getippt hat - eine halb
        // fertige Eingabe darf ihm nicht unter den Fingern verschwinden.
        Assert.Null(GermanDateInput.Normalize(text, Heute));
    }
}
