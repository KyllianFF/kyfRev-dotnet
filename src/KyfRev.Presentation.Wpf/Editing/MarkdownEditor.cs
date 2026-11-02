using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using KyfRev.Application.Ports;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le champ d'écriture : le Markdown tel qu'il est sur le disque, jamais reformaté, avec sa coloration, ses
/// fautes soulignées, le collage et le dépôt d'images. La touche Tab en sort : ce n'est pas un piège pour le clavier.
/// </summary>
internal sealed class MarkdownEditor : TextEditor, IEditorSurface
{
    private const string ColorProperty = "color";
    private const string SizeProperty = "font-size";

    /// <summary>La couleur du curseur.</summary>
    public static readonly DependencyProperty CaretBrushProperty = DependencyProperty.Register(
        nameof(CaretBrush), typeof(Brush), typeof(MarkdownEditor), new PropertyMetadata(null, (sender, e) => ((MarkdownEditor)sender).TextArea.Caret.CaretBrush = (Brush?)e.NewValue));

    /// <summary>La couleur des titres et des liens.</summary>
    public static readonly DependencyProperty EmphasisBrushProperty = DependencyProperty.Register(
        nameof(EmphasisBrush), typeof(Brush), typeof(MarkdownEditor), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>La couleur des signes de la syntaxe.</summary>
    public static readonly DependencyProperty SyntaxBrushProperty = DependencyProperty.Register(
        nameof(SyntaxBrush), typeof(Brush), typeof(MarkdownEditor), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>La couleur des balises.</summary>
    public static readonly DependencyProperty TagBrushProperty = DependencyProperty.Register(
        nameof(TagBrush), typeof(Brush), typeof(MarkdownEditor), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>La couleur des fautes.</summary>
    public static readonly DependencyProperty MistakeBrushProperty = DependencyProperty.Register(
        nameof(MistakeBrush), typeof(Brush), typeof(MarkdownEditor), new PropertyMetadata(null, OnBrushChanged));

    private readonly SpellingLayer _spellingLayer;
    private List<MarkdownLines.Fence>? _fences;
    private bool _silent;
    private int? _inputStart;

    public MarkdownEditor()
    {
        WordWrap = true;
        ShowLineNumbers = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        Options.EnableRectangularSelection = false;
        Options.CutCopyWholeLine = false;
        Options.EnableImeSupport = true;
        SetResourceReference(BackgroundProperty, "Kyf.Brush.Surface");
        SetResourceReference(ForegroundProperty, "Kyf.Brush.Text");
        SetResourceReference(FontFamilyProperty, "Kyf.Font.Code");
        SetResourceReference(FontSizeProperty, "Kyf.FontSize.Body");
        SetResourceReference(CaretBrushProperty, "Kyf.Brush.Text");
        SetResourceReference(EmphasisBrushProperty, "Kyf.Brush.Link");
        SetResourceReference(SyntaxBrushProperty, "Kyf.Brush.TextSecondary");
        SetResourceReference(TagBrushProperty, "Kyf.Brush.Warning");
        SetResourceReference(MistakeBrushProperty, "Kyf.Brush.Critical");
        TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Kyf.Brush.Selection");
        TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionForegroundProperty, "Kyf.Brush.OnSelection");
        TextArea.SelectionBorder = null;
        TextArea.SelectionCornerRadius = 0;
        TextArea.Margin = new Thickness(12, 8, 12, 8);
        System.Windows.Automation.AutomationProperties.SetName(TextArea, EditingText.Source);
        System.Windows.Automation.AutomationProperties.SetAutomationId(TextArea, "SheetSource");
        TextArea.TextView.LineTransformers.Add(new MarkdownColorizer(this));
        _spellingLayer = new SpellingLayer(this);
        TextArea.TextView.BackgroundRenderers.Add(_spellingLayer);
        Document.Changed += OnDocumentChanged;
        TextArea.Caret.PositionChanged += (_, _) => FollowCaret();
        TextArea.SelectionChanged += (_, _) => FollowCaret();
        TextArea.ContextMenuOpening += OnContextMenuOpening;
        TextArea.ContextMenu = new ContextMenu();
        ReplacePasteCommand();
        TextArea.AllowDrop = true;
        TextArea.PreviewDragOver += OnPreviewDragOver;
        TextArea.PreviewDrop += OnPreviewDrop;
    }

    /// <inheritdoc/>
    public event EventHandler? TextEdited;

    /// <inheritdoc/>
    public event EventHandler<EditorTools>? ToolsChanged;

    /// <inheritdoc/>
    public event EventHandler<LinkInput?>? LinkInputChanged;

    /// <inheritdoc/>
    public event EventHandler<LinkQuestion>? LinkAsked;

    /// <inheritdoc/>
    event EventHandler<DiagramQuestion>? IEditorSurface.DiagramAsked
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    event EventHandler<FormulaQuestion>? IEditorSurface.FormulaAsked
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    event EventHandler? IEditorSurface.SaveAsked
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    event EventHandler<Key>? IEditorSurface.ListKeyPressed
    {
        add { }
        remove { }
    }

    /// <summary>Un autre programme tient le presse-papiers : rien n'a été collé.</summary>
    public event EventHandler? ClipboardBusy;

    /// <summary>Des fichiers d'image sont déposés ou collés, avec le point de dépôt.</summary>
    public event Action<IReadOnlyList<string>, int?>? ImageFilesDropped;

    /// <summary>Une image est collée depuis le presse-papiers.</summary>
    public event Action<byte[]>? ImagePasted;

    /// <summary>Les touches prises avant le champ.</summary>
    public Func<Key, ModifierKeys, bool>? KeyFilter { get; set; }

    /// <summary>Ce qui souligne les fautes.</summary>
    internal SpellingLayer SpellingLayer => _spellingLayer;

    /// <summary>Le correcteur ; sans lui, aucune faute n'est soulignée.</summary>
    public ISpelling? Spelling
    {
        get => _spellingLayer.Spelling;
        set => _spellingLayer.Spelling = value;
    }

    /// <summary>La couleur du curseur.</summary>
    public Brush? CaretBrush
    {
        get => (Brush?)GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    /// <summary>La couleur des titres et des liens.</summary>
    public Brush? EmphasisBrush
    {
        get => (Brush?)GetValue(EmphasisBrushProperty);
        set => SetValue(EmphasisBrushProperty, value);
    }

    /// <summary>La couleur des signes de la syntaxe.</summary>
    public Brush? SyntaxBrush
    {
        get => (Brush?)GetValue(SyntaxBrushProperty);
        set => SetValue(SyntaxBrushProperty, value);
    }

    /// <summary>La couleur des balises.</summary>
    public Brush? TagBrush
    {
        get => (Brush?)GetValue(TagBrushProperty);
        set => SetValue(TagBrushProperty, value);
    }

    /// <summary>La couleur des fautes.</summary>
    public Brush? MistakeBrush
    {
        get => (Brush?)GetValue(MistakeBrushProperty);
        set => SetValue(MistakeBrushProperty, value);
    }

    /// <inheritdoc/>
    string IEditorSurface.Text
    {
        get
        {
            RemoveCarriageReturns();
            return Document.Text;
        }
    }

    /// <inheritdoc/>
    Point IEditorSurface.InputPoint => CaretPoint;

    /// <summary>Le texte sélectionné, et où il est.</summary>
    private (int Start, int End) Range => (SelectionStart, SelectionStart + SelectionLength);

    /// <summary>La ligne du curseur.</summary>
    private string CaretLine => Document.GetText(Document.GetLineByNumber(TextArea.Caret.Line));

    /// <summary>Le curseur dans sa ligne.</summary>
    private int CaretColumn => TextArea.Caret.Offset - Document.GetLineByNumber(TextArea.Caret.Line).Offset;

    /// <summary>La position du curseur dans le champ, pour y ouvrir une liste.</summary>
    private Point CaretPoint
    {
        get
        {
            var location = TextArea.TextView.GetVisualPosition(TextArea.Caret.Position, ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineBottom);
            return TextArea.TextView.TranslatePoint(new Point(location.X - TextArea.TextView.ScrollOffset.X, location.Y - TextArea.TextView.ScrollOffset.Y), this);
        }
    }

    /// <summary>Fait un remplacement, comme une frappe.</summary>
    internal void Apply(TextChange change)
    {
        Document.Replace(change.Start, change.Length, change.Text);
        Select(change.SelectionStart, change.SelectionLength);
        TextArea.Caret.Offset = change.SelectionStart + change.SelectionLength;
        TextArea.Caret.BringCaretToView();
    }

    /// <inheritdoc/>
    Task IEditorSurface.SyncAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public void ReplaceAll(string text)
    {
        _silent = true;
        Document.Text = text;
        Document.UndoStack.ClearAll();
        TextArea.Caret.Offset = 0;
        _silent = false;
        FollowCaret();
    }

    /// <inheritdoc/>
    public void ToggleMark(InlineMark mark) => Edit((text, start, end) => MarkdownEdits.Toggle(text, start, end, mark));

    /// <inheritdoc/>
    public void SetHeading(int level) => Edit((text, start, end) => MarkdownEdits.Heading(text, start, end, level));

    /// <inheritdoc/>
    public void ToggleList(ListKind kind) => Edit((text, start, end) => MarkdownEdits.List(text, start, end, kind));

    /// <inheritdoc/>
    public void InsertDiagram(string info, string code, int? position) =>
        Edit((text, start, _) => MarkdownEdits.Block(text, start, $"```{info}\n{code}\n```"));

    /// <inheritdoc/>
    public void SetColor(string? color) => EditIfAny((text, start, end) => MarkdownEdits.Style(text, start, end, ColorProperty, color));

    /// <inheritdoc/>
    public void SetFontSize(string? size) => EditIfAny((text, start, end) => MarkdownEdits.Style(text, start, end, SizeProperty, size));

    /// <inheritdoc/>
    public void SetAlign(TextAlign align) => EditIfAny((text, start, end) => MarkdownEdits.Align(text, start, end, align));

    /// <inheritdoc/>
    public void ChangeCase(CaseStyle? style)
    {
        var (start, end) = Range;
        if (MarkdownEdits.Case(((IEditorSurface)this).Text, start, end, style) is { } change)
        {
            Apply(change);
        }
    }

    /// <inheritdoc/>
    public void InsertTable(int rows, int columns) => Edit((text, start, _) => MarkdownEdits.Block(text, start, MarkdownEdits.Table(rows, columns)));

    /// <inheritdoc/>
    public void InsertCode(string language) => Edit((text, start, end) => MarkdownEdits.Code(text, start, end, language));

    /// <inheritdoc/>
    public void InsertRule() => Edit((text, start, _) => MarkdownEdits.Rule(text, start));

    /// <inheritdoc/>
    public void AskFormula() => ToggleMark(InlineMark.Formula);

    /// <inheritdoc/>
    public void InsertFormula(string tex, bool block, int? position) =>
        Edit((text, start, end) => block ? MarkdownEdits.Block(text, position ?? start, $"$$\n{tex}\n$$") : new TextChange(start, end - start, $"${tex}$", start + tex.Length + 2, 0));

    /// <inheritdoc/>
    public void AskLink()
    {
        var (start, end) = Range;
        LinkAsked?.Invoke(this, new LinkQuestion(Document.GetText(start, end - start), string.Empty));
    }

    /// <inheritdoc/>
    public void InsertLink(string label, string address) => Edit((_, start, end) => MarkdownEdits.Link(start, end, label, address));

    /// <inheritdoc/>
    public void InsertImage(string link, string alternative, int? position) =>
        Edit((text, start, _) => MarkdownEdits.Block(text, position ?? start, $"![{alternative}]({link})"));

    /// <inheritdoc/>
    public void InsertSheetLink(string label, string address)
    {
        var text = ((IEditorSurface)this).Text;
        var caret = TextArea.Caret.Offset;
        var start = _inputStart ?? caret;
        var end = string.CompareOrdinal(text, caret, "]]", 0, 2) == 0 ? caret + 2 : caret;
        var link = SheetLinks.MarkdownLink(label, address);
        Apply(new TextChange(start, end - start, link, start + link.Length, 0));
    }

    /// <inheritdoc/>
    void IEditorSurface.SetProposalsOpen(bool open)
    {
    }

    /// <inheritdoc/>
    public void GoToHeading(string title, int occurrence)
    {
        var seen = 0;
        foreach (var line in Document.Lines)
        {
            var text = Document.GetText(line);
            if (!IsInFence(line.Offset) && text.TrimStart().StartsWith('#') && text.TrimStart().TrimStart('#').Trim() == title && seen++ == occurrence)
            {
                TextArea.Caret.Offset = line.Offset;
                TextArea.Caret.BringCaretToView();
                TextArea.Focus();
                return;
            }
        }
    }

    /// <inheritdoc/>
    void IEditorSurface.Undo() => Undo();

    /// <inheritdoc/>
    void IEditorSurface.Redo() => Redo();

    /// <inheritdoc/>
    void IEditorSurface.Focus() => TextArea.Focus();

    /// <summary>Indique qu'une position du texte est dans un bloc de code.</summary>
    internal bool IsInFence(int offset)
    {
        _fences ??= MarkdownLines.Fences(Document.Text);
        foreach (var fence in _fences)
        {
            if (offset >= fence.Start && offset <= fence.End)
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (KeyFilter?.Invoke(key, Keyboard.Modifiers) == true)
        {
            e.Handled = true;
        }
        else if (key == Key.Enter && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            e.Handled = true;
            InsertLineBreak();
        }
        else if (key == Key.Tab && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            e.Handled = true;
            TextArea.MoveFocus(new TraversalRequest(Keyboard.Modifiers == ModifierKeys.Shift ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
        }
        base.OnPreviewKeyDown(e);
    }

    private static void OnBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((MarkdownEditor)sender).TextArea.TextView.Redraw();

    private static bool IsImageFile(string path) => Path.GetExtension(path).TrimStart('.') is { Length: > 0 } extension
        && ((string[])["png", "jpg", "jpeg", "gif", "webp", "avif", "bmp", "svg"]).Contains(extension, StringComparer.OrdinalIgnoreCase);

    /// <summary>Une nouvelle ligne garde le retrait de la précédente, et toujours « \n ».</summary>
    private void InsertLineBreak()
    {
        var line = CaretLine;
        var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
        TextArea.Selection.ReplaceSelectionWithText("\n" + indent[..Math.Min(indent.Length, CaretColumn)]);
        TextArea.Caret.BringCaretToView();
    }

    /// <summary>Le premier retour à la ligne d'un texte d'une seule ligne suit Windows : le filet de sécurité le remet.</summary>
    private void RemoveCarriageReturns()
    {
        var text = Document.Text;
        for (var at = text.LastIndexOf('\r'); at >= 0; at = at == 0 ? -1 : text.LastIndexOf('\r', at - 1))
        {
            var pair = at + 1 < text.Length && text[at + 1] == '\n';
            Document.Replace(at, pair ? 2 : 1, "\n");
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        _fences = null;
        if (!_silent)
        {
            TextEdited?.Invoke(this, EventArgs.Empty);
            FollowCaret();
        }
    }

    private void Edit(Func<string, int, int, TextChange> edit)
    {
        var (start, end) = Range;
        Apply(edit(((IEditorSurface)this).Text, start, end));
        TextArea.Focus();
    }

    private void EditIfAny(Func<string, int, int, TextChange?> edit)
    {
        var (start, end) = Range;
        if (edit(((IEditorSurface)this).Text, start, end) is { } change)
        {
            Apply(change);
        }
        TextArea.Focus();
    }

    /// <summary>Le ruban suit le curseur, et la liste des fiches s'ouvre sur une saisie « [[… » hors du code.</summary>
    private void FollowCaret()
    {
        var text = Document.Text;
        var (selectionStart, selectionEnd) = Range;
        ToolsChanged?.Invoke(this, new EditorTools(false, false, false, false, false, false, MarkdownEdits.HeadingLevel(CaretLine, 0), null, CanUndo, CanRedo)
        {
            Color = MarkdownEdits.StyleAround(text, selectionStart, selectionEnd, ColorProperty),
            Size = MarkdownEdits.StyleAround(text, selectionStart, selectionEnd, SizeProperty),
            Align = MarkdownEdits.AlignAt(text, selectionStart),
        });
        var before = CaretLine[..CaretColumn];
        var input = SelectionLength == 0 ? SheetLinks.InputBefore(before) : null;
        if (input is not null && (MarkdownLines.IsInInlineCode(before) || IsInFence(TextArea.Caret.Offset)))
        {
            input = null;
        }
        _inputStart = input is { } typed ? TextArea.Caret.Offset - typed.Length : null;
        LinkInputChanged?.Invoke(this, input is { } found ? new LinkInput(found.Query, _inputStart!.Value) : null);
    }

    /// <summary>Le collage reste en fin de ligne « \n » ; une image collée va à côté de la fiche.</summary>
    private void ReplacePasteCommand()
    {
        var bindings = TextArea.DefaultInputHandler.Editing.CommandBindings;
        bindings.Remove(bindings.First(binding => binding.Command == ApplicationCommands.Paste));
        bindings.Add(new CommandBinding(ApplicationCommands.Paste, (_, _) => PasteFromClipboard(), (_, e) => e.CanExecute = true));
    }

    /// <summary>Un presse-papiers occupé par un autre programme n'est pas une panne : rien n'est collé.</summary>
    private void PasteFromClipboard()
    {
        try
        {
            if (Clipboard.GetDataObject() is { } data)
            {
                Paste(data);
            }
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            ClipboardBusy?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Colle du texte ou une image.</summary>
    internal void Paste(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsImageFile))
        {
            ImageFilesDropped?.Invoke([.. files.Where(IsImageFile)], null);
        }
        else if (data.GetData(DataFormats.UnicodeText) is string text)
        {
            TextArea.Selection.ReplaceSelectionWithText(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
            TextArea.Caret.BringCaretToView();
        }
        else if (data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            ImagePasted?.Invoke(stream.ToArray());
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsImageFile))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        var place = GetPositionFromPoint(e.GetPosition(this));
        e.Handled = DropImages(e.Data, place is { } position ? Document.GetOffset(position.Location) : null);
    }

    /// <summary>Dépose des images à un endroit du texte ; rend faux s'il n'y en a pas.</summary>
    internal bool DropImages(IDataObject data, int? position)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsImageFile))
        {
            ImageFilesDropped?.Invoke([.. files.Where(IsImageFile)], position);
            return true;
        }
        return false;
    }

    /// <summary>Le menu : les remplacements du mot visé s'il est faux, puis le presse-papiers.</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = TextArea.ContextMenu!;
        menu.Items.Clear();
        foreach (var item in MenuItemsAt(GetPositionFromPoint(Mouse.GetPosition(this)) is { } place ? Document.GetOffset(place.Location) : TextArea.Caret.Offset))
        {
            menu.Items.Add(item);
        }
    }

    /// <summary>Les éléments du menu à cet endroit du texte.</summary>
    internal IReadOnlyList<Control> MenuItemsAt(int offset)
    {
        var items = new List<Control>();
        var line = Document.GetLineByOffset(offset);
        if (Spelling is { } spelling && !IsInFence(offset) && TextCase.WordAround(Document.GetText(line), offset - line.Offset) is { } range)
        {
            var word = Document.GetText(line.Offset + range.Start, range.End - range.Start);
            // Le menu attend sa réponse, le temps d'une frappe : le correcteur vit sur les fils de travail.
            var correction = Task.Run(() => spelling.Correct(word)).GetAwaiter().GetResult();
            if (correction is not null)
            {
                foreach (var suggestion in correction.Suggestions)
                {
                    var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
                    item.Click += (_, _) =>
                    {
                        var start = line.Offset + range.Start + correction.Start;
                        Apply(new TextChange(start, correction.Length, suggestion, start + suggestion.Length, 0));
                    };
                    items.Add(item);
                }
                var add = new MenuItem { Header = EditingText.AddToDictionary };
                add.Click += (_, _) =>
                {
                    _ = Task.Run(() => spelling.Add(word)).ContinueWith(_ => Dispatcher.BeginInvoke(_spellingLayer.Forget), TaskScheduler.Default);
                };
                items.Add(add);
                items.Add(new Separator());
            }
        }
        items.Add(new MenuItem { Header = EditingText.Cut, Command = ApplicationCommands.Cut, CommandTarget = TextArea });
        items.Add(new MenuItem { Header = EditingText.Copy, Command = ApplicationCommands.Copy, CommandTarget = TextArea });
        items.Add(new MenuItem { Header = EditingText.Paste, Command = ApplicationCommands.Paste, CommandTarget = TextArea });
        return items;
    }
}
