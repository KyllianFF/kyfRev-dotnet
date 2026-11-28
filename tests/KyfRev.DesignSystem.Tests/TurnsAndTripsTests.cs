using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.DesignSystem.Tests;

/// <summary>
/// Les restes de la rangée 19 et de la liste « Fluidité » : le chevron qui tourne quand un groupe ou un dossier se replie,
/// et le bloc mis en image le temps qu'il glisse (rang 12).
/// </summary>
[TestClass]
public sealed class TurnsAndTripsTests
{
    [STATestMethod]
    public void A_chevron_turns_a_quarter_when_its_group_folds_even_once_its_list_is_remade()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var group = new object();
        var chevron = new Icon { Text = Icons.ChevronDown };
        Transitions.SetTurnKey(chevron, group);
        Transitions.SetOpens(chevron, true);
        Assert.AreEqual(0, ((RotateTransform)chevron.RenderTransform).Angle, "ouvert, il pointe vers le bas");
        Assert.AreEqual(new Point(0.5, 0.5), chevron.RenderTransformOrigin, "il tourne sur lui-même");
        var panel = new StackPanel { Children = { chevron } };
        var host = Host.Of(design, panel);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();
        var rotation = (RotateTransform)chevron.RenderTransform;
        Assert.IsFalse(rotation.HasAnimatedProperties, "la vue qui s'ouvre montre ses chevrons d'un coup");

        // Replié à l'écran : il tourne d'un quart vers la droite, et s'y arrête.
        Transitions.SetOpens(chevron, false);
        Assert.IsTrue(rotation.HasAnimatedProperties);
        Assert.AreEqual(Transitions.FoldedAngle, rotation.Angle, "l'arrivée est posée d'avance");
        Wait(() => !rotation.HasAnimatedProperties);
        Assert.AreEqual(Transitions.FoldedAngle, rotation.Angle);
        Assert.AreSame(group, Transitions.GetTurnKey(chevron));
        Assert.IsFalse(Transitions.GetOpens(chevron));

        // La liste refaite : le chevron neuf du même groupe tourne depuis la position de l'ancien.
        panel.Children.Clear();
        var remade = new Icon { Text = Icons.ChevronDown };
        Transitions.SetTurnKey(remade, group);
        Transitions.SetOpens(remade, true);
        panel.Children.Add(remade);
        Host.Layout(host);
        Host.Flush();
        var turning = (RotateTransform)remade.RenderTransform;
        Assert.IsTrue(turning.HasAnimatedProperties, "déplié : il tourne vers le bas");
        Wait(() => !turning.HasAnimatedProperties);
        Assert.AreEqual(0, turning.Angle);

        // Un autre chevron, jamais vu, ou sans clé : il arrive à sa place, sans tourner.
        var other = new Icon { Text = Icons.ChevronDown };
        Transitions.SetTurnKey(other, "un dossier");
        Transitions.SetOpens(other, false);
        var bare = new Icon { Text = Icons.ChevronDown };
        Transitions.SetOpens(bare, false);
        panel.Children.Add(other);
        panel.Children.Add(bare);
        Host.Layout(host);
        Host.Flush();
        Assert.IsFalse(other.RenderTransform.HasAnimatedProperties);
        Assert.AreEqual(Transitions.FoldedAngle, ((RotateTransform)other.RenderTransform).Angle);
        Assert.IsFalse(bare.RenderTransform.HasAnimatedProperties);
        Transitions.SetOpens(bare, true);
        Assert.IsTrue(bare.RenderTransform.HasAnimatedProperties, "sans clé, il tourne quand même à l'écran");

        // Déjà dans cette position : rien. Sans valeur, rien non plus. Le même chevron en garde la rotation.
        Transitions.SetOpens(remade, true);
        Assert.IsFalse(turning.HasAnimatedProperties);
        Transitions.SetOpens(remade, null);
        Assert.AreSame(turning, remade.RenderTransform);

        // Animations réduites, ou la famille « Menu et volets » coupée : il change de position d'un coup.
        design.ReducedMotion = true;
        Transitions.SetOpens(remade, false);
        Assert.IsFalse(turning.HasAnimatedProperties);
        Assert.AreEqual(Transitions.FoldedAngle, turning.Angle);
        design.ReducedMotion = false;
        design.MotionOff = MotionKinds.Panels;
        Transitions.SetOpens(remade, true);
        Assert.IsFalse(turning.HasAnimatedProperties);
        design.MotionOff = MotionKinds.None;

        // Une rotation figée, ou un autre décalage : il prend la sienne.
        var frozen = new Icon { RenderTransform = new RotateTransform(30) };
        frozen.RenderTransform.Freeze();
        Transitions.SetOpens(frozen, false);
        Assert.AreEqual(Transitions.FoldedAngle, ((RotateTransform)frozen.RenderTransform).Angle);
        Transitions.SetOpens(new System.Windows.Documents.Run(), true);
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetOpens(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetOpens(null!, true));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.GetTurnKey(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Transitions.SetTurnKey(null!, 1));
    }

    [STATestMethod]
    public void The_turns_remembered_start_over_beyond_their_limit()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var panel = new StackPanel();
        var host = Host.Of(design, panel);
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        for (var index = 0; index <= Transitions.MostTurnsRemembered; index++)
        {
            var chevron = new Icon();
            Transitions.SetTurnKey(chevron, $"dossier {index}");
            Transitions.SetOpens(chevron, true);
            panel.Children.Add(chevron);
        }
        Host.Layout(host);
        Host.Flush();

        // Le premier a été oublié quand la mémoire a repris de zéro : refait replié, il arrive sans tourner.
        panel.Children.Clear();
        var first = new Icon();
        Transitions.SetTurnKey(first, "dossier 0");
        Transitions.SetOpens(first, false);
        panel.Children.Add(first);
        Host.Layout(host);
        Host.Flush();
        Assert.IsFalse(first.RenderTransform.HasAnimatedProperties);
    }

    [STATestMethod]
    public void A_block_is_cached_as_a_picture_while_it_glides_and_given_back_on_arrival()
    {
        var design = new DesignResources(ThemeKind.Light, reducedMotion: false);
        var pane = new Border { Width = 200, Height = 300, Visibility = Visibility.Collapsed, Child = new TextBlock { Text = "Plan" } };
        Transitions.SetShownFrom(pane, Dock.Left);
        var text = new Border { Width = 240, Height = 300 };
        var host = Host.Of(design, new DockPanel { LastChildFill = false, Children = { pane, text } });
        using var source = new HwndSource(new HwndSourceParameters("kyfRev essai") { Width = 480, Height = 320 }) { RootVisual = host };
        Host.Layout(host);
        Host.Flush();

        // Le volet qui glisse est en image le temps du trajet, puis redevient lui-même.
        Transitions.SetShown(pane, true);
        Host.Layout(host);
        Wait(() => pane.CacheMode is BitmapCache);
        var cache = (BitmapCache)pane.CacheMode;
        Assert.IsTrue(cache.SnapsToDevicePixels);
        Assert.IsGreaterThanOrEqualTo(1, cache.RenderAtScale, "à l'échelle de l'écran au moins");
        Wait(() => pane.CacheMode is null && pane.Clip is null);
        Assert.IsNull(pane.CacheMode, "arrivé, il est redessiné tel quel");

        // Le texte qui suit le volet : en image pendant qu'il glisse, rendu à l'arrivée.
        Assert.IsTrue(Transitions.FollowAsideOnceShown(text, -200));
        Wait(() => text.CacheMode is BitmapCache);
        Assert.IsInstanceOfType<BitmapCache>(text.CacheMode);
        Wait(() => text.CacheMode is null);
        Assert.IsNull(text.CacheMode);
        Assert.IsTrue(Transitions.FollowOnceShown(text, 40));
        Wait(() => text.CacheMode is BitmapCache);
        Transitions.Rest(text);
        Assert.IsNull(text.CacheMode, "remis à sa place en route : rendu tout de suite");

        // Une image que l'élément avait déjà est la sienne : elle reste.
        var own = new BitmapCache();
        text.CacheMode = own;
        Assert.IsTrue(Transitions.FollowOnceShown(text, 40));
        Wait(() => false, 400);
        Assert.AreSame(own, text.CacheMode);
        Transitions.Rest(text);
        Assert.AreSame(own, text.CacheMode);
        text.ClearValue(UIElement.CacheModeProperty);
    }

    [STATestMethod]
    public void A_trip_relaunched_keeps_the_picture_until_the_last_one_and_a_huge_block_is_never_cached()
    {
        var block = new Border { Width = 100, Height = 100 };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), block);
        var first = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(10));
        var second = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(10));
        Assert.IsTrue(Transitions.CacheWhileMoving(block, first));
        var cache = block.CacheMode;
        Assert.IsTrue(Transitions.CacheWhileMoving(block, second));
        Assert.AreSame(cache, block.CacheMode, "la même image pour le trajet qui relance l'autre");
        var still = new TranslateTransform();
        block.RenderTransform = still;
        var firstClock = first.CreateClock();
        var secondClock = second.CreateClock();
        still.ApplyAnimationClock(TranslateTransform.XProperty, firstClock);
        still.ApplyAnimationClock(TranslateTransform.YProperty, secondClock);
        Wait(() => firstClock.CurrentState != ClockState.Active && secondClock.CurrentState != ClockState.Active && block.CacheMode is null);
        Assert.IsNull(block.CacheMode, "au dernier trajet fini");

        var huge = new Border { Width = 3000, Height = 2000 };
        Host.Of(new DesignResources(ThemeKind.Light, reducedMotion: false), huge, 3000, 2000);
        Assert.IsFalse(Transitions.CacheWhileMoving(huge, new DoubleAnimation()), "trop grand : l'image coûterait plus qu'elle ne rapporte");
        Assert.IsNull(huge.CacheMode);
        Transitions.EndTrips(huge);
        Assert.IsNull(huge.CacheMode);
    }

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
}
