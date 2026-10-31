using System.Windows;
using System.Windows.Controls;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Squelette de chargement : la place d'un contenu qui arrive, qui pulse doucement, et reste
/// immobile si Windows demande de réduire les animations.
/// </summary>
public class Skeleton : Control
{
    /// <summary>Propriété <see cref="IsAnimated"/>.</summary>
    public static readonly DependencyProperty IsAnimatedProperty =
        DependencyProperty.Register(nameof(IsAnimated), typeof(bool), typeof(Skeleton), new PropertyMetadata(true));

    static Skeleton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(typeof(Skeleton)));
    }

    /// <summary>Pulse ; le style le relie au réglage des animations (<see cref="DesignKeys.AnimationsEnabled"/>).</summary>
    public bool IsAnimated
    {
        get => (bool)GetValue(IsAnimatedProperty);
        set => SetValue(IsAnimatedProperty, value);
    }
}
