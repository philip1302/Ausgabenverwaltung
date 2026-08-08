using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Startup;
using Ausgabenverwaltung.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Sieht im Hintergrund nach, ob es eine neuere Fassung gibt, laedt sie
/// und legt sie bereit - uebernommen wird sie beim naechsten Start
/// (siehe Core.Updates.UpdateInstaller und Program.Main).
///
/// Nichts davon darf den Anwender aufhalten. Der Ablauf laeuft neben der
/// Oberflaeche her, meldet sich nur im Erfolgsfall mit einem Band und
/// schweigt sonst ins Protokoll. Ein nicht erreichbares GitHub ist kein
/// Ereignis, das jemanden interessiert.
///
/// Keine Fachlogik hier (Regel 7): entschieden wird in
/// Core.Updates.UpdateEntscheidung, formuliert in
/// Core.Errors.UpdateText.
/// </summary>
public sealed partial class AktualisierungViewModel : ViewModelBase
{
    // Ein HttpClient fuer die Laufzeit dieser Ausfuehrung. Ihn je Abruf
    // neu anzulegen, verbraucht Verbindungen, die noch eine Weile offen
    // bleiben - bei zwei Abrufen pro Programmstart faellt das nicht ins
    // Gewicht, ist aber unnoetig.
    private static readonly HttpClient Netz = ErzeugeClient();

    private readonly AppSettingsStore _einstellungen;

    /// <summary>Wohin verwiesen wird, wenn nicht selbst eingespielt
    /// werden kann.</summary>
    private string? _seitenAdresse;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BandSichtbar))]
    private string? _bandText;

    /// <summary>
    /// Ob das Band zum sofortigen Neustart einlaedt. Beim blossen Hinweis
    /// (nicht selbst einspielbar) gibt es nichts neu zu starten - dort
    /// fuehrt der Weg auf die Veroeffentlichungsseite.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NurSeiteAufrufbar))]
    private bool _neustartMoeglich;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NurSeiteAufrufbar))]
    private bool _seiteAufrufbar;

    /// <summary>
    /// Die Veroeffentlichungsseite ist der EINZIGE Weg - es liegt also
    /// nichts bereit, das ein Neustart uebernehmen koennte.
    ///
    /// Das Band zeigt je Lage genau einen hervorgehobenen Knopf. Liegt
    /// eine Fassung bereit, ist das der Neustart; sonst die Seite. Beide
    /// nebeneinander sahen aus wie eine Wahl zwischen gleichwertigen
    /// Wegen, obwohl sie zu verschiedenen Lagen gehoeren.
    /// </summary>
    public bool NurSeiteAufrufbar => SeiteAufrufbar && !NeustartMoeglich;

    public bool BandSichtbar => BandText is not null;

    public AktualisierungViewModel(AppSettingsStore einstellungen)
    {
        _einstellungen = einstellungen;
    }

    private static HttpClient ErzeugeClient()
    {
        var client = new HttpClient
        {
            // Grosszuegig, aber endlich: haengt die Gegenstelle, soll der
            // Versuch irgendwann aufgeben und nicht bis zum
            // Programmende offen bleiben.
            Timeout = TimeSpan.FromMinutes(5),
        };

        // GitHub weist Anfragen ohne Kennung ab.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Ausgabenverwaltung/" + AppVersion.Anzeigetext(GlobaleFehlerbehandlung.Version));

        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        return client;
    }

    /// <summary>
    /// Der ganze Ablauf, im Hintergrund. Wirft nicht - jeder Fehlschlag
    /// endet im Protokoll.
    /// </summary>
    public async Task SucheUndLegeBereitAsync(CancellationToken abbruch = default)
    {
        try
        {
            if (!_einstellungen.Load().AutoUpdate)
            {
                AppLog.Current.Info(LogEvents.UpdateAbgeschaltet());
                return;
            }

            var ziel = UpdateInstaller.ZielPfad();
            var ordner = ziel is null ? null : System.IO.Path.GetDirectoryName(ziel);

            if (ziel is null || string.IsNullOrEmpty(ordner))
            {
                return;
            }

            var eigeneVersion = GlobaleFehlerbehandlung.Version;
            var assetName = UpdatePlatform.AssetName();

            var entscheidung = await UpdateDownload
                .PruefeAsync(eigeneVersion, assetName, HoleTextAsync, abbruch)
                .ConfigureAwait(false);

            MerkeZeitpunkt();

            if (!entscheidung.IstVerfuegbar)
            {
                MeldeOhneAktualisierung(entscheidung, eigeneVersion);
                return;
            }

            var neueVersion = AppVersion.Anzeigetext(entscheidung.Release!.TagName);
            AppLog.Current.Info(LogEvents.UpdateGefunden(neueVersion));

            _seitenAdresse = entscheidung.HtmlUrl;

            // Erst hier pruefen, ob ueberhaupt abgelegt werden kann: die
            // Frage stellt sich nur, wenn es tatsaechlich etwas abzulegen
            // gibt.
            if (!UpdateStaging.OrdnerBeschreibbar(ordner))
            {
                AppLog.Current.Info(LogEvents.UpdateOrdnerSchreibgeschuetzt(ordner));
                ZeigeHinweis(neueVersion, UpdateHindernis.OrdnerSchreibgeschuetzt);
                return;
            }

            var bereit = await UpdateDownload
                .LadeUndLegeBereitAsync(entscheidung, ziel, HoleDateiAsync, abbruch)
                .ConfigureAwait(false);

            if (bereit)
            {
                BandText = UpdateText.Bereitgelegt(neueVersion);
                NeustartMoeglich = true;
                SeiteAufrufbar = _seitenAdresse is not null;
            }
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Suchen nach einer neuen Fassung", ex);
        }
    }

    /// <summary>
    /// Es gibt zwar etwas Neueres, aber es laesst sich nicht selbst
    /// einspielen - dann bleibt der Hinweis mit dem Weg dorthin. In allen
    /// uebrigen Faellen (aktueller Stand, nichts erreichbar) bleibt es
    /// still: "es gibt nichts Neues" ist keine Nachricht.
    /// </summary>
    private void MeldeOhneAktualisierung(UpdateDecision entscheidung, string eigeneVersion)
    {
        switch (entscheidung.Grund)
        {
            case UpdateGrund.Aktuell:
                AppLog.Current.Info(
                    LogEvents.UpdateAktuell(AppVersion.Anzeigetext(eigeneVersion)));
                break;

            case UpdateGrund.KeinPassendesAsset:
                AppLog.Current.Info(
                    LogEvents.UpdateNichtGefunden(LogEvents.UpdateGrundText.KeinPassendesAsset));
                _seitenAdresse = entscheidung.HtmlUrl;
                ZeigeHinweis(
                    AppVersion.Anzeigetext(entscheidung.Release?.TagName),
                    UpdateHindernis.KeineDateiFuerDiesesSystem);
                break;

            case UpdateGrund.OhnePruefsumme:
                AppLog.Current.Info(
                    LogEvents.UpdateNichtGefunden(LogEvents.UpdateGrundText.OhnePruefsumme));
                _seitenAdresse = entscheidung.HtmlUrl;
                ZeigeHinweis(
                    AppVersion.Anzeigetext(entscheidung.Release?.TagName),
                    UpdateHindernis.OhnePruefsumme);
                break;

            case UpdateGrund.PlattformOhneVeroeffentlichung:
                AppLog.Current.Info(LogEvents.UpdateNichtGefunden(
                    LogEvents.UpdateGrundText.PlattformOhneVeroeffentlichung));
                break;

            default:
                AppLog.Current.Info(
                    LogEvents.UpdateNichtGefunden(LogEvents.UpdateGrundText.NichtsGefunden));
                break;
        }
    }

    private void ZeigeHinweis(string version, UpdateHindernis hindernis)
    {
        // Der Dateiname fuer dieses System kommt aus derselben Stelle, die
        // ihn auch beim Suchen benutzt - so kann die Anleitung nie eine
        // andere Datei nennen als die, die tatsaechlich gilt.
        BandText = UpdateText.NurHinweis(
            version,
            hindernis,
            UpdatePlatform.AssetName(),
            istBundle: RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
        NeustartMoeglich = false;
        SeiteAufrufbar = _seitenAdresse is not null;
    }

    // Der Zeitpunkt ist nur Anzeige und Protokoll - schlaegt das
    // Schreiben fehl, ist das kein Grund, irgendetwas abzubrechen.
    private void MerkeZeitpunkt()
    {
        try
        {
            _einstellungen.Save(
                _einstellungen.Load() with { LastUpdateCheckUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Merken der letzten Aktualisierungssuche", ex);
        }
    }

    private static async Task<string> HoleTextAsync(Uri adresse, CancellationToken abbruch)
    {
        using var antwort = await Netz.GetAsync(adresse, abbruch).ConfigureAwait(false);
        antwort.EnsureSuccessStatusCode();

        return await antwort.Content.ReadAsStringAsync(abbruch).ConfigureAwait(false);
    }

    private static async Task HoleDateiAsync(Uri adresse, string zielDatei, CancellationToken abbruch)
    {
        using var antwort = await Netz
            .GetAsync(adresse, HttpCompletionOption.ResponseHeadersRead, abbruch)
            .ConfigureAwait(false);

        antwort.EnsureSuccessStatusCode();

        // Ueber einen Datenstrom und nicht ueber ein Byte-Feld: die
        // Dateien sind rund 45 MB gross, und die muessen nicht auch noch
        // vollstaendig im Speicher stehen.
        await using var quelle = await antwort.Content
            .ReadAsStreamAsync(abbruch).ConfigureAwait(false);

        await using var senke = System.IO.File.Create(zielDatei);

        await quelle.CopyToAsync(senke, abbruch).ConfigureAwait(false);
    }

    [RelayCommand]
    private void Schliessen() => BandText = null;

    /// <summary>
    /// Startet die Anwendung neu, damit die bereitgelegte Fassung
    /// uebernommen wird.
    ///
    /// Hier stand einmal "Jetzt beenden" und die Begruendung, ein
    /// Selbstneustart sei nicht moeglich: der Austausch brauche einen
    /// Prozess, der die Programmdatei nicht mehr benutzt. Das stimmt
    /// weiterhin - nur gibt es diesen Prozess inzwischen. Der Nachfolger
    /// wartet ueber <see cref="Neustart.WarteAufVorgaenger"/> auf das Ende
    /// des Vorgaengers und tauscht erst danach; genau so verfaehrt
    /// <see cref="UpdateInstaller.StarteNeuenProzess"/> nach einem
    /// gelungenen Austausch schon lange.
    ///
    /// Der Unterschied fuer den Anwender ist der Punkt: der Text daneben
    /// versprach einen Neustart, der Knopf beendete nur - und wer danach
    /// vor einem geschlossenen Programm sitzt, haelt das fuer einen
    /// Fehler.
    ///
    /// Zuerst den Nachfolger starten, dann selbst enden. Laesst sich
    /// keiner starten, endet hier NICHTS: sonst waere die Anwendung weg,
    /// und die Aktualisierung haette sie gekostet.
    /// </summary>
    [RelayCommand]
    private void JetztNeuStarten()
    {
        if (!Neustart.StarteSichSelbst())
        {
            BandText = UpdateText.NeustartGescheitert();
            NeustartMoeglich = false;
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// Oeffnet die Veroeffentlichungsseite im Browser - fuer die Faelle,
    /// in denen nicht selbst eingespielt werden kann.
    /// </summary>
    [RelayCommand]
    private void SeiteOeffnen()
    {
        if (_seitenAdresse is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_seitenAdresse) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Oeffnen der Veroeffentlichungsseite", ex);
        }
    }
}
