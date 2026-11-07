using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.DesignSystem.Controls;
using KyfRev.Extensions.Host;
using KyfRev.Presentation.Wpf.Extensions;
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

    /// <summary>Elle se coche ou se décoche : seulement pour une extension active.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; set; } = true;

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

/// <summary>Une extension du catalogue, dans la carte « Extensions » : ce qu'elle est, ce qu'elle demande, et sa version installée.</summary>
public sealed partial class CatalogueRow : ObservableObject
{
    private readonly ExtensionSettingsViewModel _card;

    internal CatalogueRow(CatalogueEntry entry, ExtensionSettingsViewModel card)
    {
        Entry = entry;
        _card = card;
    }

    /// <summary>L'extension du catalogue.</summary>
    public CatalogueEntry Entry { get; }

    /// <summary>Son nom.</summary>
    public string Name => Entry.Name;

    /// <summary>Ce qu'elle fait.</summary>
    public string Summary => Entry.Summary;

    /// <summary>Elle dit ce qu'elle fait.</summary>
    public bool HasSummary => Entry.Summary.Length > 0;

    /// <summary>Ce qu'elle demande, en clair.</summary>
    public string Permissions => Entry.Permissions.IsEmpty
        ? ModulesSettingsText.CatalogueNoPermission
        : ModulesSettingsText.CataloguePermissionsOf(string.Join(", ", Entry.Permissions.Select(name => ExtensionRights.Parse(name, 2) is { } right ? ExtensionRightRow.TextOf(right) : name)));

    /// <summary>Sa version installée sur ce poste ; rien si elle ne l'est pas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Version), nameof(Action), nameof(IsUpToDate), nameof(CanInstall))]
    public partial string? InstalledVersion { get; set; }

    /// <summary>Sa version, et celle installée si elle diffère.</summary>
    public string Version => InstalledVersion is { } installed && installed != Entry.Version
        ? ModulesSettingsText.CatalogueInstalledVersionOf(Entry.Version, installed)
        : ModulesSettingsText.CatalogueVersionOf(Entry.Version);

    /// <summary>Ce que fait son bouton.</summary>
    public string Action => InstalledVersion is null ? ModulesSettingsText.CatalogueInstall : ModulesSettingsText.CatalogueUpdate;

    /// <summary>La version du catalogue est installée.</summary>
    public bool IsUpToDate => InstalledVersion == Entry.Version;

    /// <summary>Elle s'installe, ou se met à jour.</summary>
    public bool CanInstall => !IsUpToDate;

    /// <summary>Le nom du bouton, pour un lecteur d'écran.</summary>
    public string Spoken => $"{Action} {Name}";

    /// <summary>La télécharge, vérifiée, et montre ce qu'elle demande avant de l'installer.</summary>
    [RelayCommand]
    private Task InstallAsync() => _card.InstallFromCatalogueAsync(this);
}

/// <summary>Une extension installée, dans la carte « Extensions ».</summary>
/// <summary>Une erreur d'une extension, en mode développeur : où, ce qu'elle a dit, et quand.</summary>
public sealed class ExtensionFaultRow
{
    internal ExtensionFaultRow(ExtensionFault fault)
    {
        Text = ModulesSettingsText.ExtensionFaultOf(fault.Where, fault.Message);
        When = fault.At.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    /// <summary>Où, et ce qu'elle a dit.</summary>
    public string Text { get; }

    /// <summary>À quelle heure.</summary>
    public string When { get; }
}

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
    private readonly HashSet<ExtensionRight> _withdrawn;
    private bool _showing;

    internal ExtensionRow(InstalledExtension extension, IReadOnlyList<ExtensionRight> withdrawn, Action<ExtensionRow, bool> changed, Action<ExtensionRow, ExtensionRightRow, bool> granted)
    {
        var manifest = extension.Manifest;
        _changed = changed;
        _withdrawn = [.. withdrawn];
        Manifest = manifest;
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

    /// <summary>Son manifeste : ce que redit la confirmation quand on la réactive.</summary>
    internal ExtensionManifest Manifest { get; }

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
    [NotifyPropertyChangedFor(nameof(IsInactive))]
    public partial bool IsActive { get; set; }

    /// <summary>Désactivée, elle n'a plus aucune permission : la carte le dit.</summary>
    public bool IsInactive => !IsActive;

    /// <summary>Remontre l'état enregistré, sans le changer : désactivée, ses permissions se montrent décochées, et ne se cochent pas.</summary>
    internal void Show(bool active)
    {
        _showing = true;
        IsActive = active;
        _showing = false;
        foreach (var right in Rights)
        {
            right.IsAvailable = active;
            right.Show(active && !_withdrawn.Contains(right.Right));
        }
    }

    /// <summary>Une permission vient d'être retirée ou rendue.</summary>
    internal void Remember(ExtensionRight right, bool granted)
    {
        if (granted)
        {
            _withdrawn.Remove(right);
        }
        else
        {
            _withdrawn.Add(right);
        }
    }

    /// <summary>Réactivée, elle a de nouveau tout ce qu'elle demande : la confirmation vient de le montrer.</summary>
    internal void GrantAll() => _withdrawn.Clear();

    partial void OnIsActiveChanged(bool value)
    {
        if (!_showing)
        {
            _changed(this, value);
        }
    }

    /// <summary>Le dossier d'où elle a été installée ; rien s'il n'est pas connu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText))]
    public partial string? Source { get; internal set; }

    /// <summary>D'où elle vient, à lire en mode développeur.</summary>
    public string SourceText => Source is { } source ? ModulesSettingsText.ExtensionSourceOf(source) : ModulesSettingsText.ExtensionSourceUnknown;

    /// <summary>Le mode développeur est allumé : son dossier, « Recharger » et ses erreurs se montrent.</summary>
    [ObservableProperty]
    public partial bool IsDeveloper { get; internal set; }

    /// <summary>Ses dernières erreurs, de la plus récente à la plus ancienne.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFaults), nameof(HasNoFaults))]
    public partial IReadOnlyList<ExtensionFaultRow> Faults { get; internal set; } = [];

    /// <summary>Elle a eu des erreurs depuis le lancement.</summary>
    public bool HasFaults => Faults.Count > 0;

    /// <summary>Elle n'en a pas eu.</summary>
    public bool HasNoFaults => Faults.Count == 0;

    /// <summary>Le nom du bouton qui la recharge.</summary>
    public string ReloadName => ModulesSettingsText.ExtensionReloadNameOf(Name);
}

/// <summary>
/// La carte « Extensions » de l'onglet « Modules et extensions » : installer depuis un dossier, activer, retirer. Une extension est
/// installée sur cet ordinateur, pas dans le coffre ; rien ne s'installe sans avoir montré ce qu'elle ajoute et ce qu'elle demande, et
/// une permission se retire à tout moment.
/// </summary>
public sealed partial class ExtensionSettingsViewModel : ObservableObject, IForgetsVault
{
    /// <summary>Les écritures montrées sous la liste : les plus récentes.</summary>
    public const int JournalShown = 30;

    private readonly ExtensionLibrary _library;
    private readonly IFolderPicker _picker;
    private readonly Func<string, Outcome<IReadOnlyList<ExtensionJournalEntry>>> _journal;
    private readonly IExtensionFaults _faults;
    private string? _source;
    private string? _downloaded;
    private bool _created;
    private readonly bool _starting;
    private ExtensionRow? _toRemove;
    private ExtensionRow? _toReactivate;

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
        : this(library, picker, journal, NoFaults.Instance)
    {
    }

    /// <summary>La carte de cette bibliothèque, avec le journal des écritures et les erreurs des extensions, pour le mode développeur.</summary>
    /// <param name="library">Les extensions installées.</param>
    /// <param name="picker">Le choix du dossier à installer, ou de celui où créer une extension.</param>
    /// <param name="journal">Le journal d'une extension dans le coffre ouvert, du plus récent au plus ancien.</param>
    /// <param name="faults">Les dernières erreurs de chaque extension.</param>
    public ExtensionSettingsViewModel(ExtensionLibrary library, IFolderPicker picker, Func<string, Outcome<IReadOnlyList<ExtensionJournalEntry>>> journal, IExtensionFaults faults)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(faults);
        _library = library;
        _picker = picker;
        _journal = journal;
        _faults = faults;
        faults.FaultsChanged += (_, e) =>
        {
            if (Rows.FirstOrDefault(row => row.Id == e.Extension) is { } row)
            {
                ShowFaults(row);
            }
        };
        _starting = true;
        IsDeveloperMode = library.IsDeveloperMode;
        _starting = false;
    }

    /// <summary>
    /// Le mode développeur, retenu sur ce poste : chaque extension montre le dossier d'où elle vient, « Recharger » pour reprendre ses
    /// fichiers, et ses dernières erreurs.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDeveloperMode { get; set; }

    partial void OnIsDeveloperModeChanged(bool value)
    {
        foreach (var row in Rows)
        {
            row.IsDeveloper = value;
        }
        if (!_starting && _library.SetDeveloperMode(value) is { Succeeded: false } refused)
        {
            Error = refused.Error;
        }
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

    /// <summary>Le catalogue en ligne ; rien sans lecteur en ligne.</summary>
    public CatalogueReader? Catalogue { get; init; }

    /// <summary>Le catalogue peut se lire.</summary>
    public bool CanReadCatalogue => Catalogue is not null;

    /// <summary>Ce qu'on sait du catalogue lu ; rien tant qu'il n'a pas été demandé.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCatalogue))]
    public partial string? CatalogueStatus { get; private set; }

    /// <summary>Le catalogue a été demandé : sa partie se montre.</summary>
    public bool HasCatalogue => CatalogueStatus is not null;

    /// <summary>Les extensions du catalogue.</summary>
    public ObservableCollection<CatalogueRow> CatalogueRows { get; } = [];

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

    /// <summary>
    /// Oublie ce qui vient du coffre : le journal, qui nomme ses fiches, et les erreurs montrées sous chaque extension, qui peuvent
    /// en citer. Les extensions installées restent : elles sont sur ce poste. Le prochain passage sur l'onglet relit le journal.
    /// </summary>
    public void Forget()
    {
        Journal = [];
        foreach (var row in Rows)
        {
            row.Faults = [];
        }
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

    private void Prepare(ExtensionPreview preview) =>
        Prepare(
            preview.Manifest,
            preview.InstalledVersion is { } installed
                ? ModulesSettingsText.ExtensionReplacesOf(installed)
                : ModulesSettingsText.ExtensionFilesOf(preview.FileCount, DataSettingsViewModel.SizeOf(preview.Size)),
            preview.InstalledVersion is null ? [] : _library.Withdrawn(preview.Manifest.Id),
            preview.NewRights);

    /// <summary>Ce que montre la confirmation : ce que l'extension ajoute, et chaque permission qu'elle demande, avec son poids.</summary>
    private void Prepare(ExtensionManifest manifest, string? detail, IReadOnlyList<ExtensionRight> withdrawn, IReadOnlyList<ExtensionRight> newRights)
    {
        var summary = manifest.Author is { } author
            ? ModulesSettingsText.ExtensionSummaryByOf(manifest.Name, manifest.Version, author)
            : ModulesSettingsText.ExtensionSummaryOf(manifest.Name, manifest.Version);
        InstallSummary = detail is null ? $"{summary}." : $"{summary}. {detail}";
        InstallAdds = [.. AddsOf(manifest).Select(add => char.ToUpper(add[0], CultureInfo.CurrentCulture) + add[1..])];
        InstallRights = [.. manifest.Permissions.Select(right => new ExtensionRightRow(right, !withdrawn.Contains(right), newRights.Contains(right), null))];
        InstallHasHighRisk = InstallRights.Any(right => right.Risk == ExtensionRisk.High && right.IsGranted);
        InstallHasNewRights = newRights.Count > 0;
        InstallLimits = InstallRights.Any(right => right.Right == ExtensionRight.WriteSheets && right.IsGranted)
            ? ModulesSettingsText.ExtensionLimitsWriting
            : ModulesSettingsText.ExtensionLimits;
    }

    private void Show()
    {
        Rows.Clear();
        foreach (var extension in _library.Installed())
        {
            var row = new ExtensionRow(extension, _library.Withdrawn(extension.Manifest.Id), Toggle, Grant);
            row.Source = _library.Source(row.Id);
            row.IsDeveloper = IsDeveloperMode;
            ShowFaults(row);
            Rows.Add(row);
        }
        IsEmpty = Rows.Count == 0;
    }

    private void ShowFaults(ExtensionRow row) => row.Faults = [.. _faults.Faults(row.Id).Select(fault => new ExtensionFaultRow(fault))];

    /// <summary>
    /// Désactivée, une extension perd ses permissions. La réactiver repose la question : la confirmation montre de nouveau ce qu'elle
    /// demande, et rien ne change avant la réponse.
    /// </summary>
    private void Toggle(ExtensionRow row, bool active)
    {
        Error = null;
        Message = null;
        if (active && row.HasRights)
        {
            row.Show(false);
            _source = null;
            _toReactivate = row;
            InstallTitle = ModulesSettingsText.ExtensionReactivateTitle;
            InstallAction = ModulesSettingsText.ExtensionReactivate;
            Prepare(row.Manifest, null, [], []);
            IsConfirmingInstall = true;
            return;
        }
        var outcome = _library.SetActive(row.Id, active);
        if (outcome.Succeeded)
        {
            Message = active ? ModulesSettingsText.ExtensionEnabledOf(row.Name) : ModulesSettingsText.ExtensionDisabledOf(row.Name);
            row.Show(active);
            Changed?.Invoke(this, new ExtensionChangedEventArgs(row.Id));
            return;
        }
        Error = outcome.Error;
        row.Show(!active);
    }

    /// <summary>La réactivation confirmée : active, et avec tout ce qu'elle demande.</summary>
    private void Reactivate(ExtensionRow row)
    {
        var outcome = _library.SetActive(row.Id, true);
        foreach (var right in _library.Withdrawn(row.Id))
        {
            outcome = outcome.Succeeded ? _library.SetGranted(row.Id, right, true) : outcome;
        }
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        row.GrantAll();
        row.Show(true);
        Message = ModulesSettingsText.ExtensionReactivatedOf(row.Name);
        Changed?.Invoke(this, new ExtensionChangedEventArgs(row.Id));
    }

    private void Grant(ExtensionRow row, ExtensionRightRow right, bool granted)
    {
        Error = null;
        Message = null;
        var outcome = _library.SetGranted(row.Id, right.Right, granted);
        if (outcome.Succeeded)
        {
            row.Remember(right.Right, granted);
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
        _toReactivate = null;
        InstallTitle = updates ? ModulesSettingsText.ExtensionUpdateTitle : ModulesSettingsText.ExtensionInstallTitle;
        InstallAction = updates ? ModulesSettingsText.ExtensionUpdate : ModulesSettingsText.ExtensionInstall;
        Prepare(preview.Value);
        IsConfirmingInstall = true;
    }

    /// <summary>Lit le catalogue en ligne : il n'est montré que vérifié.</summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task ReadCatalogueAsync()
    {
        if (Catalogue is not { } catalogue)
        {
            return;
        }
        Error = null;
        Message = null;
        IsBusy = true;
        CatalogueStatus = ModulesSettingsText.CatalogueReading;
        var read = await Task.Run(() => catalogue.ReadAsync(CancellationToken.None)).ConfigureAwait(true);
        IsBusy = false;
        CatalogueRows.Clear();
        if (!read.Succeeded)
        {
            CatalogueStatus = null;
            Error = read.Error;
            return;
        }
        var value = read.Value;
        CatalogueStatus = value.Extensions.IsEmpty
            ? ModulesSettingsText.CatalogueEmpty
            : ModulesSettingsText.CatalogueStatusOf(value.Number.ToString(CultureInfo.CurrentCulture), value.PublishedOn.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")), value.KeyId);
        foreach (var entry in value.Extensions)
        {
            CatalogueRows.Add(new CatalogueRow(entry, this));
        }
        RefreshCatalogue();
    }

    /// <summary>La version installée de chaque extension du catalogue.</summary>
    private void RefreshCatalogue()
    {
        var installed = _library.Installed().ToDictionary(extension => extension.Manifest.Id, extension => extension.Manifest.Version, StringComparer.Ordinal);
        foreach (var row in CatalogueRows)
        {
            row.InstalledVersion = installed.GetValueOrDefault(row.Entry.Id);
        }
    }

    /// <summary>Télécharge une extension du catalogue, chaque fichier vérifié, puis montre ce qu'elle demande avant de l'installer.</summary>
    internal async Task InstallFromCatalogueAsync(CatalogueRow row)
    {
        if (Catalogue is not { } catalogue || IsBusy)
        {
            return;
        }
        Error = null;
        Message = ModulesSettingsText.CatalogueDownloadingOf(row.Entry.Name);
        IsBusy = true;
        var downloaded = await Task.Run(() => catalogue.DownloadAsync(row.Entry, CancellationToken.None)).ConfigureAwait(true);
        IsBusy = false;
        Message = null;
        if (!downloaded.Succeeded)
        {
            Error = downloaded.Error;
            return;
        }
        var preview = await Task.Run(() => _library.Read(downloaded.Value)).ConfigureAwait(true);
        if (!preview.Succeeded)
        {
            Catalogue?.Discard(downloaded.Value);
            Error = preview.Error;
            return;
        }
        var updates = preview.Value.InstalledVersion is not null;
        _source = downloaded.Value;
        _downloaded = downloaded.Value;
        _toReactivate = null;
        InstallTitle = updates ? ModulesSettingsText.ExtensionUpdateTitle : ModulesSettingsText.ExtensionInstallTitle;
        InstallAction = updates ? ModulesSettingsText.ExtensionUpdate : ModulesSettingsText.ExtensionInstall;
        Prepare(preview.Value);
        IsConfirmingInstall = true;
    }

    [RelayCommand]
    private async Task AnswerInstallAsync(bool confirmed)
    {
        IsConfirmingInstall = false;
        var downloaded = _downloaded;
        _downloaded = null;
        try
        {
            await AnswerAsync(confirmed, downloaded).ConfigureAwait(true);
        }
        finally
        {
            if (downloaded is not null)
            {
                Catalogue?.Discard(downloaded);
                RefreshCatalogue();
            }
        }
    }

    private async Task AnswerAsync(bool confirmed, string? downloaded)
    {
        if (_toReactivate is { } row)
        {
            _toReactivate = null;
            if (confirmed)
            {
                Reactivate(row);
            }
            return;
        }
        var created = _created;
        _created = false;
        if (!confirmed || _source is not { } source)
        {
            return;
        }
        IsBusy = true;
        var outcome = await Task.Run(() => downloaded is null ? _library.Install(source) : _library.InstallDownloaded(source)).ConfigureAwait(true);
        IsBusy = false;
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        var manifest = outcome.Value.Manifest;
        _faults.ForgetFaults(manifest.Id);
        if (created)
        {
            IsDeveloperMode = true;
        }
        Message = created ? ModulesSettingsText.ExtensionCreatedOf(manifest.Name, source) : ModulesSettingsText.ExtensionInstalledOf(manifest.Name, manifest.Version);
        Show();
        Changed?.Invoke(this, new ExtensionChangedEventArgs(manifest.Id));
    }

    /// <summary>« Créer une extension… » : un dossier prêt à modifier, dans le dossier choisi, puis son installation, confirmée.</summary>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task CreateAsync()
    {
        Error = null;
        Message = null;
        if (_picker.Pick(ModulesSettingsText.ExtensionCreatePick) is not { } parent)
        {
            return;
        }
        var taken = _library.Installed().Select(extension => extension.Manifest.Id).ToList();
        var created = await Task.Run(() => ExtensionTemplate.Create(parent, taken)).ConfigureAwait(true);
        if (!created.Succeeded)
        {
            Error = created.Error;
            return;
        }
        var preview = await Task.Run(() => _library.Read(created.Value)).ConfigureAwait(true);
        if (!preview.Succeeded)
        {
            Error = preview.Error;
            return;
        }
        _source = created.Value;
        _created = true;
        InstallTitle = ModulesSettingsText.ExtensionCreateTitle;
        InstallAction = ModulesSettingsText.ExtensionInstall;
        Prepare(preview.Value);
        IsConfirmingInstall = true;
    }

    /// <summary>
    /// Mode développeur : reprend les fichiers d'une extension dans le dossier d'où elle a été installée. Une permission nouvelle
    /// passe par la confirmation ; sinon, l'extension repart tout de suite, ses erreurs oubliées.
    /// </summary>
    [RelayCommand]
    private async Task ReloadAsync(ExtensionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Error = null;
        Message = null;
        if (_library.Source(row.Id) is not { } source || !System.IO.Directory.Exists(source))
        {
            Error = ModulesSettingsText.ExtensionSourceMissingOf(row.Name);
            return;
        }
        var preview = await Task.Run(() => _library.Read(source)).ConfigureAwait(true);
        if (!preview.Succeeded)
        {
            Error = preview.Error;
            return;
        }
        if (preview.Value.Manifest.Id != row.Id)
        {
            Error = ModulesSettingsText.ExtensionIdChangedOf(preview.Value.Manifest.Id);
            return;
        }
        _source = source;
        if (preview.Value.NewRights.Count > 0)
        {
            InstallTitle = ModulesSettingsText.ExtensionUpdateTitle;
            InstallAction = ModulesSettingsText.ExtensionUpdate;
            Prepare(preview.Value);
            IsConfirmingInstall = true;
            return;
        }
        var outcome = await Task.Run(() => _library.Install(source)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        _faults.ForgetFaults(row.Id);
        Message = ModulesSettingsText.ExtensionReloadedOf(outcome.Value.Manifest.Name, outcome.Value.Manifest.Version);
        Show();
        Changed?.Invoke(this, new ExtensionChangedEventArgs(row.Id));
    }

    /// <summary>Pas d'erreurs à montrer : la carte sans moteur d'extensions.</summary>
    private sealed class NoFaults : IExtensionFaults
    {
        public static NoFaults Instance { get; } = new();

        public event EventHandler<ExtensionEventArgs>? FaultsChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ExtensionFault> Faults(string id) => [];

        public void ForgetFaults(string id)
        {
        }
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
