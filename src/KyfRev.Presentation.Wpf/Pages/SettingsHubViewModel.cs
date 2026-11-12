using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Une section de la vue « Paramètres ».</summary>
public sealed partial class SettingsTab : ObservableObject
{
    internal SettingsTab(string label, object page, string group = "", string glyph = "", string part = "", string description = "")
    {
        Label = label;
        Page = page;
        Group = group;
        Glyph = glyph;
        Part = part;
        Description = description;
    }

    /// <summary>« Apparence », « Révisions »….</summary>
    public string Label { get; }

    /// <summary>Le contenu de la section : une page, qui peut en servir plusieurs.</summary>
    public object Page { get; }

    /// <summary>Le groupe où elle est rangée : « Sur cet ordinateur », « Dans ce coffre », « kyfRev ».</summary>
    public string Group { get; }

    /// <summary>Son icône.</summary>
    public string Glyph { get; }

    /// <summary>Ce que sa page doit montrer : les cartes de cette section seulement ; vide, la page entière.</summary>
    public string Part { get; }

    /// <summary>Une ligne qui dit ce qu'on y règle.</summary>
    public string Description { get; }

    /// <summary>Elle est la première de son groupe : le nom du groupe se lit au-dessus d'elle.</summary>
    public bool StartsGroup { get; internal set; }

    /// <summary>Les réglages que la recherche y trouve.</summary>
    internal IReadOnlyList<SettingsEntry> Settings { get; init; } = [];

    /// <summary>
    /// Pour une section retenue sur l'ordinateur : ses réglages, pris dans les premières préférences et posés dans les
    /// secondes. Rien pour une section qui ne se rétablit pas.
    /// </summary>
    internal Func<MachinePreferences, MachinePreferences, MachinePreferences>? Take { get; init; }

    /// <summary>La section est celle que l'on voit.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>Un réglage que la recherche peut trouver.</summary>
/// <param name="Label">Son nom, comme il se lit dans sa section.</param>
/// <param name="Words">D'autres mots qui y mènent : ses valeurs, un synonyme.</param>
internal sealed record SettingsEntry(string Label, string Words = "")
{
    /// <summary>Un nom seul suffit le plus souvent.</summary>
    public static implicit operator SettingsEntry(string label) => new(label);
}

/// <summary>Ce que la recherche a trouvé : un réglage, ou une section entière.</summary>
public sealed class SettingsHit
{
    internal SettingsHit(string label, string where, SettingsTab tab)
    {
        Label = label;
        Where = where;
        Tab = tab;
    }

    /// <summary>Le réglage, ou la section.</summary>
    public string Label { get; }

    /// <summary>Où il est : sa section ; pour une section, son groupe.</summary>
    public string Where { get; }

    /// <summary>La section qu'il ouvre.</summary>
    public SettingsTab Tab { get; }

    /// <summary>Ce que dit le lecteur d'écran.</summary>
    public string Spoken => MoreSettingsText.HitSpoken(Label, Where);

    /// <inheritdoc/>
    public override string ToString() => Spoken;
}

/// <summary>
/// La vue « Paramètres » : ses sections dans une liste à gauche, rangées par portée (ce qui est retenu sur cet ordinateur, ce
/// qui appartient au coffre ouvert, l'application), et une seule section à la fois à droite.
/// </summary>
public sealed partial class SettingsHubViewModel : ObservableObject, INavigationAware
{
    /// <summary>La part « Apparence » de la page générale.</summary>
    public const string AppearancePart = "apparence";

    /// <summary>La part « Lecture » de la page générale.</summary>
    public const string ReadingPart = "lecture";

    /// <summary>La part « Menu » de la page des raccourcis.</summary>
    public const string MenuPart = "menu";

    /// <summary>La part « Raccourcis » de la page des raccourcis.</summary>
    public const string ShortcutsPart = "raccourcis";

    /// <summary>La part « Coffre et chiffrement » de la page des données.</summary>
    public const string VaultPart = "coffre";

    /// <summary>La part « Sauvegardes et exports » de la page des données.</summary>
    public const string BackupsPart = "sauvegardes";

    private static readonly MachinePreferences Origin = new();

    private readonly List<SettingsTab> _tabs;
    private readonly MachineSettings? _machine;
    private SettingsTab? _restored;
    private MachinePreferences? _before;
    private bool _restoring;

    /// <summary>La vue, avec ses sections, dans l'ordre ; la première s'ouvre.</summary>
    public SettingsHubViewModel(GeneralSettingsViewModel general, SettingsPageViewModel revisions, ModulesSettingsViewModel modules, DataSettingsViewModel data, AboutSettingsViewModel about, SheetsSettingsViewModel? sheets = null, ShortcutsSettingsViewModel? shortcuts = null, MachineSettings? machine = null)
    {
        ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(revisions);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(about);
        const string onMachine = MoreSettingsText.GroupMachine;
        const string vault = MoreSettingsText.GroupVault;
        const string app = MoreSettingsText.GroupApp;
        _tabs =
        [
            new SettingsTab(MoreSettingsText.Appearance, general, onMachine, Icons.Palette, AppearancePart, MoreSettingsText.AppearanceAbout)
            {
                Settings =
                [
                    new(GeneralSettingsText.Theme, $"{GeneralSettingsText.ThemeSystem} {GeneralSettingsText.ThemeLight} {GeneralSettingsText.ThemeDark}"),
                    new(GeneralSettingsText.InterfaceSize, MoreSettingsText.WordsZoom),
                    GeneralSettingsText.ReducedMotion,
                    GeneralSettingsText.HighContrast,
                    GeneralSettingsText.LargerTargets,
                ],
                Take = (from, to) => to with { Theme = from.Theme, Zoom = from.Zoom, ReducedMotion = from.ReducedMotion, HighContrast = from.HighContrast, LargerTargets = from.LargerTargets },
            },
            new SettingsTab(MoreSettingsText.Reading, general, onMachine, Icons.Read, ReadingPart, MoreSettingsText.ReadingAbout)
            {
                Settings =
                [
                    GeneralSettingsText.TextSize,
                    GeneralSettingsText.Spacing,
                    new(GeneralSettingsText.Font, $"{GeneralSettingsText.FontSans} {GeneralSettingsText.FontSerif}"),
                    GeneralSettingsText.DefinedWords,
                    GeneralSettingsText.Voice,
                    GeneralSettingsText.Rate,
                ],
                Take = (from, to) => to with { TextSize = from.TextSize, LineSpacing = from.LineSpacing, Font = from.Font, MarkDefinedWords = from.MarkDefinedWords, Voice = from.Voice, Rate = from.Rate },
            },
            .. sheets is null
                ? []
                : new[]
                {
                    new SettingsTab(MoreSettingsText.Sheets, sheets, onMachine, Icons.Document, string.Empty, MoreSettingsText.SheetsAbout)
                    {
                        Settings =
                        [
                            new(MoreSettingsText.LayoutTitle, MoreSettingsText.WordsLayout),
                            MoreSettingsText.Orientation,
                            MoreSettingsText.Margins,
                            new(MoreSettingsText.PaperTitle, MoreSettingsText.WordsPaper),
                            new(MoreSettingsText.ZoomTitle, MoreSettingsText.WordsZoom),
                            new(MoreSettingsText.FontTitle, MoreSettingsText.WordsFont),
                        ],
                        Take = (from, to) => to with { Page = from.Page, Paper = from.Paper, SheetZoom = from.SheetZoom },
                    },
                },
            .. shortcuts is null
                ? []
                : new[]
                {
                    new SettingsTab(MoreSettingsText.Menu, shortcuts, onMachine, Icons.ListLines, MenuPart, MoreSettingsText.MenuAbout)
                    {
                        Settings = [MoreSettingsText.MenuTitle],
                        Take = (from, to) => to with { MenuOrder = from.MenuOrder },
                    },
                    new SettingsTab(MoreSettingsText.Shortcuts, shortcuts, onMachine, Icons.Keyboard, ShortcutsPart, MoreSettingsText.ShortcutsAbout)
                    {
                        Settings = [MoreSettingsText.ShortcutsTitle, .. ShortcutMap.Actions.Select(action => new SettingsEntry(action.Label)), GeneralSettingsText.GlobalCapture],
                        Take = (from, to) => to with { Shortcuts = from.Shortcuts, GlobalCapture = from.GlobalCapture },
                    },
                },
            new SettingsTab(SettingsHubText.Revisions, revisions, vault, Icons.Review, string.Empty, MoreSettingsText.RevisionsAbout)
            {
                Settings =
                [
                    SettingsText.Calendar,
                    SettingsText.Steps,
                    SettingsText.ActiveRecall,
                    SettingsText.SmoothDueDates,
                    SettingsText.Reminders,
                    SettingsText.MaxPerDay,
                    SettingsText.MaxWaitDays,
                    SettingsText.RestMonths,
                ],
            },
            new SettingsTab(ModulesSettingsText.Tab, modules, vault, Icons.Puzzle, string.Empty, MoreSettingsText.ModulesAbout)
            {
                Settings =
                [
                    ModulesSettingsText.QuotesModule,
                    ModulesSettingsText.Online,
                    ModulesSettingsText.DomainsTitle,
                    ModulesSettingsText.SchemasModule,
                    ModulesSettingsText.ExtensionsTitle,
                    ModulesSettingsText.CatalogueTitle,
                    ModulesSettingsText.ExtensionDeveloper,
                ],
            },
            new SettingsTab(MoreSettingsText.VaultAndEncryption, data, vault, Icons.Lock, VaultPart, MoreSettingsText.VaultAbout)
            {
                Settings =
                [
                    SettingsHubText.OpenVault,
                    SettingsHubText.CreateVault,
                    new(EncryptionText.CardTitle, MoreSettingsText.WordsEncryption),
                    EncryptionText.LockAfter,
                    EncryptionText.LockNow,
                    EncryptionText.ChangePassword,
                    EncryptionText.Remember,
                ],
            },
            new SettingsTab(MoreSettingsText.BackupsAndExports, data, vault, Icons.Archive, BackupsPart, MoreSettingsText.BackupsAbout)
            {
                Settings =
                [
                    new(SettingsHubText.BackupsTitle, SettingsHubText.BackupNow),
                    new(SettingsHubText.TrashTitle, MoreSettingsText.WordsTrash),
                    SettingsHubText.ExportTracking,
                    SettingsHubText.ExportAnnotations,
                    SettingsHubText.ExportQuotes,
                    SettingsHubText.ExportAnki,
                    SettingsHubText.ExportCalendar,
                    SettingsHubText.ExportVault,
                ],
            },
            new SettingsTab(SettingsHubText.About, about, app, Icons.Info, string.Empty, MoreSettingsText.AboutAbout)
            {
                Settings = [new(SettingsHubText.CheckUpdate, MoreSettingsText.WordsVersion), SettingsHubText.LicenseLabel],
            },
        ];
        _machine = machine;
        // Un réglage changé ailleurs que par « Rétablir » : ce qui était proposé d'annuler ne correspond plus.
        machine?.Changed += (_, _) =>
        {
            if (!_restoring)
            {
                Forget();
            }
            RestoreCommand.NotifyCanExecuteChanged();
        };
        for (var index = 0; index < _tabs.Count; index++)
        {
            var tab = _tabs[index];
            tab.StartsGroup = index == 0 || _tabs[index - 1].Group != tab.Group;
            tab.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsTab.IsSelected) && tab.IsSelected)
                {
                    Select(tab);
                }
            };
        }
        Selected = _tabs[0];
        Selected.IsSelected = true;
    }

    /// <summary>« Paramètres ».</summary>
    public static string Title => SettingsText.Title;

    /// <summary>Les sections.</summary>
    public IReadOnlyList<SettingsTab> Tabs => _tabs;

    /// <summary>La section que l'on voit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OffersRestore))]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
    public partial SettingsTab Selected { get; private set; }

    /// <summary>Ce que l'on cherche : un réglage, par quelques lettres de son nom ou de sa section.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching), nameof(HasNoResult))]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>Les réglages et les sections trouvés.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResult))]
    public partial IReadOnlyList<SettingsHit> Results { get; private set; } = [];

    /// <summary>Une recherche est en cours : ses résultats prennent la place de la liste des sections.</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(Query);

    /// <summary>La recherche ne trouve rien.</summary>
    public bool HasNoResult => IsSearching && Results.Count == 0;

    /// <summary>La section ouverte est retenue sur l'ordinateur : elle propose de rétablir ses réglages d'origine.</summary>
    public bool OffersRestore => _machine is not null && Selected.Take is not null;

    /// <summary>Ce que « Rétablir » vient de faire, tant que cela peut s'annuler.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRestoreNote))]
    public partial string? RestoreNote { get; private set; }

    /// <summary>Des réglages viennent d'être rétablis : « Annuler » les remet.</summary>
    public bool HasRestoreNote => RestoreNote is not null;

    /// <inheritdoc/>
    public void OnNavigatedTo() => (Selected.Page as INavigationAware)?.OnNavigatedTo();

    /// <summary>Ouvre la section d'un résultat, et rend la liste des sections.</summary>
    [RelayCommand]
    private void Open(SettingsHit? hit)
    {
        if (hit is not null)
        {
            hit.Tab.IsSelected = true;
            Query = string.Empty;
        }
    }

    /// <summary>Entrée, dans le champ de recherche : le premier résultat.</summary>
    [RelayCommand]
    private void OpenFirst() => Open(Results.Count > 0 ? Results[0] : null);

    /// <summary>Échap, dans le champ de recherche : la liste des sections revient.</summary>
    [RelayCommand]
    private void ClearSearch() => Query = string.Empty;

    /// <summary>Remet les réglages de la section ouverte à leur valeur d'origine ; « Annuler » les rend.</summary>
    [RelayCommand(CanExecute = nameof(CanRestore))]
    private void Restore()
    {
        if (_machine is null || Selected.Take is not { } take)
        {
            return;
        }
        var before = _machine.Current;
        var kept = Put(current => take(Origin, current));
        _restored = Selected;
        _before = before;
        RestoreNote = kept ? MoreSettingsText.Restored : MoreSettingsText.RestoredNotKept;
        UndoRestoreCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Rend à la section les réglages qu'elle avait avant « Rétablir ».</summary>
    [RelayCommand(CanExecute = nameof(HasRestoreNote))]
    private void UndoRestore()
    {
        if (_restored?.Take is { } take && _before is { } before)
        {
            Put(current => take(before, current));
        }
        Forget();
    }

    /// <summary>Au moins un réglage de la section ouverte n'a plus sa valeur d'origine.</summary>
    private bool CanRestore() => _machine is not null && Selected.Take is { } take && take(Origin, _machine.Current) != _machine.Current;

    /// <summary>Écrit des réglages, puis les fait relire à la section, qui montrait les anciens.</summary>
    private bool Put(Func<MachinePreferences, MachinePreferences> change)
    {
        _restoring = true;
        var kept = _machine!.Update(change);
        _restoring = false;
        OnNavigatedTo();
        return kept;
    }

    private void Forget()
    {
        _restored = null;
        _before = null;
        RestoreNote = null;
        UndoRestoreCommand.NotifyCanExecuteChanged();
    }

    partial void OnQueryChanged(string value) => Results = Find(value);

    /// <summary>
    /// Les sections dont le nom contient tous les mots cherchés, et les réglages qui les contiennent tous entre leur nom,
    /// leurs mots et leur section, dont un au moins hors du nom de la section : chercher une section ne déroule pas tout
    /// ce qu'elle contient. Sans égard aux accents ni à la casse.
    /// </summary>
    private List<SettingsHit> Find(string query)
    {
        var words = DefinitionsText.WithoutAccents(query).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return [];
        }
        var found = new List<SettingsHit>();
        foreach (var tab in _tabs)
        {
            var section = DefinitionsText.WithoutAccents(tab.Label);
            if (words.All(word => section.Contains(word, StringComparison.Ordinal)))
            {
                found.Add(new SettingsHit(tab.Label, tab.Group, tab));
            }
            foreach (var entry in tab.Settings)
            {
                var own = DefinitionsText.WithoutAccents($"{entry.Label} {entry.Words}");
                if (words.Any(word => own.Contains(word, StringComparison.Ordinal))
                    && words.All(word => own.Contains(word, StringComparison.Ordinal) || section.Contains(word, StringComparison.Ordinal)))
                {
                    found.Add(new SettingsHit(entry.Label, tab.Label, tab));
                }
            }
        }
        return found;
    }

    private void Select(SettingsTab tab)
    {
        if (ReferenceEquals(tab, Selected))
        {
            return;
        }
        Selected.IsSelected = false;
        Forget();
        Selected = tab;
        OnNavigatedTo();
    }
}
