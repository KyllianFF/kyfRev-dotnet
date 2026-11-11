using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using KyfRev.DesignSystem.Controls;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La fenêtre « Masquer des zones » : on trace à la souris, sur l'image, les rectangles à retrouver en révision. Un clic
/// sans glisser sur un rectangle le retire. Ce qui est tracé part vers <see cref="ImageMaskEditorViewModel"/> en parts de
/// l'image ; la fenêtre ne fait que dessiner ce qu'il garde.
/// </summary>
internal sealed partial class ImageMaskDialog : UserControl
{
    /// <summary>En dessous, en pixels, un geste est un clic et non un tracé.</summary>
    private const double ClickDistance = 4;

    private const string MaskFill = "Kyf.Brush.PillPassageBackground";
    private const string MaskStroke = "Kyf.Brush.Primary";

    private Point? _start;
    private Rectangle? _band;
    private ImageMaskEditorViewModel? _editor;

    public ImageMaskDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Follow(e.NewValue as ImageMaskEditorViewModel);
        Unloaded += (_, _) => Follow(null);
        Loaded += (_, _) => Follow(DataContext as ImageMaskEditorViewModel);
    }

    private void Follow(ImageMaskEditorViewModel? editor)
    {
        if (ReferenceEquals(editor, _editor))
        {
            return;
        }
        _editor?.Masks.CollectionChanged -= OnMasksChanged;
        _editor = editor;
        editor?.Masks.CollectionChanged += OnMasksChanged;
        Draw();
    }

    private void OnMasksChanged(object? sender, NotifyCollectionChangedEventArgs e) => Draw();

    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e) => Draw();

    private void OnClosed(object sender, DialogClosedEventArgs e) =>
        _editor?.AnswerCommand.Execute(e.Choice == DialogChoice.Primary);

    /// <summary>Redessine les zones gardées, à la taille de la surface.</summary>
    private void Draw()
    {
        Surface.Children.Clear();
        _band = null;
        if (_editor is null || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
        {
            return;
        }
        foreach (var mask in _editor.Masks)
        {
            Surface.Children.Add(Shape(mask.X * Surface.ActualWidth, mask.Y * Surface.ActualHeight, mask.Width * Surface.ActualWidth, mask.Height * Surface.ActualHeight, traced: true));
        }
    }

    private static Rectangle Shape(double left, double top, double width, double height, bool traced)
    {
        var shape = new Rectangle { Width = Math.Max(width, 0), Height = Math.Max(height, 0), StrokeThickness = 2, Opacity = traced ? 0.85 : 0.5, IsHitTestVisible = false };
        shape.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, MaskFill);
        shape.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, MaskStroke);
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        return shape;
    }

    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (_editor is not { CanDraw: true })
        {
            return;
        }
        _start = Clamp(e.GetPosition(Surface));
        _band = Shape(_start.Value.X, _start.Value.Y, 0, 0, traced: false);
        Surface.Children.Add(_band);
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnSurfaceMove(object sender, MouseEventArgs e)
    {
        if (_start is not { } start || _band is null)
        {
            return;
        }
        var now = Clamp(e.GetPosition(Surface));
        Canvas.SetLeft(_band, Math.Min(start.X, now.X));
        Canvas.SetTop(_band, Math.Min(start.Y, now.Y));
        _band.Width = Math.Abs(now.X - start.X);
        _band.Height = Math.Abs(now.Y - start.Y);
    }

    private void OnSurfaceUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is not { } start)
        {
            return;
        }
        var end = Clamp(e.GetPosition(Surface));
        _start = null;
        Surface.ReleaseMouseCapture();
        Finish(start, end);
        e.Handled = true;
    }

    /// <summary>Un tracé interrompu (fenêtre fermée, autre application) ne laisse rien.</summary>
    private void OnSurfaceLostCapture(object sender, MouseEventArgs e)
    {
        if (_start is not null)
        {
            _start = null;
            Draw();
        }
    }

    /// <summary>La fin d'un geste : un tracé ajoute une zone, un clic sur une zone la retire.</summary>
    internal void Finish(Point start, Point end)
    {
        if (_editor is null || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
        {
            return;
        }
        var (width, height) = (Surface.ActualWidth, Surface.ActualHeight);
        if (Math.Abs(end.X - start.X) < ClickDistance && Math.Abs(end.Y - start.Y) < ClickDistance)
        {
            // Le dernier tracé est au-dessus : c'est lui que le clic retire.
            for (var index = _editor.Masks.Count - 1; index >= 0; index--)
            {
                var mask = _editor.Masks[index];
                if (new Rect(mask.X * width, mask.Y * height, mask.Width * width, mask.Height * height).Contains(end))
                {
                    _editor.RemoveAt(index);
                    return;
                }
            }
            Draw();
            return;
        }
        if (!_editor.Add(new ImageMask(start.X / width, start.Y / height, (end.X - start.X) / width, (end.Y - start.Y) / height)))
        {
            Draw();
        }
    }

    private Point Clamp(Point point) =>
        new(Math.Clamp(point.X, 0, Math.Max(Surface.ActualWidth, 0)), Math.Clamp(point.Y, 0, Math.Max(Surface.ActualHeight, 0)));
}
