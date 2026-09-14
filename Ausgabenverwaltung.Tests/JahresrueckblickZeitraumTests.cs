using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Datumsarithmetik des Jahresrueckblicks. Ohne Datenbank - hier geht
/// es ausschliesslich um Jahresgrenzen und Schaltjahre.
/// </summary>
public class JahresrueckblickZeitraumTests
{
    private static readonly DateOnly Heute = new(2026, 9, 13);

    // Anzahl der Tage eines Zeitraums. Das Ende ist ausschliessend, die
    // Differenz der Tagesnummern ist damit unmittelbar die Laenge.
    private static int Tage(Core.Reports.DateRange range) =>
        range.ToExclusive.DayNumber - range.From.DayNumber;

    [Fact]
    public void Ein_laufendes_Jahr_wird_standardmaessig_nur_bis_heute_verglichen()
    {
        var zeitraeume = ReviewPeriods.Build(2026, ReviewSpan.GleicherZeitraum, Heute);

        Assert.True(zeitraeume.IsPartial);
        Assert.Equal(new DateOnly(2026, 9, 13), zeitraeume.CurrentLastDay);
        Assert.Equal(new DateOnly(2025, 9, 13), zeitraeume.PreviousLastDay);
        Assert.Equal(Tage(zeitraeume.Current), Tage(zeitraeume.Previous));
    }

    // Darauf beruht der ganze Aufbau: liegt jeder Zeitraum vollstaendig in
    // EINEM Kalenderjahr, liefert eine nach Jahren gruppierte Abfrage je
    // Zeitraum genau eine Spalte - und die beiden Ergebnisse lassen sich
    // ohne Schluesselkollision zusammenschuetten.
    [Theory]
    [InlineData(ReviewSpan.GleicherZeitraum)]
    [InlineData(ReviewSpan.GanzeKalenderjahre)]
    public void Beide_Zeitraeume_beginnen_am_ersten_Januar(ReviewSpan spanne)
    {
        var zeitraeume = ReviewPeriods.Build(2026, spanne, Heute);

        Assert.Equal(new DateOnly(2026, 1, 1), zeitraeume.Current.From);
        Assert.Equal(new DateOnly(2025, 1, 1), zeitraeume.Previous.From);
        Assert.Equal(2026, zeitraeume.CurrentLastDay.Year);
        Assert.Equal(2025, zeitraeume.PreviousLastDay.Year);
    }

    [Fact]
    public void Ganze_Kalenderjahre_reichen_vom_ersten_Januar_bis_zum_ersten_Januar()
    {
        var zeitraeume = ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, Heute);

        Assert.False(zeitraeume.IsPartial);
        Assert.Equal(new DateOnly(2027, 1, 1), zeitraeume.Current.ToExclusive);
        Assert.Equal(new DateOnly(2026, 1, 1), zeitraeume.Previous.ToExclusive);
        Assert.Equal(new DateOnly(2026, 12, 31), zeitraeume.CurrentLastDay);
    }

    // "Bis heute" liegt bei einem abgeschlossenen Jahr hinter dessen Ende.
    // Die Wahl wird dort still ignoriert statt abgelehnt - die Oberflaeche
    // bietet sie gar nicht erst an.
    [Fact]
    public void Ein_abgeschlossenes_Jahr_wird_immer_ganz_verglichen()
    {
        var zeitraeume = ReviewPeriods.Build(2024, ReviewSpan.GleicherZeitraum, Heute);

        Assert.False(zeitraeume.IsPartial);
        Assert.Equal(new DateOnly(2024, 12, 31), zeitraeume.CurrentLastDay);
        Assert.Equal(new DateOnly(2023, 12, 31), zeitraeume.PreviousLastDay);
    }

    // Den 29. Februar gibt es im Vorjahr nicht. Gekuerzt wird auf den
    // letzten vorhandenen Tag des Monats - dieselbe Regel wie bei den
    // wiederkehrenden Buchungen (Regel 5).
    [Fact]
    public void Der_neunundzwanzigste_Februar_wird_im_Vorjahr_auf_den_achtundzwanzigsten_gekuerzt()
    {
        var zeitraeume = ReviewPeriods.Build(
            2028, ReviewSpan.GleicherZeitraum, new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2028, 2, 29), zeitraeume.CurrentLastDay);
        Assert.Equal(new DateOnly(2027, 2, 28), zeitraeume.PreviousLastDay);

        // Der einzige Tag im Jahr, an dem die beiden Zeitraeume verschieden
        // lang sind - im Vorjahr fehlt schlicht der Tag.
        Assert.Equal(60, Tage(zeitraeume.Current));
        Assert.Equal(59, Tage(zeitraeume.Previous));
    }

    // Der Nachbarfall, der bei einer Ausrichtung am AUSschliessenden Ende
    // schiefginge: der 29.02. waere dort das Ende des laufenden Zeitraums
    // und wuerde im Vorjahr auf den 28.02. gekuerzt - der Vorjahreszeitraum
    // verloere einen Tag, den er hat.
    [Fact]
    public void Der_achtundzwanzigste_Februar_im_Schaltjahr_vergleicht_gleich_viele_Tage()
    {
        var zeitraeume = ReviewPeriods.Build(
            2028, ReviewSpan.GleicherZeitraum, new DateOnly(2028, 2, 28));

        Assert.Equal(new DateOnly(2028, 2, 28), zeitraeume.CurrentLastDay);
        Assert.Equal(new DateOnly(2027, 2, 28), zeitraeume.PreviousLastDay);
        Assert.Equal(Tage(zeitraeume.Current), Tage(zeitraeume.Previous));
    }

    [Fact]
    public void Am_ersten_Januar_vergleicht_der_Rueckblick_einen_einzigen_Tag()
    {
        var zeitraeume = ReviewPeriods.Build(
            2026, ReviewSpan.GleicherZeitraum, new DateOnly(2026, 1, 1));

        Assert.Equal(1, Tage(zeitraeume.Current));
        Assert.Equal(1, Tage(zeitraeume.Previous));
        Assert.False(zeitraeume.Current.IsEmpty);
        Assert.False(zeitraeume.Previous.IsEmpty);
    }

    [Fact]
    public void Der_eingeschraenkte_Zeitraum_traegt_einen_lesbaren_Namen()
    {
        var zeitraeume = ReviewPeriods.Build(2026, ReviewSpan.GleicherZeitraum, Heute);

        Assert.Equal("1. Januar bis 13. September", zeitraeume.SpanCaption);
    }

    [Fact]
    public void Ganze_Kalenderjahre_tragen_ebenfalls_einen_lesbaren_Namen()
    {
        var zeitraeume = ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, Heute);

        Assert.Equal("1. Januar bis 31. Dezember", zeitraeume.SpanCaption);
    }

    [Fact]
    public void Die_Beschriftung_nennt_das_Jahr_und_sein_Vorjahr()
    {
        var zeitraeume = ReviewPeriods.Build(2026, ReviewSpan.GleicherZeitraum, Heute);

        Assert.Equal("2026", zeitraeume.CurrentLabel);
        Assert.Equal("2025", zeitraeume.PreviousLabel);
    }

    [Theory]
    [InlineData(2026, true)]
    [InlineData(2025, false)]
    [InlineData(2027, false)]
    public void IstLaufendesJahr_erkennt_nur_das_Jahr_von_heute(int jahr, bool erwartet)
    {
        Assert.Equal(erwartet, ReviewPeriods.IstLaufendesJahr(jahr, Heute));
    }
}
