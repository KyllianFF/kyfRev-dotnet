using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// L'animation d'ouverture : les fiches de l'icône se feuillettent, la dernière s'écrit, le nom arrive, puis elle laisse la
/// place. Longue la première fois, sur l'accueil ; moins d'une seconde ensuite ; rien quand les animations sont réduites.
/// </summary>
/// <remarks>
/// Avec la variable d'environnement <c>KYFREV_CAPTURES</c> (un dossier), chaque essai y écrit les instants qu'il a regardés.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class OpeningTests
{
    [TestMethod]
    public void The_icon_is_read_piece_by_piece_from_its_drawing()
    {
        var shapes = BrandDrawing.Shapes;

        var cards = shapes.Where(shape => shape.Part == BrandPart.Card).ToList();
        Assert.AreEqual("-15|15|-7.5|7.5", string.Join('|', cards.Select(card => card.Angle.ToString(System.Globalization.CultureInfo.InvariantCulture))), "l'éventail");
        Assert.IsTrue(cards.All(card => card.Pivot == new Point(128, 256)), "il tourne autour du bas de l'icône");
        var sheet = shapes.Single(shape => shape.Part == BrandPart.Sheet);
        Assert.AreEqual(new Rect(48, 12, 160, 224), sheet.Bounds);
        Assert.AreEqual(4d, sheet.StrokeWidth);
        Assert.AreEqual(4, shapes.Count(shape => shape.Part == BrandPart.Line), "le titre et trois lignes");
        Assert.IsNotNull(shapes.Single(shape => shape.Part == BrandPart.Band).Outline, "le passage surligné est un tracé");
        Assert.AreEqual(new Rect(72, 194, 112, 4), shapes.Single(shape => shape.Part == BrandPart.Rail).Bounds);
        Assert.AreEqual(6, shapes.Count(shape => shape.Part == BrandPart.Dot), "les six étapes, de J0 à J30");
        Assert.IsTrue(shapes.All(shape => shape.Bounds.Right <= BrandDrawing.Size + 60 && shape.Bounds.Bottom <= BrandDrawing.Size));
        Assert.AreEqual(BrandPart.Card, shapes[0].Part, "dans l'ordre où elles se peignent : l'éventail d'abord");

        // Un dessin sans rien, ou sans frise : pas d'erreur, ce qui manque manque.
        Assert.IsEmpty(BrandDrawing.Read(string.Empty));
        var small = BrandDrawing.Read("""<svg xmlns="http://www.w3.org/2000/svg"><defs><rect x="1" y="1" width="2" height="2"/></defs><title>t</title><rect x="1" y="2" width="30" height="40" rx="3" fill="#102030"/><circle cx="10" cy="10" r="4" fill="#fff" stroke="#000" stroke-width="2"/><rect x="0" y="0" width="5" height="5" fill="#fff" transform="rotate(10)"/></svg>""");
        Assert.AreEqual("Sheet|Dot|Card", string.Join('|', small.Select(shape => shape.Part)));
        Assert.AreEqual(Color.FromRgb(0x10, 0x20, 0x30), small[0].Fill);
        Assert.AreEqual((new Rect(6, 6, 8, 8), Colors.Black, 2d), (small[1].Bounds, small[1].Stroke, small[1].StrokeWidth));
        Assert.AreEqual((10d, default(Point)), (small[2].Angle, small[2].Pivot));
    }

    [TestMethod]
    public void The_usual_opening_is_slow_enough_to_be_seen_and_the_first_one_takes_its_time()
    {
        Assert.AreEqual(2.7, OpeningPace.Usual.Total, 0.001, "assez posée pour qu'on voie chaque fiche passer");
        Assert.IsLessThan(3, OpeningPace.Usual.Total, "sans faire attendre chaque jour");
        Assert.IsLessThan(OpeningPace.First.Total, OpeningPace.Usual.Total);
        Assert.IsGreaterThanOrEqualTo(0.15, OpeningPace.Usual.FlipGap, "les fiches partent l'une après l'autre, pas d'un seul geste");
        Assert.AreEqual(4.75, OpeningPace.First.Total, 0.001);
        Assert.AreEqual(OpeningPace.First.Name + OpeningPace.First.NameLength + OpeningPace.First.Rest, OpeningPace.First.Drawn);

        // L'icône se voit dans une grande fenêtre et tient dans la plus étroite.
        Assert.AreEqual(120, OpeningView.MarkFor(1280, first: true));
        Assert.AreEqual(59, OpeningView.MarkFor(470, first: true));
        Assert.AreEqual(48, OpeningView.MarkFor(200, first: true));
        Assert.AreEqual(76, OpeningView.MarkFor(3440, first: false));
        Assert.AreEqual(40, OpeningView.MarkFor(470, first: false));
    }

    [STATestMethod]
    [DataRow(false, 1280)]
    [DataRow(true, 1280)]
    [DataRow(false, 470)]
    [DataRow(true, 470)]
    [DataRow(false, 2600)]
    public void The_sheets_are_leafed_through_the_last_one_is_written_and_the_name_comes(bool dark, int width)
    {
        using var ui = new UiThread();
        var pace = OpeningPace.First;
        using var scene = new Scene(dark, width, 640, pace);
        var view = scene.View;
        var name = $"ouverture-{width}-{(dark ? "sombre" : "clair")}";

        // Au départ : la pile, encore invisible, et rien d'écrit.
        view.ShowAt(0);
        Assert.AreEqual(0d, view.Logo.Opacity);
        Assert.HasCount(4, view.Tops);
        Assert.HasCount(4, view.Backs);
        Assert.HasCount(6, view.Written, "le titre, trois lignes, le passage surligné, le trait de la frise");
        Assert.IsInstanceOfType<Rectangle>(view.Written[^1], "le trait de la frise s'écrit en dernier");
        Assert.HasCount(6, view.Dots);
        Assert.IsTrue(view.Tops.All(top => Angle(top) == 0 && top.Opacity == 1), "les fiches sont en pile");
        Assert.IsTrue(view.Backs.All(back => back.Opacity == 0));
        Assert.IsTrue(view.Written.All(line => Scale(line).ScaleX == 0));
        Assert.IsTrue(view.Dots.All(dot => Scale(dot).ScaleX == 0 && Scale(dot).ScaleY == 0));
        Assert.AreEqual(0d, view.NameClip.Width);

        // On feuillette : la première fiche est partie se ranger, la troisième est en train de partir, la dernière attend.
        var third = pace.Flip + (2 * pace.FlipGap) + (pace.FlipLength * 0.5);
        view.ShowAt(third);
        scene.Capture($"{name}-1-feuilleter");
        Assert.AreEqual(1d, view.Logo.Opacity, "la pile est apparue");
        Assert.AreEqual(0d, view.Tops[0].Opacity);
        Assert.AreEqual(1d, view.Backs[0].Opacity, 0.001);
        Assert.AreEqual(-15d, Angle(view.Backs[0]), 0.001, "à sa place dans l'éventail");
        Assert.IsGreaterThan(0d, Math.Abs(Angle(view.Tops[2])), "la troisième tourne");
        Assert.AreEqual(1d, view.Tops[2].Opacity);
        Assert.AreEqual(0d, Angle(view.Tops[3]), "la quatrième n'a pas bougé");
        Assert.IsTrue(view.Written.All(line => Scale(line).ScaleX == 0), "rien ne s'écrit avant que la dernière fiche ne soit découverte");

        // La dernière fiche s'écrit : le titre et la première ligne sont là, le passage se surligne, la frise attend.
        var band = (Shape)view.Written.Single(line => line is Path);
        view.ShowAt(pace.Write + (pace.Step * 2) + (pace.Line * 0.6));
        scene.Capture($"{name}-2-ecrire");
        Assert.IsTrue(view.Tops.All(top => top.Opacity == 0), "toutes les fiches sont rangées");
        Assert.AreEqual(1d, Scale(view.Written[0]).ScaleX, 0.001, "le titre");
        Assert.AreEqual(1d, Scale(view.Written[1]).ScaleX, 0.001, "la première ligne");
        Assert.IsTrue(Scale(band).ScaleX is > 0 and < 1, "le surligneur passe");
        Assert.AreEqual(0d, Scale(view.Written[^1]).ScaleX, "la frise vient après");
        Assert.IsTrue(view.Dots.All(dot => Scale(dot).ScaleX == 0));

        // La frise des J : les premières étapes sont posées, les dernières pas encore.
        view.ShowAt(pace.Dots + (pace.Dot * 3.2));
        scene.Capture($"{name}-3-frise");
        Assert.AreEqual(1d, Scale(view.Dots[0]).ScaleX, 0.05);
        Assert.AreEqual(0d, Scale(view.Dots[5]).ScaleX);
        Assert.AreEqual(0d, view.NameClip.Width, "le nom vient après");

        // Le logotype entier : l'éventail, la fiche écrite, le nom, comme le logotype de l'application.
        view.ShowAt(pace.Drawn);
        scene.Capture($"{name}-4-logotype");
        for (var index = 0; index < 4; index++)
        {
            Assert.AreEqual(new[] { -15d, 15d, -7.5d, 7.5d }[index], Angle(view.Backs[index]), 0.001);
            Assert.AreEqual(1d, view.Backs[index].Opacity, 0.001);
        }
        Assert.IsTrue(view.Written.All(line => Math.Abs(Scale(line).ScaleX - 1) < 0.001));
        Assert.IsTrue(view.Dots.All(dot => Math.Abs(Scale(dot).ScaleX - 1) < 0.001 && Math.Abs(Scale(dot).ScaleY - 1) < 0.001));
        Assert.AreEqual(1d, view.NameText.Opacity, 0.001);
        Assert.IsGreaterThan(view.Mark, view.NameClip.Width, "le nom est découvert en entier");
        scene.Settle();
        var logo = view.Logo.TransformToVisual(scene.Host).TransformBounds(new Rect(view.Logo.RenderSize));
        Assert.AreEqual(width / 2d, logo.Left + (logo.Width / 2), 1.5, "au milieu de la fenêtre");
        Assert.IsLessThanOrEqualTo(width - 32d, logo.Width, "et il y tient");

        // Le même que le logotype de l'application, à la même taille : c'est lui qui prend la suite.
        var own = new Logotype { MarkSize = view.Mark, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        scene.Host.Children.Add(own);
        scene.Settle();
        Assert.AreEqual(own.ActualWidth, logo.Width, 3);
        Assert.AreSame(scene.Host.FindResource("Kyf.Brush.Text"), view.NameText.Foreground);
        Assert.AreSame(scene.Host.FindResource("Kyf.Brush.Primary"), view.NameText.Inlines.OfType<System.Windows.Documents.Run>().Last().Foreground);
        Assert.AreSame(scene.Host.FindResource("Kyf.Brush.Background"), ((Border)view.Veil).Background, "le fond du thème, dès la première image");
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_first_opening_carries_the_logotype_to_its_place_on_the_welcome_page(bool dark)
    {
        using var ui = new UiThread();
        var pace = OpeningPace.First;
        var landing = new Logotype { MarkSize = 56, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 120, 0, 0) };
        using var scene = new Scene(dark, 1280, 720, pace, landing);
        var view = scene.View;
        var finished = 0;
        view.Finished += (_, _) => finished++;

        // Tant que l'animation joue, le logotype de l'accueil est caché : c'est celui de l'animation qui vient à sa place.
        view.ShowAt(pace.Drawn);
        Assert.AreEqual(0d, landing.Opacity);
        Assert.AreEqual(1d, view.Veil.Opacity);

        view.ShowAt(pace.Drawn + (pace.Leave * 0.5));
        scene.Capture($"ouverture-accueil-{(dark ? "sombre" : "clair")}-en-route");
        Assert.IsTrue(view.Veil.Opacity is > 0 and < 1, "l'accueil paraît dessous pendant que le logotype monte");

        view.ShowAt(pace.Total);
        scene.Settle();
        scene.Capture($"ouverture-accueil-{(dark ? "sombre" : "clair")}-arrive");
        Assert.AreEqual(0d, view.Veil.Opacity, 0.001);
        var arrived = view.Logo.TransformToVisual(scene.Host).TransformBounds(new Rect(view.Logo.RenderSize));
        var place = landing.TransformToVisual(scene.Host).TransformBounds(new Rect(landing.RenderSize));
        Assert.AreEqual(place.Left + (place.Width / 2), arrived.Left + (arrived.Width / 2), 1, "au même endroit");
        Assert.AreEqual(place.Top + (place.Height / 2), arrived.Top + (arrived.Height / 2), 1);
        Assert.AreEqual(place.Width, arrived.Width, 4, "à la même taille");
        Assert.AreEqual(0, finished, "arrêtée à cet instant, elle n'a pas fini");

        // Passée ou finie : le logotype de l'accueil reparaît, une seule fois prévenue.
        view.Skip();
        view.Skip();
        Assert.AreEqual(1, finished);
        Assert.IsTrue(view.IsFinished);
        Assert.AreEqual(1d, landing.Opacity);
        view.Play();
        Assert.AreEqual(1, finished, "finie, elle ne rejoue pas");
    }

    [STATestMethod]
    public void It_plays_once_to_its_end_and_a_click_skips_it()
    {
        using var ui = new UiThread();
        using var scene = new Scene(dark: false, 900, 600, OpeningPace.Usual);
        var view = scene.View;
        var finished = 0;
        view.Finished += (_, _) => finished++;
        var watch = Stopwatch.StartNew();

        view.Play();
        view.Play();
        UiThread.Until(() => view.IsFinished);

        Assert.AreEqual(1, finished);
        Assert.IsGreaterThan(OpeningPace.Usual.Total * 0.8, watch.Elapsed.TotalSeconds, "elle a pris son temps, pas moins");
        Assert.IsTrue(view.IsLeaving, "elle a laissé la place en fondu");

        // Un clic la passe, et s'arrête là : il n'atteint pas la vue cachée dessous.
        using var other = new Scene(dark: false, 900, 600, OpeningPace.First);
        var clicked = 0;
        other.View.Finished += (_, _) => clicked++;
        other.View.Play();
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent };
        other.View.RaiseEvent(click);
        Assert.IsTrue(click.Handled);
        Assert.AreEqual(1, clicked);
        Assert.IsFalse(other.View.IsLeaving, "passée, elle n'a rien d'autre à jouer");
    }

    [STATestMethod]
    public void The_logotype_stays_while_the_view_is_still_loading()
    {
        using var ui = new UiThread();
        var loading = true;
        using var scene = new Scene(dark: false, 900, 600, OpeningPace.Usual, stillLoading: () => loading);
        var view = scene.View;
        view.ShowAt(OpeningPace.Usual.Drawn);

        view.Drawn();
        Assert.IsTrue(view.IsWaiting, "la vue se lit encore : le logotype reste");
        Assert.IsFalse(view.IsLeaving);
        view.Drawn();
        TestShell.Flush();
        Assert.IsTrue(view.IsWaiting);
        Assert.IsFalse(view.IsFinished);

        loading = false;
        UiThread.Until(() => view.IsLeaving);
        Assert.IsFalse(view.IsWaiting);
        UiThread.Until(() => view.IsFinished);

        // Rien à attendre : elle laisse la place aussitôt.
        using var ready = new Scene(dark: false, 900, 600, OpeningPace.Usual, stillLoading: () => false);
        ready.View.ShowAt(OpeningPace.Usual.Drawn);
        ready.View.Drawn();
        Assert.IsFalse(ready.View.IsWaiting);
        Assert.IsTrue(ready.View.IsLeaving);
        Assert.IsLessThanOrEqualTo(TimeSpan.FromSeconds(5), OpeningView.LongestWait, "une vue qui ne finit pas de se lire ne retient pas l'ouverture");
    }

    [STATestMethod]
    public void The_window_plays_the_long_opening_once_on_the_welcome_page_then_the_short_one()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var machine = new MachineSettings(new MemoryMachinePreferences());
        var memory = new FirstUse(machine);

        // Animations réduites, ou coupées pour les essais : rien ne joue, rien n'est retenu.
        var (window, shell) = Window(memory, vaults);
        try
        {
            Assert.IsFalse(window.PlayOpening(animated: false));
            Assert.IsFalse(window.PlayOpening(), "les essais coupent les transitions");
            Assert.IsNull(window.Opening);
            Assert.IsFalse(memory.Has(FirstUse.OpeningSeen));

            // La première fois, sur l'accueil : la longue, qui vise le logotype de l'accueil.
            Assert.IsTrue(window.PlayOpening(animated: true));
            var opening = window.Opening!;
            Assert.AreSame(OpeningPace.First, opening.Pace);
            Assert.IsTrue(memory.Has(FirstUse.OpeningSeen), "retenue sur l'ordinateur dès qu'elle commence");
            Assert.IsFalse(window.PlayOpening(animated: true), "une seule à la fois");
            var brand = (Logotype)Descendants(window).OfType<WelcomePage>().Single().FindName("Brand");
            Assert.AreEqual(0d, brand.Opacity, "le logotype de l'accueil attend celui de l'animation");
            var frame = (Grid)window.FindName("Frame");
            Assert.AreSame(opening, frame.Children[^1], "par-dessus toute la fenêtre");
            Assert.AreEqual(frame.RowDefinitions.Count, Grid.GetRowSpan(opening), "barre de titre comprise");
            Assert.IsTrue(opening.HasBegun, "la fenêtre déjà à l'écran : elle joue aussitôt");

            // Une touche la passe, sans activer le bouton que l'on ne voit pas encore.
            var key = Key(window, System.Windows.Input.Key.Enter);
            Assert.IsTrue(key.Handled);
            Assert.IsNull(window.Opening);
            Assert.IsTrue(opening.IsFinished);
            Assert.AreEqual(1d, brand.Opacity);
            Assert.IsFalse(frame.Children.Contains(opening));
            Assert.IsFalse(Key(window, System.Windows.Input.Key.Enter).Handled, "l'animation passée, les touches reprennent leur cours");
        }
        finally
        {
            window.Close();
        }

        // Les fois suivantes, même sur l'accueil : la courte.
        (window, shell) = Window(memory, vaults);
        try
        {
            Assert.IsTrue(window.PlayOpening(animated: true));
            Assert.AreSame(OpeningPace.Usual, window.Opening!.Pace);
            // Maj seule ne passe rien ; un raccourci la passe et garde son effet.
            Assert.IsFalse(Key(window, System.Windows.Input.Key.LeftShift).Handled);
            Assert.IsNotNull(window.Opening);
            window.Opening.Skip();
            Assert.IsNull(window.Opening);
        }
        finally
        {
            window.Close();
        }

        // Dans l'espace de travail : la courte, qui attend la liste du jour.
        (window, shell) = Window(new FirstUse(new MachineSettings(new MemoryMachinePreferences())), null);
        try
        {
            Assert.IsTrue(window.PlayOpening(animated: true));
            Assert.AreSame(OpeningPace.Usual, window.Opening!.Pace);
            Assert.IsNotNull(window.Opening.StillLoading);
            Assert.IsFalse(window.Opening.StillLoading(), "cette vue n'a rien à lire");
            Assert.IsNull(window.Opening.Landing, "pas de logotype où aller : un fondu");
            // Dans un champ, on tape déjà : la touche passe l'animation et arrive au champ.
            var box = new TextBox();
            ((Grid)window.FindName("Root")).Children.Insert(0, box);
            TestShell.Flush();
            Keyboard.Focus(box);
            if (Keyboard.FocusedElement == box)
            {
                Assert.IsFalse(Key(window, System.Windows.Input.Key.A).Handled);
                Assert.IsNull(window.Opening);
            }
        }
        finally
        {
            window.Close();
        }
        Assert.IsNotNull(shell);
    }

    [STATestMethod]
    public void Laid_before_the_window_shows_it_is_the_first_thing_drawn_and_starts_once_on_screen()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        test.Shell.FirstUse = new FirstUse(new MachineSettings(new MemoryMachinePreferences()));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        try
        {
            // Comme au démarrage : posée avant que la fenêtre ne soit montrée.
            Assert.IsFalse(window.IsRendered);
            Assert.IsTrue(window.PlayOpening(animated: true));
            var opening = window.Opening!;
            Assert.AreEqual(OpeningView.MarkFor(1000, first: false), opening.Mark, "à la taille de la fenêtre qui va paraître");
            Assert.IsFalse(opening.HasBegun, "elle attend que la fenêtre soit à l'écran");

            window.Show();
            UiThread.Until(() => window.IsRendered);
            TestShell.Flush();

            Assert.IsTrue(opening.HasBegun, "la fenêtre à l'écran, elle commence");
            var frame = (Grid)window.FindName("Frame");
            var titleBar = (FrameworkElement)window.FindName("TitleBar");
            var covered = opening.TransformToVisual(frame).TransformBounds(new Rect(opening.RenderSize));
            var bar = titleBar.TransformToVisual(frame).TransformBounds(new Rect(titleBar.RenderSize));
            Assert.IsTrue(covered.Contains(bar), "la barre de titre est sous l'animation");
            Assert.AreEqual(1d, opening.Veil.Opacity, "le fond de l'animation cache l'interface");
            Assert.AreEqual(0d, opening.Logo.Opacity, 0.2, "et le logotype commence à paraître, du début");
            opening.Skip();
            Assert.IsNull(window.Opening);
            Assert.IsFalse(frame.Children.Contains(opening));
        }
        finally
        {
            window.Close();
        }
    }

    private static (ShellWindow Window, ShellViewModel Shell) Window(FirstUse memory, TestVaults? vaults)
    {
        var test = new TestShell();
        test.Shell.FirstUse = memory;
        if (vaults is not null)
        {
            test.Shell.ShowWelcome(new WelcomePageViewModel(vaults.App.Vaults, vaults.Picker, new AppState("1.0.0-essai", AppMode.Development, null, null, null), _ => { }));
        }
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return (window, test.Shell);
    }

    private static KeyEventArgs Key(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window)!;
        var pressed = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = UIElement.PreviewKeyDownEvent };
        window.RaiseEvent(pressed);
        return pressed;
    }

    private static double Angle(Shape piece) => ((RotateTransform)piece.RenderTransform).Angle;

    private static ScaleTransform Scale(Shape piece) => (ScaleTransform)piece.RenderTransform;

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>L'animation posée dans une fenêtre, dans un thème, par-dessus ce qu'elle doit rejoindre s'il y en a.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;

        public Scene(bool dark, double width, double height, OpeningPace pace, Logotype? landing = null, Func<bool>? stillLoading = null)
        {
            var design = new DesignResources(dark ? ThemeKind.Dark : ThemeKind.Light, reducedMotion: false);
            Host = new Grid { Width = width, Height = height };
            Host.Resources.MergedDictionaries.Add(design.Dictionary);
            Host.SetResourceReference(Panel.BackgroundProperty, "Kyf.Brush.Background");
            if (landing is not null)
            {
                Host.Children.Add(landing);
            }
            View = new OpeningView(pace, OpeningView.MarkFor(width, ReferenceEquals(pace, OpeningPace.First)))
            {
                Landing = landing is null ? null : () => landing,
                StillLoading = stillLoading,
            };
            Host.Children.Add(View);
            _window = new Window { Content = new Canvas { Children = { Host } }, Left = -10000, Top = -10000, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
            _window.Show();
            Settle();
        }

        public Grid Host { get; }

        public OpeningView View { get; }

        public void Settle()
        {
            TestShell.Flush();
            Host.UpdateLayout();
            TestShell.Flush();
        }

        public void Capture(string name)
        {
            Settle();
            Host.Capture(name);
        }

        public void Dispose() => _window.Close();
    }
}
