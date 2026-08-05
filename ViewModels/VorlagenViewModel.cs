using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Verwaltungsbereich "Wiederkehrende Ausgaben": Vorlagen anlegen, aendern,
/// stilllegen und loeschen.
///
/// Gerechnet wird nichts hier: Rhythmustext, naechste Faelligkeit,
/// Monatsbelastung, Pruefung und Erzeugung stecken in Core
/// (<see cref="RecurrenceText"/>, <see cref="RecurrenceGenerator"/>,
/// <see cref="MonthlyBurden"/>, <see cref="RecurringExpenseValidator"/>,
/// <see cref="RecurringExpenseRepository"/>) - Regel 7.
/// </summary>
public sealed partial class VorlagenViewModel : ViewModelBase
{
    private readonly RecurringExpenseRepository _recurringExpenseRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly PersonRepository _personRepository;
    private readonly RecurringExpenseScheduler _scheduler;

    private VorlageZeile? _zuReaktivierendeZeile;
    private int? _zuLoeschendeId;

    /// <summary>
    /// Bitte um einen Wechsel in die Ausgabenliste, gefiltert auf eine
    /// Vorlage. Der Bereich kennt die Navigation nicht selbst - der
    /// <see cref="MainViewModel"/> hoert zu und setzt sie um.
    /// </summary>
    public event EventHandler<VorlagenBuchungenAnfrage>? BuchungenAnzeigenAngefordert;

    public ObservableCollection<VorlageZeile> Zeilen { get; } = new();

    [ObservableProperty]
    private string _anzahlText = string.Empty;

    [ObservableProperty]
    private string _monatlicheBelastungText = string.Empty;

    /// <summary>Die monatliche Belastung ist negativ - Erstattungen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _monatlicheBelastungIstErstattung;

    [ObservableProperty]
    private bool _keineVorlagen;

    // ---------------- Overlays und Baender ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BearbeitenAktiv))]
    private VorlageBearbeitenViewModel? _bearbeiten;

    public bool BearbeitenAktiv => Bearbeiten is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschAnfrageAktiv))]
    private string? _loeschAnfrageText;

    public bool LoeschAnfrageAktiv => LoeschAnfrageText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReaktivierungAnfrageAktiv))]
    private string? _reaktivierungAnfrageText;

    public bool ReaktivierungAnfrageAktiv => ReaktivierungAnfrageText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErgebnisSichtbar))]
    private string? _ergebnisText;

    public bool ErgebnisSichtbar => ErgebnisText is not null;

    public ObservableCollection<string> ErgebnisZeilen { get; } = new();

    /// <summary>
    /// Ob die Auflistung der erzeugten Buchungen etwas herzugeben hat.
    /// Bewusst eine eigene Eigenschaft statt einer Bindung an
    /// ErgebnisZeilen.Count - eine Anzahl ist kein Wahrheitswert, und die
    /// stillschweigende Umwandlung waere genau die Sorte Bindung, die erst
    /// zur Laufzeit auffaellt.
    /// </summary>
    [ObservableProperty]
    private bool _ergebnisHatZeilen;

    /// <summary>
    /// Ein Schreibfehler ausserhalb des Formulars - Loeschen, Aktivieren,
    /// Nachholen. Als Band ueber der Liste.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    public VorlagenViewModel(
        RecurringExpenseRepository recurringExpenseRepository,
        CategoryRepository categoryRepository,
        PersonRepository personRepository,
        RecurringExpenseScheduler scheduler)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _scheduler = scheduler;

        LadeListe();
    }

    // Lokales Kalenderdatum, nicht UTC: "heute faellig" bezieht sich auf den
    // Tag des Anwenders (Regel 3 betrifft nur gespeicherte Zeitstempel).
    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// Laedt die Liste neu. Oeffentlich, weil die Bereichs-ViewModels
    /// DI-Singletons sind und zwischenzeitlich erzeugte Buchungen die
    /// Spalte "erzeugt" veraendern.
    /// </summary>
    public void AktualisiereListe() => LadeListe();

    // ---------------- Anlegen und Bearbeiten ----------------

    [RelayCommand]
    private void NeueVorlage()
    {
        SchliesseBaender();
        Bearbeiten = ErzeugeFormular(vorlage: null, erzeugteAnzahl: 0);
    }

    [RelayCommand]
    private void BearbeitenOeffnen(VorlageZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchliesseBaender();
        Bearbeiten = ErzeugeFormular(zeile.Vorlage, zeile.ErzeugteAnzahl);
    }

    [RelayCommand]
    private void BearbeitenSpeichern()
    {
        if (Bearbeiten is not { } formular)
        {
            return;
        }

        var geprueft = formular.Pruefe();
        if (!geprueft.IsValid)
        {
            return;
        }

        // Ein weit zurueckliegendes Startdatum legt beim Speichern die
        // gesamte Historie an - ab einer gewissen Menge nicht mehr
        // stillschweigend.
        if (formular.RueckwirkendBestaetigungNoetig && !formular.RueckwirkendBestaetigt)
        {
            formular.FordereRueckwirkendBestaetigung();
            return;
        }

        // SpeichereUndErzeuge kann an mehreren Stellen schreiben. Ein
        // Fehler dabei laesst das Formular offen und unveraendert stehen -
        // die eingetippte Vorlage soll nicht verloren gehen.
        IReadOnlyList<Expense> erzeugt = Array.Empty<Expense>();

        formular.SpeicherFehlerText = Schreibvorgang.Versuche(
            "Beim Speichern einer Vorlage",
            () => erzeugt = SpeichereUndErzeuge(formular, geprueft));

        if (formular.SpeicherFehlerText is not null)
        {
            return;
        }

        Bearbeiten = null;
        LadeListe();
        ZeigeErgebnis(erzeugt, leerText: null);
    }

    [RelayCommand]
    private void BearbeitenAbbrechen() => Bearbeiten = null;

    [RelayCommand]
    private void RueckwirkendBestaetigen() => Bearbeiten?.BestaetigeRueckwirkend();

    [RelayCommand]
    private void RueckwirkendAbbrechen() => Bearbeiten?.BrichRueckwirkendAb();

    // ---------------- Aktivieren und Deaktivieren ----------------

    [RelayCommand]
    private void Deaktivieren(VorlageZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchliesseBaender();
        _recurringExpenseRepository.Deactivate(zeile.Id);
        LadeListe();
    }

    /// <summary>
    /// Aktiviert eine Vorlage. Steht GeneratedThrough noch auf dem Stand von
    /// vor der Stilllegung, wuerde der naechste Lauf die gesamte Pause
    /// nachholen - deshalb wird bei Rueckstand erst gefragt, statt still
    /// Dutzende Buchungen anzulegen.
    /// </summary>
    [RelayCommand]
    private void Aktivieren(VorlageZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchliesseBaender();

        var vorlage = zeile.Vorlage;
        var rueckstand = RecurrenceGenerator.GetDueOccurrences(
            vorlage.StartDate, vorlage.EndDate, vorlage.IntervalUnit,
            vorlage.IntervalCount, vorlage.AnchorDay, vorlage.GeneratedThrough, Heute);

        if (rueckstand.Count == 0)
        {
            _recurringExpenseRepository.Activate(zeile.Id);
            LadeListe();
            return;
        }

        _zuReaktivierendeZeile = zeile;

        var bisher = vorlage.GeneratedThrough is DateOnly erzeugtBis
            ? $"Für diese Vorlage wurde bereits bis zum {GermanDateInput.ToText(erzeugtBis)} erzeugt. "
            : "Für diese Vorlage wurde noch nie etwas erzeugt. ";

        ReaktivierungAnfrageText =
            bisher +
            (rueckstand.Count == 1
                ? $"Es fehlt 1 Buchung ({GermanDateInput.ToText(rueckstand[0])})."
                : $"Es fehlen {rueckstand.Count} Buchungen vom {GermanDateInput.ToText(rueckstand[0])} " +
                  $"bis zum {GermanDateInput.ToText(rueckstand[^1])}.");
    }

    [RelayCommand]
    private void ReaktivierungNachholen()
    {
        if (_zuReaktivierendeZeile is not { } zeile)
        {
            return;
        }

        IReadOnlyList<Expense> erzeugt = Array.Empty<Expense>();

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Reaktivieren einer Vorlage mit Nachholen",
            () =>
            {
                _recurringExpenseRepository.Activate(zeile.Id);
                erzeugt = _recurringExpenseRepository.GenerateDueOccurrences(zeile.Id, Heute);
            });

        if (SchreibFehlerText is not null)
        {
            return;
        }

        _zuReaktivierendeZeile = null;
        ReaktivierungAnfrageText = null;
        LadeListe();
        ZeigeErgebnis(erzeugt, leerText: null);
    }

    [RelayCommand]
    private void ReaktivierungAbHeute()
    {
        if (_zuReaktivierendeZeile is not { } zeile)
        {
            return;
        }

        // Aktivieren und den Rueckstand ueberspringen: GeneratedThrough
        // wandert auf heute, ohne dass etwas erzeugt wird.
        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Reaktivieren einer Vorlage ab heute",
            () =>
            {
                _recurringExpenseRepository.Activate(zeile.Id);
                _recurringExpenseRepository.SetGeneratedThrough(zeile.Id, Heute);
            });

        if (SchreibFehlerText is not null)
        {
            return;
        }

        _zuReaktivierendeZeile = null;
        ReaktivierungAnfrageText = null;
        LadeListe();
    }

    [RelayCommand]
    private void ReaktivierungAbbrechen()
    {
        _zuReaktivierendeZeile = null;
        ReaktivierungAnfrageText = null;
    }

    // ---------------- Loeschen ----------------

    [RelayCommand]
    private void Loeschen(VorlageZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchliesseBaender();
        Bearbeiten = null;
        _zuLoeschendeId = zeile.Id;

        var buchungen = zeile.ErzeugteAnzahl switch
        {
            0 => "Aus dieser Vorlage wurde bisher nichts erzeugt.",
            1 => "Die 1 daraus bereits erzeugte Buchung bleibt erhalten und verliert lediglich "
                 + "ihre Zuordnung zur Vorlage.",
            _ => $"Die {zeile.ErzeugteAnzahl} daraus bereits erzeugten Buchungen bleiben erhalten "
                 + "und verlieren lediglich ihre Zuordnung zur Vorlage.",
        };

        LoeschAnfrageText =
            $"Vorlage wirklich löschen?\n{zeile.LoeschBeschreibung}\n\n{buchungen}\n\n"
            + "Zum Stoppen der künftigen Erzeugung genügt \"Deaktivieren\" - dabei bleibt die "
            + "Vorlage samt Zuordnung erhalten.";
    }

    [RelayCommand]
    private void LoeschenBestaetigen()
    {
        if (_zuLoeschendeId is int id)
        {
            SchreibFehlerText = Schreibvorgang.Versuche(
                "Beim Loeschen einer Vorlage",
                () => _recurringExpenseRepository.Delete(id));

            if (SchreibFehlerText is not null)
            {
                // Die Nachfrage bleibt stehen - der Versuch laesst sich
                // gleich wiederholen.
                return;
            }
        }

        _zuLoeschendeId = null;
        LoeschAnfrageText = null;
        LadeListe();
    }

    [RelayCommand]
    private void LoeschenAbbrechen()
    {
        _zuLoeschendeId = null;
        LoeschAnfrageText = null;
    }

    // ---------------- Erzeugen und Sprung ----------------

    /// <summary>
    /// Stoesst die Erzeugung von Hand an. Noetig, wenn die Anwendung lange
    /// offen war oder eine Vorlage gerade erst entstanden ist. Ein zweiter
    /// Druck erzeugt nichts mehr - dafuer sorgt GeneratedThrough.
    /// </summary>
    [RelayCommand]
    private void JetztErzeugen()
    {
        SchliesseBaender();

        IReadOnlyList<Expense> erzeugt = Array.Empty<Expense>();

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Erzeugen faelliger Buchungen von Hand",
            () => erzeugt = _scheduler.RunNow(Heute));

        if (SchreibFehlerText is not null)
        {
            return;
        }

        LadeListe();
        ZeigeErgebnis(erzeugt, leerText: "Es war nichts fällig - alle Vorlagen sind auf dem aktuellen Stand.");
    }

    [RelayCommand]
    private void BuchungenAnzeigen(VorlageZeile? zeile)
    {
        if (zeile is null || !zeile.HatErzeugteBuchungen)
        {
            return;
        }

        BuchungenAnzeigenAngefordert?.Invoke(this, new VorlagenBuchungenAnfrage(zeile.Id, zeile.Titel));
    }

    [RelayCommand]
    private void ErgebnisSchliessen()
    {
        ErgebnisText = null;
        ErgebnisZeilen.Clear();
        ErgebnisHatZeilen = false;
    }

    // ---------------- Innereien ----------------

    private VorlageBearbeitenViewModel ErzeugeFormular(RecurringExpense? vorlage, int erzeugteAnzahl)
    {
        // Kategorien und Personen bewusst beim Oeffnen frisch laden: der
        // Bereich ist ein DI-Singleton, und nebenan in der Verwaltung
        // koennen gerade Kategorien angelegt oder archiviert worden sein.
        var pfade = CategoryPaths.BuildFullPaths(_categoryRepository.GetTree());

        return new VorlageBearbeitenViewModel(
            vorlage,
            vorlage is not null && pfade.TryGetValue(vorlage.CategoryId, out var pfad) ? pfad : null,
            _categoryRepository.GetSelectableLeaves(),
            _personRepository.GetAllActive(),
            erzeugteAnzahl,
            Heute);
    }

    private IReadOnlyList<Expense> SpeichereUndErzeuge(
        VorlageBearbeitenViewModel formular, RecurringExpenseValidation geprueft)
    {
        var categoryId = formular.AusgewaehlteKategorie!.Id;
        var payerId = formular.AusgewaehlterZahler!.Id;
        var intervalUnit = formular.AusgewaehlteIntervallEinheit.Wert;

        int id;

        if (formular.VorlageId is int bestehendeId)
        {
            // Update setzt ModifiedUtc und laesst GeneratedThrough stehen -
            // bereits erzeugte Buchungen bleiben unveraendert (Regel 6).
            _recurringExpenseRepository.Update(
                bestehendeId, categoryId, payerId, geprueft.AmountCents, formular.Titel.Trim(),
                intervalUnit, geprueft.IntervalCount, geprueft.AnchorDay,
                geprueft.StartDate, geprueft.EndDate, formular.BemerkungOderNull,
                formular.IstEinnahme);

            id = bestehendeId;
        }
        else
        {
            var erstellt = _recurringExpenseRepository.Create(
                categoryId, payerId, geprueft.AmountCents, formular.Titel.Trim(),
                intervalUnit, geprueft.IntervalCount, geprueft.AnchorDay,
                geprueft.StartDate, geprueft.EndDate, formular.BemerkungOderNull,
                formular.IstEinnahme);

            id = erstellt.Id;
        }

        // Der Aktiv-Schalter wird immer angeglichen: Create legt aktiv an,
        // Update laesst den Zustand unveraendert.
        if (formular.IstAktiv)
        {
            _recurringExpenseRepository.Activate(id);
        }
        else
        {
            _recurringExpenseRepository.Deactivate(id);
        }

        return formular.IstAktiv
            ? _recurringExpenseRepository.GenerateDueOccurrences(id, Heute)
            : Array.Empty<Expense>();
    }

    private void ZeigeErgebnis(IReadOnlyList<Expense> erzeugt, string? leerText)
    {
        ErgebnisZeilen.Clear();
        ErgebnisHatZeilen = erzeugt.Count > 0;

        if (erzeugt.Count == 0)
        {
            // Beim Speichern ist "nichts erzeugt" der Normalfall und keine
            // Meldung wert - beim Knopfdruck dagegen schon.
            ErgebnisText = leerText;
            return;
        }

        foreach (var expense in erzeugt)
        {
            ErgebnisZeilen.Add(
                $"{GermanDateInput.ToText(expense.ExpenseDate)} · " +
                $"{EuroText.Format(expense.AmountCents)}" +
                (string.IsNullOrEmpty(expense.Note) ? string.Empty : $" · {expense.Note}"));
        }

        ErgebnisText = erzeugt.Count == 1
            ? "1 Buchung wurde erzeugt."
            : $"{erzeugt.Count} Buchungen wurden erzeugt.";
    }

    private void SchliesseBaender()
    {
        _zuReaktivierendeZeile = null;
        ReaktivierungAnfrageText = null;
        _zuLoeschendeId = null;
        LoeschAnfrageText = null;

        // Auch der Fehler von vorhin: er gehoerte zu dem Vorgang, der
        // gerade weggeraeumt wird, und wuerde sonst ueber dem naechsten
        // stehen bleiben.
        SchreibFehlerText = null;
    }

    private void LadeListe()
    {
        var heute = Heute;

        var vorlagen = _recurringExpenseRepository.GetAll();
        var pfade = CategoryPaths.BuildFullPaths(_categoryRepository.GetTree());
        var personen = _personRepository.GetAll().ToDictionary(person => person.Id, person => person.Name);
        var anzahlen = _recurringExpenseRepository.GetGeneratedExpenseCounts();

        Zeilen.Clear();

        foreach (var vorlage in vorlagen)
        {
            Zeilen.Add(new VorlageZeile(
                vorlage,
                pfade.TryGetValue(vorlage.CategoryId, out var pfad) ? pfad : "(unbekannte Kategorie)",
                personen.TryGetValue(vorlage.PayerId, out var name) ? name : "(unbekannt)",
                anzahlen.TryGetValue(vorlage.Id, out var anzahl) ? anzahl : 0,
                heute));
        }

        KeineVorlagen = Zeilen.Count == 0;
        AnzahlText = Zeilen.Count == 1 ? "1 Vorlage" : $"{Zeilen.Count} Vorlagen";

        var belastungCents = MonthlyBurden.TotalPerMonthCents(vorlagen, heute);
        MonatlicheBelastungText = EuroText.Format(belastungCents);
        MonatlicheBelastungIstErstattung = EuroText.IsNegative(belastungCents);
    }
}
