using System.Collections.ObjectModel;
using System.Linq;
using Ausgabenverwaltung.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Alle Offene-Posten-Zeilen einer Person mit Zwischensumme. Die
/// Zwischensumme zaehlt nur noch offene Zeilen - beglichene Posten, die
/// ueber den Schalter "Beglichene der letzten 30 Tage anzeigen"
/// zusaetzlich eingeblendet werden, sollen die tatsaechlich offene Summe
/// nicht verfaelschen.
/// </summary>
public sealed partial class PersonenGruppe : ObservableObject
{
    public string PersonName { get; }

    public ObservableCollection<OffenerPostenZeile> Zeilen { get; } = new();

    [ObservableProperty]
    private string _zwischensummeText = string.Empty;

    /// <summary>
    /// Ob die Zeilen dieser Person sichtbar sind. Zugeklappt bleiben
    /// Name, Anzahl und Zwischensumme stehen - wer nur wissen will,
    /// was mit jemandem offen ist, braucht die Einzelposten nicht.
    /// Aufgeklappt ist der Anfangszustand; welche Person zugeklappt
    /// ist, merkt sich <see cref="OffenePostenViewModel"/> ueber das
    /// Neuladen hinweg.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AufklappZeichen))]
    private bool _istAufgeklappt = true;

    /// <summary>
    /// Dieselben zwei Zeichen wie im Report und im Jahresrueckblick
    /// (<see cref="ReportZeile.AufklappZeichen"/>) - es gibt in dieser
    /// Anwendung nur EINEN Aufklapp-Pfeil.
    /// </summary>
    public string AufklappZeichen => IstAufgeklappt ? "▾" : "▸";

    /// <summary>
    /// "3 offene Posten" neben dem Namen. Steht auch im zugeklappten
    /// Zustand da und sagt dann, wie viel man gerade verbirgt.
    /// </summary>
    [ObservableProperty]
    private string _anzahlOffenText = string.Empty;

    public PersonenGruppe(string personName)
    {
        PersonName = personName;
    }

    public void AktualisiereZwischensumme()
    {
        // Bewusst ohne Fallunterscheidung nach IstEinnahme: eine offene
        // Ausgabe und eine offene Einnahme schulden mir beide Geld in
        // derselben Richtung (siehe OpenItemsRepository.GetOpenSumsByPayer).
        // Deshalb auch keine Farbe: das Vorzeichen sagt hier nichts ueber
        // Gewinn/Verlust aus wie im Report, nur "wie viel liegt offen".
        var offene = Zeilen.Where(z => !z.IstBeglichen).ToList();

        var summeCents = offene.Sum(z => z.AmountCents);
        ZwischensummeText = EuroText.Format(summeCents);

        AnzahlOffenText = offene.Count == 1
            ? "1 offener Posten"
            : $"{offene.Count} offene Posten";
    }
}
