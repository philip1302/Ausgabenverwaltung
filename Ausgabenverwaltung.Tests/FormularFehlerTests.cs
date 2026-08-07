using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Ungueltige Eingaben in den Formularen - mit denselben ViewModels, die
/// die Ansicht bindet, aber ohne Fenster.
///
/// Zwei Dinge werden dabei geprueft: dass die Meldung am betroffenen Feld
/// erscheint, UND dass nichts gespeichert wird. Das zweite ist das
/// wichtigere - eine Fehlermeldung, hinter der trotzdem etwas in der
/// Datenbank landet, waere schlimmer als gar keine.
/// </summary>
public class FormularFehlerTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-formular-");

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    // Eigene Instanz statt WeakReferenceMessenger.Default (Regel 14): sonst
    // wuerden Registrierungen aus fruehen Tests dieser Klasse in spaetere
    // hineinwirken, weil der statische Standard prozessweit geteilt ist.
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    public FormularFehlerTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _personen.Create("Ich", isSelf: true);
        _kategorien.Create("Sonstiges", parentId: null);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _tempDir.Delete(recursive: true);
    }

    // Der Einstellungsspeicher zeigt in das Temp-Verzeichnis des Tests -
    // die Erfassungsmaske merkt sich darin die Serienerfassung, und die
    // echte Einstellungsdatei des Anwenders geht das nichts an.
    private ErfassenViewModel NeueErfassung()
        => new(_ausgaben, _kategorien, _personen,
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json")),
            _messenger);

    private int AnzahlAusgaben()
        => _ausgaben.GetRecent(1000).Count;

    // ================= Erfassungsmaske =================

    [Fact]
    public async Task Ein_ungueltiger_Betrag_wird_am_Feld_gemeldet_und_nichts_gespeichert()
    {
        var vm = NeueErfassung();
        vm.BetragText = "zwoelf fuffzig";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.BetragFehlerSichtbar);
        Assert.Equal(0, AnzahlAusgaben());

        // Und die Eingabe steht noch da.
        Assert.Equal("zwoelf fuffzig", vm.BetragText);
    }

    [Fact]
    public async Task Ein_Betrag_von_null_wird_abgelehnt()
    {
        var vm = NeueErfassung();
        vm.BetragText = "0,00";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Contains("null", vm.BetragFehler);
        Assert.Equal(0, AnzahlAusgaben());
    }

    [Fact]
    public async Task Eine_fehlende_Kategorie_wird_am_Feld_gemeldet()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = null;

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.KategorieFehlerSichtbar);
        Assert.Equal(0, AnzahlAusgaben());
    }

    [Fact]
    public async Task Ein_Tippfehler_im_Jahr_wird_am_Datumsfeld_gemeldet()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];
        vm.DatumText = "05.03.0200";

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.DatumFehlerSichtbar);
        Assert.Contains("200", vm.DatumFehler);
        Assert.Equal(0, AnzahlAusgaben());
    }

    [Fact]
    public async Task Alle_Beanstandungen_erscheinen_in_einem_Durchgang()
    {
        var vm = NeueErfassung();
        vm.BetragText = "keine Zahl";
        vm.AusgewaehlteKategorie = null;
        vm.DatumText = "kein Datum";

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.BetragFehlerSichtbar);
        Assert.True(vm.KategorieFehlerSichtbar);
        Assert.True(vm.DatumFehlerSichtbar);
    }

    // ================= Rueckfrage statt Verbot =================

    [Fact]
    public async Task Ein_weit_zurueckliegendes_Datum_fragt_nach_statt_zu_speichern()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];
        vm.DatumText = "05.03.2005";

        await vm.SpeichernCommand.ExecuteAsync(null);

        // Kein Fehler am Feld - das Datum ist ja in Ordnung.
        Assert.False(vm.DatumFehlerSichtbar);

        // Aber eine Rueckfrage, und noch nichts gespeichert.
        Assert.True(vm.DatumRueckfrageSichtbar);
        Assert.Equal(0, AnzahlAusgaben());
    }

    [Fact]
    public async Task Nach_der_Bestaetigung_wird_das_ungewoehnliche_Datum_gespeichert()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];
        vm.DatumText = "05.03.2005";

        await vm.SpeichernCommand.ExecuteAsync(null);
        vm.DatumBestaetigenCommand.Execute(null);
        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.False(vm.DatumRueckfrageSichtbar);
        Assert.Equal(1, AnzahlAusgaben());
        Assert.Equal(new DateOnly(2005, 3, 5), _ausgaben.GetRecent(1)[0].ExpenseDate);
    }

    [Fact]
    public async Task Eine_Aenderung_am_Datum_nimmt_die_Bestaetigung_zurueck()
    {
        // Sonst gaelte eine Bestaetigung fuer ein anderes Datum als das
        // gezeigte.
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];
        vm.DatumText = "05.03.2005";

        await vm.SpeichernCommand.ExecuteAsync(null);
        vm.DatumBestaetigenCommand.Execute(null);

        // Der Anwender tippt ein anderes, ebenfalls ungewoehnliches Datum.
        vm.DatumText = "05.03.2006";

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.DatumRueckfrageSichtbar);
        Assert.Equal(0, AnzahlAusgaben());
    }

    // ================= Schreibfehler =================

    [Fact]
    public async Task Ein_Schreibfehler_laesst_das_Formular_vollstaendig_stehen()
    {
        // Das ist die Zusage aus der Aufgabenstellung: nichts aergert mehr
        // als ein geleertes Formular nach einem Fehler.
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];
        vm.DatumText = "31.07.2026";
        vm.Bemerkung = "Wocheneinkauf";

        var kategorie = vm.AusgewaehlteKategorie;

        // Die Verbindung schliessen: jeder weitere Schreibversuch
        // scheitert - dasselbe Bild wie bei einem getrennten Laufwerk.
        _connection.Close();

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(vm.SpeicherFehlerSichtbar);

        // Kein einziges Feld ist geraeumt worden.
        Assert.Equal("12,50", vm.BetragText);
        Assert.Equal("31.07.2026", vm.DatumText);
        Assert.Equal("Wocheneinkauf", vm.Bemerkung);
        Assert.Same(kategorie, vm.AusgewaehlteKategorie);

        // Und die Bestaetigung "Gespeichert." erscheint nicht.
        Assert.False(vm.BestaetigungSichtbar);
    }

    [Fact]
    public async Task Die_Meldung_zum_Schreibfehler_sagt_was_mit_den_Daten_ist()
    {
        var vm = NeueErfassung();
        vm.BetragText = "12,50";
        vm.AusgewaehlteKategorie = vm.KategorieVorschlaege[0];

        _connection.Close();
        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Contains("zurückgenommen", vm.SpeicherFehlerText);
        Assert.Contains("Formular", vm.SpeicherFehlerText);

        // Keine technischen Begriffe.
        Assert.DoesNotContain("Exception", vm.SpeicherFehlerText);
        Assert.DoesNotContain("SQLite", vm.SpeicherFehlerText);
    }

    // ================= Kategorien =================

    [Fact]
    public void Ein_leerer_Kategoriename_wird_bemaengelt()
    {
        var vm = NeuesKategorienViewModel();
        vm.NeueOberkategorieCommand.Execute(null);

        var knoten = vm.AusgewaehlterKnoten!;
        knoten.BearbeitungsText = "   ";

        vm.BearbeitenUebernehmenCommand.Execute(knoten);

        Assert.NotNull(knoten.BearbeitungsFehler);
        Assert.True(knoten.WirdBearbeitet);
    }

    [Fact]
    public void Ein_doppelter_Kategoriename_wird_erklaert()
    {
        var vm = NeuesKategorienViewModel();
        vm.NeueOberkategorieCommand.Execute(null);

        var knoten = vm.AusgewaehlterKnoten!;
        knoten.BearbeitungsText = "Sonstiges";

        vm.BearbeitenUebernehmenCommand.Execute(knoten);

        Assert.Contains("bereits eine Kategorie", knoten.BearbeitungsFehler);

        // Der Name bleibt im Feld stehen, damit er nicht neu getippt
        // werden muss.
        Assert.Equal("Sonstiges", knoten.BearbeitungsText);
    }

    // ================= Personen =================

    [Fact]
    public void Ein_leerer_Personenname_wird_bemaengelt()
    {
        var vm = NeuesPersonenViewModel();
        vm.NeuePersonCommand.Execute(null);

        var zeile = vm.Personen[^1];
        zeile.BearbeitungsText = "  ";

        vm.BearbeitenUebernehmenCommand.Execute(zeile);

        Assert.NotNull(zeile.BearbeitungsFehler);
        Assert.True(zeile.WirdBearbeitet);
    }

    [Fact]
    public void Ein_doppelter_Personenname_wird_erklaert()
    {
        var vm = NeuesPersonenViewModel();
        vm.NeuePersonCommand.Execute(null);

        var zeile = vm.Personen[^1];
        zeile.BearbeitungsText = "Ich";

        vm.BearbeitenUebernehmenCommand.Execute(zeile);

        Assert.Contains("bereits eine Person", zeile.BearbeitungsFehler);
        Assert.Equal("Ich", zeile.BearbeitungsText);
    }

    // ================= Vorlagen =================

    [Fact]
    public void Ein_Enddatum_vor_dem_Startdatum_wird_am_Feld_gemeldet()
    {
        var formular = NeuesVorlagenformular();
        formular.Titel = "Miete";
        formular.BetragText = "800,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];
        formular.StartDatumText = "01.03.2026";
        formular.EndDatumText = "01.01.2026";

        var ergebnis = formular.Pruefe();

        Assert.False(ergebnis.IsValid);
        Assert.True(formular.EndDatumFehlerSichtbar);
        Assert.Contains("vor dem Startdatum", formular.EndDatumFehler);
    }

    [Fact]
    public void Ein_Ankertag_ohne_passenden_Rhythmus_wird_am_Feld_gemeldet()
    {
        var formular = NeuesVorlagenformular();
        formular.Titel = "Zeitung";
        formular.BetragText = "5,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];
        formular.AusgewaehlteIntervallEinheit =
            formular.IntervallOptionen.First(option => option.Wert == "week");
        formular.AnkertagText = "15";

        var ergebnis = formular.Pruefe();

        Assert.False(ergebnis.IsValid);
        Assert.True(formular.AnkertagFehlerSichtbar);
    }

    [Fact]
    public void Eine_Vorlage_ohne_Titel_wird_bemaengelt()
    {
        var formular = NeuesVorlagenformular();
        formular.Titel = "   ";
        formular.BetragText = "5,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];

        var ergebnis = formular.Pruefe();

        Assert.False(ergebnis.IsValid);
        Assert.True(formular.TitelFehlerSichtbar);
    }

    [Fact]
    public void Eine_Vorlage_ueber_null_Euro_wird_bemaengelt()
    {
        var formular = NeuesVorlagenformular();
        formular.Titel = "Nichts";
        formular.BetragText = "0,00";
        formular.AusgewaehlteKategorie = formular.KategorieVorschlaege[0];

        var ergebnis = formular.Pruefe();

        Assert.False(ergebnis.IsValid);
        Assert.Contains("null", formular.BetragFehler);
    }

    // ================= Hilfsmittel =================

    private KategorienViewModel NeuesKategorienViewModel()
        => new(_kategorien, NeueSicherung(), _messenger);

    private PersonenViewModel NeuesPersonenViewModel()
        => new(_personen, new OpenItemsRepository(_connection));

    private VorlageBearbeitenViewModel NeuesVorlagenformular()
        => new(
            vorlage: null,
            kategoriePfad: null,
            _kategorien.GetSelectableLeaves(),
            _personen.GetAllActive(),
            uebertragbareAnzahl: 0,
            heute: new DateOnly(2026, 7, 31));

    private BackupService NeueSicherung()
        => new(
            _connection,
            Path.Combine(_tempDir.FullName, "Backups"),
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json")));
}
