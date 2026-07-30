using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Der Bearbeiten-Dialog der Ausgabenliste. Bewusst genau die Felder der
/// Erfassungsmaske (siehe <see cref="ErfassenViewModel"/>) mit denselben
/// Pruefungen - <see cref="Money.TryParseEuroText"/> und
/// <see cref="GermanDateInput.TryParse"/> liegen in Core, hier wird nur
/// gebunden und die Fehlermeldung gesetzt (Regel 7).
///
/// SettledDate ist absichtlich KEIN Feld: die Erfassungsmaske hat es auch
/// nicht, und das Abhaken bleibt Aufgabe des Bereichs "Offene Posten".
/// Der bestehende Wert wird beim Speichern unveraendert durchgereicht.
/// </summary>
public sealed partial class AusgabeBearbeitenViewModel : ObservableObject
{
    public int ExpenseId { get; }

    /// <summary>Unveraendert durchzureichender Begleichungsstand (Regel 4).</summary>
    public DateOnly? SettledDate { get; }

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

    [ObservableProperty]
    private Person _ausgewaehlterZahler;

    public AusgabeBearbeitenViewModel(
        AusgabeZeile zeile,
        IReadOnlyList<CategoryOption> waehlbareKategorien,
        IReadOnlyList<Person> zahler)
    {
        ExpenseId = zeile.Id;
        SettledDate = zeile.SettledDate;
        IstAusVorlage = zeile.IstAusVorlage;
        VorlageHinweis = zeile.VorlageHinweis;

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

        // "0.00" statt "N2": Money.TryParseEuroText lehnt
        // Tausendertrennzeichen bewusst ab, ein vorbelegtes "1.234,56"
        // liesse sich also nicht wieder speichern.
        _betragText = Money.ToDecimal(zeile.AmountCents)
            .ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        _datumText = GermanDateInput.ToText(zeile.ExpenseDate);
        _bemerkung = zeile.Note;
    }

    /// <summary>
    /// Prueft alle Felder und liefert die uebernehmbaren Werte. Setzt bei
    /// Fehlern die Meldungen an allen betroffenen Feldern gleichzeitig -
    /// nicht nur am ersten -, damit nicht mehrfach gespeichert werden muss.
    /// </summary>
    public bool TryLeseWerte(out long amountCents, out DateOnly expenseDate)
    {
        var betragGueltig = Money.TryParseEuroText(BetragText, out amountCents);
        BetragFehler = betragGueltig ? null : "Ungueltiger Betrag (Beispiel: 12,50).";

        KategorieFehler = AusgewaehlteKategorie is null ? "Bitte eine Kategorie waehlen." : null;

        var datumGueltig = GermanDateInput.TryParse(DatumText, out expenseDate);
        DatumFehler = datumGueltig ? null : "Ungueltiges Datum (TT.MM.JJJJ).";

        return betragGueltig && datumGueltig && AusgewaehlteKategorie is not null;
    }

    public string? BemerkungOderNull =>
        string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung;
}
