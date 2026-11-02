using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;

namespace KyfRev.Presentation.Wpf.Editing;

/// <summary>
/// Le choix de la couleur du texte : celle du thème, une des couleurs proposées, ou une couleur écrite. Les pastilles
/// montrent la couleur telle qu'elle s'affichera dans ce thème.
/// </summary>
internal sealed partial class ColorChooser : Border
{
    private readonly List<(Border Dot, TokenColor Color)> _dots = [];
    private readonly TextBlock _problem = new() { Text = EditingText.ColorInvalid, Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap, MaxWidth = 196, Margin = new Thickness(0, 4, 0, 0) };

    public ColorChooser()
    {
        Padding = new Thickness(8);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, DesignKeys.Brush(ColorRole.Surface));
        SetResourceReference(BorderBrushProperty, DesignKeys.Brush(ColorRole.Divider));
        AutomationProperties.SetName(this, EditingText.TextColor);

        AutomaticButton = new Button { Content = EditingText.ColorAutomatic, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        AutomaticButton.SetResourceReference(StyleProperty, "Kyf.Button.Tool");
        AutomaticButton.Click += (_, _) => Chosen?.Invoke(null);

        Swatches = new UniformGrid { Columns = 6 };
        foreach (var choice in Palette.TextColors)
        {
            Swatches.Children.Add(BuildSwatch(choice));
        }

        var label = new TextBlock { Text = EditingText.ColorOther, Margin = new Thickness(0, 8, 0, 2) };
        label.SetResourceReference(StyleProperty, "Kyf.Text.Body");
        _problem.SetResourceReference(StyleProperty, "Kyf.Text.Body");
        OtherBox = new TextBox { Width = 100, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(OtherBox, EditingText.ColorOther);
        OtherBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyOther();
                e.Handled = true;
            }
        };
        ApplyButton = new Button { Content = EditingText.ColorApply, Margin = new Thickness(6, 0, 0, 0) };
        ApplyButton.SetResourceReference(StyleProperty, "Kyf.Button.Tool");
        ApplyButton.Click += (_, _) => ApplyOther();
        var other = new StackPanel { Orientation = Orientation.Horizontal };
        other.Children.Add(OtherBox);
        other.Children.Add(ApplyButton);

        var content = new StackPanel();
        content.Children.Add(AutomaticButton);
        content.Children.Add(Swatches);
        content.Children.Add(label);
        content.Children.Add(other);
        content.Children.Add(_problem);
        Child = content;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke();
                e.Handled = true;
            }
        };
    }

    /// <summary>Une couleur est choisie ; rien : celle du thème.</summary>
    public event Action<string?>? Chosen;

    /// <summary>Le choix est abandonné.</summary>
    public event Action? Cancelled;

    /// <summary>Rendre la couleur du thème.</summary>
    internal Button AutomaticButton { get; }

    /// <summary>Les couleurs proposées.</summary>
    internal UniformGrid Swatches { get; }

    /// <summary>La couleur écrite.</summary>
    internal TextBox OtherBox { get; }

    /// <summary>Appliquer la couleur écrite.</summary>
    internal Button ApplyButton { get; }

    /// <summary>Le problème de la couleur écrite est affiché.</summary>
    internal bool ShowsProblem => _problem.Visibility == Visibility.Visible;

    /// <summary>Remet le choix à neuf, pour le thème du moment.</summary>
    public void Prepare()
    {
        OtherBox.Clear();
        _problem.Visibility = Visibility.Collapsed;
        var ground = TryFindResource(DesignKeys.Color(ColorRole.Background)) is Color color ? new TokenColor(color.A, color.R, color.G, color.B) : default;
        foreach (var (dot, written) in _dots)
        {
            var brush = new SolidColorBrush(ground.IsOpaque ? written.ReadableOn(ground).ToColor() : written.ToColor());
            brush.Freeze();
            dot.Background = brush;
        }
    }

    private Button BuildSwatch(NamedColor choice)
    {
        var dot = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1) };
        dot.SetResourceReference(BorderBrushProperty, DesignKeys.Brush(ColorRole.Divider));
        _dots.Add((dot, choice.Color));
        var button = new Button { Content = dot, ToolTip = choice.Name };
        button.SetResourceReference(StyleProperty, "Kyf.Button.Tool");
        AutomationProperties.SetName(button, choice.Name);
        button.Click += (_, _) => Chosen?.Invoke(choice.Color.ToString());
        return button;
    }

    private void ApplyOther()
    {
        var written = OtherBox.Text.Trim();
        if (!WrittenColor().IsMatch(written))
        {
            _problem.Visibility = Visibility.Visible;
            return;
        }
        Chosen?.Invoke(written.StartsWith('#') ? written : $"#{written}");
    }

    [GeneratedRegex("^#?[0-9a-fA-F]{6}$")]
    private static partial Regex WrittenColor();
}
