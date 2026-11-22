using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Le lien entre le document natif et le texte affiché : chaque morceau du document connaît sa position,
/// ce qui permet d'aller de l'un à l'autre, et de surligner sans reconstruire le document. Un morceau est
/// coupé aux bornes des passages, comme les <c>mark</c> de la 0.x ; le texte, lui, ne change pas.
/// </summary>
internal sealed class TextLayer(Func<string, Brush?> findBrush)
{
    private const string PassageBackground = "Kyf.Brush.PillPassageBackground";
    private const string ActiveBackground = "Kyf.Brush.HighlightActive";
    private const string ActiveText = "Kyf.Brush.OnHighlight";
    private const string PassageText = "Kyf.Brush.PillPassageText";
    private const string PassageMarker = "Kyf.Brush.MarkerPassage";
    private const string DefinitionMarker = "Kyf.Brush.MarkerSheet";
    private const string WordMarker = "Kyf.Brush.TextSecondary";
    private const int WordTipLength = 400;

    private static readonly DashStyle Dots = FrozenDots();
    private static readonly DashStyle WordDots = Frozen(new DashStyle([1, 3], 0));

    private readonly List<Segment> _segments = [];
    private readonly Dictionary<Run, Segment> _byRun = [];
    private readonly List<FormulaMark> _formulas = [];
    private IReadOnlyList<Highlight> _highlights = [];
    private string? _active;
    private IReadOnlyList<TextHole> _holes = [];
    private IReadOnlyList<DefinedWordMark> _words = [];

    private static DashStyle FrozenDots() => Frozen(new DashStyle([1, 2], 0));

    private static DashStyle Frozen(DashStyle dashes)
    {
        dashes.Freeze();
        return dashes;
    }

    /// <summary>Ajoute un morceau de texte, à sa position dans le texte affiché.</summary>
    public void AddRun(Run run, int start)
    {
        var segment = new Segment(run, start, run.Text.Length);
        _segments.Add(segment);
        _byRun[run] = segment;
        if (_highlights.Count > 0 || _words.Count > 0)
        {
            Apply(_segments.Count - 1);
        }
    }

    /// <summary>Ajoute une formule, qui compte dans le texte affiché pour sa source.</summary>
    public void AddFormula(TextElement element, Border slot, int start, int end)
    {
        var formula = new FormulaMark(element, slot, start, end);
        _formulas.Add(formula);
        Style(formula);
    }

    /// <summary>Change les surlignages et celui qui ressort.</summary>
    public void Set(IReadOnlyList<Highlight> highlights, string? active)
    {
        _highlights = highlights;
        _active = active;
        for (var i = 0; i < _segments.Count; i++)
        {
            i += Apply(i);
        }
        _formulas.ForEach(Style);
    }

    /// <summary>Change les mots définis ailleurs dans le coffre, marqués d'un pointillé discret.</summary>
    public void SetWords(IReadOnlyList<DefinedWordMark> words)
    {
        _words = words;
        for (var i = 0; i < _segments.Count; i++)
        {
            i += Apply(i);
        }
    }

    /// <summary>Le mot défini à l'endroit visé par la souris ; rien s'il n'y en a pas.</summary>
    public DefinedWordMark? WordAt(object? source) =>
        source is Run run && _byRun.TryGetValue(run, out var segment) ? WordOver(segment.Start, segment.End) : null;

    /// <summary>Change les trous encore masqués.</summary>
    public void SetHoles(IReadOnlyList<TextHole> holes)
    {
        _holes = holes;
        foreach (var segment in _segments)
        {
            Style(segment, Covering(segment.Start, segment.End));
        }
        _formulas.ForEach(Style);
    }

    /// <summary>Le trou masqué à l'endroit visé par la souris ; rien s'il n'y en a pas.</summary>
    public TextHole? HoleAt(object? source)
    {
        if (source is Run run && _byRun.TryGetValue(run, out var segment))
        {
            return HoleOver(segment.Start, segment.End);
        }
        return source is Visual target && _formulas.Find(candidate => candidate.Slot == target || candidate.Slot.IsAncestorOf(target)) is { } formula
            ? HoleOver(formula.Start, formula.End)
            : null;
    }

    /// <summary>L'élément du document où se trouve une position du texte affiché ; rien si elle n'est pas montrée.</summary>
    public TextElement? ElementAt(int position)
    {
        var segment = _segments.Find(candidate => candidate.End > position);
        var formula = _formulas.Find(candidate => candidate.End > position);
        return segment is not null && (formula is null || segment.Start < formula.Start) ? segment.Run : formula?.Element;
    }

    /// <summary>L'endroit du document d'une position du texte affiché ; rien si elle n'est pas montrée.</summary>
    /// <param name="position">La position.</param>
    /// <param name="isStart">C'est le début d'une sélection : il se cherche dans le texte qui suit ; sinon, dans celui qui précède.</param>
    public TextPointer? PointerAt(int position, bool isStart)
    {
        var segment = isStart
            ? _segments.Find(candidate => candidate.Start <= position && position < candidate.End)
            : _segments.Find(candidate => candidate.Start < position && position <= candidate.End);
        return segment?.Run.ContentStart.GetPositionAtOffset(position - segment.Start);
    }

    /// <summary>La position dans le texte affiché d'un endroit du document ; rien si aucun texte n'y mène.</summary>
    /// <param name="pointer">L'endroit du document.</param>
    /// <param name="isStart">C'est le début d'une sélection : on cherche vers la suite, sinon vers l'arrière.</param>
    public int? PositionOf(TextPointer pointer, bool isStart)
    {
        var direction = isStart ? LogicalDirection.Forward : LogicalDirection.Backward;
        for (var current = pointer; current is not null; current = current.GetNextContextPosition(direction))
        {
            if (current.Parent is Run run && _byRun.TryGetValue(run, out var segment))
            {
                return segment.Start + run.ContentStart.GetOffsetToPosition(current);
            }
            var adjacent = current.GetAdjacentElement(direction);
            if (_formulas.Find(candidate => ReferenceEquals(candidate.Element, adjacent)) is { } formula)
            {
                return isStart ? formula.Start : formula.End;
            }
        }
        return null;
    }

    /// <summary>Les annotations surlignées à l'endroit visé par la souris.</summary>
    public IReadOnlyList<string> IdsAt(object? source)
    {
        (int Start, int End)? range = null;
        if (source is Run run && _byRun.TryGetValue(run, out var segment))
        {
            range = (segment.Start, segment.End);
        }
        else if (source is Visual target && _formulas.Find(candidate => candidate.Slot == target || candidate.Slot.IsAncestorOf(target)) is { } formula)
        {
            range = (formula.Start, formula.End);
        }
        return range is { } found ? [.. Covering(found.Start, found.End).Select(highlight => highlight.Id)] : [];
    }

    private TextHole? HoleOver(int start, int end) =>
        _holes.FirstOrDefault(hole => hole.Start < end && hole.End > start);

    private DefinedWordMark? WordOver(int start, int end) =>
        _words.FirstOrDefault(word => word.Start < end && word.End > start);

    private List<Highlight> Covering(int start, int end) =>
        [.. _highlights.Where(highlight => highlight.Start < end && highlight.End > start)];

    /// <summary>Coupe un morceau aux bornes des surlignages et le met en forme ; rend le nombre de morceaux ajoutés.</summary>
    private int Apply(int index)
    {
        var segment = _segments[index];
        var covering = Covering(segment.Start, segment.End);
        var cuts = covering.SelectMany(highlight => new[] { highlight.Start, highlight.End })
            .Concat(_words.Where(word => word.Start < segment.End && word.End > segment.Start).SelectMany(word => new[] { word.Start, word.End }))
            .Where(cut => cut > segment.Start && cut < segment.End)
            .Distinct()
            .Order()
            .ToList();
        if (cuts.Count == 0)
        {
            Style(segment, covering);
            return 0;
        }
        var text = segment.Run.Text;
        var end = segment.End;
        segment.Run.Text = text[..(cuts[0] - segment.Start)];
        segment.Length = cuts[0] - segment.Start;
        Style(segment, Covering(segment.Start, segment.End));
        var previous = segment.Run;
        for (var i = 0; i < cuts.Count; i++)
        {
            var next = i + 1 < cuts.Count ? cuts[i + 1] : end;
            var run = new Run(text[(cuts[i] - segment.Start)..(next - segment.Start)]);
            previous.SiblingInlines.InsertAfter(previous, run);
            var added = new Segment(run, cuts[i], next - cuts[i]);
            _segments.Insert(index + i + 1, added);
            _byRun[run] = added;
            Style(added, Covering(added.Start, added.End));
            previous = run;
        }
        return cuts.Count;
    }

    private void Style(Segment segment, List<Highlight> covering)
    {
        var hidden = HoleOver(segment.Start, segment.End) is not null;
        // Un mot défini ailleurs ne se marque que hors des passages et des trous : il n'y ajouterait que du bruit.
        var word = covering.Count == 0 && !hidden ? WordOver(segment.Start, segment.End) : null;
        if (covering.Count == 0 && !segment.IsStyled && !hidden && word is null)
        {
            return;
        }
        var run = segment.Run;
        segment.IsStyled = covering.Count > 0 || hidden || word is not null;
        if (word is not null)
        {
            run.ToolTip = WordTip(word);
        }
        else
        {
            run.ClearValue(FrameworkContentElement.ToolTipProperty);
        }
        // Ce qui répond au clic le montre : un trou se découvre, un passage ressort dans le volet, un mot dit sa définition.
        if (covering.Count > 0 || hidden || word is not null)
        {
            run.Cursor = Cursors.Hand;
        }
        else
        {
            run.ClearValue(FrameworkContentElement.CursorProperty);
        }
        if (hidden)
        {
            // Le trou garde la largeur du texte, comme une ligne à compléter : le texte a la couleur du fond.
            run.SetResourceReference(TextElement.BackgroundProperty, PassageBackground);
            run.SetResourceReference(TextElement.ForegroundProperty, PassageBackground);
            run.TextDecorations = [HoleLine()];
            return;
        }
        if (covering.Exists(highlight => highlight.Id == _active))
        {
            // Le passage visé depuis le volet doit sauter aux yeux, dans les deux thèmes.
            run.SetResourceReference(TextElement.BackgroundProperty, ActiveBackground);
            run.SetResourceReference(TextElement.ForegroundProperty, ActiveText);
        }
        else if (covering.Exists(highlight => !highlight.IsDefinition && !highlight.IsCandidate))
        {
            run.SetResourceReference(TextElement.BackgroundProperty, PassageBackground);
            run.SetResourceReference(TextElement.ForegroundProperty, PassageText);
        }
        else
        {
            run.ClearValue(TextElement.BackgroundProperty);
            run.ClearValue(TextElement.ForegroundProperty);
        }
        var decorations = new TextDecorationCollection(covering.Select(Decoration));
        if (word is not null)
        {
            decorations.Add(WordLine());
        }
        if (decorations.Count > 0)
        {
            run.TextDecorations = decorations;
        }
        else
        {
            run.ClearValue(Inline.TextDecorationsProperty);
        }
    }

    private TextDecoration HoleLine()
    {
        var decoration = new TextDecoration { Location = TextDecorationLocation.Underline };
        if (findBrush(PassageMarker) is { } brush)
        {
            decoration.Pen = new Pen(brush, 2) { DashStyle = DashStyles.Dash };
        }
        return decoration;
    }

    /// <summary>Le pointillé d'un mot défini ailleurs : plus fin, plus espacé et plus discret que celui d'une définition posée ici.</summary>
    private TextDecoration WordLine()
    {
        var decoration = new TextDecoration { Location = TextDecorationLocation.Underline };
        if (findBrush(WordMarker) is { } brush)
        {
            decoration.Pen = new Pen(brush, 1) { DashStyle = WordDots, DashCap = PenLineCap.Round };
        }
        return decoration;
    }

    /// <summary>Ce que montre le survol d'un mot défini : le terme, son explication et sa fiche.</summary>
    private static ToolTip WordTip(DefinedWordMark word)
    {
        var explanation = word.Explanation.Length > WordTipLength
            ? string.Concat(word.Explanation.AsSpan(0, WordTipLength).TrimEnd(), "…")
            : word.Explanation;
        var panel = new StackPanel { MaxWidth = 380 };
        panel.Children.Add(new TextBlock { Text = word.Term, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
        var source = new TextBlock { Text = ReadingText.DefinedIn(word.SheetTitle), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        source.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Text.Caption");
        panel.Children.Add(source);
        return new ToolTip { Content = panel };
    }

    /// <summary>Un trait sous le texte : le fond ne suffit pas, il ne se voit pas en contraste élevé.</summary>
    private TextDecoration Decoration(Highlight highlight)
    {
        var decoration = new TextDecoration { Location = TextDecorationLocation.Underline };
        if (findBrush(highlight.IsDefinition ? DefinitionMarker : PassageMarker) is { } brush)
        {
            var pen = new Pen(brush, highlight.Id == _active ? 2.5 : 1.5);
            if (highlight.IsDefinition)
            {
                pen.DashStyle = Dots;
                pen.DashCap = PenLineCap.Round;
            }
            else if (highlight.IsCandidate)
            {
                pen.DashStyle = DashStyles.Dash;
            }
            decoration.Pen = pen;
        }
        return decoration;
    }

    private void Style(FormulaMark formula)
    {
        var covering = Covering(formula.Start, formula.End);
        var hidden = HoleOver(formula.Start, formula.End) is not null;
        if (formula.Slot.Child is { } drawing)
        {
            drawing.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        }
        if (hidden)
        {
            formula.Slot.SetResourceReference(Border.BackgroundProperty, PassageBackground);
            return;
        }
        if (covering.Exists(highlight => highlight.Id == _active))
        {
            formula.Slot.SetResourceReference(Border.BackgroundProperty, ActiveBackground);
        }
        else if (covering.Exists(highlight => !highlight.IsDefinition && !highlight.IsCandidate))
        {
            formula.Slot.SetResourceReference(Border.BackgroundProperty, PassageBackground);
        }
        else
        {
            formula.Slot.ClearValue(Border.BackgroundProperty);
        }
        if (covering.Count > 0)
        {
            formula.Slot.BorderThickness = new Thickness(0, 0, 0, covering.Exists(highlight => highlight.Id == _active) ? 2.5 : 1.5);
            formula.Slot.BorderBrush = findBrush(covering[0].IsDefinition ? DefinitionMarker : PassageMarker);
        }
        else
        {
            formula.Slot.ClearValue(Border.BorderThicknessProperty);
            formula.Slot.ClearValue(Border.BorderBrushProperty);
        }
    }

    private sealed class Segment(Run run, int start, int length)
    {
        public Run Run { get; } = run;

        public int Start { get; } = start;

        public int Length { get; set; } = length;

        public int End => Start + Length;

        public bool IsStyled { get; set; }
    }

    private sealed record FormulaMark(TextElement Element, Border Slot, int Start, int End);
}
