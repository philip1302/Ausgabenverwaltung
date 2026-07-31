using System;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Ausgabenverwaltung.Views;

/// <summary>
/// Der Dialog fuer einen unerwarteten Fehler.
///
/// Er zeigt nur an: Ueberschrift, Meldung und technischen Text kommen
/// fertig formuliert aus Core (<see cref="UnexpectedErrorText"/>), hier
/// wird nichts entschieden und nichts formuliert (Regel 7).
///
/// Die Antwort des Anwenders steht danach in <see cref="Antwort"/>. Das
/// Fenster beendet die Anwendung NICHT selbst - wer es geoeffnet hat,
/// entscheidet, was daraus folgt. Ein Fenster, das je nach Umstand mal die
/// Anwendung schliesst und mal nicht, waere schwer zu ueberblicken.
/// </summary>
public partial class FehlerDialog : Window
{
    private ErrorReport? _bericht;

    public FehlerDialog()
    {
        InitializeComponent();
    }

    public FehlerDialog(ErrorReport bericht, bool fortfahrenMoeglich) : this()
    {
        _bericht = bericht;

        TitelText.Text = bericht.Title;
        MeldungText.Text = bericht.Message;
        TechnikText.Text = bericht.Technical;

        // Ohne Protokoll fuehrt die Schaltflaeche ins Leere - dann gibt es
        // sie nicht.
        ProtokollButton.IsVisible = bericht.LogFolderPath is not null;

        // Nach einem Fehler, der den Prozess ohnehin beendet, waere
        // "Fortfahren" eine Zusage, die niemand einhalten kann.
        FortfahrenButton.IsVisible = fortfahrenMoeglich;

        // Die vorausgewaehlte Schaltflaeche folgt der Empfehlung: wer
        // Enter drueckt, ohne zu lesen, tut damit das Vernuenftigere.
        if (fortfahrenMoeglich && bericht.Recommendation == ErrorRecommendation.Continue)
        {
            FortfahrenButton.IsDefault = true;
        }
        else
        {
            BeendenButton.IsDefault = true;
        }
    }

    /// <summary>
    /// Was der Anwender gewaehlt hat. Vorbelegt mit
    /// <see cref="FehlerAntwort.Beenden"/>: schliesst jemand das Fenster
    /// ueber das Kreuz, ist das keine Zustimmung zum Weiterarbeiten.
    /// </summary>
    public FehlerAntwort Antwort { get; private set; } = FehlerAntwort.Beenden;

    private void OnFortfahrenClick(object? sender, RoutedEventArgs e)
    {
        Antwort = FehlerAntwort.Fortfahren;
        Close();
    }

    private void OnBeendenClick(object? sender, RoutedEventArgs e)
    {
        Antwort = FehlerAntwort.Beenden;
        Close();
    }

    private void OnProtokollordnerOeffnenClick(object? sender, RoutedEventArgs e)
    {
        if (Ordner.Oeffne(_bericht?.LogFolderPath))
        {
            return;
        }

        // Laesst sich der Ordner nicht oeffnen, bleibt wenigstens der
        // Pfad zum Abtippen stehen. Ein zweiter Fehlerdialog ueber einen
        // Fehlerdialog waere niemandem eine Hilfe.
        ProtokollButton.IsEnabled = false;
        ProtokollButton.Content = _bericht?.LogFolderPath ?? "Kein Protokollordner";
    }

    private async void OnKopierenClick(object? sender, RoutedEventArgs e)
    {
        var zwischenablage = GetTopLevel(this)?.Clipboard;
        if (zwischenablage is null)
        {
            return;
        }

        try
        {
            await zwischenablage.SetTextAsync(_bericht?.Technical ?? string.Empty);
            KopiertHinweis.IsVisible = true;
        }
        catch (Exception ex)
        {
            // Auch hier kein zweiter Dialog - der Text steht ja im Feld
            // darueber und laesst sich von Hand markieren.
            AppLog.Current.Exception("Beim Kopieren der technischen Angaben", ex);
            KopiertHinweis.Text = "Das Kopieren hat nicht geklappt — bitte den Text oben markieren.";
            KopiertHinweis.IsVisible = true;
        }
    }
}
