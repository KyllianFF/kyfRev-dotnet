using System.Reflection;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemAppearance, WindowsAppearance>();
        services.AddSingleton<IWindowChrome, WindowChrome>();
        services.AddSingleton<IVaultLocations>(_ => VaultLocations.Detect());
        services.AddSingleton<IFolderPicker, FolderPicker>();
        // Le coffre ouvert est fermé, et son verrou libéré, quand les services le sont.
        services.AddSingleton(provider => new KyfRevApplication(
            new VaultStore(),
            new BackupFiles(),
            provider.GetRequiredService<IVaultLocations>(),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            Version));
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Vaults);
        services.AddSingleton(provider => provider.GetRequiredService<KyfRevApplication>().Reminders);
        services.AddSingleton<DashboardPageViewModel>();
        services.AddSingleton(_ => new DesignResources(ThemeKind.Light, Motion.SystemPrefersReducedMotion));
        services.AddSingleton<AppearanceController>();
        services.AddSingleton<GalleryPageViewModel>();
        services.AddSingleton(provider => new ShellViewModel(
            CorePages.All(new Dictionary<string, object> { [CorePages.Today] = provider.GetRequiredService<DashboardPageViewModel>() })
                .Concat(provider.GetServices<INavigationContributor>().Select(contributor => CorePages.FromModule(contributor.Entry))),
            provider.GetRequiredService<AppearanceController>(),
            provider.GetRequiredService<GalleryPageViewModel>()));
        services.AddTransient<ShellWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
