using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Rendering;
using LinkTarget = KyfRev.Rendering.LinkTarget;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Le texte d'une fiche, en document natif : sélection sur plusieurs blocs, défilement vertical seul.
/// Un lien vers un autre endroit de la fiche s'y suit ; les autres passent par <see cref="OpenLinkCommand"/>.
/// </summary>
internal sealed class SheetDocumentViewer : FlowDocumentScrollViewer
{
    /// <summary>Hauteur au-dessus d'un titre amené en haut de la vue.</summary>
    private const double TopMargin = 8;

    public static readonly DependencyProperty ReadingProperty = DependencyProperty.Register(
        nameof(Reading), typeof(SheetReading), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty ShowsHeaderProperty = DependencyProperty.Register(
        nameof(ShowsHeader), typeof(bool), typeof(SheetDocumentViewer), new PropertyMetadata(true, OnContentChanged));

    public static readonly DependencyProperty OpenLinkCommandProperty = DependencyProperty.Register(
        nameof(OpenLinkCommand), typeof(ICommand), typeof(SheetDocumentViewer), new PropertyMetadata(null, OnContentChanged));

    private DocumentBuilder? _built;
    private ScrollViewer? _scroller;

    public SheetDocumentViewer()
    {
        IsToolBarVisible = false;
        MinZoom = 100;
        MaxZoom = 100;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SetResourceReference(SelectionBrushProperty, "Kyf.Brush.Selection");
    }

    /// <summary>La fiche à montrer.</summary>
    public SheetReading? Reading
    {
        get => (SheetReading?)GetValue(ReadingProperty);
        set => SetValue(ReadingProperty, value);
    }

    /// <summary>Le titre et les étiquettes de la fiche ouvrent le texte.</summary>
    public bool ShowsHeader
    {
        get => (bool)GetValue(ShowsHeaderProperty);
        set => SetValue(ShowsHeaderProperty, value);
    }

    /// <summary>Ouvre un lien vers l'extérieur ou vers une autre fiche ; sans elle, ces liens restent du texte.</summary>
    public ICommand? OpenLinkCommand
    {
        get => (ICommand?)GetValue(OpenLinkCommandProperty);
        set => SetValue(OpenLinkCommandProperty, value);
    }

    /// <summary>Se termine quand les images du document sont chargées.</summary>
    public Task ImagesLoaded => _built?.Images ?? Task.CompletedTask;

    /// <summary>Il reste du contenu à ajouter ou des formules à dessiner.</summary>
    public bool HasPendingWork => _built?.HasPendingWork == true;

    public override void OnApplyTemplate()
    {
        _scroller?.ScrollChanged -= OnScrollChanged;
        base.OnApplyTemplate();
        _scroller = GetTemplateChild("PART_ContentHost") as ScrollViewer;
        _scroller?.ScrollChanged += OnScrollChanged;
    }

    /// <summary>Amène un titre en haut de la vue.</summary>
    public void Show(RenderElement heading)
    {
        _built?.Complete();
        if (_built is not null && _built.Headings.TryGetValue(heading, out var paragraph))
        {
            ShowAtTop(paragraph);
        }
    }

    /// <summary>Amène en haut de la vue l'endroit d'une position du texte affiché.</summary>
    public void Show(int position)
    {
        if (_built is null || Reading is null)
        {
            return;
        }
        _built.Complete();
        var piece = Reading.Map.Pieces.FirstOrDefault(candidate => candidate.End > position);
        if (piece?.Node is RenderText text && _built.Runs.TryGetValue(text, out var run))
        {
            ShowAtTop(run);
        }
    }

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((SheetDocumentViewer)sender).Rebuild();

    private void Rebuild()
    {
        _built?.Cancel();
        if (Reading is { } reading)
        {
            _built = new DocumentBuilder(reading, Follow, OpenLinkCommand is not null, ShowsHeader);
            Document = _built.Build();
        }
        else
        {
            _built = null;
            Document = null;
        }
    }

    private void Follow(LinkTarget target)
    {
        if (target is PageAnchor anchor)
        {
            _built?.Complete();
            if (_built is not null && _built.Identified.TryGetValue(anchor.Identifier, out var element))
            {
                ShowAtTop(element);
            }
        }
        else
        {
            OpenLinkCommand?.Execute(target);
        }
    }

    private void ShowAtTop(TextElement element)
    {
        if (_scroller is null)
        {
            element.BringIntoView();
            return;
        }
        UpdateLayout();
        var top = element.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;
        _scroller.ScrollToVerticalOffset(_scroller.VerticalOffset + top - TopMargin);
    }

    /// <summary>La partie en cours : le dernier titre passé en haut de la vue.</summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_built is null || Reading is null || Reading.Outline.Count == 0)
        {
            return;
        }
        OutlineEntry? current = null;
        foreach (var entry in Reading.Outline)
        {
            if (!_built.Headings.TryGetValue(entry.Heading, out var paragraph))
            {
                continue;
            }
            if (current is not null && paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top > ActualHeight / 3)
            {
                break;
            }
            current = entry;
        }
        foreach (var entry in Reading.Outline)
        {
            entry.IsActive = ReferenceEquals(entry, current);
        }
    }
}
