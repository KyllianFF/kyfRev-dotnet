using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Le critère de J7 : une fiche de 20 000 mots s'affiche en moins de 500 ms, et son défilement reste fluide.
/// Les bornes de l'essai sont larges, car un serveur de vérification peut être très lent : il garde contre
/// une dégradation grossière. La mesure elle-même est écrite dans la sortie de l'essai.
/// </summary>
[TestClass]
public sealed class ReadingPerformanceTests
{
    private const int Words = 20000;

    [STATestMethod]
    public void A_long_sheet_appears_quickly_and_scrolls_smoothly()
    {
        using var ui = new UiThread();
        var body = Corpus(Words);
        var sheet = new OpenedSheet("f_1", "Une très longue fiche", "fiches/Biologie/Longue.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);
        var clock = Stopwatch.StartNew();
        var reading = SheetReading.Of(sheet, _ => Outcome.Refused<byte[]>("non"));
        var analysis = clock.ElapsedMilliseconds;

        clock.Restart();
        var viewer = new SheetDocumentViewer { Reading = reading };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 1000, Height = 800, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, () => { });
        var firstDisplay = clock.ElapsedMilliseconds;
        try
        {
            // Le reste s'ajoute par morceaux : aucune opération ne doit figer l'interface.
            var longestTurn = 0L;
            var turn = Stopwatch.StartNew();
            var hooks = Dispatcher.CurrentDispatcher.Hooks;
            DispatcherHookEventHandler started = (_, _) => turn.Restart();
            DispatcherHookEventHandler completed = (_, _) => longestTurn = Math.Max(longestTurn, turn.ElapsedMilliseconds);
            hooks.OperationStarted += started;
            hooks.OperationCompleted += completed;
            clock.Restart();
            while (viewer.HasPendingWork)
            {
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, () => { });
                Assert.IsLessThan(120_000, clock.ElapsedMilliseconds, "la fiche ne finit pas de s'afficher");
            }
            var complete = clock.ElapsedMilliseconds;
            hooks.OperationStarted -= started;
            hooks.OperationCompleted -= completed;

            var scroller = Descendants(viewer).OfType<ScrollViewer>().First();
            var steps = new List<long>();
            for (var offset = 0d; offset < scroller.ScrollableHeight && steps.Count < 200; offset += scroller.ViewportHeight / 2)
            {
                turn.Restart();
                scroller.ScrollToVerticalOffset(offset);
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, () => { });
                steps.Add(turn.ElapsedMilliseconds);
            }
            steps.Sort();
            var median = steps[steps.Count / 2];
            var slow = steps[steps.Count * 95 / 100];

            Console.WriteLine($"MESURE {Words} mots : analyse {analysis} ms, premier affichage {firstDisplay} ms, reste {complete} ms (morceau le plus long {longestTurn} ms), défilement médian {median} ms, 95 % {slow} ms, maximum {steps[^1]} ms.");
            Assert.IsLessThan(10_000, analysis + firstDisplay, "premier affichage");
            Assert.IsLessThan(10_000, longestTurn, "un morceau gèle l'interface");
            Assert.IsLessThan(5_000, slow, "défilement");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Un texte ordinaire : titres, paragraphes, listes, citations, tableaux et code.</summary>
    private static string Corpus(int words)
    {
        var text = new StringBuilder();
        var count = 0;
        var part = 0;
        while (count < words)
        {
            part++;
            var section = $"""
                ## Partie {part}

                Un paragraphe avec du *texte* en italique et du **gras**, un [lien](Autre.md), et une adresse https://exemple.org/page qui reste à lire avec attention pendant la révision.
                La phrase suivante prolonge le paragraphe pour lui donner une longueur ordinaire, celle d'une fiche de cours bien remplie, avec plusieurs idées qui se suivent.

                - Un premier point de la liste, assez court.
                - Un deuxième point, avec du `code` et encore un peu de texte.

                > Une citation qui revient de temps en temps dans la fiche.

                | Colonne | Valeur |
                |---|---|
                | Première | Une valeur |
                | Seconde | Une autre valeur |

                ```csharp
                var exemple = "du code";
                ```


                """;
            text.Append(section);
            count += section.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }
        return text.ToString();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
