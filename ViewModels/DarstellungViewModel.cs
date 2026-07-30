using System.Collections.Generic;
using System.Linq;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Settings;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Verwaltungsbereich "Darstellung": die globale Schriftgroesse.
///
/// Die Stufen, ihre Faktoren und das Runden eines gespeicherten Wertes
/// stecken in Core (<see cref="FontScales"/>) - hier wird nur verdrahtet
/// (Regel 7): Auswahl an <see cref="Skalierung"/> weitergeben, damit sie
/// sofort wirkt, und in dieselbe Einstellungsdatei schreiben, in der
/// auch das Sicherungsziel steht.
/// </summary>
public sealed class DarstellungViewModel : ViewModelBase
{
    private readonly AppSettingsStore _settingsStore;

    public DarstellungViewModel(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;

        var aktuelleStufe = FontScales.FromFactor(settingsStore.Load().FontScale);

        Stufen = FontScales.Steps
            .Select(stufe => new SchriftgroesseOption(stufe, stufe == aktuelleStufe))
            .ToList();

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
                    Waehle(gewaehlt);
                }
            };
        }
    }

    public IReadOnlyList<SchriftgroesseOption> Stufen { get; }

    public string Hinweis =>
        "Die Schriftgröße gilt für die gesamte Anwendung und wirkt sofort. "
        + "Alle Spalten, Felder und Schaltflächen wachsen mit. "
        + "Die Einstellung bleibt über das Beenden hinaus erhalten.";

    /// <summary>
    /// Uebernimmt eine Stufe: sofort anwenden und dauerhaft merken.
    /// </summary>
    private void Waehle(SchriftgroesseOption option)
    {
        foreach (var stufe in Stufen)
        {
            stufe.IstAusgewaehlt = ReferenceEquals(stufe, option);
        }

        Skalierung.Aktuell.Setze(option.Faktor);

        // Laden und mit "with" weiterschreiben, damit das Sicherungsziel
        // in derselben Datei unangetastet bleibt.
        _settingsStore.Save(_settingsStore.Load() with { FontScale = option.Faktor });
    }
}
