using Ausgabenverwaltung.Core.Categories;

namespace Ausgabenverwaltung.Tests;

public class CategoryPathShorteningTests
{
    // In der Anwendung entscheidet die Schriftbreite, ob ein Vorschlag
    // passt. Hier steht dafuer eine Hoechstzahl von Zeichen: die Auswahl
    // der Ebenen ist dieselbe, und der Test bleibt ohne laufende
    // Oberflaeche pruefbar.
    private static Func<string, bool> BisZu(int zeichen) =>
        vorschlag => vorschlag.Length <= zeichen;

    private const string Voll = "Pferde › Gesundheit › Medizin › Impfungen";

    [Fact]
    public void Was_ganz_passt_bleibt_unveraendert()
    {
        Assert.Equal(Voll, CategoryPathShortening.Shorten(Voll, BisZu(100)));
    }

    [Fact]
    public void Gekuerzt_wird_vorne_und_nur_so_weit_wie_noetig()
    {
        // "… › Gesundheit › Medizin › Impfungen" ist 36 Zeichen lang und
        // passt gerade noch - eine Ebene weniger reicht also.
        Assert.Equal(
            "… › Gesundheit › Medizin › Impfungen",
            CategoryPathShortening.Shorten(Voll, BisZu(36)));

        Assert.Equal(
            "… › Medizin › Impfungen",
            CategoryPathShortening.Shorten(Voll, BisZu(30)));
    }

    [Fact]
    public void Die_Kategorie_selbst_bleibt_stehen()
    {
        // Auch wenn nur noch wenig Platz ist, faellt der Name der
        // Kategorie nicht weg - er ist die Angabe, die zaehlt.
        var gekuerzt = CategoryPathShortening.Shorten(Voll, BisZu(15));

        Assert.Equal("… › Impfungen", gekuerzt);
        Assert.EndsWith("Impfungen", gekuerzt);
    }

    [Fact]
    public void Selbst_bei_hoffnungslos_wenig_Platz_kommt_der_Name_zurueck()
    {
        // Nichts passt mehr. Zurueck kommt trotzdem der letzte Vorschlag -
        // die Zelle schneidet ihn dann hinten ab, statt leer zu bleiben.
        Assert.Equal("… › Impfungen", CategoryPathShortening.Shorten(Voll, BisZu(2)));
    }

    [Fact]
    public void Eine_Kategorie_ohne_Oberkategorie_wird_nicht_angetastet()
    {
        // Vorne ist hier nichts wegzulassen; das Kuerzen uebernimmt die
        // Zelle am Ende des Namens.
        Assert.Equal("Lebensmittel", CategoryPathShortening.Shorten("Lebensmittel", BisZu(4)));
    }

    [Fact]
    public void Leerer_Pfad_bleibt_leer()
    {
        Assert.Equal(string.Empty, CategoryPathShortening.Shorten(string.Empty, BisZu(0)));
    }

    [Fact]
    public void Die_Kuerzung_ist_am_Ergebnis_erkennbar()
    {
        // Genau daran haengt der Hilfetext: gleich = nichts weggelassen =
        // kein Hilfetext.
        Assert.Equal(Voll, CategoryPathShortening.Shorten(Voll, BisZu(100)));
        Assert.NotEqual(Voll, CategoryPathShortening.Shorten(Voll, BisZu(30)));
    }

    [Fact]
    public void Das_Trennzeichen_ist_dasselbe_wie_im_vollen_Pfad()
    {
        // Wird CategoryPaths.Separator geaendert, muss die Kuerzung
        // mitziehen - sonst zerfaellt der Pfad in zwei Schreibweisen.
        Assert.StartsWith(CategoryPathShortening.Ellipsis, CategoryPathShortening.Prefix);
        Assert.EndsWith(CategoryPaths.Separator, CategoryPathShortening.Prefix);
    }
}
