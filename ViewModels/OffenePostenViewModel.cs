using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Bereich "Offene Posten": nicht beglichene Ausgaben mit fremdem Zahler
/// (Regel 4), gruppiert nach Person mit Zwischen- und Gesamtsumme. Die
/// eigentliche Datenbeschaffung (Join, Kategoriepfad, "Tage offen") steckt
/// komplett in <see cref="OpenItemsRepository"/> (Regel 7) - hier werden
/// die Zeilen nur gruppiert, sortiert und auf Anwenderaktionen reagiert.
/// </summary>
public sealed partial class OffenePostenViewModel : ViewModelBase
{
    // So lange bleibt der Rueckgaengig-Hinweis nach dem Abhaken stehen,
    // bevor er automatisch verschwindet (siehe ZeigeRueckgaengigHinweisAsync).
    private static readonly TimeSpan RueckgaengigDauer = TimeSpan.FromSeconds(6);

    private readonly OpenItemsRepository _openItemsRepository;
    private readonly AppSettingsStore _settingsStore;
    private readonly IMessenger _messenger;
    private CancellationTokenSource? _rueckgaengigCts;
    private IReadOnlyList<int> _rueckgaengigIds = Array.Empty<int>();

    // Welche Personen zugeklappt sind - NICHT welche aufgeklappt sind:
    // aufgeklappt ist der Normalfall, und eine neu hinzugekommene Person
    // soll sichtbar sein und nicht versteckt. Ueberlebt das Neuladen der
    // Liste (Sortieren, Abhaken, BuchungenGeaendertNachricht), damit ein
    // zugeklappter Zahler nicht bei jeder Aenderung wieder aufspringt.
    // Bewusst nicht in den Einstellungen abgelegt: der Zustand gilt fuer
    // die laufende Sitzung, so wie der Aufklappzustand im Report auch.
    private readonly HashSet<string> _zugeklappteZahler = new(StringComparer.CurrentCultureIgnoreCase);

    public ObservableCollection<PersonenGruppe> Gruppen { get; } = new();

    [ObservableProperty]
    private string _gesamtsummeText = string.Empty;

    [ObservableProperty]
    private bool _keineEintraege;

    /// <summary>
    /// Anzahl der tatsaechlich noch offenen (nicht beglichenen) Posten -
    /// unabhaengig vom Schalter "Beglichene der letzten 30 Tage anzeigen".
    /// Fuer das Zaehler-Badge an "Offene Posten" in der Sidebar
    /// (UI/UX-Redesign, Abschnitt 3).
    /// </summary>
    [ObservableProperty]
    private int _anzahlOffenerPosten;

    [ObservableProperty]
    private bool _beglicheneLetzte30TageAnzeigen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatAuswahl))]
    [NotifyCanExecuteChangedFor(nameof(AusgewaehlteAbhakenCommand))]
    private int _anzahlAusgewaehlt;

    public bool HatAuswahl => AnzahlAusgewaehlt > 0;

    [ObservableProperty]
    private bool _rueckgaengigSichtbar;

    [ObservableProperty]
    private string _rueckgaengigText = string.Empty;

    /// <summary>
    /// Ein Schreibfehler beim Abhaken oder Zuruecknehmen. Als Band ueber
    /// der Liste; die Liste bleibt unveraendert stehen, weil auch in der
    /// Datenbank nichts geaendert wurde.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SchreibFehlerSichtbar))]
    private string? _schreibFehlerText;

    public bool SchreibFehlerSichtbar => SchreibFehlerText is not null;

    [RelayCommand]
    private void SchreibFehlerSchliessen() => SchreibFehlerText = null;

    /// <summary>
    /// Klappt die Posten einer Person auf oder zu. Zugeklappt verliert
    /// die Gruppe ihre Auswahl: "Ausgewählte abhaken" greift ueber ALLE
    /// Gruppen hinweg, und was man nicht sieht, soll es nicht mit
    /// erwischen.
    /// </summary>
    [RelayCommand]
    private void GruppeUmschalten(PersonenGruppe? gruppe)
    {
        if (gruppe is null)
        {
            return;
        }

        gruppe.IstAufgeklappt = !gruppe.IstAufgeklappt;

        if (gruppe.IstAufgeklappt)
        {
            _zugeklappteZahler.Remove(gruppe.PersonName);
            return;
        }

        _zugeklappteZahler.Add(gruppe.PersonName);

        foreach (var zeile in gruppe.Zeilen)
        {
            zeile.IstAusgewaehlt = false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumHeaderText))]
    [NotifyPropertyChangedFor(nameof(KategorieHeaderText))]
    [NotifyPropertyChangedFor(nameof(BetragHeaderText))]
    [NotifyPropertyChangedFor(nameof(BemerkungHeaderText))]
    [NotifyPropertyChangedFor(nameof(TageOffenHeaderText))]
    private OpenItemsSortColumn _sortSpalte = OpenItemsSortColumn.Datum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DatumHeaderText))]
    [NotifyPropertyChangedFor(nameof(KategorieHeaderText))]
    [NotifyPropertyChangedFor(nameof(BetragHeaderText))]
    [NotifyPropertyChangedFor(nameof(BemerkungHeaderText))]
    [NotifyPropertyChangedFor(nameof(TageOffenHeaderText))]
    private bool _sortAufsteigend = true;

    // Erst wenn der Konstruktor durch ist, wird eine Aenderung gemerkt -
    // sonst schriebe das Lesen aus den Einstellungen den gerade gelesenen
    // Wert sofort wieder zurueck.
    private bool _sortierungGemerkt;

    partial void OnSortSpalteChanged(OpenItemsSortColumn value) => MerkeSortierung();

    partial void OnSortAufsteigendChanged(bool value) => MerkeSortierung();

    private void MerkeSortierung()
    {
        if (!_sortierungGemerkt)
        {
            return;
        }

        // Ausdruecklich still, wie bei der Ausgabenliste: ein Klick auf
        // einen Spaltenkopf darf keinen Fehlerdialog nach sich ziehen.
        try
        {
            _settingsStore.Save(_settingsStore.Load() with
            {
                OpenItemsSortColumn = SortSpalte,
                OpenItemsSortAscending = SortAufsteigend,
            });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Speichern der Sortierung", ex);
        }
    }

    public string DatumHeaderText => KopfText("Datum", OpenItemsSortColumn.Datum);
    public string KategorieHeaderText => KopfText("Kategorie", OpenItemsSortColumn.Kategorie);
    public string BetragHeaderText => KopfText("Betrag", OpenItemsSortColumn.Betrag);
    public string BemerkungHeaderText => KopfText("Bemerkung", OpenItemsSortColumn.Bemerkung);
    public string TageOffenHeaderText => KopfText("Tage offen", OpenItemsSortColumn.TageOffen);

    public OffenePostenViewModel(
        OpenItemsRepository openItemsRepository,
        AppSettingsStore settingsStore,
        IMessenger messenger)
    {
        _openItemsRepository = openItemsRepository;
        _settingsStore = settingsStore;
        _messenger = messenger;

        // Direkte Feldzuweisung, sonst schreibt MerkeSortierung den gerade
        // gelesenen Wert zurueck.
        var einstellungen = _settingsStore.Load();
        _sortSpalte = einstellungen.OpenItemsSortColumn;
        _sortAufsteigend = einstellungen.OpenItemsSortAscending;
        _sortierungGemerkt = true;

        LadeDaten();

        // Buchungsaenderungen aus anderen Bereichen (Anlegen in "Erfassen",
        // Bearbeiten/Loeschen in der Ausgabenliste, Zusammenfuehren von
        // Kategorien) sollen hier sofort sichtbar werden, nicht erst beim
        // naechsten Navigieren zu "Offene Posten" (Regel 14).
        _messenger.Register<OffenePostenViewModel, BuchungenGeaendertNachricht>(
            this, (empfaenger, _) => empfaenger.LadeDaten());
    }

    /// <summary>
    /// Laedt die Liste neu. Wird bei Navigation zu diesem Bereich
    /// aufgerufen (siehe MainViewModel), damit zwischenzeitlich in
    /// "Erfassen" angelegte Ausgaben ohne Neustart sichtbar werden -
    /// OffenePostenViewModel ist ein DI-Singleton und laedt sonst nur
    /// einmal beim Start.
    /// </summary>
    public void AktualisiereListe() => LadeDaten();

    partial void OnBeglicheneLetzte30TageAnzeigenChanged(bool value) => LadeDaten();

    [RelayCommand]
    private void SpalteSortieren(string spalte)
    {
        var neueSpalte = spalte switch
        {
            "Datum" => OpenItemsSortColumn.Datum,
            "Kategorie" => OpenItemsSortColumn.Kategorie,
            "Betrag" => OpenItemsSortColumn.Betrag,
            "Bemerkung" => OpenItemsSortColumn.Bemerkung,
            "TageOffen" => OpenItemsSortColumn.TageOffen,
            _ => SortSpalte,
        };

        if (neueSpalte == SortSpalte)
        {
            SortAufsteigend = !SortAufsteigend;
        }
        else
        {
            SortSpalte = neueSpalte;
            SortAufsteigend = true;
        }

        foreach (var gruppe in Gruppen)
        {
            SortiereGruppe(gruppe);
        }
    }

    [RelayCommand]
    private async Task Abhaken(OffenerPostenZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Abhaken eines offenen Postens",
            () => _openItemsRepository.SetSettledDate(zeile.Id, DateOnly.FromDateTime(DateTime.Now)));

        if (SchreibFehlerText is not null)
        {
            return;
        }

        // Statt nur der eigenen Liste (LadeDaten) wird die Nachricht
        // gesendet - die eigene Registrierung im Konstruktor ladet dadurch
        // auch neu, zusaetzlich aber jeder andere Bereich mit (Regel 14).
        _messenger.Send(new BuchungenGeaendertNachricht());
        await ZeigeRueckgaengigHinweisAsync(new[] { zeile.Id }, "Als beglichen markiert.");
    }

    [RelayCommand(CanExecute = nameof(HatAuswahl))]
    private async Task AusgewaehlteAbhaken()
    {
        var ausgewaehlte = Gruppen.SelectMany(g => g.Zeilen).Where(z => z.IstAusgewaehlt).ToList();
        if (ausgewaehlte.Count == 0)
        {
            return;
        }

        var heute = DateOnly.FromDateTime(DateTime.Now);

        // Bricht es mittendrin ab, sind die vorher abgehakten Posten
        // bereits geschrieben. Genau deshalb wird die Liste danach in
        // jedem Fall neu geladen - sie zeigt dann den tatsaechlichen
        // Stand und nicht den erhofften.
        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Abhaken mehrerer offener Posten",
            () =>
            {
                foreach (var zeile in ausgewaehlte)
                {
                    _openItemsRepository.SetSettledDate(zeile.Id, heute);
                }
            });

        if (SchreibFehlerText is not null)
        {
            _messenger.Send(new BuchungenGeaendertNachricht());
            return;
        }

        _messenger.Send(new BuchungenGeaendertNachricht());
        await ZeigeRueckgaengigHinweisAsync(
            ausgewaehlte.Select(z => z.Id).ToList(),
            $"{ausgewaehlte.Count} Posten als beglichen markiert.");
    }

    [RelayCommand]
    private void AbweichendesDatumOeffnen(OffenerPostenZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        zeile.AbweichendesDatumText = GermanDateInput.ToText(DateOnly.FromDateTime(DateTime.Now));
        zeile.AbweichendesDatumFehler = null;
        zeile.WaehltAbweichendesDatum = true;
    }

    [RelayCommand]
    private async Task AbweichendesDatumUebernehmen(OffenerPostenZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        if (!GermanDateInput.TryParse(zeile.AbweichendesDatumText, out var datum))
        {
            zeile.AbweichendesDatumFehler = "Das ist kein gültiges Datum. Beispiel: 05.03.2026";
            return;
        }

        // Auch hier die harte Jahresgrenze: ein Begleichungsdatum im Jahr
        // 200 ist derselbe Tippfehler wie bei einer Ausgabe (siehe
        // DatePlausibility). Eine Rueckfrage gibt es hier bewusst nicht -
        // das Feld steht direkt in der Zeile und ist in einem Zug wieder
        // geaendert.
        if (DatePlausibility.Error(datum) is string jahresFehler)
        {
            zeile.AbweichendesDatumFehler = jahresFehler;
            return;
        }

        zeile.WaehltAbweichendesDatum = false;

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Abhaken mit abweichendem Datum",
            () => _openItemsRepository.SetSettledDate(zeile.Id, datum));

        if (SchreibFehlerText is not null)
        {
            return;
        }

        _messenger.Send(new BuchungenGeaendertNachricht());
        await ZeigeRueckgaengigHinweisAsync(new[] { zeile.Id }, "Als beglichen markiert.");
    }

    [RelayCommand]
    private void AbweichendesDatumAbbrechen(OffenerPostenZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        zeile.WaehltAbweichendesDatum = false;
        zeile.AbweichendesDatumFehler = null;
    }

    [RelayCommand]
    private void Rueckgaengig()
    {
        if (_rueckgaengigIds.Count == 0)
        {
            return;
        }

        _rueckgaengigCts?.Cancel();

        var ids = _rueckgaengigIds;

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Zuruecknehmen einer Begleichung",
            () =>
            {
                foreach (var id in ids)
                {
                    _openItemsRepository.SetSettledDate(id, null);
                }
            });

        if (SchreibFehlerText is not null)
        {
            _messenger.Send(new BuchungenGeaendertNachricht());
            return;
        }

        _rueckgaengigIds = Array.Empty<int>();
        RueckgaengigSichtbar = false;
        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    /// <summary>
    /// Dauerhafter Rueckgaengig-Button an jeder beglichenen Zeile (im
    /// Unterschied zu <see cref="Rueckgaengig"/>, dem nur kurzzeitig
    /// sichtbaren Hinweis direkt nach dem Abhaken) - noetig, damit bei
    /// aktivem Schalter "Beglichene der letzten 30 Tage anzeigen" auch
    /// laenger zurueckliegende Begleichungen rueckgaengig gemacht werden
    /// koennen.
    /// </summary>
    [RelayCommand]
    private void RueckgaengigZeile(OffenerPostenZeile? zeile)
    {
        if (zeile is null || !zeile.IstBeglichen)
        {
            return;
        }

        SchreibFehlerText = Schreibvorgang.Versuche(
            "Beim Zuruecknehmen einer Begleichung",
            () => _openItemsRepository.SetSettledDate(zeile.Id, null));

        if (SchreibFehlerText is not null)
        {
            return;
        }

        _messenger.Send(new BuchungenGeaendertNachricht());
    }

    // Bleibt fuer RueckgaengigDauer stehen, damit ein versehentliches
    // Abhaken korrigiert werden kann, und blendet sich danach selbst aus -
    // auesser ein neuerer Aufruf (weiteres Abhaken) hat den Hinweis
    // inzwischen per CancellationTokenSource abgeloest.
    private async Task ZeigeRueckgaengigHinweisAsync(IReadOnlyList<int> ids, string text)
    {
        _rueckgaengigCts?.Cancel();
        var cts = new CancellationTokenSource();
        _rueckgaengigCts = cts;

        _rueckgaengigIds = ids;
        RueckgaengigText = text;
        RueckgaengigSichtbar = true;

        try
        {
            await Task.Delay(RueckgaengigDauer, cts.Token);
            RueckgaengigSichtbar = false;
        }
        catch (TaskCanceledException)
        {
            // Wird bei Klick auf "Rueckgaengig" oder einer weiteren
            // Abhak-Aktion innerhalb der Anzeigedauer ausgeloest.
        }
    }

    private void LadeDaten()
    {
        foreach (var gruppe in Gruppen)
        {
            foreach (var zeile in gruppe.Zeilen)
            {
                zeile.PropertyChanged -= OnZeilePropertyChanged;
            }
        }

        Gruppen.Clear();

        IEnumerable<OpenItem> offeneUndBeglichene = _openItemsRepository.GetOpen();
        if (BeglicheneLetzte30TageAnzeigen)
        {
            var seit = DateOnly.FromDateTime(DateTime.Now).AddDays(-30);
            offeneUndBeglichene = offeneUndBeglichene.Concat(_openItemsRepository.GetRecentlySettled(seit));
        }

        foreach (var gruppierung in offeneUndBeglichene
                     .GroupBy(item => item.PayerName)
                     .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var personenGruppe = new PersonenGruppe(gruppierung.Key);

            foreach (var zeile in gruppierung.Select(item => new OffenerPostenZeile(item)))
            {
                zeile.PropertyChanged += OnZeilePropertyChanged;
                personenGruppe.Zeilen.Add(zeile);
            }

            SortiereGruppe(personenGruppe);
            personenGruppe.AktualisiereZwischensumme();
            personenGruppe.IstAufgeklappt = !_zugeklappteZahler.Contains(gruppierung.Key);
            Gruppen.Add(personenGruppe);
        }

        AnzahlAusgewaehlt = 0;
        KeineEintraege = Gruppen.Count == 0;
        AnzahlOffenerPosten = Gruppen.SelectMany(g => g.Zeilen).Count(z => !z.IstBeglichen);
        AktualisiereGesamtsumme();
    }

    private void SortiereGruppe(PersonenGruppe gruppe)
    {
        var sortiert = Sortiert(gruppe.Zeilen).ToList();
        gruppe.Zeilen.Clear();
        foreach (var zeile in sortiert)
        {
            gruppe.Zeilen.Add(zeile);
        }
    }

    private IEnumerable<OffenerPostenZeile> Sortiert(IEnumerable<OffenerPostenZeile> zeilen)
    {
        Func<OffenerPostenZeile, IComparable> schluessel = SortSpalte switch
        {
            OpenItemsSortColumn.Datum => z => z.ExpenseDate,
            OpenItemsSortColumn.Kategorie => z => z.CategoryFullPath,
            OpenItemsSortColumn.Betrag => z => z.AmountCents,
            OpenItemsSortColumn.Bemerkung => z => z.Note ?? string.Empty,
            OpenItemsSortColumn.TageOffen => z => z.TageOffen,
            _ => z => z.ExpenseDate,
        };

        return SortAufsteigend ? zeilen.OrderBy(schluessel) : zeilen.OrderByDescending(schluessel);
    }

    private void OnZeilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OffenerPostenZeile.IstAusgewaehlt))
        {
            AnzahlAusgewaehlt = Gruppen.SelectMany(g => g.Zeilen).Count(z => z.IstAusgewaehlt);
        }
    }

    private void AktualisiereGesamtsumme()
    {
        // Bewusst ohne Fallunterscheidung nach IstEinnahme - siehe
        // PersonenGruppe.AktualisiereZwischensumme.
        var summeCents = Gruppen.SelectMany(g => g.Zeilen)
            .Where(z => !z.IstBeglichen)
            .Sum(z => z.AmountCents);
        GesamtsummeText = EuroText.Format(summeCents);
    }

    private string KopfText(string bezeichnung, OpenItemsSortColumn spalte) =>
        SortSpalte == spalte ? $"{bezeichnung} {(SortAufsteigend ? "▲" : "▼")}" : bezeichnung;
}
