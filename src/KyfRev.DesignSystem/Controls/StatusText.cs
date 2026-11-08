using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Texte d'état : le lecteur d'écran le dit chaque fois qu'il paraît, sans que le focus y vienne. Le réglage
/// <see cref="AutomationProperties.LiveSettingProperty"/> ne suffit pas : il donne seulement la politesse de l'annonce, et
/// WPF n'annonce que ce que le code lui signale. L'apparence reste celle du texte ; les styles <c>Kyf.Text.*</c> s'y appliquent.
/// </summary>
public class StatusText : TextBlock
{
    private bool _waiting;

    /// <summary>Un texte d'état, annoncé sans interrompre.</summary>
    public StatusText()
    {
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        IsVisibleChanged += (_, _) => AnnounceOnceShown();
    }

    /// <summary>Annonce le texte comme une région active, si un client d'UI Automation écoute.</summary>
    protected virtual void Announce()
    {
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            UIElementAutomationPeer.CreatePeerForElement(this).RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    /// <summary>
    /// L'annonce attend la mise en page : le texte est alors à sa place dans l'arbre que lit le lecteur d'écran, et ce qui n'a
    /// paru qu'un instant, le temps qu'une réponse s'installe, n'est pas dit.
    /// </summary>
    private void AnnounceOnceShown()
    {
        if (!IsVisible || _waiting)
        {
            return;
        }
        _waiting = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _waiting = false;
            if (IsVisible)
            {
                Announce();
            }
        });
    }
}
