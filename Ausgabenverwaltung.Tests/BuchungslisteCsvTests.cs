using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der CSV-Export der Ausgabenliste
/// (<see cref="ReportCsv.BuildExpenseList"/>) - dasselbe Trennzeichen,
/// dieselbe Maskierung und dasselbe Zeilenende wie beim Export der
/// Kreuztabelle (siehe <see cref="ReportCsvTests"/>).
/// </summary>
public class BuchungslisteCsvTests
{
    private const string Kopf =
        "\"Datum\";\"Art\";\"Betrag\";\"Kategorie\";\"Zahler\";"
        + "\"Beglichen am\";\"Bemerkung\";\"Vorlage\"\r\n";

    private static ExpenseListItem Zeile(
        int id = 1,
        string datum = "2026-08-07",
        long cents = 4290,
        bool istEinnahme = false,
        string kategorie = "Haushalt › Lebensmittel",
        string zahler = "Ich",
        string? beglichen = null,
        string? bemerkung = "Rewe",
        string? vorlage = null) => new()
    {
        Id = id,
        ExpenseDate = DateOnly.Parse(datum),
        CategoryId = 1,
        CategoryFullPath = kategorie,
        AmountCents = cents,
        IsIncome = istEinnahme,
        PayerId = 1,
        PayerName = zahler,
        PayerIsSelf = zahler == "Ich",
        SettledDate = beglichen is null ? null : DateOnly.Parse(beglichen),
        Note = bemerkung,
        RecurringExpenseTitle = vorlage,
        RecurringExpenseId = vorlage is null ? null : 1,
    };

    [Fact]
    public void Export_bildet_Kopf_und_je_Buchung_eine_Zeile_ab()
    {
        var text = ReportCsv.BuildExpenseList([
            Zeile(),
            Zeile(id: 2, datum: "2026-08-05", cents: 120000, istEinnahme: true,
                  kategorie: "Gehalt", zahler: "Anna", beglichen: "2026-08-05",
                  bemerkung: null, vorlage: "Monatsgehalt"),
        ]);

        Assert.Equal(
            Kopf
            + "07.08.2026;\"Ausgabe\";-42,90;\"Haushalt › Lebensmittel\";\"Ich\";;\"Rewe\";\"\"\r\n"
            + "05.08.2026;\"Einnahme\";1200,00;\"Gehalt\";\"Anna\";05.08.2026;\"\";\"Monatsgehalt\"\r\n",
            text);
    }

    /// <summary>
    /// Das Vorzeichen kommt vom Buchungstyp und nicht vom gespeicherten
    /// Wert - so, wie eine einzelne Buchung ueberall in der Anwendung
    /// dargestellt wird (EuroText.FormatSigned).
    /// </summary>
    [Fact]
    public void Ausgaben_stehen_negativ_Einnahmen_positiv()
    {
        var text = ReportCsv.BuildExpenseList([
            Zeile(cents: 1000),
            Zeile(cents: 1000, istEinnahme: true),
        ]);

        Assert.Contains(";-10,00;", text);
        Assert.Contains(";10,00;", text);
    }

    /// <summary>
    /// Auch eine noch OFFENE Einnahme steht mit ihrem Betrag da. Die Summe
    /// unter der Liste zaehlt sie mit null, weil das Geld noch nicht
    /// geflossen ist - eine Zeile, die ihren eigenen Betrag verschweigt,
    /// waere im Export aber wertlos. Welche offen sind, sagt die Spalte
    /// "Beglichen am".
    /// </summary>
    [Fact]
    public void Eine_offene_Einnahme_behaelt_im_Export_ihren_Betrag()
    {
        var text = ReportCsv.BuildExpenseList([
            Zeile(cents: 34000, istEinnahme: true, zahler: "Anna", beglichen: null),
        ]);

        Assert.Contains(";340,00;", text);

        // Leeres Feld statt "—": Excel liest einen Bindestrich als Text.
        Assert.Contains("\"Anna\";;", text);
    }

    /// <summary>
    /// Alt-Datensaetze aus der Zeit vor Regel 1 koennen einen negativen
    /// Betrag tragen. Sie werden nach ihrem heutigen Typ formatiert, ohne
    /// Sonderbehandlung - sonst stuende im Export ein doppeltes Minus.
    /// </summary>
    [Fact]
    public void Ein_negativ_gespeicherter_Altbestand_bekommt_kein_doppeltes_Vorzeichen()
    {
        var text = ReportCsv.BuildExpenseList([Zeile(cents: -1000)]);

        Assert.Contains(";-10,00;", text);
        Assert.DoesNotContain("--", text);
    }

    /// <summary>
    /// Kein €-Zeichen und kein Tausenderpunkt (EuroText.Plain): beides
    /// machte die Spalte in Excel zu Text, und dann rechnet dort nichts
    /// mehr.
    /// </summary>
    [Fact]
    public void Betraege_haben_kein_Eurozeichen_und_keinen_Tausenderpunkt()
    {
        var text = ReportCsv.BuildExpenseList([Zeile(cents: 123456)]);

        Assert.Contains(";-1234,56;", text);
        Assert.DoesNotContain("€", text);
    }

    /// <summary>
    /// Ein Semikolon in der Bemerkung zerlegte sonst die Zeile, ein
    /// Anfuehrungszeichen die Maskierung.
    /// </summary>
    [Fact]
    public void Trenn_und_Anfuehrungszeichen_im_Text_werden_maskiert()
    {
        var text = ReportCsv.BuildExpenseList([
            Zeile(bemerkung: "Rewe; „groß“ und \"gross\""),
        ]);

        Assert.Contains("\"Rewe; „groß“ und \"\"gross\"\"\"", text);
    }

    /// <summary>
    /// Datum als deutsches Datum und unquotiert, damit Excel eine
    /// Datumsspalte daraus macht statt einer Textspalte.
    /// </summary>
    [Fact]
    public void Datumsangaben_stehen_deutsch_und_ohne_Anfuehrungszeichen()
    {
        var text = ReportCsv.BuildExpenseList([
            Zeile(datum: "2026-01-09", beglichen: "2026-12-31"),
        ]);

        Assert.Contains("09.01.2026;", text);
        Assert.Contains(";31.12.2026;", text);
        Assert.DoesNotContain("\"09.01.2026\"", text);
    }

    /// <summary>
    /// Eine leere Liste liefert die Kopfzeile und nichts weiter. Eine ganz
    /// leere Datei liesse offen, ob der Export ueberhaupt gelaufen ist.
    /// </summary>
    [Fact]
    public void Eine_leere_Liste_liefert_nur_die_Kopfzeile()
        => Assert.Equal(Kopf, ReportCsv.BuildExpenseList([]));
}
