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
using Avalonia.Threading;
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
/// Das Band gehoert diesem ViewModel nicht mehr selbst: es stellt seine
/// Meldung in die gemeinsame Bandzone (<see cref="BaenderViewModel"/>),
/// in der hoechstens ein Band zugleich sichtbar ist.
///
/// Keine Fachlogik hier (Regel 7): entschieden wird in
/// Core.Updates.UpdateEntscheidung, formuliert in
/// Core.Errors.UpdateText.
/// </summary>
public sealed partial class AktualisierungViewModel : ViewModelBase
{
    /// <summary>Unter diesem Namen steht das Band in der Bandzone.</summary>
    private const string BandSchluessel = "aktualisierung";

    // Ein HttpClient fuer die Laufzeit dieser Ausfuehrung. Ihn je Abruf
    // neu anzulegen, verbraucht Verbindungen, die noch eine Weile offen
    // bleiben - bei zwei Abrufen pro Programmstart faellt das nicht ins
    // Gewicht, ist aber unnoetig.
    private static readonly HttpClient Netz = ErzeugeClient();

    private readonly AppSettingsStore _einstellungen;
    private readonly BaenderViewModel _baender;

    /// <summary>Wohin verwiesen wird, wenn nicht selbst eingespielt
    /// werden kann.</summary>
    private string? _seitenAdresse;

    /// <summary>
    /// Die gefundene Fassung und ihre fertige Meldung - gemerkt, damit das
    /// Band nach einem "Später" wieder hervorgeholt werden kann, ohne
    /// erneut ins Netz zu greifen.
    /// </summary>
    private Bandmeldung? _meldung;

    private string? _gefundeneVersion;

    private bool _neustartMoeglich;

    /// <summary>
    /// Der ruhige Dauerplatz in der Sidebar-Fusszeile. Er ist der Grund,
    /// warum "Später" wirklich spaeter heissen darf: das Band kommt fuer
    /// diese Fassung nicht wieder, die Auskunft bleibt trotzdem
    /// erreichbar. Ohne ihn stuende die Wahl zwischen Nerven (jeder Start
    /// dasselbe Band) und Vergessen (weggeklickt und nie wieder
    /// auffindbar).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FusszeileSichtbar))]
    private string? _fusszeilenText;

    public bool FusszeileSichtbar => FusszeilenText is not null;

    public AktualisierungViewModel(AppSettingsStore einstellungen, BaenderViewModel baender)
    {
        _einstellungen = einstellungen;
        _baender = baender;
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
                Melde(neueVersion, UpdateText.Bereitgelegt(neueVersion), neustartMoeglich: true);
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
        var meldung = UpdateText.NurHinweis(
            version,
            hindernis,
            UpdatePlatform.AssetName(),
            istBundle: RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

        Melde(version, meldung, neustartMoeglich: false);
    }

    /// <summary>
    /// Gemeinsamer Ausgang aller Faelle: merken, die Fusszeile setzen und
    /// - sofern die Fassung nicht schon einmal weggeklickt wurde - das
    /// Band stellen.
    ///
    /// Die Suche laeuft auf einem Hintergrundstrang (siehe
    /// App.SucheNachNeuerFassung), die Oberflaeche will aber vom
    /// Oberflaechenstrang aus geaendert werden - deshalb das Posten.
    /// </summary>
    private void Melde(string version, Bandmeldung meldung, bool neustartMoeglich)
    {
        _gefundeneVersion = version;
        _meldung = meldung;
        _neustartMoeglich = neustartMoeglich;

        var schonWeggeklickt = _einstellungen.Load().DismissedUpdateVersion == version;

        Dispatcher.UIThread.Post(() =>
        {
            FusszeilenText = $"Fassung {version} verfügbar";

            if (!schonWeggeklickt)
            {
                StelleBand();
            }
        });
    }

    /// <summary>
    /// Stellt die gemerkte Meldung in die Bandzone. Genau EIN
    /// hervorgehobener Knopf, und je Lage ein anderer: liegt eine Fassung
    /// bereit, fuehrt der Weg ueber den Neustart; laesst sie sich nicht
    /// selbst einspielen, ueber die Veroeffentlichungsseite. Beide
    /// zugleich zu zeigen liess das Band wie eine Wahl unter
    /// gleichwertigen Wegen aussehen, obwohl sie zu verschiedenen Lagen
    /// gehoeren.
    /// </summary>
    private void StelleBand()
    {
        if (_meldung is null)
        {
            return;
        }

        string? text = null;
        string? tipp = null;
        Action? aktion = null;

        if (_neustartMoeglich)
        {
            text = "Jetzt neu starten";
            tipp = "Schließt die Anwendung und öffnet sie sofort wieder — dabei "
                + "wird die neue Fassung übernommen.";
            aktion = JetztNeuStarten;
        }
        else if (_seitenAdresse is not null)
        {
            text = "Veröffentlichungsseite";
            tipp = "Öffnet die Seite, auf der die neue Fassung zum Herunterladen liegt.";
            aktion = SeiteOeffnen;
        }

        _baender.Zeige(new Bandeintrag
        {
            Schluessel = BandSchluessel,
            Meldung = _meldung,
            AktionText = text,
            AktionTipp = tipp,
            Aktion = aktion,
            BeimSchliessen = MerkeVerworfen,
        });
    }

    /// <summary>
    /// Holt das weggeklickte Band zurueck - der Klick auf den Hinweis in
    /// der Sidebar-Fusszeile.
    /// </summary>
    [RelayCommand]
    private void BandWiederZeigen() => StelleBand();

    /// <summary>
    /// "Später" heisst wirklich spaeter: die weggeklickte Fassung wird
    /// gemerkt, damit dasselbe Band nicht bei jedem Start von neuem
    /// erscheint. Erscheint eine noch neuere Fassung, stimmt die gemerkte
    /// Nummer nicht mehr ueberein und das Band kommt wieder - genau so
    /// soll es sein.
    ///
    /// Schlaegt das Schreiben fehl, ist das kein Grund fuer eine Meldung:
    /// die Folge ist lediglich, dass das Band beim naechsten Start noch
    /// einmal erscheint.
    /// </summary>
    private void MerkeVerworfen()
    {
        if (_gefundeneVersion is null)
        {
            return;
        }

        try
        {
            _einstellungen.Save(
                _einstellungen.Load() with { DismissedUpdateVersion = _gefundeneVersion });
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Merken der zurueckgestellten Fassung", ex);
        }
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
    private void JetztNeuStarten()
    {
        if (!Neustart.StarteSichSelbst())
        {
            // Das gescheiterte Neustarten loest die bisherige Meldung ab,
            // statt sich daneben zu stellen - derselbe Schluessel.
            _meldung = UpdateText.NeustartGescheitert();
            _neustartMoeglich = false;
            StelleBand();
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
