using System.Windows;
using System.Windows.Input;

namespace KyfRev.DesignSystem.Controls;

/// <summary>Où en est le focus clavier, pour le rendre quand ce qui le tenait disparaît.</summary>
public static class KeyboardFocus
{
    /// <summary>
    /// Le focus est perdu : il n'est sur rien, il est tombé sur la fenêtre, ou l'élément qui le tient n'est plus visible ou
    /// plus actif (la touche Tab repartirait alors du haut de la fenêtre).
    /// </summary>
    public static bool IsLost() => Keyboard.FocusedElement is null or Window or UIElement { IsVisible: false } or UIElement { IsEnabled: false };
}
