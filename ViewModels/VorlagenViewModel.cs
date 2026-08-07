using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

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
    private readonly BackupService _backupService;
    private readonly IMessenger _messenger;

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

    /// <summary>Die monatliche Belastung ist positiv - Einnahmen ueberwiegen.</summary>
    [ObservableProperty]
    private bool _monatlicheBelastungIstEinnahme;

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

    /// <summary>
    /// Wie viele Buchungen aus der zu loeschenden Vorlage stammen. Frisch
    /// aus der Datenbank geholt und nicht aus der Zeile uebernommen: die
    /// Liste kann seit dem letzten Laden alt geworden sein, und diese Zahl
    /// steht gleich in einer Nachfrage, die nicht umkehrbar ist.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschenMitBuchungenMoeglich))]
    [NotifyPropertyChangedFor(nameof(LoeschenMitBuchungenText))]
    [NotifyPropertyChangedFor(nameof(LoeschenBestaetigenText))]
    private int _loeschenBuchungenAnzahl;

    /// <summary>
    /// Ob ueberhaupt zur Wahl steht, die Buchungen mitzuloeschen. Bei
    /// einer Vorlage ohne erzeugte Buchungen entfaellt die Auswahl - eine
    /// Frage ohne Gegenstand ist schlimmer als keine Frage.
    /// </summary>
    public bool LoeschenMitBuchungenMoeglich => LoeschenBuchungenAnzahl > 0;

    /// <summary>
    /// Die Voreinstellung ist bewusst "nicht mitloeschen": das erhaelt die
    /// Historie. Wird bei jedem neuen Loeschversuch ausdruecklich
    /// zurueckgesetzt, statt sich darauf zu verlassen, dass sie noch
    /// stimmt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschenBestaetigenText))]
    private bool _auchBuchungenLoeschen;

    public string LoeschenMitBuchungenText => LoeschenBuchungenAnzahl == 1
        ? "Auch die 1 erzeugte Buchung löschen"
        : $"Auch die {LoeschenBuchungenAnzahl} erzeugten Buchungen löschen";

    /// <summary>Beschriftung des Bestaetigungsknopfes - sie nennt, was
    /// tatsaechlich verschwindet.</summary>
    public string LoeschenBestaetigenText => AuchBuchungenLoeschen
        ? (LoeschenBuchungenAnzahl == 1
            ? "Vorlage und 1 Buchung löschen"
            : $"Vorlage und {LoeschenBuchungenAnzahl} Buchungen löschen")
        : "Löschen";

    /// <summary>
    /// Ein Fehler waehrend des Loeschens - gescheiterte Sicherung oder
    /// gescheiterter Schreibvorgang. Steht IN der Nachfrage und nicht als
    /// Band dahinter: das Overlay liegt darueber, ein Band im Hintergrund
    /// waere nicht zu lesen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoeschFehlerSichtbar))]
    private string? _loeschFehlerText;

    public bool LoeschFehlerSichtbar => LoeschFehlerText is not null;

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
        RecurringExpenseScheduler scheduler,
        BackupService backupService,
        IMessenger messenger)
    {
        _recurringExpenseRepository = recurringExpenseRepository;
        _categoryRepository = categoryRepository;
        _personRepository = personRepository;
        _scheduler = scheduler;
        _backupService = backupService;
        _messenger = messenger;

        // Die Spalte "erzeugt" zaehlt Buchungen; wird anderswo eine
        // geloescht oder eine Vorlagenbuchung bearbeitet, ist die Zahl hier
        // sonst still veraltet (Regel 14).
        _messenger.Register<VorlagenViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.LadeListe());

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

    /// <summary>
    /// Hebt genau eine Zeile hervor - fuer den Sprung aus der Kachel
    /// "Nächste fällige Vorlage" auf der Startseite.
    ///
    /// Muss NACH dem Bereichswechsel aufgerufen werden: der Wechsel laesst
    /// ueber <see cref="AktualisiereListe"/> die Zeilen neu entstehen und
    /// wuerde eine vorher gesetzte Markierung mit wegwerfen.
    /// </summary>
    public void WaehleVorlage(int vorlageId)
    {
        foreach (var zeile in Zeilen)
        {
            zeile.IstHervorgehoben = zeile.Id == vorlageId;
        }
    }

    // ---------------- Anlegen und Bearbeiten ----------------

    [RelayCommand]
    private void NeueVorlage()
    {
        SchliesseBaender();
        Bearbeiten = ErzeugeFormular(vorlage: null, uebertragbareAnzahl: 0);
    }

    [RelayCommand]
    private void BearbeitenOeffnen(VorlageZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchliesseBaender();

        // Frisch gezaehlt statt aus der Zeile uebernommen: an dieser Zahl
        // haengt das Angebot, die Aenderung auf die bestehenden Buchungen
        // zu uebertragen.
        Bearbeiten = ErzeugeFormular(
            zeile.Vorlage, _recurringExpenseRepository.CountGeneratedExpenses(zeile.Id));
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

        // Die ausdruecklich angehakte Uebertragung auf die bereits
        // erzeugten Buchungen. Sie laeuft NACH dem Speichern: uebertragen
        // wird der frisch gespeicherte Stand der Vorlage.
        var uebertragung = formular is { IstBestehend: true, AenderungUebertragen: true }
            ? UebertrageAufErzeugteBuchungen(formular.VorlageId!.Value)
            : null;

        Bearbeiten = null;
        LadeListe();
        ZeigeErgebnis(erzeugt, leerText: null, vorspann: uebertragung);
    }

    /// <summary>
    /// Uebertraegt die gespeicherte Vorlagenaenderung auf die bereits
    /// erzeugten Buchungen und liefert den Text fuer das Ergebnisband.
    ///
    /// Regel 6 bleibt gewahrt: hierher kommt nur, wer das Haekchen im
    /// Formular ausdruecklich gesetzt hat. Scheitert die Uebertragung,
    /// bleibt die gespeicherte Vorlage bestehen - das ist richtig, und
    /// genau das muss der Fehlertext sagen, sonst waere unklar, welcher
    /// Teil des Speicherns gegriffen hat.
    /// </summary>
    private string? UebertrageAufErzeugteBuchungen(int vorlageId)
    {
        // Die Sicherung enthaelt die Buchungen in ihrem Zustand VOR der
        // Uebertragung - genau die, die sonst nicht wiederzubekommen
        // waeren (Regel 8).
        if (!SichereVorNichtUmkehrbaremSchritt(
                "Vor dem Übertragen auf die bestehenden Buchungen", out var sicherungsFehler))
        {
            SchreibFehlerText =
                "Die Vorlage wurde gespeichert, die Übertragung auf die bestehenden "
                + "Buchungen jedoch nicht ausgeführt.\n\n" + sicherungsFehler;
            return null;
        }

        var anzahl = 0;

        var fehler = Schreibvorgang.Versuche(
            "Beim Uebertragen einer Vorlagenaenderung auf die erzeugten Buchungen",
            () => anzahl = _recurringExpenseRepository.ApplyToGeneratedExpenses(vorlageId));

        if (fehler is not null)
        {
            SchreibFehlerText =
                "Die Vorlage wurde gespeichert, die Übertragung auf die bestehenden "
                + "Buchungen jedoch nicht ausgeführt.\n\n" + fehler;
            return null;
        }

        // Betrag, Kategorie, Zahler und Art bestehender Buchungen haben
        // sich geaendert - jede Liste und jede Auswertung zeigt sonst
        // veraltete Werte (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());

        return anzahl == 1
            ? "Die Änderung wurde auf 1 bereits erzeugte Buchung übertragen."
            : $"Die Änderung wurde auf {anzahl} bereits erzeugte Buchungen übertragen.";
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

        LoeschenBuchungenAnzahl = _recurringExpenseRepository.CountGeneratedExpenses(zeile.Id);
        AuchBuchungenLoeschen = false;

        var buchungen = LoeschenBuchungenAnzahl switch
        {
            0 => "Aus dieser Vorlage wurde bisher nichts erzeugt.",
            1 => "Die 1 daraus bereits erzeugte Buchung bleibt erhalten und verliert lediglich "
                 + "ihre Zuordnung zur Vorlage.",
            _ => $"Die {LoeschenBuchungenAnzahl} daraus bereits erzeugten Buchungen bleiben erhalten "
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
        if (_zuLoeschendeId is not int id)
        {
            LoeschenAbbrechen();
            return;
        }

        // Die Zahl wird hier festgehalten: sie steht gleich in der
        // Erfolgsmeldung, die Eigenschaft selbst wird davor geraeumt.
        var mitBuchungen = AuchBuchungenLoeschen && LoeschenMitBuchungenMoeglich;
        var anzahl = LoeschenBuchungenAnzahl;

        if (mitBuchungen && !SichereVorNichtUmkehrbaremSchritt(
                "Vor dem Löschen der Buchungen", out var sicherungsFehler))
        {
            LoeschFehlerText = sicherungsFehler
                + "\n\nGelöscht wurde deshalb nichts — es ist alles unverändert.";
            return;
        }

        LoeschFehlerText = Schreibvorgang.Versuche(
            mitBuchungen
                ? "Beim Loeschen einer Vorlage samt ihrer Buchungen"
                : "Beim Loeschen einer Vorlage",
            () =>
            {
                if (mitBuchungen)
                {
                    _recurringExpenseRepository.DeleteWithExpenses(id);
                }
                else
                {
                    _recurringExpenseRepository.Delete(id);
                }
            });

        if (LoeschFehlerText is not null)
        {
            // Die Nachfrage bleibt stehen - der Versuch laesst sich
            // gleich wiederholen.
            return;
        }

        LoeschenAbbrechen();
        LadeListe();

        // In beiden Faellen aendert sich etwas an den Buchungen: entweder
        // sind sie weg, oder sie haben ihre Zuordnung zur Vorlage verloren
        // (ON DELETE SET NULL) und fallen damit aus einem Vorlagenfilter
        // heraus (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());

        if (mitBuchungen)
        {
            ZeigeErgebnis(
                Array.Empty<Expense>(),
                leerText: anzahl == 1
                    ? "Vorlage und 1 daraus erzeugte Buchung wurden gelöscht."
                    : $"Vorlage und {anzahl} daraus erzeugte Buchungen wurden gelöscht.");
        }
    }

    [RelayCommand]
    private void LoeschenAbbrechen()
    {
        _zuLoeschendeId = null;
        LoeschAnfrageText = null;
        LoeschFehlerText = null;
        LoeschenBuchungenAnzahl = 0;
        AuchBuchungenLoeschen = false;
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

    private VorlageBearbeitenViewModel ErzeugeFormular(RecurringExpense? vorlage, int uebertragbareAnzahl)
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
            uebertragbareAnzahl,
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

    /// <summary>
    /// Baut das Ergebnisband. <paramref name="leerText"/> erscheint, wenn
    /// nichts erzeugt wurde (beim Speichern der Normalfall und keine
    /// Meldung wert, beim Knopfdruck dagegen schon).
    /// <paramref name="vorspann"/> steht davor - beim selben Speichern
    /// kann sowohl uebertragen als auch erzeugt worden sein.
    /// </summary>
    private void ZeigeErgebnis(IReadOnlyList<Expense> erzeugt, string? leerText, string? vorspann = null)
    {
        ErgebnisZeilen.Clear();
        ErgebnisHatZeilen = erzeugt.Count > 0;

        foreach (var expense in erzeugt)
        {
            ErgebnisZeilen.Add(
                $"{GermanDateInput.ToText(expense.ExpenseDate)} · " +
                $"{EuroText.Format(expense.AmountCents)}" +
                (string.IsNullOrEmpty(expense.Note) ? string.Empty : $" · {expense.Note}"));
        }

        var erzeugtText = erzeugt.Count switch
        {
            0 => leerText,
            1 => "1 Buchung wurde erzeugt.",
            _ => $"{erzeugt.Count} Buchungen wurden erzeugt.",
        };

        var zeilen = new[] { vorspann, erzeugtText }
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();

        ErgebnisText = zeilen.Count == 0 ? null : string.Join("\n", zeilen);
    }

    /// <summary>
    /// Legt vor einem nicht umkehrbaren Schritt eine Sicherung an
    /// (Regel 8, dieselbe Behandlung wie beim Zusammenfuehren von
    /// Kategorien). Liefert false, wenn dabei etwas schiefging - dann
    /// unterbleibt der Schritt: ein nicht umkehrbarer Vorgang ohne Netz
    /// ist genau das, was die Sicherung verhindern soll.
    ///
    /// <paramref name="anlass"/> beginnt den Satz ("Vor dem Löschen der
    /// Buchungen").
    /// </summary>
    private bool SichereVorNichtUmkehrbaremSchritt(string anlass, out string fehlerText)
    {
        // Lokale Zeit wie bei jeder Sicherung - der Dateiname soll zum
        // Kalendertag des Anwenders passen.
        var sicherung = _backupService.RunNow(DateTime.Now);

        if (!sicherung.NeedsAttention)
        {
            fehlerText = string.Empty;
            return true;
        }

        fehlerText =
            $"{anlass} wird automatisch gesichert, weil sich der Vorgang nicht "
            + "rückgängig machen lässt. Genau diese Sicherung ist fehlgeschlagen.\n\n"
            + FileErrorText.ForBackup(sicherung.PrimaryProblem);

        return false;
    }

    private void SchliesseBaender()
    {
        _zuReaktivierendeZeile = null;
        ReaktivierungAnfrageText = null;
        _zuLoeschendeId = null;
        LoeschAnfrageText = null;
        LoeschFehlerText = null;
        LoeschenBuchungenAnzahl = 0;
        AuchBuchungenLoeschen = false;

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
        MonatlicheBelastungIstEinnahme = EuroText.IsPositive(belastungCents);
    }
}
