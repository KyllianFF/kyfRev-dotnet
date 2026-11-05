using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Extensions.Host;

namespace KyfRev.Presentation.Wpf.Extensions;

/// <summary>
/// Le dessin d'un bloc d'extension pour l'éditeur, qui n'affiche que des images : ce qu'elle rend, fait en éléments natifs comme partout,
/// puis photographié sur fond transparent, à l'échelle de l'écran. Ni HTML ni script de l'extension n'entrent dans la page de l'éditeur.
/// </summary>
internal static class ExtensionPicture
{
    private const double Width = 640;
    private const double Padding = 12;
    private const int MaxHeight = 4000;

    /// <summary>L'image du bloc, en adresse <c>data:</c> ; rien si l'extension ne répond pas.</summary>
    /// <param name="blocks">Les blocs des extensions.</param>
    /// <param name="language">Le langage du bloc.</param>
    /// <param name="code">Son texte.</param>
    /// <param name="owner">L'élément qui demande : ses ressources (thème, styles) et son échelle d'écran.</param>
    public static async Task<string?> DrawAsync(ExtensionBlocks blocks, string language, string code, FrameworkElement owner)
    {
        var answer = await blocks.RenderAsync(language, code).ConfigureAwait(true);
        return answer.Succeeded ? Photograph(answer.Content ?? ExtensionContent.Empty, owner) : null;
    }

    /// <summary>Le contenu, mis en page hors de l'écran avec les ressources de l'élément, puis en PNG.</summary>
    internal static string Photograph(ExtensionContent content, FrameworkElement owner)
    {
        var frame = new Border { Width = Width, Padding = new Thickness(Padding), Child = new ExtensionContentBuilder(_ => { }).Build(content) };
        foreach (var dictionary in new[] { System.Windows.Application.Current?.Resources, Window.GetWindow(owner)?.Resources })
        {
            if (dictionary is not null)
            {
                frame.Resources.MergedDictionaries.Add(dictionary);
            }
        }
        frame.Measure(new Size(Width, double.PositiveInfinity));
        frame.Arrange(new Rect(new Point(), new Size(Width, Math.Min(frame.DesiredSize.Height, MaxHeight))));
        frame.UpdateLayout();
        var scale = VisualTreeHelper.GetDpi(owner);
        var picture = new RenderTargetBitmap(
            (int)Math.Ceiling(Width * scale.DpiScaleX),
            Math.Max(1, (int)Math.Ceiling(frame.ActualHeight * scale.DpiScaleY)),
            scale.PixelsPerInchX,
            scale.PixelsPerInchY,
            PixelFormats.Pbgra32);
        picture.Render(frame);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(picture));
        using var stream = new MemoryStream();
        png.Save(stream);
        return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
    }
}
