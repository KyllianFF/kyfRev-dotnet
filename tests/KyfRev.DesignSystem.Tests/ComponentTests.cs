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
    public void A_status_text_is_announced_each_time_it_appears_with_its_text_in_place()
    {
        var text = new RecordingStatus { Text = "Avant", Visibility = Visibility.Collapsed };
        var window = InWindow(text);
        try
        {
            Host.Flush();
            Assert.IsEmpty(text.Announced, "caché, il ne dit rien");

            text.Visibility = Visibility.Visible;
            text.Text = "Rien ne correspond.";
            Assert.IsEmpty(text.Announced, "pas avant la mise en page");
            Host.Flush();
            Assert.AreEqual("Rien ne correspond.", text.Announced.Single());

            text.Visibility = Visibility.Collapsed;
            Host.Flush();
            text.Visibility = Visibility.Visible;
            Host.Flush();
            Assert.HasCount(2, text.Announced, "il reparaît : il se redit");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_status_text_that_only_flashes_is_not_announced()
    {
        var text = new RecordingStatus { Text = "Rien ne correspond.", Visibility = Visibility.Collapsed };
        var window = InWindow(text);
        try
        {
            Host.Flush();

            text.Visibility = Visibility.Visible;
            text.Visibility = Visibility.Collapsed;
            Host.Flush();
            Assert.IsEmpty(text.Announced, "le temps qu'une réponse s'installe");

            text.Visibility = Visibility.Visible;
            text.Visibility = Visibility.Collapsed;
            text.Visibility = Visibility.Visible;
            Host.Flush();
            Assert.HasCount(1, text.Announced, "une seule fois, même paru deux fois avant la mise en page");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_status_text_is_a_polite_text_that_keeps_its_style()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var text = new StatusText { Text = "Citation trouvée.", Style = (Style)design.Dictionary["Kyf.Text.Secondary"] };
        var window = InWindow(text);
        try
        {
            Host.Flush();

            Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(text));
            Assert.AreEqual("Citation trouvée.", Peer(text).GetName());
            Assert.AreEqual(AutomationLiveSetting.Polite, Peer(text).GetLiveSetting());
            Assert.AreEqual(AutomationControlType.Text, Peer(text).GetAutomationControlType());
        }
        finally
        {
            window.Close();
        }
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
    public void An_open_dialog_gives_the_focus_to_its_content_then_to_its_buttons()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var field = new TextBox();
        var dialog = new Dialog { Title = "Renommer la fiche", Content = field };
        var host = Host.Of(design, dialog);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };

        dialog.IsOpen = true;
        Host.Layout(host);
        Host.Flush();

        Assert.AreSame(field, FocusedIn(field), "le premier champ du contenu, pas « Annuler »");
        var secondary = Host.Part<Button>(dialog, Dialog.SecondaryButtonPart);
        var primary = Host.Part<Button>(dialog, Dialog.PrimaryButtonPart);
        Assert.IsTrue(field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.AreSame(secondary, FocusedIn(field), "puis les boutons, dans l'ordre où ils se lisent");
        Assert.IsTrue(secondary.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.AreSame(primary, FocusedIn(field));
        Assert.IsTrue(primary.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.AreSame(field, FocusedIn(field), "le focus reste dans le dialogue");
        Assert.IsTrue(secondary.TranslatePoint(default, host).Y > field.TranslatePoint(default, host).Y, "les boutons restent sous le contenu");
    }

    [STATestMethod]
    public void Enter_on_a_check_box_does_not_tick_it()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var box = new CheckBox { Content = "Effacer pour de bon" };
        var host = Host.Of(design, box);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Flush();

        Assert.IsFalse(KeyboardNavigation.GetAcceptsReturn(box), "Entrée va au bouton par défaut");
        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.IsFalse(box.IsChecked, "Entrée ne coche rien par mégarde");
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void What_cannot_be_undone_shows_and_enter_does_not_trigger_it(ThemeKind theme)
    {
        var design = new DesignResources(theme, reducedMotion: false);
        var dialog = new Dialog { Title = "Supprimer définitivement ?", IsOpen = true };
        Host.Of(design, dialog);
        Host.Flush();
        var button = Host.Part<Button>(dialog, Dialog.PrimaryButtonPart);
        Assert.IsTrue(button.IsDefault);
        Assert.IsFalse(dialog.IsDestructive);

        dialog.IsDestructive = true;
        Host.Flush();

        Assert.IsTrue(dialog.IsDestructive);
        Assert.IsFalse(button.IsDefault);
        var text = (SolidColorBrush)button.Foreground;
        var ground = (SolidColorBrush)button.Background;
        Assert.IsGreaterThanOrEqualTo(4.5, new TokenColor(text.Color.A, text.Color.R, text.Color.G, text.Color.B).ContrastWith(new TokenColor(ground.Color.A, ground.Color.R, ground.Color.G, ground.Color.B)));
        Assert.AreEqual(ThemeColors.For(theme)[ColorRole.CriticalBackground], new TokenColor(ground.Color.A, ground.Color.R, ground.Color.G, ground.Color.B));

        dialog.IsDestructive = false;
        Host.Flush();

        Assert.IsTrue(button.IsDefault);
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

    /// <summary>Une fenêtre hors de l'écran : seul un élément dans une vraie fenêtre est visible.</summary>
    private static Window InWindow(UIElement element)
    {
        var window = new Window { Content = element, Left = -10000, Top = -10000, Width = 300, Height = 200, ShowActivated = false };
        window.Show();
        return window;
    }

    /// <summary>Note ce qui serait annoncé : sans lecteur d'écran, UI Automation n'écoute rien.</summary>
    private sealed class RecordingStatus : StatusText
    {
        public List<string> Announced { get; } = [];

        protected override void Announce()
        {
            base.Announce();
            Announced.Add(Text);
        }
    }

    /// <summary>L'élément qui a le focus dans la portée de <paramref name="element"/>.</summary>
    private static IInputElement? FocusedIn(DependencyObject element) => FocusManager.GetFocusedElement(FocusManager.GetFocusScope(element));

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
        Assert.AreSame(design.Dictionary["Kyf.FocusVisual.Inset"], item.FocusVisualStyle);
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
        var inset = (Style)design.Dictionary["Kyf.FocusVisual.Inset"];
        var button = new Button { Content = "Valider" };
        var item = new MenuItem { Header = "Ouvrir" };
        var panel = new StackPanel { Children = { button, new Menu { Items = { item } } } };
        Host.Of(design, panel);

        Assert.AreSame(ring, button.FocusVisualStyle);
        Assert.AreSame(inset, item.FocusVisualStyle);
        Assert.AreEqual(40.0, button.MinHeight);
    }

    [STATestMethod]
    public void An_adaptive_row_keeps_its_text_readable_or_stacks_it_first()
    {
        var leading = new Border { Width = 200, Height = 20 };
        var main = new TextBlock { Text = "Étape attendue : J3, en retard de dix jours", TextWrapping = TextWrapping.Wrap };
        var trailing = new Border { Width = 300, Height = 30 };
        AdaptiveRow.SetPart(leading, AdaptiveRowPart.Leading);
        AdaptiveRow.SetPart(trailing, AdaptiveRowPart.Trailing);
        var row = new AdaptiveRow { MainMinWidth = 200 };
        row.Children.Add(leading);
        row.Children.Add(main);
        row.Children.Add(trailing);
        Assert.AreEqual(AdaptiveRowPart.Main, AdaptiveRow.GetPart(main), "le texte par défaut");

        void Lay(double width)
        {
            row.Measure(new Size(width, double.PositiveInfinity));
            row.Arrange(new Rect(0, 0, width, row.DesiredSize.Height));
        }

        Lay(1000);
        Assert.IsFalse(row.IsStacked);
        Assert.AreEqual(1000 - 200 - 300 - 32, main.ActualWidth, 0.5, "le texte prend ce qui reste");
        Assert.AreEqual(700, VisualTreeHelper.GetOffset(trailing).X, 0.5, "ce qui suit est calé à droite");

        Lay(700);
        Assert.IsTrue(row.IsStacked, "168 pixels pour le texte : moins que les 200 qu'il doit garder");
        Assert.AreEqual(0, VisualTreeHelper.GetOffset(main).Y, "le texte d'abord");
        Assert.AreEqual(main.DesiredSize.Height + 12, VisualTreeHelper.GetOffset(leading).Y, 0.5);
        Assert.IsGreaterThan(VisualTreeHelper.GetOffset(leading).Y, VisualTreeHelper.GetOffset(trailing).Y);
        Assert.AreEqual(700, main.ActualWidth, 0.5);

        trailing.Visibility = Visibility.Collapsed;
        Lay(700);
        Assert.IsFalse(row.IsStacked, "sans ce qui suit, le texte a de nouveau sa place");

        row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.IsFalse(row.IsStacked, "sans limite de largeur, tout tient sur une ligne");
    }

    [STATestMethod]
    public void The_quick_actions_share_a_row_above_the_list()
    {
        var panel = new ActionMenuPanel();
        var (first, second) = (new Border { Width = 60, Height = 50, HorizontalAlignment = HorizontalAlignment.Left }, new Border { Width = 100, Height = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
        ActionMenuPanel.SetIsQuick(first, true);
        ActionMenuPanel.SetIsQuick(second, true);
        var hidden = new Border { Width = 500, Height = 500, Visibility = Visibility.Collapsed };
        ActionMenuPanel.SetIsQuick(hidden, true);
        var (rule, entry) = (new Border { Height = 1 }, new Border { Width = 120, Height = 40 });
        foreach (var child in new[] { first, rule, second, hidden, entry })
        {
            panel.Children.Add(child);
        }

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Assert.AreEqual(new Size(200, 91), panel.DesiredSize, "deux cases de la largeur de la plus grande, puis la liste");

        panel.Arrange(new Rect(new Size(300, 91)));

        Assert.AreEqual(new Point(0, 0), first.TranslatePoint(new Point(0, 0), panel));
        Assert.AreEqual(150, second.TranslatePoint(new Point(0, 0), panel).X, 0.01);
        Assert.AreEqual(0, second.TranslatePoint(new Point(0, 0), panel).Y, 0.01, "dans la même rangée, quel que soit l'ordre");
        Assert.AreEqual(50, rule.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.AreEqual(51, entry.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenuPanel.GetIsQuick(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenuPanel.SetIsQuick(null!, true));
    }

    [STATestMethod]
    public void A_menu_without_quick_action_is_a_plain_pile()
    {
        var panel = new ActionMenuPanel();
        var (first, second) = (new Border { Width = 60, Height = 30 }, new Border { Width = 100, Height = 40 });
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        panel.Arrange(new Rect(panel.DesiredSize));

        Assert.AreEqual(new Size(100, 70), panel.DesiredSize);
        Assert.AreEqual(30, second.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
    }

    [STATestMethod]
    public void A_text_field_has_its_own_menu_with_the_clipboard_on_top()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var (one, two) = (new TextBox(), new TextBox());
        Host.Of(design, new StackPanel { Children = { one, two } });
        Assert.IsNull(one.ContextMenu, "un menu n'existe que pour un champ chargé");
        foreach (var field in new[] { one, two })
        {
            field.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        }

        var menu = one.ContextMenu;

        Assert.IsNotNull(menu);
        Assert.IsNotNull(two.ContextMenu);
        Assert.AreNotSame(menu, two.ContextMenu, "à chaque champ le sien");
        menu.Resources.MergedDictionaries.Add(design.Dictionary);
        var items = menu.Items.OfType<MenuItem>().ToList();
        CollectionAssert.AreEqual(new[] { DesignText.Cut, DesignText.Copy, DesignText.Paste, DesignText.SelectAll }, items.Select(item => (string)item.Header).ToList());
        Assert.AreEqual(ApplicationCommands.Cut, items[0].Command);
        Assert.AreEqual(ApplicationCommands.Copy, items[1].Command);
        Assert.AreEqual(ApplicationCommands.Paste, items[2].Command);
        Assert.AreEqual(ApplicationCommands.SelectAll, items[3].Command);
        Assert.IsTrue(ActionMenuPanel.GetIsQuick(items[0]));
        Assert.IsFalse(ActionMenuPanel.GetIsQuick(items[3]));
    }

    [STATestMethod]
    public void A_password_field_has_a_menu_that_only_pastes()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var password = new PasswordBox();
        Host.Of(design, password);
        Assert.IsNull(password.ContextMenu, "un menu n'existe que pour un champ chargé");

        password.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        var item = password.ContextMenu!.Items.OfType<MenuItem>().Single();
        Assert.AreEqual(ApplicationCommands.Paste, item.Command, "rien ne se coupe ni ne se copie d'un mot de passe");
        Assert.AreEqual(DesignText.Paste, item.Header);

        // Un menu donné reste.
        var own = new ContextMenu();
        var given = new PasswordBox { ContextMenu = own };
        Host.Of(design, given);
        given.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.AreSame(own, given.ContextMenu);
    }

    [STATestMethod]
    public void A_field_keeps_the_menu_it_was_given()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var own = new ContextMenu();
        var field = new TextBox { ContextMenu = own };
        Host.Of(design, field);

        field.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.AreSame(own, field.ContextMenu);
    }

    [STATestMethod]
    public void The_tools_have_their_own_row_under_the_quick_actions()
    {
        var panel = new ActionMenuPanel();
        var (quick, tool, otherTool, entry) = (new Border { Height = 50 }, new Border { Width = 40, Height = 36 }, new Border { Width = 40, Height = 36 }, new Border { Height = 40 });
        ActionMenuPanel.SetIsQuick(quick, true);
        ActionMenuPanel.SetIsTool(tool, true);
        ActionMenuPanel.SetIsTool(otherTool, true);
        foreach (var child in new[] { entry, tool, quick, otherTool })
        {
            panel.Children.Add(child);
        }

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        panel.Arrange(new Rect(new Size(200, panel.DesiredSize.Height)));

        Assert.AreEqual(126d, panel.DesiredSize.Height);
        Assert.AreEqual(0, quick.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.AreEqual(50, tool.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.AreEqual(50, otherTool.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.AreEqual(86, entry.TranslatePoint(new Point(0, 0), panel).Y, 0.01);
        Assert.IsTrue(ActionMenuPanel.GetIsTool(tool));
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenuPanel.GetIsTool(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenuPanel.SetIsTool(null!, true));
    }

    [STATestMethod]
    public void What_can_be_clicked_shows_the_hand()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var tab = new ToggleButton();
        tab.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Button.Tab");
        var tool = new MenuItem { Icon = new Icon { Text = Icons.Bold } };
        tool.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.MenuItem.Tool");
        var quick = new MenuItem { Header = "Ouvrir" };
        quick.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.MenuItem.Quick");
        var clickable = new Control[] { new Button(), new ToggleButton(), tab, new CheckBox(), new ComboBox(), new ListBoxItem(), new MenuItem(), tool, quick };
        var host = new StackPanel();
        foreach (var control in clickable)
        {
            host.Children.Add(control);
        }
        Host.Of(design, host);

        foreach (var control in clickable)
        {
            Assert.AreEqual(Cursors.Hand, control.Cursor, control.GetType().Name);
        }
    }

    [STATestMethod]
    public void A_tab_has_a_rounded_hover_and_a_straight_line()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var tab = new ToggleButton { Content = "Général", IsChecked = true };
        tab.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.Button.Tab");
        Host.Of(design, tab);
        var colors = ThemeColors.For(ThemeKind.Light);

        var chrome = Host.Part<Border>(tab, "Chrome");
        var line = Host.Part<Border>(tab, "Line");

        Assert.AreEqual(new CornerRadius(Radii.Control), chrome.CornerRadius, "comme tous les survols de l'application");
        Assert.AreEqual(new CornerRadius(0), line.CornerRadius);
        Assert.AreEqual(colors[ColorRole.Primary].ToColor(), ((SolidColorBrush)line.Background).Color);
        tab.IsEnabled = false;
        Host.Layout(tab.Parent as FrameworkElement ?? tab);
        Assert.AreEqual(0.5, Host.Part<Grid>(tab, "Root").Opacity, 0.01);
    }

    [STATestMethod]
    public void A_quick_action_shows_its_icon_and_label_and_answers_the_pointer()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var quick = new MenuItem { Header = "Ouvrir", Icon = new Icon { Text = Icons.Document } };
        quick.SetResourceReference(FrameworkElement.StyleProperty, "Kyf.MenuItem.Quick");
        var host = Host.Of(design, quick);

        Assert.IsTrue(ActionMenuPanel.GetIsQuick(quick));
        quick.IsEnabled = false;
        Host.Layout(host);
        Assert.AreEqual(0.5, ((Border)VisualTreeHelper.GetChild(quick, 0)).Opacity, 0.01);
    }

    [STATestMethod]
    public void A_choice_list_shows_the_member_it_is_told_to()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var choices = new ComboBox { ItemsSource = new[] { new KeyValuePair<int, string>(1, "Clair") }, DisplayMemberPath = "Value", SelectedIndex = 0 };
        Host.Of(design, choices);

        CollectionAssert.Contains(Texts(choices).Select(text => text.Text).ToList(), "Clair");

        static IEnumerable<TextBlock> Texts(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock text)
                {
                    yield return text;
                }
                foreach (var nested in Texts(child))
                {
                    yield return nested;
                }
            }
        }
    }

    [STATestMethod]
    public void A_choice_list_follows_the_dark_theme()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var choices = new ComboBox { ItemsSource = new[] { "Hortense", "Zira" }, SelectedIndex = 0 };
        Host.Of(design, choices);
        var colors = ThemeColors.For(ThemeKind.Dark);

        var frame = Host.Part<Border>(Host.Part<ToggleButton>(choices, "Toggle"), "Frame");

        Assert.AreEqual(colors[ColorRole.Surface].ToColor(), ((SolidColorBrush)frame.Background).Color);
        Assert.AreEqual(colors[ColorRole.Text].ToColor(), ((SolidColorBrush)choices.Foreground).Color);
        Assert.AreSame(design.Dictionary["Kyf.FocusVisual"], choices.FocusVisualStyle);
        Assert.IsGreaterThanOrEqualTo(40.0, choices.MinHeight);
    }

    [STATestMethod]
    public void The_focus_ring_of_a_row_stays_inside_the_row()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var ring = new Control { Style = (Style)design.Dictionary["Kyf.FocusVisual.Inset"] };
        Host.Of(design, ring);

        var drawn = (FrameworkElement)VisualTreeHelper.GetChild(ring, 0);

        Assert.AreEqual(default, drawn.Margin);
        Assert.AreEqual(design.Dictionary["Kyf.Radius.Control"], ((Border)drawn).CornerRadius);
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
