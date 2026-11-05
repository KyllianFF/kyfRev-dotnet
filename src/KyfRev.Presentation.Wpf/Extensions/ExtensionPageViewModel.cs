using CommunityToolkit.Mvvm.ComponentModel;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>La vue d'une extension : ce qu'elle affiche, redessiné après chaque action.</summary>
public sealed partial class ExtensionPageViewModel : ObservableObject, INavigationAware
{
    private readonly string _id;
    private readonly ExtensionEngine _engine;
    private readonly Func<bool> _isDark;

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

    /// <summary>Demande à l'extension ce qu'elle affiche maintenant.</summary>
    public async Task RenderAsync()
    {
        var answer = await _engine.RenderViewAsync(_id, _isDark()).ConfigureAwait(true);
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
        var answer = await _engine.ActAsync(_id, action.Name, action.Value, action.Data).ConfigureAwait(true);
        if (answer.Succeeded)
        {
            await RenderAsync().ConfigureAwait(true);
        }
        else
        {
            Error = answer.Error;
        }
        IsBusy = false;
    }
}

/// <summary>Les vues des extensions actives, et leurs entrées dans le menu.</summary>
public sealed class ExtensionPages
{
    private const int FirstOrder = 70;

    private readonly ExtensionLibrary _library;
    private readonly ExtensionEngine _engine;
    private readonly Func<bool> _isDark;
    private readonly Dictionary<string, ExtensionPageViewModel> _pages = new(StringComparer.Ordinal);

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
        };
    }

    /// <summary>Les entrées du menu : une par extension active qui a une vue, après celles des modules.</summary>
    public IReadOnlyList<NavigationItem> Entries() =>
        [
            .. _library.Installed()
                .Where(extension => extension.IsActive && extension.Manifest.ViewTitle is not null)
                .Select((extension, index) =>
                {
                    var manifest = extension.Manifest;
                    if (!_pages.TryGetValue(manifest.Id, out var page) || page.Title != manifest.ViewTitle)
                    {
                        page = new ExtensionPageViewModel(manifest.Id, manifest.ViewTitle!, _engine, _isDark);
                        _pages[manifest.Id] = page;
                    }
                    return new NavigationItem($"extension:{manifest.Id}", manifest.ViewTitle!, Icons.Puzzle, FirstOrder + index, page);
                }),
        ];

    /// <summary>Une extension vient de changer : son code sera rechargé au prochain usage.</summary>
    /// <param name="id">L'extension.</param>
    public void Forget(string id) => _engine.Forget(id);

    /// <summary>Un coffre s'ouvre : les extensions oublient ce qu'elles ont lu du précédent.</summary>
    public void VaultChanged() => _engine.Restart();
}
