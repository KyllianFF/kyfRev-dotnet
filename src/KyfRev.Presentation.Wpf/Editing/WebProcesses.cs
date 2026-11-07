using Microsoft.Web.WebView2.Core;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>Les processus du moteur web dont la mort arrête une page de kyfRev.</summary>
internal static class WebProcesses
{
    /// <summary>
    /// La page s'arrête si le moteur ou son propre rendu meurt, ou ne répond plus. Le processus graphique et les utilitaires,
    /// eux, sont relancés par WebView2 sans que la page ne perde rien : sans carte graphique (Windows Sandbox, bureau à
    /// distance), le processus graphique peut s'arrêter dès le démarrage, et ce n'est pas une panne de l'éditeur.
    /// </summary>
    public static bool StopsThePage(CoreWebView2ProcessFailedKind kind) => kind is
        CoreWebView2ProcessFailedKind.BrowserProcessExited
        or CoreWebView2ProcessFailedKind.RenderProcessExited
        or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive;
}
