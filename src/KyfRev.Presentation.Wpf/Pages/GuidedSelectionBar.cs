using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Presentation.Wpf.Extensions;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La sélection guidée qu'une extension demande dans une fiche ouverte : sa consigne au-dessus du texte, puis ce que l'utilisateur
/// prend dans la fiche, écrit à la main, passe, ou termine. Une seule demande à la fois : une nouvelle termine la précédente.
/// </summary>
public sealed partial class GuidedSelectionBar : ObservableObject
{
    private TaskCompletionSource<GuidedSelection>? _answer;

    /// <summary>La demande en cours ; rien s'il n'y en a pas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(Prompt), nameof(AllowsTyping), nameof(Extension))]
    public partial SelectionAsk? Ask { get; private set; }

    /// <summary>Une extension attend un texte.</summary>
    public bool IsActive => Ask is not null;

    /// <summary>L'extension qui le demande.</summary>
    public string Extension => Ask?.Extension ?? string.Empty;

    /// <summary>Sa consigne, avec son nom.</summary>
    public string Prompt => Ask is { } ask ? ReadingText.GuidedPromptOf(ask.Extension, ask.Prompt) : string.Empty;

    /// <summary>Ce qui manque dans la fiche peut s'écrire à la main.</summary>
    public bool AllowsTyping => Ask?.AllowsTyping == true;

    /// <summary>Le texte écrit à la main.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TakeTypedCommand))]
    public partial string Typed { get; set; } = string.Empty;

    /// <summary>Ce qu'il faut faire d'abord : sélectionner un texte.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string? Hint { get; private set; }

    /// <summary>Il y a quelque chose à dire.</summary>
    public bool HasHint => Hint is not null;

    /// <summary>Montre la consigne, et rend ce que l'utilisateur choisit.</summary>
    /// <param name="ask">La demande.</param>
    public Task<GuidedSelection> StartAsync(SelectionAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        Finish(new GuidedSelection(GuidedSelection.Stopped));
        _answer = new TaskCompletionSource<GuidedSelection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = _answer.Task;
        Ask = ask;
        return waiting;
    }

    /// <summary>Prend le texte sélectionné dans la fiche.</summary>
    internal void Take(string sheetId, string title, int start, int end, string text) =>
        Finish(new GuidedSelection(GuidedSelection.Taken, text, sheetId, title, start, end));

    /// <summary>« Prendre la sélection » sans rien de sélectionné.</summary>
    internal void NeedsSelection() => Hint = ReadingText.GuidedNeedsSelection;

    /// <summary>La demande s'arrête là : la fiche n'est plus ouverte.</summary>
    internal void Abandon() => Finish(new GuidedSelection(GuidedSelection.Stopped));

    private bool CanTakeTyped() => Typed.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanTakeTyped))]
    private void TakeTyped() => Finish(new GuidedSelection(GuidedSelection.Typed, Typed.Trim()));

    [RelayCommand]
    private void Skip() => Finish(new GuidedSelection(GuidedSelection.Skipped));

    [RelayCommand]
    private void StopSeries() => Finish(new GuidedSelection(GuidedSelection.Stopped));

    private void Finish(GuidedSelection answer)
    {
        var pending = _answer;
        _answer = null;
        Ask = null;
        Typed = string.Empty;
        Hint = null;
        pending?.TrySetResult(answer);
    }
}
