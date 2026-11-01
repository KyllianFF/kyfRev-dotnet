using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Ce que les composants présentent à UI Automation (lecteurs d'écran, outils d'accessibilité).
/// Sans ces pairs, un contrôle composé n'apparaîtrait pas : son texte, placé dans son modèle,
/// resterait hors de la vue « contrôle ».
/// </summary>
internal static class AutomationPeers
{
    /// <summary>Dialogue : un volet marqué « dialogue », nommé par son titre.</summary>
    internal sealed class DialogPeer(Dialog owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(Dialog);

        protected override string GetNameCore() => NameOr(AutomationProperties.GetName(owner), owner.Title);
    }

    /// <summary>Notification : un groupe nommé par son titre (ou son message), décrit par son message.</summary>
    internal sealed class NotificationPeer(Notification owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Notification);

        protected override string GetNameCore() => NameOr(AutomationProperties.GetName(owner), owner.Title.Length > 0 ? owner.Title : owner.Message);

        protected override string GetHelpTextCore() => NameOr(AutomationProperties.GetHelpText(owner), owner.Message);
    }

    /// <summary>Pastille : un texte, son libellé.</summary>
    internal sealed class PillPeer(Pill owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(Pill);

        protected override string GetNameCore() => NameOr(AutomationProperties.GetName(owner), owner.Text);
    }

    /// <summary>État vide : un groupe nommé par son titre, décrit par son explication ; l'action reste dedans.</summary>
    internal sealed class EmptyStatePeer(EmptyState owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(EmptyState);

        protected override string GetNameCore() => NameOr(AutomationProperties.GetName(owner), owner.Title);

        protected override string GetHelpTextCore() => NameOr(AutomationProperties.GetHelpText(owner), owner.Description);
    }

    /// <summary>Squelette : un texte, « Chargement… ».</summary>
    internal sealed class SkeletonPeer(Skeleton owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(Skeleton);

        protected override string GetNameCore() => NameOr(AutomationProperties.GetName(owner), DesignText.Loading);
    }

    /// <summary>Le nom donné par <c>AutomationProperties</c> s'il y en a un, sinon celui du composant.</summary>
    private static string NameOr(string given, string fallback) => string.IsNullOrEmpty(given) ? fallback : given;
}
