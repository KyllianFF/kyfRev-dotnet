using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// La coquille : menu, recherche de commandes (Ctrl+K), aide des raccourcis (F1), mode
/// concentration (Ctrl+Maj+F) et zoom de l'interface (Ctrl+plus, Ctrl+moins, Ctrl+0). Le menu
/// s'adapte à la largeur de la fenêtre (<see cref="ShellLayout"/>) : barre latérale, colonne
/// d'icônes, ou onglets en bas avec « Plus » pour les autres vues. Sans coffre ouvert, elle montre
/// l'accueil : le menu est masqué et les vues ne s'ouvrent pas.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    /// <summary>Plus petit zoom.</summary>
    public const double MinimumZoom = 0.8;

    /// <summary>Plus grand zoom.</summary>
    public const double MaximumZoom = 2.0;

    /// <summary>Pas du zoom.</summary>
    public const double ZoomStep = 0.1;

    private readonly IReadOnlyList<NavigationItem> _core;
    private readonly IReadOnlyList<PaletteEntry> _commandsWithoutVault;
    private IReadOnlyList<PaletteEntry> _commands;
    private IReadOnlyList<PaletteEntry> _extensionCommands = [];
    private IReadOnlyList<NavigationItem> _moduleEntries = [];

    /// <summary>Un coffre est ouvert : les pages ouvertes sont prévenues (<see cref="INavigationAware"/>).</summary>
    private bool _workspace;

    /// <summary>La coquille, avec les entrées du menu (cœur et modules actifs) et l'apparence.</summary>
    public ShellViewModel(IEnumerable<NavigationItem> navigation, AppearanceController appearance, object gallery)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(appearance);
        _core = [.. navigation];
        Appearance = appearance;
        appearance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppearanceController.Theme))
            {
                OnPropertyChanged(nameof(ThemeGlyph));
                OnPropertyChanged(nameof(ThemeLabel));
                OnPropertyChanged(nameof(CanToggleTheme));
            }
        };
        Gallery = new NavigationItem("galerie", ShellText.Gallery, Icons.Palette, int.MaxValue, gallery);
        Shortcuts =
        [
            new("Ctrl+K", ShellText.ShortcutSearch),
            new("F1", ShellText.ShortcutHelp),
            new("Ctrl+Maj+F", ShellText.ShortcutFocusMode),
            new("F11", ShellText.ShortcutFullScreen),
            new("Ctrl+Entrée", ShellText.ShortcutValidate),
            new("Ctrl+→", ShellText.ShortcutNext),
            new("Ctrl+1 … Ctrl+9", ShellText.ShortcutGoTo),
            new("Ctrl+plus", ShellText.ShortcutZoomIn),
            new("Ctrl+moins", ShellText.ShortcutZoomOut),
            new("Ctrl+0", ShellText.ShortcutZoomReset),
        ];
        Zoom = 1.0;
        WindowWidth = ShellLayout.SidebarMinimum;
        _commandsWithoutVault = OtherCommands();
        ArrangeMenu();
        PaletteResults = _commands;
        PaletteQuery = string.Empty;
        Home = MainNavigation.Count > 0 ? MainNavigation[0] : SecondaryNavigation.Count > 0 ? SecondaryNavigation[0] : Gallery;
        Current = Home;
        SelectedMain = MainNavigation.Contains(Current) ? Current : null;
    }

    /// <summary>L'icône du bouton de thème : le thème où il mène.</summary>
    public string ThemeGlyph => Appearance.Theme == ThemeKind.Dark ? Icons.Sun : Icons.Moon;

    /// <summary>Le nom du bouton de thème.</summary>
    public string ThemeLabel => Appearance.Theme == ThemeKind.Dark ? ShellText.ThemeToLight : ShellText.ThemeToDark;

    /// <summary>Le thème se change d'un clic, sauf en contraste élevé, qui passe avant.</summary>
    public bool CanToggleTheme => Appearance.Theme != ThemeKind.HighContrast;

    /// <summary>Un coffre va s'afficher : c'est le moment de charger ce qui dépend de lui.</summary>
    public event EventHandler? WorkspaceOpening;

    /// <summary>Entrées du haut du menu.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NavigationItem> MainNavigation { get; private set; }

    /// <summary>Entrées du bas du menu.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NavigationItem> SecondaryNavigation { get; private set; }

    /// <summary>Les premières vues, en onglets quand la fenêtre est étroite.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NavigationItem> TabNavigation { get; private set; }

    /// <summary>Les autres vues, sous « Plus » quand la fenêtre est étroite.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<NavigationItem> OverflowNavigation { get; private set; }

    /// <summary>Largeur de la fenêtre, en pixels indépendants, avant zoom.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layout))]
    public partial double WindowWidth { get; set; }

    /// <summary>Disposition de la navigation : onglets, colonne d'icônes ou barre latérale.</summary>
    public NavigationLayout Layout => ShellLayout.For(WindowWidth, Zoom);

    /// <summary>Onglet choisi ; rien quand la vue ouverte est rangée sous « Plus ».</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedTab { get; set; }

    /// <summary>La vue ouverte est rangée sous « Plus ».</summary>
    public bool IsOverflowActive => OverflowNavigation.Contains(Current);

    /// <summary>La galerie des composants, ouverte depuis la recherche.</summary>
    public NavigationItem Gallery { get; }

    /// <summary>La première vue du menu, ouverte avec le coffre.</summary>
    public NavigationItem Home { get; }

    /// <summary>Apparence (thème).</summary>
    public AppearanceController Appearance { get; }

    /// <summary>Raccourcis présentés par l'aide.</summary>
    public IReadOnlyList<Shortcut> Shortcuts { get; }

    /// <summary>Page affichée.</summary>
    [ObservableProperty]
    public partial NavigationItem Current { get; private set; }

    /// <summary>Entrée choisie dans le haut du menu.</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedMain { get; set; }

    /// <summary>Entrée choisie dans le bas du menu.</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedSecondary { get; set; }

    /// <summary>L'accueil est affiché : aucun coffre n'est ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenuVisible))]
    public partial bool IsWelcome { get; private set; }

    /// <summary>Le menu latéral est affiché : un coffre est ouvert, hors du mode concentration et du plein écran.</summary>
    public bool IsMenuVisible => !IsWelcome && !IsFocusMode && !IsFullScreen;

    /// <summary>Plein écran (F11) : la fenêtre occupe tout l'écran, sans menu, pour réviser sans distraction.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenuVisible))]
    public partial bool IsFullScreen { get; private set; }

    /// <summary>Mode concentration : le menu est masqué.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenuVisible))]
    public partial bool IsFocusMode { get; set; }

    /// <summary>Zoom de l'interface (1 : taille normale).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyPropertyChangedFor(nameof(Layout))]
    public partial double Zoom { get; private set; }

    /// <summary>« Zoom : 120 % ».</summary>
    public string ZoomLabel => ShellText.Zoom((int)Math.Round(Zoom * 100));

    /// <summary>Recherche de commandes ouverte.</summary>
    [ObservableProperty]
    public partial bool IsPaletteOpen { get; private set; }

    /// <summary>Texte cherché.</summary>
    [ObservableProperty]
    public partial string PaletteQuery { get; set; }

    /// <summary>Commandes qui correspondent au texte cherché.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResult))]
    public partial IReadOnlyList<PaletteEntry> PaletteResults { get; private set; }

    /// <summary>Aucune commande ne correspond.</summary>
    public bool HasNoResult => PaletteResults.Count == 0;

    /// <summary>Commande mise en évidence (Entrée l'exécute).</summary>
    [ObservableProperty]
    public partial PaletteEntry? SelectedResult { get; set; }

    /// <summary>Aide des raccourcis ouverte.</summary>
    [ObservableProperty]
    public partial bool IsHelpOpen { get; set; }

    /// <summary>
    /// Montre le déverrouillage d'un coffre chiffré (<paramref name="page"/>) : rien de lui n'est lu, ni menu ni
    /// recherche ; un coffre resté ouvert derrière ne reçoit plus rien jusqu'au retour à l'espace de travail.
    /// </summary>
    public void ShowUnlock(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ClosePalette();
        IsHelpOpen = false;
        IsWelcome = true;
        _workspace = false;
        SelectedMain = null;
        SelectedSecondary = null;
        Current = new NavigationItem("deverrouillage", EncryptionText.LockedPage, Icons.Lock, int.MaxValue, page);
        UpdateResults();
    }

    /// <summary>Montre l'accueil (<paramref name="page"/>) : aucun coffre n'est ouvert.</summary>
    public void ShowWelcome(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        IsWelcome = true;
        SelectedMain = null;
        SelectedSecondary = null;
        Current = new NavigationItem("accueil", ShellText.Welcome, Icons.Home, int.MaxValue, page);
        UpdateResults();
    }

    /// <summary>
    /// Un module vient de s'activer ou de se désactiver : le menu change, et la vue ouverte reste choisie, sauf si
    /// c'était celle d'un module qui s'en va.
    /// </summary>
    public void SetModuleEntries(IEnumerable<NavigationItem> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var left = _moduleEntries.Contains(Current);
        _moduleEntries = [.. entries];
        var current = Current;
        ArrangeMenu();
        UpdateResults();
        if (left)
        {
            Navigate(Home);
            return;
        }
        SelectedMain = MainNavigation.Contains(current) ? current : null;
        SelectedSecondary = SecondaryNavigation.Contains(current) ? current : null;
    }

    /// <summary>Un coffre est ouvert : le menu revient, sur sa première vue, qui est prévenue.</summary>
    public void ShowWorkspace()
    {
        WorkspaceOpening?.Invoke(this, EventArgs.Empty);
        IsWelcome = false;
        _workspace = true;
        UpdateResults();
        if (ReferenceEquals(Current, Home))
        {
            Activate(Home);
        }
        else
        {
            Navigate(Home);
        }
    }

    /// <summary>Ouvre la page d'une entrée ; rien tant que l'accueil est affiché.</summary>
    [RelayCommand]
    public void Navigate(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsWelcome)
        {
            return;
        }
        if (MainNavigation.Contains(item))
        {
            SelectedMain = item;
        }
        else if (SecondaryNavigation.Contains(item))
        {
            SelectedSecondary = item;
        }
        else
        {
            SelectedMain = null;
            SelectedSecondary = null;
            Current = item;
        }
    }

    /// <summary>Ouvre la vue <paramref name="position"/> (1 à 9) du haut du menu.</summary>
    [RelayCommand]
    public void NavigateToPosition(string position)
    {
        if (int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out var rank) && rank >= 1 && rank <= MainNavigation.Count)
        {
            Navigate(MainNavigation[rank - 1]);
        }
    }

    /// <summary>Ouvre la recherche de commandes, vide.</summary>
    [RelayCommand]
    public void OpenPalette()
    {
        IsHelpOpen = false;
        PaletteQuery = string.Empty;
        UpdateResults();
        IsPaletteOpen = true;
    }

    /// <summary>Ferme la recherche de commandes.</summary>
    [RelayCommand]
    public void ClosePalette() => IsPaletteOpen = false;

    /// <summary>Déplace la mise en évidence dans les résultats (« 1 » ou « -1 »), en boucle.</summary>
    [RelayCommand]
    public void MoveSelection(string delta)
    {
        if (PaletteResults.Count == 0 || !int.TryParse(delta, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var step))
        {
            return;
        }
        var index = SelectedResult is { } selected ? IndexOf(selected) : -1;
        SelectedResult = PaletteResults[(((index + step) % PaletteResults.Count) + PaletteResults.Count) % PaletteResults.Count];
    }

    /// <summary>Exécute la commande mise en évidence et ferme la recherche.</summary>
    [RelayCommand]
    public void RunSelected()
    {
        if (SelectedResult is { } entry)
        {
            IsPaletteOpen = false;
            entry.Execute();
        }
    }

    /// <summary>Masque ou affiche le menu.</summary>
    [RelayCommand]
    public void ToggleFocusMode() => IsFocusMode = !IsFocusMode;

    /// <summary>Passe du thème clair au thème sombre, ou l'inverse. Ce choix remplace « comme Windows » ; il se retrouve dans les Paramètres.</summary>
    [RelayCommand]
    private void ToggleTheme() => Appearance.Preference = Appearance.Theme == ThemeKind.Dark ? ThemePreference.Light : ThemePreference.Dark;

    /// <summary>Passe en plein écran, ou en sort (F11).</summary>
    [RelayCommand]
    public void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

    /// <summary>Sort du plein écran (Échap).</summary>
    [RelayCommand]
    public void ExitFullScreen() => IsFullScreen = false;

    /// <summary>Donne cette taille à l'interface, dans les limites du zoom.</summary>
    public void SetZoom(double zoom) => Zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    /// <summary>Agrandit l'interface d'un pas.</summary>
    [RelayCommand]
    public void ZoomIn() => Zoom = Math.Min(MaximumZoom, Math.Round(Zoom + ZoomStep, 1));

    /// <summary>Réduit l'interface d'un pas.</summary>
    [RelayCommand]
    public void ZoomOut() => Zoom = Math.Max(MinimumZoom, Math.Round(Zoom - ZoomStep, 1));

    /// <summary>Revient à la taille normale.</summary>
    [RelayCommand]
    public void ResetZoom() => Zoom = 1.0;

    /// <summary>Affiche l'aide des raccourcis.</summary>
    [RelayCommand]
    public void ShowHelp()
    {
        IsPaletteOpen = false;
        IsHelpOpen = true;
    }

    /// <summary>Texte normalisé pour la recherche : minuscules, sans accents.</summary>
    internal static string Fold(string text)
    {
        var decomposed = text.ToLower(CultureInfo.CurrentCulture).Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            folded.Append(c);
        }
        return folded.ToString().Normalize(NormalizationForm.FormC);
    }

    partial void OnCurrentChanged(NavigationItem value)
    {
        SelectedTab = TabNavigation.Contains(value) ? value : null;
        OnPropertyChanged(nameof(IsOverflowActive));
        if (_workspace)
        {
            Activate(value);
        }
    }

    partial void OnSelectedTabChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            Navigate(value);
        }
    }

    private static void Activate(NavigationItem item) => (item.Page as INavigationAware)?.OnNavigatedTo();

    [MemberNotNull(nameof(MainNavigation), nameof(SecondaryNavigation), nameof(TabNavigation), nameof(OverflowNavigation), nameof(_commands))]
    private void ArrangeMenu()
    {
        var items = _core.Concat(_moduleEntries).OrderBy(item => item.Order).ThenBy(item => item.Label, StringComparer.CurrentCulture).ToList();
        MainNavigation = [.. items.Where(item => !item.IsSecondary)];
        SecondaryNavigation = [.. items.Where(item => item.IsSecondary)];
        TabNavigation = [.. MainNavigation.Take(ShellLayout.TabCount)];
        OverflowNavigation = [.. MainNavigation.Skip(ShellLayout.TabCount), .. SecondaryNavigation];
        _commands = [.. NavigationCommands(), .. _extensionCommands, .. _commandsWithoutVault];
    }

    /// <summary>Les commandes des extensions actives, dans la recherche (Ctrl+K).</summary>
    public void SetExtensionCommands(IEnumerable<PaletteEntry> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _extensionCommands = [.. commands];
        _commands = [.. NavigationCommands(), .. _extensionCommands, .. _commandsWithoutVault];
        UpdateResults();
    }

    partial void OnSelectedMainChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedSecondary = null;
            Current = value;
        }
    }

    partial void OnSelectedSecondaryChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedMain = null;
            Current = value;
        }
    }

    partial void OnPaletteQueryChanged(string value) => UpdateResults();

    private int IndexOf(PaletteEntry entry)
    {
        for (var i = 0; i < PaletteResults.Count; i++)
        {
            if (ReferenceEquals(PaletteResults[i], entry))
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateResults()
    {
        var words = Fold(PaletteQuery).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        PaletteResults = [.. (IsWelcome ? _commandsWithoutVault : _commands).Where(entry => words.All(Fold(entry.Label).Contains))];
        SelectedResult = PaletteResults.Count > 0 ? PaletteResults[0] : null;
    }

    /// <summary>Les commandes qui ouvrent une vue : sans coffre ouvert, elles ne sont pas proposées.</summary>
    private List<PaletteEntry> NavigationCommands()
    {
        var position = 0;
        var commands = MainNavigation
            .Select(item => new PaletteEntry(ShellText.GoTo(item.Label), ++position <= 9 ? $"Ctrl+{position}" : null, item.Glyph, () => Navigate(item)))
            .Concat(SecondaryNavigation.Select(item => new PaletteEntry(ShellText.GoTo(item.Label), null, item.Glyph, () => Navigate(item))))
            .ToList();
        commands.Add(new(ShellText.GoTo(Gallery.Label), null, Gallery.Glyph, () => Navigate(Gallery)));
        return commands;
    }

    /// <summary>Les commandes de la coquille elle-même (mode concentration, zoom, aide, thème).</summary>
    private List<PaletteEntry> OtherCommands()
    {
        List<PaletteEntry> commands =
        [
            new(ShellText.FocusMode, "Ctrl+Maj+F", Icons.FocusMode, ToggleFocusMode),
            new(ShellText.ShortcutFullScreen, "F11", Icons.FocusMode, ToggleFullScreen),
            new(ShellText.ShortcutZoomIn, "Ctrl+plus", Icons.ZoomIn, ZoomIn),
            new(ShellText.ShortcutZoomOut, "Ctrl+moins", Icons.ZoomOut, ZoomOut),
            new(ShellText.ShortcutZoomReset, "Ctrl+0", Icons.ZoomOut, ResetZoom),
            new(ShellText.Help, "F1", Icons.Help, ShowHelp),
        ];
        foreach (var (preference, label) in new[] { (ThemePreference.System, ShellText.ThemeSystem), (ThemePreference.Light, ShellText.ThemeLight), (ThemePreference.Dark, ShellText.ThemeDark) })
        {
            commands.Add(new(ShellText.ThemeTo(label.ToLower(CultureInfo.CurrentCulture)), null, Icons.Theme, () => Appearance.Preference = preference));
        }
        return commands;
    }
}
