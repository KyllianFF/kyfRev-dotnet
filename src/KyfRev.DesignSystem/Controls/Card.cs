using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Repère latéral d'une carte (barre de 3 px), selon ce qu'elle présente.</summary>
public enum ItemMarker
{
    /// <summary>Aucun repère.</summary>
    None,

    /// <summary>Une fiche (violet).</summary>
    Sheet,

    /// <summary>Un passage annoté (pervenche).</summary>
    Passage,

    /// <summary>Un rappel de consolidation (mauve).</summary>
    Reminder,
}

/// <summary>Carte : un contenu posé sur la surface, bordé d'un trait, avec un repère latéral au besoin.</summary>
public class Card : ContentControl
{
    /// <summary>Propriété <see cref="Marker"/>.</summary>
    public static readonly DependencyProperty MarkerProperty =
        DependencyProperty.Register(nameof(Marker), typeof(ItemMarker), typeof(Card), new PropertyMetadata(ItemMarker.None));

    static Card()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(Card)));
    }

    /// <summary>Repère latéral ; aucun par défaut.</summary>
    public ItemMarker Marker
    {
        get => (ItemMarker)GetValue(MarkerProperty);
        set => SetValue(MarkerProperty, value);
    }
}
