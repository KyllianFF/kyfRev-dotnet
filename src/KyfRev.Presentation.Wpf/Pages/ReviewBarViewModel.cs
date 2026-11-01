using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

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
}

/// <summary>
/// La barre de révision d'une fiche ou d'un passage, comme dans la 0.x (<c>BarreRevision.svelte</c>) :
/// l'étape attendue et son échéance, « Valider la révision » (Ctrl+Entrée), une confirmation avant
/// une révision en avance, le report (demain, dans 3 jours), « Recommencer à J0… » et, pour une
/// fiche, « Archiver… » ; archivé, « Remettre en révision (J0) » et l'inclusion dans les rappels.
/// Valider est toujours une action explicite.
/// </summary>
public sealed partial class ReviewBarViewModel : ObservableObject
{
    private readonly ReviewUseCases _reviews;
    private readonly ReminderUseCases _reminders;
    private readonly TimeProvider _time;

    /// <summary>La barre d'un élément, dans l'état <paramref name="state"/>.</summary>
    public ReviewBarViewModel(ReviewUseCases reviews, ReminderUseCases reminders, TimeProvider time, ItemKind kind, string id, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(state);
        _reviews = reviews;
        _reminders = reminders;
        _time = time;
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
        nameof(ValidateLabel), nameof(ArchivedStatus), nameof(Steps), nameof(ReminderToggleLabel), nameof(ShowsReviewedToday))]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
    public partial ReviewState State { get; private set; }

    /// <summary>Un cycle est en cours.</summary>
    public bool IsActive => State.Status == ReviewStatus.Active;

    /// <summary>Archivé.</summary>
    public bool IsArchived => !IsActive;

    /// <summary>L'échéance n'est pas encore arrivée : valider demandera confirmation.</summary>
    public bool IsEarly => State.DueDate is { } due && due > Today;

    /// <summary>« Étape attendue » ou, en avance, « Prochaine étape ».</summary>
    public string StepLabel => IsEarly ? ReviewText.NextStep : ReviewText.ExpectedStep;

    /// <summary>« en retard de 2 j (prévue ven. 9 oct.) », « prévue aujourd'hui », « prévue lundi 12 octobre ».</summary>
    public string DueText => State.DueDate is not { } due ? string.Empty
        : State.DaysLate > 0 ? ReviewText.DueLateOf(State.DaysLate, Dates.Short(due))
        : due == Today ? ReviewText.DueToday
        : ReviewText.DueOnOf(Dates.Long(due));

    /// <summary>L'échéance est dépassée.</summary>
    public bool IsLate => State.DaysLate > 0;

    /// <summary>La révision du jour est faite : plus rien à valider aujourd'hui.</summary>
    public bool CanValidate => IsActive && !State.ReviewedToday;

    /// <summary>« Révision du jour enregistrée » est affiché.</summary>
    public bool ShowsReviewedToday => IsActive && State.ReviewedToday;

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

    /// <summary>La frise des étapes du cycle.</summary>
    public IReadOnlyList<StepMark> Steps
    {
        get
        {
            var current = State.Steps.Select(step => step.DoneOn).ToList().IndexOf(null);
            return
            [
                .. State.Steps.Select((step, index) => step.DoneOn is { } done
                    ? new StepMark(step.Label, true, false, step.DaysLate > 0, step.DaysLate switch
                    {
                        > 0 => ReviewText.StepDoneLateOf(step.Label, Dates.Short(done), step.DaysLate),
                        < 0 => ReviewText.StepDoneEarlyOf(step.Label, Dates.Short(done), -step.DaysLate),
                        _ => ReviewText.StepDoneOf(step.Label, Dates.Short(done)),
                    })
                    : index == current
                        ? new StepMark(step.Label, false, true, false, IsActive && State.DueDate is { } due ? ReviewText.StepExpectedOnOf(step.Label, Dates.Short(due)) : ReviewText.StepExpectedOf(step.Label))
                        : new StepMark(step.Label, false, false, false, ReviewText.StepUpcomingOf(step.Label))),
            ];
        }
    }

    /// <summary>Une action est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateCommand))]
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
        _ => string.Empty,
    };

    /// <summary>Explication de la confirmation.</summary>
    public string ConfirmMessage => Confirmation switch
    {
        ReviewConfirmation.Early => ReviewText.EarlyMessageOf(State.DueDate is { } due ? Dates.Long(due) : string.Empty),
        ReviewConfirmation.Restart => IsSheet ? ReviewText.RestartSheetMessage : ReviewText.RestartPassageMessage,
        ReviewConfirmation.Archive => ReviewText.ArchiveMessage,
        _ => string.Empty,
    };

    /// <summary>Libellé du bouton qui confirme.</summary>
    public string ConfirmLabel => Confirmation switch
    {
        ReviewConfirmation.Early => ReviewText.EarlyConfirm,
        ReviewConfirmation.Restart => ReviewText.RestartConfirm,
        ReviewConfirmation.Archive => ReviewText.ArchiveConfirm,
        _ => string.Empty,
    };

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private bool CanValidateNow => CanValidate && !IsBusy;

    /// <summary>Valide la révision attendue ; en avance, demande d'abord confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanValidateNow))]
    private Task ValidateAsync()
    {
        if (IsEarly)
        {
            Confirmation = ReviewConfirmation.Early;
            return Task.CompletedTask;
        }
        return ValidateNowAsync();
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
            ReviewConfirmation.Early => ValidateNowAsync(),
            ReviewConfirmation.Restart => RestartAsync(),
            ReviewConfirmation.Archive => ActAsync(() => _reviews.Archive(Id), _ => ReviewText.ArchivedMessage),
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
            () =>
            {
                var outcome = _reminders.Include(Id, include);
                return outcome.Succeeded
                    ? Outcome.Of(state with { IncludedInReminders = include, ReminderPending = include && state.ReminderPending })
                    : Outcome.Refused<ReviewState>(outcome.Error!);
            },
            _ => include ? ReviewText.Included : ReviewText.Excluded).ConfigureAwait(true);
    }

    private Task ValidateNowAsync()
    {
        var step = State.Step ?? string.Empty;
        return ActAsync(
            () => _reviews.Validate(Kind, Id),
            next => next.Status == ReviewStatus.Archived
                ? IsSheet ? ReviewText.ValidatedLastSheetOf(step) : ReviewText.ValidatedLastPassageOf(step)
                : ReviewText.ValidatedOf(step, next.Step ?? string.Empty, next.DueDate is { } due ? Dates.Long(due) : string.Empty));
    }

    private Task PostponeAsync(DateOnly day)
    {
        var step = State.Step ?? string.Empty;
        return ActAsync(() => _reviews.Postpone(Kind, Id, day), _ => ReviewText.PostponedOf(step, Dates.Long(day)));
    }

    private Task RestartAsync() =>
        ActAsync(() => _reviews.Restart(Kind, Id), next => ReviewText.RestartedOf(next.Step ?? string.Empty, next.DueDate is { } due ? Dates.Long(due) : string.Empty));

    private async Task ActAsync(Func<Outcome<ReviewState>> action, Func<ReviewState, string> message)
    {
        IsBusy = true;
        Error = null;
        try
        {
            var outcome = await Task.Run(action).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                State = outcome.Value;
                Message = message(outcome.Value);
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
