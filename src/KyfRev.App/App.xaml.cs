using System.Windows;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace KyfRev.App;

/// <summary>
/// L'application : assemble les services, applique l'apparence de Windows, ouvre le coffre prévu
/// (l'accueil à défaut), puis la fenêtre principale. Avec <c>--galerie</c>, la fenêtre s'ouvre sur
/// la galerie des composants, sans ouvrir de coffre.
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

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
