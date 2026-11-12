using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Reviews;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une confirmation demandée avant d'agir.</summary>
public enum ReviewConfirmation
{
    /// <summary>Aucune.</summary>
    None,

    /// <summary>Valider une révision avant son échéance.</summary>
    Early,

    /// <summary>Recommencer le cycle à J0.</summary>
    Restart,

    /// <summary>Archiver la fiche.</summary>
    Archive,

    /// <summary>Rappel : remettre toute la fiche en révision.</summary>
    ReviewEntirely,

    /// <summary>Rappel : aucun passage n'est marqué, enregistrer plutôt « Fiche connue ».</summary>
    NoPassageMarked,
}

/// <summary>Le jour que la barre demande de choisir.</summary>
public enum ReviewDayChoice
{
    /// <summary>Aucun.</summary>
    None,

    /// <summary>Le jour où la révision a été faite, pour l'antidater.</summary>
    Validate,

    /// <summary>Le jour où l'échéance est reportée.</summary>
    Postpone,
}

/// <summary>Ce que la dernière action de la barre a fait.</summary>
public enum ReviewAction
{
    /// <summary>Rien encore.</summary>
    None,

    /// <summary>Révision validée.</summary>
    Validated,

    /// <summary>Échéance reportée.</summary>
    Postponed,

    /// <summary>Cycle recommencé à J0 (ou élément archivé remis en révision).</summary>
    Restarted,

    /// <summary>Fiche archivée.</summary>
    Archived,

    /// <summary>Élément suspendu : il ne se présente plus à réviser.</summary>
    Suspended,

    /// <summary>Élément suspendu repris.</summary>
    Resumed,

    /// <summary>Fiche devenue une simple note, hors des révisions.</summary>
    MadeNote,

    /// <summary>Note mise en révision.</summary>
    PutInReview,

    /// <summary>Fiche exclue des rappels, ou réincluse.</summary>
    RemindersChanged,

    /// <summary>Réponse donnée à un rappel de consolidation.</summary>
    ReminderAnswered,
}

/// <summary>Un passage de la fiche, à marquer en répondant à un rappel.</summary>
/// <param name="Id">L'annotation.</param>
/// <param name="Label">« « Le passage » », ou le terme d'une définition.</param>
/// <param name="IsMarked">Son cycle a commencé aujourd'hui : il est à revoir.</param>
public sealed record PassageMark(string Id, string Label, bool IsMarked)
{
    /// <summary>Il reste à le marquer.</summary>
    public bool CanMark => !IsMarked;

    /// <summary>« Remettre à J0 : … » : le nom du bouton pour le lecteur d'écran.</summary>
    public string MarkName => $"{ReviewText.MarkPassage} : {Label}";
}

/// <summary>Une étape de la frise du cycle : J0 ✓ J1 ✓ J3 ● J7 J14 J30.</summary>
/// <param name="Label">« J3 ».</param>
/// <param name="IsDone">Validée.</param>
/// <param name="IsCurrent">L'étape attendue.</param>
/// <param name="WasLate">Validée en retard.</param>
/// <param name="Description">Ce que lit le lecteur d'écran (« J3 : validée le ven. 9 oct. (2 j de retard) »).</param>
public sealed record StepMark(string Label, bool IsDone, bool IsCurrent, bool WasLate, string Description)
{
    /// <summary>La description : c'est aussi le nom lu par le lecteur d'écran.</summary>
    public override string ToString() => Description;

    /// <summary>La frise du cycle de <paramref name="state"/>.</summary>
    internal static IReadOnlyList<StepMark> Of(ReviewState state)
    {
        var current = state.Steps.Select(step => step.DoneOn).ToList().IndexOf(null);
        return
        [
            .. state.Steps.Select((step, index) => step.DoneOn is { } done
                ? new StepMark(step.Label, true, false, step.DaysLate > 0, step.DaysLate switch
                {
                    > 0 => ReviewText.StepDoneLateOf(step.Label, Dates.Short(done), step.DaysLate),
                    < 0 => ReviewText.StepDoneEarlyOf(step.Label, Dates.Short(done), -step.DaysLate),
                    _ => ReviewText.StepDoneOf(step.Label, Dates.Short(done)),
                })
                : index == current
                    ? new StepMark(step.Label, false, true, false, state.Status == ReviewStatus.Active && state.DueDate is { } due ? ReviewText.StepExpectedOnOf(step.Label, Dates.Short(due)) : ReviewText.StepExpectedOf(step.Label))
                    : new StepMark(step.Label, false, false, false, ReviewText.StepUpcomingOf(step.Label))),
        ];
    }
}

/// <summary>
/// La barre de révision d'une fiche ou d'un passage, comme dans la 0.x (<c>BarreRevision.svelte</c>) :
/// l'étape attendue et son échéance, « Valider la révision » (Ctrl+Entrée), une confirmation avant
/// une révision en avance, le report (demain, dans 3 jours, à une date), la validation à une date passée (entre le
/// lendemain de la dernière révision et aujourd'hui), « Recommencer à J0… » et, pour une
/// fiche, « Archiver… » ; archivé, « Remettre en révision (J0) » et l'inclusion dans les rappels.
/// Valider est toujours une action explicite. Quand un rappel de consolidation attend sa réponse
/// (<c>BarreRappel.svelte</c>) : « Fiche connue », « Certains points sont à revoir » (les passages à
/// revoir se remettent à J0) ou « Fiche à revoir entièrement… ». Rien n'est enregistré tant que
/// l'utilisateur n'a pas choisi : quitter la fiche ne vaut pas réponse.
/// </summary>
public sealed partial class ReviewBarViewModel : ObservableObject
{
    private readonly ReviewUseCases _reviews;
    private readonly ReminderUseCases _reminders;
    private readonly SheetUseCases _sheets;
    private readonly TimeProvider _time;

    /// <summary>Le jour choisi d'une révision en avance, le temps de la confirmer ; rien : aujourd'hui.</summary>
    private DateOnly? _earlyDay;

    /// <summary>La barre d'un élément, dans l'état <paramref name="state"/>.</summary>
    public ReviewBarViewModel(ReviewUseCases reviews, ReminderUseCases reminders, SheetUseCases sheets, TimeProvider time, ItemKind kind, string id, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(state);
        _reviews = reviews;
        _reminders = reminders;
        _sheets = sheets;
        _time = time;
        Passages = [];
        Kind = kind;
        Id = id;
        State = state;
    }

    /// <summary>Une action a abouti : le nouvel état est dans <see cref="State"/>, son message dans <see cref="Message"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Fiche ou passage.</summary>
    public ItemKind Kind { get; }

    /// <summary>Identifiant de la fiche ou de l'annotation.</summary>
    public string Id { get; }

    /// <summary>Une fiche (sinon un passage).</summary>
    public bool IsSheet => Kind == ItemKind.Sheet;

    /// <summary>L'état de révision.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(IsArchived), nameof(IsEarly), nameof(StepLabel), nameof(DueText), nameof(IsLate), nameof(CanValidate),
        nameof(ValidateLabel), nameof(ArchivedStatus), nameof(Steps), nameof(ReminderToggleLabel), nameof(ShowsReviewedToday),
        nameof(IsReminder), nameof(ShowsArchivedActions), nameof(ShowsSteps), nameof(ShowsReminderChoices), nameof(ReminderIntro), nameof(CanAntedate), nameof(EarliestDay), nameof(LatestDay),
        nameof(CanForget), nameof(IsSuspended), nameof(ShowsActiveActions), nameof(SuspendedText), nameof(IsNote), nameof(ShowsResume), nameof(ShowsCycle))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand), nameof(ForgotCommand))]
    public partial ReviewState State { get; private set; }

    /// <summary>Un cycle est en cours.</summary>
    public bool IsActive => State.Status == ReviewStatus.Active;

    /// <summary>Archivé.</summary>
    public bool IsArchived => !IsActive;

    /// <summary>Suspendu : en révision, mais il ne se présente plus tant qu'il n'est pas repris.</summary>
    public bool IsSuspended => IsActive && State.SuspendedOn is not null;

    /// <summary>Une simple note : elle n'entre pas dans les révisions, et n'a que « Mettre en révision ».</summary>
    public bool IsNote => IsActive && State.IsNote;

    /// <summary>Suspendu et à reprendre : une note, elle, n'attend rien.</summary>
    public bool ShowsResume => IsSuspended && !IsNote;

    /// <summary>L'étape attendue et son échéance se montrent : un cycle est en cours, et ce n'est pas une note.</summary>
    public bool ShowsCycle => IsActive && !IsNote;

    /// <summary>Reporter, valider et les autres actions d'un élément en révision ; suspendu, il n'a que « Reprendre ».</summary>
    public bool ShowsActiveActions => IsActive && !IsSuspended;

    /// <summary>« Suspendue depuis le lundi 12 octobre : elle ne se présente plus à réviser. ».</summary>
    public string SuspendedText => State.SuspendedOn is { } since ? ReviewText.SuspendedSinceOf(Dates.Long(since)) : string.Empty;

    /// <summary>L'échéance n'est pas encore arrivée : valider demandera confirmation.</summary>
    public bool IsEarly => State.DueDate is { } due && due > Today;

    /// <summary>« Étape attendue » ou, en avance, « Prochaine étape ».</summary>
    public string StepLabel => IsEarly ? ReviewText.NextStep : ReviewText.ExpectedStep;

    /// <summary>« en retard de 2 j (prévue ven. 9 oct.) », « prévue aujourd'hui », « prévue lundi 12 octobre ».</summary>
    public string DueText => State.DueDate is not { } due ? string.Empty
        : IsSuspended ? SuspendedText
        : State.DaysLate > 0 ? ReviewText.DueLateOf(State.DaysLate, Dates.Short(due))
        : due == Today ? ReviewText.DueToday
        : ReviewText.DueOnOf(Dates.Long(due));

    /// <summary>L'échéance est dépassée.</summary>
    public bool IsLate => State.DaysLate > 0 && !IsSuspended;

    /// <summary>La révision du jour est faite : plus rien à valider aujourd'hui.</summary>
    public bool CanValidate => ShowsActiveActions && !State.ReviewedToday;

    /// <summary>
    /// « J'avais oublié » se dit d'une révision attendue : en avance, rien n'était encore à savoir, et
    /// l'étape n'a pas à reculer.
    /// </summary>
    public bool CanForget => CanValidate && !IsEarly;

    /// <summary>
    /// La révision peut être antidatée : valider se fait au plus tard aujourd'hui et après la dernière révision, une
    /// seule par jour. Il faut donc au moins un jour libre avant aujourd'hui.
    /// </summary>
    public bool CanAntedate => CanValidate && State.LastReview is { } last && last < Today.AddDays(-1);

    /// <summary>Quel jour la barre demande de choisir.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosingDay), nameof(DayChoiceLabel), nameof(DayChoiceConfirm), nameof(EarliestDay), nameof(LatestDay))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDayCommand), nameof(ValidateCommand), nameof(ForgotCommand))]
    public partial ReviewDayChoice DayChoice { get; private set; }

    /// <summary>Le champ de date est affiché.</summary>
    public bool IsChoosingDay => DayChoice != ReviewDayChoice.None;

    /// <summary>« Date de la révision » ou « Date du report ».</summary>
    public string DayChoiceLabel => DayChoice == ReviewDayChoice.Validate ? ReviewText.ValidationDay : ReviewText.PostponeDay;

    /// <summary>« Valider à cette date » ou « Reporter à cette date ».</summary>
    public string DayChoiceConfirm => DayChoice == ReviewDayChoice.Validate ? ReviewText.ValidateOnThatDay : ReviewText.PostponeOnThatDay;

    /// <summary>Le jour choisi.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDayCommand))]
    public partial DateOnly? ChosenDay { get; set; }

    /// <summary>Le premier jour permis : le lendemain de la dernière révision, ou demain pour un report.</summary>
    public DateOnly? EarliestDay => DayChoice switch
    {
        ReviewDayChoice.Validate => State.LastReview?.AddDays(1),
        ReviewDayChoice.Postpone => Today.AddDays(1),
        _ => null,
    };

    /// <summary>Le dernier jour permis : aujourd'hui pour une révision faite ; pas de limite pour un report.</summary>
    public DateOnly? LatestDay => DayChoice == ReviewDayChoice.Validate ? Today : null;

    /// <summary>« Révision du jour enregistrée » est affiché.</summary>
    public bool ShowsReviewedToday => ShowsActiveActions && State.ReviewedToday;

    /// <summary>« Valider la révision J3 ».</summary>
    public string ValidateLabel => ReviewText.ValidateOf(State.Step ?? string.Empty);

    /// <summary>« Fiche archivée le vendredi 9 octobre · 1 cycle(s) terminé(s) · rappel en attente ».</summary>
    public string ArchivedStatus
    {
        get
        {
            var status = IsSheet ? ReviewText.SheetArchived : ReviewText.PassageArchived;
            if (State.ArchivedOn is { } archived)
            {
                status += $" {ReviewText.ArchivedOnOf(Dates.Long(archived))}";
            }
            status += $" · {ReviewText.CyclesCompletedOf(State.CyclesCompleted)}";
            if (IsSheet)
            {
                var reminder = !State.IncludedInReminders ? ReviewText.ExcludedFromReminders
                    : State.ReminderPending ? ReviewText.ReminderPending
                    : State.EligibleOn is { } eligible ? ReviewText.EligibleOnOf(Dates.Long(eligible))
                    : null;
                if (reminder is not null)
                {
                    status += $" · {reminder}";
                }
            }
            return status;
        }
    }

    /// <summary>« Exclure des rappels » ou « Réinclure dans les rappels ».</summary>
    public string ReminderToggleLabel => State.IncludedInReminders ? ReviewText.ExcludeFromReminders : ReviewText.IncludeInReminders;

    /// <summary>Un rappel de consolidation attend sa réponse.</summary>
    public bool IsReminder => IsArchived && IsSheet && State.ReminderPending;

    /// <summary>Archivé sans rappel en attente : l'état des rappels et la remise en révision.</summary>
    public bool ShowsArchivedActions => IsArchived && !IsReminder;

    /// <summary>La frise montre où en est la révision ; un rappel porte sur toute la fiche.</summary>
    public bool ShowsSteps => !IsReminder && !IsNote;

    /// <summary>« Fiche archivée le …. Relisez-la, puis dites ce qu'il en reste. ».</summary>
    public string ReminderIntro => ReviewText.ReminderIntroOf(State.ArchivedOn is { } archived ? Dates.Long(archived) : string.Empty);

    /// <summary>« Certains points sont à revoir » : les passages à revoir se marquent.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsReminderChoices))]
    public partial bool IsMarking { get; private set; }

    /// <summary>Les trois réponses au rappel sont proposées.</summary>
    public bool ShowsReminderChoices => IsReminder && !IsMarking;

    /// <summary>Les passages de la fiche, pendant le marquage.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkedCount), nameof(MarkedLabel), nameof(HasNoPassage), nameof(HasPassages))]
    public partial IReadOnlyList<PassageMark> Passages { get; private set; }

    /// <summary>Passages marqués aujourd'hui.</summary>
    public int MarkedCount => Passages.Count(passage => passage.IsMarked);

    /// <summary>« 2 passages marqués ».</summary>
    public string MarkedLabel => Dates.Plural(MarkedCount, ReviewText.MarkedOneOf, ReviewText.MarkedManyOf);

    /// <summary>La fiche a des passages à marquer.</summary>
    public bool HasPassages => Passages.Count > 0;

    /// <summary>La fiche n'a aucun passage.</summary>
    public bool HasNoPassage => !HasPassages;

    /// <summary>La frise des étapes du cycle.</summary>
    public IReadOnlyList<StepMark> Steps => StepMark.Of(State);

    /// <summary>Ce que la dernière action réussie a fait.</summary>
    [ObservableProperty]
    public partial ReviewAction LastAction { get; private set; }

    /// <summary>Une action est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand), nameof(ConfirmDayCommand), nameof(ForgotCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Ce que la dernière action a fait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Un message est affiché.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Pourquoi la dernière action a été refusée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasError => Error is not null;

    /// <summary>La confirmation demandée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming), nameof(ConfirmTitle), nameof(ConfirmMessage), nameof(ConfirmLabel))]
    public partial ReviewConfirmation Confirmation { get; private set; }

    /// <summary>Une confirmation est affichée ; sa réponse arrive par <see cref="AnswerCommand"/>.</summary>
    public bool IsConfirming => Confirmation != ReviewConfirmation.None;

    /// <summary>Titre de la confirmation.</summary>
    public string ConfirmTitle => Confirmation switch
    {
        ReviewConfirmation.Early => ReviewText.EarlyTitle,
        ReviewConfirmation.Restart => ReviewText.RestartTitle,
        ReviewConfirmation.Archive => ReviewText.ArchiveTitle,
        ReviewConfirmation.ReviewEntirely => ReviewText.EntirelyTitle,
        ReviewConfirmation.NoPassageMarked => ReviewText.NoMarkTitle,
        _ => string.Empty,
    };

    /// <summary>Explication de la confirmation.</summary>
    public string ConfirmMessage => Confirmation switch
    {
        ReviewConfirmation.Early => EarlyMessage(),
        ReviewConfirmation.Restart => IsSheet ? ReviewText.RestartSheetMessage : ReviewText.RestartPassageMessage,
        ReviewConfirmation.Archive => ReviewText.ArchiveMessage,
        ReviewConfirmation.ReviewEntirely => ReviewText.EntirelyMessage,
        ReviewConfirmation.NoPassageMarked => ReviewText.NoMarkMessage,
        _ => string.Empty,
    };

    /// <summary>Libellé du bouton qui confirme.</summary>
    public string ConfirmLabel => Confirmation switch
    {
        ReviewConfirmation.Early => IsAntedating ? ReviewText.ValidateOnThatDay : ReviewText.EarlyConfirm,
        ReviewConfirmation.Restart => ReviewText.RestartConfirm,
        ReviewConfirmation.Archive => ReviewText.ArchiveConfirm,
        ReviewConfirmation.ReviewEntirely => ReviewText.EntirelyConfirm,
        ReviewConfirmation.NoPassageMarked => ReviewText.ReminderKnown,
        _ => string.Empty,
    };

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>Le jour se choisit : Ctrl+Entrée, tapé dans la date, ne valide pas aujourd'hui à sa place.</summary>
    private bool CanValidateNow => CanValidate && !IsBusy && !IsChoosingDay;

    private bool CanConfirmDay => IsChoosingDay && ChosenDay is not null && !IsBusy;

    /// <summary>La révision à confirmer est faite un autre jour qu'aujourd'hui.</summary>
    private bool IsAntedating => _earlyDay is { } chosen && chosen != Today;

    private bool CanForgetNow => CanForget && CanValidateNow;

    /// <summary>« J'avais oublié » : la révision est enregistrée comme oubliée, et l'étape recule d'un cran.</summary>
    [RelayCommand(CanExecute = nameof(CanForgetNow))]
    private Task ForgotAsync()
    {
        var step = State.Step ?? string.Empty;
        return ActAsync(
            ReviewAction.Validated,
            () => _reviews.Validate(Kind, Id, null, ReviewRecall.Forgotten),
            next => ReviewText.ForgotDoneOf(step, next.Step ?? string.Empty, next.DueDate is { } due ? Dates.Long(due) : string.Empty));
    }

    /// <summary>Valide la révision attendue ; en avance, demande d'abord confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanValidateNow))]
    private Task ValidateAsync()
    {
        _earlyDay = null;
        if (IsEarly)
        {
            Confirmation = ReviewConfirmation.Early;
            return Task.CompletedTask;
        }
        return ValidateNowAsync(null);
    }

    /// <summary>« Valider à une date passée… » : le champ de date s'ouvre sur hier.</summary>
    [RelayCommand]
    private void ChooseValidationDay()
    {
        DayChoice = ReviewDayChoice.Validate;
        ChosenDay = Today.AddDays(-1);
    }

    /// <summary>« Reporter à une date… » : le champ de date s'ouvre sur demain.</summary>
    [RelayCommand]
    private void ChoosePostponeDay()
    {
        DayChoice = ReviewDayChoice.Postpone;
        ChosenDay = Today.AddDays(1);
    }

    /// <summary>Referme le champ de date, sans rien faire.</summary>
    [RelayCommand]
    private void CancelDay() => CloseDayChoice();

    /// <summary>Valide ou reporte au jour choisi ; une révision en avance sur ce jour demande d'abord confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmDay))]
    private Task ConfirmDayAsync()
    {
        var day = ChosenDay!.Value;
        if (DayChoice == ReviewDayChoice.Postpone)
        {
            return PostponeAsync(day);
        }
        _earlyDay = day;
        if (State.DueDate is { } due && due > day)
        {
            Confirmation = ReviewConfirmation.Early;
            return Task.CompletedTask;
        }
        return ValidateNowAsync(day);
    }

    /// <summary>Réponse à la confirmation : l'action est faite si elle est confirmée, abandonnée sinon (Annuler, Échap).</summary>
    [RelayCommand]
    private Task AnswerAsync(bool confirmed)
    {
        var asked = Confirmation;
        Confirmation = ReviewConfirmation.None;
        if (!confirmed)
        {
            return Task.CompletedTask;
        }
        return asked switch
        {
            ReviewConfirmation.Early => ValidateNowAsync(_earlyDay),
            ReviewConfirmation.Restart => RestartAsync(),
            ReviewConfirmation.Archive => ActAsync(ReviewAction.Archived, () => _reviews.Archive(Id), _ => ReviewText.ArchivedMessage),
            ReviewConfirmation.ReviewEntirely => AnswerReminderAsync(ReminderOutcome.ReviewEntirely),
            ReviewConfirmation.NoPassageMarked => AnswerReminderAsync(ReminderOutcome.Known),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>Reporte l'échéance à demain.</summary>
    [RelayCommand]
    private Task PostponeTomorrowAsync() => PostponeAsync(Today.AddDays(1));

    /// <summary>Reporte l'échéance de 3 jours.</summary>
    [RelayCommand]
    private Task PostponeThreeDaysAsync() => PostponeAsync(Today.AddDays(3));

    /// <summary>Demande confirmation avant de recommencer le cycle à J0.</summary>
    [RelayCommand]
    private void RequestRestart() => Confirmation = ReviewConfirmation.Restart;

    /// <summary>Suspend l'élément : il garde son étape et son échéance, et attend d'être repris.</summary>
    [RelayCommand]
    private Task SuspendAsync() =>
        ActAsync(ReviewAction.Suspended, () => _reviews.Suspend(Kind, Id), _ => IsSheet ? ReviewText.SuspendedSheet : ReviewText.SuspendedPassage);

    /// <summary>Reprend l'élément suspendu, là où il en était.</summary>
    [RelayCommand]
    private Task ResumeAsync() =>
        ActAsync(ReviewAction.Resumed, () => _reviews.Resume(Kind, Id), next => ReviewText.ResumedOf(next.Step ?? string.Empty, DueText));

    /// <summary>Fait de la fiche une simple note : elle sort des révisions, avec son texte et son historique.</summary>
    [RelayCommand]
    private Task MakeNoteAsync() => ActAsync(ReviewAction.MadeNote, () => _reviews.MakeNote(Id), _ => ReviewText.MadeNote);

    /// <summary>Met la note en révision : à J0 si elle n'a jamais été révisée, sinon là où elle en était.</summary>
    [RelayCommand]
    private Task PutInReviewAsync() =>
        ActAsync(ReviewAction.PutInReview, () => _reviews.PutInReview(Id), next => ReviewText.PutInReviewOf(next.Step ?? string.Empty, DueText));

    /// <summary>Demande confirmation avant d'archiver la fiche.</summary>
    [RelayCommand]
    private void RequestArchive() => Confirmation = ReviewConfirmation.Archive;

    /// <summary>Remet en révision un élément archivé (nouveau cycle, J0 aujourd'hui).</summary>
    [RelayCommand]
    private Task RestartArchivedAsync() => RestartAsync();

    /// <summary>« Exclure des rappels » ou « Réinclure dans les rappels ».</summary>
    [RelayCommand]
    private async Task ToggleRemindersAsync()
    {
        var include = !State.IncludedInReminders;
        var state = State;
        await ActAsync(
            ReviewAction.RemindersChanged,
            () =>
            {
                var outcome = _reminders.Include(Id, include);
                return outcome.Succeeded
                    ? Outcome.Of(state with { IncludedInReminders = include, ReminderPending = include && state.ReminderPending })
                    : Outcome.Refused<ReviewState>(outcome.Error!);
            },
            _ => include ? ReviewText.Included : ReviewText.Excluded).ConfigureAwait(true);
    }

    /// <summary>Rappel : « Fiche connue ».</summary>
    [RelayCommand]
    private Task AnswerKnownAsync() => AnswerReminderAsync(ReminderOutcome.Known);

    /// <summary>Rappel : demande confirmation avant de remettre toute la fiche en révision.</summary>
    [RelayCommand]
    private void RequestReviewEntirely() => Confirmation = ReviewConfirmation.ReviewEntirely;

    /// <summary>Rappel : « Certains points sont à revoir » : montre les passages à marquer.</summary>
    [RelayCommand]
    private async Task StartMarkingAsync()
    {
        Error = null;
        await LoadPassagesAsync().ConfigureAwait(true);
        IsMarking = !HasError;
    }

    /// <summary>Relit les passages à marquer : un passage créé pendant un rappel y paraît, déjà à revoir.</summary>
    public async Task RefreshPassagesAsync()
    {
        if (IsMarking)
        {
            await LoadPassagesAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Revient aux trois réponses ; les passages déjà remis à J0 le restent.</summary>
    [RelayCommand]
    private void CancelMarking() => IsMarking = false;

    /// <summary>Remet un passage à J0 : il est à revoir dès demain.</summary>
    [RelayCommand]
    private async Task MarkPassageAsync(PassageMark passage)
    {
        ArgumentNullException.ThrowIfNull(passage);
        Error = null;
        var outcome = await Task.Run(() => _reviews.Restart(ItemKind.Passage, passage.Id)).ConfigureAwait(true);
        if (outcome.Succeeded)
        {
            Message = ReviewText.PassageMarked;
            await LoadPassagesAsync().ConfigureAwait(true);
        }
        else
        {
            Error = outcome.Error;
        }
    }

    /// <summary>Termine le marquage ; sans passage marqué, propose plutôt « Fiche connue ».</summary>
    [RelayCommand]
    private Task FinishMarkingAsync()
    {
        if (MarkedCount > 0)
        {
            return AnswerReminderAsync(ReminderOutcome.SomePointsToReview);
        }
        Confirmation = ReviewConfirmation.NoPassageMarked;
        return Task.CompletedTask;
    }

    private async Task LoadPassagesAsync()
    {
        var outcome = await Task.Run(() => _sheets.Open(Id)).ConfigureAwait(true);
        if (outcome.Succeeded)
        {
            Passages =
            [
                .. outcome.Value.Annotations.Where(annotation => !annotation.InTrash).Select(annotation => new PassageMark(
                    annotation.Id,
                    annotation.Definition?.Term ?? $"« {annotation.Anchor.Exact} »",
                    annotation.Review.StartedToday)),
            ];
        }
        else
        {
            Error = outcome.Error;
        }
    }

    private async Task AnswerReminderAsync(ReminderOutcome choice)
    {
        var marked = MarkedCount;
        await ActAsync(
            ReviewAction.ReminderAnswered,
            () =>
            {
                var done = _reminders.Handle(Id, choice);
                if (!done.Succeeded)
                {
                    return Outcome.Refused<ReviewState>(done.Error!);
                }
                var opened = _sheets.Open(Id);
                return opened.Succeeded ? Outcome.Of(opened.Value.Review) : Outcome.Refused<ReviewState>(opened.Error!);
            },
            next => ReminderMessage(choice, marked, next)).ConfigureAwait(true);
        IsMarking = IsMarking && HasError;
    }

    private static string ReminderMessage(ReminderOutcome choice, int marked, ReviewState next)
    {
        var eligible = next.EligibleOn is { } day ? $" {ReviewText.NextReminderOf(Dates.Long(day))}" : string.Empty;
        return choice switch
        {
            ReminderOutcome.Known => ReviewText.KnownDone + eligible,
            ReminderOutcome.SomePointsToReview => Dates.Plural(marked, ReviewText.SomeDoneOneOf, ReviewText.SomeDoneManyOf) + eligible,
            _ => ReviewText.EntirelyDoneOf(next.DueDate is { } due ? Dates.Long(due) : string.Empty),
        };
    }

    private string EarlyMessage()
    {
        var due = State.DueDate is { } date ? Dates.Long(date) : string.Empty;
        return IsAntedating ? ReviewText.EarlyOnDayMessageOf(due, Dates.Long(_earlyDay!.Value)) : ReviewText.EarlyMessageOf(due);
    }

    private void CloseDayChoice()
    {
        DayChoice = ReviewDayChoice.None;
        ChosenDay = null;
    }

    private Task ValidateNowAsync(DateOnly? day)
    {
        var step = State.Step ?? string.Empty;
        var antedated = day is { } chosen && chosen != Today;
        return ActAsync(
            ReviewAction.Validated,
            () => _reviews.Validate(Kind, Id, day),
            next => next.Status == ReviewStatus.Archived
                ? IsSheet ? ReviewText.ValidatedLastSheetOf(step) : ReviewText.ValidatedLastPassageOf(step)
                : antedated
                    ? ReviewText.ValidatedOnDayOf(step, Dates.Long(day!.Value), next.Step ?? string.Empty, next.DueDate is { } due ? Dates.Long(due) : string.Empty)
                    : ReviewText.ValidatedOf(step, next.Step ?? string.Empty, next.DueDate is { } dueDate ? Dates.Long(dueDate) : string.Empty));
    }

    private Task PostponeAsync(DateOnly day)
    {
        var step = State.Step ?? string.Empty;
        return ActAsync(ReviewAction.Postponed, () => _reviews.Postpone(Kind, Id, day), _ => ReviewText.PostponedOf(step, Dates.Long(day)));
    }

    private Task RestartAsync() =>
        ActAsync(ReviewAction.Restarted, () => _reviews.Restart(Kind, Id), next => ReviewText.RestartedOf(next.Step ?? string.Empty, next.DueDate is { } due ? Dates.Long(due) : string.Empty));

    private async Task ActAsync(ReviewAction kind, Func<Outcome<ReviewState>> action, Func<ReviewState, string> message)
    {
        IsBusy = true;
        Error = null;
        try
        {
            var outcome = await Task.Run(action).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                State = outcome.Value;
                LastAction = kind;
                Message = message(outcome.Value);
                CloseDayChoice();
                Changed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Error = outcome.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
