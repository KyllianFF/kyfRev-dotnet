using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>
/// Un extrait de recherche, sur deux lignes au plus : le texte autour des mots trouvés, ceux-ci surlignés. Le reste prend la
/// couleur de la ligne qui le contient, choisie ou non : lisible dans chaque thème.
/// </summary>
internal sealed class ExcerptText : TextBlock
{
    /// <summary>Les morceaux de l'extrait.</summary>
    public static readonly DependencyProperty PiecesProperty = DependencyProperty.Register(
        nameof(Pieces), typeof(IReadOnlyList<ExcerptPiece>), typeof(ExcerptText), new PropertyMetadata(null, OnPiecesChanged));

    private const int MaximumLines = 2;

    static ExcerptText()
    {
        LineHeightProperty.OverrideMetadata(typeof(ExcerptText), new FrameworkPropertyMetadata(OnLineHeightChanged));
    }

    public ExcerptText()
    {
        TextWrapping = TextWrapping.Wrap;
        TextTrimming = TextTrimming.CharacterEllipsis;
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        FontWeight = FontWeights.Normal;
        SetResourceReference(FontSizeProperty, "Kyf.FontSize.Caption");
        SetResourceReference(LineHeightProperty, "Kyf.LineHeight.Caption");
    }

    /// <inheritdoc cref="PiecesProperty"/>
    public IReadOnlyList<ExcerptPiece>? Pieces
    {
        get => (IReadOnlyList<ExcerptPiece>?)GetValue(PiecesProperty);
        set => SetValue(PiecesProperty, value);
    }

    /// <summary>La hauteur de deux lignes borne le texte : au-delà, il finit par des points de suspension.</summary>
    private static void OnLineHeightChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is double line && !double.IsNaN(line))
        {
            ((ExcerptText)owner).MaxHeight = MaximumLines * line;
        }
    }

    private static void OnPiecesChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var text = (ExcerptText)owner;
        text.Inlines.Clear();
        foreach (var piece in text.Pieces ?? [])
        {
            var run = new Run(piece.Text);
            if (piece.Found)
            {
                run.SetResourceReference(TextElement.BackgroundProperty, "Kyf.Brush.Highlight");
                run.SetResourceReference(TextElement.ForegroundProperty, "Kyf.Brush.OnHighlight");
            }
            text.Inlines.Add(run);
        }
    }
}
