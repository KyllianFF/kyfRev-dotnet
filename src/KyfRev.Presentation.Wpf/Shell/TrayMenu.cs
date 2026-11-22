using System.Windows;
using System.Windows.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>Les mots de l'icône de la barre d'état.</summary>
public static class TrayText
{
    /// <summary>« kyfRev », au survol de l'icône.</summary>
    public const string Name = "kyfRev";

    /// <summary>« Ouvrir kyfRev ».</summary>
    public const string Open = "Ouvrir kyfRev";

    /// <summary>« Capture rapide ».</summary>
    public const string Capture = "Capture rapide";

    /// <summary>« Réviser maintenant ».</summary>
    public const string Revise = "Réviser maintenant";

    /// <summary>« Paramètres ».</summary>
    public const string Settings = "Paramètres";

    /// <summary>« Quitter kyfRev ».</summary>
    public const string Quit = "Quitter kyfRev";

    /// <summary>Le titre de la bulle montrée la première fois.</summary>
    public const string TipTitle = "kyfRev reste dans la barre d'état";

    /// <summary>Le texte de la bulle montrée la première fois.</summary>
    public const string Tip = "La capture rapide et les rappels restent prêts. Pour quitter : clic droit sur l'icône, « Quitter kyfRev ».";
}

/// <summary>Ce que fait chaque entrée du menu de l'icône.</summary>
/// <param name="Open">Ouvre kyfRev.</param>
/// <param name="Capture">Ouvre la palette de capture.</param>
/// <param name="Revise">Ouvre « Aujourd'hui ».</param>
/// <param name="Settings">Ouvre les paramètres.</param>
/// <param name="Quit">Ferme kyfRev pour de bon.</param>
public sealed record TrayActions(Action Open, Action Capture, Action Revise, Action Settings, Action Quit);

/// <summary>
/// Le menu de l'icône de la barre d'état (rangée 25D), dans le dessin des menus de kyfRev : Ouvrir kyfRev, Capture rapide,
/// Réviser maintenant ; Paramètres ; Quitter kyfRev.
/// </summary>
public static class TrayMenu
{
    /// <summary>Les entrées, dans l'ordre ; « Capture rapide » dit son raccourci global quand il est demandé à Windows.</summary>
    public static IReadOnlyList<Control> Items(TrayActions actions, string? captureKeys)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var open = ActionMenus.Item(TrayText.Open, actions.Open);
        open.FontWeight = FontWeights.SemiBold;
        return
        [
            open,
            ActionMenus.Item(TrayText.Capture, actions.Capture, captureKeys),
            ActionMenus.Item(TrayText.Revise, actions.Revise),
            new Separator(),
            ActionMenus.Item(TrayText.Settings, actions.Settings),
            new Separator(),
            ActionMenus.Item(TrayText.Quit, actions.Quit),
        ];
    }
}
