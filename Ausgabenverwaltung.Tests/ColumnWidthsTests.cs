using Ausgabenverwaltung.Core.Display;

namespace Ausgabenverwaltung.Tests;

public class ColumnWidthsTests
{
    [Fact]
    public void Eine_Breite_im_erlaubten_Bereich_bleibt_stehen()
    {
        Assert.Equal(300, ColumnWidths.NormalizeCategory(300));
        Assert.Equal(ColumnWidths.CategoryMin, ColumnWidths.NormalizeCategory(ColumnWidths.CategoryMin));
        Assert.Equal(ColumnWidths.CategoryMax, ColumnWidths.NormalizeCategory(ColumnWidths.CategoryMax));
    }

    [Fact]
    public void Zu_schmal_und_zu_breit_landen_an_der_Grenze()
    {
        // Weit ueber den Fensterrand hinausgezogen oder von Hand in die
        // Einstellungsdatei geschrieben - in beiden Faellen bleibt eine
        // Spalte uebrig, die sich wieder zurueckziehen laesst.
        Assert.Equal(ColumnWidths.CategoryMin, ColumnWidths.NormalizeCategory(10));
        Assert.Equal(ColumnWidths.CategoryMax, ColumnWidths.NormalizeCategory(5000));
    }

    [Fact]
    public void Unbrauchbare_Werte_landen_bei_der_Vorgabe()
    {
        // 0 steht fuer "stand nicht in der Datei" ...
        Assert.Equal(ColumnWidths.CategoryDefault, ColumnWidths.NormalizeCategory(0));
        Assert.Equal(ColumnWidths.CategoryDefault, ColumnWidths.NormalizeCategory(-40));
        Assert.Equal(ColumnWidths.CategoryDefault, ColumnWidths.NormalizeCategory(double.NaN));
        Assert.Equal(ColumnWidths.CategoryDefault, ColumnWidths.NormalizeCategory(double.PositiveInfinity));
    }

    [Fact]
    public void Gemerkt_werden_ganze_Pixel()
    {
        // Beim Ziehen entstehen krumme Werte (die Bewegung wird durch den
        // Schriftfaktor geteilt) - in der Einstellungsdatei hat das nichts
        // zu suchen.
        Assert.Equal(261, ColumnWidths.NormalizeCategory(260.6));
    }

    [Fact]
    public void Die_Vorgabe_liegt_im_erlaubten_Bereich()
    {
        Assert.InRange(ColumnWidths.CategoryDefault, ColumnWidths.CategoryMin, ColumnWidths.CategoryMax);
    }
}
