using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Réviser une longue fiche (rangée 28C) : elle s'affiche par morceaux comme en lecture, chacun déjà masqué. À aucun
/// moment un gras arrivé n'est lisible, et une fiche sans gras se rappelle partie par partie dès le premier morceau.
/// </summary>
[TestClass]
public sealed class ReviewInPiecesTests
{
    private static string Long(bool bold) => "# Routage\n\n" + string.Concat(Enumerable.Range(1, 120).Select(index =>
        $"## Partie {index}\n\nUn {(bold ? $"**paragraphe {index}**" : $"paragraphe {index}")} assez long pour occuper plusieurs lignes, avec du texte et encore du texte.\n\n"));

    [STATestMethod]
    public void Each_piece_of_a_long_sheet_arrives_already_masked()
    {
        using var scene = new Scene(Long(bold: true));
        var viewer = scene.Viewer;
        Assert.IsTrue(viewer.HasPendingWork, "la fiche n'est pas construite d'un coup : le premier morceau paraît seul");
        var steps = 0;
        do
        {
            // Autant de gras arrivés dans le texte que de trous cachés : aucun ne se lit, même un instant.
            Assert.AreEqual(BoldsIn(viewer.Document!), scene.Page.Holes.Hidden.Count, $"morceau {steps}");
            steps++;
            // Un tour de construction : elle se fait en arrière-plan, un morceau par tour.
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, () => { });
        }
        while (viewer.HasPendingWork && steps < 500);
        Assert.IsGreaterThan(1, steps, "plusieurs morceaux");
        Assert.AreEqual(120, scene.Page.Holes.Hidden.Count, "une fois construite, chaque gras est un trou");
    }

    [STATestMethod]
    public void A_long_sheet_without_bold_is_recalled_part_by_part_from_its_first_piece()
    {
        using var scene = new Scene(Long(bold: false));
        Assert.IsTrue(scene.Viewer.HasPendingWork);
        Assert.IsTrue(scene.Page.Holes.IsBySection, "des parties, dès le premier morceau");
        Assert.IsNotEmpty(scene.Page.Holes.Hidden);
        while (scene.Viewer.HasPendingWork)
        {
            TestShell.Flush();
        }
        Assert.HasCount(121, scene.Page.Holes.Hidden, "une partie par titre, le premier compris");
    }

    private static int BoldsIn(FlowDocument document)
    {
        var count = 0;
        void Walk(object node, bool inBold)
        {
            var bold = node is Bold;
            if (bold && !inBold)
            {
                count++;
            }
            if (node is DependencyObject element)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(element))
                {
                    Walk(child, inBold || bold);
                }
            }
        }
        Walk(document, false);
        return count;
    }

    private sealed class Scene : IDisposable
    {
        private readonly UiThread _ui = new();
        private readonly ReadingVault _vault;
        private readonly Window _window;

        public Scene(string body)
        {
            _vault = new ReadingVault(body, passage: false);
            Page = _vault.Page(revising: true, activeRecall: () => true);
            var test = new TestShell();
            var view = new SheetPage { DataContext = Page, Width = 1400, Height = 900 };
            _window = new Window { Content = new Canvas { Children = { view } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            _window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
            _window.Show();
            Page.LoadCommand.Execute(null);
            Viewer = view.SheetText;
            UiThread.Until(() => Viewer.Document is not null, TimeSpan.FromMinutes(1));
        }

        public SheetPageViewModel Page { get; }

        public SheetDocumentViewer Viewer { get; }

        public void Dispose()
        {
            _window.Close();
            _vault.Dispose();
            _ui.Dispose();
        }
    }
}
