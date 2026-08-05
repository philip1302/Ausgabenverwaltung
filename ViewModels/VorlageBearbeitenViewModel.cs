using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.RecurringExpenses;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Das Formular zum Anlegen und Aendern einer Vorlage, samt Live-Vorschau
/// der naechsten Faelligkeiten.
///
/// Es wird hier NICHT gerechnet: die Termine kommen aus
/// <see cref="RecurrenceGenerator"/>, die Pruefung aus
/// <see cref="RecurringExpenseValidator"/> - beides Core (Regel 7). Diese
/// Klasse bindet, formatiert und verteilt Fehlermeldungen auf ihre Felder.
/// </summary>
public sealed partial class VorlageBearbeitenViewModel : ObservableObject
{
    /// <summary>Wie viele Termine die Vorschau zeigt.</summary>
    private const int VorschauAnzahl = 6;

    /// <summary>
    /// Ab wie vielen rueckwirkend erzeugten Buchungen ausdruecklich
    /// bestaetigt werden muss. Ein weit zurueckliegendes Startdatum kann
    /// sonst unbemerkt Dutzende Buchungen anlegen.
    /// </summary>
    private const int RueckwirkendSchwelle = 10;

    private readonly DateOnly _heute;
    private readonly DateOnly? _urspruenglichesStartdatum;

    public int? VorlageId { get; }

    public bool IstBestehend => VorlageId is not null;

    public string Titelzeile => IstBestehend ? "Vorlage bearbeiten" : "Neue Vorlage";

    /// <summary>Bis hierhin wurde bereits erzeugt, NULL = noch nie gelaufen.</summary>
    public DateOnly? GeneratedThrough { get; }

    public int ErzeugteAnzahl { get; }

    public IReadOnlyList<IntervallOption> IntervallOptionen { get; } =
    [
        new IntervallOption("Tag", "day"),
        new IntervallOption("Woche", "week"),
        new IntervallOption("Monat", "month"),
        new IntervallOption("Jahr", "year"),
    ];

    public IReadOnlyList<CategoryOption> KategorieVorschlaege { get; }

    // Alle waehlbaren Personen (inkl. eines inzwischen archivierten
    // Zahlers, siehe BaueZahlerOptionen), ungefiltert - die Quelle, aus
    // der AktualisiereZahlerAuswahl das tatsaechlich waehlbare
    // ZahlerOptionen aufbaut. Bei einer Einnahme faellt die Ich-Person
    // dort heraus: eine Einnahme kommt immer von jemand anderem, sonst
    // liesse sich nie verfolgen, ob sie ueber die Offene-Posten-Liste
    // tatsaechlich eingegangen ist (Regel 4).
    private readonly IReadOnlyList<Person> _allePersonen;

    [ObservableProperty]
    private IReadOnlyList<Person> _zahlerOptionen = Array.Empty<Person>();

    // ---------------- Felder ----------------

    [ObservableProperty]
    private string _titel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitelFehlerSichtbar))]
    private string? _titelFehler;

    public bool TitelFehlerSichtbar => !string.IsNullOrEmpty(TitelFehler);

    [ObservableProperty]
    private string _betragText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BetragFehlerSichtbar))]
    private string? _betragFehler;

    public bool BetragFehlerSichtbar => !string.IsNullOrEmpty(BetragFehler);

    [ObservableProperty]
    private CategoryOption? _ausgewaehlteKategorie;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KategorieFehlerSichtbar))]
    private string? _kategorieFehler;

    public bool KategorieFehlerSichtbar => !string.IsNullOrEmpty(KategorieFehler);

    [ObservableProperty]
    private Person? _ausgewaehlterZahler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZahlerFehlerSichtbar))]
    private string? _zahlerFehler;

    public bool ZahlerFehlerSichtbar => !string.IsNullOrEmpty(ZahlerFehler);

    [ObservableProperty]
    private string? _bemerkung;

    /// <summary>
    /// Ob die aus dieser Vorlage erzeugten Buchungen Einnahmen statt
    /// Ausgaben sind (siehe Entities.RecurringExpense.IsIncome).
    /// </summary>
    [ObservableProperty]
    private bool _istEinnahme;

    /// <summary>
    /// Erklaert, warum das Haekchen bei "Einnahme" gerade automatisch
    /// zurueckgesetzt wurde: es gibt ausser der Ich-Person niemanden, dem
    /// sich die Einnahme zuordnen liesse (siehe AktualisiereZahlerAuswahl).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EinnahmeHinweisSichtbar))]
    private string? _einnahmeHinweisText;

    public bool EinnahmeHinweisSichtbar => EinnahmeHinweisText is not null;

    // ---------------- Rhythmus ----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnkertagSichtbar))]
    private IntervallOption _ausgewaehlteIntervallEinheit;

    public bool AnkertagSichtbar => AusgewaehlteIntervallEinheit.HatAnkertag;

    [ObservableProperty]
    private string _intervallAnzahlText = "1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IntervallAnzahlFehlerSichtbar))]
    private string? _intervallAnzahlFehler;

    public bool IntervallAnzahlFehlerSichtbar => !string.IsNullOrEmpty(IntervallAnzahlFehler);

    [ObservableProperty]
    private string _ankertagText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnkertagFehlerSichtbar))]
    private string? _ankertagFehler;

    public bool AnkertagFehlerSichtbar => !string.IsNullOrEmpty(AnkertagFehler);

    /// <summary>
    /// Ankertag ueber 28: in kuerzeren Monaten wird gekuerzt (Regel 5).
    /// Gilt auch dann, wenn kein Ankertag eingetragen ist und der Tag des
    /// Startdatums einspringt.
    /// </summary>
    [ObservableProperty]
    private bool _ankertagHinweisSichtbar;

    [ObservableProperty]
    private string _startDatumText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartDatumFehlerSichtbar))]
    private string? _startDatumFehler;

    public bool StartDatumFehlerSichtbar => !string.IsNullOrEmpty(StartDatumFehler);

    [ObservableProperty]
    private string _endDatumText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndDatumFehlerSichtbar))]
    private string? _endDatumFehler;

    public bool EndDatumFehlerSichtbar => !string.IsNullOrEmpty(EndDatumFehler);

    [ObservableProperty]
    private bool _istAktiv = true;

    // ---------------- Vorschau ----------------

    public ObservableCollection<string> VorschauTermine { get; } = new();

    [ObservableProperty]
    private bool _vorschauSichtbar;

    /// <summary>
    /// Erklaerung, wenn die Vorschau leer bleibt - entweder ist der
    /// Rhythmus noch unvollstaendig oder die Laufzeit schon vorbei.
    /// </summary>
    [ObservableProperty]
    private string _vorschauLeerText = "Rhythmus und Startdatum eingeben, dann erscheinen hier die nächsten Termine.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckwirkendSichtbar))]
    private string? _rueckwirkendText;

    public bool RueckwirkendSichtbar => RueckwirkendText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckwirkendBestaetigungNoetig))]
    private int _rueckwirkendAnzahl;

    public bool RueckwirkendBestaetigungNoetig => RueckwirkendAnzahl > RueckwirkendSchwelle;

    /// <summary>
    /// Der Anwender hat die rueckwirkende Erzeugung ausdruecklich
    /// bestaetigt. Verfaellt bei jeder Rhythmusaenderung, damit die
    /// Bestaetigung nicht fuer eine andere Zahl gilt als die gezeigte.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckwirkendAbfrageSichtbar))]
    private bool _rueckwirkendBestaetigt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckwirkendAbfrageSichtbar))]
    private bool _rueckwirkendAbfrageAngefordert;

    public bool RueckwirkendAbfrageSichtbar => RueckwirkendAbfrageAngefordert && !RueckwirkendBestaetigt;

    [ObservableProperty]
    private string? _rueckwirkendAbfrageText;

    // ---------------- Hinweise beim Bearbeiten ----------------

    /// <summary>Regel 6: Betragsaenderungen wirken nie rueckwirkend.</summary>
    public bool BetragHinweisSichtbar => IstBestehend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartVorverlegtHinweisSichtbar))]
    private string? _startVorverlegtHinweis;

    public bool StartVorverlegtHinweisSichtbar => StartVorverlegtHinweis is not null;

    /// <summary>
    /// Ein Fehler beim Schreiben. Das Formular bleibt dabei offen und
    /// vollstaendig gefuellt - eine gerade eingetippte Vorlage ist zu viel
    /// Arbeit, um sie wegen eines vollen Datentraegers noch einmal
    /// eingeben zu muessen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeicherFehlerSichtbar))]
    private string? _speicherFehlerText;

    public bool SpeicherFehlerSichtbar => SpeicherFehlerText is not null;

    public VorlageBearbeitenViewModel(
        RecurringExpense? vorlage,
        string? kategoriePfad,
        IReadOnlyList<CategoryOption> waehlbareKategorien,
        IReadOnlyList<Person> zahler,
        int erzeugteAnzahl,
        DateOnly heute)
    {
        _heute = heute;
        ErzeugteAnzahl = erzeugteAnzahl;

        VorlageId = vorlage?.Id;
        GeneratedThrough = vorlage?.GeneratedThrough;
        _urspruenglichesStartdatum = vorlage?.StartDate;

        KategorieVorschlaege = BaueKategorieVorschlaege(vorlage, kategoriePfad, waehlbareKategorien);
        _allePersonen = BaueZahlerOptionen(vorlage, zahler);

        if (vorlage is null)
        {
            _ausgewaehlteIntervallEinheit = IntervallOptionen.First(option => option.Wert == "month");
            _ausgewaehlterZahler = _allePersonen.FirstOrDefault(person => person.IsSelf)
                ?? _allePersonen.FirstOrDefault();
            _startDatumText = GermanDateInput.ToText(heute);
            _ankertagText = heute.Day.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            _titel = vorlage.Title;

            // EuroText.Plain und nicht EuroText.Format: in ein Eingabefeld
            // gehoert die blanke Zahl (das €-Zeichen steht als Beschriftung
            // daneben), und der Tausenderpunkt wuerde beim Speichern
            // abgelehnt (siehe Money.TryParseEuroText).
            _betragText = EuroText.Plain(vorlage.AmountCents);
            _bemerkung = vorlage.Note;
            _istEinnahme = vorlage.IsIncome;

            _ausgewaehlteKategorie = KategorieVorschlaege
                .FirstOrDefault(option => option.Id == vorlage.CategoryId);
            _ausgewaehlterZahler = _allePersonen
                .FirstOrDefault(person => person.Id == vorlage.PayerId);

            _ausgewaehlteIntervallEinheit =
                IntervallOptionen.FirstOrDefault(option => option.Wert == vorlage.IntervalUnit)
                ?? IntervallOptionen.First(option => option.Wert == "month");
            _intervallAnzahlText = vorlage.IntervalCount.ToString(CultureInfo.InvariantCulture);
            _ankertagText = vorlage.AnchorDay?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            _startDatumText = GermanDateInput.ToText(vorlage.StartDate);
            _endDatumText = vorlage.EndDate is DateOnly ende ? GermanDateInput.ToText(ende) : string.Empty;
            _istAktiv = vorlage.IsActive;
        }

        AktualisiereZahlerAuswahl();
        AktualisiereVorschau();
    }

    // Jede Aenderung am Rhythmus baut die Vorschau neu auf - das ist der
    // Kern dieses Formulars: man soll sehen, was man einstellt, bevor man
    // speichert.
    /// <summary>
    /// Der zuletzt eingetragene Ankertag, waehrend das Feld ausgeblendet
    /// ist. Siehe <see cref="OnAusgewaehlteIntervallEinheitChanged"/>.
    /// </summary>
    private string _gemerkterAnkertag = string.Empty;

    partial void OnAusgewaehlteIntervallEinheitChanged(IntervallOption value)
    {
        // Beim Wechsel auf Tag oder Woche wird das Ankertagfeld
        // ausgeblendet - und geraeumt. Sonst stuende dort ein Wert, den
        // die Pruefung beanstandet (ein fester Tag im Monat ergibt bei
        // einem Wochenrhythmus keinen Sinn), den der Anwender aber gar
        // nicht sehen und deshalb auch nicht loeschen kann.
        //
        // Gemerkt wird er trotzdem: wer zum Ausprobieren kurz auf "Woche"
        // und wieder zurueck auf "Monat" stellt, soll seine 15 wiederhaben.
        if (!value.HatAnkertag)
        {
            if (AnkertagText.Length > 0)
            {
                _gemerkterAnkertag = AnkertagText;
            }

            AnkertagText = string.Empty;
            AnkertagFehler = null;
        }
        else if (AnkertagText.Length == 0 && _gemerkterAnkertag.Length > 0)
        {
            AnkertagText = _gemerkterAnkertag;
        }

        AktualisiereVorschau();
    }
    partial void OnIntervallAnzahlTextChanged(string value) => AktualisiereVorschau();
    partial void OnAnkertagTextChanged(string value) => AktualisiereVorschau();
    partial void OnStartDatumTextChanged(string value) => AktualisiereVorschau();
    partial void OnEndDatumTextChanged(string value) => AktualisiereVorschau();
    partial void OnIstAktivChanged(bool value) => AktualisiereVorschau();

    partial void OnIstEinnahmeChanged(bool value) => AktualisiereZahlerAuswahl();

    /// <summary>
    /// Baut ZahlerOptionen aus _allePersonen neu auf - dasselbe Verfahren
    /// wie in der Erfassungsmaske (siehe
    /// <see cref="ErfassenViewModel.AktualisiereZahlerAuswahl"/>): bei
    /// einer Einnahme faellt die Ich-Person heraus, eine bereits
    /// getroffene Auswahl bleibt erhalten, sofern sie weiterhin waehlbar
    /// ist, und gibt es ausser der Ich-Person niemanden, wird die Einnahme
    /// sofort zurueckgesetzt und ein Hinweistext erklaert warum.
    /// </summary>
    private void AktualisiereZahlerAuswahl()
    {
        if (IstEinnahme && _allePersonen.All(person => person.IsSelf))
        {
            IstEinnahme = false;
            EinnahmeHinweisText = "Für eine Einnahme wird zunächst eine weitere Person benötigt (siehe Verwaltung › Personen).";
            return;
        }

        EinnahmeHinweisText = null;

        var ausgewaehlteId = AusgewaehlterZahler?.Id;
        var gefiltert = _allePersonen.Where(person => !IstEinnahme || !person.IsSelf).ToList();
        ZahlerOptionen = gefiltert;

        AusgewaehlterZahler = gefiltert.FirstOrDefault(person => person.Id == ausgewaehlteId)
            ?? gefiltert.FirstOrDefault(person => !IstEinnahme && person.IsSelf)
            ?? gefiltert.FirstOrDefault();
    }

    /// <summary>
    /// Prueft alle Felder ueber Core und verteilt die Meldungen. Liefert
    /// das Ergebnis, damit der Aufrufer die geparsten Werte bekommt.
    /// </summary>
    public RecurringExpenseValidation Pruefe()
    {
        var ergebnis = RecurringExpenseValidator.Validate(BaueEingabe());

        TitelFehler = ergebnis.TitleError;
        KategorieFehler = ergebnis.CategoryError;
        ZahlerFehler = ergebnis.PayerError;
        BetragFehler = ergebnis.AmountError;
        IntervallAnzahlFehler = ergebnis.IntervalCountError;
        AnkertagFehler = ergebnis.AnchorDayError;
        StartDatumFehler = ergebnis.StartDateError;
        EndDatumFehler = ergebnis.EndDateError;

        return ergebnis;
    }

    /// <summary>Bemerkung leer bedeutet in der Datenbank NULL, nicht "".</summary>
    public string? BemerkungOderNull =>
        string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung;

    /// <summary>
    /// Fordert die Bestaetigung fuer die rueckwirkende Erzeugung an; der
    /// Text nennt Anzahl und Zeitraum.
    /// </summary>
    public void FordereRueckwirkendBestaetigung()
    {
        RueckwirkendAbfrageText =
            $"{RueckwirkendAnzahl} Buchungen werden rückwirkend erzeugt. Wirklich speichern?";
        RueckwirkendAbfrageAngefordert = true;
    }

    public void BestaetigeRueckwirkend()
    {
        RueckwirkendBestaetigt = true;
        RueckwirkendAbfrageAngefordert = false;
    }

    public void BrichRueckwirkendAb() => RueckwirkendAbfrageAngefordert = false;

    private RecurringExpenseInput BaueEingabe() => new()
    {
        Title = Titel,
        CategoryId = AusgewaehlteKategorie?.Id,
        PayerId = AusgewaehlterZahler?.Id,
        PayerIsSelf = AusgewaehlterZahler?.IsSelf ?? false,
        AmountText = BetragText,
        IsIncome = IstEinnahme,
        IntervalUnit = AusgewaehlteIntervallEinheit.Wert,
        IntervalCountText = IntervallAnzahlText,
        AnchorDayText = AnkertagText,
        StartDateText = StartDatumText,
        EndDateText = EndDatumText,
    };

    /// <summary>
    /// Baut Vorschau, Rueckstandsangabe und die beiden Hinweise neu auf.
    /// Laeuft bei jedem Tastendruck und darf deshalb unter keinen
    /// Umstaenden werfen: halbfertige Eingabe ist hier der Normalfall, kein
    /// Fehler. Fehlermeldungen setzt diese Methode bewusst nicht - die
    /// erscheinen erst beim Speichern (<see cref="Pruefe"/>).
    /// </summary>
    private void AktualisiereVorschau()
    {
        VorschauTermine.Clear();
        VorschauSichtbar = false;
        RueckwirkendText = null;
        RueckwirkendAnzahl = 0;
        RueckwirkendBestaetigt = false;
        RueckwirkendAbfrageAngefordert = false;
        StartVorverlegtHinweis = null;

        var geprueft = RecurringExpenseValidator.Validate(BaueEingabe());

        // Nur die rhythmusbildenden Felder muessen stimmen. Ein noch
        // fehlender Titel oder eine noch nicht gewaehlte Kategorie darf die
        // Vorschau nicht blockieren.
        var rhythmusVollstaendig =
            geprueft.StartDateError is null
            && geprueft.EndDateError is null
            && geprueft.IntervalCountError is null
            && geprueft.AnchorDayError is null;

        AnkertagHinweisSichtbar =
            AnkertagSichtbar
            && rhythmusVollstaendig
            && (geprueft.AnchorDay ?? geprueft.StartDate.Day) > 28;

        if (!rhythmusVollstaendig)
        {
            VorschauLeerText = "Rhythmus und Startdatum eingeben, dann erscheinen hier die nächsten Termine.";
            return;
        }

        foreach (var termin in RecurrenceGenerator.GetNextOccurrences(
                     geprueft.StartDate, geprueft.EndDate, AusgewaehlteIntervallEinheit.Wert,
                     geprueft.IntervalCount, geprueft.AnchorDay, _heute, VorschauAnzahl))
        {
            VorschauTermine.Add(GermanDateInput.ToText(termin));
        }

        VorschauSichtbar = VorschauTermine.Count > 0;
        if (!VorschauSichtbar)
        {
            VorschauLeerText = "Kein Termin mehr in der Zukunft - das Enddatum liegt bereits zurück.";
        }

        AktualisiereRueckstand(geprueft);
        AktualisiereStartVorverlegtHinweis(geprueft.StartDate);
    }

    // Was beim Speichern rueckwirkend entstehen wuerde. GeneratedThrough
    // haelt das bei bestehenden Vorlagen von selbst klein - genau deshalb
    // wird es hier durchgereicht statt ignoriert.
    private void AktualisiereRueckstand(RecurringExpenseValidation geprueft)
    {
        if (!IstAktiv)
        {
            // Eine inaktive Vorlage erzeugt nichts, auch nicht rueckwirkend.
            return;
        }

        var rueckstand = RecurrenceGenerator.GetDueOccurrences(
            geprueft.StartDate, geprueft.EndDate, AusgewaehlteIntervallEinheit.Wert,
            geprueft.IntervalCount, geprueft.AnchorDay, GeneratedThrough, _heute);

        RueckwirkendAnzahl = rueckstand.Count;

        if (rueckstand.Count == 0)
        {
            return;
        }

        RueckwirkendText = rueckstand.Count == 1
            ? $"Beim Speichern wird 1 Buchung rückwirkend erzeugt: {GermanDateInput.ToText(rueckstand[0])}."
            : $"Beim Speichern werden {rueckstand.Count} Buchungen rückwirkend erzeugt, " +
              $"vom {GermanDateInput.ToText(rueckstand[0])} bis zum {GermanDateInput.ToText(rueckstand[^1])}.";
    }

    private void AktualisiereStartVorverlegtHinweis(DateOnly neuesStartdatum)
    {
        if (!IstBestehend
            || GeneratedThrough is not DateOnly erzeugtBis
            || _urspruenglichesStartdatum is not DateOnly altesStartdatum
            || neuesStartdatum >= altesStartdatum)
        {
            return;
        }

        StartVorverlegtHinweis =
            $"Für diese Vorlage wurde bereits bis zum {GermanDateInput.ToText(erzeugtBis)} erzeugt " +
            $"(GeneratedThrough). Ein früheres Startdatum lässt die Buchungen davor deshalb NICHT " +
            $"nachträglich entstehen - das verhindert, dass von Hand gelöschte Buchungen wieder auftauchen.";
    }

    // Ist die Kategorie der Vorlage inzwischen archiviert oder keine
    // Blattkategorie mehr, fehlt sie in den waehlbaren Kategorien. Sie wird
    // dann zusaetzlich eingehaengt, damit sich die Vorlage ohne erzwungenen
    // Kategoriewechsel speichern laesst.
    private static IReadOnlyList<CategoryOption> BaueKategorieVorschlaege(
        RecurringExpense? vorlage, string? kategoriePfad, IReadOnlyList<CategoryOption> waehlbare)
    {
        if (vorlage is null || waehlbare.Any(option => option.Id == vorlage.CategoryId))
        {
            return waehlbare;
        }

        var vorschlaege = waehlbare.ToList();
        vorschlaege.Insert(0, new CategoryOption
        {
            Id = vorlage.CategoryId,
            FullPath = kategoriePfad ?? "(archivierte Kategorie)",

            // Die Farbe dieser nachgetragenen Kategorie ist hier nicht
            // bekannt - sie steht in keinem der uebergebenen Werte. Der
            // Standardwert ist die ehrlichere Angabe als eine geratene
            // Farbe; erkennbar bleibt die Kategorie ueber ihren Pfad.
            Color = CategoryColorPalette.DefaultHex,
        });

        return vorschlaege;
    }

    // Auch ein inzwischen archivierter Zahler muss erhalten bleiben - sonst
    // wuerde ein Speichern die Vorlage stillschweigend umbuchen.
    private static IReadOnlyList<Person> BaueZahlerOptionen(
        RecurringExpense? vorlage, IReadOnlyList<Person> zahler)
    {
        if (vorlage is null || zahler.Any(person => person.Id == vorlage.PayerId))
        {
            return zahler;
        }

        return zahler.Prepend(new Person
        {
            Id = vorlage.PayerId,
            Name = "(archivierter Zahler)",
            IsSelf = false,
            IsArchived = true,
            CreatedUtc = DateTime.UnixEpoch,
        }).ToList();
    }
}
