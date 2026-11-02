using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Paramètres » : la séquence et les rappels, sur un vrai coffre.</summary>
[TestClass]
public sealed class SettingsTests
{
    private static readonly int[] NewSteps = [2, 4, 8];

    private static readonly int[] NewRest = [3, 6];

    [TestMethod]
    public async Task It_shows_what_the_vault_has()
    {
        using var vault = new SettingsVault();

        var page = await vault.LoadedAsync();

        Assert.IsTrue(page.IsReady);
        Assert.AreEqual("1, 3, 7, 14, 30", page.Steps);
        Assert.AreEqual("2, 4, 8, 12", page.RestMonths);
        Assert.AreEqual(vault.Stored.MaxPerDay.ToString(System.Globalization.CultureInfo.InvariantCulture), page.MaxPerDay);
        Assert.AreEqual(vault.Stored.MaxWaitDays.ToString(System.Globalization.CultureInfo.InvariantCulture), page.MaxWaitDays);
        Assert.AreEqual(vault.Stored.ActiveRecall, page.ActiveRecall);
        Assert.AreEqual(vault.Stored.IncludeManualArchiving, page.IncludeManualArchiving);
        Assert.IsFalse(page.HasChanges);
        Assert.IsFalse(page.SaveCommand.CanExecute(null), "rien à enregistrer");
        Assert.IsFalse(page.HasError);
    }

    [TestMethod]
    public async Task The_preview_says_the_dates_of_the_reference_example()
    {
        using var vault = new SettingsVault(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero));

        var page = await vault.LoadedAsync();

        Assert.AreEqual("Une fiche créée aujourd'hui reviendra : J1 le 10 oct., J3 le 13 oct., J7 le 20 oct., J14 le 3 nov., J30 le 3 déc.", page.StepsPreview, "les dates du §1.2 du plan");

        page.Steps = "2, 4";
        Assert.AreEqual("Une fiche créée aujourd'hui reviendra : J2 le 11 oct., J4 le 15 oct.", page.StepsPreview);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1, deux")]
    [DataRow("3, 1")]
    [DataRow("1, 1")]
    [DataRow("0, 1")]
    [DataRow("1, 99999999")]
    public async Task The_preview_says_nothing_of_a_sequence_that_does_not_read(string steps)
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();

        page.Steps = steps;

        Assert.AreEqual(string.Empty, page.StepsPreview);
        Assert.IsFalse(page.HasStepsPreview);
    }

    [TestMethod]
    public async Task Saving_changes_only_what_the_page_shows()
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        var before = vault.Stored;

        page.Steps = "2; 4  8";
        page.RestMonths = "3, 6";
        page.MaxPerDay = "5";
        page.MaxWaitDays = "40";
        page.ActiveRecall = !before.ActiveRecall;
        page.IncludeManualArchiving = !before.IncludeManualArchiving;
        Assert.IsTrue(page.HasChanges);
        Assert.IsFalse(page.IsSaved);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsSaved);
        Assert.IsFalse(page.HasError);
        Assert.IsFalse(page.HasChanges);
        Assert.AreEqual("2, 4, 8", page.Steps, "réécrit proprement");
        var after = vault.Stored;
        CollectionAssert.AreEqual(NewSteps, after.Steps.ToArray());
        CollectionAssert.AreEqual(NewRest, after.RestSequenceMonths.ToArray());
        Assert.AreEqual(5, after.MaxPerDay);
        Assert.AreEqual(40, after.MaxWaitDays);
        Assert.AreEqual(!before.ActiveRecall, after.ActiveRecall);
        Assert.AreEqual(!before.IncludeManualArchiving, after.IncludeManualArchiving);
        Assert.AreEqual(before.BackupRotation, after.BackupRotation, "ce que la vue ne montre pas reste");
        Assert.AreEqual(before.QuotesActive, after.QuotesActive);

        page.MaxPerDay = "6";
        Assert.IsFalse(page.IsSaved, "le message ne survit pas à la frappe suivante");
    }

    [TestMethod]
    public async Task A_setting_from_elsewhere_is_kept()
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        page.MaxPerDay = "4";
        Assert.IsTrue(vault.App.Settings.Change(vault.Stored with { BackupRotation = !vault.Stored.BackupRotation }).Succeeded);
        var rotation = vault.Stored.BackupRotation;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual(4, vault.Stored.MaxPerDay);
        Assert.AreEqual(rotation, vault.Stored.BackupRotation, "les réglages sont relus avant d'écrire");
    }

    [TestMethod]
    [DataRow("1, deux", "2, 4", "3", "30", SettingsText.WholeNumbers)]
    [DataRow("1, 3", "un", "3", "30", SettingsText.WholeNumbers)]
    public async Task What_does_not_read_is_refused_before_the_vault(string steps, string rest, string perDay, string wait, string expected)
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        var before = vault.Stored;
        page.Steps = steps;
        page.RestMonths = rest;
        page.MaxPerDay = perDay;
        page.MaxWaitDays = wait;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual(expected, page.Error);
        Assert.IsFalse(page.IsSaved);
        Assert.AreEqual(steps, page.Steps, "ce qui est saisi reste");
        CollectionAssert.AreEqual(before.Steps.ToArray(), vault.Stored.Steps.ToArray());
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("21")]
    [DataRow("beaucoup")]
    public async Task The_number_of_reminders_stays_within_its_limits(string perDay)
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        page.MaxPerDay = perDay;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual(SettingsText.MaxPerDayRange, page.Error);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("366")]
    public async Task The_wait_stays_within_its_limits(string wait)
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        page.MaxWaitDays = wait;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual(SettingsText.MaxWaitDaysRange, page.Error);
    }

    [TestMethod]
    [DataRow("3, 1", "")]
    [DataRow("", "")]
    [DataRow("1, 3", "0")]
    public async Task What_the_core_refuses_is_said_in_its_words(string steps, string rest)
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        var before = vault.Stored;
        page.Steps = steps;
        page.RestMonths = rest;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsSaved);
        CollectionAssert.AreEqual(before.Steps.ToArray(), vault.Stored.Steps.ToArray(), "le coffre n'a pas changé");
        Assert.IsTrue(page.HasChanges, "la saisie attend d'être corrigée");
    }

    [TestMethod]
    public async Task Coming_back_does_not_erase_what_is_being_typed()
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        page.Steps = "2, 4";

        await page.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("2, 4", page.Steps);
    }

    [TestMethod]
    public async Task Coming_back_reads_again_when_nothing_is_being_typed()
    {
        using var vault = new SettingsVault();
        var page = await vault.LoadedAsync();
        Assert.IsTrue(vault.App.Settings.Change(vault.Stored with { MaxPerDay = 7 }).Succeeded);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("7", page.MaxPerDay);
        Assert.IsFalse(page.HasChanges);
    }

    [TestMethod]
    public async Task Without_a_vault_it_says_so_and_hides_the_form()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = new SettingsPageViewModel(vaults.App.Settings, TimeProvider.System);

        await page.LoadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsReady);
        Assert.IsFalse(page.IsLoading);
        Assert.IsFalse(page.HasChanges);
    }

    [TestMethod]
    public void The_arguments_are_required()
    {
        using var vaults = new TestVaults(AppMode.Development);
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsPageViewModel(null!, TimeProvider.System));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SettingsPageViewModel(vaults.App.Settings, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.App.Settings.Update(null!));
    }

    [STATestMethod]
    public void The_page_uses_two_columns_and_one_when_it_lacks_room()
    {
        using var ui = new UiThread();
        using var vault = new SettingsVault();
        var page = new SettingsPageViewModel(vault.App.Settings, TimeProvider.System);
        var test = new TestShell();

        // La largeur est celle de la page et non de la fenêtre, que Windows borne à la taille de l'écran.
        var host = new Border { Width = 1300, Height = 800, Child = new SettingsPage { DataContext = page } };
        var window = new Window { ShowActivated = false, Left = -10000, Top = -10000, Width = 800, Height = 600, WindowStartupLocation = WindowStartupLocation.Manual, Content = host };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            UiThread.Until(() => page.IsReady);
            TestShell.Flush();
            var sections = Named<System.Windows.Controls.Primitives.UniformGrid>(window, "Sections");
            Assert.AreEqual(2, sections.Columns);
            var save = Named<Button>(window, "SaveSettings");
            Assert.IsFalse(save.IsEnabled, "rien à enregistrer");

            Named<TextBox>(window, "StepsBox").Text = "2, 4, 8";
            Assert.IsTrue(save.IsEnabled);
            Assert.AreEqual(Visibility.Collapsed, Named<Notification>(window, "SavedMessage").Visibility);

            host.Width = 600;
            TestShell.Flush();
            Assert.AreEqual(1, sections.Columns);
            Assert.AreEqual(0, Grid.GetRow(save), "encore en face de son explication");
            host.Width = 420;
            TestShell.Flush();
            Assert.AreEqual(1, Grid.GetRow(save), "sous son explication quand la place manque");
            var scroller = Named<ScrollViewer>(window, "Scroller");
            var card = Named<Card>(window, "RemindersCard");
            Assert.IsLessThanOrEqualTo(scroller.ActualWidth + 0.5, card.TransformToAncestor(scroller).Transform(default).X + card.ActualWidth, "pas de défilement horizontal");

            save.Command.Execute(null);
            UiThread.Until(() => page.IsSaved);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, Named<Notification>(window, "SavedMessage").Visibility);
            Assert.IsFalse(save.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().First(element => element.Name == name);

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

    /// <summary>Un coffre jetable, à une date fixée.</summary>
    private sealed class SettingsVault : IDisposable
    {
        private readonly TestVaults _vaults;
        private readonly FakeTime _time;

        public SettingsVault()
            : this(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero))
        {
        }

        public SettingsVault(DateTimeOffset now)
        {
            _time = new FakeTime(now);
            _vaults = new TestVaults(AppMode.Development, _time);
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
        }

        public KyfRevApplication App => _vaults.App;

        public VaultPreferences Stored => App.Settings.Get().Value;

        public async Task<SettingsPageViewModel> LoadedAsync()
        {
            var page = new SettingsPageViewModel(App.Settings, _time);
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        public void Dispose() => _vaults.Dispose();
    }
}
