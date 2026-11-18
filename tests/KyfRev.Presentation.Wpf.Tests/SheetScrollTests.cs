using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
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
            Assert.IsLessThanOrEqualTo(SheetPageViewModel.ComfortableTextWidth + 1, textRight - left, "le texte reste à largeur de lecture");
            Assert.IsLessThan(bar.TranslatePoint(default, sheet).X, textRight, "sans passer sous la barre");
            if (width >= 1700)
            {
                // De la place de chaque côté : autant à gauche qu'à droite, à la barre près.
                var area = (FrameworkElement)sheet.FindName("SheetArea");
                var areaLeft = area.TranslatePoint(default, sheet).X;
                var free = area.ActualWidth - bar.ActualWidth - SheetPageViewModel.ComfortableTextWidth;
                Assert.AreEqual(areaLeft + (free / 2), left, 24, "la colonne est centrée dans la zone de la fiche");
                Assert.IsGreaterThan(ends.Count > 1 ? 400 : 0, textRight - left, "et garde sa largeur");
            }
            else if (width <= 600)
            {
                Assert.IsLessThan(60, left - ((FrameworkElement)sheet.FindName("SheetArea")).TranslatePoint(default, sheet).X, "étroite, la fiche prend toute la place");
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// La colonne de texte suit la largeur choisie dans Paramètres › Apparence : environ 80, 95 ou 115 caractères par
    /// ligne à la taille de texte normale. Un tableau ajusté à son contenu garde sa largeur dans les trois.
    /// </summary>
    [STATestMethod]
    [DataRow(ReadingColumn.Narrow, 80, false)]
    [DataRow(ReadingColumn.Comfortable, 95, false)]
    [DataRow(ReadingColumn.Comfortable, 95, true)]
    [DataRow(ReadingColumn.Wide, 115, false)]
    public void The_text_column_follows_the_width_chosen_on_this_computer(ReadingColumn column, int about, bool dark)
    {
        const string fittedTable = "<!-- kyfrev tableau largeur=ajustee -->\n| Étape | Jour |\n|---|---|\n| J1 | demain |\n| J3 | dans trois jours |\n\n";
        using var ui = new UiThread();
        using var vault = new ReadingVault(fittedTable + Body + "\n", passage: false);
        var page = vault.Page(column: () => column);
        var test = new TestShell(dark);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page, Width = 1700, Height = 800 };
        var host = new Border { Child = sheet, Width = 1700, Height = 800 };
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
            var name = column switch { ReadingColumn.Narrow => "etroite", ReadingColumn.Wide => "large", _ => "confortable" };
            host.Capture($"fiche-colonne-{name}-{(dark ? "sombre" : "clair")}");

            var expected = SheetPageViewModel.ColumnWidth(column);
            Assert.AreEqual(expected, page.TextWidth);
            var first = viewer.Document!.Blocks.OfType<Paragraph>().First(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.StartsWith("Paragraphe 1 ", StringComparison.Ordinal));
            var start = first.ContentStart.GetLineStartPosition(0)!;
            var next = start.GetLineStartPosition(1, out var moved);
            Assert.AreEqual(1, moved, "le paragraphe passe à la ligne");
            var line = new TextRange(start, next!).Text;
            var left = start.GetCharacterRect(LogicalDirection.Forward).Left;
            var right = next!.GetPositionAtOffset(-1, LogicalDirection.Backward)!.GetCharacterRect(LogicalDirection.Backward).Right;
            Assert.IsLessThanOrEqualTo(expected + 1, right - left, "une ligne ne dépasse pas la colonne");
            Assert.IsGreaterThan(expected - 120, right - left, "et la remplit, à un mot près");
            Assert.AreEqual(about, line.Length, about * 0.08, $"environ {about} caractères par ligne : « {line} »");

            // Le tableau ajusté : ses deux colonnes gardent ce qu'elles demandent, la colonne vide prend le reste de la ligne.
            var table = viewer.Document.Blocks.OfType<Table>().Single();
            var sizing = (TableSizing)table.Tag;
            Assert.IsTrue(sizing.Fitted);
            Assert.AreEqual(3, table.Columns.Count, "deux colonnes, et la colonne élastique");
            for (var index = 0; index < 2; index++)
            {
                Assert.AreEqual(sizing.Natural[index] / DocumentBuilder.StarUnit, table.Columns[index].Width.Value, 1e-6, "la colonne garde sa largeur, quelle que soit la colonne de texte");
            }
            Assert.AreEqual((expected - sizing.Natural.Sum()) / DocumentBuilder.StarUnit, table.Columns[2].Width.Value, 0.2, "le reste à la colonne vide");
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
