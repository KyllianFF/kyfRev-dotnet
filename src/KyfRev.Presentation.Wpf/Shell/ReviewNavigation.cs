using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Ouvre un élément du tableau de bord pour le réviser.</summary>
public interface IReviewOpener
{
    /// <summary>Ouvre une fiche ou un passage à réviser.</summary>
    void Open(DueItem item, bool activeRecall);

    /// <summary>Ouvre la fiche d'un rappel de consolidation.</summary>
    void OpenReminder(PendingReminder reminder);

    /// <summary>Ouvre une fiche, pour la relire.</summary>
    void OpenSheet(string sheetId);

    /// <summary>Ouvre une fiche, sur un de ses passages.</summary>
    void OpenPassageInSheet(string sheetId, string passageId);

    /// <summary>Ouvre une fiche, pour la réviser.</summary>
    void ReviseSheet(string sheetId);

    /// <summary>Ouvre une fiche, pour l'écrire.</summary>
    void WriteSheet(string sheetId);

    /// <summary>Lance une séance d'étude sur ces éléments, dans cet ordre.</summary>
    void StartSession(IReadOnlyList<DueItem> items, bool activeRecall);

    /// <summary>Éléments restants de la séance interrompue aujourd'hui dans ce coffre ; rien s'il n'y en a pas.</summary>
    int? ResumableItems();

    /// <summary>Reprend la séance interrompue, là où elle s'était arrêtée.</summary>
    void ResumeSession();
}

/// <summary>
/// Ouvre les fiches et les passages hors du menu, comme dans la 0.x : une fiche, un passage, la
/// fiche d'un passage (« Voir dans la fiche »), et la séance d'étude. « Retour » ramène à la vue
/// d'où la page a été ouverte, qui est alors relue ; une séance quittée ainsi est gardée pour être
/// reprise le même jour.
/// </summary>
public sealed class ReviewNavigation : IReviewOpener
{
    private readonly SheetUseCases _sheets;
    private readonly ReviewUseCases _reviews;
    private readonly ReminderUseCases _reminders;
    private readonly TimeProvider _time;
    private readonly VaultUseCases _vaults;
    private readonly IStudySessionStore _sessions;
    private readonly IExternalLinks _links;
    private readonly AnnotationUseCases _annotations;
    private readonly EditingUseCases _editing;
    private readonly EditorEnvironment _environment;
    private readonly SettingsUseCases? _settings;
    private readonly ISpeech? _speech;
    private readonly MachineSettings? _machine;
    private ShellViewModel? _shell;

    /// <summary>La navigation, sur les cas d'usage des fiches, des révisions et des rappels, et la séance gardée sur ce poste.</summary>
    public ReviewNavigation(SheetUseCases sheets, AnnotationUseCases annotations, ReviewUseCases reviews, ReminderUseCases reminders, VaultUseCases vaults, IStudySessionStore sessions, TimeProvider time, IExternalLinks links, EditingUseCases editing, EditorEnvironment environment, SettingsUseCases? settings = null, ISpeech? speech = null, MachineSettings? machine = null)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(vaults);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(editing);
        ArgumentNullException.ThrowIfNull(environment);
        _settings = settings;
        _speech = speech;
        _machine = machine;
        _editing = editing;
        _environment = environment;
        _links = links;
        _annotations = annotations;
        _sheets = sheets;
        _reviews = reviews;
        _reminders = reminders;
        _vaults = vaults;
        _sessions = sessions;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private ShellViewModel Shell => _shell ?? throw new InvalidOperationException("La coquille n'est pas encore assemblée.");

    /// <summary>La coquille où les pages s'ouvrent (elle est assemblée après les vues qui ouvrent ces pages).</summary>
    public void Attach(ShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _shell = shell;
    }

    /// <inheritdoc/>
    public void Open(DueItem item, bool activeRecall)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind == ItemKind.Sheet)
        {
            OpenSheet(item.Id, revising: true);
        }
        else
        {
            OpenPassage(item.Id, item.SheetId, activeRecall);
        }
    }

    /// <inheritdoc/>
    public void OpenReminder(PendingReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        OpenSheet(reminder.SheetId);
    }

    /// <inheritdoc/>
    /// <remarks>« Retour » ramène à la page courante.</remarks>
    public void OpenSheet(string sheetId) => OpenSheet(sheetId, revising: false);

    /// <inheritdoc/>
    public void OpenPassageInSheet(string sheetId, string passageId) => OpenSheet(sheetId, revising: false, passageId);

    /// <inheritdoc/>
    public void ReviseSheet(string sheetId) => OpenSheet(sheetId, revising: true);

    /// <summary>Ouvre une fiche en lecture, et y sélectionne un texte s'il est donné : là où une extension la rouvre.</summary>
    /// <param name="sheetId">La fiche.</param>
    /// <param name="start">Le début du texte, dans le texte affiché.</param>
    /// <param name="end">Sa fin.</param>
    public void OpenSheetAt(string sheetId, int? start, int? end) =>
        OpenSheet(sheetId, revising: false, range: start is { } from && end is { } to && to > from ? (from, to) : null);

    /// <inheritdoc/>
    public void WriteSheet(string sheetId) => OpenSheet(sheetId, revising: false, writes: true);

    private void OpenSheet(string sheetId, bool revising, string? passageId = null, bool writes = false, (int Start, int End)? range = null)
    {
        var shell = Shell;
        var from = shell.Current;
        var page = new SheetPageViewModel(_sheets, _annotations, Bar, sheetId, () => shell.Navigate(from), OpenSheet, _links, Editor, revising, ActiveRecall) { PassageToShow = passageId, RangeToShow = range, ReadAloud = _speech is null ? null : new ReadAloudViewModel(_speech, _machine), ReadingMaxWidth = _machine?.Current.Width == ReadingWidth.Full ? double.PositiveInfinity : SheetPageViewModel.TextMaxWidth };
        shell.Navigate(new NavigationItem("fiche", ReviewText.SheetPage, Icons.Document, int.MaxValue, page));
        if (writes)
        {
            _ = LoadThenWriteAsync(page);
        }
        else
        {
            page.LoadCommand.Execute(null);
        }
    }

    private static async Task LoadThenWriteAsync(SheetPageViewModel page)
    {
        await page.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        await page.StartWritingCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    private void OpenPassage(string passageId, string sheetId, bool activeRecall)
    {
        var shell = Shell;
        var from = shell.Current;
        var page = new PassagePageViewModel(_sheets, Bar, passageId, sheetId, activeRecall, () => shell.Navigate(from), OpenSheet);
        shell.Navigate(new NavigationItem("passage", ReviewText.Passage, Icons.Highlighter, int.MaxValue, page));
        page.LoadCommand.Execute(null);
    }

    /// <inheritdoc/>
    public void StartSession(IReadOnlyList<DueItem> items, bool activeRecall)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || _vaults.State().Vault is not { } vault)
        {
            return;
        }
        ShowSession(new StudySession(vault, Today, [.. items.Select(item => new SessionItem(item.Kind, item.Id, item.SheetId))], 0, 0, 0, 0, TimeSpan.Zero, activeRecall));
    }

    /// <inheritdoc/>
    public int? ResumableItems() => Resumable()?.Remaining;

    /// <inheritdoc/>
    public void ResumeSession()
    {
        if (Resumable() is { } session)
        {
            ShowSession(session);
        }
    }

    /// <summary>La séance gardée, si elle est de ce coffre et d'aujourd'hui, et qu'il lui reste des éléments.</summary>
    private StudySession? Resumable() =>
        _sessions.Load() is { Remaining: > 0 } session && session.Day == Today && _vaults.State().Vault is { } vault && session.Vault == vault ? session : null;

    private void ShowSession(StudySession session)
    {
        var shell = Shell;
        var from = shell.Current;
        StudySessionViewModel? study = null;
        void Leave()
        {
            study!.Interrupt();
            shell.Navigate(from);
        }
        study = new StudySessionViewModel(session, _sessions, _time, (item, activeRecall) => ItemPage(item, activeRecall, Leave), () => shell.Navigate(from), shell.ToggleFullScreen);
        shell.Navigate(new NavigationItem("seance", SessionText.Session, Icons.Calendar, int.MaxValue, study));
    }

    /// <summary>La page d'un élément de séance ; « Retour » quitte la séance, gardée pour être reprise.</summary>
    private SheetEditorViewModel Editor(OpenedSheet sheet) => new(sheet, _editing, _sheets, _environment);

    private object ItemPage(SessionItem item, bool activeRecall, Action leave)
    {
        if (item.Kind == ItemKind.Sheet)
        {
            var sheet = new SheetPageViewModel(_sheets, _annotations, Bar, item.Id, leave, OpenSheet, _links, Editor, revising: true, () => activeRecall);
            sheet.LoadCommand.Execute(null);
            return sheet;
        }
        var passage = new PassagePageViewModel(_sheets, Bar, item.Id, item.SheetId, activeRecall, leave, OpenSheet);
        passage.LoadCommand.Execute(null);
        return passage;
    }

    /// <summary>Le rappel actif est choisi dans les réglages du coffre ouvert.</summary>
    private bool ActiveRecall() => _settings?.Get() is { Succeeded: true } preferences && preferences.Value.ActiveRecall;

    private ReviewBarViewModel Bar(ItemKind kind, string id, ReviewState state) => new(_reviews, _reminders, _sheets, _time, kind, id, state);
}
