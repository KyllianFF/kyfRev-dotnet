using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>
/// La fluidité (rangée 20 des maquettes) : une liste qui paraît en cascade, une ligne retirée qui s'efface pendant que les
/// suivantes remontent, un défilement accompagné, un squelette qui s'efface sous le contenu, un texte qui glisse avec le
/// volet d'à côté. Rien ne bouge quand les animations sont réduites.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class FluidityTests
{
    private sealed record Sheet(string Id);

    private sealed record Row(Sheet Card);

    private static Row Line(string id) => new(new Sheet(id));

    private static Row[] Lines(params string[] ids) => [.. ids.Select(Line)];

    private static FrameworkElement RowOf(ItemsControl list, int index) => (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index);

    private static void Wait(Func<bool> done, int milliseconds = 3000)
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

    [STATestMethod]
    public void A_list_seen_for_the_first_time_arrives_in_a_short_cascade()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var model = new object();
        var list = new ItemsControl { DataContext = model, ItemsSource = Lines("a", "b", "c", "d", "e", "f", "g", "h") };
        Transitions.SetNewcomerKey(list, "Card.Id");
        var host = Host.Of(design, list);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);

        // La liste qu'on trouve en arrivant : aucune ligne n'est « nouvelle », mais ses six premières arrivent l'une après l'autre.
        Assert.AreEqual(0, Transitions.Welcome(list));
        for (var index = 0; index < Transitions.CascadeRows; index++)
        {
            Assert.IsTrue(RowOf(list, index).HasAnimatedProperties, $"la ligne {index + 1} arrive");
            Assert.IsInstanceOfType<TranslateTransform>(RowOf(list, index).RenderTransform, "en montant un peu");
        }
        Assert.IsFalse(RowOf(list, Transitions.CascadeRows).HasAnimatedProperties, "les suivantes sont là d'emblée");
        var last = RowOf(list, Transitions.CascadeRows - 1);
        Wait(() => last.Opacity < 0.5);
        Assert.IsLessThan(0.5, last.Opacity, "la dernière attend son tour, invisible");
        Wait(() => last.Opacity >= 1);
        Assert.AreEqual(1.0, last.Opacity, "puis elle arrive");
        Assert.AreEqual(1.0, RowOf(list, 0).Opacity);
        Assert.AreEqual(0, ((TranslateTransform)last.RenderTransform).Y, "à sa place");

        // La même vue, une autre liste : la cascade est passée.
        list.ItemsSource = Lines("a", "b", "c", "d", "e", "f", "g", "h");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));
        Assert.IsFalse(RowOf(list, 0).HasAnimatedProperties);

        // Une vue neuve du même modèle (on revient sur la page) : la cascade de nouveau ; sauf si une fiche créée entre-temps
        // y arrive, qui est alors seule à bouger.
        var again = new ItemsControl { DataContext = model, ItemsSource = Lines("a", "b", "c") };
        Transitions.SetNewcomerKey(again, "Card.Id");
        host.Child = again;
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(again));
        Assert.IsTrue(RowOf(again, 2).HasAnimatedProperties);
        var withNew = new ItemsControl { DataContext = model, ItemsSource = Lines("a", "nouvelle", "b") };
        Transitions.SetNewcomerKey(withNew, "Card.Id");
        host.Child = withNew;
        Host.Layout(host);
        Assert.AreEqual(1, Transitions.Welcome(withNew));
        Assert.IsFalse(RowOf(withNew, 0).HasAnimatedProperties, "la fiche créée arrive seule");
        Assert.IsTrue(RowOf(withNew, 1).HasAnimatedProperties);

        // Cachée, ou animations réduites : pas de cascade.
        var hidden = new ItemsControl { ItemsSource = Lines("a", "b"), Visibility = Visibility.Collapsed };
        Transitions.SetNewcomerKey(hidden, "Card.Id");
        host.Child = hidden;
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Cascade(hidden));
        design.ReducedMotion = true;
        host.Child = list;
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Cascade(list));
        Host.Flush();
    }

    [STATestMethod]
    public void A_row_taken_away_fades_where_it_was_and_those_below_glide_up()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var list = new ItemsControl { ItemsSource = Lines("a", "b", "c", "d") };
        Transitions.SetNewcomerKey(list, "Card.Id");
        var host = Host.Of(design, new AdornerDecorator { Child = list });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list));
        var height = RowOf(list, 0).ActualHeight;
        Assert.IsGreaterThan(0, height);
        var layer = AdornerLayer.GetAdornerLayer(list)!;

        // « b » retirée, la liste reconstruite : son image s'efface à sa place, « c » et « d » repartent de là où elles étaient.
        list.ItemsSource = Lines("a", "c", "d");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Welcome(list), "aucune ligne nouvelle");
        var ghosts = layer.GetAdorners(list);
        Assert.IsNotNull(ghosts, "l'image de la ligne retirée");
        Assert.HasCount(1, ghosts);
        Assert.IsInstanceOfType<Transitions.LeaverAdorner>(ghosts[0]);
        Assert.IsFalse(ghosts[0].IsHitTestVisible);
        Assert.AreEqual(height, ((TranslateTransform)RowOf(list, 1).RenderTransform).Y, 0.5, "« c » repart d'une ligne plus bas");
        Assert.AreEqual(height, ((TranslateTransform)RowOf(list, 2).RenderTransform).Y, 0.5);
        Assert.IsFalse(RowOf(list, 0).RenderTransform is TranslateTransform { Y: > 7 }, "« a », au-dessus, ne repart pas d'une ligne plus bas");
        Wait(() => layer.GetAdorners(list) is null && RowOf(list, 1).RenderTransform == Transform.Identity && RowOf(list, 2).RenderTransform == Transform.Identity);
        Assert.IsNull(layer.GetAdorners(list), "effacée, l'image est retirée");
        Assert.AreEqual(Transform.Identity, RowOf(list, 1).RenderTransform, "arrivées, les lignes sont à leur place");

        // La dernière ligne retirée : rien à remonter, son image s'efface.
        list.ItemsSource = Lines("a", "c");
        Host.Layout(host);
        Transitions.Welcome(list);
        Assert.IsNotNull(layer.GetAdorners(list));
        Wait(() => layer.GetAdorners(list) is null);

        // Plus de trois lignes en moins, ou une autre liste : ce n'est pas un retrait, rien ne s'anime.
        list.ItemsSource = Lines("a", "c", "d", "e", "f", "g");
        Host.Layout(host);
        Transitions.Welcome(list);
        Host.Flush();
        list.ItemsSource = Lines("a", "g");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Portray(list));
        Assert.AreEqual(0, Transitions.Close(list));
        Assert.AreEqual(2, Transitions.Place(list));
        list.ItemsSource = Lines("a", "x");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Close(list), "autant de lignes, mais pas les mêmes");
        Assert.AreEqual(2, Transitions.Place(list));

        // La liste n'est plus où on l'a vue (ses lignes ont grandi) : remonter les suivantes ne ferait que la secouer.
        list.ItemsSource = Lines("a", "x", "y", "z");
        Host.Layout(host);
        Assert.AreEqual(4, Transitions.Place(list));
        list.FontSize *= 3;
        list.ItemsSource = Lines("a", "y", "z");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Close(list));
        Assert.AreEqual(Transform.Identity, RowOf(list, 1).RenderTransform);
        // Une fois le calme revenu, elle est regardée de nouveau (elle a changé de taille) : le retrait suivant s'anime.
        Host.Flush();
        list.ItemsSource = Lines("a", "z");
        Host.Layout(host);
        Assert.AreEqual(1, Transitions.Close(list));
        Wait(() => RowOf(list, 1).RenderTransform == Transform.Identity);

        // Animations réduites, liste cachée ou jamais regardée : rien.
        Transitions.Place(list);
        design.ReducedMotion = true;
        list.ItemsSource = Lines("a");
        Host.Layout(host);
        Assert.AreEqual(0, Transitions.Portray(list));
        Assert.AreEqual(0, Transitions.Close(list));
        design.ReducedMotion = false;
        Assert.AreEqual(0, Transitions.Close(new ItemsControl()));
        Assert.AreEqual(0, Transitions.Portray(new ItemsControl()));
        Transitions.SetNewcomerKey(list, string.Empty);
        Host.Flush();
    }

    [STATestMethod]
    public void Going_to_a_place_the_view_glides_there()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var scroller = new ScrollViewer { Content = new Border { Height = 3000 } };
        var host = Host.Of(design, scroller);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();
        var view = scroller.ViewportHeight;
        Assert.IsGreaterThan(0, view);

        // Tout près : la vue y glisse, et prévient une fois arrivée.
        var arrived = 0;
        Assert.IsTrue(Transitions.ScrollTo(scroller, 200, () => arrived++));
        Assert.AreEqual(0, arrived, "elle est en route");
        Wait(() => arrived == 1);
        Assert.AreEqual(1, arrived);
        Assert.AreEqual(200, scroller.VerticalOffset, 0.5);

        // De loin : elle saute d'abord tout près, puis glisse ; on ne voit pas défiler toute la fiche.
        Assert.IsTrue(Transitions.ScrollTo(scroller, 2400, () => arrived++));
        Wait(() => scroller.VerticalOffset > 201 || arrived == 2);
        Assert.IsGreaterThanOrEqualTo(2400 - (view * Transitions.MostScrolledViews) - 1, scroller.VerticalOffset);
        Wait(() => arrived == 2);
        Assert.AreEqual(2400, scroller.VerticalOffset, 0.5);

        // Au-delà de la fin : jusqu'à la fin. Déjà sur place : rien à faire, la suite est prévenue tout de suite.
        Assert.IsTrue(Transitions.ScrollTo(scroller, 9000, () => arrived++));
        Wait(() => arrived == 3);
        Assert.AreEqual(scroller.ScrollableHeight, scroller.VerticalOffset, 0.5);
        Assert.IsFalse(Transitions.ScrollTo(scroller, scroller.ScrollableHeight, () => arrived++));
        Assert.AreEqual(4, arrived);

        // La molette ou la barre reprennent la main : le trajet s'arrête là où il en est.
        var end = scroller.VerticalOffset;
        Assert.IsTrue(Transitions.ScrollTo(scroller, 0, () => arrived++));
        Transitions.StopScroll(scroller);
        Wait(() => false, 300);
        Assert.AreEqual(4, arrived, "arrêté, il n'arrive pas");
        Assert.IsGreaterThan(0, scroller.VerticalOffset);
        Assert.IsLessThanOrEqualTo(end, scroller.VerticalOffset);

        // Animations réduites : d'un coup.
        design.ReducedMotion = true;
        Assert.IsFalse(Transitions.ScrollTo(scroller, 100, () => arrived++));
        Host.Layout(host);
        Assert.AreEqual(5, arrived);
        Assert.AreEqual(100, scroller.VerticalOffset, 0.5);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.ScrollTo(null!, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.StopScroll(null!));
    }

    [STATestMethod]
    public void A_skeleton_gives_its_place_and_fades_under_the_content_that_appears()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var header = new Border { Height = 20 };
        var skeleton = new Border { Height = 100, Margin = new Thickness(0, 12, 0, 0) };
        var content = new Border { Height = 50 };
        var absent = new Border { Height = 50, Visibility = Visibility.Collapsed };
        var footer = new Border { Height = 10 };
        var panel = new StackPanel { Children = { header, skeleton, content, absent, footer } };
        var host = Host.Of(design, panel);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();
        Assert.IsTrue(Transitions.GetStandsIn(skeleton), "il tient la place tant qu'on ne dit rien");
        Assert.AreEqual(132, content.TranslatePoint(default, panel).Y, 0.5);
        Assert.AreEqual(0, Transitions.Reveal(skeleton), "tant qu'il tient la place, rien ne paraît");

        // Le contenu est là : le squelette rend sa place tout de suite, et s'efface encore sous lui, sans plus rien recevoir.
        Transitions.SetStandsIn(skeleton, false);
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, skeleton.Visibility, "il s'efface encore");
        Assert.IsTrue(skeleton.HasAnimatedProperties);
        Assert.IsFalse(skeleton.IsHitTestVisible);
        Assert.AreEqual(20, content.TranslatePoint(default, panel).Y, 0.5, "le contenu a déjà sa place");
        // Ce qui le suit, et se voit, paraît en fondu ; pas ce qui le précède.
        Assert.AreEqual(2, Transitions.Reveal(skeleton));
        Assert.IsTrue(content.HasAnimatedProperties);
        Assert.IsTrue(footer.HasAnimatedProperties);
        Assert.IsFalse(header.HasAnimatedProperties);
        Assert.IsFalse(absent.HasAnimatedProperties);
        Wait(() => skeleton.Visibility == Visibility.Collapsed);
        Assert.AreEqual(Visibility.Collapsed, skeleton.Visibility);
        Assert.AreEqual(new Thickness(0, 12, 0, 0), skeleton.Margin, "parti, il retrouve sa marge");
        Assert.IsTrue(skeleton.IsHitTestVisible);
        Assert.IsFalse(skeleton.HasAnimatedProperties);

        // On relit : il reprend sa place ; et s'il la reprend pendant qu'il s'effaçait, il est là, entier.
        Transitions.SetStandsIn(skeleton, true);
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, skeleton.Visibility);
        Assert.AreEqual(132, content.TranslatePoint(default, panel).Y, 0.5);
        Transitions.SetStandsIn(skeleton, false);
        Transitions.SetStandsIn(skeleton, true);
        Host.Layout(host);
        Assert.AreEqual(Visibility.Visible, skeleton.Visibility);
        Assert.AreEqual(new Thickness(0, 12, 0, 0), skeleton.Margin);
        Assert.IsFalse(skeleton.HasAnimatedProperties);
        Assert.IsTrue(skeleton.IsHitTestVisible);
        Wait(() => false, 250);
        Assert.AreEqual(Visibility.Visible, skeleton.Visibility, "le départ arrêté ne le retire pas");

        // Animations réduites, ou pas encore à l'écran : d'un coup.
        design.ReducedMotion = true;
        Transitions.SetStandsIn(skeleton, false);
        Assert.AreEqual(Visibility.Collapsed, skeleton.Visibility);
        Assert.AreEqual(0, Transitions.Reveal(skeleton));
        design.ReducedMotion = false;
        var loose = new Border();
        Transitions.SetStandsIn(loose, false);
        Assert.AreEqual(Visibility.Collapsed, loose.Visibility);
        Assert.AreEqual(0, Transitions.Reveal(loose), "hors d'un panneau, rien à faire paraître");
        Transitions.SetStandsIn(new Run(), false);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetStandsIn(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetStandsIn(null!, true));
        Host.Flush();
    }

    [STATestMethod]
    public void Beside_a_pane_that_comes_or_goes_the_text_glides_with_it()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var text = new Border { Width = 200, Height = 100 };
        var host = Host.Of(design, text);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Un volet a paru : la mise en page a déjà déplacé le texte, qui repart de là où il était et rejoint sa place.
        Assert.IsTrue(Transitions.FollowAsideOnceShown(text, -60));
        Assert.AreEqual(-60, ((TranslateTransform)text.RenderTransform).X, "il n'a pas encore bougé à l'écran");
        Wait(() => text.RenderTransform == Transform.Identity);
        Assert.AreEqual(Transform.Identity, text.RenderTransform, "arrivé, il n'est plus décalé");
        Assert.IsFalse(Transitions.FollowAsideOnceShown(text, 0.2), "un rien : pas de trajet");

        // Un volet s'en va : le texte glisse d'avance vers la place qu'il lui rendra, et y reste jusqu'à ce qu'il l'ait.
        Assert.IsTrue(Transitions.ShiftAsideOnceShown(text, 40));
        var move = (TranslateTransform)text.RenderTransform;
        Wait(() => Math.Abs(move.X - 40) < 0.01);
        Assert.AreEqual(40, move.X, 0.01);
        Wait(() => false, 100);
        Assert.AreSame(move, text.RenderTransform, "il y reste");
        Transitions.Rest(text);
        Assert.AreEqual(Transform.Identity, text.RenderTransform);
        Assert.IsFalse(Transitions.ShiftAsideOnceShown(text, 0));

        // Un trajet remplacé avant d'avoir commencé ne part pas.
        Assert.IsTrue(Transitions.ShiftAsideOnceShown(text, 40));
        var replaced = (TranslateTransform)text.RenderTransform;
        Assert.IsTrue(Transitions.FollowAsideOnceShown(text, 30));
        Assert.IsTrue(Transitions.ShiftAsideOnceShown(text, -20));
        Wait(() => text.RenderTransform is TranslateTransform { X: < -19.99 });
        Assert.AreEqual(0, replaced.X, "le premier n'est jamais parti");
        Transitions.Rest(text);

        // Une arrivée qui attend son tour : tenue invisible, puis le même trajet.
        Assert.IsTrue(Transitions.Enter(text, DesignKeys.FastDuration, rise: 6, delay: TimeSpan.FromMilliseconds(60)));
        Wait(() => text.Opacity < 0.5);
        Assert.IsLessThan(0.5, text.Opacity, "elle attend son tour, invisible");
        Wait(() => text.Opacity >= 1);
        Assert.AreEqual(1.0, text.Opacity);

        // Animations réduites : rien ne glisse.
        design.ReducedMotion = true;
        Assert.IsFalse(Transitions.FollowAsideOnceShown(text, -60));
        Assert.IsFalse(Transitions.ShiftAsideOnceShown(text, 40));
        Assert.AreEqual(Transform.Identity, text.RenderTransform);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.FollowAsideOnceShown(null!, 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.ShiftAsideOnceShown(null!, 1));
    }

    [STATestMethod]
    public void A_gauge_advances_and_a_count_rolls()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var gauge = new ProgressBar { Maximum = 100, Value = 20, Width = 200 };
        var count = new TextBlock();
        var fresh = new TextBlock();
        Transitions.SetRollingText(count, "17");
        var host = Host.Of(design, new StackPanel { Children = { gauge, count, fresh } });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Une jauge qui avance : sa barre a déjà sa nouvelle longueur, et repart de l'ancienne.
        Assert.IsTrue(Transitions.GetAdvances(gauge), "le style des jauges le demande");
        var indicator = Host.Part<FrameworkElement>(gauge, "PART_Indicator");
        gauge.Value = 60;
        Assert.IsInstanceOfType<ScaleTransform>(indicator.RenderTransform);
        var stretch = (ScaleTransform)indicator.RenderTransform;
        Assert.AreEqual(new Point(0, 0.5), indicator.RenderTransformOrigin, "elle grandit depuis son début");
        Wait(() => stretch.ScaleX < 0.99);
        Assert.IsLessThan(0.99, stretch.ScaleX, "elle part de l'ancienne longueur");
        Assert.IsGreaterThanOrEqualTo((20.0 / 60) - 0.001, stretch.ScaleX);
        Wait(() => stretch.ScaleX >= 1);
        Assert.AreEqual(1.0, stretch.ScaleX, "et arrive à la nouvelle");

        // Remise à zéro, ou repartie de zéro : elle ne glisse pas.
        gauge.Value = 0;
        Assert.AreEqual(Transform.Identity, indicator.RenderTransform);
        gauge.Value = 30;
        Assert.AreEqual(Transform.Identity, indicator.RenderTransform);
        // Animations réduites, jauge qui n'en veut plus, jauge sans modèle : rien.
        design.ReducedMotion = true;
        gauge.Value = 80;
        Assert.AreEqual(Transform.Identity, indicator.RenderTransform);
        design.ReducedMotion = false;
        Transitions.SetAdvances(gauge, false);
        gauge.Value = 90;
        Assert.AreEqual(Transform.Identity, indicator.RenderTransform);
        Assert.IsFalse(Transitions.Advance(new ProgressBar(), 10, 20));
        Transitions.SetAdvances(new Border(), true);

        // Un compte qui change : la nouvelle valeur arrive par le bas, en fondu.
        Assert.AreEqual("17", count.Text);
        Assert.AreEqual("17", Transitions.GetRollingText(count));
        Assert.IsFalse(count.HasAnimatedProperties, "posé avant d'être à l'écran, il ne bouge pas");
        Transitions.SetRollingText(count, "16");
        Assert.AreEqual("16", count.Text);
        Assert.IsTrue(count.HasAnimatedProperties);
        Assert.IsInstanceOfType<TranslateTransform>(count.RenderTransform);
        Wait(() => count.Opacity >= 1 && ((TranslateTransform)count.RenderTransform).Y == 0);
        Assert.AreEqual(1.0, count.Opacity);

        // Un compte qui paraît, ou qui s'en va, n'est pas un compte qui change ; animations réduites : rien.
        Transitions.SetRollingText(fresh, "3");
        Assert.AreEqual("3", fresh.Text);
        Assert.IsFalse(fresh.HasAnimatedProperties);
        Transitions.SetRollingText(fresh, null);
        Assert.AreEqual(string.Empty, fresh.Text);
        Assert.IsFalse(fresh.HasAnimatedProperties);
        Transitions.SetRollingText(fresh, "4");
        design.ReducedMotion = true;
        Transitions.SetRollingText(fresh, "5");
        Assert.AreEqual("5", fresh.Text);
        Assert.IsFalse(fresh.HasAnimatedProperties);
        Transitions.SetRollingText(new Border(), "sans effet");
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetAdvances(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetAdvances(null!, true));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetRollingText(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetRollingText(null!, "x"));
    }
}
