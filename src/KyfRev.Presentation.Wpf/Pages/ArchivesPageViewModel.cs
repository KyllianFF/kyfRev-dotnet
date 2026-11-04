using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Domain.Reviews;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Depuis quand une fiche est archivée.</summary>
public enum ArchivePeriod
{
    /// <summary>N'importe quand.</summary>
    All,

    /// <summary>Ce dernier mois.</summary>
    LastMonth,

    /// <summary>Ces six derniers mois.</summary>
    LastSixMonths,

    /// <summary>Il y a plus de six mois.</summary>
    Older,
}

/// <summary>Combien de cycles une fiche a terminés.</summary>
public enum ArchiveCycles
{
    /// <summary>Peu importe.</summary>
    All,

    /// <summary>Un seul.</summary>
    One,

    /// <summary>Deux.</summary>
    Two,

    /// <summary>Trois ou plus.</summary>
    ThreeOrMore,
}

/// <summary>L'ordre de la liste.</summary>
public enum ArchiveSort
{
    /// <summary>Les plus récemment archivées d'abord.</summary>
    Archiving,

    /// <summary>Par titre.</summary>
    Title,

    /// <summary>Le plus de cycles d'abord.</summary>
    Cycles,
}

/// <summary>Une fiche archivée, telle que la liste la montre.</summary>
public sealed class ArchiveRow
{
    internal ArchiveRow(ArchivedSheetEntry entry)
    {
        Entry = entry;
        var parts = new List<string>();
        if (entry.Folder.Length > 0)
        {
            parts.Add(entry.Folder.Replace("/", " › ", StringComparison.Ordinal));
        }
        var archived = Dates.Short(entry.ArchivedOn);
        parts.Add(entry.ManualArchiving ? ArchivesText.ArchivedByHandOf(archived) : ArchivesText.ArchivedOnOf(archived));
        parts.Add(Dates.Plural(entry.CompletedCycles, ArchivesText.CompletedCyclesOneOf, ArchivesText.CompletedCyclesManyOf));
        if (entry.LastReminderOn is { } reminded && entry.LastReminderOutcome is { } outcome)
        {
            parts.Add(ArchivesText.LastReminderOf(Dates.Short(reminded), OutcomeLabel(outcome)));
        }
        Meta = string.Join(" · ", parts);
        Status = entry.ReminderStatus switch
        {
            ReminderStatus.Resting => ArchivesText.RestingUntilOf(Dates.Short(entry.EligibleOn)),
            ReminderStatus.Eligible => ArchivesText.Eligible,
            ReminderStatus.Pending => ArchivesText.Pending,
            _ => ArchivesText.Excluded,
        };
        StatusKind = entry.ReminderStatus is ReminderStatus.Eligible or ReminderStatus.Pending ? PillKind.Reminder : PillKind.Archived;
    }

    /// <summary>La ligne lue dans le coffre.</summary>
    public ArchivedSheetEntry Entry { get; }

    /// <summary>Titre.</summary>
    public string Title => Entry.Title;

    /// <summary>« Histoire · archivée le ven. 9 oct. · 1 cycle terminé » : sous le titre.</summary>
    public string Meta { get; }

    /// <summary>Le fichier de la fiche est introuvable ou en double.</summary>
    public bool FileMissing => !Entry.FilePresent;

    /// <summary>« Au repos jusqu'au ven. 9 oct. ».</summary>
    public string Status { get; }

    /// <summary>La pastille de cet état.</summary>
    public PillKind StatusKind { get; }

    /// <summary>La fiche est exclue des rappels.</summary>
    public bool IsExcluded => Entry.ReminderStatus == ReminderStatus.Excluded;

    /// <summary>« Exclure des rappels » ou « Réinclure ».</summary>
    public string ToggleLabel => IsExcluded ? ArchivesText.Include : ArchivesText.Exclude;

    /// <summary>Le nom lu par le lecteur d'écran, pour ouvrir la fiche.</summary>
    public string Spoken => string.Join(", ", new[] { Title, Meta, Status, FileMissing ? DashboardText.FileMissing : string.Empty }.Where(part => part.Length > 0));

    /// <summary>Le nom du bouton d'exclusion : toutes les lignes ont le même, le titre les distingue.</summary>
    public string ToggleName => ArchivesText.ActionOnOf(ToggleLabel, Title);

    /// <summary>Le nom du bouton de remise en révision.</summary>
    public string RestartName => ArchivesText.ActionOnOf(ArchivesText.RestartConfirm, Title);

    /// <inheritdoc/>
    public override string ToString() => Spoken;

    private static string OutcomeLabel(ReminderOutcome outcome) => outcome switch
    {
        ReminderOutcome.Known => ArchivesText.OutcomeKnown,
        ReminderOutcome.SomePointsToReview => ArchivesText.OutcomeSomePoints,
        _ => ArchivesText.OutcomeEntirely,
    };
}

/// <summary>
/// Vue « Archives », comme dans la 0.x : rien n'y est supprimé ; une fiche peut être relue, remise
/// en révision, exclue des rappels ou réincluse.
/// </summary>
public sealed partial class ArchivesPageViewModel : ObservableObject, INavigationAware
{
    /// <summary>Largeur sous laquelle les actions passent sous le titre d'une ligne.</summary>
    public const double RoomMinimum = 600;

    private static readonly StringComparer FrenchTitles = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: false);

    private readonly ReminderUseCases _reminders;
    private readonly ReviewUseCases _reviews;
    private readonly IReviewOpener _opener;
    private readonly TimeProvider _time;
    private readonly ListFilter _folder;
    private readonly ListFilter _status;
    private readonly ListFilter _period;
    private readonly ListFilter _cycles;
    private readonly ListFilter _sort;
    private IReadOnlyList<ArchivedSheetEntry>? _entries;
    private ArchiveRow? _restarting;

    /// <summary>La vue, sur les cas d'usage des rappels (qui rendent les archives) et des révisions ; <paramref name="opener"/> ouvre ses fiches.</summary>
    public ArchivesPageViewModel(ReminderUseCases reminders, ReviewUseCases reviews, IReviewOpener opener, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(time);
        _reminders = reminders;
        _reviews = reviews;
        _opener = opener;
        _time = time;
        Bar = new FilterBarViewModel(BuildRows);
        _folder = Bar.Add(ArchivesText.FilterFolder, [new(string.Empty, ArchivesText.AllFolders)]);
        _status = Bar.Add(
            ArchivesText.FilterReminders,
            [
                new(null, ArchivesText.AllStatuses),
                new(ReminderStatus.Resting, ArchivesText.StatusResting),
                new(ReminderStatus.Eligible, ArchivesText.StatusEligible),
                new(ReminderStatus.Pending, ArchivesText.Pending),
                new(ReminderStatus.Excluded, ArchivesText.StatusExcluded),
            ]);
        _period = Bar.Add(
            ArchivesText.FilterPeriod,
            [
                new(ArchivePeriod.All, ArchivesText.PeriodAll),
                new(ArchivePeriod.LastMonth, ArchivesText.PeriodLastMonth),
                new(ArchivePeriod.LastSixMonths, ArchivesText.PeriodLastSixMonths),
                new(ArchivePeriod.Older, ArchivesText.PeriodOlder),
            ]);
        _cycles = Bar.Add(
            ArchivesText.FilterCycles,
            [
                new(ArchiveCycles.All, ArchivesText.AllCycles),
                new(ArchiveCycles.One, "1"),
                new(ArchiveCycles.Two, "2"),
                new(ArchiveCycles.ThreeOrMore, ArchivesText.ThreeOrMoreCycles),
            ]);
        _sort = Bar.Add(
            ArchivesText.FilterSort,
            [
                new(ArchiveSort.Archiving, ArchivesText.SortArchiving),
                new(ArchiveSort.Title, ArchivesText.SortTitle),
                new(ArchiveSort.Cycles, ArchivesText.SortCycles),
            ],
            counts: false);
        Rows = [];
        ConfirmMessage = string.Empty;
    }

    /// <summary>« Archives ».</summary>
    public static string Title => ShellText.Archives;

    /// <summary>La recherche et les filtres.</summary>
    public FilterBarViewModel Bar { get; }

    /// <summary>
    /// « 5 fiches archivées · elles restent ici… » ; le nombre seul en fenêtre étroite ; rien
    /// avant la première lecture.
    /// </summary>
    public string Summary
    {
        get
        {
            if (_entries is not { } entries)
            {
                return string.Empty;
            }
            var count = Dates.Plural(entries.Count, ArchivesText.CountOneOf, ArchivesText.CountManyOf);
            return HasRoom ? $"{count} · {ArchivesText.Promise}" : count;
        }
    }

    /// <summary>Les fiches retenues par les filtres, dans l'ordre choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    public partial IReadOnlyList<ArchiveRow> Rows { get; private set; }

    /// <summary>Au moins une fiche à montrer.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Le coffre a été lu, et n'a aucune fiche archivée.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>Le coffre a des fiches archivées, mais aucune ne correspond aux filtres.</summary>
    [ObservableProperty]
    public partial bool HasNoMatch { get; private set; }

    /// <summary>Il y a des fiches archivées : la barre de filtres a un sens.</summary>
    [ObservableProperty]
    public partial bool HasArchives { get; private set; }

    /// <summary>Première lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Refus de la dernière opération.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une opération a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>La remise en révision attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirming { get; private set; }

    /// <summary>Ce que la confirmation annonce.</summary>
    [ObservableProperty]
    public partial string ConfirmMessage { get; private set; }

    /// <summary>Largeur de la page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoom), nameof(Summary))]
    public partial double PageWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Assez de largeur pour montrer les actions à droite du titre.</summary>
    public bool HasRoom => PageWidth >= RoomMinimum;

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Message = null;
        RefreshCommand.Execute(null);
    }

    /// <summary>Montre ces fiches archivées.</summary>
    public void Show(IReadOnlyList<ArchivedSheetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
        Error = null;
        HasArchives = entries.Count > 0;
        OnPropertyChanged(nameof(Summary));
        _folder.Replace(
        [
            new(string.Empty, ArchivesText.AllFolders),
            .. entries.Select(entry => entry.Folder).Where(folder => folder.Length > 0).Distinct().Order(FrenchTitles)
                .Select(folder => new Choice<object?>(folder, folder.Replace("/", " › ", StringComparison.Ordinal))),
        ]);
        BuildRows();
    }

    partial void OnPageWidthChanged(double value) => Bar.HasRoom = HasRoom;

    /// <summary>Relit les archives.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = _entries is null;
        try
        {
            var outcome = await Task.Run(_reminders.Archives).ConfigureAwait(true);
            if (outcome.Succeeded)
            {
                Show(outcome.Value);
            }
            else
            {
                Error = outcome.Error;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Ouvre une fiche archivée, pour la relire.</summary>
    [RelayCommand]
    private void Open(ArchiveRow row) => _opener.OpenSheet(row.Entry.Id);

    /// <summary>« Exclure des rappels » ou « Réinclure ».</summary>
    [RelayCommand]
    private Task ToggleRemindersAsync(ArchiveRow row)
    {
        var include = row.IsExcluded;
        return ActAsync(
            () => _reminders.Include(row.Entry.Id, include).Error,
            include ? ArchivesText.IncludedMessageOf(row.Title) : ArchivesText.ExcludedMessageOf(row.Title));
    }

    /// <summary>« Remettre en révision… » : demande confirmation.</summary>
    [RelayCommand]
    private void RequestRestart(ArchiveRow row)
    {
        _restarting = row;
        ConfirmMessage = ArchivesText.RestartMessageOf(row.Title);
        IsConfirming = true;
    }

    /// <summary>La réponse à la confirmation.</summary>
    [RelayCommand]
    private async Task AnswerRestartAsync(bool confirmed)
    {
        IsConfirming = false;
        if (confirmed && _restarting is { } row)
        {
            await ActAsync(() => _reviews.Restart(ItemKind.Sheet, row.Entry.Id).Error, ArchivesText.RestartedMessageOf(row.Title)).ConfigureAwait(true);
        }
        _restarting = null;
    }

    private async Task ActAsync(Func<string?> action, string done)
    {
        Error = null;
        Message = null;
        var refused = await Task.Run(action).ConfigureAwait(true);
        if (refused is not null)
        {
            Error = refused;
            return;
        }
        Message = done;
        await RefreshAsync().ConfigureAwait(true);
    }

    private void BuildRows()
    {
        // Choisir un filtre avant la première lecture ne trie rien.
        if (_entries is not { } entries)
        {
            return;
        }
        var wanted = ShellViewModel.Fold(Bar.Search.Trim());
        var day = Present.Of(_time).Now.Day;
        var kept = entries.Where(entry => Matches(entry, wanted, day));
        Rows = [.. Sorted(kept).Select(entry => new ArchiveRow(entry))];
        IsEmpty = entries.Count == 0;
        HasNoMatch = entries.Count > 0 && Rows.Count == 0;
    }

    private bool Matches(ArchivedSheetEntry entry, string wanted, DateOnly day)
    {
        if (wanted.Length > 0 && !ShellViewModel.Fold($"{entry.Title} {string.Join(' ', entry.Tags)}").Contains(wanted, StringComparison.Ordinal))
        {
            return false;
        }
        var folder = (string)_folder.Value!;
        if (folder.Length > 0 && entry.Folder != folder && !entry.Folder.StartsWith(folder + "/", StringComparison.Ordinal))
        {
            return false;
        }
        if (_status.Value is ReminderStatus status && entry.ReminderStatus != status)
        {
            return false;
        }
        var inPeriod = (ArchivePeriod)_period.Value! switch
        {
            ArchivePeriod.LastMonth => entry.ArchivedOn >= day.AddDays(-31),
            ArchivePeriod.LastSixMonths => entry.ArchivedOn >= day.AddDays(-183),
            ArchivePeriod.Older => entry.ArchivedOn < day.AddDays(-183),
            _ => true,
        };
        return inPeriod && (ArchiveCycles)_cycles.Value! switch
        {
            ArchiveCycles.One => entry.CompletedCycles == 1,
            ArchiveCycles.Two => entry.CompletedCycles == 2,
            ArchiveCycles.ThreeOrMore => entry.CompletedCycles >= 3,
            _ => true,
        };
    }

    private IEnumerable<ArchivedSheetEntry> Sorted(IEnumerable<ArchivedSheetEntry> entries) => (ArchiveSort)_sort.Value! switch
    {
        ArchiveSort.Title => entries.OrderBy(entry => entry.Title, FrenchTitles),
        ArchiveSort.Cycles => entries.OrderByDescending(entry => entry.CompletedCycles).ThenBy(entry => entry.Title, FrenchTitles),
        _ => entries.OrderByDescending(entry => entry.ArchivedOn),
    };
}
