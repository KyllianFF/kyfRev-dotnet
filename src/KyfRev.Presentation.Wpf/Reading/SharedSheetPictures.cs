using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Rendering;
using KyfRev.Rendering.Diagrams;
using WpfMath;
using WpfMath.Parsers;
using XamlMath.Exceptions;

namespace KyfRev.Presentation.Wpf.Reading;

/// <summary>
/// Dessine ce qu'une fiche partagée ne dit pas en texte, comme la lecture : les formules, les schémas et les graphiques, toujours
/// aux couleurs du thème clair, celles de la page. Ce qui ne se dessine pas reste lisible en texte.
/// </summary>
public sealed class SharedSheetPictures
{
    /// <summary>Les formules se dessinent deux fois plus fines que la page : elles restent nettes, imprimées ou agrandies.</summary>
    private const double Density = 2;

    /// <summary>La taille du texte de la page.</summary>
    private const double TextSize = 17;

    private const double BlockScale = 1.2;

    private const string SystemFont = "Arial";

    private readonly IMermaidEngine? _engine;
    private readonly Func<INetworkSchemas?> _schemas;
    private FrameworkElement? _light;

    /// <summary>Les dessins viennent de <paramref name="engine"/>, s'il y en a un ; les schémas réseau, de <paramref name="schemas"/>.</summary>
    public SharedSheetPictures(IMermaidEngine? engine, Func<INetworkSchemas?>? schemas = null)
    {
        _engine = engine;
        _schemas = schemas ?? (() => null);
    }

    private FrameworkElement Light => _light ??= LightTheme();

    /// <summary>
    /// Prépare ce qui sort du texte, sur le fil de l'interface et peu à peu : les formules, et l'attente de la page cachée pour les
    /// dessins. Sans <paramref name="drawing"/>, seuls les titres des schémas sont lus. Rien si la demande est dépassée.
    /// </summary>
    internal async Task<SharedDrawings?> DrawAsync(RenderElement root, bool drawing, CancellationToken cancellation)
    {
        var pictures = new Dictionary<RenderElement, string>();
        var titles = new Dictionary<RenderElement, string>();
        foreach (var element in Elements(root))
        {
            if (cancellation.IsCancellationRequested)
            {
                return null;
            }
            if (element.Formula is { } formula)
            {
                if (drawing && Formula(formula, element.IsFormulaBlock) is { } picture)
                {
                    pictures[element] = picture;
                    await Dispatcher.Yield(DispatcherPriority.Background);
                }
            }
            else if (element.Language == MermaidTemplates.Language && drawing)
            {
                await DrawMermaidAsync(element, pictures).ConfigureAwait(true);
            }
            else if (element.Language == NetworkBlock.Language)
            {
                await DrawNetworkAsync(element, drawing, pictures, titles).ConfigureAwait(true);
            }
        }
        return cancellation.IsCancellationRequested ? null : new SharedDrawings { Pictures = pictures, Titles = titles };
    }

    /// <summary>Une formule en image, avec sa source pour nom ; rien si elle ne se dessine pas, comme en lecture.</summary>
    internal static string? Formula(string source, bool block)
    {
        var size = TextSize * (block ? BlockScale : 1) * Density;
        byte[] png;
        try
        {
            png = WpfTeXFormulaParser.Instance.Parse(DocumentBuilder.Drawable(source)).RenderToPng(size, 0, 0, SystemFont);
        }
        catch (Exception problem) when (problem is TexException or ArgumentException)
        {
            return null;
        }
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)) / Density;
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)) / Density;
        return string.Create(CultureInfo.InvariantCulture, $"<img src=\"{DataAddress(png)}\" alt=\"{SharedSheet.Escape(source)}\" style=\"width:{width}px;height:{height}px\">");
    }

    private static IEnumerable<RenderElement> Elements(RenderElement root)
    {
        var pending = new Stack<RenderElement>([root]);
        while (pending.TryPop(out var element))
        {
            yield return element;
            foreach (var child in element.Children.OfType<RenderElement>().Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static string CodeOf(RenderElement code) => code.Children.OfType<RenderText>().FirstOrDefault()?.Value.TrimEnd('\n') ?? string.Empty;

    private static string DataAddress(ReadOnlySpan<byte> png) => $"data:image/png;base64,{Convert.ToBase64String(png)}";

    private static string Picture(MermaidPicture picture) =>
        string.Create(CultureInfo.InvariantCulture, $"<img src=\"{DataAddress(picture.Png.Span)}\" alt=\"{SharedSheet.Escape(SharingText.DiagramName)}\" style=\"width:{picture.Width}px\">");

    /// <summary>Le titre écrit dans un schéma réseau ; rien s'il n'en a pas.</summary>
    private static string? TitleOf(string schema)
    {
        try
        {
            using var document = JsonDocument.Parse(schema);
            return document.RootElement.TryGetProperty("titre", out var title) && title.GetString() is { Length: > 0 } text ? text : null;
        }
        catch (Exception problem) when (problem is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Un élément qui n'est dans aucune page, mais qui en a les couleurs : celles du thème clair.</summary>
    private static FrameworkElement LightTheme()
    {
        var resources = new ResourceDictionary();
        foreach (var (role, color) in ThemeColors.For(ThemeKind.Light))
        {
            var brush = new SolidColorBrush(color.ToColor());
            brush.Freeze();
            resources[DesignKeys.Brush(role)] = brush;
        }
        return new FrameworkElement { Resources = resources };
    }

    private async Task DrawMermaidAsync(RenderElement code, Dictionary<RenderElement, string> pictures)
    {
        if (_engine is { } engine && await engine.DrawAsync(CodeOf(code), MermaidEngine.ThemeOf(Light)).ConfigureAwait(true) is { } picture)
        {
            pictures[code] = Picture(picture);
        }
    }

    private async Task DrawNetworkAsync(RenderElement code, bool drawing, Dictionary<RenderElement, string> pictures, Dictionary<RenderElement, string> titles)
    {
        if (_schemas() is not { IsActive: true } schemas || await NetworkBlock.TextOfAsync(schemas, CodeOf(code)).ConfigureAwait(true) is not { } schema)
        {
            return;
        }
        if (TitleOf(schema) is { } title)
        {
            titles[code] = title;
        }
        if (drawing && _engine is { } engine && await engine.DrawNetworkAsync(schema, SchemaIsland.ThemeOf(Light)).ConfigureAwait(true) is { } picture)
        {
            pictures[code] = Picture(picture);
        }
    }
}
