using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une page qui porte la barre de révision d'un élément, une fois l'élément lu.</summary>
public interface IReviewPage : INotifyPropertyChanged
{
    /// <summary>La barre de révision ; rien tant que l'élément n'est pas lu.</summary>
    ReviewBarViewModel? Review { get; }
}

/// <summary>
/// La séance d'étude, repensée comme le demande le plan (J6) : les éléments du jour se suivent,
/// la progression est visible, « Suivant » (ou « Passer » tant que rien n'a été fait) mène au
/// suivant (Ctrl+→), le mode plein écran est à un clic (F11). La séance est gardée à chaque pas :
/// interrompue (vue quittée, application fermée), elle se reprend le même jour. Le bilan de fin
/// donne le temps passé, les éléments faits, reportés et restants.
/// </summary>
public sealed partial class StudySessionViewModel : ObservableObject, ILeaving
{
    private readonly IStudySessionStore _store;
    private readonly TimeProvider _time;
    private readonly Func<SessionItem, bool, object> _page;
    private readonly Action _leave;
    private readonly Action _toggleFullScreen;
    private StudySession _session;
    private DateTimeOffset _since;
    private ReviewBarViewModel? _bar;

    /// <summary>
    /// La séance <paramref name="session"/> ; <paramref name="page"/> ouvre la page d'un élément,
    /// <paramref name="leave"/> ramène à la vue d'où la séance a été lancée.
    /// </summary>
    public StudySessionViewModel(StudySession session, IStudySessionStore store, TimeProvider time, Func<SessionItem, bool, object> page, Action leave, Action toggleFullScreen)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(leave);
        ArgumentNullException.ThrowIfNull(toggleFullScreen);
        _session = session;
        _store = store;
        _time = time;
        _page = page;
        _leave = leave;
        _toggleFullScreen = toggleFullScreen;
        _since = time.GetUtcNow();
        Summary = string.Empty;
        Duration = string.Empty;
        ShowCurrent();
    }

    /// <summary>La séance telle qu'elle est gardée.</summary>
    public StudySession Session => _session;

    /// <summary>La page de l'élément en cours ; rien une fois la séance terminée.</summary>
    [ObservableProperty]
    public partial object? Current { get; private set; }

    /// <summary>Quelque chose a été fait sur l'élément en cours (validé, reporté…).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextLabel), nameof(IsWaiting))]
    public partial bool HasActed { get; private set; }

    /// <summary>Rien n'a encore été fait sur l'élément en cours : il peut être passé.</summary>
    public bool IsWaiting => !HasActed;

    /// <summary>La séance est terminée : le bilan est affiché.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    public partial bool IsFinished { get; private set; }

    /// <summary>La séance est en cours.</summary>
    public bool IsRunning => !IsFinished;

    /// <summary>« Séance : 3 / 12 ».</summary>
    public string ProgressLabel => SessionText.ProgressOf(Math.Min(_session.Position + 1, Total), Total);

    /// <summary>« Progression de la séance : élément 3 sur 12 ».</summary>
    public string ProgressName => SessionText.ProgressNameOf(Math.Min(_session.Position + 1, Total), Total);

    /// <summary>Part de la séance déjà parcourue (de 0 à 1).</summary>
    public double Progress => Total == 0 ? 1 : (double)Math.Min(_session.Position, Total) / Total;

    /// <summary>« Suivant » une fois l'élément traité, « Passer » sinon.</summary>
    public string NextLabel => HasActed ? SessionText.Next : SessionText.Skip;

    /// <summary>« 12 éléments passés en revue. ».</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; }

    /// <summary>La durée de la séance, « 12 min 30 s ».</summary>
    [ObservableProperty]
    public partial string Duration { get; private set; }

    /// <summary>Les éléments faits.</summary>
    public int DoneCount => _session.Done;

    /// <summary>Les éléments reportés.</summary>
    public int PostponedCount => _session.Postponed;

    /// <summary>Les éléments restants : ceux qui ont été passés et ceux que la séance n'a pas atteints.</summary>
    public int RemainingCount => _session.Skipped + _session.Remaining;

    private int Total => _session.Items.Length;

    /// <summary>
    /// La séance est quittée sans être terminée (Retour, autre vue) : elle est gardée avec le
    /// temps passé, pour être reprise.
    /// </summary>
    public void Interrupt()
    {
        if (!IsFinished)
        {
            Keep();
        }
    }

    /// <summary>
    /// Avant que le coffre se verrouille : ce qui s'écrit sur l'élément en cours s'enregistre, puis la séance est gardée
    /// pour être reprise. Faux si ce qui s'écrit n'a pas pu être enregistré.
    /// </summary>
    public async Task<bool> LeaveAsync()
    {
        if (Current is ILeaving item && !await item.LeaveAsync().ConfigureAwait(true))
        {
            return false;
        }
        Interrupt();
        return true;
    }

    /// <summary>Élément suivant (ou passer celui-ci) ; après le dernier, le bilan.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Next()
    {
        Record();
        _session = _session with { Position = _session.Position + 1 };
        if (_session.Position >= Total)
        {
            Finish();
        }
        else
        {
            ShowCurrent();
        }
    }

    /// <summary>Termine la séance tout de suite ; ce qui n'a pas été vu reste à faire.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void End()
    {
        if (HasActed)
        {
            Record();
            _session = _session with { Position = _session.Position + 1 };
        }
        Finish();
    }

    /// <summary>Revient à la vue d'où la séance a été lancée.</summary>
    [RelayCommand]
    private void Leave()
    {
        Interrupt();
        _leave();
    }

    /// <summary>Mode révision plein écran, ou retour à la fenêtre.</summary>
    [RelayCommand]
    private void ToggleFullScreen() => _toggleFullScreen();

    /// <summary>Ce qu'il est advenu de l'élément en cours.</summary>
    private void Record()
    {
        _session = (_bar?.LastAction ?? ReviewAction.None) switch
        {
            ReviewAction.Validated or ReviewAction.Restarted or ReviewAction.Archived => _session with { Done = _session.Done + 1 },
            ReviewAction.Postponed or ReviewAction.Suspended => _session with { Postponed = _session.Postponed + 1 },
            _ => _session with { Skipped = _session.Skipped + 1 },
        };
    }

    private void ShowCurrent()
    {
        Watch(null);
        HasActed = false;
        var page = _page(_session.Items[_session.Position], _session.ActiveRecall);
        if (page is IReviewPage reviewPage)
        {
            reviewPage.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IReviewPage.Review) && ReferenceEquals(Current, reviewPage))
                {
                    Watch(reviewPage.Review);
                }
            };
            Watch(reviewPage.Review);
        }
        Current = page;
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(ProgressName));
        OnPropertyChanged(nameof(Progress));
        Keep();
    }

    /// <summary>Suit la barre de révision de l'élément en cours.</summary>
    private void Watch(ReviewBarViewModel? bar)
    {
        _bar?.PropertyChanged -= OnBarChanged;
        _bar = bar;
        _bar?.PropertyChanged += OnBarChanged;
    }

    private void OnBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewBarViewModel.LastAction))
        {
            HasActed = _bar is { LastAction: not ReviewAction.None };
        }
    }

    /// <summary>Garde la séance, avec le temps passé jusqu'ici.</summary>
    private void Keep()
    {
        var now = _time.GetUtcNow();
        _session = _session with { Elapsed = _session.Elapsed + (now - _since) };
        _since = now;
        _store.Save(_session);
    }

    private void Finish()
    {
        Keep();
        Watch(null);
        _store.Forget();
        var reviewed = _session.Done + _session.Postponed + _session.Skipped;
        Summary = Dates.Plural(reviewed, SessionText.ReviewedOneOf, SessionText.ReviewedManyOf);
        var elapsed = _session.Elapsed;
        Duration = elapsed.TotalMinutes >= 1
            ? SessionText.MinutesSecondsOf((int)elapsed.TotalMinutes, elapsed.Seconds)
            : SessionText.SecondsOf(elapsed.Seconds);
        Current = null;
        IsFinished = true;
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(PostponedCount));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(Progress));
        NextCommand.NotifyCanExecuteChanged();
        EndCommand.NotifyCanExecuteChanged();
    }
}
