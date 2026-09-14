using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

public class DateRangePresetsTests
{
    [Fact]
    public void ThisMonth_reicht_vom_Monatsersten_bis_zum_Ersten_des_Folgemonats()
    {
        var range = DateRangePresets.ThisMonth(new DateOnly(2026, 3, 17));

        Assert.Equal(new DateOnly(2026, 3, 1), range.From);
        Assert.Equal(new DateOnly(2026, 4, 1), range.ToExclusive);
    }

    [Fact]
    public void ThisMonth_im_Dezember_endet_im_Folgejahr()
    {
        var range = DateRangePresets.ThisMonth(new DateOnly(2026, 12, 31));

        Assert.Equal(new DateOnly(2026, 12, 1), range.From);
        Assert.Equal(new DateOnly(2027, 1, 1), range.ToExclusive);
    }

    [Fact]
    public void ThisYear_reicht_vom_ersten_Januar_bis_zum_ersten_Januar_des_Folgejahres()
    {
        var range = DateRangePresets.ThisYear(new DateOnly(2026, 7, 29));

        Assert.Equal(new DateOnly(2026, 1, 1), range.From);
        Assert.Equal(new DateOnly(2027, 1, 1), range.ToExclusive);
    }

    [Fact]
    public void LastYear_umfasst_genau_das_Vorjahr()
    {
        var range = DateRangePresets.LastYear(new DateOnly(2026, 7, 29));

        Assert.Equal(new DateOnly(2025, 1, 1), range.From);
        Assert.Equal(new DateOnly(2026, 1, 1), range.ToExclusive);
    }

    // Nimmt die Jahreszahl unmittelbar entgegen statt sie aus heute
    // abzuleiten - der Jahresrueckblick vergleicht frei gewaehlte Jahre.
    [Fact]
    public void Year_umfasst_genau_das_angegebene_Kalenderjahr()
    {
        var range = DateRangePresets.Year(2023);

        Assert.Equal(new DateOnly(2023, 1, 1), range.From);
        Assert.Equal(new DateOnly(2024, 1, 1), range.ToExclusive);
    }

    // Bewusst Kalenderjahre und nicht rollierend: die Jahresspalten der
    // Auswertung sollen vollstaendig sein.
    [Fact]
    public void LastThreeYears_umfasst_drei_volle_Kalenderjahre_inklusive_des_laufenden()
    {
        var range = DateRangePresets.LastThreeYears(new DateOnly(2026, 7, 29));

        Assert.Equal(new DateOnly(2024, 1, 1), range.From);
        Assert.Equal(new DateOnly(2027, 1, 1), range.ToExclusive);
    }

    [Fact]
    public void LastTwelveMonths_schliesst_den_heutigen_Tag_ein()
    {
        var range = DateRangePresets.LastTwelveMonths(new DateOnly(2026, 7, 29));

        Assert.Equal(new DateOnly(2025, 7, 29), range.From);
        Assert.Equal(new DateOnly(2026, 7, 30), range.ToExclusive);
    }

    // AddMonths kuerzt auf den letzten Tag des Zielmonats, wenn es den
    // Ankertag dort nicht gibt - hier 29.02.2028 zurueck auf 29.02.2027,
    // das es nicht gibt, also 28.02.2027.
    [Fact]
    public void LastTwelveMonths_kuerzt_ueber_den_Schalttag_hinweg()
    {
        var range = DateRangePresets.LastTwelveMonths(new DateOnly(2028, 2, 29));

        Assert.Equal(new DateOnly(2027, 2, 28), range.From);
        Assert.Equal(new DateOnly(2028, 3, 1), range.ToExclusive);
    }

    [Fact]
    public void Everything_umspannt_den_gesamten_darstellbaren_Bereich()
    {
        var range = DateRangePresets.Everything();

        Assert.Equal(DateOnly.MinValue, range.From);
        Assert.Equal(DateOnly.MaxValue, range.ToExclusive);
    }

    // Das Bis-Datum der Filterleiste ist einschliessend gemeint, die
    // Filtersemantik ausschliessend - der eingegebene Tag muss also noch
    // dazugehoeren.
    [Fact]
    public void FromInclusiveBounds_verschiebt_das_Ende_um_einen_Tag()
    {
        var range = DateRangePresets.FromInclusiveBounds(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(new DateOnly(2026, 3, 1), range.From);
        Assert.Equal(new DateOnly(2026, 4, 1), range.ToExclusive);
    }

    [Fact]
    public void FromInclusiveBounds_laesst_einzelne_Grenzen_offen()
    {
        var ohneEnde = DateRangePresets.FromInclusiveBounds(new DateOnly(2026, 3, 1), null);
        var ohneAnfang = DateRangePresets.FromInclusiveBounds(null, new DateOnly(2026, 3, 31));

        Assert.Equal(new DateOnly(2026, 3, 1), ohneEnde.From);
        Assert.Equal(DateOnly.MaxValue, ohneEnde.ToExclusive);

        Assert.Equal(DateOnly.MinValue, ohneAnfang.From);
        Assert.Equal(new DateOnly(2026, 4, 1), ohneAnfang.ToExclusive);
    }

    [Fact]
    public void FromInclusiveBounds_ohne_Grenzen_entspricht_Everything()
    {
        Assert.Equal(DateRangePresets.Everything(), DateRangePresets.FromInclusiveBounds(null, null));
    }

    // AddDays wuerde am 31.12.9999 ueberlaufen.
    [Fact]
    public void FromInclusiveBounds_laeuft_am_hoechsten_Datum_nicht_ueber()
    {
        var range = DateRangePresets.FromInclusiveBounds(null, DateOnly.MaxValue);

        Assert.Equal(DateOnly.MaxValue, range.ToExclusive);
    }

    [Fact]
    public void IsEmpty_erkennt_ein_Bis_Datum_vor_dem_Von_Datum()
    {
        var verdreht = DateRangePresets.FromInclusiveBounds(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 3, 1));

        Assert.True(verdreht.IsEmpty);
    }

    [Fact]
    public void IsEmpty_ist_bei_einem_einzelnen_Tag_falsch()
    {
        var einTag = DateRangePresets.FromInclusiveBounds(
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));

        Assert.False(einTag.IsEmpty);
    }
}
