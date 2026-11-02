using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Ce que dit un lecteur d'écran sur les vues remplies, et non vides comme dans la galerie : ce qui ne paraît
/// qu'avec des données (cartes, barres, listes) a son nom, et pas le nom d'une classe.
/// </summary>
[TestClass]
public sealed class FilledViewsAccessibilityTests
{
    private const string Body = "# Mitose\n\nUn autre paragraphe avec un mot à définir.\n\nUn second paragraphe.\n";

    [STATestMethod]
    public void The_filled_glossary_names_all_its_controls()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: true);
        var page = new DefinitionsPageViewModel(vault.App.Annotations, new FakeOpener());

        var names = Names(page, () => page.Rows.Count > 0, root => page.Rows[0].EditCommand.Execute(null));

        CollectionAssert.Contains(names, DefinitionsText.WholeVault);
        CollectionAssert.Contains(names, DefinitionsText.TermLabel);
        CollectionAssert.Contains(names, DefinitionsText.SeeInSheet);
    }

    [STATestMethod]
    public void The_settings_name_all_their_controls()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var page = new SettingsPageViewModel(vault.App.Settings, TimeProvider.System);

        var names = Names(page, () => page.IsReady, null);

        CollectionAssert.Contains(names, SettingsText.Steps);
        CollectionAssert.Contains(names, SettingsText.ActiveRecall);
        CollectionAssert.Contains(names, SettingsText.Save);
    }

    [STATestMethod]
    public void Listening_and_changing_a_place_name_all_their_controls()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: true);
        var page = vault.Page(speech: new SilentVoices());
        page.LoadCommand.Execute(null);
        UiThread.Until(() => page.Reading is not null && !page.IsLoading);

        var names = Names(page, () => page.Reading is not null, root =>
        {
            Descendants(root).OfType<ToggleButton>().First(toggle => toggle.Name == "ListenToggle").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            page.ReadAloud!.PlayPauseCommand.Execute(null);
            page.StartRebindingCommand.Execute(page.Passages[0]);
        });

        CollectionAssert.Contains(names, ReadingText.Listen);
        CollectionAssert.Contains(names, ReadingText.ListenPause);
        CollectionAssert.Contains(names, ReadingText.ListenStop);
        CollectionAssert.Contains(names, ReadingText.RebindHere);
    }

    /// <summary>Montre une vue, attend qu'elle soit prête, vérifie le nom de chaque commande, et rend ces noms.</summary>
    private static List<string> Names(object page, Func<bool> ready, Action<DependencyObject>? adjust)
    {
        var test = new TestShell();
        test.Shell.Navigate(new NavigationItem("vue", "Vue", Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 720, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            UiThread.Until(ready);
            TestShell.Flush();
            adjust?.Invoke(window);
            TestShell.Flush();
            var names = new List<string>();
            foreach (var control in Descendants(window).OfType<Control>().Where(control => control is ButtonBase or TextBox or ListBoxItem && control.IsVisible))
            {
                var name = UIElementAutomationPeer.CreatePeerForElement(control).GetName();
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"Sans nom : {control.GetType().Name} « {control.Name} »");
                Assert.DoesNotContain(" { ", name, $"Le nom d'une classe : {name}");
                Assert.DoesNotContain("KyfRev.", name, $"Le nom d'une classe : {name}");
                names.Add(name);
            }
            return names;
        }
        finally
        {
            window.Close();
        }
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

    private sealed class SilentVoices : KyfRev.Application.Ports.ISpeech
    {
        public IReadOnlyList<string> FrenchVoices() => ["Hortense"];

        public Task<KyfRev.Application.Outcome<KyfRev.Application.Done>> SpeakAsync(string text, string? voice, double rate, CancellationToken cancellation) =>
            new TaskCompletionSource<KyfRev.Application.Outcome<KyfRev.Application.Done>>().Task;

        public void PauseSpeaking()
        {
        }

        public void ResumeSpeaking()
        {
        }
    }
}
