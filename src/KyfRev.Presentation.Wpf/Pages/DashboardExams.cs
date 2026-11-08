using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>À quel point un examen est proche : la couleur de son compte à rebours.</summary>
public enum ExamUrgency
{
    /// <summary>Plus d'une semaine.</summary>
    Normal,

    /// <summary>Dans la semaine.</summary>
    Soon,

    /// <summary>Passé.</summary>
    Past,
}

/// <summary>Une confirmation demandée avant de toucher à un examen.</summary>
public enum ExamConfirmation
{
    /// <summary>Aucune.</summary>
    None,

    /// <summary>Ramener les échéances avant l'examen.</summary>
    Tighten,

    /// <summary>Retirer l'examen.</summary>
    Withdraw,
}

/// <summary>Un examen à venir, avec son compte à rebours (<c>Examens.svelte</c>).</summary>
public sealed class ExamRow
{
    private const int SoonDays = 7;

    internal ExamRow(UpcomingExam exam)
    {
        Exam = exam;
        Countdown = exam.DaysLeft switch
        {
            0 => ExamText.Today,
            1 => ExamText.Tomorrow,
            > 1 => ExamText.DaysLeftOf(exam.DaysLeft),
            _ => Dates.Plural(-exam.DaysLeft, ExamText.PastOneOf, ExamText.PastManyOf),
        };
        Urgency = exam.DaysLeft switch
        {
            < 0 => ExamUrgency.Past,
            <= SoonDays => ExamUrgency.Soon,
            _ => ExamUrgency.Normal,
        };
        DayLabel = Dates.Short(exam.Day);
        var where = exam.Folder.Length == 0 ? ExamText.WholeVault : exam.Folder.Replace("/", " › ", StringComparison.Ordinal);
        var items = $"{where} · {Dates.Plural(exam.Items, ExamText.ItemsOneOf, ExamText.ItemsManyOf)}";
        if (exam.DaysLeft > 0)
        {
            Detail = ExamText.WithIncludingOf(items);
            AfterExam = Dates.Plural(exam.AfterExam, ExamText.AfterOneOf, ExamText.AfterManyOf);
        }
        else
        {
            Detail = items;
            AfterExam = string.Empty;
        }
    }

    /// <summary>L'examen.</summary>
    public UpcomingExam Exam { get; }

    /// <summary>Son nom.</summary>
    public string Name => Exam.Name;

    /// <summary>« J-12 », « demain », « aujourd'hui », « passé depuis 3 jours ».</summary>
    public string Countdown { get; }

    /// <summary>À quel point il est proche.</summary>
    public ExamUrgency Urgency { get; }

    /// <summary>« lun. 23 nov. ».</summary>
    public string DayLabel { get; }

    /// <summary>« Biologie › Cellule · 12 éléments actifs, dont ».</summary>
    public string Detail { get; }

    /// <summary>« 3 prévus après l'examen » ; vide une fois l'examen arrivé.</summary>
    public string AfterExam { get; }

    /// <summary>Des échéances tombent après la veille de l'examen : c'est à signaler.</summary>
    public bool HasAlert => Exam.DaysLeft > 0 && Exam.AfterExam > 0;

    /// <summary>« Resserrer… » est proposé : des échéances tombent après, et il reste au moins un jour avant la veille.</summary>
    public bool CanTighten => Exam.AfterExam > 0 && Exam.DaysLeft > 1;

    /// <summary>« Resserrer avant l'examen Bac blanc ».</summary>
    public string TightenName => ExamText.TightenNameOf(Name);

    /// <summary>« Autres actions de l'examen Bac blanc ».</summary>
    public string MoreName => ExamText.MoreActionsOf(Name);

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => ExamText.SpokenOf(Name, Countdown, DayLabel, $"{Detail}{AfterExam}");
}

/// <summary>
/// Les examens du tableau de bord (<c>Examens.svelte</c>, <c>DialogueExamen.svelte</c>) : en fixer un (un dossier n'en a
/// qu'un : le redéfinir le remplace), le modifier, le retirer, et ramener avant lui les échéances qui tombent après.
/// Retirer et resserrer demandent confirmation. Les calendriers ne changent que par « Resserrer ».
/// </summary>
public sealed partial class DashboardExams : ObservableObject
{
    private const int DefaultDaysAhead = 14;

    private readonly ExamUseCases _exams;
    private readonly FolderUseCases _folders;
    private readonly Func<Task> _refresh;

    /// <summary>Nombre d'oublis du coffre : une réponse partie avant le dernier n'y remet rien.</summary>
    private int _generation;

    /// <summary>Numéro de la dernière ouverture du formulaire : seule elle compte.</summary>
    private int _opening;

    private DateOnly _today;
    private ExamRow? _target;

    internal DashboardExams(ExamUseCases exams, FolderUseCases folders, Func<Task> refresh)
    {
        _exams = exams;
        _folders = folders;
        _refresh = refresh;
        EditorTitle = ExamText.EditorNew;
        ExamName = string.Empty;
        Folders = [];
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>Le formulaire est ouvert.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    /// <summary>« Date d'examen » ou « Modifier l'examen ».</summary>
    [ObservableProperty]
    public partial string EditorTitle { get; private set; }

    /// <summary>Le nom saisi.</summary>
    [ObservableProperty]
    public partial string ExamName { get; set; }

    /// <summary>« Tout le coffre », puis les dossiers.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Choice<string>> Folders { get; private set; }

    /// <summary>Le dossier choisi.</summary>
    [ObservableProperty]
    public partial Choice<string>? Folder { get; set; }

    /// <summary>Le jour choisi.</summary>
    [ObservableProperty]
    public partial DateOnly? Day { get; set; }

    /// <summary>Le premier jour permis : aujourd'hui, ou le jour d'un examen déjà passé qu'on modifie.</summary>
    [ObservableProperty]
    public partial DateOnly? Earliest { get; private set; }

    /// <summary>Pourquoi le formulaire est refusé ; rien sinon.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditorError))]
    public partial string? EditorError { get; private set; }

    /// <summary>Le formulaire a été refusé.</summary>
    public bool HasEditorError => EditorError is not null;

    /// <summary>La confirmation demandée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming), nameof(ConfirmTitle), nameof(ConfirmMessage), nameof(ConfirmLabel))]
    public partial ExamConfirmation Confirmation { get; private set; }

    /// <summary>Une confirmation est affichée.</summary>
    public bool IsConfirming => Confirmation != ExamConfirmation.None;

    /// <summary>Titre de la confirmation.</summary>
    public string ConfirmTitle => Confirmation switch
    {
        ExamConfirmation.Tighten => ExamText.TightenTitle,
        ExamConfirmation.Withdraw => ExamText.WithdrawTitle,
        _ => string.Empty,
    };

    /// <summary>Explication de la confirmation.</summary>
    public string ConfirmMessage => (Confirmation, _target) switch
    {
        (ExamConfirmation.Tighten, { } row) => ExamText.TightenMessageOf(Dates.Plural(row.Exam.AfterExam, ExamText.TightenFallsOneOf, ExamText.TightenFallsManyOf), row.Name, row.DayLabel),
        (ExamConfirmation.Withdraw, { } row) => ExamText.WithdrawMessageOf(row.Name),
        _ => string.Empty,
    };

    /// <summary>Libellé du bouton qui confirme.</summary>
    public string ConfirmLabel => Confirmation == ExamConfirmation.Tighten ? ExamText.TightenConfirm : ExamText.WithdrawConfirm;

    /// <summary>Ce que la dernière action a fait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; }

    /// <summary>Un message est affiché.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>Pourquoi la dernière action a été refusée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Une action est en cours.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Oublie ce qui vient du coffre : le formulaire, l'examen en attente de confirmation, les messages et les dossiers.</summary>
    public void Forget()
    {
        _generation++;
        _opening++;
        _target = null;
        IsEditing = false;
        ExamName = string.Empty;
        Folders = [];
        Folder = null;
        Day = null;
        Earliest = null;
        EditorError = null;
        Confirmation = ExamConfirmation.None;
        IsBusy = false;
        ClearFeedback();
    }

    /// <summary>Le jour du tableau de bord, lu de l'horloge : le formulaire en part.</summary>
    internal void SetToday(DateOnly today) => _today = today;

    /// <summary>La vue se rouvre : les messages de la fois d'avant s'en vont.</summary>
    internal void ClearFeedback()
    {
        Message = string.Empty;
        Error = string.Empty;
    }

    /// <summary>Demande confirmation avant de ramener les échéances avant l'examen.</summary>
    internal void RequestTighten(ExamRow row) => Ask(row, ExamConfirmation.Tighten);

    /// <summary>Demande confirmation avant de retirer l'examen.</summary>
    internal void RequestWithdraw(ExamRow row) => Ask(row, ExamConfirmation.Withdraw);

    /// <summary>Ouvre le formulaire, sur un nouvel examen.</summary>
    [RelayCommand]
    private Task NewAsync() => OpenEditorAsync(null);

    /// <summary>Ouvre le formulaire, sur un examen à modifier.</summary>
    [RelayCommand]
    private Task EditAsync(ExamRow row) => OpenEditorAsync(row);

    /// <summary>La réponse au formulaire : un refus le laisse ouvert, avec ce qui a été saisi.</summary>
    [RelayCommand]
    private async Task AnswerEditorAsync(bool confirmed)
    {
        IsEditing = false;
        if (!confirmed)
        {
            EditorError = null;
            return;
        }
        var name = ExamName.Trim();
        if (name.Length == 0)
        {
            Refuse(ExamText.NameRequired);
            return;
        }
        if (Day is not { } day)
        {
            Refuse(ExamText.DayRequired);
            return;
        }
        var folder = Folder?.Value ?? string.Empty;
        var generation = _generation;
        var outcome = await Task.Run(() => _exams.Define(folder, name, day)).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }
        if (!outcome.Succeeded)
        {
            Refuse(outcome.Error!);
            return;
        }
        EditorError = null;
        Message = string.Empty;
        Error = string.Empty;
        await _refresh().ConfigureAwait(true);
    }

    /// <summary>La réponse à la confirmation : l'action est faite si elle est confirmée, abandonnée sinon.</summary>
    [RelayCommand]
    private async Task AnswerAsync(bool confirmed)
    {
        var (asked, target) = (Confirmation, _target);
        Confirmation = ExamConfirmation.None;
        _target = null;
        if (!confirmed || target is null)
        {
            return;
        }
        var folder = target.Exam.Folder;
        var name = target.Name;
        if (asked == ExamConfirmation.Tighten)
        {
            await ActAsync(() => _exams.Tighten(folder), moved => moved == 0
                ? ExamText.NothingToTighten
                : Dates.Plural(moved, count => ExamText.TightenedOneOf(count, name), count => ExamText.TightenedManyOf(count, name))).ConfigureAwait(true);
        }
        else
        {
            await ActAsync(() => _exams.Withdraw(folder), _ => ExamText.WithdrawnOf(name)).ConfigureAwait(true);
        }
    }

    private void Ask(ExamRow row, ExamConfirmation confirmation)
    {
        _target = row;
        Confirmation = confirmation;
    }

    private void Refuse(string reason)
    {
        EditorError = reason;
        IsEditing = true;
    }

    private async Task ActAsync<T>(Func<Outcome<T>> action, Func<T, string> message)
    {
        var generation = _generation;
        IsBusy = true;
        Error = string.Empty;
        try
        {
            var outcome = await Task.Run(action).ConfigureAwait(true);
            if (generation != _generation)
            {
                return;
            }
            if (outcome.Succeeded)
            {
                Message = message(outcome.Value);
                await _refresh().ConfigureAwait(true);
            }
            else
            {
                Error = outcome.Error!;
            }
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
            }
        }
    }

    private async Task OpenEditorAsync(ExamRow? row)
    {
        var opening = ++_opening;
        var outcome = await Task.Run(_folders.Folders).ConfigureAwait(true);
        if (opening != _opening)
        {
            return;
        }
        if (!outcome.Succeeded)
        {
            Error = outcome.Error!;
            return;
        }
        var folder = row?.Exam.Folder ?? string.Empty;
        var known = outcome.Value.Contains(folder, StringComparer.Ordinal) || folder.Length == 0 ? outcome.Value : [.. outcome.Value, folder];
        Folders = [new(string.Empty, ExamText.WholeVault), .. known.Select(path => new Choice<string>(path, path.Replace("/", " › ", StringComparison.Ordinal)))];
        Folder = Folders.First(choice => choice.Value == folder);
        EditorTitle = row is null ? ExamText.EditorNew : ExamText.EditorEdit;
        ExamName = row?.Name ?? string.Empty;
        EditorError = null;
        // Un examen déjà passé garde sa date le temps qu'on la change.
        Earliest = row?.Exam.Day < _today ? row.Exam.Day : _today;
        Day = row?.Exam.Day ?? _today.AddDays(DefaultDaysAhead);
        IsEditing = true;
    }
}
