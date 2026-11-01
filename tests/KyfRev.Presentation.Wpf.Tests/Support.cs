using System.Windows.Threading;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Préférences de Windows simulées : modifiables, et leur changement signalé à la demande.</summary>
internal sealed class FakeAppearance : ISystemAppearance
{
    public event EventHandler? Changed;

    public bool PrefersDarkTheme { get; set; }

    public bool HighContrast { get; set; }

    public bool ReducedMotion { get; set; }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Cadre de fenêtre simulé : note chaque demande de barre de titre.</summary>
internal sealed class FakeChrome : IWindowChrome
{
    public List<(nint Handle, bool Dark)> Calls { get; } = [];

    public bool UseDarkTitleBar(nint windowHandle, bool dark)
    {
        Calls.Add((windowHandle, dark));
        return true;
    }
}

/// <summary>Une coquille assemblée comme dans l'application, avec des préférences de Windows simulées.</summary>
internal sealed class TestShell
{
    public TestShell(bool dark = false)
    {
        System = new FakeAppearance { PrefersDarkTheme = dark };
        Design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        Appearance = new AppearanceController(Design, System, Chrome);
        Gallery = new GalleryPageViewModel(Appearance);
        Shell = new ShellViewModel(CorePages.All(), Appearance, Gallery);
    }

    public FakeAppearance System { get; }

    public FakeChrome Chrome { get; } = new();

    public DesignResources Design { get; }

    public AppearanceController Appearance { get; }

    public GalleryPageViewModel Gallery { get; }

    public ShellViewModel Shell { get; }

    /// <summary>Exécute ce qui attend sur le fil de l'interface.</summary>
    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
}

/// <summary>Racine du dépôt : le dossier qui contient la solution.</summary>
internal static class Repository
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(folder.FullName, "KyfRev.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("Solution introuvable.");
    }
}
