using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Les masques d'une image, posés par-dessus elle : en révision, chaque zone encore masquée est un rectangle plein, qui
/// se retire d'un clic ou avec Entrée, comme un trou du texte. Hors révision, rien n'est dessiné et l'image se voit
/// entière. Le cache prend la taille de l'image : ses zones, écrites en parts de l'image, restent à leur place.
/// </summary>
internal sealed class ImageMaskOverlay : FrameworkElement
{
    /// <summary>La couleur d'un masque : celle d'un trou du texte.</summary>
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(ImageMaskOverlay), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Le contour d'un masque : il se voit encore sur une image de la même couleur que lui.</summary>
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(ImageMaskOverlay), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    private ImmutableHashSet<int> _hidden = [];

    /// <summary>Le cache de l'image de rang <paramref name="imageIndex"/>, avec ses masques.</summary>
    public ImageMaskOverlay(int imageIndex, ImmutableArray<ImageMask> masks)
    {
        ImageIndex = imageIndex;
        Masks = masks;
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    /// <summary>Rang de l'image parmi celles de la fiche.</summary>
    public int ImageIndex { get; }

    /// <summary>Ses masques.</summary>
    public ImmutableArray<ImageMask> Masks { get; }

    /// <summary>Les masques encore posés, par leur rang.</summary>
    public IReadOnlyCollection<int> Hidden => _hidden;

    /// <summary>La couleur d'un masque.</summary>
    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Le contour d'un masque.</summary>
    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Le nom d'un masque parmi les trous de la fiche, stable d'un affichage à l'autre.</summary>
    public static string KeyOf(int imageIndex, int mask) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"masque:{imageIndex}:{mask}");

    /// <summary>Change les masques encore posés.</summary>
    public void SetHidden(IEnumerable<int> hidden)
    {
        var next = hidden.Where(index => index >= 0 && index < Masks.Length).ToImmutableHashSet();
        if (!next.SetEquals(_hidden))
        {
            _hidden = next;
            InvalidateVisual();
        }
    }

    /// <summary>Le masque encore posé à cet endroit du cache ; rien s'il n'y en a pas. Le dernier tracé est au-dessus.</summary>
    public int? MaskAt(Point point)
    {
        for (var index = Masks.Length - 1; index >= 0; index--)
        {
            if (_hidden.Contains(index) && RectOf(Masks[index]).Contains(point))
            {
                return index;
            }
        }
        return null;
    }

    /// <summary>La place d'un masque sur le cache, à sa taille du moment.</summary>
    internal Rect RectOf(ImageMask mask) =>
        new(mask.X * ActualWidth, mask.Y * ActualHeight, mask.Width * ActualWidth, mask.Height * ActualHeight);

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var pen = new Pen(Stroke, 1);
        foreach (var index in _hidden.Order())
        {
            drawingContext.DrawRectangle(Fill, pen, RectOf(Masks[index]));
        }
    }

    /// <summary>Seul un masque encore posé reçoit le clic : ailleurs, l'image reste cliquable comme avant.</summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
        MaskAt(hitTestParameters.HitPoint) is null ? null : new PointHitTestResult(this, hitTestParameters.HitPoint);
}
