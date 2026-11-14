using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Applique l'apparence : le thème choisi ou celui de Windows, le contraste élevé dès que Windows
/// l'active ou qu'il est demandé ici (il passe avant tout choix), les animations réduites de même,
/// les petits boutons agrandis et la barre de titre assortie. Suit les changements faits dans
/// Windows sans redémarrage.
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

    /// <summary>Contraste élevé, même si Windows ne l'active pas.</summary>
    [ObservableProperty]
    public partial bool ForcesHighContrast { get; set; }

    /// <summary>Animations réduites, même si Windows ne le demande pas.</summary>
    [ObservableProperty]
    public partial bool ForcesReducedMotion { get; set; }

    /// <summary>Les petits boutons sont agrandis.</summary>
    [ObservableProperty]
    public partial bool LargerTargets { get; set; }

    /// <summary>Thème en vigueur.</summary>
    public ThemeKind Theme => _design.Theme;

    /// <summary>Le fond en vigueur est sombre : le thème sombre, ou un contraste élevé sombre.</summary>
    public bool IsDark => _design.IsDark;

    /// <summary>Windows est en contraste élevé : ses couleurs passent avant tout choix de thème.</summary>
    public bool FollowsWindowsContrast => _system.HighContrast;

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

    partial void OnForcesHighContrastChanged(bool value) => Apply();

    partial void OnForcesReducedMotionChanged(bool value) => Apply();

    partial void OnLargerTargetsChanged(bool value) => _design.LargerTargets = value;

    private void OnSystemChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(Apply);

    private void Apply()
    {
        var chosen = Preference switch
        {
            ThemePreference.Light => ThemeKind.Light,
            ThemePreference.Dark => ThemeKind.Dark,
            _ => _system.PrefersDarkTheme ? ThemeKind.Dark : ThemeKind.Light,
        };
        // Le contraste de Windows prend ses couleurs ; forcé dans kyfRev, il garde le thème choisi, clair ou sombre.
        _design.HighContrastPalette = _system.HighContrast
            ? HighContrastPalette.Windows
            : chosen == ThemeKind.Dark ? HighContrastPalette.Dark : HighContrastPalette.Light;
        _design.Theme = _system.HighContrast || ForcesHighContrast ? ThemeKind.HighContrast : chosen;
        _design.ReducedMotion = _system.ReducedMotion || ForcesReducedMotion;
        foreach (var window in _windows)
        {
            ApplyTitleBar(window);
        }
        OnPropertyChanged(nameof(Theme));
    }

    /// <summary>Ce qui dépasse de l'écran d'une fenêtre agrandie, en pixels.</summary>
    internal WindowOverhang MaximizedOverhang(nint window) => _chrome.MaximizedOverhang(window);

    /// <summary>Ce qui se voit vraiment de la fenêtre, en pixels ; vide si Windows ne le dit pas.</summary>
    internal ScreenBounds VisibleBounds(nint window) => _chrome.VisibleBounds(window);

    /// <summary>L'espace de travail de l'écran où se trouve la fenêtre, en pixels.</summary>
    internal ScreenArea WorkArea(nint window) => _chrome.WorkArea(window);

    private void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            _chrome.UseDarkTitleBar(handle, _design.IsDark);
        }
    }
}
