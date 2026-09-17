using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Die kurzen Bestaetigungen ("Gespeichert.", "Abgehakt.") - als
/// schwebender Hinweis unten rechts, nicht als Band im Inhalt.
///
/// Warum getrennt von den Baendern: eine Bestaetigung meldet keinen
/// Zustand, sie quittiert eine Handlung. Sie ist in dem Augenblick
/// vorbei, in dem man sie gelesen hat. Als Band im Layout hatte sie zwei
/// Nachteile, die beide nichts mit ihrem Inhalt zu tun haben: sie schob
/// beim Erscheinen alles darunter nach unten und beim Verschwinden wieder
/// hoch - genau waehrend man weitertippt -, und sie belegte den Platz der
/// echten Zustandsmeldungen.
///
/// Die Anzeigedauer war an ihren bisherigen Stellen zwei Sekunden. Das
/// ist knapp: die gaengige Empfehlung liegt bei vier bis acht, weil ein
/// Hinweis, der schon weg ist, bevor der Blick ihn findet, so gut ist wie
/// keiner.
/// </summary>
public sealed partial class ToastViewModel : ViewModelBase
{
    /// <summary>
    /// Wie lange ein Hinweis stehen bleibt. Fuenf Sekunden - lang genug,
    /// um ihn zu bemerken, kurz genug, um nicht im Weg zu stehen.
    /// </summary>
    public static readonly TimeSpan Anzeigedauer = TimeSpan.FromSeconds(5);

    // Loest den vorigen Hinweis ab: wer zweimal schnell hintereinander
    // speichert, soll den zweiten Hinweis volle Dauer sehen und nicht den
    // Rest der Dauer des ersten.
    private CancellationTokenSource? _laufend;

    private Action? _aktion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sichtbar))]
    private string? _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AktionVorhanden))]
    private string? _aktionText;

    public bool Sichtbar => Text is not null;

    public bool AktionVorhanden => AktionText is not null;

    /// <summary>Eine reine Bestaetigung ohne Knopf.</summary>
    public void Zeige(string text) => Zeige(text, null, null);

    /// <summary>
    /// Eine Bestaetigung mit genau einer Rueckholmoeglichkeit
    /// ("Rückgängig"). Mehr als einen Knopf bekommt ein Toast nicht - wer
    /// eine Wahl treffen soll, braucht etwas, das nicht von selbst
    /// verschwindet.
    /// </summary>
    public void Zeige(string text, string? aktionText, Action? aktion)
    {
        _laufend?.Cancel();
        var eigene = new CancellationTokenSource();
        _laufend = eigene;

        Text = text;
        AktionText = aktionText;
        _aktion = aktion;

        _ = BlendeAusAsync(eigene.Token);
    }

    /// <summary>
    /// Nimmt den Hinweis vorzeitig zurueck - etwa beim Bereichswechsel,
    /// wo eine Bestaetigung zu einer nicht mehr sichtbaren Liste nur noch
    /// raetselhaft ist.
    /// </summary>
    [RelayCommand]
    public void Schliessen()
    {
        _laufend?.Cancel();
        _laufend = null;
        Text = null;
        AktionText = null;
        _aktion = null;
    }

    /// <summary>Loest den einen Knopf aus und raeumt den Hinweis weg.</summary>
    [RelayCommand]
    private void Ausfuehren()
    {
        var aktion = _aktion;
        Schliessen();
        aktion?.Invoke();
    }

    private async Task BlendeAusAsync(CancellationToken abbruch)
    {
        try
        {
            await Task.Delay(Anzeigedauer, abbruch);
        }
        catch (TaskCanceledException)
        {
            // Abgeloest oder von Hand geschlossen - dann hat der Nachfolger
            // die Anzeige bereits uebernommen und darf nicht geraeumt
            // werden.
            return;
        }

        Text = null;
        AktionText = null;
        _aktion = null;
    }
}
