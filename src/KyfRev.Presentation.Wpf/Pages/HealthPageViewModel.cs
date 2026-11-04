using System.Globalization;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une sauvegarde à choisir, pour restaurer une fiche manquante.</summary>
public sealed record BackupChoice(string Name, string Label)
{
    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>Une ligne de la santé du coffre : ce qui est concerné, et ce qu'on peut y faire.</summary>
public sealed class HealthRow
{
    internal HealthRow(string title, string detail, string? actionLabel = null, IRelayCommand? action = null, bool subtle = false, IReadOnlyList<BackupChoice>? backups = null, string? hint = null)
    {
        Title = title;
        Detail = detail;
        ActionLabel = actionLabel;
        Action = action;
        IsSubtle = subtle;
        Backups = backups ?? [];
        SelectedBackup = Backups.Count > 0 ? Backups[0] : null;
        Hint = hint;
    }

    /// <summary>Le titre de la fiche, ou le nom du fichier.</summary>
    public string Title { get; }

    /// <summary>Son chemin, ou ce qui lui arrive.</summary>
    public string Detail { get; }

    /// <summary>« Restaurer », « Garder comme original »… ; rien pour une simple information.</summary>
    public string? ActionLabel { get; }

    /// <summary>Ce que fait le bouton.</summary>
    public IRelayCommand? Action { get; }

    /// <summary>L'action est une simple ouverture, pas une réparation.</summary>
    public bool IsSubtle { get; }

    /// <summary>La ligne a un bouton de réparation.</summary>
    public bool HasRepair => Action is not null && !IsSubtle;

    /// <summary>La ligne a un bouton d'ouverture.</summary>
    public bool HasOpening => Action is not null && IsSubtle;

    /// <summary>Les sauvegardes d'où restaurer une fiche manquante.</summary>
    public IReadOnlyList<BackupChoice> Backups { get; }

    /// <summary>Il y a une sauvegarde à choisir.</summary>
    public bool HasBackups => Backups.Count > 0;

    /// <summary>La sauvegarde choisie.</summary>
    public BackupChoice? SelectedBackup { get; set; }

    /// <summary>« Aucune sauvegarde disponible. ».</summary>
    public string? Hint { get; }

    /// <summary>Il y a une indication à montrer.</summary>
    public bool HasHint => Hint is not null;

    /// <summary>Le nom du bouton : toutes les lignes ont le même, le titre les distingue.</summary>
    public string ActionName => HealthText.ActionOnOf(ActionLabel ?? string.Empty, Title);

    /// <summary>Le nom lu par le lecteur d'écran.</summary>
    public string Spoken => Detail.Length == 0 ? Title : HealthText.ActionOnOf(Title, Detail);

    /// <inheritdoc/>
    public override string ToString() => Spoken;
}

/// <summary>Une section de la santé du coffre : un type de problème, ses lignes, et ce qu'il faut en savoir.</summary>
/// <param name="Title">« Fiches manquantes (2) ».</param>
/// <param name="Help">Ce qui se passe, et ce qu'on peut décider.</param>
/// <param name="Rows">Les lignes.</param>
/// <param name="IsInformation">Rien n'y attend de décision.</param>
public sealed record HealthSection(string Title, string? Help, IReadOnlyList<HealthRow> Rows, bool IsInformation = false)
{
    /// <summary>La section explique ce qui se passe.</summary>
    public bool HasHelp => Help is not null;
}

/// <summary>
/// Vue « Santé du coffre », comme dans la 0.x : chaque problème attend une décision, et rien n'est réparé ni effacé sans
/// la personne. En bas, le diagnostic à copier pour un signalement : rien ne part tout seul.
/// </summary>
public sealed partial class HealthPageViewModel : ObservableObject, INavigationAware
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly MaintenanceUseCases _maintenance;
    private readonly BackupUseCases _backups;
    private readonly SheetUseCases _sheets;
    private readonly IReviewOpener _opener;
    private readonly Func<string> _diagnostic;
    private readonly Action<string> _copy;
    private readonly IEnumerable<IVaultHealthCheck> _checks;
    private IReadOnlyList<BackupEntry> _backupList = [];
    private bool _shown;

    /// <summary>La vue ; <paramref name="diagnostic"/> rédige le diagnostic, <paramref name="copy"/> le met dans le presse-papiers.</summary>
    public HealthPageViewModel(MaintenanceUseCases maintenance, BackupUseCases backups, SheetUseCases sheets, IReviewOpener opener, Func<string> diagnostic, Action<string> copy, IEnumerable<IVaultHealthCheck>? checks = null)
    {
        ArgumentNullException.ThrowIfNull(maintenance);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(opener);
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(copy);
        _maintenance = maintenance;
        _backups = backups;
        _sheets = sheets;
        _opener = opener;
        _diagnostic = diagnostic;
        _copy = copy;
        _checks = checks ?? [];
    }

    /// <summary>Copie ce que dit une ligne, pour un signalement.</summary>
    internal void CopyRow(HealthRow row) => _copy($"{row.Title} : {row.Detail}");

    /// <summary>« Santé du coffre ».</summary>
    public static string Title => HealthText.Title;

    /// <summary>« Aucun problème : tout est en ordre. », ou le nombre de points qui attendent une décision.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    /// <summary>Aucun point n'attend de décision.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems))]
    public partial bool IsHealthy { get; private set; }

    /// <summary>Au moins un point attend une décision.</summary>
    public bool HasProblems => IsLoaded && !IsHealthy;

    /// <summary>Les sections à montrer : celles qui ont quelque chose à dire.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSections))]
    public partial IReadOnlyList<HealthSection> Sections { get; private set; } = [];

    /// <summary>Il y a au moins une section : une liste vide n'a pas de place à l'écran, et n'a pas à être annoncée.</summary>
    public bool HasSections => Sections.Count > 0;

    /// <summary>Le rapport est là.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Première lecture en cours.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Une analyse ou une réparation est en cours.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Rien n'est en cours : les boutons répondent.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Refus de la dernière opération.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une opération a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière réparation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une réparation vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Le texte du diagnostic est affiché.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiagnosticToggle))]
    public partial bool IsDiagnosticShown { get; private set; }

    /// <summary>« Afficher » ou « Masquer ».</summary>
    public string DiagnosticToggle => IsDiagnosticShown ? HealthText.Hide : HealthText.Show;

    /// <summary>Le texte du diagnostic.</summary>
    [ObservableProperty]
    public partial string DiagnosticText { get; private set; } = string.Empty;

    /// <summary>Ce que le diagnostic vient de faire.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiagnosticMessage))]
    public partial string? DiagnosticMessage { get; private set; }

    /// <summary>Le diagnostic a quelque chose à dire.</summary>
    public bool HasDiagnosticMessage => DiagnosticMessage is not null;

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Message = null;
        RefreshCommand.Execute(null);
    }

    /// <summary>Montre ce rapport ; <paramref name="backups"/> sont les sauvegardes d'où restaurer une fiche manquante, <paramref name="notices"/> les remarques des modules.</summary>
    public void Show(HealthDetails report, IReadOnlyList<BackupEntry> backups, IReadOnlyList<HealthNotice>? notices = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(backups);
        _shown = true;
        _backupList = backups;
        Error = null;
        IsHealthy = report.Problems == 0;
        Summary = report.Problems == 0 ? HealthText.AllGood : Dates.Plural(report.Problems, HealthText.PointOneOf, HealthText.PointManyOf);
        Sections = BuildSections(report, notices ?? []);
        IsLoaded = true;
    }

    /// <summary>Le nom du fichier d'un chemin du coffre.</summary>
    internal static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string BackupLabel(BackupEntry backup) => backup.Timestamp.ToString("d MMM yyyy, HH:mm", French);

    private List<HealthSection> BuildSections(HealthDetails report, IReadOnlyList<HealthNotice> notices)
    {
        var sections = new List<HealthSection>();
        void Add(Func<object, string> title, string? help, IReadOnlyList<HealthRow> rows)
        {
            if (rows.Count > 0)
            {
                sections.Add(new HealthSection(title(rows.Count), help, rows));
            }
        }

        var choices = _backupList.Select(backup => new BackupChoice(backup.Name, BackupLabel(backup))).ToList();
        Add(HealthText.MissingTitleOf, HealthText.MissingHelp, [.. report.Missing.Select(sheet => MissingRow(sheet, choices))]);
        Add(HealthText.DuplicatesTitleOf, HealthText.DuplicatesHelp, [.. report.Duplicates.SelectMany(DuplicateRows)]);
        Add(
            HealthText.UntrackedTitleOf,
            HealthText.UntrackedHelp,
            [
                .. report.Untracked.Select(path => new HealthRow(
                    FileName(path),
                    path,
                    HealthText.Track,
                    new AsyncRelayCommand(() => ActAsync(() => _sheets.Track(path).Error, HealthText.TrackedOf(FileName(path)))))),
                .. report.WithoutTracking.Select(sheet => new HealthRow(
                    sheet.Title,
                    HealthText.TrackingLostOf(sheet.Path),
                    HealthText.RecreateTracking,
                    new AsyncRelayCommand(() => ActAsync(() => _maintenance.RecreateTracking(sheet.Path).Error, HealthText.TrackingRecreatedOf(sheet.Title))))),
            ]);
        Add(
            HealthText.BrokenLinksTitleOf,
            HealthText.BrokenLinksHelp,
            [.. report.BrokenLinks.Select(link => new HealthRow(link.Title, HealthText.LinkTargetOf(link.Target), HealthText.OpenSheet, new RelayCommand(() => _opener.OpenSheet(link.SheetId)), subtle: true))]);
        Add(
            HealthText.PassagesToCheckTitleOf,
            HealthText.PassagesToCheckHelp,
            [
                .. report.AnnotationsToCheck.Select(passage => new HealthRow(
                    HealthText.QuotedOf(passage.Exact),
                    HealthText.PassageStateOf(passage.Title, passage.State == AnchorState.NeedsReview ? HealthText.PositionToCheck : HealthText.NotFound),
                    HealthText.SeeInSheet,
                    new RelayCommand(() => _opener.OpenPassageInSheet(passage.SheetId, passage.Id)),
                    subtle: true)),
            ]);
        Add(
            HealthText.OrphansTitleOf,
            HealthText.OrphansHelp,
            [.. report.OrphanAnnotations.Select(passage => new HealthRow(HealthText.QuotedOf(passage.Exact), passage.Title))]);
        Add(
            HealthText.ToExamineTitleOf,
            HealthText.ToExamineHelp,
            [
                .. report.Unreadable.Select(file => new HealthRow(file.Path, file.Message)),
                .. report.LinksToCheck.Select(path => new HealthRow(path, HealthText.LinksToCheck)),
            ]);

        List<HealthRow> information =
        [
            .. report.Moved.Select(moved => new HealthRow(moved.Title, HealthText.MovedOf(moved.To))),
            .. report.ModifiedOutside.Select(sheet => new HealthRow(sheet.Title, HealthText.ModifiedOutside)),
            .. notices.Select(notice => new HealthRow(notice.Path, notice.Message)),
        ];
        if (information.Count > 0)
        {
            sections.Add(new HealthSection(HealthText.Information, null, information, IsInformation: true));
        }
        return sections;
    }

    private HealthRow MissingRow(ConcernedSheet sheet, List<BackupChoice> choices)
    {
        if (choices.Count == 0)
        {
            return new HealthRow(sheet.Title, sheet.Path, hint: HealthText.NoBackup);
        }
        HealthRow? row = null;
        row = new HealthRow(
            sheet.Title,
            sheet.Path,
            HealthText.Restore,
            new AsyncRelayCommand(() => ActAsync(() => _maintenance.RestoreFromBackup(sheet.Id, row!.SelectedBackup!.Name).Error, HealthText.RestoredOf(sheet.Title))),
            backups: choices);
        return row;
    }

    private IEnumerable<HealthRow> DuplicateRows(DuplicateDetails duplicate) => duplicate.Paths.Select(path => new HealthRow(
        duplicate.Title,
        path,
        HealthText.KeepOriginal,
        new AsyncRelayCommand(() => ActAsync(() => _maintenance.KeepOriginal(duplicate.Id, path).Error, HealthText.KeptOf(FileName(path))))));

    /// <summary>Lit le rapport sans analyser : l'ouverture de la vue reste immédiate.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = !_shown;
        try
        {
            await ReadAsync(_maintenance.Health).ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>« Analyser à nouveau » : le coffre est relu en entier.</summary>
    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        Message = null;
        try
        {
            await ReadAsync(_maintenance.Analyze).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReadAsync(Func<Outcome<HealthDetails>> read)
    {
        var (report, backups, notices) = await Task.Run(() => (read(), _backups.List(), _checks.SelectMany(check => check.Notices()).ToList())).ConfigureAwait(true);
        if (report.Succeeded)
        {
            Show(report.Value, backups.Succeeded ? backups.Value.Backups : [], notices);
        }
        else
        {
            Error = report.Error;
        }
    }

    /// <summary>Une réparation, puis le rapport relu ; <paramref name="action"/> rend son refus, ou rien.</summary>
    private async Task ActAsync(Func<string?> action, string done)
    {
        IsBusy = true;
        Error = null;
        Message = null;
        try
        {
            var refusal = await Task.Run(action).ConfigureAwait(true);
            if (refusal is not null)
            {
                Error = refusal;
                return;
            }
            Message = done;
            await ReadAsync(_maintenance.Health).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>« Copier le diagnostic » : si la copie échoue, le texte s'affiche pour être sélectionné à la main.</summary>
    [RelayCommand]
    private void CopyDiagnostic()
    {
        DiagnosticText = _diagnostic();
        try
        {
            _copy(DiagnosticText);
            DiagnosticMessage = HealthText.DiagnosticCopied;
        }
        catch (ExternalException)
        {
            IsDiagnosticShown = true;
            DiagnosticMessage = HealthText.DiagnosticNotCopied;
        }
    }

    /// <summary>« Afficher » ou « Masquer » le texte du diagnostic.</summary>
    [RelayCommand]
    private void ToggleDiagnostic()
    {
        IsDiagnosticShown = !IsDiagnosticShown;
        if (IsDiagnosticShown)
        {
            DiagnosticText = _diagnostic();
        }
    }
}
