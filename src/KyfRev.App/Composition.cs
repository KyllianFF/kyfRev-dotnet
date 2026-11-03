using System.IO;
using System.Reflection;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.App;

/// <summary>
/// Racine de composition : le seul endroit qui relie la présentation à l'infrastructure. Les
/// modules s'y ajouteront à l'ouverture d'un coffre, selon ses réglages (<see cref="ModuleCatalog"/>).
/// </summary>
internal static class Composition
{
    /// <summary>Version de l'application, sans l'empreinte du commit.</summary>
    public static string Version { get; } =
        typeof(Composition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Les services de l'application.</summary>
    /// <param name="time">Horloge ; celle du système par défaut.</param>
    /// <param name="locations">Emplacements ; ceux de l'exécutable en cours par défaut.</param>
    public static ServiceProvider Build(TimeProvider? time = null, VaultLocations? locations = null)
    {
        var clock = time ?? TimeProvider.System;
        var services = new ServiceCollection();
        // Signalé ouvert à l'installateur tant que les services vivent.
        services.AddSingleton<OpenMarker>();
        services.AddSingleton<ISystemAppearance, WindowsAppearance>();
        services.AddSingleton<IWindowChrome, WindowChrome>();
        services.AddSingleton<IExternalLinks, ShellLinks>();
        services.AddSingleton(_ => locations ?? VaultLocations.Detect());
        services.AddSingleton<IVaultLocations>(provider => provider.GetRequiredService<VaultLocations>());
        services.AddSingleton<IStudySessionStore>(provider => new StudySessionFile(provider.GetRequiredService<VaultLocations>().LocalData));
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            var locations = provider.GetRequiredService<VaultLocations>();
            var log = new DiagnosticLog(locations.LocalData, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), clock);
            log.Started(Version, ModeName(locations.Mode));
            return log;
        });
        services.AddSingleton<IFolderPicker, FolderPicker>();
        // Le coffre ouvert est fermé, et son verrou libéré, quand les services le sont.
        services.AddSingleton(provider => new KyfRevApplication(
            new VaultStore(),
            new BackupFiles(),
            provider.GetRequiredService<IVaultLocations>(),
            clock,
            provider.GetRequiredService<ILoggerFactory>(),
            Version));
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Vaults);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Reminders);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Sheets);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Annotations);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Folders);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Templates);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Reviews);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Editing);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Settings);
        services.AddSingleton<ISpelling, WindowsSpelling>();
        services.AddSingleton<ISpeech, WindowsSpeech>();
        services.AddSingleton(provider => new EditorEnvironment(
            provider.GetRequiredService<ISpelling>(),
            Path.Combine(provider.GetRequiredService<VaultLocations>().LocalData, "WebView2")));
        services.AddSingleton(provider => new MermaidEngine(Path.Combine(provider.GetRequiredService<VaultLocations>().LocalData, "WebView2")));
        services.AddSingleton(clock);
        services.AddSingleton<ReviewNavigation>();
        services.AddSingleton<IReviewOpener>(provider => provider.GetRequiredService<ReviewNavigation>());
        services.AddSingleton<DashboardPageViewModel>();
        services.AddSingleton<SheetsPageViewModel>();
        services.AddSingleton<DefinitionsPageViewModel>();
        services.AddSingleton<SettingsPageViewModel>();
        services.AddSingleton<ArchivesPageViewModel>();
        services.AddSingleton<StatisticsPageViewModel>();
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Maintenance);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Backups);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Trash);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Exports);
        services.AddSingleton(provider => new DataSettingsViewModel(
            provider.GetRequiredService<VaultUseCases>(),
            provider.GetRequiredService<BackupUseCases>(),
            provider.GetRequiredService<TrashUseCases>(),
            provider.GetRequiredService<AnnotationUseCases>(),
            provider.GetRequiredService<ExportUseCases>(),
            provider.GetRequiredService<SettingsUseCases>(),
            provider.GetRequiredService<IFolderPicker>(),
            provider.GetRequiredService<IReviewOpener>(),
            () =>
            {
                provider.GetRequiredService<SettingsPageViewModel>().Forget();
                provider.GetRequiredService<ShellViewModel>().ShowWorkspace();
            }));
        services.AddSingleton<AboutSettingsViewModel>();
        services.AddSingleton<SettingsHubViewModel>();
        services.AddSingleton(provider => new HealthPageViewModel(
            provider.GetRequiredService<MaintenanceUseCases>(),
            provider.GetRequiredService<BackupUseCases>(),
            provider.GetRequiredService<SheetUseCases>(),
            provider.GetRequiredService<IReviewOpener>(),
            () => DiagnosticText(provider),
            System.Windows.Clipboard.SetText));
        services.AddSingleton(_ => new DesignResources(ThemeKind.Light, Motion.SystemPrefersReducedMotion));
        services.AddSingleton<AppearanceController>();
        services.AddSingleton<GalleryPageViewModel>();
        services.AddSingleton(provider =>
        {
            var shell = new ShellViewModel(
                CorePages.All(new Dictionary<string, object>
                {
                    [CorePages.Today] = provider.GetRequiredService<DashboardPageViewModel>(),
                    [CorePages.Sheets] = provider.GetRequiredService<SheetsPageViewModel>(),
                    [CorePages.Definitions] = provider.GetRequiredService<DefinitionsPageViewModel>(),
                    [CorePages.Settings] = provider.GetRequiredService<SettingsHubViewModel>(),
                    [CorePages.Archives] = provider.GetRequiredService<ArchivesPageViewModel>(),
                    [CorePages.Statistics] = provider.GetRequiredService<StatisticsPageViewModel>(),
                    [CorePages.Health] = provider.GetRequiredService<HealthPageViewModel>(),
                })
                    .Concat(provider.GetServices<INavigationContributor>().Select(contributor => CorePages.FromModule(contributor.Entry))),
                provider.GetRequiredService<AppearanceController>(),
                provider.GetRequiredService<GalleryPageViewModel>());
            // Les fiches et les passages s'ouvrent dans la coquille.
            provider.GetRequiredService<ReviewNavigation>().Attach(shell);
            return shell;
        });
        services.AddTransient<ShellWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Le mode tel que le journal le nomme, comme la 0.x.</summary>
    private static string DiagnosticText(IServiceProvider provider) => DiagnosticReport.Write(
        Version,
        ModeName(provider.GetRequiredService<VaultLocations>().Mode),
        WebViewVersion(),
        provider.GetRequiredService<VaultUseCases>().State().Vault is not null,
        provider.GetRequiredService<TimeProvider>().GetLocalNow(),
        provider.GetRequiredService<ILoggerFactory>() is DiagnosticLog log ? log.Tail(DiagnosticReport.LogLines) : string.Empty);

    private static string WebViewVersion()
    {
        try
        {
            return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException problem)
        {
            return $"introuvable ({problem.Message})";
        }
    }

    internal static string ModeName(AppMode mode) => mode switch
    {
        AppMode.Portable => "portable",
        AppMode.Installed => "installée",
        _ => "développement",
    };
}
