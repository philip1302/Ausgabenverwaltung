using System;
using System.Data;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Reports;
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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databaseFilePath = AppPaths.GetDatabaseFilePath();

            StartupResult startupResult;
            try
            {
                startupResult = StartupService.Run(databaseFilePath);
            }
            catch (SchemaVersionTooNewException ex)
            {
                desktop.MainWindow = new StartupErrorWindow(ex.Message);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            _services = BuildServiceProvider(databaseFilePath, startupResult);
            desktop.Exit += (_, _) => _services.Dispose();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Reine DI-Verdrahtung: Core-Repositories und -Dienste werden hier
    // registriert, damit ViewModels sie im Konstruktor bekommen statt sie
    // selbst zu erzeugen (siehe CLAUDE.md).
    private static ServiceProvider BuildServiceProvider(string databaseFilePath, StartupResult startupResult)
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
        services.AddSingleton(new BackupSettingsStore(AppPaths.GetSettingsFilePath()));
        services.AddSingleton(provider => new BackupService(
            provider.GetRequiredService<IDbConnection>(),
            AppPaths.GetBackupFolderPath(),
            provider.GetRequiredService<BackupSettingsStore>()));

        // Der Erzeugungslauf des Programmstarts ist gerade gelaufen (siehe
        // StartupService.Run) und zaehlt als der heutige - der Scheduler
        // startet deshalb mit dem heutigen Datum und laesst den naechsten
        // Lauf erst morgen zu.
        services.AddSingleton(provider => new RecurringExpenseScheduler(
            provider.GetRequiredService<RecurringExpenseRepository>(),
            DateOnly.FromDateTime(DateTime.Now)));

        services.AddSingleton(startupResult);
        services.AddSingleton<StartupNoticeViewModel>();

        services.AddSingleton<ErfassenViewModel>();
        services.AddSingleton<OffenePostenViewModel>();
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<AusgabenlisteViewModel>();
        services.AddSingleton<KategorienViewModel>();
        services.AddSingleton<PersonenViewModel>();
        services.AddSingleton<VorlagenViewModel>();
        services.AddSingleton<DatensicherungViewModel>();
        services.AddSingleton<VerwaltungViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
