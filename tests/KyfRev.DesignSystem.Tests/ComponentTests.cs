using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
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
        Assert.AreEqual("Retard 4 j", Peer(pill).GetName());
        Assert.AreEqual(AutomationControlType.Text, Peer(pill).GetAutomationControlType());
        Assert.AreEqual(nameof(Pill), Peer(pill).GetClassName());
        // Un retard est ambre (avertissement) : le rouge brique est réservé aux erreurs.
        Assert.AreEqual(ThemeColors.For(ThemeKind.Light)[ColorRole.Warning].ToColor(), ((SolidColorBrush)pill.Foreground).Color);
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
        Assert.AreEqual("Sauvegarde faite", Peer(notification).GetName());
        Assert.AreEqual("Le coffre est sauvegardé.", Peer(notification).GetHelpText());
        Assert.AreEqual(AutomationControlType.Group, Peer(notification).GetAutomationControlType());
        Assert.AreEqual(nameof(Notification), Peer(notification).GetClassName());

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
        notification.Title = string.Empty;
        Assert.AreEqual("Le coffre est sauvegardé.", Peer(notification).GetName());
        AutomationProperties.SetName(notification, "Alerte");
        Assert.AreEqual("Alerte", Peer(notification).GetName());
    }

    [STATestMethod]
    public void A_dialog_closes_with_the_chosen_button_or_escape()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var dialog = new Dialog { Title = "Mettre à la corbeille ?", Content = "La fiche reste récupérable.", PrimaryButtonText = "Mettre à la corbeille" };
        Host.Of(design, dialog);
        Assert.AreEqual(Visibility.Collapsed, dialog.Visibility);
        Assert.AreEqual(DesignText.Cancel, dialog.SecondaryButtonText);
        Assert.AreEqual("Mettre à la corbeille ?", Peer(dialog).GetName());
        Assert.AreEqual(AutomationControlType.Pane, Peer(dialog).GetAutomationControlType());
        Assert.AreEqual(nameof(Dialog), Peer(dialog).GetClassName());
        Assert.IsTrue(Peer(dialog).IsDialog());

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
    public void Closing_keeps_a_one_way_binding_of_is_open()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var source = new Switch { IsOn = true };
        var dialog = new Dialog { Title = "Recommencer à J0 ?", PrimaryButtonText = "Recommencer", DataContext = source };
        var notification = new Notification { Message = "Fiche archivée.", DataContext = source };
        foreach (var (control, property) in new (FrameworkElement, DependencyProperty)[] { (dialog, Dialog.IsOpenProperty), (notification, Notification.IsOpenProperty) })
        {
            control.SetBinding(property, new System.Windows.Data.Binding(nameof(Switch.IsOn)) { Mode = System.Windows.Data.BindingMode.OneWay });
        }
        Host.Of(design, dialog);
        Host.Of(design, notification);

        dialog.Close(DialogChoice.Primary);
        notification.Close();
        Assert.IsFalse(dialog.IsOpen);
        Assert.IsFalse(notification.IsOpen);

        // La source dit encore « ouvert » ; quand elle change, la liaison suit toujours.
        source.IsOn = false;
        source.IsOn = true;
        Assert.IsTrue(dialog.IsOpen);
        Assert.IsTrue(notification.IsOpen);
    }

    [STATestMethod]
    public void An_empty_state_offers_its_action()
    {
        var action = new Button { Content = "Créer une fiche" };
        var empty = new EmptyState { Title = "Aucune fiche", Description = "Créez votre première fiche.", Action = action, Glyph = Icons.Document };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), empty);

        Assert.AreSame(action, empty.Action);
        Assert.AreEqual("Aucune fiche", Peer(empty).GetName());
        Assert.AreEqual("Créez votre première fiche.", Peer(empty).GetHelpText());
        Assert.AreEqual(AutomationControlType.Group, Peer(empty).GetAutomationControlType());
        Assert.AreEqual(nameof(EmptyState), Peer(empty).GetClassName());
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
        Assert.AreEqual(DesignText.Loading, Peer(skeleton).GetName());
        Assert.AreEqual(AutomationControlType.Text, Peer(skeleton).GetAutomationControlType());
        Assert.AreEqual(nameof(Skeleton), Peer(skeleton).GetClassName());

        design.ReducedMotion = true;

        Assert.IsFalse(skeleton.IsAnimated);
    }

    [STATestMethod]
    public void A_long_check_box_label_wraps_instead_of_being_cut()
    {
        var label = new TextBlock { Text = "Un libellé assez long pour ne pas tenir sur une seule ligne de cette largeur", TextWrapping = TextWrapping.Wrap };
        var box = new CheckBox { Content = label };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), box, width: 200);

        Assert.IsLessThanOrEqualTo(200, box.ActualWidth);
        Assert.IsLessThan(200, label.ActualWidth, "le libellé garde la place de la case");
        Assert.IsGreaterThan(label.FontSize * 2, label.ActualHeight, "il passe à la ligne");
    }

    [STATestMethod]
    public void A_field_of_several_lines_keeps_its_text_off_the_edge()
    {
        var single = new TextBox();
        var several = new TextBox { AcceptsReturn = true };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), new StackPanel { Children = { single, several } });

        Assert.AreEqual(0, single.Padding.Top);
        Assert.AreEqual(8, several.Padding.Top);
    }

    [STATestMethod]
    public void Typed_text_starts_where_the_placeholder_does()
    {
        var field = new TextBox();
        Field.SetPlaceholder(field, "Rechercher une fiche");
        var host = Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), field);
        var placeholder = Host.Part<TextBlock>(field, "Placeholder");
        var start = placeholder.TranslatePoint(new Point(placeholder.Padding.Left, 0), field).X;

        field.Text = "Mitose";
        Host.Layout(host);

        Assert.AreEqual(start, field.GetRectFromCharacterIndex(0).Left, 1, "le texte ne saute pas à la première frappe");
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

    private static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element);

    [STATestMethod]
    public void A_selected_list_item_is_filled_and_bold_never_the_color_alone()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var list = new ListBox { ItemsSource = new[] { "Aujourd'hui", "Fiches" } };
        Host.Of(design, list);
        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
        var row = Host.Part<Border>(item, "Row");
        Assert.AreEqual(FontWeights.Normal, item.FontWeight);

        list.SelectedIndex = 1;

        Assert.AreEqual(FontWeights.SemiBold, item.FontWeight);
        Assert.AreEqual(ThemeColors.For(ThemeKind.Light)[ColorRole.NavigationActiveBackground].ToColor(), ((SolidColorBrush)row.Background).Color);
        Assert.AreEqual(ThemeColors.For(ThemeKind.Light)[ColorRole.NavigationActiveText].ToColor(), ((SolidColorBrush)item.Foreground).Color);
        Assert.AreSame(design.Dictionary["Kyf.FocusVisual"], item.FocusVisualStyle);
        Assert.IsGreaterThanOrEqualTo(44.0, item.MinHeight);
    }

    [STATestMethod]
    public void A_decorative_icon_stays_out_of_the_screen_reader()
    {
        var icon = new Icon { Text = Icons.Search };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), icon);

        Assert.IsFalse(Peer(icon).IsControlElement());
        Assert.IsFalse(Peer(icon).IsContentElement());
        Assert.AreEqual(16.0, icon.FontSize);
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
        Assert.AreEqual(40.0, button.MinHeight);
    }
}

/// <summary>Une source de liaison qui signale ses changements.</summary>
internal sealed class Switch : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isOn;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            _isOn = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOn)));
        }
    }
}
