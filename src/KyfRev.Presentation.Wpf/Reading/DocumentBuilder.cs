using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Rendering;
using KyfRev.Rendering.Diagrams;
using WpfMath.Controls;
using WpfMath.Parsers;
using XamlMath.Exceptions;
using LinkTarget = KyfRev.Rendering.LinkTarget;
using Typography = KyfRev.DesignSystem.Tokens.Typography;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>Un bloc du texte, et ce qu'il donne à voix haute.</summary>
/// <param name="Block">Le bloc.</param>
/// <param name="Text">Ce qui se dit.</param>
internal sealed record SpokenBlock(Block Block, string Text);

/// <summary>
/// Tire un document natif du contenu d'une fiche. Le texte de chaque nœud reste dans son propre morceau,
/// de la même longueur : c'est ce qui permet de passer d'une position du texte affiché à l'écran. Le
/// contenu n'exécute jamais rien : ni HTML, ni script, et un lien ne navigue que par l'action reçue.
/// Un document natif calcule toute sa mise en page d'un coup : le début de la fiche paraît donc tout de
/// suite, et la suite s'ajoute juste après, entre deux gestes de l'utilisateur, quelle que soit sa longueur.
/// </summary>
internal sealed class DocumentBuilder
{
    /// <summary>
    /// Au-delà, les blocs et le texte en ligne ne s'imbriquent plus : le contenu reste entier, et une fiche
    /// piégée ne peut pas épuiser la mise en page.
    /// </summary>
    private const int MaxBlockDepth = 12;

    private const int MaxInlineDepth = 24;

    private const double ImageMaxHeight = 560;

    private const int ImageMaxPixels = 2400;

    /// <summary>Ce qui paraît au premier affichage, en caractères : plusieurs écrans.</summary>
    private const int FirstCharacters = 12000;

    private const int CharactersPerTurn = 6000;

    /// <summary>Un bloc coûte plus que son texte.</summary>
    private const int ElementWeight = 40;

    /// <summary>Les formules dessinées avant le premier affichage ; les suivantes le sont juste après, peu à peu.</summary>
    private const int FormulasAtOnce = 6;

    private const int FormulasPerTurn = 8;

    private const string Text = "Kyf.Brush.Text";
    private const string TextSecondary = "Kyf.Brush.TextSecondary";
    private const string SurfaceAlt = "Kyf.Brush.SurfaceAlt";
    private const string Divider = "Kyf.Brush.Divider";
    private const string LinkBrush = "Kyf.Brush.Link";
    private const string CodeFont = "Kyf.Font.Code";
    private const string DisplayFont = "Kyf.Font.Display";
    private const string UiFont = "Kyf.Font.Ui";

    /// <summary>« [ ] », avec son espace.</summary>
    private const int TaskMarkLength = 4;

    private const double TaskBoxSize = 15;

    private const double DiagramWidth = 640;

    private const double ListIndent = 24;

    private readonly SheetReading _reading;
    private readonly Action<LinkTarget> _follow;
    private readonly bool _opensLinks;
    private readonly bool _showsHeader;
    private readonly List<Task> _images = [];
    private readonly Queue<(Border Slot, string Source, double Size)> _pendingFormulas = new();
    private readonly HashSet<RenderText> _taskTexts = [];
    private readonly Dictionary<RenderNode, TextPiece> _pieces;
    private readonly HashSet<Paragraph> _unspoken = [];
    private FlowDocument? _document;
    private Paragraph? _openRootParagraph;
    private int _nextRootChild;
    private int _formulas;
    private bool _isScheduled;
    private bool _isCancelled;
    private bool _afterBreak;
    private readonly ReadingComfort _comfort;
    private double _inlineSize;
    private int _boldDepth;

    public DocumentBuilder(SheetReading reading, Action<LinkTarget> follow, bool opensLinks, bool showsHeader, Func<string, Brush?>? findBrush = null, ReadingComfort? comfort = null)
    {
        _comfort = comfort ?? ReadingComfort.Default;
        _inlineSize = _comfort.Size;
        _reading = reading;
        _pieces = reading.Map.Pieces.ToDictionary(piece => piece.Node);
        Layer = new TextLayer(findBrush ?? (_ => null));
        _follow = follow;
        _opensLinks = opensLinks;
        _showsHeader = showsHeader;
    }

    /// <summary>Le paragraphe de chaque titre.</summary>
    public Dictionary<RenderElement, Paragraph> Headings { get; } = [];

    /// <summary>L'endroit de chaque identifiant (notes et renvois).</summary>
    public Dictionary<string, TextElement> Identified { get; } = new(StringComparer.Ordinal);

    /// <summary>Les étendues de texte en gras, dans le texte affiché.</summary>
    public List<(int Start, int End)> BoldRanges { get; } = [];

    /// <summary>Ce qui fait une image d'un SVG ; rien sans le moteur web.</summary>
    public IMermaidEngine? Pictures { get; init; }

    /// <summary>Les schémas se dessinent ; sinon, ils se lisent en code.</summary>
    public bool DrawsMermaid { get; init; }

    /// <summary>Les blocs que dessinent les extensions actives ; sans eux, ces blocs restent du code.</summary>
    public ExtensionBlocks? ExtensionBlocks { get; init; }

    /// <summary>Le lien entre le document et le texte affiché, et les surlignages.</summary>
    public TextLayer Layer { get; }

    /// <summary>Il reste du contenu à ajouter ou des formules à dessiner.</summary>
    public bool HasPendingWork => HasPendingContent || _pendingFormulas.Count > 0;

    /// <summary>Se termine quand les images demandées sont chargées.</summary>
    public Task Images => Task.WhenAll(_images);

    private bool HasPendingContent => _nextRootChild < _reading.Root.Children.Count;

    public FlowDocument Build()
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0, 4, 0, 24),
            TextAlignment = TextAlignment.Left,
            IsHyphenationEnabled = false,
            IsOptimalParagraphEnabled = false,
            FontSize = _comfort.Size,
            LineHeight = _comfort.Size * _comfort.LineSpacing,
            LineStackingStrategy = LineStackingStrategy.MaxHeight,
        };
        document.SetResourceReference(TextElement.ForegroundProperty, Text);
        if (_comfort.Family is { } family)
        {
            document.FontFamily = family;
        }
        else
        {
            document.SetResourceReference(TextElement.FontFamilyProperty, UiFont);
        }
        if (_showsHeader)
        {
            AddHeader(document.Blocks);
        }
        _document = document;
        AppendContent(FirstCharacters);
        ScheduleNextTurn();
        return document;
    }

    /// <summary>Ajoute tout de suite ce qui reste du contenu : il faut aller quelque part dans la suite.</summary>
    public void Complete() => AppendContent(int.MaxValue);

    /// <summary>Ce document n'est plus montré : plus rien ne s'y ajoute.</summary>
    public void Cancel() => _isCancelled = true;

    private static bool IsBlock(RenderElement element) => element.Tag is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6"
        or "ul" or "ol" or "blockquote" or "pre" or "table" or "hr" or "section" or "div";

    private static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);

    private static int Weight(RenderNode node) => node is RenderText text
        ? text.Value.Length
        : ElementWeight + ((RenderElement)node).Children.Sum(Weight);

    private double HeadingSize(int level) => level switch
    {
        1 => 28,
        2 => Typography.Subtitle.Size + 2,
        3 => Typography.Subtitle.Size - 2,
        _ => _comfort.Size,
    };

    /// <summary>Lit une image sans le fil de l'interface, en plafonnant sa taille en mémoire.</summary>
    private static BitmapImage Decode(byte[] content)
    {
        using var probe = new MemoryStream(content);
        var width = BitmapDecoder.Create(probe, BitmapCreateOptions.None, BitmapCacheOption.None).Frames[0].PixelWidth;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = new MemoryStream(content);
        if (width > ImageMaxPixels)
        {
            bitmap.DecodePixelWidth = ImageMaxPixels;
        }
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[]? EmbeddedContent(string data)
    {
        const string marker = ";base64,";
        var start = data.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }
        var buffer = new byte[data.Length];
        return Convert.TryFromBase64String(data[(start + marker.Length)..], buffer, out var written) ? buffer[..written] : null;
    }

    private static Paragraph NewParagraph() => new() { Margin = new Thickness(0, 0, 0, 12) };

    /// <summary>Ce qui se lit à voix haute, dans l'ordre : ni le code, ni les dessins, ni ce qui entoure la fiche.</summary>
    public IReadOnlyList<SpokenBlock> SpokenBlocks()
    {
        Complete();
        var spoken = new List<SpokenBlock>();
        CollectSpoken(_document!.Blocks, spoken);
        return spoken;
    }

    private void CollectSpoken(IEnumerable<Block> blocks, List<SpokenBlock> spoken)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph when !_unspoken.Contains(paragraph):
                    var text = new StringBuilder();
                    AppendSpoken(paragraph.Inlines, text);
                    var said = string.Join(' ', text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                    if (said.Length >= 2)
                    {
                        spoken.Add(new SpokenBlock(paragraph, said));
                    }
                    break;
                case Section section:
                    CollectSpoken(section.Blocks, spoken);
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                    {
                        CollectSpoken(item.Blocks, spoken);
                    }
                    break;
                case Table table:
                    foreach (var cell in table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells))
                    {
                        CollectSpoken(cell.Blocks, spoken);
                    }
                    break;
            }
        }
    }

    /// <summary>Le texte d'un bloc tel qu'il se dit : les formules et les images y sont nommées.</summary>
    private static void AppendSpoken(InlineCollection inlines, StringBuilder text)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    text.Append(run.Text);
                    break;
                case Span span:
                    AppendSpoken(span.Inlines, text);
                    break;
                case LineBreak:
                    text.Append(' ');
                    break;
                case InlineUIContainer { Child: Image image }:
                    var alternative = AutomationProperties.GetName(image);
                    text.Append(' ').Append(ReadingText.SpokenImage);
                    text.Append(alternative.Length > 0 ? $" : {alternative} " : " ");
                    break;
                case InlineUIContainer { Child: Border }:
                    text.Append(' ').Append(ReadingText.SpokenFormula).Append(' ');
                    break;
                case InlineUIContainer { Child: { } child }:
                    text.Append(' ').Append(AutomationProperties.GetName(child)).Append(' ');
                    break;
            }
        }
    }

    private void AddHeader(BlockCollection blocks)
    {
        if (_reading.Details.Length > 0)
        {
            var details = new Paragraph(new Run(_reading.Details)) { Margin = new Thickness(0, 0, 0, 4), FontSize = Typography.Caption.Size };
            _unspoken.Add(details);
            details.SetResourceReference(TextElement.ForegroundProperty, TextSecondary);
            blocks.Add(details);
        }
        var title = new Paragraph(new Run(_reading.Title))
        {
            Margin = new Thickness(0, 0, 0, 16),
            FontSize = Typography.Title.Size,
            FontWeight = FontWeights.SemiBold,
            LineHeight = Typography.Title.LineHeight,
        };
        title.SetResourceReference(TextElement.FontFamilyProperty, DisplayFont);
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1);
        blocks.Add(title);
    }

    private void AppendContent(int characters)
    {
        var children = _reading.Root.Children;
        var added = 0;
        while (added < characters && _nextRootChild < children.Count)
        {
            var child = children[_nextRootChild++];
            AddChild(child, _document!.Blocks, 0, ref _openRootParagraph);
            added += Weight(child);
        }
    }

    private void ScheduleNextTurn()
    {
        if (!_isScheduled && !_isCancelled && HasPendingWork)
        {
            _isScheduled = true;
            _document!.Dispatcher.BeginInvoke(DispatcherPriority.Background, NextTurn);
        }
    }

    private void NextTurn()
    {
        _isScheduled = false;
        if (_isCancelled)
        {
            return;
        }
        if (HasPendingContent)
        {
            AppendContent(CharactersPerTurn);
        }
        else
        {
            DrawPendingFormulas();
        }
        ScheduleNextTurn();
    }

    /// <summary>Le contenu d'un conteneur : ses blocs, et un paragraphe pour le texte qui n'est dans aucun.</summary>
    private void AddBlocks(RenderElement parent, BlockCollection blocks, int depth)
    {
        Paragraph? open = null;
        foreach (var child in parent.Children)
        {
            AddChild(child, blocks, depth, ref open);
        }
    }

    private void AddChild(RenderNode child, BlockCollection blocks, int depth, ref Paragraph? open)
    {
        if (child is RenderElement element && IsBlock(element))
        {
            open = null;
            AddBlock(element, blocks, depth);
        }
        else if (open is not null || child is not RenderText text || !IsBlank(text.Value))
        {
            if (open is null)
            {
                open = NewParagraph();
                open.Margin = new Thickness(0, 0, 0, 4);
                blocks.Add(open);
            }
            AddInline(child, open.Inlines, 0, false);
        }
    }

    private void AddBlock(RenderElement element, BlockCollection blocks, int depth)
    {
        switch (element.Tag)
        {
            case "p":
                var paragraph = NewParagraph();
                AddInlines(element, paragraph.Inlines, 0, false);
                blocks.Add(paragraph);
                break;
            case "ul" or "ol":
                AddList(element, blocks, depth);
                break;
            case "blockquote":
                AddQuote(element, blocks, depth);
                break;
            case "pre":
                AddCode(element, blocks);
                break;
            case "table":
                blocks.Add(BuildTable(element));
                break;
            case "hr":
                blocks.Add(BuildRule());
                break;
            case "section":
                var notes = new Section { FontSize = Typography.Body.Size };
                AddBlocks(element, notes.Blocks, depth + 1);
                blocks.Add(notes);
                break;
            case "div" when element.Alignment is { } alignment:
                AddAligned(element, alignment, blocks, depth);
                break;
            case "div":
                blocks.Add(BuildFormulaBlock(element));
                break;
            default:
                AddHeading(element, blocks);
                break;
        }
    }

    private void AddAligned(RenderElement element, string alignment, BlockCollection blocks, int depth)
    {
        if (depth >= MaxBlockDepth)
        {
            AddBlocks(element, blocks, depth);
            return;
        }
        var section = new Section
        {
            TextAlignment = alignment switch
            {
                "center" => TextAlignment.Center,
                "right" => TextAlignment.Right,
                _ => TextAlignment.Justify,
            },
        };
        AddBlocks(element, section.Blocks, depth + 1);
        blocks.Add(section);
    }

    private void AddHeading(RenderElement element, BlockCollection blocks)
    {
        // Le titre de la page le dit déjà ; il reste compté dans le texte affiché.
        if (element.IsRepeatedTitle)
        {
            return;
        }
        var level = element.HeadingLevel;
        var size = HeadingSize(level);
        var heading = new Paragraph
        {
            Margin = new Thickness(0, level <= 2 ? 24 : 16, 0, 8),
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            LineHeight = size * 1.3,
            KeepWithNext = true,
        };
        heading.SetResourceReference(TextElement.FontFamilyProperty, DisplayFont);
        AutomationProperties.SetHeadingLevel(heading, level switch
        {
            1 => AutomationHeadingLevel.Level2,
            2 => AutomationHeadingLevel.Level3,
            3 => AutomationHeadingLevel.Level4,
            4 => AutomationHeadingLevel.Level5,
            5 => AutomationHeadingLevel.Level6,
            _ => AutomationHeadingLevel.Level7,
        });
        _inlineSize = size;
        AddInlines(element, heading.Inlines, 0, false);
        _inlineSize = _comfort.Size;
        Headings[element] = heading;
        blocks.Add(heading);
    }

    private void AddList(RenderElement element, BlockCollection blocks, int depth)
    {
        var items = element.Children.OfType<RenderElement>().Where(child => child.Tag == "li").ToList();
        if (depth >= MaxBlockDepth)
        {
            foreach (var item in items)
            {
                AddBlocks(item, blocks, depth);
            }
            return;
        }
        var list = new List
        {
            MarkerStyle = element.Tag == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 12),
            // La puce grandit avec le texte : à retrait fixe, elle serait coupée à gauche.
            Padding = new Thickness(ListIndent * _comfort.Size / ReadingComfort.Default.Size, 0, 0, 0),
        };
        if (element.ListStart is { } start)
        {
            list.StartIndex = Math.Max(start, 1);
        }
        if (items.TrueForAll(item => item.Task is not null))
        {
            // Seulement des tâches : la case prend la place de la puce.
            list.MarkerStyle = TextMarkerStyle.None;
            list.Padding = new Thickness(4, 0, 0, 0);
        }
        foreach (var item in items)
        {
            var entry = new ListItem();
            if (item.Task is not null && FirstText(item) is { } written)
            {
                _taskTexts.Add(written);
            }
            AddBlocks(item, entry.Blocks, depth + 1);
            if (entry.Blocks.Count == 0)
            {
                entry.Blocks.Add(new Paragraph { Margin = default });
            }
            if (item.Task is { } done)
            {
                MarkTask(entry, done);
            }
            if (item.Identifier is { } identifier)
            {
                Identified[identifier] = entry;
            }
            list.ListItems.Add(entry);
        }
        blocks.Add(list);
    }

    /// <summary>Le premier texte d'un élément : celui où est écrite la case d'une tâche.</summary>
    private static RenderText? FirstText(RenderElement element)
    {
        foreach (var child in element.Children)
        {
            switch (child)
            {
                case RenderText text when !IsBlank(text.Value):
                    return text;
                case RenderElement inner when FirstText(inner) is { } found:
                    return found;
            }
        }
        return null;
    }

    /// <summary>La case d'une tâche devant son texte ; faite, le texte est barré.</summary>
    private static void MarkTask(ListItem entry, bool done)
    {
        if (entry.Blocks.FirstBlock is not Paragraph paragraph)
        {
            return;
        }
        if (done)
        {
            // Le texte seul : la case reste nette.
            foreach (var inline in paragraph.Inlines)
            {
                inline.TextDecorations = TextDecorations.Strikethrough;
                inline.SetResourceReference(TextElement.ForegroundProperty, TextSecondary);
            }
        }
        // Une case de la hauteur de sa police, posée sur la ligne : elle ne l'élargit pas.
        var box = new Icon
        {
            Text = done ? Icons.TaskDone : Icons.TaskOpen,
            FontSize = TaskBoxSize,
            LineHeight = TaskBoxSize,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Margin = new Thickness(0, 0, 6, -2),
        };
        if (done)
        {
            box.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.Primary");
        }
        AutomationProperties.SetName(box, done ? ReadingText.TaskDone : ReadingText.TaskOpen);
        var mark = new InlineUIContainer(box);
        if (paragraph.Inlines.FirstInline is { } first)
        {
            paragraph.Inlines.InsertBefore(first, mark);
        }
        else
        {
            paragraph.Inlines.Add(mark);
        }
    }

    private void AddQuote(RenderElement element, BlockCollection blocks, int depth)
    {
        if (depth >= MaxBlockDepth)
        {
            AddBlocks(element, blocks, depth);
            return;
        }
        var quote = new Section
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(16, 0, 0, 0),
            BorderThickness = new Thickness(3, 0, 0, 0),
        };
        quote.SetResourceReference(Block.BorderBrushProperty, Divider);
        quote.SetResourceReference(TextElement.ForegroundProperty, TextSecondary);
        AddBlocks(element, quote.Blocks, depth + 1);
        blocks.Add(quote);
    }

    private void AddCode(RenderElement element, BlockCollection blocks)
    {
        var code = element.Children.OfType<RenderElement>().First();
        var value = code.Children.OfType<RenderText>().FirstOrDefault();
        var text = value?.Value.TrimEnd('\n') ?? string.Empty;
        if (ExtensionBlocks?.Draws(code.Language) == true)
        {
            var view = new ExtensionBlockView { BlockLanguage = code.Language!, Source = text, MaxWidth = DiagramWidth };
            blocks.Add(new BlockUIContainer(view) { Margin = new Thickness(0, 4, 0, 16) });
            return;
        }
        if (code.Language == SmartArt.Language && SmartArt.Draw(code.CodeArguments, text) is { } scene)
        {
            var drawing = new DiagramView { Scene = scene, MaxWidth = DiagramWidth };
            blocks.Add(new BlockUIContainer(drawing) { Margin = new Thickness(0, 4, 0, 16) });
            return;
        }
        if (code.Language == MermaidTemplates.Language && DrawsMermaid)
        {
            var view = new MermaidView { Code = text, MaxWidth = DiagramWidth };
            blocks.Add(new BlockUIContainer(view) { Margin = new Thickness(0, 4, 0, 16) });
            return;
        }
        if (code.Language == NetworkBlock.Language && DrawsMermaid)
        {
            var view = new NetworkView { Source = text, MaxWidth = DiagramWidth };
            blocks.Add(new BlockUIContainer(view) { Margin = new Thickness(0, 4, 0, 16) });
            return;
        }
        if (code.Language is "mermaid" or NetworkBlock.Language)
        {
            var note = new Paragraph(new Run(ReadingText.DiagramAsText)) { Margin = new Thickness(0, 0, 0, 4), FontSize = Typography.Caption.Size };
            note.SetResourceReference(TextElement.ForegroundProperty, TextSecondary);
            _unspoken.Add(note);
            blocks.Add(note);
        }
        var block = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(12, 8, 12, 8),
            FontSize = Typography.Body.Size,
            LineHeight = Typography.Body.LineHeight,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };
        block.SetResourceReference(TextElement.FontFamilyProperty, CodeFont);
        block.SetResourceReference(TextElement.BackgroundProperty, SurfaceAlt);
        _unspoken.Add(block);
        blocks.Add(block);
        var place = value is not null && _pieces.TryGetValue(value, out var piece) ? piece.Start : (int?)null;
        foreach (var colored in CodeColors.Pieces(text, code.Language))
        {
            var run = new Run(text.Substring(colored.Start, colored.Length));
            if (colored.Brush is { } brush)
            {
                // Autour du texte, et non sur lui : surligner ou couper le texte ne perd pas la couleur.
                var span = new Span(run);
                span.SetResourceReference(TextElement.ForegroundProperty, brush);
                block.Inlines.Add(span);
            }
            else
            {
                block.Inlines.Add(run);
            }
            if (place is { } start)
            {
                Layer.AddRun(run, start + colored.Start);
            }
        }
    }

    private Table BuildTable(RenderElement element)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12), BorderThickness = new Thickness(1, 1, 0, 0) };
        table.SetResourceReference(Block.BorderBrushProperty, Divider);
        var groups = element.Children.OfType<RenderElement>().ToList();
        var columns = groups.SelectMany(group => group.Children.OfType<RenderElement>()).Max(row => row.Children.OfType<RenderElement>().Count());
        for (var i = 0; i < columns; i++)
        {
            table.Columns.Add(new TableColumn());
        }
        foreach (var group in groups)
        {
            var rows = new TableRowGroup();
            foreach (var row in group.Children.OfType<RenderElement>())
            {
                var line = new TableRow();
                foreach (var cell in row.Children.OfType<RenderElement>())
                {
                    line.Cells.Add(BuildCell(cell));
                }
                rows.Rows.Add(line);
            }
            table.RowGroups.Add(rows);
        }
        return table;
    }

    private TableCell BuildCell(RenderElement cell)
    {
        var paragraph = new Paragraph
        {
            Margin = default,
            TextAlignment = cell.Alignment switch
            {
                "center" => TextAlignment.Center,
                "right" => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
        };
        AddInlines(cell, paragraph.Inlines, 0, false);
        var result = new TableCell(paragraph) { Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0, 0, 1, 1) };
        result.SetResourceReference(TableCell.BorderBrushProperty, Divider);
        if (cell.Tag == "th")
        {
            result.FontWeight = FontWeights.SemiBold;
            result.SetResourceReference(TextElement.BackgroundProperty, SurfaceAlt);
        }
        return result;
    }

    private static Paragraph BuildRule()
    {
        var rule = new Paragraph { Margin = new Thickness(0, 12, 0, 16), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 1, LineHeight = 1, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        rule.SetResourceReference(Block.BorderBrushProperty, Divider);
        return rule;
    }

    private BlockUIContainer BuildFormulaBlock(RenderElement element)
    {
        var slot = new Border { HorizontalAlignment = HorizontalAlignment.Center };
        PlaceFormula(slot, element.Formula!, _comfort.Size * 1.2, block: true);
        var container = new BlockUIContainer(slot) { Margin = new Thickness(0, 4, 0, 16) };
        RegisterFormula(element, container, slot);
        return container;
    }

    private void RegisterFormula(RenderElement element, TextElement container, Border slot)
    {
        if (_pieces.TryGetValue(element, out var piece))
        {
            Layer.AddFormula(container, slot, piece.Start, piece.End);
        }
    }

    private void PlaceFormula(Border slot, string source, double size, bool block)
    {
        if (_formulas++ < FormulasAtOnce)
        {
            slot.Child = FormulaOrText(source, size, block);
        }
        else
        {
            slot.Child = FormulaAsText(source, block);
            _pendingFormulas.Enqueue((slot, source, block ? size : -size));
        }
    }

    private void DrawPendingFormulas()
    {
        for (var i = 0; i < FormulasPerTurn && _pendingFormulas.TryDequeue(out var formula); i++)
        {
            formula.Slot.Child = FormulaOrText(formula.Source, Math.Abs(formula.Size), formula.Size > 0);
        }
    }

    /// <summary>Une formule dessinée ; une large se réduit plutôt que de faire défiler la page. Inconnue, elle reste lisible.</summary>
    internal static UIElement FormulaOrText(string source, double size, bool block)
    {
        // Deux écritures de KaTeX que ce moteur ne connaît pas, et qui se lisent très bien comme une fraction.
        var drawn = source.Replace("\\tfrac", "\\frac", StringComparison.Ordinal).Replace("\\dfrac", "\\frac", StringComparison.Ordinal);
        try
        {
            WpfTeXFormulaParser.Instance.Parse(drawn);
        }
        catch (TexException)
        {
            return FormulaAsText(source, block);
        }
        var control = new FormulaControl { Formula = drawn, Scale = size, SnapsToDevicePixels = true };
        control.SetResourceReference(Control.ForegroundProperty, Text);
        AutomationProperties.SetName(control, source);
        return block ? new Viewbox { StretchDirection = StretchDirection.DownOnly, Child = control } : control;
    }

    private static TextBlock FormulaAsText(string source, bool block)
    {
        var text = new TextBlock { Text = block ? $"$${source}$$" : $"${source}$", TextWrapping = TextWrapping.Wrap, ToolTip = ReadingText.FormulaAsText };
        text.SetResourceReference(TextElement.FontFamilyProperty, CodeFont);
        return text;
    }

    private void AddInlines(RenderElement parent, InlineCollection inlines, int depth, bool inLink)
    {
        foreach (var child in parent.Children)
        {
            AddInline(child, inlines, depth, inLink);
        }
    }

    private void AddInline(RenderNode node, InlineCollection inlines, int depth, bool inLink)
    {
        if (node is RenderText text)
        {
            AddText(text, inlines);
            return;
        }
        var element = (RenderElement)node;
        switch (element.Tag)
        {
            case "br":
                inlines.Add(new LineBreak());
                _afterBreak = true;
                break;
            case "span" when element.Formula is { } formula:
                AddFormula(element, formula, inlines);
                break;
            case "span" when depth < MaxInlineDepth:
                AddStyled(element, inlines, depth, inLink);
                break;
            case "img":
                inlines.Add(BuildImage(element));
                break;
            case "a" when depth < MaxInlineDepth:
                AddLink(element, inlines, depth, inLink);
                break;
            case "code" when depth < MaxInlineDepth:
                var code = new Span { FontSize = _inlineSize * 0.92 };
                code.SetResourceReference(TextElement.FontFamilyProperty, CodeFont);
                code.SetResourceReference(TextElement.BackgroundProperty, SurfaceAlt);
                AddSpan(element, code, inlines, depth, inLink);
                break;
            case "em" when depth < MaxInlineDepth:
                AddSpan(element, new Italic(), inlines, depth, inLink);
                break;
            case "strong" when depth < MaxInlineDepth:
                AddBold(element, inlines, depth, inLink);
                break;
            case "u" when depth < MaxInlineDepth:
                AddSpan(element, new Underline(), inlines, depth, inLink);
                break;
            case "s" when depth < MaxInlineDepth:
                AddSpan(element, new Span { TextDecorations = TextDecorations.Strikethrough }, inlines, depth, inLink);
                break;
            case "sup" when depth < MaxInlineDepth:
                AddSpan(element, new Span { BaselineAlignment = BaselineAlignment.Superscript, FontSize = _inlineSize * 0.75 }, inlines, depth, inLink);
                break;
            case "sub" when depth < MaxInlineDepth:
                AddSpan(element, new Span { BaselineAlignment = BaselineAlignment.Subscript, FontSize = _inlineSize * 0.75 }, inlines, depth, inLink);
                break;
            default:
                AddInlines(element, inlines, depth, inLink);
                break;
        }
    }

    /// <summary>Du gras : un trou, en révision. Le gras dans le gras n'en fait qu'un.</summary>
    private void AddBold(RenderElement element, InlineCollection inlines, int depth, bool inLink)
    {
        var pieces = Descendants(element).OfType<RenderText>().Select(text => _pieces.GetValueOrDefault(text)).OfType<TextPiece>().ToList();
        if (_boldDepth == 0 && pieces.Count > 0)
        {
            BoldRanges.Add((pieces[0].Start, pieces[^1].End));
        }
        _boldDepth++;
        AddSpan(element, new Bold(), inlines, depth, inLink);
        _boldDepth--;
    }

    private static IEnumerable<RenderNode> Descendants(RenderElement element) =>
        element.Children.SelectMany(child => child is RenderElement inner ? Descendants(inner) : [child]);

    /// <summary>Un passage coloré ou agrandi par la fiche ; la taille est relative au texte qui l'entoure.</summary>
    private void AddStyled(RenderElement element, InlineCollection inlines, int depth, bool inLink)
    {
        var span = element.TextColor is { } color ? new ColoredSpan(TokenColor.FromHex(color)) : new Span();
        var outer = _inlineSize;
        if (element.FontScale is { } scale)
        {
            _inlineSize = outer * scale;
            span.FontSize = _inlineSize;
        }
        AddSpan(element, span, inlines, depth, inLink);
        _inlineSize = outer;
    }

    private void AddSpan(RenderElement element, Span span, InlineCollection inlines, int depth, bool inLink)
    {
        AddInlines(element, span.Inlines, depth + 1, inLink);
        inlines.Add(span);
    }

    private void AddText(RenderText text, InlineCollection inlines)
    {
        // Un saut de ligne simple se lit comme une espace ; celui qui suit un saut dur est déjà à l'écran.
        var value = text.Value.Replace('\n', ' ');
        // La case d'une tâche se dessine : son écriture compte dans les positions, sans s'afficher.
        var skipped = _taskTexts.Remove(text) ? Math.Min(TaskMarkLength, value.Length) : _afterBreak && value.StartsWith(' ') ? 1 : 0;
        _afterBreak = false;
        var run = new Run(value[skipped..]);
        inlines.Add(run);
        if (_pieces.TryGetValue(text, out var piece))
        {
            Layer.AddRun(run, piece.Start + skipped);
        }
    }

    private void AddFormula(RenderElement element, string source, InlineCollection inlines)
    {
        var slot = new Border();
        PlaceFormula(slot, source, _comfort.Size, block: false);
        var container = new InlineUIContainer(slot) { BaselineAlignment = BaselineAlignment.Center };
        inlines.Add(container);
        RegisterFormula(element, container, slot);
    }

    private void AddLink(RenderElement element, InlineCollection inlines, int depth, bool inLink)
    {
        var target = ContentTargets.OfLink(element.Target ?? string.Empty, _reading.Folder);
        Span link;
        if (inLink || target is BrokenLink || (!_opensLinks && target is not PageAnchor))
        {
            link = new Span();
            if (target is BrokenLink)
            {
                link.ToolTip = ReadingText.BrokenLink;
                link.TextDecorations = TextDecorations.Underline;
            }
        }
        else
        {
            // Un clic plutôt qu'une commande : une commande s'abonne pour chaque lien, ce qui se sent sur une longue fiche.
            var hyperlink = new Hyperlink { ToolTip = target is ExternalLink external ? external.Address : null, Tag = target };
            hyperlink.Click += (_, _) => _follow(target);
            hyperlink.SetResourceReference(TextElement.ForegroundProperty, LinkBrush);
            link = hyperlink;
            inLink = true;
        }
        if (element.Identifier is { } identifier)
        {
            Identified[identifier] = link;
        }
        AddSpan(element, link, inlines, depth, inLink);
    }

    private Inline BuildImage(RenderElement element)
    {
        var target = ContentTargets.OfImage(element.Target ?? string.Empty, _reading.Folder);
        if (target is BlockedImage blocked)
        {
            return BuildNotice(ReadingText.Blocked(blocked));
        }
        var svg = target is VaultImage { Path: var path } && path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? path : null;
        if (svg is not null && Pictures is null)
        {
            return BuildNotice($"{ReadingText.SvgLater} : {svg[(svg.LastIndexOf('/') + 1)..]}");
        }
        var image = new Image { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = ImageMaxHeight };
        AutomationProperties.SetName(image, element.AlternativeText ?? string.Empty);
        var container = new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center };
        _images.Add(svg is not null ? LoadSvgAsync(container, image, svg) : LoadImageAsync(container, image, target));
        return container;
    }

    private static Span BuildNotice(string message)
    {
        var notice = new Span(new Run(message)) { FontSize = Typography.Caption.Size };
        notice.SetResourceReference(TextElement.BackgroundProperty, SurfaceAlt);
        notice.SetResourceReference(TextElement.ForegroundProperty, TextSecondary);
        return notice;
    }

    /// <summary>Une image SVG passe par la page cachée, qui la rend sans qu'aucun script ni adresse n'y agisse.</summary>
    private async Task LoadSvgAsync(InlineUIContainer container, Image image, string path)
    {
        var content = await Task.Run(() => _reading.ReadImage(path)).ConfigureAwait(true);
        var picture = content.Succeeded && Pictures is { } engine
            ? await engine.DrawSvgAsync(content.Value).ConfigureAwait(true)
            : null;
        if (picture is null)
        {
            ShowUnreadable(container);
            return;
        }
        try
        {
            using var stream = new MemoryStream(picture.Png.ToArray());
            image.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            image.MaxWidth = picture.Width;
        }
        catch (Exception problem) when (problem is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            ShowUnreadable(container);
        }
    }

    private static void ShowUnreadable(InlineUIContainer container)
    {
        var notice = new TextBlock { Text = ReadingText.UnreadableImage, FontSize = Typography.Caption.Size };
        notice.SetResourceReference(TextBlock.ForegroundProperty, TextSecondary);
        container.Child = notice;
    }

    private async Task LoadImageAsync(InlineUIContainer container, Image image, ImageTarget target)
    {
        var bitmap = await Task.Run(() => ReadBitmap(target)).ConfigureAwait(true);
        if (bitmap is not null)
        {
            image.Source = bitmap;
        }
        else
        {
            ShowUnreadable(container);
        }
    }

    private BitmapImage? ReadBitmap(ImageTarget target)
    {
        var content = target is VaultImage vault
            ? _reading.ReadImage(vault.Path) is { Succeeded: true } outcome ? outcome.Value : null
            : EmbeddedContent(((EmbeddedImage)target).Data);
        if (content is null)
        {
            return null;
        }
        try
        {
            return Decode(content);
        }
        catch (Exception problem) when (problem is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException or IOException or OverflowException)
        {
            return null;
        }
    }
}
