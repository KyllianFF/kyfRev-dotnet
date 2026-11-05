using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Un élément du dessin en cours d'écriture.</summary>
public sealed partial class DiagramNode : ObservableObject
{
    private const double LevelWidth = 20;

    /// <summary>Un élément à ce niveau, avec ce texte et ces valeurs.</summary>
    public DiagramNode(string text, int level, string value = "", string secondValue = "")
    {
        Text = text;
        Level = level;
        Value = value;
        SecondValue = secondValue;
    }

    /// <summary>Son texte.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>Son niveau : 0 au sommet, et pour tout élément d'une simple liste.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indent))]
    public partial int Level { get; set; }

    /// <summary>Sa valeur, dans un graphique.</summary>
    [ObservableProperty]
    public partial string Value { get; set; }

    /// <summary>Sa seconde valeur, dans un graphique qui en a deux.</summary>
    [ObservableProperty]
    public partial string SecondValue { get; set; }

    /// <summary>C'est l'élément sur lequel portent les actions.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Son décalage dans la liste.</summary>
    public Thickness Indent => new(Level * LevelWidth, 0, 0, 0);
}

/// <summary>
/// L'éditeur d'un dessin : la liste de ses éléments, et son dessin qui suit. Selon le modèle, c'est une simple liste, une hiérarchie où
/// ajouter sous un élément ne touche que lui, ou un tableau de valeurs. Comme dans le volet de texte d'un SmartArt, tout se fait dans la liste.
/// </summary>
public sealed partial class DiagramEditorViewModel : ObservableObject
{
    private DiagramEditorViewModel(GalleryKind kind, IReadOnlyList<GalleryItem> models, GalleryItem model, string text, int? position)
    {
        Kind = kind;
        Models = models;
        Position = position;
        SelectedModel = model;
        Load(text);
        Select(Nodes[0]);
    }

    /// <summary>Un élément doit recevoir le focus : la liste vient de changer.</summary>
    internal event EventHandler<DiagramNode>? NodeToFocus;

    /// <summary>Ce qui se dessine : un SmartArt, un schéma ou un graphique.</summary>
    public GalleryKind Kind { get; }

    /// <summary>Le bloc qu'il remplace ; rien : il en insère un.</summary>
    public int? Position { get; }

    /// <summary>Les modèles de la même famille, pour en changer sans rien perdre.</summary>
    public IReadOnlyList<GalleryItem> Models { get; }

    /// <summary>Les éléments, dans l'ordre : chacun est suivi de ce qui en dépend.</summary>
    public ObservableCollection<DiagramNode> Nodes { get; } = [];

    /// <summary>Le modèle choisi.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ConfirmLabel), nameof(Description), nameof(ShowsLevels), nameof(ShowsDirection), nameof(IsAcross), nameof(IsDown), nameof(ShowsSecondValue), nameof(AddLabel), nameof(KeysHelp), nameof(HighlightedBox))]
    public partial GalleryItem SelectedModel { get; set; }

    /// <summary>L'élément sur lequel portent les actions.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HighlightedBox))]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand), nameof(AddSiblingCommand), nameof(IndentCommand), nameof(OutdentCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(DeleteCommand))]
    public partial DiagramNode? Selected { get; private set; }

    /// <summary>Le code du dessin se montre sous l'aperçu.</summary>
    [ObservableProperty]
    public partial bool ShowsCode { get; set; }

    /// <summary>Le titre de la fenêtre.</summary>
    public string Title => Position is null ? EditingText.DiagramEditorNewOf(SelectedModel.Name) : EditingText.DiagramEditorEditOf(SelectedModel.Name);

    /// <summary>Le libellé du bouton qui valide.</summary>
    public string ConfirmLabel => Position is null ? EditingText.GalleryInsert : EditingText.DiagramEditorSave;

    /// <summary>À quoi sert le modèle.</summary>
    public string Description => SelectedModel.Description;

    /// <summary>Le dessin s'écrit en Mermaid : c'est la page cachée qui le fait.</summary>
    public bool IsMermaid => Kind != GalleryKind.SmartArt;

    /// <summary>Le dessin est fait par kyfRev.</summary>
    public bool IsDrawn => !IsMermaid;

    /// <summary>Le modèle a des niveaux.</summary>
    public bool ShowsLevels => SmartArt.IsHierarchy(SelectedModel.Id) || MermaidTemplates.IsHierarchy(SelectedModel.Id);

    /// <summary>Le modèle peut aller de haut en bas ou de gauche à droite.</summary>
    public bool ShowsDirection => SmartArt.IsHierarchy(SelectedModel.Id);

    /// <summary>Chaque élément porte une valeur.</summary>
    public bool ShowsValues => Kind == GalleryKind.Chart;

    /// <summary>Chaque élément porte deux valeurs.</summary>
    public bool ShowsSecondValue => MermaidTemplates.MeasureCount(SelectedModel.Id) == 2;

    /// <summary>Le dessin va de gauche à droite, plutôt que de haut en bas.</summary>
    public bool IsAcross
    {
        get => SelectedModel.Id == SmartArt.HorizontalHierarchyId;
        set => SelectedModel = Models.First(model => model.Id == (value ? SmartArt.HorizontalHierarchyId : SmartArt.OrganizationChartId));
    }

    /// <summary>Le dessin va de haut en bas.</summary>
    public bool IsDown
    {
        get => !IsAcross;
        set => IsAcross = !value;
    }

    /// <summary>Le libellé du bouton qui ajoute un élément à côté de celui-ci.</summary>
    public string AddLabel => ShowsLevels ? EditingText.DiagramAddSibling : ShowsValues ? EditingText.DiagramAddRow : EditingText.DiagramAddItem;

    /// <summary>Les touches de la liste.</summary>
    public string KeysHelp => ShowsDirection ? EditingText.DiagramKeys : ShowsLevels ? EditingText.DiagramKeysNoClick : EditingText.DiagramListKeys;

    /// <summary>Le dessin de ce qui est écrit, s'il est fait par kyfRev.</summary>
    public DiagramScene? Preview => IsDrawn ? SmartArt.Draw(SelectedModel.Id, Code) : null;

    /// <summary>Le code de ce qui est écrit, s'il s'écrit en Mermaid.</summary>
    public string? MermaidCode => IsMermaid ? MermaidTemplates.Code(SelectedModel.Id, Code) : null;

    /// <summary>La boîte de l'élément choisi dans le dessin ; rien s'il est vide, ou si le dessin n'en donne pas une par élément.</summary>
    public int? HighlightedBox
    {
        get
        {
            if (!ShowsDirection || Selected is not { } selected || string.IsNullOrWhiteSpace(selected.Text))
            {
                return null;
            }
            return Written.ToList().IndexOf(selected);
        }
    }

    /// <summary>Ce que le modèle ne pourra pas montrer ; rien si tout y tient.</summary>
    public string? LimitWarning
    {
        get
        {
            var limit = SmartArt.Layouts.FirstOrDefault(layout => layout.Id == SelectedModel.Id)?.Maximum;
            return limit is { } maximum && Written.Count() > maximum ? EditingText.DiagramLimitOf(maximum) : null;
        }
    }

    /// <summary>Une limite est dépassée.</summary>
    public bool HasLimitWarning => LimitWarning is not null;

    /// <summary>Une valeur ne se lit pas : elle comptera pour 1.</summary>
    public bool HasValueWarning =>
        ShowsValues && Written.Any(node => !MermaidTemplates.IsMeasure(node.Value) || (ShowsSecondValue && !MermaidTemplates.IsMeasure(node.SecondValue)));

    private IEnumerable<DiagramNode> Written => Nodes.Where(node => !string.IsNullOrWhiteSpace(node.Text));

    private string Code
    {
        get
        {
            if (ShowsValues)
            {
                return string.Join('\n', Written.Select(ValuesLine));
            }
            return ShowsLevels
                ? DiagramOutline.Write(Nodes.Select(node => new OutlineItem(node.Level, node.Text)))
                : string.Join('\n', Written.Select(node => node.Text.Trim()));
        }
    }

    private int Current => Selected is { } node ? Nodes.IndexOf(node) : -1;

    /// <summary>Un éditeur pour un nouveau bloc, ou pour remplacer celui qui est à cet endroit.</summary>
    /// <param name="model">Le nom du modèle dans le fichier.</param>
    /// <param name="text">Les éléments, un par ligne.</param>
    /// <param name="position">Où il se trouve ; rien : il est à insérer.</param>
    public static DiagramEditorViewModel For(string model, string text, int? position)
    {
        var kind = KindOf(model);
        var models = DiagramGalleryViewModel.ItemsOf(kind);
        return new(kind, models, models.First(item => item.Id == model), text, position);
    }

    /// <summary>Un éditeur ouvert sur un bloc de la fiche ; rien si ce bloc n'a pas été écrit par un modèle, ou a été retouché à la main.</summary>
    /// <param name="info">Ce qui suit les accents graves de son bloc.</param>
    /// <param name="code">Le contenu de son bloc.</param>
    /// <param name="position">Où il se trouve.</param>
    public static DiagramEditorViewModel? ForExisting(string info, string code, int position)
    {
        var parts = (info ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is [SmartArt.Language, var layout] && SmartArt.Layouts.Any(candidate => candidate.Id == layout))
        {
            return For(layout, code ?? string.Empty, position);
        }
        return parts is [MermaidTemplates.Language] && MermaidTemplates.ReadBack(code) is { } read ? For(read.Template, read.Text, position) : null;
    }

    /// <summary>Ce qui s'écrit dans le fichier ; rien si tout est vide.</summary>
    public (string Info, string Code)? Source()
    {
        if (!Written.Any())
        {
            return null;
        }
        return IsDrawn ? SmartArt.Source(SelectedModel.Id, Code) : MermaidTemplates.Source(SelectedModel.Id, Code);
    }

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
        var node = ShowsDirection ? Written.ElementAtOrDefault(box) : null;
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

    private static GalleryKind KindOf(string model) =>
        MermaidTemplates.All.FirstOrDefault(template => template.Id == model) is { } mermaid
            ? (mermaid.Family == MermaidFamily.Chart ? GalleryKind.Chart : GalleryKind.Schema)
            : GalleryKind.SmartArt;

    private static void Run(IRelayCommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>Changer de modèle garde ce qui est écrit, comme dans Office ; seuls les niveaux s'adaptent.</summary>
    partial void OnSelectedModelChanged(GalleryItem value)
    {
        if (ShowsLevels && Nodes.Count > 1 && Nodes.All(node => node.Level == 0))
        {
            foreach (var node in Nodes.Skip(1))
            {
                node.Level = 1;
            }
        }
        else if (!ShowsLevels)
        {
            foreach (var node in Nodes)
            {
                node.Level = 0;
            }
        }
        Changed();
    }

    partial void OnShowsCodeChanged(bool value) => OnPropertyChanged(nameof(MermaidCode));

    private void Load(string text)
    {
        if (ShowsValues)
        {
            foreach (var line in SmartArt.Items(text))
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                Insert(Nodes.Count, new DiagramNode(parts[0], 0, parts.Length > 1 ? parts[1] : string.Empty, parts.Length > 2 ? parts[2] : string.Empty));
            }
        }
        else if (ShowsLevels)
        {
            foreach (var item in DiagramOutline.Parse(text))
            {
                Insert(Nodes.Count, new DiagramNode(item.Label, item.Level));
            }
        }
        else
        {
            foreach (var item in SmartArt.Items(text))
            {
                Insert(Nodes.Count, new DiagramNode(item, 0));
            }
        }
        if (Nodes.Count == 0)
        {
            Insert(0, new DiagramNode(string.Empty, 0));
        }
    }

    /// <summary>« libellé : valeur : valeur » : un deux-points dans un libellé ne doit pas se lire comme une séparation.</summary>
    private string ValuesLine(DiagramNode node)
    {
        var label = node.Text.Replace(':', ' ').Trim();
        return ShowsSecondValue ? $"{label} : {node.Value.Trim()} : {node.SecondValue.Trim()}" : $"{label} : {node.Value.Trim()}";
    }

    private bool CanAddChild() => ShowsLevels && Selected is not null;

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

    private bool CanAddSibling() => Selected is { } node && (!ShowsLevels || node.Level > 0);

    /// <summary>Ajoute un élément à côté de celui-ci, au même niveau. Le sommet d'une hiérarchie reste seul.</summary>
    [RelayCommand(CanExecute = nameof(CanAddSibling))]
    private void AddSibling()
    {
        var at = EndOf(Current);
        var node = new DiagramNode(string.Empty, Selected!.Level);
        Insert(at, node);
        Changed();
        Focus(node);
    }

    private bool CanIndent() => ShowsLevels && Current >= 0 && PreviousSibling(Current) is not null;

    /// <summary>Le place sous l'élément qui le précède, avec ce qui en dépend.</summary>
    [RelayCommand(CanExecute = nameof(CanIndent))]
    private void Indent() => Shift(1);

    private bool CanOutdent() => ShowsLevels && Selected is { Level: > 1 };

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
        // Le sommet d'une hiérarchie ne s'en va que s'il laisse un seul élément pour le remplacer.
        return !ShowsLevels || node.Level > 0 || Nodes.Skip(1).Count(other => other.Level == 1) <= 1;
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
        if (e.PropertyName is nameof(DiagramNode.Text) or nameof(DiagramNode.Value) or nameof(DiagramNode.SecondValue))
        {
            Changed();
        }
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(MermaidCode));
        OnPropertyChanged(nameof(HighlightedBox));
        OnPropertyChanged(nameof(LimitWarning));
        OnPropertyChanged(nameof(HasLimitWarning));
        OnPropertyChanged(nameof(HasValueWarning));
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
