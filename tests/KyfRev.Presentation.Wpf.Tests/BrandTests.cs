using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La marque dans l'application : l'icône de kyfRev à toutes les tailles, et son logotype en deux tons.</summary>
[TestClass]
public sealed class BrandTests
{
    [STATestMethod]
    public void The_icon_takes_the_picture_closest_to_its_size_on_screen()
    {
        var small = (BitmapSource)BrandMark.FrameFor(16)!;
        var medium = (BitmapSource)BrandMark.FrameFor(48)!;
        var large = (BitmapSource)BrandMark.FrameFor(10_000)!;

        Assert.IsGreaterThanOrEqualTo(16, small.PixelWidth, "jamais une image plus petite que la place à remplir");
        Assert.IsGreaterThanOrEqualTo(48, medium.PixelWidth);
        Assert.IsGreaterThanOrEqualTo(small.PixelWidth, medium.PixelWidth);
        Assert.IsGreaterThanOrEqualTo(medium.PixelWidth, large.PixelWidth, "au-delà de la plus grande, la plus grande");
        Assert.IsGreaterThanOrEqualTo(256, large.PixelWidth);

        // Chaque fenêtre décode la sienne : une image de WPF ne se partage pas d'un fil à l'autre.
        Assert.AreNotSame(BrandMark.FrameFor(48), medium);
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void The_logotype_writes_the_name_in_two_tones_and_follows_the_theme(bool dark)
    {
        using var ui = new UiThread();
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var logotype = new Logotype { MarkSize = 96 };
        var host = new Border { Padding = new Thickness(24), Child = logotype };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.AreEqual("kyfRev", logotype.Wordmark);
            var mark = logotype.Children.OfType<BrandMark>().Single();
            var name = logotype.Children.OfType<TextBlock>().Single();
            Assert.AreEqual((96d, 96d), (mark.ActualWidth, mark.ActualHeight));
            Assert.IsNotNull(mark.Source, "l'icône est lue une fois à l'écran");
            Assert.AreEqual(ShellText.AppName, AutomationProperties.GetName(mark));
            Assert.AreEqual(77d, name.FontSize, "le nom suit la taille de l'icône");
            Assert.AreEqual(FontWeights.Bold, name.FontWeight);
            var runs = name.Inlines.OfType<Run>().ToList();
            Assert.AreEqual("kyf|Rev", string.Join('|', runs.Select(run => run.Text)));
            Assert.AreSame(host.FindResource("Kyf.Brush.Text"), name.Foreground);
            Assert.AreSame(host.FindResource("Kyf.Brush.Primary"), runs[1].Foreground, "« Rev » dans la couleur principale du thème");
            Assert.IsTrue(name.TranslatePoint(default, logotype).X >= mark.ActualWidth, "le nom à droite de l'icône, sans la toucher");

            // Sans fond : l'image sert aussi hors de l'application, sur une page claire ou sombre.
            host.Capture($"logotype-{(dark ? "sombre" : "clair")}");

            logotype.MarkSize = 40;
            TestShell.Flush();
            Assert.AreEqual((40d, 32d), (mark.ActualWidth, name.FontSize));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_welcome_the_unlock_screen_and_the_about_section_show_the_brand()
    {
        Assert.IsInstanceOfType<Logotype>(new WelcomePage().FindName("Brand"));
        Assert.IsInstanceOfType<Logotype>(new AboutSettingsPage().FindName("Brand"));
        var unlock = (BrandMark)new UnlockPage().FindName("Brand");
        Assert.AreEqual(64d, unlock.Width, "l'icône seule, avec son cadenas en pastille");
        Assert.AreEqual(BitmapScalingMode.HighQuality, RenderOptions.GetBitmapScalingMode(unlock));
    }
}
