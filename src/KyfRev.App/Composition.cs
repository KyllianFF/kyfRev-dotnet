using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.App;

/// <summary>
/// Racine de composition : le seul endroit qui relie la présentation à l'infrastructure. Les
/// modules s'y ajouteront à l'ouverture d'un coffre, selon ses réglages (<see cref="ModuleCatalog"/>).
/// </summary>
internal static class Composition
{
    /// <summary>Les services de l'application.</summary>
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemAppearance, WindowsAppearance>();
        services.AddSingleton<IWindowChrome, WindowChrome>();
        services.AddSingleton(_ => new DesignResources(ThemeKind.Light, Motion.SystemPrefersReducedMotion));
        services.AddSingleton<AppearanceController>();
        services.AddSingleton<GalleryPageViewModel>();
        services.AddSingleton(provider => new ShellViewModel(
            CorePages.All().Concat(provider.GetServices<INavigationContributor>().Select(contributor => CorePages.FromModule(contributor.Entry))),
            provider.GetRequiredService<AppearanceController>(),
            provider.GetRequiredService<GalleryPageViewModel>()));
        services.AddTransient<ShellWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
