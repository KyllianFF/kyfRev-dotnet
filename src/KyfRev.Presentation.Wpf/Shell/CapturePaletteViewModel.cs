using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.DesignSystem.Controls;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ce que fait un choix de la palette de capture.</summary>
public enum CaptureChoiceKind
{
    /// <summary>Ajoute le texte à la « Boîte de réception ».</summary>
    Inbox,

    /// <summary>Crée une fiche de révision à ce titre, à la racine.</summary>
    NewSheet,

    /// <summary>Crée une note à ce titre, à la racine.</summary>
    NewNote,

    /// <summary>Cherche le texte dans le coffre, dans kyfRev.</summary>
    Search,

    /// <summary>Ouvre « Aujourd'hui », pour réviser.</summary>
    Revise,

    /// <summary>Ouvre kyfRev.</summary>
    Open,
}

/// <summary>Un choix de la palette : son groupe, ce qu'il dit, la touche ou le nombre à droite, son icône.</summary>
/// <param name="Kind">Ce qu'il fait.</param>
/// <param name="Group">« Noter » ou « Aller ».</param>
/// <param name="Title">Ce qu'il dit.</param>
/// <param name="Hint">À droite : sa touche, ou un nombre ; rien sinon.</param>
/// <param name="Glyph">Son icône.</param>
public sealed record CaptureChoice(CaptureChoiceKind Kind, string Group, string Title, string Hint, string Glyph)
{
    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Hint.Length > 0 ? $"{Title}, {Hint}" : Title;

    /// <summary>Il y a quelque chose à droite.</summary>
    public bool HasHint => Hint.Length > 0;
}

/// <summary>Les mots de la palette de capture.</summary>
public static class CapturePaletteText
{
    /// <summary>« Capture rapide ».</summary>
    public const string Title = "Capture rapide";

    /// <summary>Le texte gris du champ.</summary>
    public const string Placeholder = "Noter une idée, créer une fiche, chercher…";

    /// <summary>« Noter ».</summary>
    public const string Write = "Noter";

    /// <summary>« Aller ».</summary>
    public const string Go = "Aller";

    /// <summary>« Entrée ».</summary>
    public const string Enter = "Entrée";

    /// <summary>« Ctrl+Entrée ».</summary>
    public const string ControlEnter = "Ctrl+Entrée";

    /// <summary>« Réviser maintenant ».</summary>
    public const string Revise = "Réviser maintenant";

    /// <summary>« Ouvrir kyfRev ».</summary>
    public const string Open = "Ouvrir kyfRev";

    /// <summary>Le coffre chiffré attend son mot de passe.</summary>
    public const string Locked = "Le coffre est verrouillé. Ouvrez kyfRev pour le déverrouiller.";

    /// <summary>Aucun coffre n'est ouvert.</summary>
    public const string NoVault = "Aucun coffre n'est ouvert. Ouvrez kyfRev pour en choisir un.";

    /// <summary>« Noté dans la Boîte de réception ».</summary>
    public const string Noted = "Noté dans la Boîte de réception";

    /// <summary>« ↑ ↓ choisir ».</summary>
    public const string HintMove = "choisir";

    /// <summary>« Entrée faire ».</summary>
    public const string HintRun = "faire";

    /// <summary>« Échap fermer ».</summary>
    public const string HintClose = "fermer";

    /// <summary>« Ajouter « … » à la Boîte de réception ».</summary>
    public static string InboxOf(string text) => $"Ajouter « {text} » à la Boîte de réception";

    /// <summary>« Nouvelle fiche « … » ».</summary>
    public static string NewSheetOf(string text) => $"Nouvelle fiche « {text} »";

    /// <summary>« Nouvelle note « … » ».</summary>
    public static string NewNoteOf(string text) => $"Nouvelle note « {text} »";

    /// <summary>« Chercher « … » dans le coffre ».</summary>
    public static string SearchOf(string text) => $"Chercher « {text} » dans le coffre";

    /// <summary>« 9 à réviser ».</summary>
    public static string DueOf(int count) => count == 0 ? "rien à réviser" : $"{count} à réviser";

    /// <summary>« Fiche « … » créée, à la racine ».</summary>
    public static string CreatedOf(string text, bool note) => note ? $"Note « {text} » créée, à la racine" : $"Fiche « {text} » créée, à la racine";
}

/// <summary>
/// La palette de capture rapide (rangée 25B) : un champ, et dessous ce qu'on peut faire de ce qu'on tape. Elle s'ouvre par
/// Ctrl+Alt+N où que l'on soit, même kyfRev réduit dans la barre d'état, sans ouvrir sa fenêtre ; ou par Ctrl+Maj+N dans
/// kyfRev. Coffre verrouillé ou absent : elle le dit, et ne propose que d'ouvrir kyfRev.
/// </summary>
public sealed partial class CapturePaletteViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly Func<string, bool, Outcome<string>>? _create;
    private readonly Func<int?>? _due;

    /// <summary>Une palette sur la coquille, ce qui crée une fiche (titre, note) et ce qui compte les révisions du jour.</summary>
    public CapturePaletteViewModel(ShellViewModel shell, Func<string, bool, Outcome<string>>? create = null, Func<int?>? due = null)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
        _create = create;
        _due = due;
    }

    /// <summary>Montre la fenêtre de kyfRev, au premier plan.</summary>
    public Action? ShowApplication { get; set; }

    /// <summary>La palette a fini : elle se ferme aussitôt (faux), ou après avoir dit ce qui est fait (vrai).</summary>
    public Action<bool>? Finished { get; set; }

    /// <summary>Ce qu'on tape.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    partial void OnTextChanged(string value)
    {
        Error = null;
        Rebuild();
    }

    /// <summary>Les choix, dans l'ordre.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CaptureChoice> Choices { get; private set; } = [];

    /// <summary>Le choix qu'Entrée fera.</summary>
    [ObservableProperty]
    public partial CaptureChoice? Selected { get; set; }

    /// <summary>Ce qui vient d'être fait ; la palette le dit avant de se fermer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDone))]
    public partial string? Done { get; private set; }

    /// <summary>Elle dit ce qui vient d'être fait.</summary>
    public bool HasDone => Done is not null;

    /// <summary>Le refus du coffre ; le texte est gardé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Il y a un refus à dire.</summary>
    public bool HasError => Error is not null;

    /// <summary>Un choix est en train de se faire.</summary>
    [ObservableProperty]
    public partial bool IsWorking { get; private set; }

    /// <summary>Pas de coffre où écrire : verrouillé, ou aucun d'ouvert.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockedMessage))]
    public partial bool IsLocked { get; private set; }

    /// <summary>Ce que la palette dit quand elle ne peut rien noter.</summary>
    public string? LockedMessage => IsLocked ? _shell.IsLockedOut ? CapturePaletteText.Locked : CapturePaletteText.NoVault : null;

    /// <summary>Le nombre de révisions du jour, lu à l'ouverture ; rien s'il n'a pas pu l'être.</summary>
    private int? _dueCount;

    /// <summary>La palette s'ouvre : vide, sur l'état du coffre de maintenant.</summary>
    public void Open()
    {
        IsLocked = !_shell.CanCapture;
        Done = null;
        Error = null;
        IsWorking = false;
        _dueCount = null;
        Text = string.Empty;
        Rebuild();
        if (!IsLocked && _due is { } due)
        {
            _ = CountAsync(due);
        }
    }

    /// <summary>Le nombre de révisions du jour, lu à côté : la palette paraît sans l'attendre.</summary>
    private async Task CountAsync(Func<int?> due)
    {
        var count = await Task.Run(due).ConfigureAwait(true);
        _dueCount = count;
        var kept = Selected?.Kind;
        Rebuild();
        Selected = Choices.FirstOrDefault(choice => choice.Kind == kept) ?? Selected;
    }

    /// <summary>Monte ou descend d'un choix ; au bout, repart de l'autre.</summary>
    public void Move(int step)
    {
        if (Choices.Count == 0)
        {
            return;
        }
        var index = Selected is null ? -1 : Choices.ToList().IndexOf(Selected);
        Selected = Choices[(((index + step) % Choices.Count) + Choices.Count) % Choices.Count];
    }

    /// <summary>Entrée : le choix en cours.</summary>
    [RelayCommand]
    private Task RunSelectedAsync() => Selected is { } choice ? RunAsync(choice) : Task.CompletedTask;

    /// <summary>Ctrl+Entrée : une nouvelle fiche à ce titre, s'il y a un titre.</summary>
    [RelayCommand]
    private Task RunNewSheetAsync() => Choices.FirstOrDefault(choice => choice.Kind == CaptureChoiceKind.NewSheet) is { } choice ? RunAsync(choice) : Task.CompletedTask;

    /// <summary>Fait le choix donné.</summary>
    [RelayCommand]
    private async Task RunAsync(CaptureChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (IsWorking)
        {
            return;
        }
        var text = Text.Trim();
        switch (choice.Kind)
        {
            case CaptureChoiceKind.Inbox when _shell.Capture is { } capture:
                await WriteAsync(() => capture(text), CapturePaletteText.Noted).ConfigureAwait(true);
                break;
            case CaptureChoiceKind.NewSheet or CaptureChoiceKind.NewNote when _create is { } create:
                var note = choice.Kind == CaptureChoiceKind.NewNote;
                await WriteAsync(() => create(text, note), CapturePaletteText.CreatedOf(text, note)).ConfigureAwait(true);
                break;
            case CaptureChoiceKind.Search:
                ShowApplication?.Invoke();
                _shell.OpenPalette();
                _shell.PaletteQuery = text;
                Finished?.Invoke(false);
                break;
            case CaptureChoiceKind.Revise:
                ShowApplication?.Invoke();
                if (_shell.MainNavigation.FirstOrDefault(item => item.Id == CorePages.Today) is { } today)
                {
                    _shell.Navigate(today);
                }
                Finished?.Invoke(false);
                break;
            default:
                ShowApplication?.Invoke();
                Finished?.Invoke(false);
                break;
        }
    }

    /// <summary>Écrit dans le coffre ; dit ce qui est fait, ou garde le texte avec le refus.</summary>
    private async Task WriteAsync(Func<Outcome<string>> write, string done)
    {
        IsWorking = true;
        var outcome = await Task.Run(write).ConfigureAwait(true);
        IsWorking = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Done = done;
        _shell.ShowWritten();
        Finished?.Invoke(true);
    }

    /// <summary>Les choix selon ce qui est tapé, et selon le coffre.</summary>
    private void Rebuild()
    {
        var text = Text.Trim();
        var open = new CaptureChoice(CaptureChoiceKind.Open, CapturePaletteText.Go, CapturePaletteText.Open, string.Empty, Icons.Read);
        if (IsLocked)
        {
            Choices = [open with { Hint = CapturePaletteText.Enter }];
        }
        else
        {
            var revise = new CaptureChoice(CaptureChoiceKind.Revise, CapturePaletteText.Go, CapturePaletteText.Revise, _dueCount is { } due ? CapturePaletteText.DueOf(due) : string.Empty, Icons.Review);
            Choices = text.Length == 0
                ? [revise, open]
                :
                [
                    new(CaptureChoiceKind.Inbox, CapturePaletteText.Write, CapturePaletteText.InboxOf(text), CapturePaletteText.Enter, Icons.Add),
                    .. _create is null ? Array.Empty<CaptureChoice>() :
                    [
                        new(CaptureChoiceKind.NewSheet, CapturePaletteText.Write, CapturePaletteText.NewSheetOf(text), CapturePaletteText.ControlEnter, Icons.Edit),
                        new(CaptureChoiceKind.NewNote, CapturePaletteText.Write, CapturePaletteText.NewNoteOf(text), string.Empty, Icons.Comment),
                    ],
                    new(CaptureChoiceKind.Search, CapturePaletteText.Go, CapturePaletteText.SearchOf(text), string.Empty, Icons.Search),
                    revise,
                    open,
                ];
        }
        Selected = Choices.Count > 0 ? Choices[0] : null;
    }
}
