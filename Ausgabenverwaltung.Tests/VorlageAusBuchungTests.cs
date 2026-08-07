using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Dapper;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// "Das kommt jeden Monat" - aus einer bestehenden Buchung eine Vorlage
/// machen. Geprueft wird der ganze Weg durch die ViewModels, weil genau
/// dort die Falle sitzt: die Vorlage darf die Buchung, aus der sie
/// entstanden ist, nicht gleich ein zweites Mal anlegen.
/// </summary>
public class VorlageAusBuchungTests : IDisposable
{
    private readonly IDbConnection _connection;
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-vorlage-aus-buchung-");

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;
    private readonly RecurringExpenseRepository _vorlagen;

    private readonly int _kategorieId;
    private readonly int _personId;

    public VorlageAusBuchungTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);
        _vorlagen = new RecurringExpenseRepository(_connection);

        var wohnen = _kategorien.Create("Wohnen", parentId: null);
        _kategorieId = _kategorien.Create("Miete", wohnen.Id).Id;
        _personId = _personen.Create("Ich", isSelf: true).Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _tempDir.Delete(recursive: true);
    }

    private VorlagenViewModel NeuesViewModel() => new(
        _vorlagen,
        _ausgaben,
        _kategorien,
        _personen,
        new RecurringExpenseScheduler(_vorlagen, DateOnly.FromDateTime(DateTime.Now)),
        new BackupService(
            _connection,
            Path.Combine(_tempDir.FullName, "Backups"),
            new AppSettingsStore(Path.Combine(_tempDir.FullName, "settings.json"))),
        new WeakReferenceMessenger());

    private int AnzahlBuchungen() => _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Expense");

    [Fact]
    public void Das_Formular_ist_aus_der_Buchung_vorbelegt()
    {
        var buchung = _ausgaben.Create(
            _kategorieId, 84500, new DateOnly(2026, 5, 15), _personId, note: "Kaltmiete");

        var vm = NeuesViewModel();
        vm.NeueVorlageAus(buchung.Id);

        var formular = Assert.IsType<VorlageBearbeitenViewModel>(vm.Bearbeiten);

        Assert.Equal("Kaltmiete", formular.Titel);
        Assert.Equal("845,00", formular.BetragText);
        Assert.Equal(_kategorieId, formular.AusgewaehlteKategorie!.Id);
        Assert.Equal(_personId, formular.AusgewaehlterZahler!.Id);
        Assert.Equal("Kaltmiete", formular.Bemerkung);
        Assert.False(formular.IstEinnahme);

        Assert.Equal("month", formular.AusgewaehlteIntervallEinheit.Wert);
        Assert.Equal("1", formular.IntervallAnzahlText);
        Assert.Equal("15", formular.AnkertagText);
        Assert.Equal("15.05.2026", formular.StartDatumText);
    }

    [Fact]
    public void Das_Formular_legt_an_und_aendert_nicht()
    {
        // Der Entwurf traegt die Werte einer Buchung, ist aber selbst keine
        // gespeicherte Vorlage - sonst wuerde das Speichern eine fremde Id
        // ueberschreiben.
        var buchung = _ausgaben.Create(_kategorieId, 1000, new DateOnly(2026, 5, 15), _personId);

        var vm = NeuesViewModel();
        vm.NeueVorlageAus(buchung.Id);

        Assert.False(vm.Bearbeiten!.IstBestehend);
        Assert.Null(vm.Bearbeiten.VorlageId);
        Assert.Equal("Neue Vorlage", vm.Bearbeiten.Titelzeile);

        // Und kein Angebot, etwas zu uebertragen: es gibt noch keine
        // erzeugten Buchungen.
        Assert.False(vm.Bearbeiten.UebertragungMoeglich);
    }

    [Fact]
    public void Ohne_Bemerkung_wird_der_Kategoriename_zum_Titel()
    {
        var buchung = _ausgaben.Create(_kategorieId, 1000, new DateOnly(2026, 5, 15), _personId);

        var vm = NeuesViewModel();
        vm.NeueVorlageAus(buchung.Id);

        // Die letzte Stufe des Pfades, nicht "Wohnen › Miete".
        Assert.Equal("Miete", vm.Bearbeiten!.Titel);
    }

    [Fact]
    public void Eine_Einnahme_bleibt_eine_Einnahme()
    {
        var anna = _personen.Create("Anna", isSelf: false);
        var buchung = _ausgaben.Create(
            _kategorieId, 30000, new DateOnly(2026, 5, 15), anna.Id, isIncome: true);

        var vm = NeuesViewModel();
        vm.NeueVorlageAus(buchung.Id);

        Assert.True(vm.Bearbeiten!.IstEinnahme);
        Assert.Equal(anna.Id, vm.Bearbeiten.AusgewaehlterZahler!.Id);
    }

    [Fact]
    public void Das_Speichern_legt_die_Vorlage_an_ohne_die_Buchung_zu_verdoppeln()
    {
        // Startdatum in der Vergangenheit, damit ein Erzeugungslauf
        // ueberhaupt etwas zu tun haette - genau hier entstuende sonst das
        // Duplikat.
        var buchungsdatum = DateOnly.FromDateTime(DateTime.Now).AddDays(-3);
        var buchung = _ausgaben.Create(
            _kategorieId, 84500, buchungsdatum, _personId, note: "Kaltmiete");

        var vm = NeuesViewModel();
        vm.NeueVorlageAus(buchung.Id);
        vm.BearbeitenSpeichernCommand.Execute(null);

        Assert.Null(vm.Bearbeiten);

        var vorlage = Assert.Single(_vorlagen.GetAll());
        Assert.Equal("Kaltmiete", vorlage.Title);
        Assert.Equal(84500, vorlage.AmountCents);
        Assert.Equal(buchungsdatum, vorlage.StartDate);

        // Der entscheidende Punkt: die Buchung von vorhin ist die einzige
        // geblieben. GeneratedThrough startete auf ihrem Datum, der
        // Erzeugungslauf direkt nach dem Speichern fand deshalb nichts
        // Faelliges mehr und hat den Wert nur noch auf heute
        // fortgeschrieben (siehe GenerateForTemplate).
        Assert.Equal(1, AnzahlBuchungen());
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), vorlage.GeneratedThrough);
    }

    [Fact]
    public void Eine_zwischenzeitlich_geloeschte_Buchung_wird_erklaert()
    {
        var vm = NeuesViewModel();

        vm.NeueVorlageAus(4711);

        Assert.Null(vm.Bearbeiten);
        Assert.True(vm.SchreibFehlerSichtbar);
        Assert.Contains("gibt es nicht mehr", vm.SchreibFehlerText);
    }
}
