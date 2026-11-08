using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Un texte que le lecteur d'écran annonce quand il change : déclarer une région active ne suffit pas à WPF, il faut lever
/// l'événement. Poli par défaut ; <c>AutomationProperties.LiveSetting</c> à <c>Assertive</c> pour un refus.
/// </summary>
public sealed class LiveText : TextBlock
{
    static LiveText()
    {
        TextProperty.OverrideMetadata(typeof(LiveText), new FrameworkPropertyMetadata((text, _) => ((LiveText)text).Announce()));
        // Une valeur par défaut : un modèle peut encore la changer, ce qu'il ne pourrait pas contre une valeur locale.
        AutomationProperties.LiveSettingProperty.OverrideMetadata(typeof(LiveText), new UIPropertyMetadata(AutomationLiveSetting.Polite));
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new AutomationPeers.LiveTextPeer(this);

    /// <summary>Annonce le texte, une fois mis en page : celui qui vient de paraître est alors visible pour le lecteur d'écran.</summary>
    private void Announce()
    {
        if (Text.Length > 0 && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UIElementAutomationPeer.CreatePeerForElement(this).RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        }
    }
}
