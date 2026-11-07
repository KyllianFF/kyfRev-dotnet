using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.Application.Modules;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>La vue d'une extension : ce qu'elle affiche, redessiné après chaque action.</summary>
public sealed partial class ExtensionPageViewModel : ObservableObject, INavigationAware, IForgetsVault
{
    private readonly string _id;
    private readonly ExtensionEngine _engine;
    private readonly Func<bool> _isDark;
    private int _generation;

    /// <summary>La vue de cette extension.</summary>
    /// <param name="id">L'extension.</param>
    /// <param name="title">Le titre de sa vue.</param>
    /// <param name="engine">Le moteur où tourne son code.</param>
    /// <param name="isDark">Le thème est sombre.</param>
    public ExtensionPageViewModel(string id, string title, ExtensionEngine engine, Func<bool> isDark)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(isDark);
        _id = id;
        Title = title;
        _engine = engine;
        _isDark = isDark;
    }

    /// <summary>Une action vient d'aboutir : ce qu'elle a changé peut changer son compteur.</summary>
    public event EventHandler? Acted;

    /// <summary>Le titre de sa vue.</summary>
    public string Title { get; }

    /// <summary>Ce qu'elle affiche.</summary>
    [ObservableProperty]
    public partial ExtensionContent? Content { get; private set; }

    /// <summary>Elle n'a pas répondu, ou elle a refusé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une erreur est à dire.</summary>
    public bool HasError => Error is not null;

    /// <summary>Une action ou un rendu est en cours.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>La dernière action : le focus y revient après le nouveau rendu.</summary>
    internal string? LastAction { get; private set; }

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        LastAction = null;
        _ = RenderAsync();
    }

    /// <summary>
    /// Oublie ce que l'extension a affiché du coffre : son rendu, son refus et sa dernière action. Sa vue se redessine à sa
    /// prochaine ouverture ; un rendu ou une action partis avant n'y remettent rien.
    /// </summary>
    public void Forget()
    {
        _generation++;
        Content = null;
        Error = null;
        IsBusy = false;
        LastAction = null;
    }

    /// <summary>Demande à l'extension ce qu'elle affiche maintenant.</summary>
    public async Task RenderAsync()
    {
        var generation = _generation;
        var answer = await _engine.RenderViewAsync(_id, _isDark()).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (answer.Succeeded)
        {
            Error = null;
            Content = answer.Content ?? ExtensionContent.Empty;
            return;
        }
        Error = answer.Error;
    }

    /// <summary>Transmet une action, puis redessine. Une action à la fois : un double-clic n'en fait pas deux.</summary>
    public async Task ActAsync(ExtensionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsBusy)
        {
            return;
        }
        IsBusy = true;
        LastAction = action.Name;
        var generation = _generation;
        var answer = await _engine.ActAsync(_id, action.Name, action.Value, action.Data).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (answer.Succeeded)
        {
            await RenderAsync().ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
        }
        else
        {
            Error = answer.Error;
        }
        IsBusy = false;
        Acted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Dit un refus venu d'une commande lancée ailleurs (Ctrl+K, clic droit).</summary>
    internal void Report(string error) => Error = error;
}

/// <summary>
/// Ce que les extensions actives ajoutent à l'interface : leurs vues et leurs entrées du menu, avec leur icône et leur compteur, leurs
/// commandes dans Ctrl+K, leurs entrées au clic droit et leurs encarts sur « Aujourd'hui ». Tout se lit dans leur manifeste : leur code
/// ne démarre qu'au premier usage.
/// </summary>
public sealed class ExtensionPages : IForgetsVault
{
    /// <summary>La clé sous laquelle les vues trouvent les entrées des extensions dans les ressources de l'application.</summary>
    public const string ResourceKey = "Kyf.ExtensionPages";

    private const int FirstOrder = 70;
    private const double PictureSize = 16;

    private readonly ExtensionLibrary _library;
    private readonly ExtensionEngine _engine;
    private readonly Func<bool> _isDark;
    private readonly Dictionary<string, ExtensionPageViewModel> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NavigationBadge> _badges = new(StringComparer.Ordinal);
    private int _generation;

    /// <summary>Les vues de ces extensions.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="engine">Le moteur où tourne leur code.</param>
    /// <param name="isDark">Le thème est sombre.</param>
    public ExtensionPages(ExtensionLibrary library, ExtensionEngine engine, Func<bool> isDark)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(isDark);
        _library = library;
        _engine = engine;
        _isDark = isDark;
        engine.RefreshAsked += (_, e) =>
        {
            if (_pages.TryGetValue(e.Extension, out var page))
            {
                _ = page.RenderAsync();
            }
            _ = RefreshCountAsync(e.Extension);
        };
        engine.ViewAsked += (_, e) => ViewAsked?.Invoke(this, e);
    }

    /// <summary>Une extension demande d'ouvrir sa vue.</summary>
    public event EventHandler<ExtensionEventArgs>? ViewAsked;

    /// <summary>L'identifiant de l'entrée du menu d'une extension.</summary>
    /// <param name="id">L'extension.</param>
    public static string EntryId(string id) => $"extension:{id}";

    /// <summary>Les entrées du menu : une par extension active qui a une vue, après celles des modules, avec son icône et son compteur.</summary>
    public IReadOnlyList<NavigationItem> Entries() =>
        [
            .. Active()
                .Where(manifest => manifest.ViewTitle is not null)
                .Select((manifest, index) =>
                {
                    if (!_pages.TryGetValue(manifest.Id, out var page) || page.Title != manifest.ViewTitle)
                    {
                        page = new ExtensionPageViewModel(manifest.Id, manifest.ViewTitle!, _engine, _isDark);
                        page.Acted += (_, _) => _ = RefreshCountAsync(manifest.Id);
                        _pages[manifest.Id] = page;
                    }
                    var badge = manifest.HasCounter ? BadgeOf(manifest.Id) : null;
                    return new NavigationItem(EntryId(manifest.Id), manifest.ViewTitle!, Icons.Puzzle, FirstOrder + index, page, PictureOf(_library.Icon(manifest.Id)), badge);
                }),
        ];

    /// <summary>Les commandes des extensions actives, dans la recherche : « Quiz : Nouveau quiz ».</summary>
    public IReadOnlyList<PaletteEntry> Commands() =>
        [
            .. Active().SelectMany(manifest => manifest.Commands.Select(command =>
                new PaletteEntry($"{manifest.Name} : {command.Title}", null, Icons.Puzzle, () => _ = RunAsync(manifest.Id, command.Id, new Dictionary<string, string>())))),
        ];

    /// <summary>Les entrées que les extensions actives ajoutent à un clic droit, avec ce sur quoi il porte.</summary>
    /// <param name="zone">Le clic droit : une fiche ou un dossier.</param>
    /// <param name="context">Ce sur quoi il porte, transmis à la commande.</param>
    public IReadOnlyList<MenuContribution> MenuEntries(ExtensionMenuZone zone, IReadOnlyDictionary<string, string> context) =>
        [
            .. Active()
                .Where(manifest => zone != ExtensionMenuZone.Selection || _library.Granted(manifest.Id).Contains(ExtensionRight.Selection))
                .SelectMany(manifest => manifest.Menus.Where(menu => menu.Zone == zone).Select(menu =>
                new MenuContribution(menu.Title, () => _ = RunAsync(manifest.Id, menu.Command, context)))),
        ];

    /// <summary>Les encarts des extensions actives sur « Aujourd'hui ». Une extension qui ne répond pas n'en a pas.</summary>
    public async Task<IReadOnlyList<DashboardCard>> CardsAsync()
    {
        var generation = _generation;
        List<DashboardCard> cards = [];
        foreach (var manifest in Active().Where(manifest => manifest.CardTitle is not null))
        {
            if (await _engine.CardAsync(manifest.Id).ConfigureAwait(true) is { } card)
            {
                cards.Add(new DashboardCard(manifest.CardTitle!, card.Text, card.Detail));
            }
            if (generation != _generation)
            {
                return [];
            }
        }
        return cards;
    }

    /// <summary>Redemande leur compteur aux extensions qui en montrent un.</summary>
    public async Task RefreshCountsAsync()
    {
        foreach (var manifest in Active().Where(manifest => manifest.HasCounter))
        {
            await RefreshCountAsync(manifest.Id).ConfigureAwait(true);
        }
    }

    /// <summary>Une extension vient de changer : son code sera rechargé au prochain usage.</summary>
    /// <param name="id">L'extension.</param>
    public void Forget(string id) => _engine.Forget(id);

    /// <summary>Un coffre s'ouvre : les extensions oublient ce qu'elles ont lu du précédent.</summary>
    public void VaultChanged() => _engine.Restart();

    /// <summary>
    /// Oublie tout ce qui vient du coffre : chaque vue efface son rendu, chaque compteur se tait, et le moteur s'arrête avec ce
    /// que les extensions avaient lu. Les vues et les compteurs restent les mêmes objets, pour le menu ; une réponse partie avant
    /// n'y remet rien, et tout se redemande au prochain usage.
    /// </summary>
    public void Forget()
    {
        _generation++;
        foreach (var page in _pages.Values)
        {
            page.Forget();
        }
        foreach (var badge in _badges.Values)
        {
            badge.Text = null;
        }
        _engine.Restart();
    }

    /// <summary>L'icône d'une extension, en géométrie WPF ; rien si elle n'en a pas ou si aucun tracé ne se lit.</summary>
    internal static NavigationPicture? PictureOf(ExtensionIcon? icon)
    {
        if (icon is null)
        {
            return null;
        }
        var group = new GeometryGroup();
        foreach (var path in icon.Paths)
        {
            try
            {
                group.Children.Add(Geometry.Parse(path));
            }
            catch (Exception exception) when (exception is FormatException or InvalidOperationException)
            {
                continue;
            }
        }
        if (group.Children.Count == 0)
        {
            return null;
        }
        // La boîte de vue de l'icône devient le carré de 16 pixels du menu, centrée : ses marges restent les siennes.
        var scale = PictureSize / Math.Max(icon.Width, icon.Height);
        var placed = new TransformGroup();
        placed.Children.Add(new TranslateTransform(-icon.Left, -icon.Top));
        placed.Children.Add(new ScaleTransform(scale, scale));
        placed.Children.Add(new TranslateTransform((PictureSize - (icon.Width * scale)) / 2, (PictureSize - (icon.Height * scale)) / 2));
        group.Transform = placed;
        group.Freeze();
        return new NavigationPicture(group, icon.Stroked);
    }

    private IEnumerable<ExtensionManifest> Active() => _library.Installed().Where(extension => extension.IsActive).Select(extension => extension.Manifest);

    private NavigationBadge BadgeOf(string id)
    {
        if (!_badges.TryGetValue(id, out var badge))
        {
            badge = new NavigationBadge();
            _badges[id] = badge;
        }
        return badge;
    }

    private async Task RefreshCountAsync(string id)
    {
        if (_library.Active(id) is { HasCounter: true })
        {
            var generation = _generation;
            var count = await _engine.CountAsync(id).ConfigureAwait(true);
            if (generation == _generation)
            {
                BadgeOf(id).Text = count?.ToString(CultureInfo.CurrentCulture);
            }
        }
    }

    /// <summary>Lance une commande ; un refus se dit dans sa vue, qui se redessine, et son compteur suit.</summary>
    private async Task RunAsync(string id, string command, IReadOnlyDictionary<string, string> context)
    {
        var generation = _generation;
        var answer = await _engine.CommandAsync(id, command, context).ConfigureAwait(true);
        if (generation == _generation && _pages.TryGetValue(id, out var page))
        {
            if (answer.Succeeded)
            {
                await page.RenderAsync().ConfigureAwait(true);
            }
            else
            {
                page.Report(answer.Error);
            }
        }
        if (generation == _generation)
        {
            await RefreshCountAsync(id).ConfigureAwait(true);
        }
    }
}
