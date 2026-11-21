using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KyfRev.DesignSystem.Controls;

/// <summary>
/// Les nombres et les jauges (rangée 19C des maquettes) : une jauge avance au lieu de sauter à sa nouvelle longueur, un
/// compteur qui change fait arriver sa nouvelle valeur par le bas, en fondu.
/// </summary>
public static partial class Transitions
{
    /// <summary>De combien la nouvelle valeur d'un compteur monte en arrivant.</summary>
    public const double RollRise = 6;

    /// <summary>
    /// Sur une jauge (<see cref="ProgressBar"/>) : quand sa valeur change, sa barre glisse de l'ancienne longueur à la
    /// nouvelle. Posée par le style des jauges.
    /// </summary>
    public static readonly DependencyProperty AdvancesProperty =
        DependencyProperty.RegisterAttached("Advances", typeof(bool), typeof(Transitions), new PropertyMetadata(false, OnAdvances));

    /// <summary>Lit <see cref="AdvancesProperty"/>.</summary>
    public static bool GetAdvances(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(AdvancesProperty);

    /// <summary>Écrit <see cref="AdvancesProperty"/>.</summary>
    public static void SetAdvances(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(AdvancesProperty, value);

    private static void OnAdvances(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not RangeBase gauge)
        {
            return;
        }
        gauge.ValueChanged -= OnGaugeChanged;
        if ((bool)e.NewValue)
        {
            gauge.ValueChanged += OnGaugeChanged;
        }
    }

    private static void OnGaugeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Advance((RangeBase)sender, e.OldValue, e.NewValue);

    /// <summary>
    /// La jauge vient de passer de <paramref name="from"/> à <paramref name="to"/> : sa barre a déjà sa nouvelle longueur, et
    /// repart de l'ancienne. Une jauge qui repart de zéro, ou qui y revient, ne glisse pas.
    /// </summary>
    /// <returns>Vrai si la barre glisse.</returns>
    internal static bool Advance(RangeBase gauge, double from, double to)
    {
        if (gauge.Template?.FindName("PART_Indicator", gauge) is not FrameworkElement indicator)
        {
            return false;
        }
        var filled = to - gauge.Minimum;
        var was = from - gauge.Minimum;
        if (filled <= 0 || was <= 0 || !gauge.IsVisible || !Moves(gauge, DesignKeys.NormalDuration, out var duration, MotionKinds.Feedback))
        {
            indicator.ClearValue(UIElement.RenderTransformProperty);
            return false;
        }
        var stretch = new ScaleTransform(1, 1);
        indicator.RenderTransformOrigin = new Point(0, 0.5);
        indicator.RenderTransform = stretch;
        stretch.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(was / filled, 1, duration) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
        return true;
    }

    /// <summary>
    /// Sur un texte qui montre un compte (« 17 », « 3 / 14 ») : à la place d'une liaison sur <see cref="TextBlock.Text"/>.
    /// Quand la valeur change, l'ancienne sort par le haut et la nouvelle arrive par le bas, en fondu.
    /// </summary>
    public static readonly DependencyProperty RollingTextProperty =
        DependencyProperty.RegisterAttached("RollingText", typeof(string), typeof(Transitions), new PropertyMetadata(null, OnRollingText));

    /// <summary>Lit <see cref="RollingTextProperty"/>.</summary>
    public static string? GetRollingText(DependencyObject element) => (string?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(RollingTextProperty);

    /// <summary>Écrit <see cref="RollingTextProperty"/>.</summary>
    public static void SetRollingText(DependencyObject element, string? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(RollingTextProperty, value);

    private static void OnRollingText(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TextBlock text)
        {
            return;
        }
        var before = text.Text;
        var now = e.NewValue as string ?? string.Empty;
        text.Text = now;
        // Un compte qui paraît ou disparaît n'est pas un compte qui change.
        if (before.Length > 0 && now.Length > 0 && before != now && text.IsLoaded && text.IsVisible)
        {
            Roll(text, before);
        }
    }

    /// <summary>
    /// Le compte de <paramref name="text"/> vient de changer : la nouvelle valeur arrive par le bas, et l'image de
    /// <paramref name="former"/>, posée là où elle était, sort par le haut en s'effaçant.
    /// </summary>
    /// <returns>Vrai si l'ancienne valeur a son image ; faux si seule la nouvelle bouge, ou rien.</returns>
    internal static bool Roll(TextBlock text, string former)
    {
        // L'ancienne valeur se dessine où la mise en page l'avait posée : avant que la nouvelle ne déplace le texte.
        var place = VisualTreeHelper.GetOffset(text);
        var width = text.ActualWidth;
        if (!Enter(text, DesignKeys.FastDuration, rise: RollRise, kind: MotionKinds.Feedback)
            || !Moves(text, DesignKeys.FastDuration, out var duration, MotionKinds.Feedback)
            || VisualTreeHelper.GetParent(text) is not UIElement parent
            || AdornerLayer.GetAdornerLayer(parent) is not { } layer)
        {
            return false;
        }
        var drawn = new FormattedText(
            former,
            System.Globalization.CultureInfo.CurrentUICulture,
            text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize,
            text.Foreground,
            VisualTreeHelper.GetDpi(text).PixelsPerDip);
        var free = Math.Max(0, width - text.Padding.Left - text.Padding.Right - drawn.Width);
        var aside = text.TextAlignment switch
        {
            TextAlignment.Right => free,
            TextAlignment.Center => free / 2,
            _ => 0,
        };
        var ghost = new FormerTextAdorner(parent, drawn, new Point(place.X + text.Padding.Left + aside, place.Y + text.Padding.Top));
        layer.Add(ghost);
        ghost.BeginAnimation(FormerTextAdorner.LiftProperty, new DoubleAnimation(0, RollRise, duration) { EasingFunction = Leave, FillBehavior = FillBehavior.HoldEnd });
        Exit(ghost, () => layer.Remove(ghost), DesignKeys.FastDuration, kind: MotionKinds.Feedback);
        return true;
    }

    /// <summary>
    /// L'ancienne valeur d'un compteur, dessinée sur le parent du texte, à la place qu'elle avait, le temps de sortir par le
    /// haut. Elle monte par son propre dessin : la couche des ornements tient déjà sa transformation.
    /// </summary>
    internal sealed class FormerTextAdorner : Adorner
    {
        /// <summary>De combien l'ancienne valeur est montée.</summary>
        public static readonly DependencyProperty LiftProperty =
            DependencyProperty.Register(nameof(Lift), typeof(double), typeof(FormerTextAdorner), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly FormattedText _text;
        private readonly Point _place;

        public FormerTextAdorner(UIElement parent, FormattedText text, Point place)
            : base(parent)
        {
            _text = text;
            _place = place;
            IsHitTestVisible = false;
        }

        /// <inheritdoc cref="LiftProperty"/>
        public double Lift => (double)GetValue(LiftProperty);

        protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawText(_text, new Point(_place.X, _place.Y - Lift));
    }
}
