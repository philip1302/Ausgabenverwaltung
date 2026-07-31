using System;
using System.Diagnostics;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Startup;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Ausgabenverwaltung.Views;

/// <summary>
/// Das Fenster fuer einen abgebrochenen Programmstart - gesperrte,
/// beschaedigte oder zu neue Datenbank, gescheiterte Migration.
///
/// Reines Anzeigefenster: Ueberschrift, Meldung, technischer Text und der
/// zu oeffnende Ordner kommen fertig aus Core
/// (<see cref="StartupFailureText"/>). Hier steht keine Fachlogik
/// (Regel 7) - dieses Fenster kennt nicht einmal die Ausnahmetypen, um die
/// es geht.
/// </summary>
public partial class StartupErrorWindow : Window
{
    private StartupFailure? _fehler;

    public StartupErrorWindow()
    {
        InitializeComponent();
    }

    public StartupErrorWindow(StartupFailure fehler) : this()
    {
        _fehler = fehler;

        TitelText.Text = fehler.Title;
        MessageText.Text = fehler.Message;
        TechnikText.Text = fehler.Technical;

        if (fehler.FolderPath is null)
        {
            OrdnerButton.IsVisible = false;
        }
        else
        {
            OrdnerButton.Content = fehler.FolderButtonText ?? "Ordner öffnen";
        }

        // "Erneut versuchen" nur, wo ein zweiter Versuch ueberhaupt
        // Aussicht hat. Bei einer beschaedigten Datei muss erst jemand
        // etwas tun; ein Knopf, der zuverlaessig wieder in dasselbe
        // Fenster fuehrt, waere Spott.
        ErneutButton.IsVisible = fehler.RetryWorthwhile;
    }

    private void OnBeendenClick(object? sender, RoutedEventArgs e) => Close();

    private void OnOrdnerOeffnenClick(object? sender, RoutedEventArgs e)
    {
        if (Ordner.Oeffne(_fehler?.FolderPath))
        {
            return;
        }

        // Statt eines zweiten Fehlers: der Pfad steht dann auf der
        // Schaltflaeche und laesst sich abtippen.
        OrdnerButton.IsEnabled = false;
        OrdnerButton.Content = _fehler?.FolderPath ?? "Kein Ordner";
    }

    // Startet die Anwendung neu und schliesst dieses Fenster. Der Weg fuer
    // den haeufigsten Fall: das zweite Fenster war noch offen, ist jetzt
    // zu, und der Start soll einfach noch einmal laufen.
    private void OnErneutVersuchenClick(object? sender, RoutedEventArgs e)
    {
        var programm = Environment.ProcessPath;

        if (programm is null)
        {
            ErneutButton.IsEnabled = false;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = programm,
                UseShellExecute = true,
            });

            Close();
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim erneuten Starten der Anwendung", ex);

            ErneutButton.IsEnabled = false;
            ErneutButton.Content = "Neustart nicht möglich";
        }
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
            await zwischenablage.SetTextAsync(_fehler?.Technical ?? string.Empty);
            KopiertHinweis.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Kopieren der technischen Angaben", ex);
            KopiertHinweis.Text = "Das Kopieren hat nicht geklappt — bitte den Text oben markieren.";
            KopiertHinweis.IsVisible = true;
        }
    }
}
