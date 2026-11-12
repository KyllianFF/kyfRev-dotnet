using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using KyfRev.DesignSystem.Controls;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// La surface où l'on trace les masques : une toile qui prend le clavier, et que le lecteur d'écran voit et nomme, ce
/// qu'une toile ordinaire ne fait pas.
/// </summary>
internal sealed class ImageMaskSurface : Canvas
{
    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new SurfacePeer(this);

    private sealed class SurfacePeer(ImageMaskSurface owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

        protected override string GetClassNameCore() => nameof(ImageMaskSurface);

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}

/// <summary>
/// La fenêtre « Masquer des zones » : on trace à la souris, sur l'image, les rectangles à retrouver en révision. Un clic
/// sans glisser sur un rectangle le retire. Au clavier, Inser ajoute une zone, les flèches la placent, Maj et les flèches
/// changent sa taille. Ce qui est tracé part vers <see cref="ImageMaskEditorViewModel"/> en parts de l'image ; la fenêtre
/// ne fait que dessiner ce qu'il garde.
/// </summary>
internal sealed partial class ImageMaskDialog : UserControl
{
    /// <summary>En dessous, en pixels, un geste est un clic et non un tracé.</summary>
    private const double ClickDistance = 4;

    /// <summary>Le pas d'une flèche, en part de l'image ; avec Ctrl, cinq fois plus.</summary>
    private const double KeyStep = 0.01;

    /// <summary>La moitié du côté d'une poignée de rognage, et la distance à laquelle on la saisit, en pixels.</summary>
    private const double Handle = 6;

    private const double Reach = 10;

    /// <summary>Le voile sur ce que le rognage écarte.</summary>
    private const string CropShade = "Kyf.Brush.Scrim";

    private const string HandleFill = "Kyf.Brush.Surface";

    /// <summary>Ce que le geste de rognage en cours déplace : des bords du cadre, ou le cadre entier.</summary>
    [Flags]
    internal enum Grip
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        Move = 16,
    }

    private Grip _grip;
    private Point _gripStart;
    private ImageMask _gripFrame;

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
        _editor?.PropertyChanged -= OnEditorChanged;
        _editor = editor;
        editor?.Masks.CollectionChanged += OnMasksChanged;
        editor?.PropertyChanged += OnEditorChanged;
        Draw();
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageMaskEditorViewModel.SelectedIndex))
        {
            Draw();
        }
    }

    /// <summary>Après « Ajouter une zone », le clavier est sur l'image : les flèches placent la zone tout de suite.</summary>
    private void OnAddClick(object sender, RoutedEventArgs e) => Surface.Focus();

    private void OnSurfaceKey(object sender, KeyEventArgs e) => e.Handled = Press(e.Key, Keyboard.Modifiers);

    /// <summary>Une touche sur l'image ; faux si elle n'y fait rien, et elle garde alors son sens habituel.</summary>
    internal bool Press(Key key, ModifierKeys modifiers)
    {
        if (_editor is not { CanDraw: true } editor || (modifiers & ModifierKeys.Alt) != 0)
        {
            return false;
        }
        var step = (modifiers & ModifierKeys.Control) != 0 ? KeyStep * 5 : KeyStep;
        var (right, down) = key switch
        {
            Key.Left => (-step, 0d),
            Key.Right => (step, 0d),
            Key.Up => (0d, -step),
            Key.Down => (0d, step),
            _ => (0d, 0d),
        };
        switch (key)
        {
            case Key.Left or Key.Right or Key.Up or Key.Down when (modifiers & ModifierKeys.Shift) != 0:
                editor.ResizeSelected(right, down);
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down:
                editor.MoveSelected(right, down);
                return true;
            case Key.Insert when editor.AddZoneCommand.CanExecute(null):
                editor.AddZoneCommand.Execute(null);
                return true;
            case Key.PageDown:
                editor.SelectBy(1);
                return true;
            case Key.PageUp:
                editor.SelectBy(-1);
                return true;
            case Key.Delete or Key.Back:
                editor.RemoveSelected();
                return true;
            default:
                return false;
        }
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
        if (_editor.IsCrop)
        {
            DrawCrop(_editor.Crop);
            return;
        }
        for (var index = 0; index < _editor.Masks.Count; index++)
        {
            var mask = _editor.Masks[index];
            var shape = Shape(mask.X * Surface.ActualWidth, mask.Y * Surface.ActualHeight, mask.Width * Surface.ActualWidth, mask.Height * Surface.ActualHeight, traced: true);
            // La zone choisie, celle que le clavier déplace, a un contour plus épais.
            shape.StrokeThickness = index == _editor.SelectedIndex ? 4 : 2;
            Surface.Children.Add(shape);
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

    /// <summary>
    /// Le rognage, comme dans un traitement de texte : l'image entière, ce qui est écarté sous un voile, le cadre et ses
    /// huit poignées, aux angles et au milieu des bords.
    /// </summary>
    private void DrawCrop(ImageMask crop)
    {
        var (width, height) = (Surface.ActualWidth, Surface.ActualHeight);
        var frame = new Rect(crop.X * width, crop.Y * height, crop.Width * width, crop.Height * height);
        foreach (var shade in new[]
        {
            new Rect(0, 0, width, frame.Top),
            new Rect(0, frame.Bottom, width, Math.Max(height - frame.Bottom, 0)),
            new Rect(0, frame.Top, frame.Left, frame.Height),
            new Rect(frame.Right, frame.Top, Math.Max(width - frame.Right, 0), frame.Height),
        })
        {
            var veil = new Rectangle { Width = shade.Width, Height = shade.Height, IsHitTestVisible = false };
            veil.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, CropShade);
            Canvas.SetLeft(veil, shade.Left);
            Canvas.SetTop(veil, shade.Top);
            Surface.Children.Add(veil);
        }
        var outline = new Rectangle { Width = frame.Width, Height = frame.Height, StrokeThickness = 2, IsHitTestVisible = false };
        outline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, MaskStroke);
        Canvas.SetLeft(outline, frame.Left);
        Canvas.SetTop(outline, frame.Top);
        Surface.Children.Add(outline);
        foreach (var point in HandlePoints(frame))
        {
            var handle = new Rectangle { Width = 2 * Handle, Height = 2 * Handle, StrokeThickness = 2, RadiusX = 2, RadiusY = 2, IsHitTestVisible = false };
            handle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, HandleFill);
            handle.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, MaskStroke);
            Canvas.SetLeft(handle, Math.Clamp(point.X - Handle, 0, Math.Max(width - 2 * Handle, 0)));
            Canvas.SetTop(handle, Math.Clamp(point.Y - Handle, 0, Math.Max(height - 2 * Handle, 0)));
            Surface.Children.Add(handle);
        }
    }

    /// <summary>Les huit poignées d'un cadre : ses angles, puis le milieu de ses bords.</summary>
    private static Point[] HandlePoints(Rect frame) =>
    [
        frame.TopLeft, frame.TopRight, frame.BottomLeft, frame.BottomRight,
        new(frame.Left + frame.Width / 2, frame.Top), new(frame.Left + frame.Width / 2, frame.Bottom),
        new(frame.Left, frame.Top + frame.Height / 2), new(frame.Right, frame.Top + frame.Height / 2),
    ];

    /// <summary>Ce qu'un appui à cet endroit saisit du cadre : un angle, un bord, le cadre entier, ou rien hors de lui.</summary>
    internal Grip GripAt(Point point)
    {
        if (_editor is not { IsCrop: true, CanDraw: true } editor || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
        {
            return Grip.None;
        }
        var crop = editor.Crop;
        var frame = new Rect(crop.X * Surface.ActualWidth, crop.Y * Surface.ActualHeight, crop.Width * Surface.ActualWidth, crop.Height * Surface.ActualHeight);
        if (point.X < frame.Left - Reach || point.X > frame.Right + Reach || point.Y < frame.Top - Reach || point.Y > frame.Bottom + Reach)
        {
            return Grip.None;
        }
        var grip = Grip.None;
        if (Math.Abs(point.X - frame.Left) <= Reach)
        {
            grip |= Grip.Left;
        }
        else if (Math.Abs(point.X - frame.Right) <= Reach)
        {
            grip |= Grip.Right;
        }
        if (Math.Abs(point.Y - frame.Top) <= Reach)
        {
            grip |= Grip.Top;
        }
        else if (Math.Abs(point.Y - frame.Bottom) <= Reach)
        {
            grip |= Grip.Bottom;
        }
        return grip == Grip.None ? Grip.Move : grip;
    }

    /// <summary>Le cadre que donne un geste parti de <paramref name="start"/> et arrivé à <paramref name="now"/>, pour ce qu'il a saisi.</summary>
    internal bool DragCrop(Grip grip, ImageMask from, Point start, Point now)
    {
        if (_editor is null || grip == Grip.None || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
        {
            return false;
        }
        var (dx, dy) = ((now.X - start.X) / Surface.ActualWidth, (now.Y - start.Y) / Surface.ActualHeight);
        var (left, top, right, bottom) = (from.X, from.Y, from.X + from.Width, from.Y + from.Height);
        if (grip == Grip.Move)
        {
            // Le cadre garde sa taille et s'arrête aux bords de l'image.
            dx = Math.Clamp(dx, -left, 1 - right);
            dy = Math.Clamp(dy, -top, 1 - bottom);
            return _editor.SetCrop(new ImageMask(left + dx, top + dy, from.Width, from.Height));
        }
        // Un bord ne croise pas le bord d'en face : le cadre garde de quoi se voir.
        var least = 2 * ImageMasks.MinimumSide;
        if (grip.HasFlag(Grip.Left))
        {
            left = Math.Clamp(left + dx, 0, right - least);
        }
        if (grip.HasFlag(Grip.Right))
        {
            right = Math.Clamp(right + dx, left + least, 1);
        }
        if (grip.HasFlag(Grip.Top))
        {
            top = Math.Clamp(top + dy, 0, bottom - least);
        }
        if (grip.HasFlag(Grip.Bottom))
        {
            bottom = Math.Clamp(bottom + dy, top + least, 1);
        }
        return _editor.SetCrop(new ImageMask(left, top, right - left, bottom - top));
    }

    private void OnSurfaceDown(object sender, MouseButtonEventArgs e)
    {
        if (_editor is not { CanDraw: true })
        {
            return;
        }
        if (_editor.IsCrop)
        {
            // Rogner : on saisit une poignée, un bord ou le cadre ; un appui hors du cadre ne fait rien.
            Surface.Focus();
            _gripStart = Clamp(e.GetPosition(Surface));
            _grip = GripAt(_gripStart);
            _gripFrame = _editor.Crop;
            if (_grip != Grip.None)
            {
                Surface.CaptureMouse();
            }
            e.Handled = true;
            return;
        }
        Surface.Focus();
        _start = Clamp(e.GetPosition(Surface));
        _band = Shape(_start.Value.X, _start.Value.Y, 0, 0, traced: false);
        Surface.Children.Add(_band);
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnSurfaceMove(object sender, MouseEventArgs e)
    {
        if (_editor is { IsCrop: true })
        {
            var here = Clamp(e.GetPosition(Surface));
            if (_grip != Grip.None)
            {
                DragCrop(_grip, _gripFrame, _gripStart, here);
            }
            else
            {
                Surface.Cursor = CursorOf(GripAt(here));
            }
            return;
        }
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

    /// <summary>Le pointeur dit ce que l'appui saisirait.</summary>
    private static Cursor CursorOf(Grip grip) => grip switch
    {
        Grip.Move => Cursors.SizeAll,
        Grip.Left or Grip.Right => Cursors.SizeWE,
        Grip.Top or Grip.Bottom => Cursors.SizeNS,
        Grip.Left | Grip.Top or Grip.Right | Grip.Bottom => Cursors.SizeNWSE,
        Grip.Right | Grip.Top or Grip.Left | Grip.Bottom => Cursors.SizeNESW,
        _ => Cursors.Arrow,
    };

    private void OnSurfaceUp(object sender, MouseButtonEventArgs e)
    {
        if (_grip != Grip.None)
        {
            _grip = Grip.None;
            Surface.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
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
        _grip = Grip.None;
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
