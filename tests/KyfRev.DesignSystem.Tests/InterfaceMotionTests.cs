using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
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
        Assert.IsTrue(indicator.HasAnimatedProperties, "il glisse");
        Assert.AreEqual(first.ActualWidth, Settled(indicator, TabIndicatorAdorner.StartProperty), 0.5, "jusque sous l'onglet choisi");
        Assert.AreEqual(second.ActualWidth, Settled(indicator, TabIndicatorAdorner.LengthProperty), 0.5, "et en prend la largeur");
        indicator.Measure(new Size(480, 320));
        indicator.Arrange(new Rect(0, 0, 480, 320));
        indicator.UpdateLayout();

        // Plus rien de choisi : pas de repère ; animations réduites : il saute.
        second.IsChecked = false;
        Assert.IsNull(indicator.Target);
        design.ReducedMotion = true;
        first.IsChecked = true;
        Assert.IsFalse(indicator.HasAnimatedProperties);

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

    private sealed record Sheet(string Id);

    private sealed record Row(Sheet Card);

    private static Brush Brushes(DesignResources design, ColorRole role) => (Brush)design.Dictionary[DesignKeys.Brush(role)];

    private static SolidColorBrush Solid(byte a, byte r, byte g, byte b) => new(new TokenColor(a, r, g, b).ToColor());
}
