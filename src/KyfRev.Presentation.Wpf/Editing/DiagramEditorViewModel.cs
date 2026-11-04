using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Un élément de la hiérarchie en cours d'écriture.</summary>
public sealed partial class DiagramNode : ObservableObject
{
    private const double LevelWidth = 20;

    /// <summary>Un élément à ce niveau, avec ce texte.</summary>
    public DiagramNode(string text, int level)
    {
        Text = text;
        Level = level;
    }

    /// <summary>Son texte.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Son niveau : 0 au sommet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indent))]
    public partial int Level { get; set; }

    /// <summary>C'est l'élément sur lequel portent les actions.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Son décalage dans la liste.</summary>
    public Thickness Indent => new(Level * LevelWidth, 0, 0, 0);
}

/// <summary>
/// L'éditeur d'une hiérarchie : la liste des éléments, décalés selon leur niveau, et leur dessin qui suit. Comme dans le volet de
/// texte d'un SmartArt, tout se fait dans la liste ; ajouter sous un élément ne touche que lui. Il n'y a qu'un sommet.
/// </summary>
public sealed partial class DiagramEditorViewModel : ObservableObject
{
    private DiagramEditorViewModel(string layout, string text, int? position)
    {
        Position = position;
        IsAcross = layout == SmartArt.HorizontalHierarchyId;
        foreach (var item in DiagramOutline.Parse(text))
        {
            Insert(Nodes.Count, new DiagramNode(item.Label, item.Level));
        }
        if (Nodes.Count == 0)
        {
            Insert(0, new DiagramNode(string.Empty, 0));
        }
        Select(Nodes[0]);
    }

    /// <summary>Un élément doit recevoir le focus : la liste vient de changer.</summary>
    internal event EventHandler<DiagramNode>? NodeToFocus;

    /// <summary>Le bloc qu'il remplace ; rien : il en insère un.</summary>
    public int? Position { get; }

    /// <summary>Les éléments, dans l'ordre : chacun est suivi de ce qui en dépend.</summary>
    public ObservableCollection<DiagramNode> Nodes { get; } = [];

    /// <summary>L'élément sur lequel portent les actions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HighlightedBox))]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand), nameof(AddSiblingCommand), nameof(IndentCommand), nameof(OutdentCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(DeleteCommand))]
    public partial DiagramNode? Selected { get; private set; }

    /// <summary>Le dessin va de gauche à droite, plutôt que de haut en bas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDown), nameof(Preview))]
    public partial bool IsAcross { get; set; }

    /// <summary>Le dessin va de haut en bas.</summary>
    public bool IsDown
    {
        get => !IsAcross;
        set => IsAcross = !value;
    }

    /// <summary>Le titre de la fenêtre.</summary>
    public string Title => Position is null ? EditingText.DiagramEditorNew : EditingText.DiagramEditorEdit;

    /// <summary>Le libellé du bouton qui valide.</summary>
    public string ConfirmLabel => Position is null ? EditingText.GalleryInsert : EditingText.DiagramEditorSave;

    /// <summary>Le dessin de ce qui est écrit.</summary>
    public DiagramScene? Preview => SmartArt.Draw(Layout, Code);

    /// <summary>La boîte de l'élément choisi dans le dessin ; rien s'il est vide.</summary>
    public int? HighlightedBox
    {
        get
        {
            if (Selected is not { } selected || string.IsNullOrWhiteSpace(selected.Text))
            {
                return null;
            }
            return Written.ToList().IndexOf(selected);
        }
    }

    private string Layout => IsAcross ? SmartArt.HorizontalHierarchyId : SmartArt.OrganizationChartId;

    private IEnumerable<DiagramNode> Written => Nodes.Where(node => !string.IsNullOrWhiteSpace(node.Text));

    private string Code => DiagramOutline.Write(Nodes.Select(node => new OutlineItem(node.Level, node.Text)));

    private int Current => Selected is { } node ? Nodes.IndexOf(node) : -1;

    /// <summary>Un éditeur pour un nouveau bloc, ou pour remplacer celui qui est à cet endroit.</summary>
    /// <param name="layout">Le nom du modèle dans le fichier.</param>
    /// <param name="text">Le contenu du bloc.</param>
    /// <param name="position">Où il se trouve ; rien : il est à insérer.</param>
    public static DiagramEditorViewModel For(string layout, string text, int? position) => new(layout, text, position);

    /// <summary>Un éditeur ouvert sur un bloc de la fiche ; rien si ce bloc n'est pas une hiérarchie.</summary>
    /// <param name="info">Ce qui suit les accents graves de son bloc.</param>
    /// <param name="code">Le contenu de son bloc.</param>
    /// <param name="position">Où il se trouve.</param>
    public static DiagramEditorViewModel? ForExisting(string info, string code, int position)
    {
        var parts = (info ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts is [SmartArt.Language, var layout] && SmartArt.IsHierarchy(layout) ? new(layout, code ?? string.Empty, position) : null;
    }

    /// <summary>Ce qui s'écrit dans le fichier ; rien si tout est vide.</summary>
    public (string Info, string Code)? Source() => Written.Any() ? SmartArt.Source(Layout, Code) : null;

    /// <summary>Choisit l'élément sur lequel portent les actions.</summary>
    public void Select(DiagramNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Selected?.IsSelected = false;
        node.IsSelected = true;
        Selected = node;
    }

    /// <summary>Choisit l'élément d'une boîte du dessin, et lui donne le focus.</summary>
    /// <param name="box">Le rang de la boîte.</param>
    public void SelectBox(int box)
    {
        var node = Written.ElementAtOrDefault(box);
        if (node is not null)
        {
            Focus(node);
        }
    }

    /// <summary>Les touches de la liste : rend vrai si la touche a servi.</summary>
    public bool TakeKey(Key key, ModifierKeys modifiers)
    {
        if (Selected is not { } node)
        {
            return false;
        }
        switch (key, modifiers)
        {
            case (Key.Enter, ModifierKeys.None):
                Run(CanAddSibling() ? AddSiblingCommand : AddChildCommand);
                return true;
            case (Key.Enter, ModifierKeys.Control):
                Run(AddChildCommand);
                return true;
            case (Key.Right, ModifierKeys.Alt):
                Run(IndentCommand);
                return true;
            case (Key.Left, ModifierKeys.Alt):
                Run(OutdentCommand);
                return true;
            case (Key.Up, ModifierKeys.Alt):
                Run(MoveUpCommand);
                return true;
            case (Key.Down, ModifierKeys.Alt):
                Run(MoveDownCommand);
                return true;
            case (Key.Up, ModifierKeys.None):
                FocusRow(Current - 1);
                return true;
            case (Key.Down, ModifierKeys.None):
                FocusRow(Current + 1);
                return true;
            case (Key.Back or Key.Delete, ModifierKeys.None) when node.Text.Length == 0 && CanDelete():
                Run(DeleteCommand);
                return true;
            default:
                return false;
        }
    }

    private static void Run(IRelayCommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private bool CanAddChild() => Selected is not null;

    /// <summary>Ajoute un élément sous celui-ci, et sous lui seul.</summary>
    [RelayCommand(CanExecute = nameof(CanAddChild))]
    private void AddChild()
    {
        var at = EndOf(Current);
        var node = new DiagramNode(string.Empty, Selected!.Level + 1);
        Insert(at, node);
        Changed();
        Focus(node);
    }

    private bool CanAddSibling() => Selected is { Level: > 0 };

    /// <summary>Ajoute un élément à côté de celui-ci, au même niveau. Le sommet reste seul.</summary>
    [RelayCommand(CanExecute = nameof(CanAddSibling))]
    private void AddSibling()
    {
        var at = EndOf(Current);
        var node = new DiagramNode(string.Empty, Selected!.Level);
        Insert(at, node);
        Changed();
        Focus(node);
    }

    private bool CanIndent() => Current >= 0 && PreviousSibling(Current) is not null;

    /// <summary>Le place sous l'élément qui le précède, avec ce qui en dépend.</summary>
    [RelayCommand(CanExecute = nameof(CanIndent))]
    private void Indent() => Shift(1);

    private bool CanOutdent() => Selected is { Level: > 1 };

    /// <summary>Le remonte d'un niveau, avec ce qui en dépend.</summary>
    [RelayCommand(CanExecute = nameof(CanOutdent))]
    private void Outdent() => Shift(-1);

    private bool CanMoveUp() => Current >= 0 && PreviousSibling(Current) is not null;

    /// <summary>L'échange avec l'élément d'avant, au même niveau.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        var from = Current;
        var count = EndOf(from) - from;
        var to = PreviousSibling(from)!.Value;
        for (var i = 0; i < count; i++)
        {
            Nodes.Move(from + i, to + i);
        }
        Changed();
        Focus(Selected!);
    }

    private bool CanMoveDown() => Current >= 0 && NextSibling(Current) is not null;

    /// <summary>L'échange avec l'élément d'après, au même niveau.</summary>
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        var from = Current;
        var next = NextSibling(from)!.Value;
        var count = EndOf(next) - next;
        for (var i = 0; i < count; i++)
        {
            Nodes.Move(next + i, from + i);
        }
        Changed();
        Focus(Selected!);
    }

    private bool CanDelete()
    {
        if (Selected is not { } node || Nodes.Count == 1)
        {
            return false;
        }
        // Le sommet ne s'en va que s'il laisse un seul élément pour le remplacer.
        return node.Level > 0 || Nodes.Skip(1).Count(other => other.Level == 1) <= 1;
    }

    /// <summary>Le retire ; ce qui en dépend remonte d'un niveau, rien d'autre ne se perd.</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        var at = Current;
        var end = EndOf(at);
        for (var i = at + 1; i < end; i++)
        {
            Nodes[i].Level--;
        }
        Nodes[at].PropertyChanged -= OnNodeChanged;
        Nodes.RemoveAt(at);
        Changed();
        Focus(Nodes[Math.Max(0, at - 1)]);
    }

    private void Shift(int levels)
    {
        var at = Current;
        var end = EndOf(at);
        for (var i = at; i < end; i++)
        {
            Nodes[i].Level += levels;
        }
        Changed();
        Focus(Selected!);
    }

    private void Insert(int at, DiagramNode node)
    {
        node.PropertyChanged += OnNodeChanged;
        Nodes.Insert(at, node);
    }

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramNode.Text))
        {
            Changed();
        }
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(HighlightedBox));
        AddChildCommand.NotifyCanExecuteChanged();
        AddSiblingCommand.NotifyCanExecuteChanged();
        IndentCommand.NotifyCanExecuteChanged();
        OutdentCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void Focus(DiagramNode node)
    {
        Select(node);
        NodeToFocus?.Invoke(this, node);
    }

    private void FocusRow(int row)
    {
        if (row >= 0 && row < Nodes.Count)
        {
            Focus(Nodes[row]);
        }
    }

    /// <summary>La ligne qui suit l'élément et ce qui en dépend.</summary>
    private int EndOf(int at)
    {
        var end = at + 1;
        while (end < Nodes.Count && Nodes[end].Level > Nodes[at].Level)
        {
            end++;
        }
        return end;
    }

    private int? PreviousSibling(int at)
    {
        for (var i = at - 1; i >= 0; i--)
        {
            if (Nodes[i].Level == Nodes[at].Level)
            {
                return i;
            }
            if (Nodes[i].Level < Nodes[at].Level)
            {
                return null;
            }
        }
        return null;
    }

    private int? NextSibling(int at)
    {
        var end = EndOf(at);
        return end < Nodes.Count && Nodes[end].Level == Nodes[at].Level ? end : null;
    }
}
