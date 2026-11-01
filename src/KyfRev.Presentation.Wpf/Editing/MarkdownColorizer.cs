using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using KyfRev.Application.Ports;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// La coloration du Markdown, ligne par ligne : elle guide l'œil sans jamais toucher au texte. Les couleurs sont
/// des rôles du design system, dont le contraste est vérifié dans chaque thème.
/// </summary>
internal sealed partial class MarkdownColorizer(MarkdownEditor editor) : DocumentColorizingTransformer
{
    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        if (text.Length == 0)
        {
            return;
        }
        if (editor.IsInFence(line.Offset))
        {
            Paint(line.Offset, line.Offset + line.Length, editor.SyntaxBrush);
            return;
        }
        if (HeadingLine().IsMatch(text))
        {
            ChangeLinePart(line.Offset, line.Offset + line.Length, element =>
            {
                if (editor.EmphasisBrush is { } brush)
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                }
                var face = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, face.Style, FontWeights.SemiBold, face.Stretch));
            });
            return;
        }
        PaintMatches(ListMarker(), text, line.Offset, editor.SyntaxBrush);
        PaintMatches(EmphasisSign(), text, line.Offset, editor.SyntaxBrush);
        PaintMatches(AllowedTag(), text, line.Offset, editor.TagBrush);
        foreach (Match link in LinkParts().Matches(text))
        {
            Paint(line.Offset + link.Index, line.Offset + link.Groups[1].Index, editor.SyntaxBrush);
            Paint(line.Offset + link.Groups[1].Index, line.Offset + link.Groups[1].Index + link.Groups[1].Length, editor.EmphasisBrush);
            Paint(line.Offset + link.Groups[1].Index + link.Groups[1].Length, line.Offset + link.Index + link.Length, editor.SyntaxBrush);
        }
    }

    private void PaintMatches(Regex regex, string text, int offset, Brush? brush)
    {
        foreach (Match match in regex.Matches(text))
        {
            Paint(offset + match.Index, offset + match.Index + match.Length, brush);
        }
    }

    private void Paint(int start, int end, Brush? brush)
    {
        if (brush is not null && end > start)
        {
            ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    [GeneratedRegex(@"^ {0,3}#{1,6}(?:[ \t]|$)")]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)]|>)(?=\s)")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"(?<!\\)(?:\*+|~~|`+)")]
    private static partial Regex EmphasisSign();

    [GeneratedRegex("</?(?:u|sup|sub)>")]
    private static partial Regex AllowedTag();

    [GeneratedRegex(@"!?\[([^\]\n]*)\]\([^)\n]*\)")]
    private static partial Regex LinkParts();
}

/// <summary>
/// Souligne les fautes des lignes visibles. Le correcteur travaille hors du fil de l'interface : une ligne encore
/// inconnue se souligne dès qu'il a répondu, et la frappe n'attend jamais. Le mot sous le curseur reste tranquille.
/// </summary>
internal sealed class SpellingLayer(MarkdownEditor editor) : IBackgroundRenderer
{
    private const int MaximumKnownLines = 4000;

    private readonly Dictionary<string, IReadOnlyList<SpellingMistake>> _known = new(StringComparer.Ordinal);
    private readonly HashSet<string> _waiting = new(StringComparer.Ordinal);
    private bool _checking;

    /// <summary>Le correcteur.</summary>
    public ISpelling? Spelling { get; set; }

    /// <inheritdoc/>
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Oublie ce qui est connu : le dictionnaire a changé.</summary>
    public void Forget()
    {
        _known.Clear();
        editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    /// <summary>Les fautes connues d'une ligne ; rien si le correcteur ne l'a pas encore lue.</summary>
    internal IReadOnlyList<SpellingMistake>? MistakesOf(string line) => _known.GetValueOrDefault(line);

    /// <inheritdoc/>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Spelling is null || editor.MistakeBrush is not { } brush || !textView.VisualLinesValid)
        {
            return;
        }
        var pen = new Pen(brush, 1);
        pen.Freeze();
        var caret = editor.TextArea.Caret.Offset;
        foreach (var visual in textView.VisualLines)
        {
            var line = visual.FirstDocumentLine;
            if (line.Length == 0 || editor.IsInFence(line.Offset))
            {
                continue;
            }
            var text = textView.Document.GetText(line);
            if (!_known.TryGetValue(text, out var mistakes))
            {
                _waiting.Add(text);
                continue;
            }
            foreach (var mistake in mistakes)
            {
                var start = line.Offset + mistake.Start;
                if (caret < start || caret > start + mistake.Length)
                {
                    DrawWave(textView, drawingContext, pen, start, mistake.Length);
                }
            }
        }
        if (_waiting.Count > 0 && !_checking)
        {
            _ = CheckWaitingAsync();
        }
    }

    private static void DrawWave(TextView textView, DrawingContext drawingContext, Pen pen, int start, int length)
    {
        const double step = 3;
        var segment = new TextSegment { StartOffset = start, Length = length };
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var y = rect.Bottom - 1;
                context.BeginFigure(new Point(rect.Left, y), false, false);
                var up = true;
                for (var x = rect.Left + step; x <= rect.Right + step; x += step)
                {
                    context.LineTo(new Point(Math.Min(x, rect.Right), up ? y - step / 2 : y), true, false);
                    up = !up;
                }
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }
    }

    /// <summary>Ce qui n'est pas du français (code, adresses, balises) ne compte pas comme une faute.</summary>
    private static List<SpellingMistake> Keep(string line, IReadOnlyList<SpellingMistake> mistakes)
    {
        var skipped = MarkdownLines.Unchecked(line).ToList();
        return [.. mistakes.Where(mistake => !skipped.Exists(range => mistake.Start < range.End && mistake.Start + mistake.Length > range.Start))];
    }

    private async Task CheckWaitingAsync()
    {
        _checking = true;
        var spelling = Spelling!;
        var lines = _waiting.ToList();
        _waiting.Clear();
        var found = await Task.Run(() => lines.Select(line => (Line: line, Mistakes: Keep(line, spelling.Check(line)))).ToList()).ConfigureAwait(true);
        if (_known.Count + found.Count > MaximumKnownLines)
        {
            _known.Clear();
        }
        foreach (var (line, mistakes) in found)
        {
            _known[line] = mistakes;
        }
        _checking = false;
        editor.TextArea.TextView.InvalidateLayer(Layer);
    }
}
