using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La vue « Paramètres », première partie : la séquence des étapes et les rappels de consolidation.
/// Tout s'enregistre d'un bloc ; ce que la vue ne montre pas encore ne change pas.
/// </summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    /// <summary>Le plus de rappels par jour que l'on puisse demander.</summary>
    public const int MaxPerDayLimit = 20;

    /// <summary>La plus longue attente que l'on puisse demander, en jours.</summary>
    public const int MaxWaitDaysLimit = 365;

    private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    private readonly SettingsUseCases _settings;
    private readonly TimeProvider _time;
    private VaultPreferences? _stored;

    /// <summary>La vue, sur ces cas d'usage.</summary>
    public SettingsPageViewModel(SettingsUseCases settings, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _settings = settings;
        _time = time;
    }

    /// <summary>Les étapes après J0, telles que saisies.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges), nameof(StepsPreview), nameof(HasStepsPreview))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Steps { get; set; } = string.Empty;

    /// <summary>Le rappel actif.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool ActiveRecall { get; set; }

    /// <summary>Le nombre de rappels par jour, tel que saisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string MaxPerDay { get; set; } = string.Empty;

    /// <summary>L'attente avant la priorité, telle que saisie.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string MaxWaitDays { get; set; } = string.Empty;

    /// <summary>Les repos, tels que saisis.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string RestMonths { get; set; } = string.Empty;

    /// <summary>Les fiches archivées à la main reviennent aussi en rappel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IncludeManualArchiving { get; set; }

    /// <summary>La première lecture est en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Les réglages sont là, le formulaire peut se montrer.</summary>
    [ObservableProperty]
    public partial bool IsReady { get; private set; }

    /// <summary>Ce qui vient d'échouer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Le dernier enregistrement a réussi, et rien n'a changé depuis.</summary>
    [ObservableProperty]
    public partial bool IsSaved { get; private set; }

    /// <summary>Quelque chose vient d'échouer.</summary>
    public bool HasError => Error is not null;

    /// <summary>Ce qui est saisi diffère de ce qui est enregistré.</summary>
    public bool HasChanges
    {
        get
        {
            if (_stored is null)
            {
                return false;
            }
            return !Same(Numbers(Steps), _stored.Steps)
                || !Same(Numbers(RestMonths), _stored.RestSequenceMonths)
                || Number(MaxPerDay) != _stored.MaxPerDay
                || Number(MaxWaitDays) != _stored.MaxWaitDays
                || ActiveRecall != _stored.ActiveRecall
                || IncludeManualArchiving != _stored.IncludeManualArchiving;
        }
    }

    /// <summary>Ce que donne la séquence saisie ; rien tant qu'elle ne se lit pas.</summary>
    public string StepsPreview
    {
        get
        {
            if (Numbers(Steps) is not { IsEmpty: false } steps || steps.Any(step => step <= 0) || !steps.SequenceEqual(steps.Order().Distinct()))
            {
                return string.Empty;
            }
            var day = Present.Of(_time).Now.Day;
            var dates = new List<(int, DateOnly)>(steps.Length);
            foreach (var step in steps)
            {
                // Une date au-delà du calendrier ne se montre pas : le cœur refusera la séquence.
                if ((long)day.DayNumber + step >= DateOnly.MaxValue.DayNumber)
                {
                    return string.Empty;
                }
                day = day.AddDays(step);
                dates.Add((step, day));
            }
            return SettingsText.Preview(dates);
        }
    }

    /// <summary>La séquence saisie se lit : ce qu'elle donne se montre.</summary>
    public bool HasStepsPreview => StepsPreview.Length > 0;

    /// <summary>Un autre coffre vient de s'ouvrir : ce qui était saisi ne le concerne pas.</summary>
    public void Forget()
    {
        _stored = null;
        OnPropertyChanged(nameof(HasChanges));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lit les réglages, sans écraser ce qui est en cours de saisie.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (HasChanges)
        {
            return;
        }
        IsLoading = _stored is null;
        var outcome = await Task.Run(_settings.Get).ConfigureAwait(true);
        IsLoading = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            IsReady = false;
            return;
        }
        Error = null;
        Show(outcome.Value);
        IsReady = true;
    }

    private bool CanSave() => HasChanges;

    /// <summary>Enregistre ce qui est saisi, après l'avoir vérifié.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaved = false;
        if (Check() is { } refused)
        {
            Error = refused;
            return;
        }
        var (steps, rest, maxPerDay, maxWait, recall, manual) = (Numbers(Steps)!.Value, Numbers(RestMonths)!.Value, Number(MaxPerDay)!.Value, Number(MaxWaitDays)!.Value, ActiveRecall, IncludeManualArchiving);
        var outcome = await Task.Run(() => _settings.Update(current => current with
        {
            Steps = steps,
            RestSequenceMonths = rest,
            MaxPerDay = maxPerDay,
            MaxWaitDays = maxWait,
            ActiveRecall = recall,
            IncludeManualArchiving = manual,
        })).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Error = null;
        Show(outcome.Value);
        IsSaved = true;
    }

    private string? Check()
    {
        if (Numbers(Steps) is null || Numbers(RestMonths) is null)
        {
            return SettingsText.WholeNumbers;
        }
        if (Number(MaxPerDay) is not (>= 1 and <= MaxPerDayLimit))
        {
            return SettingsText.MaxPerDayRange;
        }
        return Number(MaxWaitDays) is not (>= 1 and <= MaxWaitDaysLimit) ? SettingsText.MaxWaitDaysRange : null;
    }

    private void Show(VaultPreferences preferences)
    {
        _stored = preferences;
        Steps = string.Join(", ", preferences.Steps);
        RestMonths = string.Join(", ", preferences.RestSequenceMonths);
        MaxPerDay = preferences.MaxPerDay.ToString(CultureInfo.InvariantCulture);
        MaxWaitDays = preferences.MaxWaitDays.ToString(CultureInfo.InvariantCulture);
        ActiveRecall = preferences.ActiveRecall;
        IncludeManualArchiving = preferences.IncludeManualArchiving;
        OnPropertyChanged(nameof(HasChanges));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static bool Same(ImmutableArray<int>? typed, ImmutableArray<int> stored) => typed is { } numbers && numbers.SequenceEqual(stored);

    private static int? Number(string text) =>
        int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Une liste de nombres, séparés comme on le veut ; rien si l'un d'eux ne se lit pas.</summary>
    private static ImmutableArray<int>? Numbers(string text)
    {
        var numbers = ImmutableArray.CreateBuilder<int>();
        foreach (var part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Number(part) is not { } number)
            {
                return null;
            }
            numbers.Add(number);
        }
        return numbers.ToImmutable();
    }

    partial void OnStepsChanged(string value) => IsSaved = false;

    partial void OnActiveRecallChanged(bool value) => IsSaved = false;

    partial void OnMaxPerDayChanged(string value) => IsSaved = false;

    partial void OnMaxWaitDaysChanged(string value) => IsSaved = false;

    partial void OnRestMonthsChanged(string value) => IsSaved = false;

    partial void OnIncludeManualArchivingChanged(bool value) => IsSaved = false;
}
