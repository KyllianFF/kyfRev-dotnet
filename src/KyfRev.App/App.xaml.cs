using System.Windows;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.App;

/// <summary>
/// L'application : assemble les services, applique l'apparence de Windows, ouvre la fenêtre
/// principale (sur la galerie des composants avec <c>--galerie</c>).
/// </summary>
internal sealed partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _services = Composition.Build();
        Resources.MergedDictionaries.Add(_services.GetRequiredService<AppearanceController>().Resources);
        if (e.Args.Contains("--galerie", StringComparer.Ordinal))
        {
            // Revue visuelle : la galerie des composants d'emblée.
            var shell = _services.GetRequiredService<ShellViewModel>();
            shell.Navigate(shell.Gallery);
        }
        var window = _services.GetRequiredService<ShellWindow>();
        MainWindow = window;
        window.Show();
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
