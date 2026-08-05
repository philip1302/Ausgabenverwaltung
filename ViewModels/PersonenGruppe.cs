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

    [ObservableProperty]
    private bool _zwischensummeIstErstattung;

    public PersonenGruppe(string personName)
    {
        PersonName = personName;
    }

    public void AktualisiereZwischensumme()
    {
        // Bewusst ohne Fallunterscheidung nach IstEinnahme: eine offene
        // Ausgabe und eine offene Einnahme schulden mir beide Geld in
        // derselben Richtung (siehe OpenItemsRepository.GetOpenSumsByPayer).
        var summeCents = Zeilen
            .Where(z => !z.IstBeglichen)
            .Sum(z => z.AmountCents);
        ZwischensummeText = EuroText.Format(summeCents);
        ZwischensummeIstErstattung = EuroText.IsNegative(summeCents);
    }
}
