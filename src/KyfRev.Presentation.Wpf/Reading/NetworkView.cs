using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Schemas;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Un bloc <c>```reseau</c> d'une fiche : le dessin du schéma qu'il cite, et un double clic ouvre son éditeur. Sans le module, le bloc reste
/// intact dans la fiche et la vue le dit : le schéma n'est pas perdu.
/// </summary>
internal sealed class NetworkView : Border
{
    /// <summary>Le contenu du bloc.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(NetworkView), new PropertyMetadata(null, (sender, _) => ((NetworkView)sender).Redraw()));

    private readonly Image _image = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap };
    private int _request;

    public NetworkView()
    {
        _notice.SetResourceReference(TextBlock.ForegroundProperty, "Kyf.Brush.TextSecondary");
        Child = _notice;
        Loaded += (_, _) => Redraw();
    }

    /// <inheritdoc cref="SourceProperty"/>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Le dessin est affiché.</summary>
    internal bool ShowsPicture => Child == _image;

    /// <summary>Ce qui s'affiche à la place du dessin.</summary>
    internal string Notice => _notice.Text;

    /// <summary>Le dessin s'obtient une fois tout en place, et se refait à chaque retour sur la vue : le schéma a pu changer.</summary>
    internal Task Drawing { get; private set; } = Task.CompletedTask;

    /// <summary>Un double clic ouvre l'éditeur du schéma cité.</summary>
    internal void OpenEditor()
    {
        if (NetworkBlock.ReferenceOf(Source) is { } id && TryFindResource(NetworkBlock.ResourceKey) is INetworkSchemas { IsActive: true } schemas)
        {
            schemas.Open(id);
        }
    }

    /// <inheritdoc/>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        ArgumentNullException.ThrowIfNull(e);
        if (e.ClickCount == 2)
        {
            OpenEditor();
            e.Handled = true;
        }
    }

    private void Redraw()
    {
        if (IsLoaded)
        {
            Drawing = DrawAsync();
        }
    }

    private void Show(string notice)
    {
        _notice.Text = notice;
        AutomationProperties.SetName(this, notice);
        Child = _notice;
    }

    private async Task DrawAsync()
    {
        var request = ++_request;
        if (TryFindResource(NetworkBlock.ResourceKey) is not INetworkSchemas { IsActive: true } schemas)
        {
            Show(SchemaText.ModuleOff);
            return;
        }
        if (string.IsNullOrWhiteSpace(Source))
        {
            Show(SchemaText.EmptyBlock);
            return;
        }
        var text = await NetworkBlock.TextOfAsync(schemas, Source).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (text is null || TryFindResource(MermaidEngine.ResourceKey) is not IMermaidEngine engine)
        {
            Show(SchemaText.Unavailable);
            return;
        }
        var picture = await engine.DrawNetworkAsync(text, SchemaIsland.ThemeOf(this)).ConfigureAwait(true);
        if (request != _request)
        {
            return;
        }
        if (picture is null)
        {
            Show(SchemaText.Unavailable);
            return;
        }
        using var stream = new MemoryStream(picture.Png.ToArray());
        _image.Source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        _image.MaxWidth = picture.Width;
        var editable = NetworkBlock.ReferenceOf(Source) is not null;
        Cursor = editable ? Cursors.Hand : null;
        ToolTip = editable ? SchemaText.EditHint : SchemaText.InlineHint;
        AutomationProperties.SetName(this, SchemaText.PictureName);
        Child = _image;
    }
}
