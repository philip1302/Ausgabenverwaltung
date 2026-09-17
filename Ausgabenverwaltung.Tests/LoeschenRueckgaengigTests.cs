using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Loeschen in der Ausgabenliste wirkt sofort und ohne Nachfrage - dafuer
/// laesst es sich zurueckholen, solange der Bereich nicht gewechselt wurde.
///
/// Zwei Dinge stehen hier im Vordergrund: dass wirklich JEDER Wert der
/// Buchung zurueckkommt (nicht nur Betrag und Datum), und dass der Vorrat,
/// aus dem wiederhergestellt wird, an den richtigen Stellen wieder
/// verschwindet - ein Band, das ein Angebot macht, das es nicht mehr
/// einloesen kann, waere schlimmer als gar keines.
/// </summary>
public class LoeschenRueckgaengigTests : IDisposable
{
    private readonly IDbConnection _connection;

    private readonly CategoryRepository _kategorien;
    private readonly PersonRepository _personen;
    private readonly ExpenseRepository _ausgaben;

    private readonly int _wohnenId;
    private readonly int _ichId;
    private readonly int _annaId;

    public LoeschenRueckgaengigTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);

        _kategorien = new CategoryRepository(_connection);
        _personen = new PersonRepository(_connection);
        _ausgaben = new ExpenseRepository(_connection);

        _wohnenId = _kategorien.Create("Wohnen", parentId: null).Id;
        _ichId = _personen.Create("Ich", isSelf: true).Id;
        _annaId = _personen.Create("Anna", isSelf: false).Id;
    }

    // Eigene Einstellungsdatei: die Liste merkt sich ihre Sortierung, und
    // das darf nicht in der settings.json des Rechners landen.
    private readonly TestEinstellungen _einstellungen = new();

    public void Dispose()
    {
        _connection.Dispose();
        _einstellungen.Dispose();
    }

    private AusgabenlisteViewModel NeueListe() =>
        new(_ausgaben, _kategorien, _personen, _einstellungen.Store,
            new WeakReferenceMessenger(), new ToastViewModel());

    // Der Vorgabezeitraum der Liste ist das laufende Jahr - die
    // Testbuchungen liegen deshalb auf heute, damit sie in der Liste
    // auftauchen und sich markieren lassen.
    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    [Fact]
    public void Loeschen_wirkt_sofort_und_bietet_Rueckgaengig_an()
    {
        var buchung = _ausgaben.Create(_wohnenId, 4290, Heute, _ichId);

        var vm = NeueListe();
        var zeile = vm.Zeilen.Single(z => z.Id == buchung.Id);

        vm.LoeschenCommand.Execute(zeile);

        // Weg ist weg - es gibt keine Zwischenstufe mehr, in der nur
        // gefragt wird.
        Assert.Null(_ausgaben.GetById(buchung.Id));
        Assert.True(vm.RueckgaengigSichtbar);
        Assert.Contains("gelöscht", vm.RueckgaengigText!);
    }

    [Fact]
    public void Rueckgaengig_legt_die_Buchung_mit_allen_Werten_wieder_an()
    {
        var buchung = _ausgaben.Create(
            _wohnenId,
            amountCents: 12345,
            expenseDate: Heute.AddDays(-3),
            payerId: _annaId,
            note: "Wocheneinkauf",
            settledDate: Heute.AddDays(-1),
            recurringExpenseId: null,
            isIncome: true);

        // Aus der Datenbank gelesen und nicht aus dem Rueckgabewert von
        // Create: gespeichert wird sekundengenau (Regel 3), im Speicher
        // steht der Zeitstempel mit Bruchteilen.
        var erfasstUtc = _ausgaben.GetById(buchung.Id)!.CreatedUtc;

        var vm = NeueListe();
        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == buchung.Id));

        vm.RueckgaengigCommand.Execute(null);

        // Die Id ist eine neue - alles uebrige steht wieder so da wie
        // vorher, einschliesslich CreatedUtc.
        var wiederhergestellteId = Assert.Single(vm.Zeilen).Id;
        Assert.NotEqual(buchung.Id, wiederhergestellteId);

        var zurueck = _ausgaben.GetById(wiederhergestellteId)!;

        Assert.Equal(_wohnenId, zurueck.CategoryId);
        Assert.Equal(12345, zurueck.AmountCents);
        Assert.Equal(Heute.AddDays(-3), zurueck.ExpenseDate);
        Assert.Equal(_annaId, zurueck.PayerId);
        Assert.Equal("Wocheneinkauf", zurueck.Note);
        Assert.Equal(Heute.AddDays(-1), zurueck.SettledDate);
        Assert.True(zurueck.IsIncome);
        Assert.Null(zurueck.RecurringExpenseId);
        Assert.Equal(erfasstUtc, zurueck.CreatedUtc);

        Assert.False(vm.RueckgaengigSichtbar);
        Assert.Contains("wiederhergestellt", vm.ErfolgText!);
    }

    [Fact]
    public void Rueckgaengig_holt_eine_ganze_Sammelloeschung_zurueck()
    {
        _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);
        _ausgaben.Create(_wohnenId, 2000, Heute, _ichId);
        var kontrolle = _ausgaben.Create(_wohnenId, 3000, Heute, _ichId);

        var vm = NeueListe();
        foreach (var zeile in vm.Zeilen.Where(z => z.Id != kontrolle.Id))
        {
            zeile.IstAusgewaehlt = true;
        }

        vm.AusgewaehlteLoeschenCommand.Execute(null);

        Assert.Single(vm.Zeilen);
        Assert.Contains("2 Buchungen gelöscht", vm.RueckgaengigText!);

        vm.RueckgaengigCommand.Execute(null);

        Assert.Equal(3, vm.Zeilen.Count);
        Assert.Equal(6000, vm.Zeilen.Sum(z => z.AmountCents));
    }

    [Fact]
    public void Rueckgaengig_behaelt_den_Vorlagenbezug()
    {
        // Der Fremdschluessel steht auf ON DELETE SET NULL - eine
        // wiederhergestellte Buchung muss ihre Herkunft trotzdem
        // zurueckbekommen, sonst faellt sie stillschweigend aus jedem
        // Vorlagenfilter heraus.
        var vorlagen = new Core.RecurringExpenses.RecurringExpenseRepository(_connection);
        var vorlage = vorlagen.Create(
            _wohnenId, _ichId, 5000, "Miete", "month", 1, 1, Heute, null, null, false);

        var ausVorlage = _ausgaben.Create(
            _wohnenId, 5000, Heute, _ichId, recurringExpenseId: vorlage.Id);

        var vm = NeueListe();
        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == ausVorlage.Id));
        vm.RueckgaengigCommand.Execute(null);

        var zurueck = Assert.Single(vm.Zeilen);
        Assert.True(zurueck.IstAusVorlage);
        Assert.Equal(vorlage.Id, zurueck.RecurringExpenseId);
    }

    [Fact]
    public void Schliessen_laesst_die_Loeschung_stehen_und_raeumt_das_Band()
    {
        var buchung = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == buchung.Id));

        vm.RueckgaengigSchliessenCommand.Execute(null);

        Assert.False(vm.RueckgaengigSichtbar);
        Assert.Null(_ausgaben.GetById(buchung.Id));

        // Der Vorrat ist mit dem Band weg: ein zweiter Druck holt nichts
        // mehr zurueck.
        vm.RueckgaengigCommand.Execute(null);
        Assert.Empty(vm.Zeilen);
    }

    [Fact]
    public void Der_Bereichswechsel_beendet_das_Angebot()
    {
        var buchung = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);

        var vm = NeueListe();
        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == buchung.Id));
        Assert.True(vm.RueckgaengigSichtbar);

        // Genau das ruft der MainViewModel beim Wechsel in den Bereich.
        vm.AktualisiereListe();

        Assert.False(vm.RueckgaengigSichtbar);

        vm.RueckgaengigCommand.Execute(null);
        Assert.Empty(vm.Zeilen);
    }

    [Fact]
    public void Das_Loeschen_der_einzeln_gefilterten_Buchung_hebt_den_Filter_auf()
    {
        var buchung = _ausgaben.Create(_wohnenId, 1000, Heute, _ichId);
        var andere = _ausgaben.Create(_wohnenId, 2000, Heute, _ichId);

        var vm = NeueListe();
        vm.ZeigeEinzelneBuchung(buchung.Id, "Testbuchung");
        Assert.True(vm.BuchungFilterAktiv);

        vm.LoeschenCommand.Execute(vm.Zeilen.Single(z => z.Id == buchung.Id));

        // Sonst zeigte die Liste dauerhaft nichts mehr an, und der Grund
        // dafuer waere nicht mehr da.
        Assert.False(vm.BuchungFilterAktiv);
        Assert.Contains(vm.Zeilen, z => z.Id == andere.Id);
    }
}
