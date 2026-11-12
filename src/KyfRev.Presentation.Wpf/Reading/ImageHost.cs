using System.Windows;
using System.Windows.Controls;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// La boîte d'une image réglée, en lecture : elle lui donne sa largeur, comptée en pourcentage de la largeur du texte, et
/// sa place. Une image placée à gauche, au centre ou à droite prend toute la ligne : elle est seule sur la sienne, comme
/// en écriture.
/// </summary>
internal sealed class ImageHost : Decorator
{
    /// <summary>La largeur de l'image, en pourcentage de la place offerte ; rien : sa taille d'origine, sans la dépasser.</summary>
    public int? Percent { get; init; }

    /// <summary>Sa place.</summary>
    public ImageAlign Align { get; init; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not { } child)
        {
            return default;
        }
        var room = constraint.Width;
        var bounded = !double.IsInfinity(room);
        child.Measure(new Size(Percent is { } percent && bounded ? room * percent / 100 : room, double.PositiveInfinity));
        var wanted = child.DesiredSize;
        return Align == ImageAlign.Inline || !bounded ? wanted : new Size(room, wanted.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is { } child)
        {
            var width = Math.Min(child.DesiredSize.Width, arrangeSize.Width);
            var left = Align switch
            {
                ImageAlign.Center => (arrangeSize.Width - width) / 2,
                ImageAlign.Right => arrangeSize.Width - width,
                _ => 0,
            };
            child.Arrange(new Rect(left, 0, width, child.DesiredSize.Height));
        }
        return arrangeSize;
    }
}
