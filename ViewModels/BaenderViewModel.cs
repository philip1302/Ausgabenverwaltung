using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Core.Errors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die eine Bandzone ganz oben, gemeinsam fuer alle app-weiten Hinweise.
///
/// Vorher hatte jede Meldung ihr eigenes, dauerhaft angedocktes Band:
/// Aktualisierung, Sicherungsfehler und erzeugte wiederkehrende Buchungen
/// standen im schlechtesten Fall zu dritt uebereinander und schoben den
/// Inhalt um ein Vielfaches ihrer eigenen Hoehe nach unten. Jetzt gibt es
/// eine Zone, die hoechstens ein Band zeigt; die uebrigen lassen sich
/// durchblaettern (siehe Core.Errors.Bandauswahl).
///
/// Welches Band vorn steht, entscheidet Core - hier steht nur die
/// Verdrahtung (Regel 7).
/// </summary>
public sealed partial class BaenderViewModel : ViewModelBase
{
    // Reihenfolge des Eintreffens. Die Rangfolge macht Bandauswahl
    // daraus, nicht diese Liste.
    private readonly List<Bandeintrag> _eintraege = [];

    // Stand des Durchblaetterns, 1-basiert. Wird von Bandauswahl in den
    // gueltigen Bereich gebracht, muss hier also nicht selbst gehuetet
    // werden.
    private int _nummer = 1;

    private Bandlage _lage = Bandlage.Leer;

    /// <summary>
    /// Der Langtext des gerade gezeigten Bandes, solange er aufgeschlagen
    /// ist. NULL = die Auflage ist zu.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsAktiv))]
    private string? _detailsText;

    [ObservableProperty]
    private string _detailsTitel = string.Empty;

    public bool DetailsAktiv => DetailsText is not null;

    private Bandeintrag? Aktuell => _lage.Sichtbar ? _eintraege[_lage.Index] : null;

    public bool Sichtbar => _lage.Sichtbar;

    public string Titel => Aktuell?.Meldung.Titel ?? string.Empty;

    public string Kurz => Aktuell?.Meldung.Kurz ?? string.Empty;

    /// <summary>"2 von 3" - leer, solange nur ein Band ansteht.</summary>
    public string Zaehltext => _lage.Zaehltext ?? string.Empty;

    public bool MehrereVorhanden => _lage.MehrereVorhanden;

    public bool DetailsVorhanden => Aktuell?.Meldung.HatDetails ?? false;

    public bool AktionVorhanden => Aktuell?.AktionText is not null;

    public string AktionText => Aktuell?.AktionText ?? string.Empty;

    public string AktionTipp => Aktuell?.AktionTipp ?? string.Empty;

    // Vier Umschalter statt eines Rangs als Bindung: die Oberflaeche
    // setzt damit Classes.hinweis/.warnung/... direkt, ohne Wandler.
    public bool IstHinweis => Aktuell?.Meldung.Rang == Bandrang.Hinweis;
    public bool IstErfolg => Aktuell?.Meldung.Rang == Bandrang.Erfolg;
    public bool IstWarnung => Aktuell?.Meldung.Rang == Bandrang.Warnung;
    public bool IstFehler => Aktuell?.Meldung.Rang == Bandrang.Fehler;

    /// <summary>
    /// Stellt ein Band ein. Ein bereits vorhandenes desselben Absenders
    /// wird ersetzt - so loest etwa die Meldung "Neustart hat nicht
    /// geklappt" die Meldung "Fassung ist bereit" ab, statt sich daneben
    /// zu stellen.
    /// </summary>
    public void Zeige(Bandeintrag eintrag)
    {
        var vorhanden = _eintraege.FindIndex(e => e.Schluessel == eintrag.Schluessel);

        if (vorhanden >= 0)
        {
            _eintraege[vorhanden] = eintrag;
        }
        else
        {
            _eintraege.Add(eintrag);
        }

        // Ein neues Band springt nach vorn: der Anwender soll nicht erst
        // blaettern muessen, um zu sehen, was gerade passiert ist.
        _nummer = 1;
        Aktualisiere();
    }

    /// <summary>Nimmt ein Band zurueck, ohne dass der Anwender es schliessen musste.</summary>
    public void Entferne(string schluessel)
    {
        if (_eintraege.RemoveAll(e => e.Schluessel == schluessel) > 0)
        {
            Aktualisiere();
        }
    }

    /// <summary>Blaettert zum naechsten anstehenden Band und am Ende wieder nach vorn.</summary>
    [RelayCommand]
    private void Weiter()
    {
        if (!_lage.MehrereVorhanden)
        {
            return;
        }

        _nummer = _lage.Nummer >= _lage.Gesamt ? 1 : _lage.Nummer + 1;
        Aktualisiere();
    }

    /// <summary>Schlaegt den Langtext des gezeigten Bandes auf.</summary>
    [RelayCommand]
    private void Details()
    {
        if (Aktuell is not { Meldung.HatDetails: true } eintrag)
        {
            return;
        }

        DetailsTitel = eintrag.Meldung.Titel;
        DetailsText = eintrag.Meldung.Details;
    }

    [RelayCommand]
    private void DetailsSchliessen() => DetailsText = null;

    /// <summary>Loest den einen hervorgehobenen Knopf des gezeigten Bandes aus.</summary>
    [RelayCommand]
    private void Ausfuehren() => Aktuell?.Aktion?.Invoke();

    /// <summary>
    /// Schliesst NUR das gerade gezeigte Band. Die uebrigen stehen weiter
    /// an - "Schließen" auf einem Band, das drei Meldungen auf einmal
    /// verschwinden laesst, waere ein Verlust, den niemand bemerkt.
    /// </summary>
    [RelayCommand]
    private void Schliessen()
    {
        if (Aktuell is not { } eintrag)
        {
            return;
        }

        _eintraege.Remove(eintrag);
        eintrag.BeimSchliessen?.Invoke();

        // Auf der Stelle stehen bleiben, nicht nach vorn springen: wer das
        // dringendste Band weggeklickt hat, will das naechste sehen, und
        // das steht nach dem Entfernen an derselben Nummer.
        Aktualisiere();
    }

    private void Aktualisiere()
    {
        _lage = Bandauswahl.Waehle(
            _eintraege.Select(e => e.Meldung).ToList(), _nummer);

        _nummer = _lage.Sichtbar ? _lage.Nummer : 1;

        // Alles Sichtbare haengt an _lage und Aktuell; beides sind
        // abgeleitete Werte ohne eigenes Feld, deshalb hier von Hand
        // gemeldet.
        OnPropertyChanged(nameof(Sichtbar));
        OnPropertyChanged(nameof(Titel));
        OnPropertyChanged(nameof(Kurz));
        OnPropertyChanged(nameof(Zaehltext));
        OnPropertyChanged(nameof(MehrereVorhanden));
        OnPropertyChanged(nameof(DetailsVorhanden));
        OnPropertyChanged(nameof(AktionVorhanden));
        OnPropertyChanged(nameof(AktionText));
        OnPropertyChanged(nameof(AktionTipp));
        OnPropertyChanged(nameof(IstHinweis));
        OnPropertyChanged(nameof(IstErfolg));
        OnPropertyChanged(nameof(IstWarnung));
        OnPropertyChanged(nameof(IstFehler));
    }
}
