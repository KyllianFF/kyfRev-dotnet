using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Thème choisi par l'utilisateur.</summary>
public enum ThemePreference
{
    /// <summary>Comme Windows (clair ou sombre).</summary>
    System,

    /// <summary>Toujours clair.</summary>
    Light,

    /// <summary>Toujours sombre.</summary>
    Dark,
}

/// <summary>
/// Applique l'apparence : le thème choisi ou celui de Windows, le contraste élevé dès que Windows
/// l'active (il passe avant tout choix), les animations réduites si Windows le demande, et la
/// barre de titre assortie. Suit les changements faits dans Windows sans redémarrage.
/// </summary>
public sealed partial class AppearanceController : ObservableObject, IDisposable
{
    private readonly DesignResources _design;
    private readonly ISystemAppearance _system;
    private readonly IWindowChrome _chrome;
    private readonly Dispatcher _dispatcher;
    private readonly List<Window> _windows = [];

    /// <summary>Applique tout de suite l'apparence de Windows.</summary>
    public AppearanceController(DesignResources design, ISystemAppearance system, IWindowChrome chrome)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(chrome);
        _design = design;
        _system = system;
        _chrome = chrome;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _system.Changed += OnSystemChanged;
        Apply();
    }

    /// <summary>Thème choisi ; comme Windows par défaut.</summary>
    [ObservableProperty]
    public partial ThemePreference Preference { get; set; }

    /// <summary>Thème en vigueur.</summary>
    public ThemeKind Theme => _design.Theme;

    /// <summary>Ressources du design system, à fusionner dans celles de l'application.</summary>
    public ResourceDictionary Resources => _design.Dictionary;

    /// <summary>Accorde la barre de titre de <paramref name="window"/> au thème, dès qu'elle existe.</summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows.Add(window);
        window.SourceInitialized += (_, _) => ApplyTitleBar(window);
        window.Closed += (_, _) => _windows.Remove(window);
        ApplyTitleBar(window);
    }

    /// <summary>Cesse de suivre Windows.</summary>
    public void Dispose() => _system.Changed -= OnSystemChanged;

    partial void OnPreferenceChanged(ThemePreference value) => Apply();

    private void OnSystemChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(Apply);

    private void Apply()
    {
        _design.Theme = _system.HighContrast
            ? ThemeKind.HighContrast
            : Preference switch
            {
                ThemePreference.Light => ThemeKind.Light,
                ThemePreference.Dark => ThemeKind.Dark,
                _ => _system.PrefersDarkTheme ? ThemeKind.Dark : ThemeKind.Light,
            };
        _design.ReducedMotion = _system.ReducedMotion;
        foreach (var window in _windows)
        {
            ApplyTitleBar(window);
        }
        OnPropertyChanged(nameof(Theme));
    }

    private void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            _chrome.UseDarkTitleBar(handle, _design.Theme == ThemeKind.Dark);
        }
    }
}
