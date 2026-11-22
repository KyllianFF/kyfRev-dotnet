using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Quand la valeur change, la nouvelle arrive par le bas, en fondu.
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
        if (before.Length > 0 && now.Length > 0 && text.IsLoaded && text.IsVisible)
        {
            Enter(text, DesignKeys.FastDuration, rise: RollRise, kind: MotionKinds.Feedback);
        }
    }
}
