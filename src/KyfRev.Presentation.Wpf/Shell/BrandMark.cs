using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KyfRev.Presentation.Wpf.Shell;

/// <summary>
/// L'icône de kyfRev, montrée dans l'application : à l'accueil, au déverrouillage, dans « À propos ». Elle prend, dans le
/// fichier .ico du programme, l'image la plus proche de sa taille à l'écran : nette à toutes les tailles et à toutes les
/// échelles d'affichage.
/// </summary>
internal sealed class BrandMark : Image
{
    /// <summary>Le nom du fichier .ico dans les ressources du programme.</summary>
    internal const string Resource = "kyfrev-icone.ico";

    private static readonly Lazy<List<BitmapFrame>> Frames = new(Read);

    public BrandMark()
    {
        Width = Height = 48;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        AutomationProperties.SetName(this, ShellText.AppName);
        Loaded += (_, _) => Show();
        SizeChanged += (_, _) => Show();
    }

    /// <summary>
    /// L'image du fichier .ico pour cette taille à l'écran, en pixels : la plus petite qui la couvre, sinon la plus grande.
    /// Rien si le programme n'a pas son icône.
    /// </summary>
    internal static ImageSource? FrameFor(double pixels)
    {
        var frames = Frames.Value;
        return frames.Where(frame => frame.PixelWidth >= pixels).OrderBy(frame => frame.PixelWidth).FirstOrDefault()
            ?? frames.OrderByDescending(frame => frame.PixelWidth).FirstOrDefault();
    }

    private void Show() => Source = FrameFor(Math.Max(Width, ActualWidth) * VisualTreeHelper.GetDpi(this).DpiScaleX);

    private static List<BitmapFrame> Read()
    {
        using var stream = typeof(BrandMark).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return [];
        }
        var frames = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.ToList();
        // Lues une fois pour toutes les fenêtres : figées, elles se partagent d'un fil à l'autre.
        foreach (var frame in frames)
        {
            frame.Freeze();
        }
        return frames;
    }
}
