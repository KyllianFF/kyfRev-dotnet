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
/// La coquille : menu, recherche (Ctrl+K) des commandes et dans le coffre, aide des raccourcis (F1),
/// mode concentration (Ctrl+Maj+F) et zoom de l'interface (Ctrl+plus, Ctrl+moins, Ctrl+0). Le menu
/// s'adapte à la largeur de la fenêtre (<see cref="ShellLayout"/>) : barre latérale, colonne
/// d'icônes, ou onglets en bas avec « Plus » pour les autres vues. Sans coffre ouvert, elle montre
/// l'accueil : le menu est masqué et les vues ne s'ouvrent pas.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IForgetsVault, IStopsSearch, IFocusMode
{
    /// <summary>Plus petit zoom.</summary>
    public const double MinimumZoom = 0.8;

    /// <summary>Plus grand zoom.</summary>
    public const double MaximumZoom = 2.0;

    /// <summary>Pas du zoom.</summary>
    public const double ZoomStep = 0.1;

    /// <summary>Caractères à écrire, au moins, pour chercher dans le coffre.</summary>
    private const int MinimumSearch = 2;

    /// <summary>Le temps d'attendre la frappe suivante avant de chercher dans le coffre.</summary>
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(150);

    private readonly IReadOnlyList<NavigationItem> _core;
    private readonly IReadOnlyList<PaletteEntry> _commandsWithoutVault;
    private readonly LatestSearch _vaultSearch = new();
    private IReadOnlyList<PaletteEntry> _commands;
    private IReadOnlyList<PaletteEntry> _extensionCommands = [];
    private List<PaletteEntry> _hits = [];
    private IReadOnlyList<NavigationItem> _moduleEntries = [];
    private double _screenHeight = double.PositiveInfinity;

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
        ChosenZoom = 1.0;
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
    public string ThemeGlyph => Appearance.IsDark ? Icons.Sun : Icons.Moon;

    /// <summary>Le nom du bouton de thème.</summary>
    public string ThemeLabel => Appearance.IsDark ? ShellText.ThemeToLight : ShellText.ThemeToDark;

    /// <summary>
    /// Le thème se change d'un clic, en contraste élevé forcé dans kyfRev aussi (il passe du clair au sombre) ; pas quand Windows
    /// est en contraste élevé : ses couleurs passent avant.
    /// </summary>
    public bool CanToggleTheme => !(Appearance.Theme == ThemeKind.HighContrast && Appearance.FollowsWindowsContrast);

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
    [NotifyPropertyChangedFor(nameof(Layout), nameof(CanToggleMenu), nameof(IsMenuVisible))]
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

    /// <summary>Raccourcis présentés par l'aide : ceux qui se changent y montrent leur touche en vigueur.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Shortcut> Shortcuts { get; private set; } = CurrentShortcuts();

    /// <summary>Les raccourcis viennent de changer : l'aide les relit.</summary>
    public void RefreshShortcuts() => Shortcuts = CurrentShortcuts();

    private static List<Shortcut> CurrentShortcuts() =>
    [
        new(ShortcutMap.Current.DisplayOf("recherche"), ShellText.ShortcutSearch),
        new(ShortcutMap.Current.DisplayOf("aide"), ShellText.ShortcutHelp),
        new(ShortcutMap.Current.DisplayOf("concentration"), ShellText.ShortcutFocusMode),
        new(ShortcutMap.Current.DisplayOf("plein-ecran"), ShellText.ShortcutFullScreen),
        new(ShortcutMap.Current.DisplayOf("valider"), ShellText.ShortcutValidate),
        new("Entrée, Espace", ShellText.ShortcutReveal),
        new("H", ShellText.ShortcutHighlight),
        new(ShortcutMap.Current.DisplayOf("ecrire"), ShellText.ShortcutWrite),
        new(ShortcutMap.Current.DisplayOf("imprimer"), ShellText.ShortcutPrint),
        new(ShortcutMap.Current.DisplayOf("partager"), ShellText.ShortcutShare),
        new(ShortcutMap.Current.DisplayOf("capture"), ShellText.ShortcutCapture),
        new("Ctrl+→", ShellText.ShortcutNext),
        new("Ctrl+1 … Ctrl+9", ShellText.ShortcutGoTo),
        new("Ctrl+plus", ShellText.ShortcutZoomIn),
        new("Ctrl+moins", ShellText.ShortcutZoomOut),
        new("Ctrl+0", ShellText.ShortcutZoomReset),
        new(ShortcutMap.Current.DisplayOf("verrouiller"), ShellText.ShortcutLock),
    ];

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
    [NotifyPropertyChangedFor(nameof(IsMenuVisible), nameof(ShowsSearchHint), nameof(NoResultText), nameof(PaletteLabel))]
    [NotifyCanExecuteChangedFor(nameof(LockCommand))]
    public partial bool IsWelcome { get; private set; }

    /// <summary>Le coffre ouvert est chiffré : il se verrouille (Ctrl+Maj+L), et la recherche le propose.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LockCommand))]
    public partial bool CanLock { get; set; }

    /// <summary>Ce qui verrouille le coffre (<see cref="VaultLocking"/>) : la racine de composition le donne.</summary>
    public Func<Task>? Locking { get; set; }

    /// <summary>
    /// Un coffre chiffré ouvert se verrouille, même derrière l'écran de déverrouillage d'un autre coffre choisi depuis les
    /// paramètres.
    /// </summary>
    private bool IsLockable => CanLock && Locking is not null;

    /// <summary>Verrouille le coffre chiffré ouvert : son écran de déverrouillage remplace les vues.</summary>
    [RelayCommand(CanExecute = nameof(IsLockable))]
    private Task LockAsync() => IsLockable ? Locking!() : Task.CompletedTask;

    partial void OnCanLockChanged(bool value)
    {
        RebuildCommands();
        UpdateResults();
    }

    /// <summary>
    /// Le menu est affiché : un coffre est ouvert, hors du plein écran, et le menu latéral n'est pas masqué. En fenêtre étroite, le menu
    /// passe en onglets au bas de la fenêtre et son bouton quitte la barre de titre : le masquage ne compte plus, sans quoi rien ne
    /// permettrait de changer de vue. Le choix reste retenu, et reprend dès que la fenêtre retrouve un menu latéral.
    /// </summary>
    public bool IsMenuVisible => !IsWelcome && !IsFullScreen && (!IsFocusMode || Layout == NavigationLayout.Tabs);

    /// <summary>Le bouton de la barre de titre peut masquer ou rendre le menu : en plein écran, il n'y a pas de menu à rendre.</summary>
    public bool CanToggleMenu => !IsFullScreen && Layout != NavigationLayout.Tabs;

    /// <summary>« Masquer le menu (Ctrl+Maj+F) » ou, quand il est masqué, « Afficher le menu (Ctrl+Maj+F) ».</summary>
    public string MenuToggleLabel => IsFocusMode ? ShellText.ExitFocusMode : ShellText.EnterFocusMode;

    /// <summary>Plein écran (F11) : la fenêtre occupe tout l'écran, sans menu, pour réviser sans distraction.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenuVisible), nameof(CanToggleMenu))]
    public partial bool IsFullScreen { get; private set; }

    /// <summary>Mode concentration : le menu est masqué.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenuVisible), nameof(MenuToggleLabel))]
    public partial bool IsFocusMode { get; set; }

    /// <summary>Zoom de l'interface (1 : taille normale) : celui choisi, tant que l'écran de la fenêtre peut le tenir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    [NotifyPropertyChangedFor(nameof(Layout), nameof(CanToggleMenu), nameof(IsMenuVisible))]
    [NotifyPropertyChangedFor(nameof(MinimumWindowWidth))]
    [NotifyPropertyChangedFor(nameof(MinimumWindowHeight))]
    public partial double Zoom { get; private set; }

    /// <summary>Le zoom choisi, celui qu'on retient : un écran plus petit le réduit sans le faire oublier.</summary>
    [ObservableProperty]
    public partial double ChosenZoom { get; private set; }

    /// <summary>Le plus fort zoom que l'écran de la fenêtre peut tenir, la fenêtre gardant sa largeur minimale.</summary>
    public double ZoomLimit { get; private set; } = MaximumZoom;

    /// <summary>La largeur minimale de la fenêtre : <see cref="ShellLayout.MinimumWidth"/> pixels vus, quel que soit le zoom.</summary>
    public double MinimumWindowWidth => ShellLayout.MinimumWidth * Zoom;

    /// <summary>La hauteur minimale de la fenêtre : <see cref="ShellLayout.MinimumHeight"/> pixels vus, jamais plus que l'écran.</summary>
    public double MinimumWindowHeight => Math.Min(ShellLayout.MinimumHeight * Zoom, _screenHeight);

    /// <summary>« Zoom : 120 % ».</summary>
    public string ZoomLabel => ShellText.Zoom((int)Math.Round(Zoom * 100));

    /// <summary>Recherche ouverte.</summary>
    [ObservableProperty]
    public partial bool IsPaletteOpen { get; private set; }

    /// <summary>Ce qui cherche dans le coffre (la racine de composition le donne) ; rien : seules les commandes se cherchent.</summary>
    public GlobalSearch? Search { get; set; }

    /// <summary>Ce que dit le champ de recherche : « Rechercher partout », ou seulement les commandes quand aucun coffre n'est cherché.</summary>
    public string PaletteLabel => CanSearch ? ShellText.SearchPlaceholder : ShellText.SearchCommandsPlaceholder;

    /// <summary>Texte cherché.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSearchHint), nameof(NoResultText))]
    public partial string PaletteQuery { get; set; }

    /// <summary>Commandes qui correspondent au texte cherché, puis ce qui a été trouvé dans le coffre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResult), nameof(ShowsKeyHelp))]
    public partial IReadOnlyList<PaletteEntry> PaletteResults { get; private set; }

    /// <summary>Rien ne correspond, et aucune recherche n'est en cours.</summary>
    public bool HasNoResult => PaletteResults.Count == 0 && !IsSearching && PaletteError is null;

    /// <summary>« Aucune commande ne correspond. », ou « Rien ne correspond à « … ». » quand le coffre a été cherché.</summary>
    public string NoResultText => CanSearchVault ? ShellText.SearchNothingOf(PaletteQuery.Trim()) : ShellText.NoCommand;

    /// <summary>Une recherche dans le coffre est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResult))]
    public partial bool IsSearching { get; private set; }

    /// <summary>Refus de la dernière recherche dans le coffre.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPaletteError), nameof(HasNoResult))]
    public partial string? PaletteError { get; private set; }

    /// <summary>La dernière recherche dans le coffre a été refusée.</summary>
    public bool HasPaletteError => PaletteError is not null;

    /// <summary>Trop peu est écrit pour chercher dans le coffre : on dit comment faire.</summary>
    public bool ShowsSearchHint => CanSearch && PaletteQuery.Trim().Length < MinimumSearch;

    /// <summary>Des résultats du coffre sont montrés : on rappelle les touches.</summary>
    public bool ShowsKeyHelp => _hits.Count > 0;

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

    /// <summary>
    /// Montre la conversion du coffre ouvert (<paramref name="page"/>), hors de l'espace de travail : ni menu ni recherche.
    /// Le coffre reste ouvert derrière jusqu'au début de la conversion, sans rien recevoir ; il se ferme ensuite pour être
    /// converti.
    /// </summary>
    public void ShowConversion(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ClosePalette();
        IsHelpOpen = false;
        IsWelcome = true;
        _workspace = false;
        SelectedMain = null;
        SelectedSecondary = null;
        Current = new NavigationItem("conversion", EncryptionText.ConversionPage, Icons.Lock, int.MaxValue, page);
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
        var replaced = _moduleEntries;
        _moduleEntries = [.. entries];
        var current = Current;
        ArrangeMenu();
        UpdateResults();
        if (left)
        {
            Navigate(Home);
        }
        else
        {
            SelectedMain = MainNavigation.Contains(current) ? current : null;
            SelectedSecondary = SecondaryNavigation.Contains(current) ? current : null;
        }
        // Les vues remplacées se ferment, et ce qu'elles gardaient ouvert avec elles (l'îlot des schémas et son moteur web).
        foreach (var page in replaced.Select(entry => entry.Page).Except(_moduleEntries.Select(entry => entry.Page)).OfType<IDisposable>())
        {
            page.Dispose();
        }
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

    /// <summary>Ouvre la recherche, vide.</summary>
    [RelayCommand]
    public void OpenPalette()
    {
        IsHelpOpen = false;
        PaletteQuery = string.Empty;
        UpdateResults();
        IsPaletteOpen = true;
    }

    /// <summary>Ferme la recherche : ce qu'elle avait trouvé dans le coffre n'est pas gardé.</summary>
    [RelayCommand]
    public void ClosePalette()
    {
        IsPaletteOpen = false;
        DropHits();
    }

    /// <summary>Oublie ce qui a été cherché et trouvé ; une recherche partie avant n'y remet rien.</summary>
    public void Forget()
    {
        IsPaletteOpen = false;
        PaletteQuery = string.Empty;
        DropHits();
    }

    /// <inheritdoc/>
    public void StopSearch() => _vaultSearch.Cancel();

    /// <inheritdoc/>
    public void ResumeSearch()
    {
        // Encore « en cours » : la recherche arrêtée n'a pas répondu, et personne ne l'a abandonnée depuis.
        if (IsSearching)
        {
            SearchVaultCommand.Execute(PaletteQuery.Trim());
        }
    }

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
            ClosePalette();
            entry.Execute();
        }
    }

    /// <summary>Masque ou affiche le menu latéral. En fenêtre étroite, il n'y en a pas : le choix fait en grand ne change pas.</summary>
    [RelayCommand]
    public void ToggleFocusMode()
    {
        if (Layout != NavigationLayout.Tabs)
        {
            IsFocusMode = !IsFocusMode;
        }
    }

    /// <summary>Passe du thème clair au thème sombre, ou l'inverse. Ce choix remplace « comme Windows » ; il se retrouve dans les Paramètres.</summary>
    [RelayCommand]
    private void ToggleTheme() => Appearance.Preference = Appearance.IsDark ? ThemePreference.Light : ThemePreference.Dark;

    /// <summary>Passe en plein écran, ou en sort (F11).</summary>
    [RelayCommand]
    public void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

    /// <summary>Sort du plein écran (Échap).</summary>
    [RelayCommand]
    public void ExitFullScreen() => IsFullScreen = false;

    /// <summary>Donne cette taille à l'interface, dans les limites du zoom.</summary>
    public void SetZoom(double zoom) => Choose(Math.Clamp(zoom, MinimumZoom, MaximumZoom));

    /// <summary>Agrandit l'interface d'un pas.</summary>
    [RelayCommand]
    public void ZoomIn()
    {
        // Arrivé à ce que l'écran tient, rien ne bouge : le zoom choisi n'est pas oublié pour autant.
        var next = Math.Min(Math.Min(MaximumZoom, ZoomLimit), Math.Round(Zoom + ZoomStep, 1));
        if (next > Zoom)
        {
            Choose(next);
        }
    }

    /// <summary>Réduit l'interface d'un pas.</summary>
    [RelayCommand]
    public void ZoomOut() => Choose(Math.Max(MinimumZoom, Math.Round(Zoom - ZoomStep, 1)));

    /// <summary>Revient à la taille normale.</summary>
    [RelayCommand]
    public void ResetZoom() => Choose(1.0);

    /// <summary>
    /// L'espace de travail de l'écran où se trouve la fenêtre, en pixels indépendants. Le zoom s'arrête là où la fenêtre ne tiendrait
    /// plus ses <see cref="ShellLayout.MinimumWidth"/> pixels vus, et revient au zoom choisi sur un écran assez grand.
    /// </summary>
    public void FitScreen(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }
        ZoomLimit = Math.Clamp(Math.Floor((width / ShellLayout.MinimumWidth * 10) + 1e-9) / 10, MinimumZoom, MaximumZoom);
        _screenHeight = height;
        Zoom = Math.Min(ChosenZoom, ZoomLimit);
        OnPropertyChanged(nameof(MinimumWindowHeight));
    }

    private void Choose(double zoom)
    {
        ChosenZoom = zoom;
        Zoom = Math.Min(zoom, ZoomLimit);
    }

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

    private List<string> _menuOrder = [];

    /// <summary>L'ordre choisi des vues du haut du menu : leurs identifiants séparés par des virgules ; vide pour l'ordre d'origine.</summary>
    public void SetMenuOrder(string? written)
    {
        var order = (written ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (order.SequenceEqual(_menuOrder))
        {
            return;
        }
        _menuOrder = order;
        var current = Current;
        ArrangeMenu();
        SelectedMain = MainNavigation.Contains(current) ? current : null;
    }

    [MemberNotNull(nameof(MainNavigation), nameof(SecondaryNavigation), nameof(TabNavigation), nameof(OverflowNavigation), nameof(_commands))]
    private void ArrangeMenu()
    {
        var items = _core.Concat(_moduleEntries).OrderBy(item => item.Order).ThenBy(item => item.Label, StringComparer.CurrentCulture).ToList();
        // L'ordre choisi dans les paramètres passe avant celui d'origine ; une vue qu'il ne nomme pas garde sa place, après.
        MainNavigation = [.. items.Where(item => !item.IsSecondary).OrderBy(item => _menuOrder.IndexOf(item.Id) is var rank and >= 0 ? rank : int.MaxValue)];
        SecondaryNavigation = [.. items.Where(item => item.IsSecondary)];
        TabNavigation = [.. MainNavigation.Take(ShellLayout.TabCount)];
        OverflowNavigation = [.. MainNavigation.Skip(ShellLayout.TabCount), .. SecondaryNavigation];
        RebuildCommands();
    }

    /// <summary>Les commandes avec un coffre ouvert : ses vues, celles des extensions, le verrouillage s'il est chiffré, et celles de la coquille.</summary>
    [MemberNotNull(nameof(_commands))]
    private void RebuildCommands() =>
        _commands = [.. NavigationCommands(), .. CaptureCommands(), .. _extensionCommands, .. LockCommands(), .. _commandsWithoutVault];

    private List<PaletteEntry> LockCommands() =>
        CanLock ? [new(ShellText.ShortcutLock, "Ctrl+Maj+L", Icons.Lock, () => LockCommand.Execute(null))] : [];

    /// <summary>Les commandes des extensions actives, dans la recherche (Ctrl+K).</summary>
    public void SetExtensionCommands(IEnumerable<PaletteEntry> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _extensionCommands = [.. commands];
        RebuildCommands();
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

    partial void OnPaletteQueryChanged(string value)
    {
        if (CanSearchVault)
        {
            IsSearching = true;
            SearchVaultCommand.Execute(value.Trim());
        }
        else
        {
            DropHits();
        }
        UpdateResults();
    }

    /// <summary>Cherche dans le coffre, une fois la frappe arrêtée un instant ; les résultats d'avant restent jusqu'à la réponse.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task SearchVaultAsync(string text)
    {
        var search = Search!;
        // Les services des modules se lisent ici, sur le fil de l'interface : ils changent quand un module s'active.
        var providers = search.Providers();
        return _vaultSearch.RunAsync(SearchPause, token => search.UseCases.Everywhere(text, providers, token), outcome =>
        {
            IsSearching = false;
            PaletteError = outcome.Error;
            _hits = outcome.Succeeded ? SearchEntries.Of(outcome.Value, search.Open) : [];
            var selected = SelectedResult;
            UpdateResults();
            // Une ligne déjà choisie, que la réponse n'a pas remplacée, le reste.
            if (selected is not null && IndexOf(selected) >= 0)
            {
                SelectedResult = selected;
            }
        });
    }

    /// <summary>Un coffre est ouvert, et de quoi le chercher est donné.</summary>
    private bool CanSearch => Search is not null && !IsWelcome;

    /// <summary>Assez est écrit pour chercher dans le coffre, et un coffre est ouvert.</summary>
    private bool CanSearchVault => CanSearch && PaletteQuery.Trim().Length >= MinimumSearch;

    /// <summary>Arrête la recherche dans le coffre, et oublie ce qu'elle avait trouvé.</summary>
    private void DropHits()
    {
        _vaultSearch.Cancel();
        IsSearching = false;
        PaletteError = null;
        if (_hits.Count > 0)
        {
            _hits = [];
            UpdateResults();
        }
    }

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
        PaletteResults = [.. (IsWelcome ? _commandsWithoutVault : _commands).Where(entry => words.All(Fold(entry.Label).Contains)), .. _hits];
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
