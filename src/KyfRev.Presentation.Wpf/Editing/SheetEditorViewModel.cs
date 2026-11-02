using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Où en est l'enregistrement.</summary>
public enum SaveState
{
    /// <summary>Tout est enregistré.</summary>
    Saved,

    /// <summary>Une frappe attend d'être enregistrée.</summary>
    Modified,

    /// <summary>L'enregistrement est en cours.</summary>
    Saving,

    /// <summary>La fiche a changé sur le disque : rien n'est écrit tant que ce n'est pas tranché.</summary>
    Conflict,

    /// <summary>L'enregistrement a échoué.</summary>
    Failed,
}

/// <summary>La taille d'un tableau à insérer.</summary>
/// <param name="Rows">Ses lignes, en-tête compris.</param>
/// <param name="Columns">Ses colonnes.</param>
public sealed record TableSize(int Rows, int Columns);

/// <summary>Ce dont l'écriture a besoin autour d'elle.</summary>
/// <param name="Spelling">Le correcteur d'orthographe.</param>
/// <param name="WebDataFolder">Le dossier des données de WebView2.</param>
/// <param name="StartsInMarkdown">Ouvrir sur le code, et non sur le texte mis en forme.</param>
public sealed record EditorEnvironment(ISpelling Spelling, string WebDataFolder, bool StartsInMarkdown = false);

/// <summary>Ce que le ruban affiche de l'endroit du curseur.</summary>
/// <param name="Bold">Gras.</param>
/// <param name="Italic">Italique.</param>
/// <param name="Underline">Souligné.</param>
/// <param name="Strike">Barré.</param>
/// <param name="Superscript">Exposant.</param>
/// <param name="Subscript">Indice.</param>
/// <param name="Heading">Le niveau de titre ; 0 pour du texte.</param>
/// <param name="List">La liste où il se trouve.</param>
/// <param name="CanUndo">Un geste peut être annulé.</param>
/// <param name="CanRedo">Un geste annulé peut être rétabli.</param>
public sealed record EditorTools(bool Bold, bool Italic, bool Underline, bool Strike, bool Superscript, bool Subscript, int Heading, ListKind? List, bool CanUndo, bool CanRedo);

/// <summary>Une saisie « [[… » au curseur.</summary>
/// <param name="Query">Ce qui est cherché.</param>
/// <param name="Start">Où elle commence.</param>
public sealed record LinkInput(string Query, int Start);

/// <summary>Ce qui préremplit la saisie d'un lien.</summary>
/// <param name="Label">Son texte.</param>
/// <param name="Address">Son adresse.</param>
public sealed record LinkQuestion(string Label, string Address);

/// <summary>Ce qui préremplit la saisie d'une formule.</summary>
/// <param name="Tex">Sa source.</param>
/// <param name="Block">Sur sa propre ligne.</param>
/// <param name="Position">Celle qu'elle remplace.</param>
public sealed record FormulaQuestion(string Tex, bool Block, int? Position);

/// <summary>
/// Ce que l'éditeur attend d'une façon d'écrire : le texte mis en forme ou le code Markdown. L'enregistrement et
/// les conflits ne dépendent que de cette interface.
/// </summary>
internal interface IEditorSurface
{
    /// <summary>Le texte a changé par la main de l'utilisateur.</summary>
    event EventHandler? TextEdited;

    /// <summary>L'endroit du curseur a changé.</summary>
    event EventHandler<EditorTools>? ToolsChanged;

    /// <summary>Une saisie « [[… » au curseur, ou plus de saisie.</summary>
    event EventHandler<LinkInput?>? LinkInputChanged;

    /// <summary>La saisie d'un lien est demandée.</summary>
    event EventHandler<LinkQuestion>? LinkAsked;

    /// <summary>La saisie d'une formule est demandée.</summary>
    event EventHandler<FormulaQuestion>? FormulaAsked;

    /// <summary>Enregistrer tout de suite.</summary>
    event EventHandler? SaveAsked;

    /// <summary>Une touche pour la liste des fiches.</summary>
    event EventHandler<Key>? ListKeyPressed;

    /// <summary>Le Markdown actuel de la fiche, avec des fins de ligne « \n ».</summary>
    string Text { get; }

    /// <summary>Où ouvrir la liste des fiches, dans le champ.</summary>
    Point InputPoint { get; }

    /// <summary>Met <see cref="Text"/> à jour de la dernière frappe.</summary>
    Task SyncAsync();

    /// <summary>Remplace tout par un contenu déjà connu : rien à enregistrer, rien à annuler.</summary>
    void ReplaceAll(string text);

    /// <summary>Met en forme la sélection, ou retire sa mise en forme.</summary>
    void ToggleMark(InlineMark mark);

    /// <summary>Donne un niveau de titre ; 0 en refait du texte.</summary>
    void SetHeading(int level);

    /// <summary>Met en liste, ou en sort.</summary>
    void ToggleList(ListKind kind);

    /// <summary>Change la casse ; sans style, la suivante.</summary>
    void ChangeCase(CaseStyle? style);

    /// <summary>Insère un tableau vide.</summary>
    void InsertTable(int rows, int columns);

    /// <summary>Crée un bloc de code, ou change le langage de celui où est le curseur.</summary>
    void InsertCode(string language);

    /// <summary>Insère une ligne horizontale.</summary>
    void InsertRule();

    /// <summary>Demande la saisie d'une formule, si cette façon d'écrire en a une.</summary>
    void AskFormula();

    /// <summary>Insère une formule, ou remplace celle de cette position.</summary>
    void InsertFormula(string tex, bool block, int? position);

    /// <summary>Demande la saisie d'un lien.</summary>
    void AskLink();

    /// <summary>Crée ou modifie un lien.</summary>
    void InsertLink(string label, string address);

    /// <summary>Insère une image du coffre.</summary>
    void InsertImage(string link, string alternative, int? position);

    /// <summary>Remplace la saisie « [[… » par un lien vers une fiche.</summary>
    void InsertSheetLink(string label, string address);

    /// <summary>Dit si la liste des fiches est ouverte.</summary>
    void SetProposalsOpen(bool open);

    /// <summary>Amène un titre en haut de la vue.</summary>
    void GoToHeading(string title, int occurrence);

    /// <summary>Annule le dernier geste.</summary>
    void Undo();

    /// <summary>Rétablit le dernier geste annulé.</summary>
    void Redo();

    /// <summary>Rend le focus au champ.</summary>
    void Focus();
}

/// <summary>
/// L'écriture d'une fiche : enregistrement automatique après la dernière frappe, un seul à la fois, et conflit
/// avec le disque sans perte. Par défaut, le texte mis en forme se modifie à sa place, comme dans un traitement de
/// texte ; un bouton affiche le code Markdown à gauche et son rendu à droite. Ni l'une ni l'autre ne réécrit ce que
/// l'utilisateur n'a pas touché : il reste tel quel, octet pour octet.
/// </summary>
public sealed partial class SheetEditorViewModel : ObservableObject
{
    /// <summary>Taille maximale d'une image, comme dans la 0.x.</summary>
    public const long MaximumImage = 50 * 1024 * 1024;

    /// <summary>En dessous, le rendu ne tient plus à côté du code.</summary>
    public const double PreviewMinimum = 880;

    private const int SaveAttempts = 3;

    private readonly OpenedSheet _sheet;
    private readonly EditingUseCases _editing;
    private readonly SheetUseCases _sheets;
    private readonly bool _crlf;
    private readonly ulong _session;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _previewTimer;
    private IEditorSurface? _source;
    private IEditorSurface? _visual;
    private Func<IEditorSurface?>? _visualProvider;
    private IEditorSurface? _surface;
    private string _fingerprint;
    private string _saved;
    private Task? _running;
    private bool _again;
    private int _previewVersion;
    private IReadOnlyList<VaultSheet>? _known;
    private bool _loadingKnown;
    private int? _inputStart;
    private int? _dismissedAt;
    private string _inputQuery = string.Empty;
    private SaveConflict? _conflict;
    private int? _formulaPosition;

    /// <summary>L'écriture de <paramref name="sheet"/>.</summary>
    public SheetEditorViewModel(OpenedSheet sheet, EditingUseCases editing, SheetUseCases sheets, EditorEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(editing);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(environment);
        _sheet = sheet;
        _editing = editing;
        _sheets = sheets;
        Environment = environment;
        _crlf = sheet.Body.Contains("\r\n", StringComparison.Ordinal);
        _fingerprint = sheet.Fingerprint;
        _saved = Normalize(sheet.Body);
        // Une session d'écriture : sa première modification garde une version de l'ancien texte.
        _session = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong))) >> 16;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _saveTimer.Tick += (_, _) => _ = SaveAsync();
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            _ = RefreshPreviewAsync();
        };
        LinkLabel = string.Empty;
        LinkAddress = string.Empty;
        FormulaTex = string.Empty;
        Proposals = [];
        IsMarkdownShown = environment.StartsInMarkdown;
    }

    /// <summary>Ce dont l'écriture a besoin autour d'elle.</summary>
    public EditorEnvironment Environment { get; }

    /// <summary>Le correcteur d'orthographe.</summary>
    public ISpelling Spelling => Environment.Spelling;

    /// <summary>L'identifiant de la fiche écrite.</summary>
    public string SheetId => _sheet.Id;

    /// <summary>Le dossier de la fiche dans le coffre, d'où partent ses images.</summary>
    public string Folder => ContentTargets.FolderOf(_sheet.Path);

    /// <summary>Le délai entre la dernière frappe et l'enregistrement.</summary>
    internal TimeSpan SaveDelay
    {
        get => _saveTimer.Interval;
        set => _saveTimer.Interval = value;
    }

    /// <summary>Le délai entre la dernière frappe et le rendu.</summary>
    internal TimeSpan PreviewDelay
    {
        get => _previewTimer.Interval;
        set => _previewTimer.Interval = value;
    }

    /// <summary>Où en est l'enregistrement.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel), nameof(HasConflict), nameof(HasFailed))]
    public partial SaveState State { get; private set; }

    /// <summary>L'état de l'enregistrement, en mots.</summary>
    public string StateLabel => State switch
    {
        SaveState.Modified => EditingText.Modified,
        SaveState.Saving => EditingText.Saving,
        SaveState.Conflict => EditingText.InConflict,
        SaveState.Failed => EditingText.SaveFailed,
        _ => EditingText.Saved,
    };

    /// <summary>La fiche a changé sur le disque.</summary>
    public bool HasConflict => State == SaveState.Conflict;

    /// <summary>L'enregistrement a échoué.</summary>
    public bool HasFailed => State == SaveState.Failed;

    /// <summary>Le dernier refus : un enregistrement, une image.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    /// <summary>Un refus est affiché.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Un enregistrement a écrit le texte sur le disque.</summary>
    public bool HasChanged { get; private set; }

    /// <summary>Le rendu du Markdown actuel, qui donne aussi le plan.</summary>
    [ObservableProperty]
    public partial SheetReading? Preview { get; private set; }

    /// <summary>Ce que le ruban affiche de l'endroit du curseur.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StyleLabel))]
    public partial EditorTools? Tools { get; private set; }

    /// <summary>Le style de la ligne du curseur.</summary>
    public string StyleLabel => Tools is { Heading: > 0 } tools ? EditingText.Heading(tools.Heading) : EditingText.NormalText;

    /// <summary>Le code Markdown est affiché, à gauche de son rendu. Sinon, le texte se modifie à sa place.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisualShown), nameof(ShowsPreview), nameof(ShowsSource), nameof(IsPreviewOn), nameof(IsMarkdownOn))]
    public partial bool IsMarkdownShown { get; private set; }

    /// <summary>Le texte se modifie à sa place.</summary>
    public bool IsVisualShown => !IsMarkdownShown;

    /// <summary>Le texte mis en forme a pu s'ouvrir.</summary>
    [ObservableProperty]
    public partial bool IsVisualAvailable { get; private set; } = true;

    /// <summary>Le bouton « Afficher le Markdown ».</summary>
    public bool IsMarkdownOn
    {
        get => IsMarkdownShown;
        set => _ = ShowMarkdownAsync(value);
    }

    /// <summary>La largeur de la vue d'écriture.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFits), nameof(ShowsPreview), nameof(ShowsSource), nameof(IsPreviewOn))]
    public partial double EditorWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Le rendu tient à côté du code.</summary>
    public bool PreviewFits => EditorWidth >= PreviewMinimum;

    /// <summary>Le rendu est voulu à côté du code, quand il y tient.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPreview), nameof(ShowsSource), nameof(IsPreviewOn))]
    private partial bool WantsPreview { get; set; } = true;

    /// <summary>Faute de place, le rendu prend la place du code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPreview), nameof(ShowsSource), nameof(IsPreviewOn))]
    private partial bool PreviewReplacesSource { get; set; }

    /// <summary>Le rendu est montré, à côté du code.</summary>
    public bool ShowsPreview => IsMarkdownShown && (PreviewFits ? WantsPreview : PreviewReplacesSource);

    /// <summary>Le code est montré.</summary>
    public bool ShowsSource => IsMarkdownShown && (PreviewFits || !PreviewReplacesSource);

    /// <summary>Le bouton de l'aperçu : à côté du code s'il y tient, sinon à sa place.</summary>
    public bool IsPreviewOn
    {
        get => ShowsPreview;
        set
        {
            if (PreviewFits)
            {
                WantsPreview = value;
            }
            else
            {
                PreviewReplacesSource = value;
            }
        }
    }

    /// <summary>La saisie d'un lien est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsLinkOpen { get; private set; }

    /// <summary>Le texte du lien.</summary>
    [ObservableProperty]
    public partial string LinkLabel { get; set; }

    /// <summary>L'adresse du lien.</summary>
    [ObservableProperty]
    public partial string LinkAddress { get; set; }

    /// <summary>La saisie d'une formule est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsFormulaOpen { get; private set; }

    /// <summary>La source de la formule.</summary>
    [ObservableProperty]
    public partial string FormulaTex { get; set; }

    /// <summary>La formule est sur sa propre ligne.</summary>
    [ObservableProperty]
    public partial bool FormulaBlock { get; set; }

    /// <summary>Les fiches proposées après « [[ ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProposals))]
    public partial IReadOnlyList<VaultSheet> Proposals { get; private set; }

    /// <summary>Une liste de fiches est ouverte.</summary>
    public bool HasProposals => Proposals.Count > 0;

    /// <summary>La fiche que choisirait Entrée.</summary>
    [ObservableProperty]
    public partial VaultSheet? ActiveProposal { get; set; }

    /// <summary>Où ouvrir la liste des fiches, dans le champ actif.</summary>
    internal Point InputPoint => _surface?.InputPoint ?? default;

    /// <summary>Le champ actif.</summary>
    internal IEditorSurface? Surface => _surface;

    /// <summary>Les champs d'écriture : le code, et le texte mis en forme, créé à la première demande.</summary>
    internal void Attach(IEditorSurface source, Func<IEditorSurface?> visual)
    {
        Hook(_surface, false);
        _source = source;
        _visualProvider = visual;
        _visual = null;
        var surface = IsMarkdownShown ? source : VisualOrFallback();
        IsMarkdownShown = ReferenceEquals(surface, source);
        surface.ReplaceAll(_saved);
        _surface = surface;
        Hook(surface, true);
    }

    /// <summary>Le texte mis en forme s'est arrêté : l'écriture continue en Markdown, sans rien perdre.</summary>
    internal async Task VisualFailedAsync()
    {
        IsVisualAvailable = false;
        _visualProvider = null;
        Problem = EditingText.VisualUnavailable;
        if (!IsMarkdownShown)
        {
            await ShowMarkdownAsync(true).ConfigureAwait(true);
        }
    }

    /// <summary>Passe d'une façon d'écrire à l'autre, sans rien perdre ni enregistrer.</summary>
    public async Task ShowMarkdownAsync(bool markdown)
    {
        if (markdown != IsMarkdownShown && _surface is { } from && _source is { } source && (markdown || IsVisualAvailable))
        {
            await from.SyncAsync().ConfigureAwait(true);
            var text = from.Text;
            var to = markdown ? source : VisualOrFallback();
            Hook(from, false);
            to.ReplaceAll(text);
            _surface = to;
            Hook(to, true);
            IsMarkdownShown = ReferenceEquals(to, source);
            await RefreshPreviewAsync().ConfigureAwait(true);
            to.Focus();
        }
        OnPropertyChanged(nameof(IsMarkdownOn));
    }

    /// <summary>Enregistre le texte s'il a changé ; un seul enregistrement à la fois, le suivant repart du texte du moment.</summary>
    public Task SaveAsync()
    {
        _saveTimer.Stop();
        if (_running is not null)
        {
            _again = true;
            return _running;
        }
        if (_surface is null || HasConflict)
        {
            return Task.CompletedTask;
        }
        var loop = SaveLoopAsync();
        _running = loop.IsCompleted ? null : loop;
        return loop;
    }

    /// <summary>Avant de quitter l'écriture : vrai si tout est enregistré.</summary>
    public async Task<bool> FinishAsync()
    {
        _saveTimer.Stop();
        _previewTimer.Stop();
        if (_surface is not { } surface)
        {
            return true;
        }
        await surface.SyncAsync().ConfigureAwait(true);
        for (var attempt = 0; attempt < SaveAttempts && surface.Text != _saved && !HasConflict && !HasFailed; attempt++)
        {
            await SaveAsync().ConfigureAwait(true);
            await surface.SyncAsync().ConfigureAwait(true);
        }
        return surface.Text == _saved;
    }

    /// <summary>Prépare le premier rendu, ou le refait.</summary>
    public async Task RefreshPreviewAsync()
    {
        if (_surface is not { } surface)
        {
            return;
        }
        var version = ++_previewVersion;
        await surface.SyncAsync().ConfigureAwait(true);
        var text = surface.Text;
        var reading = await Task.Run(() => SheetReading.Of(_sheet with { Body = text, Annotations = [] }, _sheets)).ConfigureAwait(true);
        if (version == _previewVersion)
        {
            Preview = reading;
        }
    }

    /// <summary>Le contenu d'une image du coffre, pour le texte mis en forme.</summary>
    public Outcome<byte[]> ReadResource(string path) => _sheets.ReadResource(path);

    /// <summary>Copie des images choisies à côté de la fiche et les insère.</summary>
    public async Task InsertImageFilesAsync(IReadOnlyList<string> paths, int? position = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            try
            {
                if (new FileInfo(path).Length > MaximumImage)
                {
                    Problem = EditingText.ImageTooLarge(name);
                    continue;
                }
                var content = await File.ReadAllBytesAsync(path).ConfigureAwait(true);
                if (await InsertImageAsync(name, content, position).ConfigureAwait(true))
                {
                    position = null;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Problem = exception.Message;
            }
        }
    }

    /// <summary>Copie une image à côté de la fiche et l'insère ; rend vrai si elle y est.</summary>
    public async Task<bool> InsertImageAsync(string name, byte[]? content, int? position = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (content is null)
        {
            Problem = EditingText.ImageTooLarge(name);
            return false;
        }
        var outcome = await Task.Run(() => _editing.AddImage(SheetId, name, content)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Problem = outcome.Error;
            return false;
        }
        var alternative = Path.GetFileNameWithoutExtension(name).Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
        _surface?.InsertImage(outcome.Value, alternative, position);
        return true;
    }

    /// <summary>Les touches qui agissent sur le code, avant le champ : rend vrai si l'une d'elles est prise.</summary>
    public bool TakeKey(Key key, ModifierKeys modifiers)
    {
        if (TakeProposalKey(key, modifiers))
        {
            return true;
        }
        Action? action = (key, modifiers) switch
        {
            (Key.B, ModifierKeys.Control) => () => ToggleMark(InlineMark.Bold),
            (Key.I, ModifierKeys.Control) => () => ToggleMark(InlineMark.Italic),
            (Key.U, ModifierKeys.Control) => () => ToggleMark(InlineMark.Underline),
            (Key.S, ModifierKeys.Control) => () => _ = SaveAsync(),
            (Key.F3, ModifierKeys.Shift) => () => ChangeCaseTo(null),
            _ => null,
        };
        action?.Invoke();
        return action is not null;
    }

    /// <summary>Dit ce qui vient d'échouer.</summary>
    internal void Report(string problem) => Problem = problem;

    /// <summary>Amène un titre du plan en haut du texte.</summary>
    internal void GoToHeading(string title, int occurrence) => _surface?.GoToHeading(title, occurrence);

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private IEditorSurface VisualOrFallback()
    {
        _visual ??= _visualProvider?.Invoke();
        return _visual ?? _source!;
    }

    private void Hook(IEditorSurface? surface, bool on)
    {
        if (surface is null)
        {
            return;
        }
        if (on)
        {
            surface.TextEdited += OnTextEdited;
            surface.ToolsChanged += OnToolsChanged;
            surface.LinkInputChanged += OnLinkInput;
            surface.LinkAsked += OnLinkAsked;
            surface.FormulaAsked += OnFormulaAsked;
            surface.SaveAsked += OnSaveAsked;
            surface.ListKeyPressed += OnListKey;
        }
        else
        {
            surface.TextEdited -= OnTextEdited;
            surface.ToolsChanged -= OnToolsChanged;
            surface.LinkInputChanged -= OnLinkInput;
            surface.LinkAsked -= OnLinkAsked;
            surface.FormulaAsked -= OnFormulaAsked;
            surface.SaveAsked -= OnSaveAsked;
            surface.ListKeyPressed -= OnListKey;
        }
    }

    private string ToDisk(string text) => _crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;

    private async Task SaveLoopAsync()
    {
        try
        {
            do
            {
                _again = false;
                await _surface!.SyncAsync().ConfigureAwait(true);
                var current = _surface.Text;
                if (HasConflict)
                {
                    return;
                }
                if (current == _saved)
                {
                    break;
                }
                State = SaveState.Saving;
                var (fingerprint, session, written) = (_fingerprint, _session, ToDisk(current));
                var outcome = await Task.Run(() => _editing.Save(SheetId, written, fingerprint, session)).ConfigureAwait(true);
                if (!outcome.Succeeded)
                {
                    Problem = outcome.Error;
                    State = SaveState.Failed;
                    return;
                }
                if (outcome.Value is SaveConflict conflict)
                {
                    _conflict = conflict;
                    State = SaveState.Conflict;
                    return;
                }
                _fingerprint = ((SheetSaved)outcome.Value).Fingerprint;
                _saved = current;
                HasChanged = true;
                Problem = null;
            }
            while (_again);
            State = _surface!.Text == _saved ? SaveState.Saved : SaveState.Modified;
        }
        finally
        {
            _running = null;
        }
    }

    /// <summary>« Garder ma version » : celle du disque va d'abord dans l'historique, comme à chaque session.</summary>
    [RelayCommand]
    private async Task KeepMineAsync()
    {
        if (_conflict is not { } conflict)
        {
            return;
        }
        _fingerprint = conflict.DiskFingerprint;
        _conflict = null;
        State = SaveState.Modified;
        await SaveAsync().ConfigureAwait(true);
        _surface?.Focus();
    }

    /// <summary>« Recharger la version du disque » : mon texte va d'abord dans l'historique.</summary>
    [RelayCommand]
    private async Task ReloadDiskAsync()
    {
        if (_conflict is not { } conflict || _surface is not { } surface)
        {
            return;
        }
        await surface.SyncAsync().ConfigureAwait(true);
        var mine = ToDisk(surface.Text);
        var kept = await Task.Run(() => _editing.KeepAsVersion(SheetId, mine)).ConfigureAwait(true);
        if (!kept.Succeeded)
        {
            Problem = kept.Error;
            return;
        }
        _fingerprint = conflict.DiskFingerprint;
        _saved = Normalize(conflict.DiskBody);
        _conflict = null;
        surface.ReplaceAll(_saved);
        State = SaveState.Saved;
        HasChanged = true;
        Problem = null;
        surface.Focus();
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    /// <summary>Après un échec, réessaie d'enregistrer.</summary>
    [RelayCommand]
    private async Task RetryAsync()
    {
        Problem = null;
        State = SaveState.Modified;
        await SaveAsync().ConfigureAwait(true);
    }

    /// <summary>Referme le refus affiché.</summary>
    [RelayCommand]
    private void DismissProblem() => Problem = null;

    /// <summary>Annule le dernier geste.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _surface?.Undo();
        _surface?.Focus();
    }

    private bool CanUndo() => Tools?.CanUndo == true;

    /// <summary>Rétablit le dernier geste annulé.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _surface?.Redo();
        _surface?.Focus();
    }

    private bool CanRedo() => Tools?.CanRedo == true;

    /// <summary>Met en forme la sélection, ou retire sa mise en forme.</summary>
    [RelayCommand]
    private void ToggleMark(InlineMark mark) => _surface?.ToggleMark(mark);

    /// <summary>Donne un niveau de titre aux lignes de la sélection ; 0 en refait du texte.</summary>
    [RelayCommand]
    private void SetHeading(int level) => _surface?.SetHeading(level);

    /// <summary>Met les lignes de la sélection en liste, ou les en sort.</summary>
    [RelayCommand]
    private void ToggleList(ListKind kind) => _surface?.ToggleList(kind);

    /// <summary>Change la casse de la sélection, ou du mot sous le curseur.</summary>
    [RelayCommand]
    private void ChangeCase(CaseStyle style) => ChangeCaseTo(style);

    /// <summary>Insère un tableau vide.</summary>
    [RelayCommand]
    private void InsertTable(TableSize size) => _surface?.InsertTable(size.Rows, size.Columns);

    /// <summary>Crée un bloc de code, ou change le langage de celui où est le curseur.</summary>
    [RelayCommand]
    private void InsertCode(string language) => _surface?.InsertCode(language);

    /// <summary>Insère une ligne horizontale.</summary>
    [RelayCommand]
    private void InsertRule() => _surface?.InsertRule();

    /// <summary>Demande une formule : une saisie dans le texte mis en forme, des signes dans le code.</summary>
    [RelayCommand]
    private void AskFormula() => _surface?.AskFormula();

    /// <summary>Insère la formule saisie.</summary>
    [RelayCommand]
    private void AnswerFormula(bool confirmed)
    {
        IsFormulaOpen = false;
        if (confirmed && FormulaTex.Trim().Length > 0)
        {
            _surface?.InsertFormula(FormulaTex.Trim(), FormulaBlock, _formulaPosition);
        }
        else
        {
            _surface?.Focus();
        }
    }

    /// <summary>Ouvre la saisie d'un lien, le texte sélectionné comme libellé.</summary>
    [RelayCommand]
    private void StartLink() => _surface?.AskLink();

    /// <summary>Insère le lien saisi.</summary>
    [RelayCommand]
    private void AnswerLink(bool confirmed)
    {
        IsLinkOpen = false;
        var address = LinkAddress.Trim();
        if (confirmed && address.Length > 0)
        {
            _surface?.InsertLink(LinkLabel.Trim().Length > 0 ? LinkLabel.Trim() : address, address);
        }
        else
        {
            _surface?.Focus();
        }
    }

    /// <summary>Choisit une fiche de la liste : la saisie « [[… » devient un lien ordinaire.</summary>
    [RelayCommand]
    private void ChooseProposal(VaultSheet sheet)
    {
        if (_surface is { } surface && _inputStart is not null)
        {
            surface.InsertSheetLink(sheet.Title, SheetLinks.AddressOf(Folder, sheet.Path));
            surface.Focus();
        }
    }

    private void ChangeCaseTo(CaseStyle? style)
    {
        _surface?.ChangeCase(style);
        _surface?.Focus();
    }

    private void OnTextEdited(object? sender, EventArgs e)
    {
        if (!HasConflict)
        {
            State = SaveState.Modified;
        }
        _saveTimer.Stop();
        _saveTimer.Start();
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnToolsChanged(object? sender, EditorTools tools)
    {
        Tools = tools;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnSaveAsked(object? sender, EventArgs e) => _ = SaveAsync();

    private void OnListKey(object? sender, Key key) => TakeProposalKey(key, ModifierKeys.None);

    private void OnLinkAsked(object? sender, LinkQuestion question)
    {
        LinkLabel = question.Label;
        LinkAddress = question.Address;
        IsLinkOpen = true;
    }

    private void OnFormulaAsked(object? sender, FormulaQuestion question)
    {
        FormulaTex = question.Tex;
        FormulaBlock = question.Block;
        _formulaPosition = question.Position;
        IsFormulaOpen = true;
    }

    /// <summary>La liste des fiches s'ouvre sur une saisie « [[… », et se tient fermée après Échap.</summary>
    private void OnLinkInput(object? sender, LinkInput? input)
    {
        if (input?.Start != _dismissedAt)
        {
            _dismissedAt = null;
        }
        if (input is null || input.Start == _dismissedAt)
        {
            CloseProposals();
            return;
        }
        _inputStart = input.Start;
        _inputQuery = input.Query;
        if (_known is null)
        {
            _ = LoadKnownAsync();
            return;
        }
        ShowProposals();
    }

    private void ShowProposals()
    {
        Proposals = SheetLinks.Propose(_known!, _inputQuery, SheetId);
        if (ActiveProposal is null || !Proposals.Contains(ActiveProposal))
        {
            ActiveProposal = Proposals.Count > 0 ? Proposals[0] : null;
        }
        _surface?.SetProposalsOpen(HasProposals);
    }

    private async Task LoadKnownAsync()
    {
        if (_loadingKnown)
        {
            return;
        }
        _loadingKnown = true;
        var listed = await Task.Run(_sheets.List).ConfigureAwait(true);
        _known = listed.Succeeded ? listed.Value : [];
        _loadingKnown = false;
        if (_inputStart is not null)
        {
            ShowProposals();
        }
    }

    private void CloseProposals()
    {
        _inputStart = null;
        Proposals = [];
        ActiveProposal = null;
        _surface?.SetProposalsOpen(false);
    }

    private bool TakeProposalKey(Key key, ModifierKeys modifiers)
    {
        if (_inputStart is not { } start || modifiers != ModifierKeys.None)
        {
            return false;
        }
        if (key == Key.Escape)
        {
            _dismissedAt = start;
            CloseProposals();
            return true;
        }
        if (Proposals.Count == 0)
        {
            return false;
        }
        var index = ActiveProposal is null ? 0 : Math.Max(0, IndexOf(ActiveProposal));
        switch (key)
        {
            case Key.Down:
                ActiveProposal = Proposals[(index + 1) % Proposals.Count];
                return true;
            case Key.Up:
                ActiveProposal = Proposals[(index - 1 + Proposals.Count) % Proposals.Count];
                return true;
            case Key.Enter or Key.Tab:
                ChooseProposal(Proposals[index]);
                return true;
            default:
                return false;
        }
    }

    private int IndexOf(VaultSheet sheet)
    {
        for (var i = 0; i < Proposals.Count; i++)
        {
            if (Proposals[i] == sheet)
            {
                return i;
            }
        }
        return -1;
    }
}
