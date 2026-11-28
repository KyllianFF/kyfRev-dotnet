using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Le chevron d'un groupe ou d'un dossier qu'on replie (rangée 19, « Menu et volets ») : il tourne d'un quart de tour au lieu
/// de changer de glyphe.
/// </summary>
public static partial class Transitions
{
    /// <summary>L'angle d'un chevron replié : le glyphe pointe vers le bas, replié il pointe vers la droite.</summary>
    public const double FoldedAngle = -90;

    /// <summary>Au-delà de ce nombre de chevrons retenus, la mémoire repart de zéro : seul le dernier geste compte.</summary>
    internal const int MostTurnsRemembered = 512;

    /// <summary>
    /// Sur un chevron dont le glyphe pointe vers le bas (<see cref="Icons.ChevronDown"/>) : vrai, il pointe vers le bas (ouvert) ;
    /// faux, il pointe vers la droite (replié). Une fois à l'écran, il tourne d'une position à l'autre, même quand sa liste le
    /// refait en se repliant : <see cref="TurnKeyProperty"/> dit alors lequel il était.
    /// </summary>
    public static readonly DependencyProperty OpensProperty =
        DependencyProperty.RegisterAttached("Opens", typeof(bool?), typeof(Transitions), new PropertyMetadata(null, OnOpens));

    /// <summary>
    /// Ce qui distingue le chevron d'un autre d'une liste refaite à l'autre (le groupe, le chemin du dossier) : un chevron neuf
    /// qui a la même clé qu'un ancien tourne depuis la position de l'ancien.
    /// </summary>
    public static readonly DependencyProperty TurnKeyProperty =
        DependencyProperty.RegisterAttached("TurnKey", typeof(object), typeof(Transitions), new PropertyMetadata(null));

    // Par fil d'interface : chaque fenêtre (et chaque essai) a ses chevrons.
    [ThreadStatic]
    private static Dictionary<object, bool>? _turned;

    private static Dictionary<object, bool> Turned => _turned ??= [];

    /// <summary>Lit <see cref="OpensProperty"/>.</summary>
    public static bool? GetOpens(DependencyObject element) => (bool?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(OpensProperty);

    /// <summary>Écrit <see cref="OpensProperty"/>.</summary>
    public static void SetOpens(DependencyObject element, bool? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(OpensProperty, value);

    /// <summary>Lit <see cref="TurnKeyProperty"/>.</summary>
    public static object? GetTurnKey(DependencyObject element) => (element ?? throw new ArgumentNullException(nameof(element))).GetValue(TurnKeyProperty);

    /// <summary>Écrit <see cref="TurnKeyProperty"/>.</summary>
    public static void SetTurnKey(DependencyObject element, object? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(TurnKeyProperty, value);

    /// <summary>L'angle d'un chevron ouvert ou replié.</summary>
    internal static double AngleOf(bool open) => open ? 0 : FoldedAngle;

    private static void OnOpens(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement chevron || e.NewValue is not bool open)
        {
            return;
        }
        var rotation = RotationOf(chevron);
        rotation.Angle = AngleOf(open);
        chevron.Loaded -= OnChevronLoaded;
        if (chevron.IsLoaded)
        {
            // Le même chevron, resté à l'écran : il tourne depuis là où il était.
            if (e.OldValue is bool was && was != open)
            {
                Turn(chevron, rotation, was, open);
            }
            Remember(chevron, open);
        }
        else
        {
            // Un chevron neuf (sa liste vient d'être refaite) : à son arrivée, il tourne depuis la position de l'ancien.
            chevron.Loaded += OnChevronLoaded;
        }
    }

    private static void OnChevronLoaded(object sender, RoutedEventArgs e)
    {
        var chevron = (FrameworkElement)sender;
        chevron.Loaded -= OnChevronLoaded;
        if (GetOpens(chevron) is not { } open)
        {
            return;
        }
        if (GetTurnKey(chevron) is { } key && Turned.TryGetValue(key, out var was) && was != open)
        {
            Turn(chevron, RotationOf(chevron), was, open);
        }
        Remember(chevron, open);
    }

    private static void Remember(FrameworkElement chevron, bool open)
    {
        if (GetTurnKey(chevron) is not { } key)
        {
            return;
        }
        if (Turned.Count >= MostTurnsRemembered && !Turned.ContainsKey(key))
        {
            Turned.Clear();
        }
        Turned[key] = open;
    }

    /// <summary>Tourne le chevron d'une position à l'autre ; l'angle d'arrivée est déjà posé, l'animation part de l'ancien.</summary>
    private static void Turn(FrameworkElement chevron, RotateTransform rotation, bool from, bool to)
    {
        if (!Moves(chevron, DesignKeys.NormalDuration, out var duration, MotionKinds.Panels))
        {
            return;
        }
        var turn = new DoubleAnimation(AngleOf(from), AngleOf(to), duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop };
        chevron.SetValue(TurningProperty, turn);
        // Arrivé, il ne garde rien de l'animation ; un tour qui en a remplacé un autre reste au suivant.
        turn.Completed += (_, _) =>
        {
            if (ReferenceEquals(chevron.GetValue(TurningProperty), turn))
            {
                chevron.ClearValue(TurningProperty);
                rotation.BeginAnimation(RotateTransform.AngleProperty, null);
            }
        };
        rotation.BeginAnimation(RotateTransform.AngleProperty, turn);
    }

    // Le tour en cours du chevron.
    private static readonly DependencyProperty TurningProperty =
        DependencyProperty.RegisterAttached("Turning", typeof(DoubleAnimation), typeof(Transitions), new PropertyMetadata(null));

    /// <summary>La rotation du chevron, autour de son milieu : la sienne s'il en a une, sinon une nouvelle.</summary>
    private static RotateTransform RotationOf(FrameworkElement chevron)
    {
        if (chevron.RenderTransform is RotateTransform { IsFrozen: false } own)
        {
            return own;
        }
        var rotation = new RotateTransform();
        chevron.RenderTransformOrigin = new Point(0.5, 0.5);
        chevron.RenderTransform = rotation;
        return rotation;
    }
}
