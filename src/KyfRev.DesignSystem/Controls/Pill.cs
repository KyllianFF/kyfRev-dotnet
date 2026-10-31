using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Ce qu'une pastille signale. Chaque sorte a son icône : l'état ne dépend jamais de la seule couleur.</summary>
public enum PillKind
{
    /// <summary>Fiche à réviser (document, violet).</summary>
    Sheet,

    /// <summary>Passage annoté (surligneur, pervenche).</summary>
    Passage,

    /// <summary>Rappel de consolidation (flèches circulaires, mauve).</summary>
    Reminder,

    /// <summary>En retard (horloge, rouge fonctionnel).</summary>
    Overdue,

    /// <summary>Archivée (boîte, neutre atténué).</summary>
    Archived,

    /// <summary>Annotation à vérifier (triangle, orange fonctionnel).</summary>
    NeedsReview,
}

/// <summary>Pastille : une icône et un court libellé (« Fiche · J7 », « Retard 4 j »).</summary>
public class Pill : Control
{
    /// <summary>Propriété <see cref="Kind"/>.</summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(PillKind), typeof(Pill), new PropertyMetadata(PillKind.Sheet, (pill, _) => ((Pill)pill).UpdateGlyph()));

    /// <summary>Propriété <see cref="Text"/>.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Pill), new PropertyMetadata(string.Empty));

    private static readonly DependencyPropertyKey GlyphPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(Glyph), typeof(string), typeof(Pill), new PropertyMetadata(Icons.Of(PillKind.Sheet)));

    /// <summary>Propriété <see cref="Glyph"/>.</summary>
    public static readonly DependencyProperty GlyphProperty = GlyphPropertyKey.DependencyProperty;

    static Pill()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Pill), new FrameworkPropertyMetadata(typeof(Pill)));
    }

    /// <summary>Ce que la pastille signale.</summary>
    public PillKind Kind
    {
        get => (PillKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Libellé.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Icône de la sorte de pastille (police d'icônes de Windows).</summary>
    public string Glyph => (string)GetValue(GlyphProperty);

    private void UpdateGlyph() => SetValue(GlyphPropertyKey, Icons.Of(Kind));
}
