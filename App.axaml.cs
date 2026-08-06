using System;
using System.Data;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Reports;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Startup;
using Ausgabenverwaltung.ViewModels;
using Ausgabenverwaltung.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Ausgabenverwaltung;

public partial class App : Application
{
    // Haelt den Container am Leben, damit die einzige DB-Verbindung der
    // Anwendung erst beim Beenden geschlossen wird (siehe Exit-Handler).
    private ServiceProvider? _services;

    public override void Initialize()
    {
        // So frueh wie moeglich: ab hier ist der Oberflaechen-Thread
        // abgesichert, und alles, was beim Aufbau der Fenster
        // schiefgeht, landet im Fehlerdialog statt im Nichts.
        GlobaleFehlerbehandlung.Einrichten();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Starte(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Starte(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var settingsStore = new AppSettingsStore(AppPaths.GetSettingsFilePath());

        // Die gespeicherte Schriftgroesse gilt ab dem ersten Fenster -
        // auch fuer das Fehlerfenster, falls der Start scheitert.
        var settings = settingsStore.Load();
        Skalierung.Aktuell.Setze(settings.FontScale);

        // Ebenso das gespeicherte Thema (UI/UX-Redesign, Verwaltung ▸
        // Darstellung) - "System" entspricht unveraendert der bisherigen
        // Vorgabe "Default" aus App.axaml.
        RequestedThemeVariant = settings.ThemeMode switch
        {
            Core.Display.ThemeMode.Light => Avalonia.Styling.ThemeVariant.Light,
            Core.Display.ThemeMode.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };

        // Dieselbe Datei traegt die zuletzt gezogene Breite der
        // Kategoriespalte. Geschrieben wird sie erst beim Loslassen des
        // Spaltengriffs, und immer mit "with" auf dem gerade gelesenen
        // Stand - sonst faenden Sicherungsziel und Schriftgroesse sich
        // auf ihren Vorgabewerten wieder.
        Spaltenbreiten.Aktuell.SetzeKategorie(settings.CategoryColumnWidth);
        Spaltenbreiten.Aktuell.Sichern = breite =>
        {
            // Ausdruecklich still: das hier laeuft beim Loslassen des
            // Spaltengriffs. Ein Fehlerdialog mitten in einer Zieh-Geste,
            // nur weil sich eine Spaltenbreite nicht merken laesst, waere
            // voellig unverhaeltnismaessig. Die gezogene Breite gilt fuer
            // diese Sitzung ohnehin; verloren geht hoechstens, dass sie
            // den Neustart uebersteht.
            try
            {
                settingsStore.Save(settingsStore.Load() with { CategoryColumnWidth = breite });
            }
            catch (Exception ex)
            {
                AppLog.Current.Exception("Beim Speichern der Spaltenbreite", ex);
            }
        };

        // Es laeuft schon eine, die sich nicht meldet (siehe Program).
        if (Program.LaeuftBereitsOhneAntwort)
        {
            ZeigeStartfehler(desktop, StartupFailureText.AlreadyRunning(AppLog.Current.FolderPath));
            return;
        }

        // Schon der Anwendungsdatenordner war nicht zu erreichen.
        if (Program.DatenbankPfad is not string databaseFilePath)
        {
            ZeigeStartfehler(desktop, BeschreibeStartfehler(
                Program.Pfadfehler ?? new InvalidOperationException(
                    "Der Pfad der Datenbank konnte nicht ermittelt werden."),
                AppPaths.DatabaseFileName));
            return;
        }

        StartupResult startupResult;
        try
        {
            startupResult = StartupService.Run(databaseFilePath);
        }
        catch (Exception ex)
        {
            // Bewusst ALLE Ausnahmen: gesperrte Datei, beschaedigte Datei,
            // gescheiterte Migration, zu neues Schema - und auch das, was
            // hier noch niemand bedacht hat. Jede davon bekommt eine
            // Erklaerung statt eines Fensters, das gar nicht erst aufgeht
            // (die Einteilung besorgt StartupFailureText).
            AppLog.Current.Exception("Beim Programmstart", ex);
            ZeigeStartfehler(desktop, BeschreibeStartfehler(ex, databaseFilePath));
            return;
        }

        _services = BuildServiceProvider(databaseFilePath, startupResult, settingsStore);

        desktop.Exit += (_, _) =>
        {
            AppLog.Current.Info(LogEvents.ProgramEnd());
            _services.Dispose();
            Program.Einzelinstanz?.Dispose();
        };

        var hauptfenster = new MainWindow
        {
            DataContext = _services.GetRequiredService<MainViewModel>(),
        };

        desktop.MainWindow = hauptfenster;

        // Ab jetzt auf weitere Startversuche horchen: statt eines zweiten
        // Fensters kommt dieses hier nach vorn.
        Program.Einzelinstanz?.StartListening(
            () => Dispatcher.UIThread.Post(() => HoleNachVorn(hauptfenster)));

        SucheNachNeuerFassung();
    }

    /// <summary>
    /// Stoesst die Suche nach einer neuen Fassung an - bewusst ERST hier,
    /// wenn das Fenster steht: der Startvorgang selbst haengt damit an
    /// keiner Netzverbindung, und ein langsames oder nicht erreichbares
    /// GitHub verzoegert nichts.
    ///
    /// Absichtlich ohne await. Die Aufgabe laeuft neben der Oberflaeche
    /// her; sie meldet sich nur im Erfolgsfall ueber ihr Band und
    /// schweigt sonst ins Protokoll. Ihre Ausnahmen faengt sie selbst -
    /// das Auffangnetz aus GlobaleFehlerbehandlung liegt trotzdem
    /// darunter.
    /// </summary>
    private void SucheNachNeuerFassung()
    {
        if (_services is null)
        {
            return;
        }

        var aktualisierung = _services.GetRequiredService<AktualisierungViewModel>();

        _ = Task.Run(async () =>
        {
            try
            {
                await aktualisierung.SucheUndLegeBereitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Current.Exception("Beim Suchen nach einer neuen Fassung", ex);
            }
        });
    }

    private static StartupFailure BeschreibeStartfehler(Exception ex, string databaseFilePath)
    {
        // Der Sicherungsordner wird hier nur benannt, nicht angelegt -
        // scheitert auch das noch, soll die Meldung darueber trotzdem
        // erscheinen.
        string backupFolderPath;
        try
        {
            backupFolderPath = AppPaths.GetBackupFolderPath();
        }
        catch (Exception)
        {
            backupFolderPath = "(Sicherungsordner nicht erreichbar)";
        }

        return StartupFailureText.Describe(
            ex, databaseFilePath, backupFolderPath,
            AppLog.Current.FolderPath, GlobaleFehlerbehandlung.Version);
    }

    private static void ZeigeStartfehler(
        IClassicDesktopStyleApplicationLifetime desktop, StartupFailure fehler)
    {
        desktop.MainWindow = new StartupErrorWindow(fehler);

        // Der belegte Platz gehoert auch dann freigegeben, wenn nur das
        // Fehlerfenster zu sehen war - sonst haelt eine gescheiterte
        // Ausfuehrung den naechsten Start auf.
        desktop.Exit += (_, _) => Program.Einzelinstanz?.Dispose();
    }

    // Holt das vorhandene Fenster nach vorn, wenn jemand die Anwendung ein
    // zweites Mal startet.
    private static void HoleNachVorn(Window fenster)
    {
        if (fenster.WindowState == WindowState.Minimized)
        {
            fenster.WindowState = WindowState.Normal;
        }

        fenster.Show();
        fenster.Activate();

        // Windows laesst ein Fenster nicht ohne Weiteres von einem anderen
        // Prozess in den Vordergrund holen. Der kurze Sprung ueber
        // Topmost ist der uebliche Weg daran vorbei - und er wird sofort
        // wieder zurueckgenommen, damit das Fenster nicht dauerhaft ueber
        // allem anderen klebt.
        fenster.Topmost = true;
        fenster.Topmost = false;
    }

    // Reine DI-Verdrahtung: Core-Repositories und -Dienste werden hier
    // registriert, damit ViewModels sie im Konstruktor bekommen statt sie
    // selbst zu erzeugen (siehe CLAUDE.md).
    private static ServiceProvider BuildServiceProvider(
        string databaseFilePath, StartupResult startupResult, AppSettingsStore settingsStore)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDbConnection>(
            _ => SqliteConnectionFactory.OpenConnection($"Data Source={databaseFilePath}"));

        services.AddSingleton<PersonRepository>();
        services.AddSingleton<CategoryRepository>();
        services.AddSingleton<ExpenseRepository>();
        services.AddSingleton<OpenItemsRepository>();
        services.AddSingleton<RecurringExpenseRepository>();
        services.AddSingleton<ReportRepository>();

        // Die Sicherung benutzt dieselbe Verbindung wie der Rest der
        // Anwendung: VACUUM INTO schreibt daraus eine konsistente Kopie,
        // ohne dass die Anwendung dafuer pausieren muesste.
        // Ein Einstellungsspeicher fuer die ganze Anwendung: Sicherungsziel
        // und Schriftgroesse liegen in derselben Datei, und wer eines
        // aendert, darf das andere nicht ueberschreiben.
        services.AddSingleton(settingsStore);
        services.AddSingleton(provider => new BackupService(
            provider.GetRequiredService<IDbConnection>(),
            AppPaths.GetBackupFolderPath(),
            provider.GetRequiredService<AppSettingsStore>()));

        // Der Erzeugungslauf des Programmstarts ist gerade gelaufen (siehe
        // StartupService.Run) und zaehlt als der heutige - der Scheduler
        // startet deshalb mit dem heutigen Datum und laesst den naechsten
        // Lauf erst morgen zu.
        services.AddSingleton(provider => new RecurringExpenseScheduler(
            provider.GetRequiredService<RecurringExpenseRepository>(),
            DateOnly.FromDateTime(DateTime.Now)));

        services.AddSingleton(startupResult);
        services.AddSingleton<StartupNoticeViewModel>();
        services.AddSingleton<AktualisierungViewModel>();

        services.AddSingleton<StartseiteViewModel>();
        services.AddSingleton<ErfassenViewModel>();
        services.AddSingleton<OffenePostenViewModel>();
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<AusgabenlisteViewModel>();
        services.AddSingleton<KategorienViewModel>();
        services.AddSingleton<PersonenViewModel>();
        services.AddSingleton<VorlagenViewModel>();
        services.AddSingleton<DatensicherungViewModel>();
        services.AddSingleton<DarstellungViewModel>();
        services.AddSingleton<VerwaltungViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
