using System.Windows;
using KyfRev.Application.UseCases;
using KyfRev.Infrastructure.Windows;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyfRev.App;

/// <summary>
/// L'application : se signale ouverte à l'installateur, assemble les services, applique
/// l'apparence de Windows, ouvre le coffre prévu
/// (l'accueil à défaut), puis la fenêtre principale. Avec <c>--galerie</c>, la fenêtre s'ouvre sur
/// la galerie des composants, sans ouvrir de coffre.
/// </summary>
internal sealed partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    /// <summary>L'horloge de l'application ; les scénarios d'acceptation y mettent une date simulée.</summary>
    internal TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Les emplacements de l'application ; les scénarios d'acceptation y mettent un dossier à eux.</summary>
    internal VaultLocations? Locations { get; init; }

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _services = Composition.Build(Time, Locations);
        _services.GetRequiredService<OpenMarker>();
        WatchUnexpectedErrors(_services.GetRequiredService<ILoggerFactory>().CreateLogger("kyfRev"));
        Resources.MergedDictionaries.Add(_services.GetRequiredService<AppearanceController>().Resources);
        Resources[MermaidEngine.ResourceKey] = _services.GetRequiredService<MermaidEngine>();
        Resources[NetworkBlock.ResourceKey] = _services.GetRequiredService<INetworkSchemas>();
        Resources[ExtensionBlocks.ResourceKey] = _services.GetRequiredService<ExtensionBlocks>();
        var shell = _services.GetRequiredService<ShellViewModel>();
        if (e.Args.Contains("--galerie", StringComparer.Ordinal))
        {
            // Revue visuelle : la galerie des composants d'emblée.
            shell.Navigate(shell.Gallery);
        }
        else
        {
            VaultStartup.Run(shell, _services.GetRequiredService<VaultUseCases>(), _services.GetRequiredService<IFolderPicker>());
        }
        var window = _services.GetRequiredService<ShellWindow>();
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Un arrêt inattendu est consigné dans le journal avant le message habituel de Windows, comme
    /// dans la 0.x ; rien n'est masqué ni rattrapé.
    /// </summary>
    private void WatchUnexpectedErrors(ILogger log)
    {
        DispatcherUnhandledException += (_, args) => DiagnosticLog.UnexpectedStop(log, args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => DiagnosticLog.UnexpectedStop(log, args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => DiagnosticLog.UnobservedTask(log, args.Exception);
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
