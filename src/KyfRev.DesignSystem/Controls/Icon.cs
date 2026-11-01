using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Icône décorative (police d'icônes de Windows, 16 px) : le lecteur d'écran l'ignore, le libellé
/// qui l'accompagne suffit. Sa police est posée par l'icône elle-même : dans un modèle de données,
/// un style implicite ne l'atteindrait pas.
/// </summary>
public class Icon : TextBlock
{
    private static readonly FontFamily IconFont = new(Tokens.Typography.IconFamily);

    /// <summary>Une icône dans la police d'icônes, centrée verticalement.</summary>
    public Icon()
    {
        FontFamily = IconFont;
        FontSize = 16;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new IconAutomationPeer(this);

    /// <summary>Hors des vues « contrôle » et « contenu » de l'automatisation.</summary>
    private sealed class IconAutomationPeer(Icon owner) : TextBlockAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
