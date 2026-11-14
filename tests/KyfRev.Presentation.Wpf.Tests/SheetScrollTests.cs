using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La barre de défilement d'une fiche est au bord droit de la fenêtre, pas collée à sa feuille ; le texte, lui, reste
/// une colonne à largeur de lecture, à toutes les largeurs de fenêtre et dans les deux thèmes.
/// </summary>
[TestClass]
public sealed class SheetScrollTests
{
    private static readonly string Body = string.Join("\n\n", Enumerable.Range(1, 60).Select(index => $"Paragraphe {index} : assez de mots pour passer à la ligne dans une colonne de lecture, et voir où elle commence et où elle finit dans la fenêtre."));

    [STATestMethod]
    [DataRow(470, false)]
    [DataRow(900, false)]
    [DataRow(900, true)]
    [DataRow(1700, false)]
    [DataRow(2600, true)]
    [DataRow(3440, false)]
    public void The_scroll_bar_of_a_sheet_is_at_the_right_edge_and_its_text_stays_a_column(int width, bool dark)
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body + "\n", passage: false);
        var page = vault.Page();
        var test = new TestShell(dark);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page, Width = width, Height = 800 };
        var host = new Border { Child = sheet, Width = width, Height = 800 };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        host.SetResourceReference(Border.BackgroundProperty, "Kyf.Brush.Background");
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            var viewer = (SheetDocumentViewer)sheet.FindName("SheetText");
            UiThread.Until(() => viewer.Document is not null);
            viewer.CompleteDocument();
            for (var pass = 0; pass < 8; pass++)
            {
                TestShell.Flush();
                sheet.UpdateLayout();
            }
            host.Capture($"fiche-{width}-{(dark ? "sombre" : "clair")}");

            // La barre : dans la vue de la fiche, à son bord droit, lui-même au bord de la page.
            var bar = Visuals(viewer).OfType<ScrollBar>().Single(candidate => candidate.Orientation == Orientation.Vertical && candidate.IsVisible);
            var right = bar.TranslatePoint(new Point(bar.ActualWidth, 0), sheet).X;
            Assert.IsLessThanOrEqualTo(width, right);
            Assert.IsGreaterThanOrEqualTo(width - 8, right, "la barre de défilement est au bord droit de la page");

            // Le texte : une colonne qui ne dépasse pas la largeur de lecture, centrée dans ce qui reste.
            var first = viewer.Document!.Blocks.OfType<Paragraph>().First(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.StartsWith("Paragraphe 1 ", StringComparison.Ordinal));
            var start = first.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var left = viewer.TranslatePoint(new Point(start.Left, 0), sheet).X;
            var ends = new List<double>();
            for (var position = first.ContentStart.GetLineStartPosition(0); position is not null && position.CompareTo(first.ContentEnd) < 0; position = position.GetLineStartPosition(1, out var moved) is { } next && moved == 1 ? next : null)
            {
                var end = position.GetLineStartPosition(1, out var count) is { } after && count == 1 ? after.GetPositionAtOffset(-1, LogicalDirection.Backward) ?? first.ContentEnd : first.ContentEnd;
                ends.Add(viewer.TranslatePoint(new Point(end.GetCharacterRect(LogicalDirection.Backward).Right, 0), sheet).X);
            }
            var textRight = ends.Max();
            Assert.IsLessThanOrEqualTo(SheetPageViewModel.TextMaxWidth + 1, textRight - left, "le texte reste à largeur de lecture");
            Assert.IsLessThan(bar.TranslatePoint(default, sheet).X, textRight, "sans passer sous la barre");
            if (width >= 1700)
            {
                // De la place de chaque côté : autant à gauche qu'à droite, à la barre près.
                var area = (FrameworkElement)sheet.FindName("SheetArea");
                var areaLeft = area.TranslatePoint(default, sheet).X;
                var free = area.ActualWidth - bar.ActualWidth - SheetPageViewModel.TextMaxWidth;
                Assert.AreEqual(areaLeft + (free / 2), left, 24, "la colonne est centrée dans la zone de la fiche");
                Assert.IsGreaterThan(ends.Count > 1 ? 400 : 0, textRight - left, "et garde sa largeur");
            }
            else if (width <= 900)
            {
                Assert.IsLessThan(60, left - ((FrameworkElement)sheet.FindName("SheetArea")).TranslatePoint(default, sheet).X, "étroite, la fiche prend toute la place");
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var deeper in Visuals(child))
            {
                yield return deeper;
            }
        }
    }
}
