using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

/// <summary>Une extension installée, dans la carte « Extensions ».</summary>
public sealed partial class ExtensionRow : ObservableObject
{
    private readonly Action<ExtensionRow, bool> _changed;
    private bool _showing;

    internal ExtensionRow(InstalledExtension extension, Action<ExtensionRow, bool> changed)
    {
        var manifest = extension.Manifest;
        _changed = changed;
        Id = manifest.Id;
        Name = manifest.Name;
        Version = manifest.Author is { } author ? ModulesSettingsText.ExtensionVersionByOf(manifest.Version, author) : manifest.Version;
        Description = manifest.Description;
        Adds = ModulesSettingsText.ExtensionAddsOf(ExtensionSettingsViewModel.AddsOf(manifest));
        Permissions = manifest.Permissions.IsEmpty ? string.Empty : ModulesSettingsText.ExtensionMayOf(ExtensionSettingsViewModel.PermissionsOf(manifest));
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

    /// <summary>Les blocs et la vue qu'elle ajoute.</summary>
    public string Adds { get; }

    /// <summary>Ce qu'elle a le droit de faire.</summary>
    public string Permissions { get; }

    /// <summary>Elle demande des permissions.</summary>
    public bool HasPermissions => Permissions.Length > 0;

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
/// installée sur cet ordinateur, pas dans le coffre ; rien ne s'installe sans avoir montré ce qu'elle demande.
/// </summary>
public sealed partial class ExtensionSettingsViewModel : ObservableObject
{
    private readonly ExtensionLibrary _library;
    private readonly IFolderPicker _picker;
    private string? _source;
    private ExtensionRow? _toRemove;

    /// <summary>La carte de cette bibliothèque.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="picker">Le choix du dossier à installer.</param>
    public ExtensionSettingsViewModel(ExtensionLibrary library, IFolderPicker picker)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(picker);
        _library = library;
        _picker = picker;
    }

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

    /// <summary>Ce que l'extension ajoute et ce qu'elle demande.</summary>
    [ObservableProperty]
    public partial string? InstallDetails { get; private set; }

    /// <summary>Un retrait attend d'être confirmé.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingRemove { get; private set; }

    /// <summary>Ce que devient l'extension à retirer.</summary>
    [ObservableProperty]
    public partial string? RemoveDetails { get; private set; }

    /// <summary>Les blocs et la vue qu'ajoute une extension.</summary>
    internal static string AddsOf(ExtensionManifest manifest) =>
        string.Join(", ", manifest.Blocks.Select(ModulesSettingsText.ExtensionBlockOf).Concat(manifest.ViewTitle is { } view ? [ModulesSettingsText.ExtensionViewOf(view)] : []));

    /// <summary>Ce qu'une extension a le droit de faire, en clair.</summary>
    internal static string PermissionsOf(ExtensionManifest manifest) =>
        string.Join(ModulesSettingsText.ExtensionAnd, manifest.Permissions.Select(permission => permission == ExtensionRight.ReadSheets
            ? ModulesSettingsText.ExtensionReadSheets
            : ModulesSettingsText.ExtensionStorage));

    /// <summary>Relit les extensions installées, en arrivant sur l'onglet.</summary>
    public void Reload()
    {
        Error = null;
        Message = null;
        Show();
    }

    private static string DetailsOf(ExtensionPreview preview)
    {
        var manifest = preview.Manifest;
        return string.Join(
            ' ',
            manifest.Author is { } author
                ? ModulesSettingsText.ExtensionSummaryByOf(manifest.Name, manifest.Version, author, AddsOf(manifest))
                : ModulesSettingsText.ExtensionSummaryOf(manifest.Name, manifest.Version, AddsOf(manifest)),
            manifest.Permissions.IsEmpty ? ModulesSettingsText.ExtensionNoPermission : ModulesSettingsText.ExtensionWillOf(PermissionsOf(manifest)),
            ModulesSettingsText.ExtensionLimits,
            preview.InstalledVersion is { } installed
                ? ModulesSettingsText.ExtensionReplacesOf(installed)
                : ModulesSettingsText.ExtensionFilesOf(preview.FileCount, DataSettingsViewModel.SizeOf(preview.Size)),
            ModulesSettingsText.ExtensionTrust);
    }

    private void Show()
    {
        Rows.Clear();
        foreach (var extension in _library.Installed())
        {
            Rows.Add(new ExtensionRow(extension, Toggle));
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
        InstallDetails = DetailsOf(preview.Value);
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
