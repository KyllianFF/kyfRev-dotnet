using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.Application.Model;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>L'extrait d'un rétrolien : le texte autour d'un lien, le texte du lien ressorti sur la couleur des passages.</summary>
internal sealed class BacklinkExcerpt : TextBlock
{
    public static readonly DependencyProperty PartsProperty = DependencyProperty.Register(
        nameof(Parts), typeof(IReadOnlyList<ExcerptPart>), typeof(BacklinkExcerpt), new PropertyMetadata(null, OnPartsChanged));

    /// <summary>Les morceaux de l'extrait.</summary>
    public IReadOnlyList<ExcerptPart>? Parts
    {
        get => (IReadOnlyList<ExcerptPart>?)GetValue(PartsProperty);
        set => SetValue(PartsProperty, value);
    }

    private static void OnPartsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var excerpt = (BacklinkExcerpt)sender;
        excerpt.Inlines.Clear();
        foreach (var part in excerpt.Parts ?? [])
        {
            var run = new Run(part.Text);
            if (part.IsLink)
            {
                run.SetResourceReference(TextElement.BackgroundProperty, "Kyf.Brush.PillPassageBackground");
                run.SetResourceReference(TextElement.ForegroundProperty, "Kyf.Brush.PillPassageText");
            }
            excerpt.Inlines.Add(run);
        }
    }
}
