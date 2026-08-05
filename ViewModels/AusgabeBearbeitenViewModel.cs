using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Der Bearbeiten-Dialog der Ausgabenliste. Bewusst genau die Felder der
/// Erfassungsmaske (siehe <see cref="ErfassenViewModel"/>) mit denselben
/// Pruefungen - <see cref="Money.TryParseEuroText"/> und
/// <see cref="GermanDateInput.TryParse"/> liegen in Core, hier wird nur
/// gebunden und die Fehlermeldung gesetzt (Regel 7).
///
/// SettledDate ("bezahlt am") ist hier bewusst ENGER gefasst als im
/// Bereich "Offene Posten": es gibt kein "heute abhaken" - das bleibt
/// dort. Bei einem fremden Zahler laesst sich das Datum aber direkt
/// korrigieren (<see cref="BezahltAmSichtbar"/>, Regel 4: nur relevant,
/// wenn der Zahler nicht die eigene Person ist), etwa wenn es beim
/// Abhaken falsch eingetragen wurde.
/// </summary>
public sealed partial class AusgabeBearbeitenViewModel : ObservableObject
{
    public int ExpenseId { get; }

    /// <summary>
    /// Hinweis fuer aus einer Vorlage erzeugte Buchungen: die Aenderung
    /// betrifft nur diese eine Buchung (Regel 6 - Betraege werden beim
    /// Erzeugen kopiert, die Historie haengt nicht an der Vorlage).
    /// </summary>
    public bool IstAusVorlage { get; }

    public string VorlageHinweis { get; }

    public IReadOnlyList<CategoryOption> KategorieVorschlaege { get; }
    public IReadOnlyList<Person> ZahlerOptionen { get; }

    [ObservableProperty]
    private string _betragText;

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
    private string _datumText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumFehlerSichtbar))]
    private string? _datumFehler;

    public bool DatumFehlerSichtbar => !string.IsNullOrEmpty(DatumFehler);

    [ObservableProperty]
    private string? _bemerkung;

    /// <summary>
    /// Ob dieser Betrag eine allgemeine Einnahme ist statt einer Ausgabe
    /// (siehe Entities.Expense.IsIncome) - mindert die Summen in Liste
    /// und Auswertung, statt sie zu erhoehen.
    /// </summary>
    [ObservableProperty]
    private bool _istEinnahme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BezahltAmSichtbar))]
    private Person _ausgewaehlterZahler;

    /// <summary>
    /// "Bezahlt am" ist nur bei einem fremden Zahler ueberhaupt gemeint
    /// (Regel 4) - bei der eigenen Person bleibt das Feld unsichtbar und
    /// SettledDate unangetastet.
    /// </summary>
    public bool BezahltAmSichtbar => !AusgewaehlterZahler.IsSelf;

    [ObservableProperty]
    private string _bezahltAmText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BezahltAmFehlerSichtbar))]
    private string? _bezahltAmFehler;

    public bool BezahltAmFehlerSichtbar => !string.IsNullOrEmpty(BezahltAmFehler);

    public AusgabeBearbeitenViewModel(
        AusgabeZeile zeile,
        IReadOnlyList<CategoryOption> waehlbareKategorien,
        IReadOnlyList<Person> zahler)
    {
        ExpenseId = zeile.Id;
        IstAusVorlage = zeile.IstAusVorlage;
        VorlageHinweis = zeile.VorlageHinweis;
        IstEinnahme = zeile.IstEinnahme;

        // Ist die Kategorie der Buchung inzwischen archiviert oder keine
        // Blattkategorie mehr, fehlt sie in den waehlbaren Kategorien. Sie
        // wird dann zusaetzlich eingehaengt, damit sich die Buchung ohne
        // erzwungenen Kategoriewechsel speichern laesst.
        var vorschlaege = waehlbareKategorien.ToList();
        if (vorschlaege.All(option => option.Id != zeile.CategoryId))
        {
            vorschlaege.Insert(0, new CategoryOption
            {
                Id = zeile.CategoryId,
                FullPath = zeile.CategoryFullPath,

                // Die Zeile kennt ihre aufgeloeste Farbe bereits - die
                // nachgetragene Kategorie sieht damit genauso aus wie in
                // der Liste dahinter.
                Color = zeile.FarbeHex,
            });
        }

        KategorieVorschlaege = vorschlaege;
        _ausgewaehlteKategorie = vorschlaege.First(option => option.Id == zeile.CategoryId);

        ZahlerOptionen = zahler;

        // Auch ein inzwischen archivierter Zahler muss erhalten bleiben -
        // sonst wuerde ein Speichern die Buchung stillschweigend umbuchen.
        _ausgewaehlterZahler = zahler.FirstOrDefault(person => person.Id == zeile.PayerId)
            ?? new Person
            {
                Id = zeile.PayerId,
                Name = zeile.PayerName,
                IsSelf = zeile.PayerIsSelf,
                IsArchived = true,
                CreatedUtc = DateTime.UnixEpoch,
            };

        if (ZahlerOptionen.All(person => person.Id != _ausgewaehlterZahler.Id))
        {
            ZahlerOptionen = zahler.Prepend(_ausgewaehlterZahler).ToList();
        }

        // EuroText.Plain und nicht EuroText.Format: in ein Eingabefeld
        // gehoert die blanke Zahl (das €-Zeichen steht als Beschriftung
        // daneben), und der Tausenderpunkt wuerde beim Speichern
        // abgelehnt (siehe Money.TryParseEuroText).
        _betragText = EuroText.Plain(zeile.AmountCents);
        _datumText = GermanDateInput.ToText(zeile.ExpenseDate);
        _bemerkung = zeile.Note;

        // Leer = noch offen, sonst dasselbe Textformat wie beim
        // Buchungsdatum. Nur bei fremdem Zahler ueberhaupt aussagekraeftig
        // (siehe BezahltAmSichtbar), wird aber unabhaengig davon befuellt -
        // ein spaeterer Zahlerwechsel zurueck zu "fremd" soll den Wert
        // nicht verloren haben.
        _bezahltAmText = zeile.SettledDate is DateOnly bezahlt
            ? GermanDateInput.ToText(bezahlt)
            : string.Empty;
    }

    /// <summary>
    /// Die Rueckfrage bei einem ungewoehnlichen, aber moeglichen Datum -
    /// dieselbe Regel wie in der Erfassungsmaske (siehe
    /// <see cref="DatePlausibility"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumRueckfrageSichtbar))]
    private string? _datumRueckfrageText;

    public bool DatumRueckfrageSichtbar => DatumRueckfrageText is not null;

    private bool _datumBestaetigt;

    /// <summary>
    /// Ein Fehler beim Schreiben. Steht im Dialog, und der Dialog bleibt
    /// dabei offen und vollstaendig gefuellt.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeicherFehlerSichtbar))]
    private string? _speicherFehlerText;

    public bool SpeicherFehlerSichtbar => SpeicherFehlerText is not null;

    // Eine Aenderung am Datum nimmt eine erteilte Bestaetigung zurueck.
    partial void OnDatumTextChanged(string value)
    {
        _datumBestaetigt = false;
        DatumRueckfrageText = null;
    }

    // Beim Ankreuzen auf die Ich-Person vorbelegen, sofern gerade ein
    // fremder Zahler gewaehlt ist - dasselbe Verhalten wie in der
    // Erfassungsmaske (siehe ErfassenViewModel.OnIstEinnahmeChanged).
    partial void OnIstEinnahmeChanged(bool value)
    {
        if (value && !AusgewaehlterZahler.IsSelf)
        {
            AusgewaehlterZahler = ZahlerOptionen.First(person => person.IsSelf);
        }
    }

    /// <summary>Der Anwender bestaetigt das ungewoehnliche Datum.</summary>
    [RelayCommand]
    private void DatumBestaetigen()
    {
        _datumBestaetigt = true;
        DatumRueckfrageText = null;
    }

    [RelayCommand]
    private void DatumRueckfrageAbbrechen() => DatumRueckfrageText = null;

    /// <summary>
    /// Prueft alle Felder und liefert die uebernehmbaren Werte. Setzt bei
    /// Fehlern die Meldungen an allen betroffenen Feldern gleichzeitig -
    /// nicht nur am ersten -, damit nicht mehrfach gespeichert werden muss.
    ///
    /// Liefert auch dann false, wenn noch eine Rueckfrage offen ist: das
    /// Datum ist dann in Ordnung, aber ungewoehnlich, und der Anwender
    /// soll es einmal bestaetigen.
    /// </summary>
    public bool TryLeseWerte(out long amountCents, out DateOnly expenseDate, out DateOnly? settledDate)
    {
        // Dieselbe Pruefung wie in der Erfassungsmaske, aus Core (Regel 7).
        var pruefung = ExpenseValidator.Validate(new ExpenseInput
        {
            AmountText = BetragText,
            IsIncome = IstEinnahme,
            CategoryId = AusgewaehlteKategorie?.Id,
            PayerId = AusgewaehlterZahler.Id,
            DateText = DatumText,
            Today = DateOnly.FromDateTime(DateTime.Now),
        });

        amountCents = pruefung.AmountCents;
        expenseDate = pruefung.Date;
        settledDate = null;

        BetragFehler = pruefung.AmountError;
        KategorieFehler = pruefung.CategoryError;
        DatumFehler = pruefung.DateError;

        // Bei der eigenen Person wird SettledDate nie ausgewertet
        // (Regel 4) - das Feld bleibt dann unsichtbar, und was auch immer
        // noch darin steht, wird ignoriert statt geprueft.
        if (BezahltAmSichtbar && !TryLeseBezahltAm(out settledDate))
        {
            return false;
        }

        if (!pruefung.IsValid)
        {
            return false;
        }

        if (pruefung.NeedsConfirmation && !_datumBestaetigt)
        {
            DatumRueckfrageText = pruefung.DateConfirmation;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Leer bedeutet weiterhin offen (NULL) - kein Fehler. Dieselbe
    /// Pruefung wie beim abweichenden Datum in der Offene-Posten-Liste
    /// (siehe OffenePostenViewModel.AbweichendesDatumUebernehmen), nur
    /// ohne Rueckfrage: das Feld steht direkt im Formular und ist in
    /// einem Zug wieder geaendert.
    /// </summary>
    private bool TryLeseBezahltAm(out DateOnly? settledDate)
    {
        settledDate = null;
        BezahltAmFehler = null;

        if (string.IsNullOrWhiteSpace(BezahltAmText))
        {
            return true;
        }

        if (!GermanDateInput.TryParse(BezahltAmText, out var datum))
        {
            BezahltAmFehler = "Das ist kein gültiges Datum. Beispiel: 05.03.2026";
            return false;
        }

        if (DatePlausibility.Error(datum) is string jahresFehler)
        {
            BezahltAmFehler = jahresFehler;
            return false;
        }

        settledDate = datum;
        return true;
    }

    public string? BemerkungOderNull =>
        string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung;
}
