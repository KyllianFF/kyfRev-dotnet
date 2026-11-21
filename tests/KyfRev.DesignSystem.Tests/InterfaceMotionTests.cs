using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>
/// Les animations d'interface (rangée 18 des maquettes) : les couleurs passent en fondu, les boutons s'enfoncent sous le
/// clic, une coche paraît en grandissant, le rond d'un interrupteur glisse, le repère des onglets glisse de l'un à
/// l'autre, un menu grandit en s'ouvrant. Rien ne bouge quand les animations sont réduites.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class InterfaceMotionTests
{
    private static readonly MethodInfo SetIsPressed = typeof(ButtonBase).GetMethod("SetIsPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [STATestMethod]
    public void A_color_changed_by_a_trigger_fades_into_the_new_one()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var first = new SolidColorBrush(Colors.Transparent);
        var second = (Brush)design.Dictionary[DesignKeys.Brush(ColorRole.Primary)];
        var chrome = new Border { Width = 80, Height = 30, Background = first };
        Transitions.SetSmoothBrushes(chrome, true);
        Host.Of(design, chrome);
        Assert.IsTrue(Transitions.GetSmoothBrushes(chrome));
        Assert.IsFalse(chrome.HasAnimatedProperties, "la première couleur est là d'emblée");

        chrome.Background = second;

        Assert.IsTrue(chrome.HasAnimatedProperties, "la couleur passe en fondu");
        Assert.AreSame(second, chrome.GetAnimationBaseValue(Border.BackgroundProperty), "la couleur voulue reste celle du style");
        chrome.BorderBrush = second;
        Assert.IsTrue(chrome.HasAnimatedProperties);

        // Animations réduites : la couleur change d'un coup.
        design.ReducedMotion = true;
        chrome.Background = first;
        Assert.AreSame(first, chrome.Background);
    }

    [STATestMethod]
    public void A_shape_follows_its_fill_and_stroke_and_other_elements_are_left_alone()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var ring = new System.Windows.Shapes.Ellipse { Width = 18, Height = 18, Fill = Brushes(design, ColorRole.Surface) };
        Transitions.SetSmoothBrushes(ring, true);
        Host.Of(design, ring);
        ring.Fill = Brushes(design, ColorRole.Primary);
        Assert.IsTrue(ring.HasAnimatedProperties);
        ring.Stroke = Brushes(design, ColorRole.Primary);

        // Ni bordure ni forme : rien à suivre. Désactivé : rien non plus.
        var text = new TextBlock();
        Transitions.SetSmoothBrushes(text, true);
        Transitions.SetSmoothBrushes(new Border(), false);
        Assert.IsFalse(text.HasAnimatedProperties);
    }

    [TestMethod]
    public void The_passing_color_is_mixed_between_the_two_and_never_whitens_a_transparent_end()
    {
        var a = Solid(255, 0, 0, 0);
        var b = Solid(255, 200, 100, 50);
        var middle = (SolidColorBrush)BrushTransition.Between(a, b, 0.5)!;
        Assert.AreEqual(100, middle.Color.R);
        Assert.AreEqual(50, middle.Color.G);
        Assert.IsTrue(middle.IsFrozen);

        // Depuis le transparent, seule l'opacité change : la teinte est celle de la couleur visible.
        var clear = Solid(0, 255, 255, 255);
        var fading = (SolidColorBrush)BrushTransition.Between(clear, b, 0.5)!;
        Assert.AreEqual(200, fading.Color.R);
        Assert.AreEqual(128, fading.Color.A);
        var leaving = (SolidColorBrush)BrushTransition.Between(b, clear, 0.5)!;
        Assert.AreEqual(50, leaving.Color.B);

        // Au bout, ou entre deux pinceaux qui ne sont pas unis : l'un ou l'autre.
        Assert.AreSame(b, BrushTransition.Between(a, b, 1));
        var gradient = new LinearGradientBrush();
        Assert.AreSame(gradient, BrushTransition.Between(a, gradient, 0.7));
        Assert.AreSame(a, BrushTransition.Between(a, gradient, 0.2));

        // L'animation elle-même : du pinceau de départ vers la valeur de base.
        var transition = new BrushTransition { From = a, Duration = TimeSpan.FromMilliseconds(120) };
        Assert.AreEqual(typeof(Brush), transition.TargetPropertyType);
        var clock = transition.CreateClock();
        Assert.IsNotNull(transition.GetCurrentValue(a, b, clock));
        Assert.IsInstanceOfType<BrushTransition>(transition.Clone());
        Assert.ThrowsExactly<ArgumentNullException>(() => transition.GetCurrentValue(a, b, null!));
    }

    [STATestMethod]
    public void A_button_sinks_under_the_click_and_comes_back()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var button = new Button { Content = "Exporter…" };
        Host.Of(design, button);
        Assert.AreEqual(0.97, Transitions.GetPressScale(button), "le style du bouton le demande");

        SetIsPressed.Invoke(button, [true]);
        var scale = (ScaleTransform)button.RenderTransform;
        Assert.IsTrue(scale.HasAnimatedProperties, "il descend");
        Assert.AreEqual(new Point(0.5, 0.5), button.RenderTransformOrigin, "autour de son centre");
        SetIsPressed.Invoke(button, [false]);
        Assert.IsTrue(scale.HasAnimatedProperties, "et revient");

        // Animations réduites : il ne bouge pas.
        design.ReducedMotion = true;
        SetIsPressed.Invoke(button, [true]);
        Assert.IsFalse(scale.HasAnimatedProperties);
    }

    [STATestMethod]
    public void A_button_already_moved_by_someone_else_or_without_depth_does_not_sink()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var turned = new Button { RenderTransform = new RotateTransform(10) };
        var flat = new Button();
        var panel = new StackPanel { Children = { turned, flat } };
        Host.Of(design, panel);
        Transitions.SetPressScale(flat, 0);

        SetIsPressed.Invoke(turned, [true]);
        SetIsPressed.Invoke(flat, [true]);

        Assert.IsInstanceOfType<RotateTransform>(turned.RenderTransform, "sa transformation reste la sienne");
        Assert.IsFalse(flat.RenderTransform is ScaleTransform { HasAnimatedProperties: true }, "sans profondeur, rien");
        // Un outil du ruban, plus petit, descend un peu plus.
        Assert.AreEqual(0.94, Transitions.GetPressScale(new Button { Style = (Style)design.Dictionary["Kyf.Button.Tool"] }));
    }

    [STATestMethod]
    public void A_mark_pops_in_and_a_knob_glides_across_its_track()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var mark = new Border { Width = 10, Height = 10, Visibility = Visibility.Collapsed };
        Transitions.SetPopsIn(mark, true);
        Assert.IsTrue(Transitions.GetPopsIn(mark));
        var knob = new Border { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left };
        Transitions.SetGlides(knob, true);
        Assert.IsTrue(Transitions.GetGlides(knob));
        var track = new Grid { Width = 44, Height = 24, Children = { knob } };
        var panel = new StackPanel { Children = { mark, track } };
        var host = Host.Of(design, panel);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        mark.Visibility = Visibility.Visible;
        Host.Layout(host);
        Assert.IsTrue(mark.HasAnimatedProperties, "la coche paraît en grandissant");

        knob.HorizontalAlignment = HorizontalAlignment.Right;
        Host.Layout(host);
        Assert.IsInstanceOfType<TranslateTransform>(knob.RenderTransform);
        Assert.IsTrue(knob.RenderTransform.HasAnimatedProperties, "le rond part de l'ancienne place");

        // Ne plus glisser, ne plus grandir : rien ne reste accroché.
        Transitions.SetGlides(knob, false);
        Transitions.SetPopsIn(mark, false);
        Transitions.SetGlides(new TextBlock(), true);
    }

    [STATestMethod]
    public void The_tab_line_slides_from_the_left_tab_to_the_chosen_one()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var tab = (Style)design.Dictionary["Kyf.Button.Tab"];
        var first = new ToggleButton { Style = tab, Content = "Accueil", IsChecked = true };
        var second = new ToggleButton { Style = tab, Content = "Mise en page" };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } };
        Transitions.SetTabIndicator(tabs, true);
        var host = Host.Of(design, new AdornerDecorator { Child = tabs });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        tabs.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.IsTrue(Transitions.GetTabIndicator(tabs));
        Assert.IsTrue(Transitions.GetHasSharedIndicator(second), "chaque onglet le sait");
        Assert.AreEqual(Visibility.Hidden, Host.Part<Border>(first, "Line").Visibility, "et cache son propre trait");
        var indicator = Transitions.IndicatorOf(tabs) ?? throw new AssertFailedException("pas de repère");
        Assert.AreEqual(0, indicator.Start, 0.5);
        Assert.AreEqual(first.ActualWidth, indicator.Length, 0.5);
        Assert.AreEqual(indicator.Target, Transitions.IndicatedTab(tabs));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.IndicatedTab(null!));

        second.IsChecked = true;
        first.IsChecked = false;
        Assert.IsTrue(indicator.Slid, "il glisse");
        Assert.AreEqual(first.ActualWidth, Settled(indicator, TabIndicatorAdorner.StartProperty), 0.5, "jusque sous l'onglet choisi");
        Assert.AreEqual(second.ActualWidth, Settled(indicator, TabIndicatorAdorner.LengthProperty), 0.5, "et en prend la largeur");
        indicator.Measure(new Size(480, 320));
        indicator.Arrange(new Rect(0, 0, 480, 320));
        indicator.UpdateLayout();

        // Dans l'ordre du ruban, l'onglet quitté est décoché avant que le nouveau soit coché : le repère glisse quand même.
        second.IsChecked = false;
        first.IsChecked = true;
        Assert.IsTrue(indicator.Slid, "il glisse aussi vers la gauche");
        Host.Flush();
        Assert.IsNotNull(indicator.Target);

        // Pendant le glissement, la mise en page change (un onglet plus large) : le repère suit la nouvelle place.
        Assert.IsTrue(indicator.HasAnimatedProperties);
        first.MinWidth = 200;
        Host.Layout(host);
        Assert.AreEqual(200, indicator.Target!.Value.Width, 0.5, "il suit, même en cours de glissement");

        // Plus rien de choisi : le repère s'efface au tour suivant ; animations réduites : il saute.
        first.IsChecked = false;
        Assert.IsNotNull(indicator.Target, "pas tout de suite : un autre onglet peut être coché juste après");
        Host.Flush();
        Assert.IsNull(indicator.Target);
        design.ReducedMotion = true;
        first.IsChecked = true;
        Assert.IsFalse(indicator.Slid);

        // Le panneau n'en veut plus : le repère s'en va.
        Transitions.SetTabIndicator(tabs, false);
        Assert.IsNull(Transitions.IndicatorOf(tabs));
        Assert.IsFalse(Transitions.GetHasSharedIndicator(second));
    }

    [STATestMethod]
    public void In_a_column_of_sections_the_mark_is_on_the_left_and_moves_down()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var tool = (Style)design.Dictionary["Kyf.Button.Tool"];
        var general = new RadioButton { Style = tool, Content = "Général", IsChecked = true, GroupName = "Essai" };
        var reviews = new RadioButton { Style = tool, Content = "Révisions", GroupName = "Essai" };
        var sections = new StackPanel { Children = { new ContentControl { Content = general }, new ContentControl { Content = reviews } } };
        Transitions.SetTabIndicatorSide(sections, Dock.Left);
        Assert.AreEqual(Dock.Left, Transitions.GetTabIndicatorSide(sections));
        var host = Host.Of(design, new AdornerDecorator { Child = sections });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Transitions.SetTabIndicator(sections, true);
        Host.Flush();

        var indicator = Transitions.IndicatorOf(sections) ?? throw new AssertFailedException("pas de repère");
        Assert.AreEqual(general.ActualHeight, indicator.Length, 0.5, "de la hauteur de la section");
        reviews.IsChecked = true;
        Assert.AreEqual(general.ActualHeight, Settled(indicator, TabIndicatorAdorner.StartProperty), 0.5, "il descend sous la suivante");
        Assert.AreSame(reviews, TabIndicatorAdorner.Chosen(sections));
        indicator.Measure(new Size(480, 320));
        indicator.Arrange(new Rect(0, 0, 480, 320));
        indicator.UpdateLayout();
        sections.Visibility = Visibility.Collapsed;
        Host.Layout(host);
        Assert.IsNull(indicator.Target, "un panneau caché n'a pas de repère");
    }

    [STATestMethod]
    public void A_context_menu_grows_from_its_corner_and_a_notification_rises()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        // Un menu contextuel n'a pas de parent : il porte lui-même les ressources du thème.
        var menu = new ContextMenu { Items = { new MenuItem { Header = "Renommer" } }, Resources = design.Dictionary };
        menu.ApplyTemplate();
        Assert.IsTrue(Transitions.GetGrowsOnOpen(menu), "le style du menu le demande");
        // Un menu fait à la volée n'a pas encore son cadre : il grandit au tour suivant, une fois posé.
        Assert.IsFalse(Transitions.GrowFrame(new ContextMenu()));
        Host.Flush();
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
        var frame = (FrameworkElement)VisualTreeHelper.GetChild(menu, 0);
        Assert.IsTrue(frame.HasAnimatedProperties, "il paraît en fondu");
        Assert.IsInstanceOfType<ScaleTransform>(frame.RenderTransform, "en grandissant");
        Assert.AreEqual(new Point(0, 0), frame.RenderTransformOrigin, "depuis son coin");
        Transitions.SetGrowsOnOpen(menu, false);

        var notification = new Notification { Message = "Fiche archivée.", Visibility = Visibility.Collapsed };
        var host = Host.Of(design, notification);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        notification.Visibility = Visibility.Visible;
        Host.Layout(host);
        var card = (FrameworkElement)VisualTreeHelper.GetChild(notification, 0);
        Assert.IsTrue(card.HasAnimatedProperties, "la notification paraît en fondu");
        Assert.IsInstanceOfType<TranslateTransform>(card.RenderTransform, "en montant : son cadre, pas elle");
        Assert.IsFalse(notification.RenderTransform is TranslateTransform, "sa place reste la sienne");
    }

    [STATestMethod]
    public void Content_can_slide_in_from_a_side_and_nothing_moves_outside_a_theme()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var panel = new Border { Width = 100, Height = 40 };
        Host.Of(design, panel);
        Assert.IsTrue(Transitions.Enter(panel, DesignKeys.FastDuration, shift: 8));
        var move = (TranslateTransform)panel.RenderTransform;
        Assert.IsTrue(move.HasAnimatedProperties);

        Assert.IsFalse(Transitions.Grow(new Border(), 0.6, new Point(0.5, 0.5)), "hors de tout thème, rien ne bouge");
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Grow(null!, 0.6, default));
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), ((Duration)design.Dictionary[DesignKeys.PressDuration]).TimeSpan);
        design.ReducedMotion = true;
        Assert.AreEqual(TimeSpan.Zero, ((Duration)design.Dictionary[DesignKeys.PressDuration]).TimeSpan);
    }

    /// <summary>Où va le repère : sa valeur à la fin du glissement en cours.</summary>
    private static double Settled(TabIndicatorAdorner indicator, DependencyProperty property) => (double)indicator.GetAnimationBaseValue(property);

    [STATestMethod]
    public void A_sheet_added_to_a_rebuilt_list_arrives_and_nothing_else_moves()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        static Row Sheet(string id) => new(new Sheet(id));
        var list = new ItemsControl { ItemsSource = new[] { Sheet("f_a"), Sheet("f_b") } };
        Transitions.SetNewcomerKey(list, "Card.Id");
        Assert.AreEqual("Card.Id", Transitions.GetNewcomerKey(list));
        var host = Host.Of(design, list);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // La liste qu'on trouve en arrivant ne s'anime pas ; une fiche nouvelle, dans la liste reconstruite, arrive.
        Assert.AreEqual(0, Transitions.Welcome(list));
        var added = Sheet("f_c");
        list.ItemsSource = new[] { Sheet("f_a"), added, Sheet("f_b") };
        Host.Layout(host);
        Assert.AreEqual(1, Transitions.Welcome(list), "seule la fiche nouvelle arrive");
        Assert.IsTrue(((FrameworkElement)list.ItemContainerGenerator.ContainerFromItem(added)).HasAnimatedProperties);
        Host.Flush();

        // Des fiches déjà vues qui reparaissent (un filtre levé) ne s'animent pas ; une liste toute neuve non plus.
        list.ItemsSource = new[] { Sheet("f_b") };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));
        list.ItemsSource = new[] { Sheet("f_a"), Sheet("f_b"), Sheet("f_c") };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));
        list.ItemsSource = new[] { Sheet("n_1"), Sheet("n_2"), Sheet("n_3"), Sheet("n_4") };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list), "plus de trois : une autre liste, pas un ajout");

        // Une ligne sans clé (un dossier) est ignorée ; animations réduites ou liste cachée : rien ne bouge.
        list.ItemsSource = new object[] { "Dossier", Sheet("n_5") };
        Host.Layout(host);
        design.ReducedMotion = true;
        Assert.AreEqual(0, Transitions.Welcome(list));
        design.ReducedMotion = false;
        list.ItemsSource = new[] { Sheet("n_6") };
        list.Visibility = Visibility.Collapsed;
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));

        Assert.IsNull(Transitions.KeyOf(new Row(null!), "Card.Id"), "un chemin interrompu n'a pas de clé");
        Assert.IsNull(Transitions.KeyOf("texte", "Card.Id"));
        Transitions.SetNewcomerKey(list, string.Empty);
        Assert.AreEqual(0, Transitions.Welcome(list), "la liste n'en veut plus");
        Transitions.SetNewcomerKey(new Border(), "Card.Id");
    }

    [STATestMethod]
    public void A_list_still_loading_is_not_taken_for_new_sheets()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var list = new ItemsControl();
        Transitions.SetNewcomerKey(list, "Card.Id");
        var host = Host.Of(design, list);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };

        // Vide, puis chargée : c'est la liste qu'on trouve en arrivant.
        list.ItemsSource = new[] { new Row(new Sheet("f_a")) };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));
        Host.Flush();
    }

    [STATestMethod]
    public void A_sheet_created_elsewhere_arrives_when_its_list_is_shown_again()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var model = new object();
        static Row Sheet(string id) => new(new Sheet(id));
        ItemsControl View(object[] rows)
        {
            var list = new ItemsControl { DataContext = model, ItemsSource = rows };
            Transitions.SetNewcomerKey(list, "Card.Id");
            return list;
        }

        // La page vue une première fois, puis quittée : sa vue est jetée, son modèle reste.
        var first = View([Sheet("f_a"), Sheet("f_b")]);
        var host = Host.Of(design, first);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(first));

        // De retour, une vue neuve du même modèle : la fiche créée entre-temps arrive.
        var created = Sheet("f_c");
        var again = View([Sheet("f_a"), created, Sheet("f_b")]);
        host.Child = again;
        Host.Layout(host);
        Assert.AreEqual(1, Transitions.Welcome(again), "la fiche créée arrive, les autres non");

        // La liste encore cachée quand elle change : la nouvelle fiche attend qu'elle se montre.
        var waiting = Sheet("f_d");
        again.Visibility = Visibility.Collapsed;
        again.ItemsSource = new[] { Sheet("f_a"), Sheet("f_b"), waiting };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(again));
        again.Visibility = Visibility.Visible;
        Host.Layout(host);
        Assert.AreEqual(1, Transitions.Welcome(again), "elle arrive quand la liste se montre");
        Host.Flush();
    }

    [STATestMethod]
    public void A_drop_down_list_or_a_sub_menu_grows_as_it_opens()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var content = new Border { Width = 120, Height = 80 };
        var popup = new System.Windows.Controls.Primitives.Popup { Child = content, Resources = design.Dictionary };
        Transitions.SetGrowsOnOpen(popup, true);
        Assert.IsTrue(Transitions.GetGrowsOnOpen(popup));

        typeof(System.Windows.Controls.Primitives.Popup).GetMethod("OnOpened", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(popup, [EventArgs.Empty]);

        Assert.IsTrue(content.HasAnimatedProperties, "son contenu paraît en fondu");
        Assert.IsInstanceOfType<ScaleTransform>(content.RenderTransform, "en grandissant");
        Transitions.SetGrowsOnOpen(popup, false);
        Transitions.SetGrowsOnOpen(new Border(), true);
    }

    [STATestMethod]
    public void In_a_menu_column_the_chosen_box_slides_from_one_choice_to_the_next()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var panel = new FrameworkElementFactory(typeof(SlidingSelectionPanel));
        var menu = new ListBox { ItemsSource = new[] { "Aujourd'hui", "Carnet", "Définitions" }, ItemsPanel = new ItemsPanelTemplate(panel) };
        var host = Host.Of(design, menu);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        var column = (SlidingSelectionPanel)VisualTreeHelper.GetParent(menu.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.IsNull(column.Selection, "rien de choisi : pas de cadre");

        // Le premier choix : le cadre paraît à sa place, en fondu ; la ligne ne dessine plus le sien.
        menu.SelectedIndex = 0;
        Host.Layout(host);
        var first = (ListBoxItem)menu.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.AreEqual(0, column.Selection!.Value.Top, 0.5);
        Assert.IsTrue(Transitions.GetHasSlidingSelection(first), "la ligne le sait");
        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)Host.Part<Border>(first, "Row").Background).Color, "et ne dessine plus son cadre");

        // Le suivant : le cadre glisse jusqu'à lui.
        menu.SelectedIndex = 2;
        var third = (ListBoxItem)menu.ItemContainerGenerator.ContainerFromIndex(2);
        Assert.AreEqual(SelectionMove.Slid, column.LastMove, "il glisse");
        // Le cadre part une fois montré ce que le choix change : d'ici là, il reste sous l'ancien.
        Host.Flush();
        Assert.AreEqual(third.TransformToAncestor(column).Transform(default).Y, (double)column.GetAnimationBaseValue(SlidingSelectionPanel.SelectionStartProperty), 0.5, "jusqu'au choix");
        Assert.AreEqual(third.ActualHeight, (double)column.GetAnimationBaseValue(SlidingSelectionPanel.SelectionLengthProperty), 0.5);
        var picture = new RenderTargetBitmap(480, 320, 96, 96, PixelFormats.Pbgra32);
        picture.Render(host);

        // Plus rien de choisi : plus de cadre. Animations réduites : il saute.
        menu.SelectedIndex = -1;
        Host.Layout(host);
        Assert.IsNull(column.Selection);
        design.ReducedMotion = true;
        menu.SelectedIndex = 1;
        Host.Layout(host);
        Assert.IsFalse(column.HasAnimatedProperties);
        Assert.AreEqual(1d, column.SelectionOpacity);
    }

    [STATestMethod]
    public void In_a_column_of_sections_a_checked_button_lets_the_column_draw_its_box()
    {
        var design = new DesignResources(ThemeKind.Dark, reducedMotion: false);
        var tool = (Style)design.Dictionary["Kyf.Button.Tool"];
        var general = new RadioButton { Style = tool, Content = "Général", IsChecked = true, GroupName = "Colonne" };
        var reviews = new RadioButton { Style = tool, Content = "Révisions", GroupName = "Colonne" };
        var column = new SlidingSelectionPanel { Children = { new ContentControl { Content = general }, new ContentControl { Content = reviews } } };
        var host = Host.Of(design, column);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)general.GetAnimationBaseValue(Control.BackgroundProperty)).Color, "la section cochée ne dessine plus son cadre");
        Assert.AreEqual(0, column.Selection!.Value.Top, 0.5);
        reviews.IsChecked = true;
        Assert.AreSame(reviews, SlidingSelectionPanel.Chosen(column));
        // Le cadre part une fois montré ce que le choix change : d'ici là, il reste sous l'ancien.
        Host.Flush();
        Assert.AreEqual(general.ActualHeight, (double)column.GetAnimationBaseValue(SlidingSelectionPanel.SelectionStartProperty), 0.5, "le cadre descend à la suivante");
        column.Visibility = Visibility.Collapsed;
        Host.Layout(host);
        Assert.IsNull(column.Selection, "une colonne cachée n'a pas de cadre");
    }

    [STATestMethod]
    public void At_first_opening_the_box_is_under_the_choice_made_before_any_layout()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        // Comme à l'ouverture de kyfRev : la vue est choisie avant que le menu ait été mis en page.
        var menu = new ListBox { ItemsSource = new[] { "Aujourd'hui", "Carnet", "Définitions" }, ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(SlidingSelectionPanel))), SelectedIndex = 0 };
        var host = Host.Of(design, menu);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        var first = (ListBoxItem)menu.ItemContainerGenerator.ContainerFromIndex(0);
        var column = (SlidingSelectionPanel)VisualTreeHelper.GetParent(first);
        Assert.IsNotNull(column.Selection, "le cadre est là dès la première image");
        Assert.AreEqual(first.ActualHeight, column.SelectionLength, 0.5, "de la hauteur du choix");
        Assert.AreEqual(first.ActualWidth, column.Selection!.Value.Width, 0.5);
    }

    [STATestMethod]
    public void The_box_follows_its_choice_when_the_menu_narrows_or_widens_even_while_it_fades_in()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var menu = new ListBox { ItemsSource = new[] { "Aujourd'hui", "Paramètres" }, ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(SlidingSelectionPanel))), Width = 240 };
        var host = Host.Of(design, menu);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        menu.SelectedIndex = 1;
        Host.Layout(host);
        var chosen = (ListBoxItem)menu.ItemContainerGenerator.ContainerFromIndex(1);
        var column = (SlidingSelectionPanel)VisualTreeHelper.GetParent(chosen);
        Assert.AreEqual(SelectionMove.Faded, column.LastMove, "premier choix : il paraît en fondu");
        Assert.IsTrue(column.HasAnimatedProperties);
        var wide = column.Selection!.Value.Width;

        // Le menu se réduit à la colonne d'icônes pendant que le cadre paraît encore : il se réduit avec lui.
        menu.Width = 80;
        Host.Layout(host);
        Assert.IsLessThan(wide, column.Selection!.Value.Width, "le cadre s'est réduit");
        Assert.AreEqual(chosen.ActualWidth, column.Selection!.Value.Width, 0.5);
        Assert.AreEqual(SelectionMove.Jumped, column.LastMove, "avec la mise en page, sans glisser");

        // Puis s'élargit : le cadre aussi.
        menu.Width = 240;
        Host.Layout(host);
        Assert.AreEqual(wide, column.Selection!.Value.Width, 0.5);

        // Un autre choix : l'ancien est quitté avant que le nouveau soit pris, et le cadre glisse quand même de l'un à l'autre.
        menu.SelectedIndex = 0;
        Assert.AreEqual(SelectionMove.Slid, column.LastMove);
        Host.Flush();
        Assert.IsNotNull(column.Selection);

        // Plus rien de choisi dans cette colonne : le cadre s'efface au tour suivant.
        menu.SelectedIndex = -1;
        Assert.IsNotNull(column.Selection, "pas tout de suite : un autre choix peut arriver juste après");
        Host.Flush();
        Assert.IsNull(column.Selection);
    }

    [STATestMethod]
    public void What_appears_after_a_busy_moment_waits_to_be_on_screen_before_fading_in()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var ribbon = new Border { Width = 200, Height = 40 };
        Host.Of(design, ribbon);

        Assert.IsTrue(Transitions.EnterOnceShown(ribbon, rise: -8));
        Assert.IsTrue(ribbon.HasAnimatedProperties, "tenu invisible en attendant");
        Assert.IsNotInstanceOfType<TranslateTransform>(ribbon.RenderTransform, "pas encore parti");
        Host.Flush();
        Assert.IsInstanceOfType<TranslateTransform>(ribbon.RenderTransform, "l'écran prêt, il descend en fondu");
        Assert.AreEqual(1d, (double)ribbon.GetAnimationBaseValue(UIElement.OpacityProperty), "et retrouve son opacité à la fin");

        // Animations réduites : montré tel quel, tout de suite.
        design.ReducedMotion = true;
        Assert.IsFalse(Transitions.EnterOnceShown(ribbon, rise: -8));
        Assert.AreEqual(1d, ribbon.Opacity);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.EnterOnceShown(null!));
    }

    [STATestMethod]
    public void A_family_switched_off_does_not_move_and_leaves_the_others_alone()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var block = new Border { Width = 100, Height = 40 };
        var read = new ToggleButton { Content = "Lire", IsChecked = true };
        var write = new ToggleButton { Content = "Écrire" };
        var row = new SlidingSelectionPanel { Orientation = Orientation.Horizontal, Children = { read, write } };
        var host = Host.Of(design, new StackPanel { Children = { block, row } });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Assert.AreEqual(MotionKinds.None, design.MotionOff);
        Assert.IsTrue(Transitions.Enter(block), "rien n'est coupé : tout joue");

        // Les changements de vue et les menus coupés : leurs transitions n'ont pas lieu, celle en cours s'arrête.
        design.MotionOff = MotionKinds.Views | MotionKinds.Surfaces;
        Assert.AreEqual(MotionKinds.Views | MotionKinds.Surfaces, design.Dictionary[DesignKeys.MotionOff]);
        Assert.IsFalse(Transitions.Enter(block));
        Assert.IsFalse(block.HasAnimatedProperties);
        Assert.IsFalse(Transitions.EnterOnceShown(block));
        Assert.IsFalse(Transitions.Grow(block, 0.9, new Point(0, 0), kind: MotionKinds.Surfaces));
        Assert.AreEqual(TimeSpan.Zero, Transitions.DurationOf(block), "la page de l'éditeur reçoit une durée nulle");
        Assert.AreEqual(PopupAnimation.None, design.Dictionary[DesignKeys.PopupMotion], "les menus s'ouvrent d'un coup");

        // Les autres familles gardent leurs durées.
        Assert.IsTrue(Transitions.Enter(block, kind: MotionKinds.Lists));
        Assert.IsGreaterThan(TimeSpan.Zero, Transitions.DurationOf(block, kind: MotionKinds.Panels));
        write.IsChecked = true;
        read.IsChecked = false;
        Assert.AreEqual(SelectionMove.Slid, row.LastMove, "la sélection glisse encore");

        // La sélection coupée à son tour : le cadre saute.
        design.MotionOff = MotionKinds.Selection;
        Host.Flush();
        read.IsChecked = true;
        write.IsChecked = false;
        Assert.AreEqual(SelectionMove.Jumped, row.LastMove);
        Assert.AreEqual(PopupAnimation.Fade, design.Dictionary[DesignKeys.PopupMotion]);

        // Les animations réduites coupent tout, sans oublier les familles réglées.
        design.ReducedMotion = true;
        Assert.AreEqual(MotionKinds.Selection, design.MotionOff);
        Assert.AreEqual(MotionKinds.Selection, design.Dictionary[DesignKeys.MotionOff]);
        Assert.IsFalse(Transitions.Enter(block, kind: MotionKinds.Lists));
    }

    [STATestMethod]
    public void A_side_panel_that_changes_width_slides_its_edge_and_what_follows_it()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var panel = new Border { Width = 248, Height = 300 };
        var page = new Border { Width = 200, Height = 300 };
        var curtain = new Border { Visibility = Visibility.Collapsed };
        var canvas = new Canvas { Children = { curtain } };
        var host = Host.Of(design, new Grid { Children = { panel, page, canvas } });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        // Élargi : le panneau se découvre depuis son ancienne largeur, la page glisse avec son bord.
        Assert.IsTrue(Transitions.Resize(panel, curtain, page, from: 77, to: 248));
        Assert.AreEqual(77, ((RectangleGeometry)panel.Clip).Rect.Width, 1, "découpé à l'ancienne largeur au départ");
        Assert.AreEqual(Visibility.Collapsed, curtain.Visibility);
        Assert.IsTrue(((TranslateTransform)page.RenderTransform).HasAnimatedProperties);

        // Réduit : le rideau tient la place quittée, du nouveau bord à l'ancien, et se retire.
        Assert.IsTrue(Transitions.Resize(panel, curtain, page, from: 248, to: 77));
        Assert.IsNull(panel.Clip, "la transition d'avant s'est arrêtée");
        Assert.AreEqual(Visibility.Visible, curtain.Visibility);
        Assert.AreEqual(77, Canvas.GetLeft(curtain));
        Assert.AreEqual(171, curtain.Width);
        Assert.IsTrue(curtain.RenderTransform.HasAnimatedProperties);

        // Masqué, sans rien qui suive (une page à fenêtre native) : seul le rideau bouge.
        Assert.IsTrue(Transitions.Resize(panel, curtain, null, from: 77, to: 0));
        Assert.AreEqual(0, Canvas.GetLeft(curtain));

        // La même largeur, ou les animations réduites : rien ne bouge, et rien ne reste en route.
        Assert.IsFalse(Transitions.Resize(panel, curtain, page, from: 77, to: 77));
        Assert.AreEqual(Visibility.Collapsed, curtain.Visibility);
        design.ReducedMotion = true;
        Assert.IsFalse(Transitions.Resize(panel, curtain, page, from: 0, to: 248));
        Assert.IsNull(panel.Clip);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Resize(null!, curtain, page, 0, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Resize(panel, null!, page, 0, 1));
    }

    private sealed record Sheet(string Id);

    private sealed record Row(Sheet Card);

    [STATestMethod]
    public void In_a_row_of_modes_the_chosen_box_slides_sideways_and_keeps_the_outline_of_a_checked_tool()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var read = new ToggleButton { Content = "Lire", IsChecked = true, Style = (Style)design.Dictionary["Kyf.Button.Tool"] };
        var revise = new ToggleButton { Content = "Réviser", Style = (Style)design.Dictionary["Kyf.Button.Tool"] };
        var write = new ToggleButton { Content = "Écrire", Style = (Style)design.Dictionary["Kyf.Button.Tool"] };
        var row = new SlidingSelectionPanel { Orientation = Orientation.Horizontal, Children = { read, revise, write } };
        var host = Host.Of(design, row);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        // Le cadre est sous le mode choisi, de sa largeur ; le bouton ne dessine plus le sien.
        Assert.AreEqual(0, row.SelectionStart, 0.5);
        Assert.AreEqual(read.ActualWidth, row.SelectionLength, 0.5, "dans une rangée, la longueur du cadre est une largeur");
        Assert.AreEqual(Colors.Transparent, ((SolidColorBrush)read.Background).Color);

        // Un autre mode : l'ancien se décoche après, le cadre glisse de côté jusqu'au nouveau.
        write.IsChecked = true;
        read.IsChecked = false;
        Assert.AreEqual(SelectionMove.Slid, row.LastMove);
        // Le cadre part une fois montré ce que le choix change : d'ici là, il reste sous l'ancien.
        Host.Flush();
        Assert.AreEqual(write.TransformToAncestor(row).Transform(default).X, (double)row.GetAnimationBaseValue(SlidingSelectionPanel.SelectionStartProperty), 0.5);
        Assert.AreEqual(write.ActualWidth, (double)row.GetAnimationBaseValue(SlidingSelectionPanel.SelectionLengthProperty), 0.5);
        Host.Flush();
        Assert.IsNotNull(row.Selection, "le mode quitté n'efface pas le cadre");
        var picture = new RenderTargetBitmap(480, 320, 96, 96, PixelFormats.Pbgra32);
        picture.Render(host);
    }

    [STATestMethod]
    public void A_block_shown_slides_in_from_its_edge_and_slides_back_before_giving_up_its_place()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var pane = new Border { Width = 200, Height = 300, Visibility = Visibility.Collapsed };
        var bar = new Border { Width = 300, Height = 60 };
        Transitions.SetShownFrom(pane, Dock.Left);
        Assert.AreEqual(Dock.Bottom, Transitions.GetShownFrom(bar), "sans indication, un bloc vient du bas");

        // Avant d'être à l'écran : montré ou caché d'un coup.
        Transitions.SetShown(bar, false);
        Assert.AreEqual(Visibility.Collapsed, bar.Visibility);
        Transitions.SetShown(bar, true);
        Assert.AreEqual(Visibility.Visible, bar.Visibility);
        Assert.IsTrue(Transitions.GetShown(bar));
        Transitions.SetShown(bar, null);
        Assert.AreEqual(Visibility.Visible, bar.Visibility, "sans valeur, rien n'est décidé");

        var host = Host.Of(design, new DockPanel { LastChildFill = false, Children = { pane, bar } });
        DockPanel.SetDock(bar, Dock.Bottom);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Montré : il prend sa place aussitôt, invisible, puis glisse depuis son bord, découpé à sa place.
        Transitions.SetShown(pane, true);
        Assert.AreEqual(Visibility.Visible, pane.Visibility);
        Assert.AreEqual(0, Transitions.SlidesOf(pane), "il attend sa mise en page, et que l'application soit libre");
        Assert.IsTrue(pane.HasAnimatedProperties, "invisible d'ici là");
        Host.Layout(host);
        Wait(() => Transitions.SlidesOf(pane) == 1 && pane.Clip is null);
        Assert.AreEqual(1, Transitions.SlidesOf(pane), "puis glisse");
        Assert.IsNull(pane.Clip, "arrivé, il est entier");

        // Caché : il repart vers son bord, et ne cède sa place qu'une fois parti.
        Transitions.SetShown(pane, false);
        Assert.AreEqual(Visibility.Visible, pane.Visibility, "il garde sa place le temps de partir");
        Wait(() => pane.Visibility == Visibility.Collapsed);
        Assert.AreEqual(2, Transitions.SlidesOf(pane));
        Assert.AreEqual(Visibility.Collapsed, pane.Visibility, "parti, il la cède");
        Assert.IsNull(pane.Clip, "et rien ne reste de la transition");

        // Une barre du bas descend pour se cacher ; remontrée avant d'être partie, elle revient, et le départ oublié ne la cache pas.
        Transitions.SetShown(bar, false);
        Transitions.SetShown(bar, true);
        Wait(() => false, 400);
        Assert.AreEqual(Visibility.Visible, bar.Visibility);
        Assert.IsNull(bar.Clip);
        Assert.AreEqual(0, Transitions.SlidesOf(bar), "le départ annulé n'a pas eu lieu");
        Transitions.SetShown(bar, false);
        Wait(() => Transitions.SlidesOf(bar) == 1);
        Transitions.SetShown(bar, true);
        Wait(() => Transitions.SlidesOf(bar) == 2 && bar.Clip is null);
        Assert.AreEqual(Visibility.Visible, bar.Visibility, "rappelée en route, elle revient");
        Assert.IsNull(bar.Clip);

        // Déjà montré : rien à faire. Déjà caché : rien à faire glisser.
        Transitions.SetShown(bar, null);
        Transitions.SetShown(bar, true);
        Assert.IsFalse(Transitions.IsSliding(bar));
        Transitions.SetShown(pane, null);
        Transitions.SetShown(pane, false);
        Assert.IsFalse(Transitions.IsSliding(pane));

        // Une autre fiche dans la même vue : ce que le bloc montre alors est son point de départ, rien ne glisse. Ensuite, si.
        var before = Transitions.SlidesOf(bar);
        host.DataContext = "une autre fiche";
        Transitions.SetShown(bar, false);
        Assert.AreEqual(Visibility.Collapsed, bar.Visibility, "d'un coup");
        Assert.AreEqual(before, Transitions.SlidesOf(bar));
        Host.Layout(host);
        Host.Flush();
        Transitions.SetShown(bar, true);
        Wait(() => Transitions.SlidesOf(bar) == before + 1 && bar.Clip is null);
        Assert.AreEqual(before + 1, Transitions.SlidesOf(bar), "le même contexte : le bloc glisse de nouveau");

        // Un bouton dont le cadre glisse ne s'enfonce pas : c'est le cadre qui répond au clic.
        var plain = new ToggleButton { Style = (Style)design.Dictionary["Kyf.Button.Tool"] };
        var sliding = new ToggleButton { Style = (Style)design.Dictionary["Kyf.Button.Tool"] };
        Transitions.SetHasSlidingSelection(sliding, true);
        Assert.IsGreaterThan(0d, Transitions.GetPressScale(plain));
        Assert.AreEqual(0d, Transitions.GetPressScale(sliding));

        // Animations réduites : d'un coup, dans les deux sens.
        design.ReducedMotion = true;
        Assert.AreEqual(TimeSpan.Zero, Transitions.DurationOf(pane));
        Transitions.SetShown(pane, true);
        Assert.AreEqual(Visibility.Visible, pane.Visibility);
        Assert.IsFalse(Transitions.IsSliding(pane));
        Transitions.SetShown(pane, false);
        Assert.AreEqual(Visibility.Collapsed, pane.Visibility);
        design.ReducedMotion = false;
        Assert.IsGreaterThan(TimeSpan.Zero, Transitions.DurationOf(pane), "la durée à donner à ce qui s'anime hors de WPF");
        Transitions.SetShown(new TextElementProbe(), true);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetShown(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetShown(null!, true));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetShownFrom(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetShownFrom(null!, Dock.Top));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.DurationOf(null!));
    }

    [STATestMethod]
    public void What_layout_pushes_follows_from_where_it_was_and_can_be_shifted_for_a_moment()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var body = new Border { Width = 200, Height = 100 };
        var host = Host.Of(design, body);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Poussé de 120 px vers le bas par un bloc paru au-dessus : il reste où il était, puis rejoint sa place.
        var arrived = 0;
        Assert.IsTrue(Transitions.FollowOnceShown(body, -120, done: () => arrived++));
        var held = (TranslateTransform)body.RenderTransform;
        Assert.AreEqual(-120, held.Y, "il est encore là où il était");
        Assert.AreEqual(0, arrived);
        Wait(() => arrived == 1);
        Assert.AreEqual(1, arrived, "arrivé, il le dit, une fois");
        Assert.AreEqual(Transform.Identity, body.RenderTransform, "et ne garde rien du trajet");

        // Un second trajet demandé avant que le premier ne parte prend sa suite : le premier s'efface, et le dit.
        var first = 0;
        var second = 0;
        Transitions.FollowOnceShown(body, 60, done: () => first++);
        Transitions.FollowOnceShown(body, 40, done: () => second++);
        Wait(() => first == 1 && second == 1);
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(Transform.Identity, body.RenderTransform);

        // Décalé un instant : il glisse jusque-là et y reste, jusqu'à ce qu'on le remette à sa place.
        var shifted = 0;
        Assert.IsTrue(Transitions.ShiftTo(body, -40, done: () => shifted++));
        Wait(() => shifted == 1);
        Assert.AreEqual(1, shifted);
        Assert.AreEqual(-40, ((TranslateTransform)body.RenderTransform).Y, 0.5, "il y reste");
        Transitions.Rest(body);
        Assert.AreEqual(Transform.Identity, body.RenderTransform);
        Transitions.Rest(body);

        // Sans distance, ou les animations coupées : rien ne bouge, et la suite est prévenue tout de suite.
        var told = 0;
        Assert.IsFalse(Transitions.FollowOnceShown(body, 0, done: () => told++));
        Assert.IsFalse(Transitions.ShiftTo(body, 0, done: () => told++));
        design.MotionOff = MotionKinds.Views;
        Assert.IsFalse(Transitions.FollowOnceShown(body, -120, done: () => told++));
        Assert.IsFalse(Transitions.ShiftTo(body, -40, done: () => told++));
        Assert.AreEqual(4, told);
        Assert.AreEqual(Transform.Identity, body.RenderTransform);
        Transitions.FollowOnceShown(body, -10);
        Transitions.ShiftTo(body, -10);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.FollowOnceShown(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.ShiftTo(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Rest(null!));
    }

    [STATestMethod]
    public void What_leaves_fades_out_before_giving_up_its_place()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var block = new Border { Width = 200, Height = 100 };
        var dialog = new Dialog { Title = "Supprimer ?", Content = "Elle part à la corbeille." };
        var notice = new Notification { Message = "Enregistré.", Severity = NotificationSeverity.Success };
        var ribbon = new Border { Width = 300, Height = 60 };
        Transitions.SetShownFrom(ribbon, Dock.Top);
        Transitions.SetShownTravel(ribbon, 8);
        Assert.AreEqual(8, Transitions.GetShownTravel(ribbon));
        var host = Host.Of(design, new Grid { Children = { new StackPanel { Children = { ribbon, block, notice } }, dialog } });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Un élément qui part s'efface en accélérant, et ne prévient qu'une fois parti ; il redevient alors entier.
        var gone = 0;
        Assert.IsTrue(Transitions.Exit(block, () => gone++, drop: 4, shift: -10));
        Assert.AreEqual(0, gone, "il est encore là");
        Assert.IsTrue(block.HasAnimatedProperties);
        Assert.IsInstanceOfType<TranslateTransform>(block.RenderTransform);
        Wait(() => gone == 1);
        Assert.AreEqual(1, gone);
        Assert.IsFalse(block.HasAnimatedProperties);
        Assert.AreEqual(Transform.Identity, block.RenderTransform);

        // Un dialogue fermé s'efface, avec son voile, sans plus rien recevoir ; puis il n'est plus là.
        dialog.IsOpen = true;
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, dialog.Visibility);
        dialog.IsOpen = false;
        Assert.IsTrue(dialog.IsLeaving);
        Assert.AreEqual(Visibility.Visible, dialog.Visibility, "il s'efface encore");
        Assert.IsFalse(dialog.IsHitTestVisible);
        Wait(() => !dialog.IsLeaving);
        Assert.IsFalse(dialog.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, dialog.Visibility);
        // Rouvert pendant qu'il s'efface : il est là, entier.
        dialog.IsOpen = true;
        dialog.IsOpen = false;
        dialog.IsOpen = true;
        Assert.IsFalse(dialog.IsLeaving);
        Assert.AreEqual(Visibility.Visible, dialog.Visibility);
        Assert.IsTrue(dialog.IsHitTestVisible);
        dialog.IsOpen = false;
        Wait(() => !dialog.IsLeaving);

        // Un avis fermé, ou lu jusqu'au bout, s'efface de même avant de rendre sa place.
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, notice.Visibility);
        notice.Close();
        Assert.IsTrue(notice.IsLeaving);
        Assert.AreEqual(Visibility.Visible, notice.Visibility);
        Wait(() => !notice.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
        notice.IsOpen = true;
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, notice.Visibility);
        notice.Expire();
        Assert.IsTrue(notice.IsLeaving);
        Wait(() => !notice.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
        notice.Expire();
        Assert.IsFalse(notice.IsLeaving, "déjà partie, elle ne repart pas");

        // Une page vide souvent son message au moment de fermer : l'avis part avec ce qu'il disait, pas vide.
        string? leftWith = null;
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Notification.IsLeavingProperty, typeof(Notification)).AddValueChanged(notice, (_, _) =>
        {
            if (!notice.IsLeaving)
            {
                leftWith = notice.ShownMessage;
            }
        });
        notice.Message = "Déplacé.";
        Host.Layout(host);
        Host.Flush();
        Assert.AreEqual(Visibility.Visible, notice.Visibility, "un nouveau message la fait reparaître");
        Assert.AreEqual("Déplacé.", notice.ShownMessage);
        notice.Message = string.Empty;
        notice.IsOpen = false;
        Wait(() => !notice.IsLeaving);
        Host.Flush();
        Assert.AreEqual("Déplacé.", leftWith, "elle a gardé son texte en s'effaçant");
        Assert.AreEqual(string.Empty, notice.ShownMessage, "partie, elle suit de nouveau son message");
        // Vidé sans être fermé : le texte suit.
        notice.Message = "Copié.";
        notice.IsOpen = true;
        Host.Layout(host);
        Host.Flush();
        notice.Message = string.Empty;
        Host.Flush();
        Assert.AreEqual(string.Empty, notice.ShownMessage);

        // Un nouveau message pendant qu'elle s'efface : elle revient entière, et son temps repart.
        notice.Message = "Collé.";
        Host.Layout(host);
        Host.Flush();
        notice.Expire();
        Assert.IsTrue(notice.IsLeaving);
        notice.Message = "Archivé.";
        Assert.IsFalse(notice.IsLeaving);
        Assert.IsFalse(notice.IsExpired);
        Assert.AreEqual("Archivé.", notice.ShownMessage);
        Assert.AreEqual(Visibility.Visible, notice.Visibility);
        Assert.IsFalse(notice.HasAnimatedProperties, "son fondu de départ est arrêté");
        // Fermée pendant qu'elle s'efface d'elle-même : elle finit de partir, et se rouvre comme neuve.
        notice.Expire();
        notice.IsOpen = false;
        notice.Message = string.Empty;
        Assert.IsTrue(notice.IsLeaving);
        Wait(() => !notice.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
        Assert.AreEqual("Archivé.", leftWith);
        notice.Message = "Rétabli.";
        notice.IsOpen = true;
        Host.Layout(host);
        Assert.IsFalse(notice.IsExpired);
        Assert.AreEqual(Visibility.Visible, notice.Visibility);

        // Un bloc qui paraît presque sur place (le ruban) s'en va de même, vite, sans glisser de toute sa hauteur.
        Transitions.SetShown(ribbon, false);
        Wait(() => ribbon.Visibility == Visibility.Collapsed);
        Assert.AreEqual(Visibility.Collapsed, ribbon.Visibility);
        Assert.AreEqual(1, Transitions.SlidesOf(ribbon));
        Transitions.SetShown(ribbon, true);
        Host.Layout(host);
        Wait(() => Transitions.SlidesOf(ribbon) == 2 && ribbon.Clip is null);
        Assert.AreEqual(Visibility.Visible, ribbon.Visibility);

        // Animations coupées : tout part d'un coup, et la suite est prévenue tout de suite.
        design.ReducedMotion = true;
        Assert.IsFalse(Transitions.Exit(block, () => gone++));
        Assert.AreEqual(2, gone);
        dialog.IsOpen = true;
        dialog.IsOpen = false;
        Assert.IsFalse(dialog.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, dialog.Visibility);
        notice.Close();
        Assert.IsFalse(notice.IsLeaving);
        Assert.AreEqual(Visibility.Collapsed, notice.Visibility);
        // Un départ arrêté ne prévient plus personne : l'élément est là, entier.
        design.ReducedMotion = false;
        var stopped = 0;
        Assert.IsTrue(Transitions.Exit(block, () => stopped++, drop: 4));
        Transitions.StopExit(block);
        Assert.AreEqual(Transform.Identity, block.RenderTransform);
        Wait(() => false, 300);
        Assert.AreEqual(0, stopped);
        Assert.AreEqual(1.0, block.Opacity);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.StopExit(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Exit(null!, () => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.Exit(block, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetShownTravel(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetShownTravel(null!, 1));
    }

    [STATestMethod]
    public void A_block_can_come_from_any_edge()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        foreach (var edge in new[] { Dock.Left, Dock.Right, Dock.Top, Dock.Bottom })
        {
            var block = new Border { Width = 120, Height = 80, Visibility = Visibility.Collapsed };
            Transitions.SetShownFrom(block, edge);
            var host = Host.Of(design, block);
            using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
            Host.Layout(host);
            Host.Flush();
            Transitions.SetShown(block, true);
            Host.Layout(host);
            Wait(() => Transitions.SlidesOf(block) == 1 && block.Clip is null);
            Assert.AreEqual(1, Transitions.SlidesOf(block), $"depuis {edge}");
            Assert.IsNull(block.Clip, "arrivé, il est entier");
            Assert.AreEqual(Visibility.Visible, block.Visibility);
        }
    }

    /// <summary>Laisse tourner les animations jusqu'à ce que la condition soit vraie, deux secondes au plus par défaut.</summary>
    private static void Wait(Func<bool> done, int milliseconds = 2000)
    {
        var limit = DateTime.UtcNow.AddMilliseconds(milliseconds);
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(15),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => frame.Continue = !done() && DateTime.UtcNow < limit,
            System.Windows.Threading.Dispatcher.CurrentDispatcher);
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    /// <summary>Un objet qui n'est pas un élément d'interface : la propriété n'y fait rien.</summary>
    private sealed class TextElementProbe : DependencyObject;

    private static Brush Brushes(DesignResources design, ColorRole role) => (Brush)design.Dictionary[DesignKeys.Brush(role)];

    private static SolidColorBrush Solid(byte a, byte r, byte g, byte b) => new(new TokenColor(a, r, g, b).ToColor());
}
