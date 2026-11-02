using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Un schéma ou un graphique : son image, dessinée par la page cachée. Tant qu'elle n'est pas là, ou si le code ne se dessine
/// pas, c'est son code qui se lit : rien n'est perdu. Elle se redessine quand le thème change.
/// </summary>
internal sealed class MermaidView : Border
{
    /// <summary>Le code Mermaid.</summary>
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code), typeof(string), typeof(MermaidView), new PropertyMetadata(null, OnCodeChanged));

    private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
        "ThemeProbe", typeof(Brush), typeof(MermaidView), new PropertyMetadata(null, OnCodeChanged));

    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _note = new() { Text = ReadingText.DiagramAsText, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _source = new() { TextWrapping = TextWrapping.Wrap, Padding = new Thickness(12, 8, 12, 8) };
    private readonly StackPanel _fallback = new();
    private readonly DispatcherTimer _timer;
    private int _request;

    public MermaidView()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TypingDelay };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _ = DrawAsync();
        };
        _note.SetResourceReference(TextBlock.ForegroundProperty, DesignKeys.Brush(ColorRole.TextSecondary));
        _source.SetResourceReference(TextBlock.ForegroundProperty, DesignKeys.Brush(ColorRole.Text));
        _source.SetResourceReference(TextBlock.BackgroundProperty, DesignKeys.Brush(ColorRole.SurfaceAlt));
        _source.SetResourceReference(TextBlock.FontFamilyProperty, "Kyf.Font.Code");
        _fallback.Children.Add(_note);
        _fallback.Children.Add(_source);
        Child = _fallback;
        Loaded += (_, _) => _ = DrawAsync();
        SetResourceReference(ThemeProbeProperty, DesignKeys.Brush(ColorRole.Text));
    }

    /// <inheritdoc cref="CodeProperty"/>
    public string? Code
    {
        get => (string?)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>Le dessin est affiché.</summary>
    internal bool ShowsPicture => Child == _image;

    /// <summary>Le code est affiché, faute de dessin.</summary>
    internal bool ShowsCode => Child == _fallback;

    private static void OnCodeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (MermaidView)sender;
        view._source.Text = view.Code ?? string.Empty;
        if (view.IsLoaded)
        {
            view._timer.Stop();
            view._timer.Start();
        }
    }

    private async Task DrawAsync()
    {
        var request = ++_request;
        if (string.IsNullOrWhiteSpace(Code) || TryFindResource(MermaidEngine.ResourceKey) is not IMermaidEngine engine)
        {
            Child = _fallback;
            return;
        }
        var picture = await engine.DrawAsync(Code, MermaidEngine.ThemeOf(this)).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (picture is null)
        {
            Child = _fallback;
            return;
        }
        using var stream = new MemoryStream(picture.Png.ToArray());
        _image.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        _image.MaxWidth = picture.Width;
        AutomationProperties.SetName(_image, Code);
        Child = _image;
    }
}
