using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une extension vient de changer.</summary>
/// <param name="extension">Son identifiant.</param>
public sealed class ExtensionChangedEventArgs(string extension) : EventArgs
{
    /// <summary>Son identifiant.</summary>
    public string Extension { get; } = extension;
}

/// <summary>
/// Une permission d'une extension, dite en clair avec son poids : une alerte pour les sensibles, une alerte rouge pour celles qui
/// modifient les fiches. Dans la carte, elle se retire et s'accorde de nouveau.
/// </summary>
public sealed partial class ExtensionRightRow : ObservableObject
{
    private readonly Action<ExtensionRightRow, bool>? _changed;
    private bool _showing;

    internal ExtensionRightRow(ExtensionRight right, bool granted, bool isNew, Action<ExtensionRightRow, bool>? changed)
    {
        _changed = changed;
        Right = right;
        Risk = ExtensionRights.RiskOf(right);
        Text = TextOf(right);
        IsNew = isNew;
        Show(granted);
    }

    /// <summary>La permission.</summary>
    public ExtensionRight Right { get; }

    /// <summary>Ce qu'elle permet, en clair.</summary>
    public string Text { get; }

    /// <summary>Ce que risque l'utilisateur en l'accordant.</summary>
    public ExtensionRisk Risk { get; }

    /// <summary>Le symbole de ce risque : il se lit sans les couleurs.</summary>
    public string Glyph => Risk switch
    {
        ExtensionRisk.High => Icons.Error,
        ExtensionRisk.Notable => Icons.Warning,
        _ => Icons.Info,
    };

    /// <summary>Ce qu'en dit un lecteur d'écran.</summary>
    public string Spoken => Risk switch
    {
        ExtensionRisk.High => $"{Text}, {ModulesSettingsText.ExtensionRiskHigh}",
        ExtensionRisk.Notable => $"{Text}, {ModulesSettingsText.ExtensionRiskNotable}",
        _ => Text,
    };

    /// <summary>Une mise à jour la demande pour la première fois.</summary>
    public bool IsNew { get; }

    /// <summary>Elle est accordée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWithdrawn))]
    public partial bool IsGranted { get; set; }

    /// <summary>L'utilisateur l'a retirée.</summary>
    public bool IsWithdrawn => !IsGranted;

    /// <summary>Ce qu'une permission permet, en clair.</summary>
    internal static string TextOf(ExtensionRight right) => right switch
    {
        ExtensionRight.ReadSheets => ModulesSettingsText.ExtensionRightReadSheets,
        ExtensionRight.Storage => ModulesSettingsText.ExtensionRightStorage,
        ExtensionRight.Selection => ModulesSettingsText.ExtensionRightSelection,
        ExtensionRight.CreateSheets => ModulesSettingsText.ExtensionRightCreateSheets,
        _ => ModulesSettingsText.ExtensionRightWriteSheets,
    };

    /// <summary>Remontre l'état enregistré, sans le changer.</summary>
    internal void Show(bool granted)
    {
        _showing = true;
        IsGranted = granted;
        _showing = false;
    }

    partial void OnIsGrantedChanged(bool value)
    {
        if (!_showing)
        {
            _changed?.Invoke(this, value);
        }
    }
}

/// <summary>Une extension installée, dans la carte « Extensions ».</summary>
/// <summary>Une écriture d'une extension, sous la liste : ce qu'elle a fait à quelle fiche, puis qui et quand.</summary>
public sealed class ExtensionJournalRow
{
    internal ExtensionJournalRow(string extension, ExtensionJournalEntry entry)
    {
        At = entry.At;
        Text = entry.Action switch
        {
            ExtensionJournalEntry.Creation => entry.Detail is { Length: > 0 } folder
                ? ModulesSettingsText.ExtensionJournalCreatedInOf(entry.Title, folder)
                : ModulesSettingsText.ExtensionJournalCreatedOf(entry.Title),
            ExtensionJournalEntry.Tagging => entry.Detail is { Length: > 0 } tags
                ? ModulesSettingsText.ExtensionJournalTaggedOf(entry.Title, tags)
                : ModulesSettingsText.ExtensionJournalUntaggedOf(entry.Title),
            ExtensionJournalEntry.Unreadable => ModulesSettingsText.ExtensionJournalUnreadableOf(entry.Detail ?? string.Empty),
            _ => ModulesSettingsText.ExtensionJournalWrittenOf(entry.Title, entry.Detail ?? string.Empty),
        };
        When = entry.Action == ExtensionJournalEntry.Unreadable ? extension : ModulesSettingsText.ExtensionJournalWhenOf(extension, entry.At.ToLocalTime().DateTime);
        Spoken = $"{Text}. {When}";
    }

    /// <summary>Quand.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Ce qu'elle a fait, à quelle fiche.</summary>
    public string Text { get; }

    /// <summary>Quelle extension, et quand.</summary>
    public string When { get; }

    /// <summary>Les deux, pour un lecteur d'écran.</summary>
    public string Spoken { get; }
}

public sealed partial class ExtensionRow : ObservableObject
{
    private readonly Action<ExtensionRow, bool> _changed;
    private bool _showing;

    internal ExtensionRow(InstalledExtension extension, IReadOnlyList<ExtensionRight> withdrawn, Action<ExtensionRow, bool> changed, Action<ExtensionRow, ExtensionRightRow, bool> granted)
    {
        var manifest = extension.Manifest;
        _changed = changed;
        Id = manifest.Id;
        Name = manifest.Name;
        Version = manifest.Author is { } author ? ModulesSettingsText.ExtensionVersionByOf(manifest.Version, author) : manifest.Version;
        Description = manifest.Description;
        Adds = ModulesSettingsText.ExtensionAddsOf(string.Join(", ", ExtensionSettingsViewModel.AddsOf(manifest)));
        Rights = [.. manifest.Permissions.Select(right => new ExtensionRightRow(right, !withdrawn.Contains(right), false, (row, value) => granted(this, row, value)))];
        Show(extension.IsActive);
    }

    /// <summary>Son identifiant.</summary>
    public string Id { get; }

    /// <summary>Son nom.</summary>
    public string Name { get; }

    /// <summary>Sa version et son auteur.</summary>
    public string Version { get; }

    /// <summary>Ce qu'elle fait.</summary>
    public string Description { get; }

    /// <summary>Elle dit ce qu'elle fait.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Ce qu'elle ajoute à l'interface.</summary>
    public string Adds { get; }

    /// <summary>Les permissions qu'elle demande, chacune à retirer ou à rendre.</summary>
    public IReadOnlyList<ExtensionRightRow> Rights { get; }

    /// <summary>Elle demande des permissions.</summary>
    public bool HasRights => Rights.Count > 0;

    /// <summary>Le nom du bouton qui la retire.</summary>
    public string RemoveName => ModulesSettingsText.ExtensionRemoveNameOf(Name);

    /// <summary>Elle est active.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Remontre l'état enregistré, sans le changer.</summary>
    internal void Show(bool active)
    {
        _showing = true;
        IsActive = active;
        _showing = false;
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!_showing)
        {
            _changed(this, value);
        }
    }
}

/// <summary>
/// La carte « Extensions » de l'onglet « Modules et extensions » : installer depuis un dossier, activer, retirer. Une extension est
/// installée sur cet ordinateur, pas dans le coffre ; rien ne s'installe sans avoir montré ce qu'elle ajoute et ce qu'elle demande, et
/// une permission se retire à tout moment.
/// </summary>
public sealed partial class ExtensionSettingsViewModel : ObservableObject
{
    /// <summary>Les écritures montrées sous la liste : les plus récentes.</summary>
    public const int JournalShown = 30;

    private readonly ExtensionLibrary _library;
    private readonly IFolderPicker _picker;
    private readonly Func<string, Outcome<IReadOnlyList<ExtensionJournalEntry>>> _journal;
    private string? _source;
    private ExtensionRow? _toRemove;

    /// <summary>La carte de cette bibliothèque, sans journal.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="picker">Le choix du dossier à installer.</param>
    public ExtensionSettingsViewModel(ExtensionLibrary library, IFolderPicker picker)
        : this(library, picker, _ => Outcome.Of<IReadOnlyList<ExtensionJournalEntry>>([]))
    {
    }

    /// <summary>La carte de cette bibliothèque, avec ce que ses extensions ont écrit dans le coffre ouvert.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="picker">Le choix du dossier à installer.</param>
    /// <param name="journal">Le journal d'une extension dans le coffre ouvert, du plus récent au plus ancien.</param>
    public ExtensionSettingsViewModel(ExtensionLibrary library, IFolderPicker picker, Func<string, Outcome<IReadOnlyList<ExtensionJournalEntry>>> journal)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(journal);
        _library = library;
        _picker = picker;
        _journal = journal;
    }

    /// <summary>Ce que les extensions installées ont écrit dans le coffre ouvert, du plus récent au plus ancien.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasJournal))]
    public partial IReadOnlyList<ExtensionJournalRow> Journal { get; private set; } = [];

    /// <summary>Une extension a écrit dans ce coffre.</summary>
    public bool HasJournal => Journal.Count > 0;

    /// <summary>Une extension vient de s'installer, de s'activer, de s'arrêter ou d'être retirée.</summary>
    public event EventHandler<ExtensionChangedEventArgs>? Changed;

    /// <summary>Les extensions installées.</summary>
    public ObservableCollection<ExtensionRow> Rows { get; } = [];

    /// <summary>Aucune extension n'est installée.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Une installation est en cours.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Refus de la dernière action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>La dernière action a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Une installation attend d'être confirmée.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingInstall { get; private set; }

    /// <summary>Le titre de cette confirmation.</summary>
    [ObservableProperty]
    public partial string InstallTitle { get; private set; } = ModulesSettingsText.ExtensionInstallTitle;

    /// <summary>Le bouton de cette confirmation.</summary>
    [ObservableProperty]
    public partial string InstallAction { get; private set; } = ModulesSettingsText.ExtensionInstall;

    /// <summary>Son nom, sa version, son auteur, et ses fichiers ou la version qu'elle remplace.</summary>
    [ObservableProperty]
    public partial string? InstallSummary { get; private set; }

    /// <summary>Ce qu'elle ajoute à l'interface.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallHasAdds))]
    public partial IReadOnlyList<string> InstallAdds { get; private set; } = [];

    /// <summary>Elle ajoute quelque chose.</summary>
    public bool InstallHasAdds => InstallAdds.Count > 0;

    /// <summary>Ce qu'elle pourra faire, une ligne par permission.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallHasNoRights))]
    public partial IReadOnlyList<ExtensionRightRow> InstallRights { get; private set; } = [];

    /// <summary>Elle ne demande aucune permission.</summary>
    public bool InstallHasNoRights => InstallRights.Count == 0;

    /// <summary>Elle pourra modifier les fiches : une alerte rouge, et le bouton se fait celui d'une action qui compte.</summary>
    [ObservableProperty]
    public partial bool InstallHasHighRisk { get; private set; }

    /// <summary>Une mise à jour demande des permissions nouvelles.</summary>
    [ObservableProperty]
    public partial bool InstallHasNewRights { get; private set; }

    /// <summary>Ce qu'elle ne pourra jamais faire.</summary>
    [ObservableProperty]
    public partial string? InstallLimits { get; private set; }

    /// <summary>Un retrait attend d'être confirmé.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingRemove { get; private set; }

    /// <summary>Ce que devient l'extension à retirer.</summary>
    [ObservableProperty]
    public partial string? RemoveDetails { get; private set; }

    /// <summary>Ce qu'une extension ajoute à l'interface : ses blocs, sa vue, ses commandes, ses entrées au clic droit, son encart.</summary>
    internal static IReadOnlyList<string> AddsOf(ExtensionManifest manifest) =>
        [
            .. manifest.Blocks.Select(ModulesSettingsText.ExtensionBlockOf),
            .. manifest.ViewTitle is { } view ? [ModulesSettingsText.ExtensionViewOf(view)] : Array.Empty<string>(),
            .. manifest.Commands.Length switch
            {
                0 => Array.Empty<string>(),
                1 => [ModulesSettingsText.ExtensionCommandOf(manifest.Commands[0].Title)],
                var count => [ModulesSettingsText.ExtensionCommandsOf(count)],
            },
            .. manifest.Menus.Select(menu => menu.Zone == ExtensionMenuZone.Sheet ? ModulesSettingsText.ExtensionSheetMenuOf(menu.Title) : ModulesSettingsText.ExtensionFolderMenuOf(menu.Title)),
            .. manifest.CardTitle is { } card ? [ModulesSettingsText.ExtensionCardOf(card)] : Array.Empty<string>(),
        ];

    /// <summary>Relit les extensions installées, et leur journal, en arrivant sur l'onglet.</summary>
    public void Reload()
    {
        Error = null;
        Message = null;
        Show();
        ReadJournal();
    }

    /// <summary>Relit le journal des extensions installées : sans coffre ouvert, il est vide.</summary>
    private void ReadJournal()
    {
        var entries = new List<ExtensionJournalRow>();
        foreach (var extension in _library.Installed())
        {
            if (_journal(extension.Manifest.Id) is { Succeeded: true } read)
            {
                entries.AddRange(read.Value.Select(entry => new ExtensionJournalRow(extension.Manifest.Name, entry)));
            }
        }
        Journal = [.. entries.OrderByDescending(row => row.At).Take(JournalShown)];
    }

    private void Prepare(ExtensionPreview preview)
    {
        var manifest = preview.Manifest;
        var summary = manifest.Author is { } author
            ? ModulesSettingsText.ExtensionSummaryByOf(manifest.Name, manifest.Version, author)
            : ModulesSettingsText.ExtensionSummaryOf(manifest.Name, manifest.Version);
        InstallSummary = $"{summary}. " + (preview.InstalledVersion is { } installed
            ? ModulesSettingsText.ExtensionReplacesOf(installed)
            : ModulesSettingsText.ExtensionFilesOf(preview.FileCount, DataSettingsViewModel.SizeOf(preview.Size)));
        InstallAdds = [.. AddsOf(manifest).Select(add => char.ToUpper(add[0], CultureInfo.CurrentCulture) + add[1..])];
        var withdrawn = preview.InstalledVersion is null ? [] : _library.Withdrawn(manifest.Id);
        InstallRights = [.. manifest.Permissions.Select(right => new ExtensionRightRow(right, !withdrawn.Contains(right), preview.NewRights.Contains(right), null))];
        InstallHasHighRisk = InstallRights.Any(right => right.Risk == ExtensionRisk.High && right.IsGranted);
        InstallHasNewRights = preview.NewRights.Count > 0;
        InstallLimits = InstallRights.Any(right => right.Right == ExtensionRight.WriteSheets && right.IsGranted)
            ? ModulesSettingsText.ExtensionLimitsWriting
            : ModulesSettingsText.ExtensionLimits;
    }

    private void Show()
    {
        Rows.Clear();
        foreach (var extension in _library.Installed())
        {
            Rows.Add(new ExtensionRow(extension, _library.Withdrawn(extension.Manifest.Id), Toggle, Grant));
        }
        IsEmpty = Rows.Count == 0;
    }

    private void Toggle(ExtensionRow row, bool active)
    {
        Error = null;
        Message = null;
        var outcome = _library.SetActive(row.Id, active);
        if (outcome.Succeeded)
        {
            Message = active ? ModulesSettingsText.ExtensionEnabledOf(row.Name) : ModulesSettingsText.ExtensionDisabledOf(row.Name);
            Changed?.Invoke(this, new ExtensionChangedEventArgs(row.Id));
            return;
        }
        Error = outcome.Error;
        row.Show(!active);
    }

    private void Grant(ExtensionRow row, ExtensionRightRow right, bool granted)
    {
        Error = null;
        Message = null;
        var outcome = _library.SetGranted(row.Id, right.Right, granted);
        if (outcome.Succeeded)
        {
            Message = granted ? ModulesSettingsText.ExtensionRightGrantedOf(row.Name, right.Text) : ModulesSettingsText.ExtensionRightWithdrawnOf(row.Name, right.Text);
            return;
        }
        Error = outcome.Error;
        right.Show(!granted);
    }

    private bool CanInstall() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        Error = null;
        Message = null;
        if (_picker.Pick(ModulesSettingsText.ExtensionPick) is not { } source)
        {
            return;
        }
        var preview = await Task.Run(() => _library.Read(source)).ConfigureAwait(true);
        if (!preview.Succeeded)
        {
            Error = preview.Error;
            return;
        }
        var updates = preview.Value.InstalledVersion is not null;
        _source = source;
        InstallTitle = updates ? ModulesSettingsText.ExtensionUpdateTitle : ModulesSettingsText.ExtensionInstallTitle;
        InstallAction = updates ? ModulesSettingsText.ExtensionUpdate : ModulesSettingsText.ExtensionInstall;
        Prepare(preview.Value);
        IsConfirmingInstall = true;
    }

    [RelayCommand]
    private async Task AnswerInstallAsync(bool confirmed)
    {
        IsConfirmingInstall = false;
        if (!confirmed || _source is not { } source)
        {
            return;
        }
        IsBusy = true;
        var outcome = await Task.Run(() => _library.Install(source)).ConfigureAwait(true);
        IsBusy = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Message = ModulesSettingsText.ExtensionInstalledOf(outcome.Value.Manifest.Name, outcome.Value.Manifest.Version);
        Show();
        Changed?.Invoke(this, new ExtensionChangedEventArgs(outcome.Value.Manifest.Id));
    }

    [RelayCommand]
    private void RequestRemove(ExtensionRow row)
    {
        Error = null;
        Message = null;
        _toRemove = row;
        RemoveDetails = ModulesSettingsText.ExtensionRemoveDetailsOf(row.Name);
        IsConfirmingRemove = true;
    }

    [RelayCommand]
    private void AnswerRemove(bool confirmed)
    {
        IsConfirmingRemove = false;
        if (!confirmed || _toRemove is not { } row)
        {
            return;
        }
        var outcome = _library.Retire(row.Id);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        Message = ModulesSettingsText.ExtensionRemovedOf(row.Name);
        Show();
        Changed?.Invoke(this, new ExtensionChangedEventArgs(row.Id));
    }
}
