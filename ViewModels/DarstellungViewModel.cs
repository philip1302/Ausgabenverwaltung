using System;
using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Settings;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Verwaltungsbereich "Darstellung": Thema und globale Schriftgroesse.
///
/// Die Stufen, ihre Faktoren und das Runden eines gespeicherten Wertes
/// stecken in Core (<see cref="FontScales"/>) - hier wird nur verdrahtet
/// (Regel 7): Auswahl an <see cref="Skalierung"/> weitergeben, damit sie
/// sofort wirkt, und in dieselbe Einstellungsdatei schreiben, in der
/// auch das Sicherungsziel steht.
///
/// Das Thema (UI/UX-Redesign, Abschnitt 5.9) ist neu: bisher folgte die
/// Anwendung immer dem System (<c>RequestedThemeVariant="Default"</c> in
/// App.axaml), ohne dass sich das bewusst waehlen liess. "System" bleibt
/// die Vorgabe und bedeutet weiterhin genau das bisherige Verhalten.
/// </summary>
public sealed partial class DarstellungViewModel : ViewModelBase
{
    private readonly AppSettingsStore _settingsStore;

    public DarstellungViewModel(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;

        var einstellungen = settingsStore.Load();

        var aktuelleStufe = FontScales.FromFactor(einstellungen.FontScale);
        Stufen = FontScales.Steps
            .Select(stufe => new SchriftgroesseOption(stufe, stufe == aktuelleStufe))
            .ToList();

        ThemaOptionen = new List<ThemaOption>
        {
            new(ThemeMode.Light, "Hell", einstellungen.ThemeMode == ThemeMode.Light),
            new(ThemeMode.Dark, "Dunkel", einstellungen.ThemeMode == ThemeMode.Dark),
            new(ThemeMode.System, "System", einstellungen.ThemeMode == ThemeMode.System),
        };

        // Die Auswahlknoepfe melden sich ueber die Stufe selbst. Kein
        // Aufruf aus der Ansicht heraus, kein Code-Behind - und die beim
        // Start gesetzte Stufe loest nichts aus, weil sie schon vor
        // dieser Anmeldung steht.
        foreach (var stufe in Stufen)
        {
            stufe.PropertyChanged += (absender, argumente) =>
            {
                if (argumente.PropertyName == nameof(SchriftgroesseOption.IstAusgewaehlt)
                    && absender is SchriftgroesseOption gewaehlt
                    && gewaehlt.IstAusgewaehlt)
                {
                    WaehleStufe(gewaehlt);
                }
            };
        }

        foreach (var thema in ThemaOptionen)
        {
            thema.PropertyChanged += (absender, argumente) =>
            {
                if (argumente.PropertyName == nameof(ThemaOption.IstAusgewaehlt)
                    && absender is ThemaOption gewaehlt
                    && gewaehlt.IstAusgewaehlt)
                {
                    WaehleThema(gewaehlt);
                }
            };
        }

        AktualisiereFusszeilenText();
    }

    public IReadOnlyList<SchriftgroesseOption> Stufen { get; }

    public IReadOnlyList<ThemaOption> ThemaOptionen { get; }

    public string Hinweis =>
        "Die Schriftgröße gilt für die gesamte Anwendung und wirkt sofort. "
        + "Alle Spalten, Felder und Schaltflächen wachsen mit. "
        + "Die Einstellung bleibt über das Beenden hinaus erhalten.";

    public string ThemaHinweis =>
        "Neu im Redesign: Hell/Dunkel lässt sich jetzt bewusst wählen, statt nur dem System zu folgen.";

    /// <summary>
    /// Kompakter Status fuer die Sidebar-Fusszeile ("Hell · Normal") -
    /// UI/UX-Redesign, Abschnitt 3.
    /// </summary>
    [ObservableProperty]
    private string _fusszeilenText = string.Empty;

    /// <summary>
    /// Uebernimmt eine Schriftstufe: sofort anwenden und dauerhaft merken.
    /// </summary>
    private void WaehleStufe(SchriftgroesseOption option)
    {
        foreach (var stufe in Stufen)
        {
            stufe.IstAusgewaehlt = ReferenceEquals(stufe, option);
        }

        Skalierung.Aktuell.Setze(option.Faktor);

        // Laden und mit "with" weiterschreiben, damit die uebrigen
        // Einstellungen in derselben Datei unangetastet bleiben.
        //
        // Ein Fehler beim Schreiben bekommt hier ausdruecklich KEINEN
        // Dialog: die Schriftgroesse ist bereits umgestellt und wirkt, es
        // ginge nur darum, dass sie einen Neustart nicht uebersteht. Ein
        // Fehlerfenster dafuer waere unverhaeltnismaessig. Der Hinweis
        // steht neben der Auswahl, das Uebrige im Protokoll.
        SpeicherHinweis = Schreibvorgang.Versuche(
            "Beim Speichern der Schriftgroesse",
            () => _settingsStore.Save(_settingsStore.Load() with { FontScale = option.Faktor })) is null
            ? null
            : "Die Schriftgröße wirkt sofort, ließ sich aber nicht dauerhaft merken — "
              + "nach einem Neustart gilt wieder die vorherige Stufe.";

        AktualisiereFusszeilenText();
    }

    /// <summary>
    /// Uebernimmt eine Themenvariante: sofort auf das laufende Fenster
    /// anwenden (<see cref="Application.RequestedThemeVariant"/>) und
    /// dauerhaft merken - analog zu <see cref="WaehleStufe"/>.
    /// </summary>
    private void WaehleThema(ThemaOption option)
    {
        foreach (var thema in ThemaOptionen)
        {
            thema.IstAusgewaehlt = ReferenceEquals(thema, option);
        }

        if (Application.Current is { } anwendung)
        {
            anwendung.RequestedThemeVariant = option.Modus switch
            {
                ThemeMode.Light => ThemeVariant.Light,
                ThemeMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        SpeicherHinweis = Schreibvorgang.Versuche(
            "Beim Speichern des Themas",
            () => _settingsStore.Save(_settingsStore.Load() with { ThemeMode = option.Modus })) is null
            ? null
            : "Das Thema wirkt sofort, ließ sich aber nicht dauerhaft merken — "
              + "nach einem Neustart gilt wieder die vorherige Wahl.";

        AktualisiereFusszeilenText();
    }

    private void AktualisiereFusszeilenText()
    {
        var thema = ThemaOptionen.FirstOrDefault(t => t.IstAusgewaehlt)?.Bezeichnung ?? "System";
        var stufe = Stufen.FirstOrDefault(s => s.IstAusgewaehlt)?.Bezeichnung ?? "Normal";
        FusszeilenText = $"{thema} · {stufe}";
    }

    /// <summary>
    /// Hinweis, wenn eine Einstellung nicht dauerhaft gemerkt werden
    /// konnte. NULL im Normalfall.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeicherHinweisSichtbar))]
    private string? _speicherHinweis;

    public bool SpeicherHinweisSichtbar => SpeicherHinweis is not null;
}
