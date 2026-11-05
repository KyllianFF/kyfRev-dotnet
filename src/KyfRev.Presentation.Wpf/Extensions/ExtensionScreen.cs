using System.ComponentModel;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>
/// Ce que l'application montre pour les extensions : la sélection guidée, dans la fiche ouverte ou dans celle qu'elle désigne, et une
/// fiche ouverte à un passage. Quitter la fiche termine la sélection guidée : l'extension n'attend pas indéfiniment.
/// </summary>
public sealed class ExtensionScreen : IExtensionScreen
{
    private readonly ShellViewModel _shell;
    private readonly ReviewNavigation _navigation;
    private SheetPageViewModel? _guided;

    /// <summary>L'écran de cette coquille.</summary>
    /// <param name="shell">La coquille : la vue ouverte.</param>
    /// <param name="navigation">L'ouverture des fiches.</param>
    public ExtensionScreen(ShellViewModel shell, ReviewNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(navigation);
        _shell = shell;
        _navigation = navigation;
        shell.PropertyChanged += OnShellChanged;
    }

    /// <inheritdoc/>
    public async Task<(GuidedSelection? Answer, string? Refusal)> AskSelectionAsync(SelectionAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var page = OpenedSheet(ask.SheetId);
        if (page is null && ask.SheetId is { Length: > 0 } sheetId)
        {
            _navigation.OpenSheet(sheetId);
            page = OpenedSheet(sheetId);
        }
        if (page is null)
        {
            return (null, ReadingText.GuidedNoSheet);
        }
        _guided = page;
        var answer = await page.Guided.StartAsync(ask).ConfigureAwait(true);
        if (ReferenceEquals(_guided, page) && !page.Guided.IsActive)
        {
            _guided = null;
        }
        return (answer, null);
    }

    /// <inheritdoc/>
    public string? OpenSheet(string sheetId, int? passageStart, int? passageEnd)
    {
        if (string.IsNullOrWhiteSpace(sheetId))
        {
            return ReadingText.GuidedNoSheet;
        }
        _navigation.OpenSheetAt(sheetId, passageStart, passageEnd);
        return null;
    }

    private SheetPageViewModel? OpenedSheet(string? sheetId) =>
        _shell.Current.Page is SheetPageViewModel page && (sheetId is null || page.SheetId == sheetId) ? page : null;

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Current) && _guided is { } page && !ReferenceEquals(_shell.Current.Page, page))
        {
            _guided = null;
            page.Guided.Abandon();
        }
    }
}
