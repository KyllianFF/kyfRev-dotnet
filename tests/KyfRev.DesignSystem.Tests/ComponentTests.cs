using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>Les composants, mis en page sans fenêtre : modèle appliqué, comportement, accessibilité.</summary>
[TestClass]
public sealed class ComponentTests
{
    [STATestMethod]
    public void A_pill_shows_an_icon_and_its_label_never_the_color_alone()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var pill = new Pill { Kind = PillKind.Overdue, Text = "Retard 4 j" };
        Host.Of(design, pill);

        Assert.AreEqual(Icons.Clock, pill.Glyph);
        Assert.AreEqual("Retard 4 j", AutomationProperties.GetName(pill));
        Assert.AreEqual(ThemeColors.For(ThemeKind.Light)[ColorRole.Critical].ToColor(), ((SolidColorBrush)pill.Foreground).Color);
        var glyphs = Enum.GetValues<PillKind>().Select(kind =>
        {
            pill.Kind = kind;
            return pill.Glyph;
        }).ToList();
        Assert.HasCount(glyphs.Count, glyphs.Distinct());
        pill.Kind = PillKind.Reminder;
        Assert.AreEqual(ThemeColors.For(ThemeKind.Light)[ColorRole.PillReminderBackground].ToColor(), ((SolidColorBrush)pill.Background).Color);
    }

    [STATestMethod]
    public void A_card_shows_the_marker_of_what_it_presents()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var card = new Card { Content = "Mitose" };
        Host.Of(design, card);
        var marker = Host.Part<Border>(card, "Marker");
        Assert.AreEqual(Visibility.Collapsed, marker.Visibility);

        foreach (var (kind, role) in new[] { (ItemMarker.Sheet, ColorRole.MarkerSheet), (ItemMarker.Passage, ColorRole.MarkerPassage), (ItemMarker.Reminder, ColorRole.MarkerReminder) })
        {
            card.Marker = kind;
            Assert.AreEqual(Visibility.Visible, marker.Visibility);
            Assert.AreEqual(ThemeColors.For(ThemeKind.Dark)[role].ToColor(), ((SolidColorBrush)marker.Background).Color);
        }
        Assert.AreEqual(ItemMarker.Reminder, card.Marker);
    }

    [STATestMethod]
    public void A_notification_is_announced_and_closes()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var notification = new Notification { Title = "Sauvegarde faite", Message = "Le coffre est sauvegardé." };
        Host.Of(design, notification);
        Assert.AreEqual(Icons.Info, notification.Glyph);
        Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(notification));
        Assert.AreEqual("Sauvegarde faite", AutomationProperties.GetName(notification));

        notification.Severity = NotificationSeverity.Critical;
        Assert.AreEqual(Icons.Error, notification.Glyph);
        Assert.AreEqual(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(notification));
        notification.Severity = NotificationSeverity.Success;
        Assert.AreEqual(Icons.Success, notification.Glyph);
        notification.Severity = NotificationSeverity.Warning;
        Assert.AreEqual(Icons.Warning, notification.Glyph);

        var closed = 0;
        notification.Closed += (_, _) => closed++;
        var close = Host.Part<Button>(notification, Notification.CloseButtonPart);
        Assert.AreEqual(DesignText.Close, AutomationProperties.GetName(close));
        close.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        Assert.IsFalse(notification.IsOpen);
        Assert.AreEqual(1, closed);
        Assert.AreEqual(Visibility.Collapsed, notification.Visibility);
        notification.IsClosable = false;
        notification.IsOpen = true;
        Host.Layout((FrameworkElement)notification.Parent);
        Assert.AreEqual(Visibility.Collapsed, close.Visibility);
        Assert.IsFalse(notification.IsClosable);
        Assert.AreEqual("Le coffre est sauvegardé.", notification.Message);
    }

    [STATestMethod]
    public void A_dialog_closes_with_the_chosen_button_or_escape()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var dialog = new Dialog { Title = "Mettre à la corbeille ?", Content = "La fiche reste récupérable.", PrimaryButtonText = "Mettre à la corbeille" };
        Host.Of(design, dialog);
        Assert.AreEqual(Visibility.Collapsed, dialog.Visibility);
        Assert.AreEqual(DesignText.Cancel, dialog.SecondaryButtonText);

        var choices = new List<DialogChoice>();
        dialog.Closed += (_, e) => choices.Add(e.Choice);
        dialog.IsOpen = true;
        Host.Flush();
        Assert.AreEqual(Visibility.Visible, dialog.Visibility);
        Host.Part<Button>(dialog, Dialog.PrimaryButtonPart).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.IsFalse(dialog.IsOpen);

        dialog.IsOpen = true;
        Host.Part<Button>(dialog, Dialog.SecondaryButtonPart).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        dialog.IsOpen = true;
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai"));
        dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });

        CollectionAssert.AreEqual(new[] { DialogChoice.Primary, DialogChoice.Secondary, DialogChoice.None }, choices);
        Assert.AreEqual("Mettre à la corbeille", dialog.PrimaryButtonText);
        Assert.AreEqual("Mettre à la corbeille ?", dialog.Title);
        dialog.SecondaryButtonText = string.Empty;
        dialog.IsOpen = true;
        Host.Layout((FrameworkElement)dialog.Parent);
        Assert.AreEqual(Visibility.Collapsed, Host.Part<Button>(dialog, Dialog.SecondaryButtonPart).Visibility);
    }

    [STATestMethod]
    public void An_empty_state_offers_its_action()
    {
        var action = new Button { Content = "Créer une fiche" };
        var empty = new EmptyState { Title = "Aucune fiche", Description = "Créez votre première fiche.", Action = action, Glyph = Icons.Document };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), empty);

        Assert.AreSame(action, empty.Action);
        Assert.IsTrue(action.IsVisible || action.ActualWidth > 0);
        Assert.AreEqual("Aucune fiche", empty.Title);
        Assert.AreEqual("Créez votre première fiche.", empty.Description);
        Assert.AreEqual(Icons.Document, empty.Glyph);
    }

    [STATestMethod]
    public void A_skeleton_stays_still_when_animations_are_reduced()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var skeleton = new Skeleton { Height = 16 };
        Host.Of(design, skeleton);
        Assert.IsTrue(skeleton.IsAnimated);
        Assert.AreEqual(DesignText.Loading, AutomationProperties.GetName(skeleton));

        design.ReducedMotion = true;

        Assert.IsFalse(skeleton.IsAnimated);
    }

    [STATestMethod]
    public void A_field_shows_its_placeholder_while_empty()
    {
        var field = new TextBox();
        Field.SetPlaceholder(field, "Rechercher une fiche");
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), field);
        var placeholder = Host.Part<TextBlock>(field, "Placeholder");

        Assert.AreEqual(Visibility.Visible, placeholder.Visibility);
        Assert.AreEqual("Rechercher une fiche", placeholder.Text);
        Assert.AreEqual("Rechercher une fiche", AutomationProperties.GetHelpText(field));
        field.Text = "Mitose";
        Assert.AreEqual(Visibility.Collapsed, placeholder.Visibility);
        Assert.AreEqual("Rechercher une fiche", Field.GetPlaceholder(field));
        Assert.ThrowsExactly<ArgumentNullException>(() => Field.GetPlaceholder(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Field.SetPlaceholder(null!, "x"));
    }

    [STATestMethod]
    public void Every_control_shows_its_focus()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var ring = (Style)design.Dictionary["Kyf.FocusVisual"];
        var button = new Button { Content = "Valider" };
        var item = new MenuItem { Header = "Ouvrir" };
        var panel = new StackPanel { Children = { button, new Menu { Items = { item } } } };
        Host.Of(design, panel);

        Assert.AreSame(ring, button.FocusVisualStyle);
        Assert.AreSame(ring, item.FocusVisualStyle);
        Assert.AreEqual(32.0, button.MinHeight);
    }
}
