using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using KyfRev.DesignSystem;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// Le rythme de l'animation d'ouverture, en secondes depuis son début : quand les fiches se feuillettent, quand la dernière
/// s'écrit, quand la frise des J se remplit, quand le nom arrive, et combien dure ce qui lui fait quitter l'écran.
/// </summary>
/// <param name="Appear">Le temps que met la pile de fiches à apparaître.</param>
/// <param name="Flip">Le moment où la première fiche quitte le dessus de la pile.</param>
/// <param name="FlipGap">L'écart entre deux fiches qui partent.</param>
/// <param name="FlipLength">Le temps que met une fiche à partir.</param>
/// <param name="Write">Le moment où la dernière fiche commence à s'écrire.</param>
/// <param name="Step">L'écart entre deux lignes qui s'écrivent.</param>
/// <param name="Line">Le temps que met une ligne à s'écrire.</param>
/// <param name="Dots">Le moment où la première étape de la frise des J paraît.</param>
/// <param name="Dot">L'écart entre deux étapes.</param>
/// <param name="Name">Le moment où le nom commence à paraître.</param>
/// <param name="NameLength">Le temps que met le nom à paraître.</param>
/// <param name="Rest">Le temps laissé au logotype entier avant qu'il ne laisse la place.</param>
/// <param name="Leave">Le temps que met l'animation à laisser la place à la vue.</param>
internal sealed record OpeningPace(
    double Appear, double Flip, double FlipGap, double FlipLength, double Write, double Step, double Line, double Dots, double Dot, double Name, double NameLength, double Rest, double Leave)
{
    /// <summary>La première ouverture : on voit chaque fiche passer, puis le logotype monte en tête de l'accueil.</summary>
    public static OpeningPace First { get; } = new(0.3, 0.4, 0.26, 0.34, 1.55, 0.14, 0.24, 2.45, 0.09, 3.1, 0.5, 0.3, 0.85);

    /// <summary>Toutes les autres : le même déroulé en moins d'une seconde.</summary>
    public static OpeningPace Usual { get; } = new(0.05, 0.04, 0.055, 0.12, 0.26, 0.03, 0.12, 0.38, 0.03, 0.46, 0.22, 0.08, 0.18);

    /// <summary>Le moment où le logotype est entier et a été vu.</summary>
    public double Drawn => Name + NameLength + Rest;

    /// <summary>La durée de toute l'animation, quand rien ne la retient.</summary>
    public double Total => Drawn + Leave;
}

/// <summary>
/// L'animation d'ouverture de kyfRev, posée par-dessus la vue qui s'ouvre : les fiches de l'icône se feuillettent et se
/// rangent en éventail, la dernière s'écrit (son titre, ses lignes, un passage surligné, puis la frise des J), et le nom
/// arrive en fondu. Elle laisse ensuite la place : en fondu, ou, à la première ouverture, en portant le logotype à sa place
/// en tête de l'accueil. Un clic la passe. Elle ne dessine rien : elle déplace les pièces de <see cref="BrandDrawing"/>.
/// </summary>
internal sealed class OpeningView : Grid
{
    /// <summary>Le plus longtemps qu'elle attend une vue qui se lit encore, le logotype affiché.</summary>
    internal static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(4);

    private static readonly KeySpline Settling = Frozen(new KeySpline(0.2, 0.8, 0.2, 1));
    private static readonly KeySpline Leaving = Frozen(new KeySpline(0.5, 0, 0.8, 0.4));
    private static readonly KeySpline Carrying = Frozen(new KeySpline(0.4, 0, 0.2, 1));

    private readonly OpeningPace _pace;
    private readonly Storyboard _play = new();
    private readonly Storyboard _leave = new();
    private readonly Border _veil = new();
    private readonly StackPanel _logo;
    private readonly Border _nameClip;
    private readonly TextBlock _name;
    private readonly List<Shape> _tops = [];
    private readonly List<Shape> _backs = [];
    private readonly List<Shape> _written = [];
    private readonly List<Shape> _dots = [];
    private readonly List<double> _angles = [];
    private Shape? _rail;
    private Logotype? _landing;
    private DispatcherTimer? _wait;
    private bool _prepared;
    private bool _begun;
    private bool _leaveBuilt;
    private bool _leaveBegun;

    /// <summary>L'animation, à ce rythme, pour une icône de <paramref name="mark"/> pixels de côté.</summary>
    public OpeningView(OpeningPace pace, double mark)
    {
        ArgumentNullException.ThrowIfNull(pace);
        _pace = pace;
        Mark = mark;
        Focusable = false;
        _veil.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
        Children.Add(_veil);

        // Les proportions du logotype de l'application : le nom à 0,8 fois l'icône, un quart d'icône entre les deux.
        var rev = new Run("Rev");
        rev.SetResourceReference(TextElement.ForegroundProperty, "Kyf.Brush.Primary");
        _name = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            FontSize = Math.Round(mark * 0.8),
            Margin = new Thickness(Math.Round(mark / 4), 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
        };
        _name.Inlines.Add(new Run("kyf"));
        _name.Inlines.Add(rev);
        _name.SetResourceReference(TextBlock.FontFamilyProperty, DesignKeys.DisplayFont);
        _name.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.Text");
        _nameClip = new Border { Width = 0, ClipToBounds = true, Child = _name, VerticalAlignment = VerticalAlignment.Center };
        _logo = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, 1), new TranslateTransform() } },
        };
        _logo.Children.Add(new Viewbox { Width = mark, Height = mark, Child = Draw(), VerticalAlignment = VerticalAlignment.Center });
        _logo.Children.Add(_nameClip);
        Children.Add(_logo);
        // Un clic n'importe où la passe ; il s'arrête là, sans atteindre la vue cachée dessous.
        MouseDown += (_, e) =>
        {
            Skip();
            e.Handled = true;
        };
    }

    /// <summary>Elle a laissé la place : la fenêtre la retire.</summary>
    public event EventHandler? Finished;

    /// <summary>Le côté de l'icône, en pixels.</summary>
    public double Mark { get; }

    /// <summary>Son rythme.</summary>
    public OpeningPace Pace => _pace;

    /// <summary>
    /// Le logotype de la vue qui s'ouvre, s'il y en a un (l'accueil) : celui de l'animation monte à sa place, et c'est lui
    /// qu'on voit ensuite, sans coupure. Demandé au moment de jouer, la vue une fois en place.
    /// </summary>
    public Func<Logotype?>? Landing { get; init; }

    /// <summary>La vue qui s'ouvre se lit encore : le logotype reste affiché au lieu de laisser voir une vue vide.</summary>
    public Func<bool>? StillLoading { get; init; }

    /// <summary>Elle a laissé la place, ou on l'a passée.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Le logotype est entier, et elle attend que la vue soit prête pour laisser la place.</summary>
    internal bool IsWaiting => _wait is { IsEnabled: true };

    /// <summary>Elle est en train de laisser la place.</summary>
    internal bool IsLeaving => _leaveBegun;

    /// <summary>Les fiches qui quittent le dessus de la pile, la première à partir d'abord.</summary>
    internal IReadOnlyList<Shape> Tops => _tops;

    /// <summary>Les mêmes fiches, rangées en éventail derrière.</summary>
    internal IReadOnlyList<Shape> Backs => _backs;

    /// <summary>Ce qui s'écrit sur la dernière fiche, dans l'ordre : le titre, les lignes, le passage surligné, le trait de la frise.</summary>
    internal IReadOnlyList<Shape> Written => _written;

    /// <summary>Les étapes de la frise des J.</summary>
    internal IReadOnlyList<Shape> Dots => _dots;

    /// <summary>Le logotype : l'icône, puis le nom.</summary>
    internal FrameworkElement Logo => _logo;

    /// <summary>Ce qui découvre le nom, de gauche à droite.</summary>
    internal FrameworkElement NameClip => _nameClip;

    /// <summary>Le nom.</summary>
    internal TextBlock NameText => _name;

    /// <summary>Le fond qui cache la vue pendant l'animation.</summary>
    internal FrameworkElement Veil => _veil;

    /// <summary>La taille de l'icône pour une fenêtre de cette largeur : assez grande pour se voir, jamais plus large que la fenêtre.</summary>
    public static double MarkFor(double width, bool first) =>
        first ? Math.Round(Math.Clamp(width / 8, 48, 120)) : Math.Round(Math.Clamp(width / 12, 40, 76));

    /// <summary>Joue l'animation, une fois posée dans la fenêtre.</summary>
    public void Play()
    {
        if (_begun || IsFinished)
        {
            return;
        }
        Prepare();
        _begun = true;
        _play.Completed += (_, _) => Drawn();
        _play.Begin(this, isControllable: true);
    }

    /// <summary>La passe : la vue se montre tout de suite.</summary>
    public void Skip() => Finish();

    /// <summary>
    /// Pour les essais et les captures : montre l'animation telle qu'elle est à cet instant, arrêtée. Au-delà du logotype
    /// entier, c'est ce qui lui fait quitter l'écran qui est montré.
    /// </summary>
    internal void ShowAt(double seconds)
    {
        Prepare();
        _begun = true;
        _play.Begin(this, isControllable: true);
        _play.Pause(this);
        _play.SeekAlignedToLastTick(this, TimeSpan.FromSeconds(Math.Clamp(seconds, 0, _pace.Drawn)), TimeSeekOrigin.BeginTime);
        if (seconds > _pace.Drawn)
        {
            BuildLeave();
            _leaveBegun = true;
            _leave.Begin(this, isControllable: true);
            _leave.Pause(this);
            _leave.SeekAlignedToLastTick(this, TimeSpan.FromSeconds(Math.Min(seconds - _pace.Drawn, _pace.Leave)), TimeSeekOrigin.BeginTime);
        }
    }

    /// <summary>Le logotype est entier : elle laisse la place, ou attend la vue qui se lit encore.</summary>
    internal void Drawn()
    {
        if (IsFinished || _leaveBegun || IsWaiting)
        {
            return;
        }
        if (StillLoading?.Invoke() != true)
        {
            BeginLeave();
            return;
        }
        var waited = TimeSpan.Zero;
        _wait = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
        _wait.Tick += (_, _) =>
        {
            waited += _wait.Interval;
            if (StillLoading?.Invoke() != true || waited >= LongestWait)
            {
                _wait.Stop();
                BeginLeave();
            }
        };
        _wait.Start();
    }

    private void BeginLeave()
    {
        if (IsFinished || _leaveBegun)
        {
            return;
        }
        BuildLeave();
        _leaveBegun = true;
        _leave.Completed += (_, _) => Finish();
        _leave.Begin(this, isControllable: true);
    }

    private void Finish()
    {
        if (IsFinished)
        {
            return;
        }
        IsFinished = true;
        _wait?.Stop();
        if (_begun)
        {
            _play.Stop(this);
        }
        if (_leaveBegun)
        {
            _leave.Stop(this);
        }
        // Le logotype de la vue reparaît à l'instant où celui de l'animation disparaît : ils sont au même endroit.
        _landing?.ClearValue(OpacityProperty);
        Finished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Mesure le nom et règle chaque pièce sur le rythme : fait une fois, la vue posée dans la fenêtre.</summary>
    private void Prepare()
    {
        if (_prepared)
        {
            return;
        }
        _prepared = true;
        _landing = Landing?.Invoke();
        // Caché le temps de l'animation : c'est celui de l'animation qui vient à sa place.
        _landing?.Opacity = 0;
        _name.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = Math.Ceiling(_name.DesiredSize.Width);
        var pace = _pace;

        Animate(_play, _logo, new PropertyPath(OpacityProperty), (0, 0, null), (pace.Appear, 1, null));
        for (var index = 0; index < _tops.Count; index++)
        {
            var angle = _angles[index];
            var away = Math.Sign(angle) * (16.5 + (Math.Abs(angle) * 0.9));
            var start = pace.Flip + (index * pace.FlipGap);
            var end = start + pace.FlipLength;
            var gone = start + (pace.FlipLength * 0.7);
            // La fiche quitte le dessus de la pile en tournant, et s'efface en fin de course…
            Animate(_play, _tops[index], Turn, (0, 0, null), (start, 0, null), (gone, away * 0.8, Leaving), (end, away, null));
            Animate(_play, _tops[index], new PropertyPath(OpacityProperty), (0, 1, null), (gone, 1, null), (end, 0, null));
            // … pendant que la même, derrière, vient se ranger à sa place dans l'éventail.
            var from = start + (pace.FlipLength * 0.55);
            var placed = end + (pace.FlipLength * 0.6);
            Animate(_play, _backs[index], Turn, (0, angle * 1.6, null), (from, angle * 1.6, null), (placed, angle, Settling));
            Animate(_play, _backs[index], new PropertyPath(OpacityProperty), (0, 0, null), (from, 0, null), (placed, 1, Settling));
        }
        // La dernière fiche s'écrit, une ligne après l'autre ; le passage surligné prend le temps d'un coup de surligneur.
        var rank = 0;
        foreach (var line in _written)
        {
            var rail = line == _rail;
            var band = line is Path;
            var from = rail ? pace.Dots - (pace.Dot * 0.5) : pace.Write + (pace.Step * rank);
            var length = rail ? pace.Dot * 5 : band ? pace.Line * 1.7 : pace.Line;
            Animate(_play, line, Widen, (0, 0, null), (from, 0, null), (from + length, 1, Settling));
            rank += band ? 2 : 1;
        }
        // La frise des J : une étape après l'autre, chacune avec un léger rebond.
        for (var index = 0; index < _dots.Count; index++)
        {
            var from = pace.Dots + (index * pace.Dot);
            foreach (var path in new[] { Widen, Heighten })
            {
                var grow = new DoubleAnimationUsingKeyFrames();
                grow.KeyFrames.Add(new LinearDoubleKeyFrame(0, At(0)));
                grow.KeyFrames.Add(new LinearDoubleKeyFrame(0, At(from)));
                grow.KeyFrames.Add(new EasingDoubleKeyFrame(1, At(from + (pace.Dot * 2.4)), new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 }));
                Add(_play, grow, _dots[index], path);
            }
        }
        // Le nom se découvre de gauche à droite, en fondu, et pousse l'icône à sa place dans le logotype.
        Animate(_play, _nameClip, new PropertyPath(WidthProperty), (0, 0, null), (pace.Name, 0, null), (pace.Name + pace.NameLength, natural, Settling));
        Animate(_play, _name, new PropertyPath(OpacityProperty), (0, 0, null), (pace.Name, 0, null), (pace.Name + pace.NameLength, 1, Settling));
        _play.Duration = TimeSpan.FromSeconds(pace.Drawn);
    }

    /// <summary>
    /// Ce qui lui fait quitter l'écran : le logotype porté à la place de celui de la vue pendant que le fond s'efface, ou,
    /// sans logotype où aller, un simple fondu.
    /// </summary>
    private void BuildLeave()
    {
        if (_leaveBuilt)
        {
            return;
        }
        _leaveBuilt = true;
        var length = _pace.Leave;
        UpdateLayout();
        if (_landing is { IsVisible: true, ActualWidth: > 0, ActualHeight: > 0 } landing && _logo.ActualWidth > 0)
        {
            var target = landing.TransformToVisual(this).TransformBounds(new Rect(landing.RenderSize));
            var here = _logo.TransformToVisual(this).TransformBounds(new Rect(_logo.RenderSize));
            // La taille de son icône à l'écran, zoom de l'interface compris.
            var scale = landing.MarkSize * (target.Width / landing.ActualWidth) / Mark;
            var shift = new Vector((target.Left + (target.Width / 2)) - (here.Left + (here.Width / 2)), (target.Top + (target.Height / 2)) - (here.Top + (here.Height / 2)));
            var carried = length * 0.7;
            Animate(_leave, _logo, Part(0, ScaleTransform.ScaleXProperty), (0, 1, null), (carried, scale, Carrying));
            Animate(_leave, _logo, Part(0, ScaleTransform.ScaleYProperty), (0, 1, null), (carried, scale, Carrying));
            Animate(_leave, _logo, Part(1, TranslateTransform.XProperty), (0, 0, null), (carried, shift.X, Carrying));
            Animate(_leave, _logo, Part(1, TranslateTransform.YProperty), (0, 0, null), (carried, shift.Y, Carrying));
            Animate(_leave, _veil, new PropertyPath(OpacityProperty), (0, 1, null), (length * 0.3, 1, null), (length, 0, null));
        }
        else
        {
            Animate(_leave, this, new PropertyPath(OpacityProperty), (0, 1, null), (length, 0, Leaving));
        }
        _leave.Duration = TimeSpan.FromSeconds(length);
    }

    private static PropertyPath Turn => new("(0).(1)", RenderTransformProperty, RotateTransform.AngleProperty);

    private static PropertyPath Widen => new("(0).(1)", RenderTransformProperty, ScaleTransform.ScaleXProperty);

    private static PropertyPath Heighten => new("(0).(1)", RenderTransformProperty, ScaleTransform.ScaleYProperty);

    /// <summary>Une propriété de la mise à l'échelle (0) ou du déplacement (1) du logotype.</summary>
    private static PropertyPath Part(int index, DependencyProperty property) =>
        new($"(0).(1)[{index}].(2)", RenderTransformProperty, TransformGroup.ChildrenProperty, property);

    private static KeyTime At(double seconds) => KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Math.Max(0, seconds)));

    /// <summary>Une valeur qui passe par ces points : le moment, la valeur, et la courbe qui y mène (rien : tout droit).</summary>
    private static void Animate(Storyboard story, DependencyObject target, PropertyPath path, params (double At, double Value, KeySpline? Curve)[] points)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        foreach (var (at, value, curve) in points)
        {
            animation.KeyFrames.Add(curve is null ? new LinearDoubleKeyFrame(value, At(at)) : new SplineDoubleKeyFrame(value, At(at), curve));
        }
        Add(story, animation, target, path);
    }

    private static void Add(Storyboard story, Timeline animation, DependencyObject target, PropertyPath path)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        story.Children.Add(animation);
    }

    private static KeySpline Frozen(KeySpline spline)
    {
        spline.Freeze();
        return spline;
    }

    /// <summary>
    /// L'icône, pièce par pièce : l'éventail derrière, la fiche et ce qui s'y écrit, puis, par-dessus, les fiches de l'éventail
    /// à la taille de la fiche, empilées, la première à partir sur le dessus.
    /// </summary>
    private Canvas Draw()
    {
        var canvas = new Canvas { Width = BrandDrawing.Size, Height = BrandDrawing.Size };
        var shapes = BrandDrawing.Shapes;
        var cards = shapes.Where(shape => shape.Part == BrandPart.Card).ToList();
        foreach (var card in cards)
        {
            var back = Place(canvas, card);
            back.Opacity = 0;
            back.RenderTransform = new RotateTransform(card.Angle * 1.6, card.Pivot.X - Canvas.GetLeft(back), card.Pivot.Y - Canvas.GetTop(back));
            _backs.Add(back);
            _angles.Add(card.Angle);
        }
        var sheet = shapes.FirstOrDefault(shape => shape.Part == BrandPart.Sheet);
        foreach (var shape in shapes.Where(shape => shape.Part != BrandPart.Card))
        {
            var piece = Place(canvas, shape);
            switch (shape.Part)
            {
                case BrandPart.Line:
                    piece.RenderTransform = new ScaleTransform(0, 1);
                    _written.Add(piece);
                    break;
                case BrandPart.Rail:
                    piece.RenderTransform = new ScaleTransform(0, 1);
                    _rail = piece;
                    break;
                case BrandPart.Band:
                    piece.RenderTransform = new ScaleTransform(0, 1, shape.Bounds.Left, 0);
                    _written.Add(piece);
                    break;
                case BrandPart.Dot:
                    piece.RenderTransform = new ScaleTransform(0, 0, piece.Width / 2, piece.Height / 2);
                    _dots.Add(piece);
                    break;
                default:
                    break;
            }
        }
        // Le trait de la frise s'écrit en dernier, quel que soit son rang dans le dessin.
        if (_rail is not null)
        {
            _written.Add(_rail);
        }
        if (sheet is not null)
        {
            foreach (var card in Enumerable.Reverse(cards))
            {
                var top = Place(canvas, sheet with { Fill = card.Fill });
                top.RenderTransform = new RotateTransform(0, card.Pivot.X - Canvas.GetLeft(top), card.Pivot.Y - Canvas.GetTop(top));
                _tops.Insert(0, top);
            }
        }
        return canvas;
    }

    /// <summary>Pose une pièce dans le dessin : en SVG le trait est à cheval sur le bord, ici il est dedans, d'où la demi-épaisseur ajoutée.</summary>
    private static Shape Place(Canvas canvas, BrandShape shape)
    {
        var half = shape.Stroke is null ? 0 : shape.StrokeWidth / 2;
        Shape piece = shape.Part switch
        {
            BrandPart.Band => new Path { Data = Geometry.Parse(shape.Outline!), StrokeLineJoin = PenLineJoin.Round },
            BrandPart.Dot => new Ellipse { Width = shape.Bounds.Width + (2 * half), Height = shape.Bounds.Height + (2 * half) },
            _ => new Rectangle { Width = shape.Bounds.Width + (2 * half), Height = shape.Bounds.Height + (2 * half), RadiusX = shape.Radius, RadiusY = shape.Radius },
        };
        piece.Fill = Brush(shape.Fill);
        if (shape.Stroke is { } stroke)
        {
            piece.Stroke = Brush(stroke);
            piece.StrokeThickness = shape.StrokeWidth;
        }
        if (piece is not Path)
        {
            Canvas.SetLeft(piece, shape.Bounds.Left - half);
            Canvas.SetTop(piece, shape.Bounds.Top - half);
        }
        else
        {
            Canvas.SetLeft(piece, 0);
            Canvas.SetTop(piece, 0);
        }
        canvas.Children.Add(piece);
        return piece;
    }

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
