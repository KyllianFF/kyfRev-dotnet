using System.Windows;
using System.Windows.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le défilement du lecteur : une autre fiche dans la même vue (une séance passe à la suivante) commence en haut ; la
/// molette avance de trois lignes de texte par cran (le réglage de Windows), en glissant.
/// </summary>
public sealed partial class ReadingTests
{
    [STATestMethod]
    public void Another_sheet_in_the_same_view_starts_at_the_top_and_the_same_one_keeps_its_place()
    {
        var host = Host(Long());
        try
        {
            var scroller = Scroller(host.Viewer);
            scroller.ScrollToVerticalOffset(900);
            TestShell.Flush();
            Assert.IsGreaterThan(0, scroller.VerticalOffset);

            // La même fiche relue (après l'avoir écrite, par exemple) garde sa place.
            host.Viewer.Reading = SheetReading.Of(Sheet(Long() + "\n\nUne phrase de plus."), _ => Outcome.Refused<byte[]>("non"));
            TestShell.Flush();
            Assert.IsGreaterThan(0, scroller.VerticalOffset, "la même fiche ne remonte pas");

            // Une autre fiche, dans la même vue : elle commence en haut.
            host.Viewer.Reading = SheetReading.Of(OtherSheet(Long()), _ => Outcome.Refused<byte[]>("non"));
            TestShell.Flush();
            Assert.AreEqual(0, scroller.VerticalOffset, "une autre fiche commence en haut");

            // Un passage par « rien » (la fiche suivante se charge) ne compte pas pour une fiche.
            scroller.ScrollToVerticalOffset(600);
            TestShell.Flush();
            host.Viewer.Reading = null;
            host.Viewer.Reading = SheetReading.Of(Sheet(Long()), _ => Outcome.Refused<byte[]>("non"));
            TestShell.Flush();
            Assert.AreEqual(0, scroller.VerticalOffset, "la fiche d'avant, puis rien, puis une autre : en haut");
        }
        finally
        {
            host.Window.Close();
        }
    }

    [STATestMethod]
    public void A_wheel_notch_scrolls_three_lines_of_text()
    {
        var host = Host(Long());
        try
        {
            var scroller = Scroller(host.Viewer);
            Assert.AreEqual(0, scroller.VerticalOffset);

            Wheel(host.Viewer, -Mouse.MouseWheelDeltaForOneLine);
            TestShell.Flush();
            var notch = scroller.VerticalOffset;
            Assert.IsGreaterThanOrEqualTo(Math.Max(1, SystemParameters.WheelScrollLines) * 32, notch, "trois lignes de texte au moins, pas 48 pixels");

            // Deux crans de suite vont deux fois plus loin ; vers le haut, la vue revient ; jamais au-delà du début.
            Wheel(host.Viewer, -Mouse.MouseWheelDeltaForOneLine);
            TestShell.Flush();
            Assert.AreEqual(2 * notch, scroller.VerticalOffset, 1);
            Wheel(host.Viewer, 3 * Mouse.MouseWheelDeltaForOneLine);
            TestShell.Flush();
            Assert.AreEqual(0, scroller.VerticalOffset);

            // Un pavé tactile envoie de petits pas : la vue avance d'autant.
            Wheel(host.Viewer, -Mouse.MouseWheelDeltaForOneLine / 4);
            TestShell.Flush();
            Assert.AreEqual(notch / 4, scroller.VerticalOffset, 1);
        }
        finally
        {
            host.Window.Close();
        }
    }

    /// <summary>Un cran de molette, comme Windows le donne, au-dessus du texte.</summary>
    private static void Wheel(UIElement target, int delta) =>
        target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent });

    private static OpenedSheet OtherSheet(string body) => new(
        "f_2", "Méiose", "fiches/Biologie/Méiose.md", "Biologie", ["bio"], body, 1, "sha256:autre", null, DateTimeOffset.UnixEpoch, null!, []);
}
