using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'icône et les images de l'installateur, générées depuis assets/icon/kyfrev.svg.</summary>
[TestClass]
public sealed class IconTests
{
    private static readonly int[] IconSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    private static readonly int[] SmallSizes = [58, 77, 97, 116, 124, 143, 159];

    private static readonly (int Width, int Height)[] PanelSizes = [(202, 386), (269, 515), (336, 643), (403, 772), (430, 824), (498, 953), (534, 1022)];

    private static string InRepository(params string[] parts) => Path.Combine([Repository.Root, .. parts]);

    [STATestMethod]
    public void The_icon_has_every_size_and_no_background()
    {
        var decoder = new IconBitmapDecoder(new Uri(InRepository("assets", "icon", "kyfrev.ico")), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        CollectionAssert.AreEquivalent(IconSizes, decoder.Frames.Select(frame => frame.PixelWidth).ToList());
        foreach (var frame in decoder.Frames)
        {
            Assert.AreEqual(frame.PixelWidth, frame.PixelHeight);
            var last = frame.PixelWidth - 1;
            Assert.AreEqual(0, Alpha(frame, 0, 0), $"{frame.PixelWidth} px : pas de fond");
            Assert.AreEqual(0, Alpha(frame, last, 0), $"{frame.PixelWidth} px : pas de fond");
            Assert.AreEqual(255, Alpha(frame, frame.PixelWidth / 2, frame.PixelHeight / 2), $"{frame.PixelWidth} px : la fiche est opaque");
        }
    }

    [STATestMethod]
    public void The_installer_has_its_images_for_every_display_scale()
    {
        foreach (var size in SmallSizes)
        {
            var image = Png("petit", size);
            Assert.AreEqual((size, size), (image.PixelWidth, image.PixelHeight));
            Assert.AreEqual(0, Alpha(image, 0, 0), "sans fond : l'assistant peut être clair ou sombre");
        }
        foreach (var (width, height) in PanelSizes)
        {
            var image = Png("assistant", width);
            Assert.AreEqual((width, height), (image.PixelWidth, image.PixelHeight));
            Assert.AreEqual(255, Alpha(image, 0, 0), "le panneau a son fond");
        }
    }

    [TestMethod]
    public void The_application_and_the_installer_use_them()
    {
        var project = File.ReadAllText(InRepository("src", "KyfRev.App", "KyfRev.App.csproj"));
        Assert.Contains(@"<ApplicationIcon>..\..\assets\icon\kyfrev.ico</ApplicationIcon>", project);
        var script = File.ReadAllText(InRepository("installer", "kyfRev.iss"));
        Assert.Contains(@"SetupIconFile=..\assets\icon\kyfrev.ico", script);
        Assert.Contains(@"WizardImageFile=images\assistant-*.png", script);
        Assert.Contains(@"WizardSmallImageFile=images\petit-*.png", script);
        Assert.Contains("<title>", File.ReadAllText(InRepository("assets", "icon", "kyfrev.svg")));
    }

    private static BitmapFrame Png(string name, int size) =>
        new PngBitmapDecoder(new Uri(InRepository("installer", "images", $"{name}-{size}.png")), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];

    private static byte Alpha(BitmapSource image, int x, int y)
    {
        var pixel = new byte[4];
        new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3];
    }
}
