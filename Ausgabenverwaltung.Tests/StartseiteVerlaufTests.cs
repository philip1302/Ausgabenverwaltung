using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die kleinen Verlaufslinien in den Kacheln der Startseite.
///
/// Gebucht wird relativ zum heutigen Tag: die Startseite liest ihn aus
/// der Systemuhr, weil sie immer "diesen Monat" meint. Ein fester
/// Stichtag waere hier also kein Gewinn, sondern nur ein Test, der zum
/// Monatswechsel etwas anderes misst als die Anwendung.
/// </summary>
public class StartseiteVerlaufTests : IDisposable
{
    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Now);
    private static readonly DateOnly DieserMonat = new(Heute.Year, Heute.Month, 1);

    private readonly System.Data.IDbConnection _connection;
    private readonly ExpenseRepository _expenses;
    private readonly int _selfId;
    private readonly int _otherId;
    private readonly int _kategorieId;

    public StartseiteVerlaufTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _expenses = new ExpenseRepository(_connection);

        var people = new PersonRepository(_connection);
        _selfId = people.Create("Ich", isSelf: true).Id;
        _otherId = people.Create("Mitbewohner").Id;

        _kategorieId = new CategoryRepository(_connection).Create("Wohnen", null).Id;
    }

    public void Dispose() => _connection.Dispose();

    // Eigene Messenger-Instanz je Seite (Regel 14).
    private StartseiteViewModel NeueSeite() => new(
        _expenses,
        new OpenItemsRepository(_connection),
        new RecurringExpenseRepository(_connection),
        new ReportRepository(_connection),
        new WeakReferenceMessenger());

    /// <summary>Eine Ausgabe im Monat <paramref name="vorMonaten"/> zurueck.</summary>
    private void Buche(long cents, int vorMonaten) =>
        _expenses.Create(_kategorieId, cents, DieserMonat.AddMonths(-vorMonaten), _selfId);

    // Eine Einnahme zaehlt erst, wenn sie beglichen ist (Regel 4).
    private void BucheEinnahme(long cents, int vorMonaten)
    {
        var datum = DieserMonat.AddMonths(-vorMonaten);
        _expenses.Create(_kategorieId, cents, datum, _otherId, settledDate: datum, isIncome: true);
    }

    private static StartseiteViewModel MitFlaeche(StartseiteViewModel seite)
    {
        seite.VerlaufflaecheGeaendert(160, 28);
        return seite;
    }

    // ---------------- Wann die Linie ueberhaupt erscheint ----------------

    [Fact]
    public void Ohne_jede_Buchung_bleibt_die_Linie_weg()
    {
        var seite = MitFlaeche(NeueSeite());

        Assert.False(seite.AusgabenVerlaufVorhanden);
        Assert.Empty(seite.AusgabenVerlauf);
    }

    /// <summary>
    /// Ein einziger Monat ergaebe eine Linie, die elf Monate auf null
    /// liegt und dann hochschnellt - das sieht nach einem Ausbruch aus und
    /// ist doch nur "hier faengt es an".
    /// </summary>
    [Fact]
    public void Ein_einzelner_Monat_ergibt_noch_keine_Linie()
    {
        Buche(50000, vorMonaten: 0);

        var seite = MitFlaeche(NeueSeite());

        Assert.False(seite.AusgabenVerlaufVorhanden);
    }

    [Fact]
    public void Ab_zwei_Monaten_mit_Buchungen_steht_die_Linie()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = MitFlaeche(NeueSeite());

        Assert.True(seite.AusgabenVerlaufVorhanden);
        Assert.NotEmpty(seite.AusgabenVerlauf);
    }

    /// <summary>
    /// Ohne gemeldete Flaeche gibt es keine Punkte - die Ansicht meldet
    /// ihre Groesse erst beim ersten Anzeigen.
    /// </summary>
    [Fact]
    public void Ohne_gemeldete_Flaeche_gibt_es_keine_Punkte()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = NeueSeite();

        Assert.Empty(seite.AusgabenVerlauf);
    }

    /// <summary>
    /// Der Fall, an dem die Sparkline schon einmal vollstaendig
    /// verschwunden ist, und der Grund, warum es zwei Merkmale gibt:
    ///
    /// Die Sichtbarkeit haengt in der Ansicht an DIESEM Merkmal, und die
    /// Flaeche meldet ihre Groesse ueber SizeChanged. Ein unsichtbares
    /// Element wird aber gar nicht erst vermessen - haengt das Merkmal am
    /// Ergebnis der Groessenrechnung, wird es nie wahr, die Flaeche meldet
    /// nie, und die Linie kann nie erscheinen.
    ///
    /// Deshalb: "gibt es etwas zu zeigen" muss OHNE jede gemeldete Groesse
    /// wahr sein. Ein Test, der die Groesse vorher von Hand meldet, prueft
    /// den Zustand und nicht den Weg dorthin - und faellt genau darauf
    /// herein.
    /// </summary>
    [Fact]
    public void Das_Merkmal_gilt_schon_bevor_die_Flaeche_ihre_Groesse_meldet()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = NeueSeite();

        Assert.True(
            seite.AusgabenVerlaufVorhanden,
            "Ohne dieses Merkmal bleibt die Flaeche unsichtbar und meldet nie ihre Groesse.");
    }

    /// <summary>
    /// Und die Gegenprobe: sobald die Groesse da ist, fuellen sich die
    /// Punkte nach - ohne dass etwas anderes angestossen werden muesste.
    /// </summary>
    [Fact]
    public void Nach_der_Groessenmeldung_fuellen_sich_die_Punkte_nach()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = NeueSeite();
        Assert.Empty(seite.AusgabenVerlauf);

        seite.VerlaufflaecheGeaendert(160, 28);

        Assert.Equal(12, seite.AusgabenVerlauf.Count);
        Assert.True(seite.AusgabenVerlaufVorhanden);
    }

    /// <summary>
    /// Umgekehrt darf eine gemeldete Groesse aus fehlenden Daten keine
    /// Linie machen.
    /// </summary>
    [Fact]
    public void Eine_gemeldete_Groesse_erzeugt_ohne_Daten_keine_Linie()
    {
        Buche(50000, vorMonaten: 0);

        var seite = MitFlaeche(NeueSeite());

        Assert.False(seite.AusgabenVerlaufVorhanden);
        Assert.Empty(seite.AusgabenVerlauf);
    }

    // ---------------- Die Reihe steht fest auf zwoelf Monaten ----------------

    [Fact]
    public void Die_Linie_traegt_immer_zwoelf_Monate()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 5);

        var seite = MitFlaeche(NeueSeite());

        Assert.Equal(12, seite.AusgabenVerlauf.Count);
    }

    /// <summary>
    /// Der wichtigste Fall: das Diagramm darunter laesst sich auf 6 oder
    /// 24 Monate umstellen. Die Kachel darf das nicht mitmachen - ihr
    /// Verlauf wechselte sonst beim Umschalten seine Bedeutung.
    /// </summary>
    [Fact]
    public void Der_Umschalter_des_Diagramms_laesst_die_Linie_unberuehrt()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 5);

        var seite = MitFlaeche(NeueSeite());

        seite.ZeitraumWaehlenCommand.Execute("24");
        Assert.Equal(12, seite.AusgabenVerlauf.Count);

        seite.ZeitraumWaehlenCommand.Execute("6");
        Assert.Equal(12, seite.AusgabenVerlauf.Count);
    }

    // ---------------- Die beiden Kacheln sind getrennt ----------------

    [Fact]
    public void Einnahmen_und_Ausgaben_haben_getrennte_Linien()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = MitFlaeche(NeueSeite());

        Assert.True(seite.AusgabenVerlaufVorhanden);
        Assert.False(seite.EinnahmenVerlaufVorhanden);
    }

    [Fact]
    public void Die_Einnahmenlinie_steht_bei_zwei_beglichenen_Monaten()
    {
        BucheEinnahme(80000, vorMonaten: 0);
        BucheEinnahme(60000, vorMonaten: 2);

        var seite = MitFlaeche(NeueSeite());

        Assert.True(seite.EinnahmenVerlaufVorhanden);
        Assert.Equal(12, seite.EinnahmenVerlauf.Count);
    }

    // ---------------- Die Form stimmt ----------------

    /// <summary>
    /// Die Linie nutzt die gemeldete Breite ganz aus - sonst endete sie
    /// mitten in der Kachel.
    /// </summary>
    [Fact]
    public void Die_Linie_spannt_sich_ueber_die_ganze_Breite()
    {
        Buche(50000, vorMonaten: 0);
        Buche(30000, vorMonaten: 1);

        var seite = MitFlaeche(NeueSeite());

        Assert.Equal(0, seite.AusgabenVerlauf[0].X, precision: 6);
        Assert.Equal(160, seite.AusgabenVerlauf[^1].X, precision: 6);
    }

    /// <summary>
    /// Der teurere Monat liegt weiter OBEN, hat also ein kleineres Y -
    /// der Ursprung liegt oben links.
    /// </summary>
    [Fact]
    public void Der_teurere_Monat_liegt_weiter_oben()
    {
        Buche(90000, vorMonaten: 0);
        Buche(10000, vorMonaten: 1);

        var seite = MitFlaeche(NeueSeite());

        var vormonat = seite.AusgabenVerlauf[^2];
        var dieserMonat = seite.AusgabenVerlauf[^1];

        Assert.True(dieserMonat.Y < vormonat.Y);
    }
}
