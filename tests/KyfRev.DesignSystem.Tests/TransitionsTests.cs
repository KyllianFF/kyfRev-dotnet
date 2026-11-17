using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>Les transitions : ce qui apparaît arrive en fondu, sur les durées du design system, et rien ne bouge quand les animations sont réduites.</summary>
[TestClass]
[DoNotParallelize]
public sealed class TransitionsTests
{
    [STATestMethod]
    public void What_appears_fades_in_and_gives_back_its_opacity_and_place()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var element = new Border { Width = 100, Height = 40, Opacity = 0.8 };
        Host.Of(design, element);

        Assert.IsTrue(Transitions.Enter(element), "une transition commence");
        Assert.IsTrue(element.HasAnimatedProperties);
        Assert.AreEqual(0.8, (double)element.GetAnimationBaseValue(UIElement.OpacityProperty), "son opacité propre n'est pas touchée : l'animation la rend à la fin");
        Assert.IsNotInstanceOfType<TranslateTransform>(element.RenderTransform, "sans montée demandée, l'élément ne bouge pas");

        // Une fenêtre de dialogue monte un peu en apparaissant.
        Assert.IsTrue(Transitions.Enter(element, DesignKeys.FastDuration, rise: Transitions.DialogRise));
        var move = (TranslateTransform)element.RenderTransform;
        Assert.IsTrue(move.HasAnimatedProperties);
        Assert.AreEqual(0d, (double)move.GetAnimationBaseValue(TranslateTransform.YProperty), "sa place à la fin est la sienne");

        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Enter(null!));
    }

    [STATestMethod]
    public void With_reduced_motion_nothing_moves_and_a_running_transition_stops()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var element = new Border { Width = 100, Height = 40 };
        Host.Of(design, element);
        Assert.IsTrue(Transitions.Enter(element, rise: 8));
        Assert.AreEqual(PopupAnimation.Fade, design.Dictionary[DesignKeys.PopupMotion], "les menus s'ouvrent en fondu");

        design.ReducedMotion = true;

        Assert.IsFalse(Transitions.Enter(element, rise: 8), "durée nulle : montré tel quel");
        Assert.IsFalse(element.HasAnimatedProperties, "la transition en cours s'arrête");
        Assert.IsFalse(((TranslateTransform)element.RenderTransform).HasAnimatedProperties);
        Assert.AreEqual(1d, element.Opacity);
        Assert.AreEqual(PopupAnimation.None, design.Dictionary[DesignKeys.PopupMotion], "et les menus s'ouvrent d'un coup");

        // Un élément hors de tout thème n'a pas de durée : il est montré tel quel.
        Assert.IsFalse(Transitions.Enter(new Border()));
    }

    [STATestMethod]
    public void Tests_can_make_every_transition_end_at_once()
    {
        try
        {
            Transitions.Skip = true;
            var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
            var element = new Border { Width = 100, Height = 40 };
            Host.Of(design, element);

            Assert.IsFalse(Transitions.Enter(element));
            Assert.IsFalse(element.HasAnimatedProperties);
            Assert.AreEqual(PopupAnimation.None, design.Dictionary[DesignKeys.PopupMotion]);
        }
        finally
        {
            Transitions.Skip = false;
        }
    }

    [STATestMethod]
    public void A_dialog_that_opens_fades_in_and_rises()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var dialog = new Dialog { Title = "Renommer la fiche", Content = new TextBox() };
        var host = Host.Of(design, dialog);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        // Jamais ouverte : son modèle n'est pas encore en place, et la première ouverture s'anime quand même.
        dialog.IsOpen = true;
        var card = Host.Part<FrameworkElement>(dialog, "Card");

        Assert.IsTrue(card.HasAnimatedProperties, "sa carte arrive en fondu");
        Assert.IsInstanceOfType<TranslateTransform>(card.RenderTransform, "en montant un peu");

        // Animations réduites : elle s'ouvre d'un coup.
        dialog.IsOpen = false;
        design.ReducedMotion = true;
        dialog.IsOpen = true;
        Assert.IsFalse(card.HasAnimatedProperties);
    }

    [STATestMethod]
    public void A_dialog_without_a_main_action_closes_by_its_plain_button()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: true);
        var dialog = new Dialog { Title = "Historique", PrimaryButtonText = string.Empty, SecondaryButtonText = "Fermer", IsOpen = true };
        var host = Host.Of(design, dialog);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        var primary = Host.Part<Button>(dialog, Dialog.PrimaryButtonPart);
        var secondary = Host.Part<Button>(dialog, Dialog.SecondaryButtonPart);
        Assert.AreEqual(Visibility.Collapsed, primary.Visibility, "aucun bouton plein : celui du contenu reste le seul");
        Assert.AreEqual(Visibility.Visible, secondary.Visibility);
        Assert.AreEqual("Fermer", secondary.Content);
        Assert.IsTrue(secondary.IsCancel, "Échap ferme toujours");
        Assert.AreEqual(new Thickness(0), secondary.Margin, "plus de place réservée à côté de lui");
    }
}
