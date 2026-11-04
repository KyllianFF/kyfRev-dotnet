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

        var names = Names(page, () => page.Rows.Count > 0, null);
        CollectionAssert.Contains(names, DefinitionsText.WholeVault);
        CollectionAssert.Contains(names, DefinitionsText.SeeInSheet);

        var editing = Names(page, () => page.Rows.Count > 0, root => page.Rows[0].EditCommand.Execute(null));
        CollectionAssert.Contains(editing, DefinitionsText.TermLabel);
        CollectionAssert.Contains(editing, DefinitionsText.Save);
    }

    [STATestMethod]
    public void The_settings_name_all_their_controls()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var app = vault.App;
        var page = new SettingsPageViewModel(app.Settings, TimeProvider.System);
        var data = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, new FakePicker(), new FakeOpener(), () => { });
        var general = new GeneralSettingsViewModel(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices("Hortense"));
        var modules = new ModulesSettingsViewModel(app.Settings, _ => { });
        var hub = new SettingsHubViewModel(general, page, modules, data, new AboutSettingsViewModel(app.Vaults));

        var names = Names(hub, () => hub.Selected.Page == general, null);

        CollectionAssert.Contains(names, GeneralSettingsText.General);
        CollectionAssert.Contains(names, GeneralSettingsText.ReducedMotion);
        CollectionAssert.Contains(names, GeneralSettingsText.LargerTargets);

        hub.Tabs[1].IsSelected = true;
        names = Names(hub, () => page.IsReady, null);

        CollectionAssert.Contains(names, SettingsHubText.Data);
        CollectionAssert.Contains(names, SettingsText.Steps);
        CollectionAssert.Contains(names, SettingsText.ActiveRecall);
        CollectionAssert.Contains(names, SettingsText.Save);

        hub.Tabs[2].IsSelected = true;
        names = Names(hub, () => hub.Selected.Page == modules, null);

        CollectionAssert.Contains(names, ModulesSettingsText.QuotesModule);
        CollectionAssert.Contains(names, ModulesSettingsText.SchemasModule);

        hub.Tabs[3].IsSelected = true;
        names = Names(hub, () => data.IsLoaded, null);

        CollectionAssert.Contains(names, SettingsHubText.OpenVault);
        CollectionAssert.Contains(names, SettingsHubText.BackupNow);
        CollectionAssert.Contains(names, SettingsHubText.Rotation);
        CollectionAssert.Contains(names, SettingsHubText.ExportVault);
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
            foreach (var control in Descendants(window).OfType<Control>().Where(control => control is ButtonBase or TextBox or ListBoxItem or ComboBox && control.IsVisible && control.TemplatedParent is not (ComboBox or ScrollBar)))
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
