using System.IO;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

/// <summary>Ce que l'éditeur attend du champ d'écriture. Le texte n'y a que des fins de ligne « \n ».</summary>
internal interface IEditorSurface
{
    /// <summary>Le texte a changé par la main de l'utilisateur.</summary>
    event EventHandler? TextEdited;

    /// <summary>Le curseur a bougé.</summary>
    event EventHandler? CaretMoved;

    /// <summary>Le Markdown actuel de la fiche.</summary>
    string Text { get; }

    /// <summary>Le début de la sélection.</summary>
    int SelectionStart { get; }

    /// <summary>La fin de la sélection.</summary>
    int SelectionEnd { get; }

    /// <summary>La ligne du curseur.</summary>
    string CaretLine { get; }

    /// <summary>Le curseur dans sa ligne.</summary>
    int CaretColumn { get; }

    /// <summary>Un geste peut être annulé.</summary>
    bool CanUndo { get; }

    /// <summary>Un geste annulé peut être rétabli.</summary>
    bool CanRedo { get; }

    /// <summary>Fait un remplacement, comme une frappe.</summary>
    void Apply(TextChange change);

    /// <summary>Remplace tout par un contenu déjà enregistré : rien à enregistrer, rien à annuler.</summary>
    void ReplaceAll(string text);

    /// <summary>Annule le dernier geste.</summary>
    void Undo();

    /// <summary>Rétablit le dernier geste annulé.</summary>
    void Redo();

    /// <summary>Rend le focus au champ.</summary>
    void Focus();
}

/// <summary>
/// L'écriture d'une fiche : enregistrement automatique après la dernière frappe, un seul à la fois, et conflit
/// avec le disque sans perte. Les gestes du ruban rendent un remplacement du Markdown, et rien d'autre ne le
/// réécrit : ce que l'utilisateur n'a pas touché reste tel quel, octet pour octet.
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

    /// <summary>L'écriture de <paramref name="sheet"/>.</summary>
    public SheetEditorViewModel(OpenedSheet sheet, EditingUseCases editing, SheetUseCases sheets, ISpelling spelling)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(editing);
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(spelling);
        _sheet = sheet;
        _editing = editing;
        _sheets = sheets;
        Spelling = spelling;
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
        Proposals = [];
    }

    /// <summary>Le correcteur d'orthographe.</summary>
    public ISpelling Spelling { get; }

    /// <summary>L'identifiant de la fiche écrite.</summary>
    public string SheetId => _sheet.Id;

    /// <summary>Le Markdown à l'ouverture, avec des fins de ligne « \n ».</summary>
    public string InitialText => Normalize(_sheet.Body);

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

    /// <summary>Le rendu du Markdown actuel.</summary>
    [ObservableProperty]
    public partial SheetReading? Preview { get; private set; }

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

    /// <summary>Le rendu est montré.</summary>
    public bool ShowsPreview => PreviewFits ? WantsPreview : PreviewReplacesSource;

    /// <summary>Le code est montré.</summary>
    public bool ShowsSource => PreviewFits || !PreviewReplacesSource;

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

    /// <summary>Le style de la ligne du curseur.</summary>
    [ObservableProperty]
    public partial string StyleLabel { get; private set; } = EditingText.NormalText;

    /// <summary>La saisie d'un lien est ouverte.</summary>
    [ObservableProperty]
    public partial bool IsLinkOpen { get; private set; }

    /// <summary>Le texte du lien.</summary>
    [ObservableProperty]
    public partial string LinkLabel { get; set; }

    /// <summary>L'adresse du lien.</summary>
    [ObservableProperty]
    public partial string LinkAddress { get; set; }

    /// <summary>Les fiches proposées après « [[ ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProposals))]
    public partial IReadOnlyList<VaultSheet> Proposals { get; private set; }

    /// <summary>Une liste de fiches est ouverte.</summary>
    public bool HasProposals => Proposals.Count > 0;

    /// <summary>La fiche que choisirait Entrée.</summary>
    [ObservableProperty]
    public partial VaultSheet? ActiveProposal { get; set; }

    /// <summary>Le champ d'écriture : le remet sur le texte à ouvrir.</summary>
    internal void Attach(IEditorSurface surface)
    {
        if (_surface is not null)
        {
            _surface.TextEdited -= OnTextEdited;
            _surface.CaretMoved -= OnCaretMoved;
        }
        _surface = surface;
        surface.ReplaceAll(_saved);
        surface.TextEdited += OnTextEdited;
        surface.CaretMoved += OnCaretMoved;
        OnCaretMoved(surface, EventArgs.Empty);
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
        if (_surface is null || HasConflict || _surface.Text == _saved)
        {
            return Task.CompletedTask;
        }
        _running = SaveLoopAsync();
        return _running;
    }

    /// <summary>Avant de quitter l'écriture : vrai si tout est enregistré.</summary>
    public async Task<bool> FinishAsync()
    {
        _saveTimer.Stop();
        _previewTimer.Stop();
        for (var attempt = 0; attempt < SaveAttempts && _surface is not null && _surface.Text != _saved && !HasConflict && !HasFailed; attempt++)
        {
            await SaveAsync().ConfigureAwait(true);
        }
        return _surface is null || _surface.Text == _saved;
    }

    /// <summary>Prépare le premier rendu, ou le refait.</summary>
    public async Task RefreshPreviewAsync()
    {
        if (_surface is not { } surface)
        {
            return;
        }
        var version = ++_previewVersion;
        var text = surface.Text;
        var reading = await Task.Run(() => SheetReading.Of(_sheet with { Body = text, Annotations = [] }, _sheets)).ConfigureAwait(true);
        if (version == _previewVersion)
        {
            Preview = reading;
        }
    }

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
    public async Task<bool> InsertImageAsync(string name, byte[] content, int? position = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);
        var outcome = await Task.Run(() => _editing.AddImage(SheetId, name, content)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Problem = outcome.Error;
            return false;
        }
        if (_surface is { } surface)
        {
            var alternative = Path.GetFileNameWithoutExtension(name).Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
            surface.Apply(MarkdownEdits.Block(surface.Text, position ?? surface.SelectionStart, $"![{alternative}]({outcome.Value})"));
        }
        return true;
    }

    /// <summary>Les touches qui agissent sur le texte, avant le champ : rend vrai si l'une d'elles est prise.</summary>
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

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private string ToDisk(string text) => _crlf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;

    private async Task SaveLoopAsync()
    {
        try
        {
            do
            {
                _again = false;
                var current = _surface!.Text;
                if (HasConflict || current == _saved)
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

    private SaveConflict? _conflict;

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

    private bool CanUndo() => _surface?.CanUndo == true;

    /// <summary>Rétablit le dernier geste annulé.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _surface?.Redo();
        _surface?.Focus();
    }

    private bool CanRedo() => _surface?.CanRedo == true;

    /// <summary>Met en forme la sélection, ou retire sa mise en forme.</summary>
    [RelayCommand]
    private void ToggleMark(InlineMark mark) => Edit((text, start, end) => MarkdownEdits.Toggle(text, start, end, mark));

    /// <summary>Donne un niveau de titre aux lignes de la sélection ; 0 en refait du texte.</summary>
    [RelayCommand]
    private void SetHeading(int level) => Edit((text, start, end) => MarkdownEdits.Heading(text, start, end, level));

    /// <summary>Met les lignes de la sélection en liste, ou les en sort.</summary>
    [RelayCommand]
    private void ToggleList(ListKind kind) => Edit((text, start, end) => MarkdownEdits.List(text, start, end, kind));

    /// <summary>Change la casse de la sélection, ou du mot sous le curseur.</summary>
    [RelayCommand]
    private void ChangeCase(CaseStyle style) => ChangeCaseTo(style);

    /// <summary>Insère un tableau vide.</summary>
    [RelayCommand]
    private void InsertTable(TableSize size) => Edit((text, start, _) => MarkdownEdits.Block(text, start, MarkdownEdits.Table(size.Rows, size.Columns)));

    /// <summary>Crée un bloc de code, ou change le langage de celui où est le curseur.</summary>
    [RelayCommand]
    private void InsertCode(string language) => Edit((text, start, end) => MarkdownEdits.Code(text, start, end, language));

    /// <summary>Insère une ligne horizontale.</summary>
    [RelayCommand]
    private void InsertRule() => Edit((text, start, _) => MarkdownEdits.Rule(text, start));

    /// <summary>Ouvre la saisie d'un lien, le texte sélectionné comme libellé.</summary>
    [RelayCommand]
    private void StartLink()
    {
        if (_surface is { } surface)
        {
            LinkLabel = surface.Text[surface.SelectionStart..surface.SelectionEnd];
            LinkAddress = string.Empty;
            IsLinkOpen = true;
        }
    }

    /// <summary>Insère le lien saisi.</summary>
    [RelayCommand]
    private void AnswerLink(bool confirmed)
    {
        IsLinkOpen = false;
        var address = LinkAddress.Trim();
        if (confirmed && address.Length > 0)
        {
            var label = LinkLabel.Trim().Length > 0 ? LinkLabel.Trim() : address;
            Edit((_, start, end) => MarkdownEdits.Link(start, end, label, address));
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
        if (_surface is not { } surface || _inputStart is not { } start)
        {
            return;
        }
        var caret = surface.SelectionStart;
        var text = surface.Text;
        var end = string.CompareOrdinal(text, caret, "]]", 0, 2) == 0 ? caret + 2 : caret;
        var link = SheetLinks.MarkdownLink(sheet.Title, SheetLinks.AddressOf(ContentTargets.FolderOf(_sheet.Path), sheet.Path));
        surface.Apply(new TextChange(start, end - start, link, start + link.Length, 0));
        surface.Focus();
    }

    private void Edit(Func<string, int, int, TextChange> edit)
    {
        if (_surface is { } surface)
        {
            surface.Apply(edit(surface.Text, surface.SelectionStart, surface.SelectionEnd));
            surface.Focus();
        }
    }

    private void ChangeCaseTo(CaseStyle? style)
    {
        if (_surface is { } surface && MarkdownEdits.Case(surface.Text, surface.SelectionStart, surface.SelectionEnd, style) is { } change)
        {
            surface.Apply(change);
        }
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
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        var surface = _surface!;
        var level = MarkdownEdits.HeadingLevel(surface.CaretLine, 0);
        StyleLabel = level > 0 ? EditingText.Heading(level) : EditingText.NormalText;
        FollowLinkInput(surface);
    }

    /// <summary>La liste des fiches s'ouvre sur une saisie « [[… » hors du code, et se tient fermée après Échap.</summary>
    private void FollowLinkInput(IEditorSurface surface)
    {
        var before = surface.CaretLine[..surface.CaretColumn];
        var input = surface.SelectionStart == surface.SelectionEnd ? SheetLinks.InputBefore(before) : null;
        if (input is { } found && (MarkdownLines.IsInInlineCode(before) || MarkdownLines.FenceAt(surface.Text, surface.SelectionStart) is not null))
        {
            input = null;
        }
        var start = input is { } typed ? surface.SelectionStart - typed.Length : (int?)null;
        if (start != _dismissedAt)
        {
            _dismissedAt = null;
        }
        if (input is null || start == _dismissedAt)
        {
            CloseProposals();
            return;
        }
        _inputStart = start;
        if (_known is null)
        {
            _ = LoadKnownAsync();
            return;
        }
        var query = input.Value.Query;
        Proposals = SheetLinks.Propose(_known, query, SheetId);
        if (ActiveProposal is null || !Proposals.Contains(ActiveProposal))
        {
            ActiveProposal = Proposals.Count > 0 ? Proposals[0] : null;
        }
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
        if (_surface is { } surface)
        {
            FollowLinkInput(surface);
        }
    }

    private void CloseProposals()
    {
        _inputStart = null;
        Proposals = [];
        ActiveProposal = null;
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
