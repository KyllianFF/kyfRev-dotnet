using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Les animations de l'interface, réglées dans « Apparence » : toutes, les essentielles, aucune, ou famille par famille.</summary>
[TestClass]
public sealed class MotionSettingsTests
{
    private static readonly bool[] OnlyEssential = [true, true, false, false, false, false, false];

    [TestMethod]
    public void The_families_kept_on_this_computer_are_those_of_the_design_system()
    {
        foreach (var kind in Enum.GetValues<MotionKinds>())
        {
            Assert.AreEqual((int)kind, (int)Enum.Parse<MotionFamilies>(kind.ToString()), kind.ToString());
        }
    }

    [STATestMethod]
    public void A_family_switched_off_reaches_the_design_system_at_startup_and_at_each_change()
    {
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { MotionOff = MotionFamilies.Lists } };
        var machine = new MachineSettings(store);
        var test = new TestShell();

        machine.Follow(test.Appearance, test.Shell);
        Assert.AreEqual(MotionKinds.Lists, test.Design.MotionOff);

        machine.Update(preferences => preferences with { MotionOff = MotionFamilies.Panels | MotionFamilies.Opening });
        Assert.AreEqual(MotionKinds.Panels | MotionKinds.Opening, test.Design.MotionOff);
        Assert.AreEqual(MotionKinds.Panels | MotionKinds.Opening, test.Design.Dictionary[DesignKeys.MotionOff], "toute vue le trouve dans les ressources");
        Assert.AreEqual(MotionFamilies.Panels | MotionFamilies.Opening, test.Appearance.MotionOff);
    }

    [TestMethod]
    public void All_essential_or_none_is_one_choice_and_each_family_has_its_switch()
    {
        var store = new MemoryMachinePreferences();
        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices("Hortense"));
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.IsTrue(page.IsMotionAll);
        Assert.IsTrue(page.CanChooseMotions);
        Assert.HasCount(7, page.Motions);
        Assert.IsTrue(page.Motions.All(choice => choice.IsOn));
        Assert.AreEqual(GeneralSettingsText.MotionFeedback, page.Motions[0].Name);
        Assert.AreEqual(GeneralSettingsText.MotionFeedbackHelp, page.Motions[0].Help);

        // Les essentielles : le retour au toucher et la sélection qui glisse.
        page.IsMotionEssential = true;
        Assert.AreEqual(MotionFamilies.BeyondEssential, store.Stored.MotionOff);
        Assert.IsTrue(page.IsMotionEssential);
        Assert.IsFalse(page.IsMotionAll);
        CollectionAssert.AreEqual(OnlyEssential, page.Motions.Select(choice => choice.IsOn).ToArray());
        CollectionAssert.Contains(changed, nameof(GeneralSettingsViewModel.IsMotionAll));
        CollectionAssert.Contains(changed, nameof(GeneralSettingsViewModel.IsMotionEssential));

        // Une famille rallumée à la main : ni toutes, ni les essentielles, ni aucune.
        var lists = page.Motions.Single(choice => choice.Family == MotionFamilies.Lists);
        var told = 0;
        lists.PropertyChanged += (_, _) => told++;
        lists.IsOn = true;
        Assert.IsGreaterThan(0, told, "son interrupteur le dit");
        Assert.AreEqual(MotionFamilies.BeyondEssential & ~MotionFamilies.Lists, store.Stored.MotionOff);
        Assert.IsFalse(page.IsMotionAll || page.IsMotionEssential || page.IsMotionNone);
        lists.IsOn = false;
        Assert.IsTrue(page.IsMotionEssential);

        // Aucune : les animations réduites, comme avant. Les familles gardent leur réglage pour plus tard, sans plus servir.
        page.IsMotionNone = true;
        Assert.IsTrue(store.Stored.ReducedMotion);
        Assert.AreEqual(MotionFamilies.BeyondEssential, store.Stored.MotionOff);
        Assert.IsTrue(page.IsMotionNone);
        Assert.IsFalse(page.IsMotionEssential);
        Assert.IsFalse(page.CanChooseMotions);

        // Toutes.
        page.IsMotionAll = true;
        Assert.IsFalse(store.Stored.ReducedMotion);
        Assert.AreEqual(MotionFamilies.None, store.Stored.MotionOff);
        Assert.IsTrue(page.IsMotionAll);
        Assert.IsTrue(page.CanChooseMotions);

        // Un choix décoché par le suivant ne décide de rien.
        page.IsMotionAll = false;
        page.IsMotionEssential = false;
        page.IsMotionNone = false;
        Assert.IsTrue(page.IsMotionAll);
    }

    [TestMethod]
    public void The_tab_shows_what_is_kept_and_restoring_the_defaults_plays_everything_again()
    {
        var store = new MemoryMachinePreferences { Stored = new MachinePreferences { MotionOff = MotionFamilies.Opening | MotionFamilies.Surfaces } };
        var page = new GeneralSettingsViewModel(new MachineSettings(store), new FakeVoices("Hortense"));
        Assert.AreEqual(MotionFamilies.Opening | MotionFamilies.Surfaces, page.MotionOff);
        Assert.IsFalse(page.Motions.Single(choice => choice.Family == MotionFamilies.Opening).IsOn);
        Assert.IsTrue(page.Motions.Single(choice => choice.Family == MotionFamilies.Views).IsOn);
        Assert.AreEqual(0, store.Saves, "montrer n'écrit rien");
    }

    [STATestMethod]
    [DataRow(1000d, false)]
    [DataRow(470d, true)]
    public void The_card_holds_the_three_way_choice_and_one_switch_per_family(double width, bool dark)
    {
        using var ui = new UiThread();
        var test = new TestShell(dark);
        test.Design.Theme = dark ? ThemeKind.Dark : ThemeKind.Light;
        var model = new GeneralSettingsViewModel(new MachineSettings(new MemoryMachinePreferences()), new FakeVoices("Hortense"));
        var page = new GeneralSettingsPage { DataContext = model };
        var host = new Border { Width = width, Height = 1400, Child = page, Background = (Brush)test.Design.Dictionary["Kyf.Brush.Background"] };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsTrue(page.MotionCard.IsVisible);
            Assert.IsLessThan(page.AccessibilityCard.TranslatePoint(default, page).Y, page.MotionCard.TranslatePoint(default, page).Y, "avant l'accessibilité");
            Assert.IsLessThanOrEqualTo(width + 0.5, page.MotionCard.TranslatePoint(new Point(page.MotionCard.ActualWidth, 0), host).X, "rien ne dépasse de la fenêtre");
            Assert.IsTrue(page.MotionAllChoice.IsChecked);

            // Une rangée par famille, chacune avec son interrupteur, nommé pour le lecteur d'écran.
            var switches = Descendants(page.MotionChoices).OfType<CheckBox>().ToList();
            Assert.HasCount(7, switches);
            Assert.IsTrue(switches.All(box => box.IsChecked == true && box.IsEnabled));
            Assert.AreEqual(GeneralSettingsText.MotionSelection, System.Windows.Automation.AutomationProperties.GetName(switches[1]));
            foreach (var box in switches)
            {
                Assert.IsLessThanOrEqualTo(width + 0.5, box.TranslatePoint(new Point(box.ActualWidth, 0), host).X, "aucun interrupteur hors de la fenêtre");
            }

            // Les essentielles : le choix le dit, cinq interrupteurs s'éteignent.
            page.MotionEssentialChoice.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(model.IsMotionEssential);
            CollectionAssert.AreEqual(OnlyEssential, switches.Select(box => box.IsChecked == true).ToArray());

            // Un interrupteur basculé : plus aucun des trois choix.
            switches[6].IsChecked = true;
            TestShell.Flush();
            Assert.IsFalse(model.Motions[6].Family != MotionFamilies.Opening || !model.Motions[6].IsOn);
            Assert.IsFalse(page.MotionAllChoice.IsChecked == true || page.MotionEssentialChoice.IsChecked == true || page.MotionNoneChoice.IsChecked == true);
            host.Capture($"animations-{width}-{(dark ? "sombre" : "clair")}");

            // Aucune : les interrupteurs ne servent plus.
            page.MotionNoneChoice.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(model.ReducedMotion);
            Assert.IsFalse(page.MotionChoices.IsEnabled);
            Assert.IsFalse(switches[0].IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_opening_does_not_play_once_its_family_is_off()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1000, Height = 700, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        try
        {
            Transitions.Skip = false;
            test.Design.ReducedMotion = false;
            test.Design.MotionOff = MotionKinds.Opening;
            Assert.IsFalse(window.PlayOpening(), "coupée dans les Paramètres, elle ne joue pas");
            Assert.IsNull(window.Opening);
            test.Design.MotionOff = MotionKinds.Lists;
            Assert.IsTrue(window.PlayOpening(), "une autre famille coupée ne la retient pas");
            window.Opening!.Skip();
        }
        finally
        {
            Transitions.Skip = true;
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
