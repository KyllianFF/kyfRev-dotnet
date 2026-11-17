using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// L'accueil et le démarrage, sur de vrais coffres : créer le coffre prévu, choisir un dossier,
/// afficher un refus, et le menu qui ne revient qu'une fois un coffre ouvert.
/// </summary>
[TestClass]
public sealed class WelcomeTests
{
    [TestMethod]
    public async Task The_planned_vault_is_created_where_it_is_planned_without_asking()
    {
        using var test = new TestVaults(AppMode.Installed);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);
        var changes = new List<string>();
        welcome.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        Assert.IsTrue(welcome.HasVaultToCreate);
        Assert.AreEqual(test.Planned, welcome.VaultToCreate);
        Assert.AreEqual(WelcomeText.CreateHere, welcome.CreateHelp);
        Assert.IsTrue(welcome.CanOpenExisting, "installé : un coffre existant peut aussi être repris");
        Assert.IsFalse(welcome.ExplainsNewVault);

        await welcome.CreateCommand.ExecuteAsync(null);

        Assert.IsEmpty(test.Picker.Titles);
        Assert.AreEqual(test.Planned, opened.Single().Vault);
        Assert.IsTrue(Directory.Exists(Path.Combine(test.Planned, ".suivi")));
        CollectionAssert.IsSubsetOf(new[] { nameof(WelcomePageViewModel.IsBusy) }, changes);
        Assert.IsFalse(welcome.IsBusy);
        Assert.IsTrue(welcome.CreateCommand.CanExecute(null));
    }

    [TestMethod]
    public void The_portable_welcome_offers_only_its_planned_vault()
    {
        using var test = new TestVaults(AppMode.Portable);

        var welcome = test.Welcome(_ => { });

        Assert.IsTrue(welcome.HasVaultToCreate);
        Assert.IsFalse(welcome.CanOpenExisting);
    }

    [TestMethod]
    public async Task Without_a_planned_vault_a_folder_is_chosen()
    {
        using var test = new TestVaults(AppMode.Development);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);
        Assert.IsFalse(welcome.HasVaultToCreate);
        Assert.AreEqual(WelcomeText.CreateAnywhere, welcome.CreateHelp);
        Assert.IsTrue(welcome.CanOpenExisting);
        Assert.IsTrue(welcome.ExplainsNewVault);

        // Renoncer ne fait rien.
        test.Picker.Answers.Enqueue(null);
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.AreEqual("Choisissez un dossier vide pour le nouveau coffre", test.Picker.Titles.Single());
        Assert.IsEmpty(opened);
        Assert.IsFalse(welcome.HasError);

        // Un dossier qui n'est pas vide est refusé, avec le message du coffre.
        var notEmpty = test.Folder("Notes");
        await File.WriteAllTextAsync(Path.Combine(notEmpty, "note.txt"), "Une note.");
        test.Picker.Answers.Enqueue(notEmpty);
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.IsTrue(welcome.HasError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(welcome.Error));
        Assert.IsEmpty(opened);

        // Un coffre existant est ouvert ; le message précédent est effacé dès la nouvelle tentative.
        var existing = test.CreateVault("Existant");
        test.Picker.Answers.Enqueue(existing);
        await welcome.OpenCommand.ExecuteAsync(null);
        Assert.AreEqual("Choisissez le dossier du coffre", test.Picker.Titles[^1]);
        Assert.IsNull(welcome.Error);
        Assert.AreEqual(existing, opened.Single().Vault);
    }

    [TestMethod]
    public async Task A_folder_that_is_not_a_vault_is_refused()
    {
        using var test = new TestVaults(AppMode.Development);
        var opened = new List<AppState>();
        var welcome = test.Welcome(opened.Add);

        test.Picker.Answers.Enqueue(test.Folder("Pas un coffre"));
        await welcome.OpenCommand.ExecuteAsync(null);

        Assert.IsTrue(welcome.HasError);
        Assert.IsEmpty(opened);
    }

    [TestMethod]
    public async Task The_startup_error_is_shown_until_the_next_attempt()
    {
        using var test = new TestVaults(AppMode.Development);
        var state = new AppState("1.0.0-essai", AppMode.Development, null, null, "Le coffre est déjà ouvert ailleurs.");
        var welcome = new WelcomePageViewModel(test.App.Vaults, test.Picker, state, _ => { });

        Assert.AreEqual("Le coffre est déjà ouvert ailleurs.", welcome.Error);
        Assert.IsTrue(welcome.HasError);

        test.Picker.Answers.Enqueue(null);
        await welcome.OpenCommand.ExecuteAsync(null);
        Assert.IsFalse(welcome.HasError);
    }

    [TestMethod]
    public void The_welcome_needs_its_services()
    {
        using var test = new TestVaults(AppMode.Development);
        var state = test.App.Vaults.State();
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(null!, test.Picker, state, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, null!, state, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, test.Picker, null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WelcomePageViewModel(test.App.Vaults, test.Picker, state, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.Run(null!, test.App.Vaults, test.Picker));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultStartup.Run(new TestShell().Shell, null!, test.Picker));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestShell().Shell.ShowWelcome(null!));
    }

    [TestMethod]
    public void The_welcome_hides_the_menu_and_the_views_until_a_vault_is_open()
    {
        var shell = new TestShell().Shell;
        var page = new object();

        shell.ShowWelcome(page);

        Assert.IsTrue(shell.IsWelcome);
        Assert.IsFalse(shell.IsMenuVisible);
        Assert.AreSame(page, shell.Current.Page);
        Assert.AreEqual("Bienvenue dans kyfRev", shell.Current.Label);
        Assert.IsNull(shell.SelectedMain);
        shell.Navigate(shell.MainNavigation[1]);
        shell.NavigateToPosition("2");
        shell.Navigate(shell.Gallery);
        Assert.AreSame(page, shell.Current.Page);

        // Sans coffre, la recherche ne propose que les commandes de la coquille.
        shell.OpenPalette();
        shell.PaletteQuery = "aller";
        Assert.IsTrue(shell.HasNoResult);
        shell.PaletteQuery = "interface";
        Assert.HasCount(2, shell.PaletteResults);
        shell.ClosePalette();

        shell.ShowWorkspace();

        Assert.IsFalse(shell.IsWelcome);
        Assert.IsTrue(shell.IsMenuVisible);
        Assert.AreSame(shell.Home, shell.Current);
        Assert.AreSame(shell.Home, shell.SelectedMain);
        shell.PaletteQuery = "aller";
        Assert.IsFalse(shell.HasNoResult);
        shell.ToggleFocusMode();
        Assert.IsFalse(shell.IsMenuVisible);
    }

    [TestMethod]
    public async Task At_startup_the_remembered_vault_opens_and_the_welcome_waits_otherwise()
    {
        using var test = new TestVaults(AppMode.Development);
        var shell = new TestShell().Shell;

        var state = VaultStartup.Run(shell, test.App.Vaults, test.Picker);

        Assert.IsNull(state.Vault);
        Assert.IsTrue(shell.IsWelcome);
        var welcome = (WelcomePageViewModel)shell.Current.Page;
        test.Picker.Answers.Enqueue(test.PathOf("Coffre"));
        await welcome.CreateCommand.ExecuteAsync(null);
        Assert.IsFalse(shell.IsWelcome);
        Assert.AreSame(shell.Home, shell.Current);

        // Au démarrage suivant, le coffre retenu est rouvert : pas d'accueil.
        var next = test.Restart();
        var nextShell = new TestShell().Shell;
        Assert.AreEqual(test.PathOf("Coffre"), VaultStartup.Run(nextShell, next.Vaults, test.Picker).Vault);
        Assert.IsFalse(nextShell.IsWelcome);
    }

    [TestMethod]
    public void The_folder_picker_returns_the_chosen_folder_only()
    {
        string? title = null;
        var chosen = new FolderPicker(dialog =>
        {
            title = dialog.Title;
            Assert.IsFalse(dialog.Multiselect);
            dialog.FolderName = @"C:\Coffre";
            return true;
        });
        Assert.AreEqual(@"C:\Coffre", chosen.Pick("Choisissez"));
        Assert.AreEqual("Choisissez", title);

        Assert.IsNull(new FolderPicker(_ => false).Pick("Choisissez"));
        Assert.IsNull(new FolderPicker(_ => null).Pick("Choisissez"));
        Assert.IsInstanceOfType<IFolderPicker>(new FolderPicker());
    }

    [STATestMethod]
    public void The_welcome_page_shows_the_planned_vault_and_the_error()
    {
        using var test = new TestVaults(AppMode.Installed);
        var shell = new TestShell();
        var state = test.App.Vaults.State() with { OpenError = "Le coffre est déjà ouvert ailleurs." };
        shell.Shell.ShowWelcome(new WelcomePageViewModel(test.App.Vaults, test.Picker, state, _ => { }));
        var window = new ShellWindow(shell.Shell) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(shell.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.AreNotEqual(0, new WindowInteropHelper(window).Handle);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "Sidebar").Visibility);
            var create = Named<Button>(window, "CreateButton");
            Assert.AreEqual("Créer mon coffre", AutomationProperties.GetName(create));
            Assert.AreEqual(WelcomeText.CreateHere, AutomationProperties.GetHelpText(create));
            Assert.AreEqual(Visibility.Visible, Named<Button>(window, "OpenButton").Visibility);
            Assert.AreEqual(test.Planned, Named<TextBlock>(window, "PlannedVault").Text);
            Assert.AreEqual(WelcomeText.CreateHere, Named<TextBlock>(window, "NewVaultHelp").Text);
            Assert.AreEqual(Visibility.Collapsed, Named<Button>(window, "SampleButton").Visibility, "sans rien pour le créer, le coffre d'exemple n'est pas proposé");
            Assert.AreEqual(Visibility.Collapsed, Named<Button>(window, "GuideButton").Visibility);
            var error = Named<Notification>(window, "ErrorMessage");
            Assert.IsTrue(error.IsOpen);
            Assert.AreEqual("Le coffre est déjà ouvert ailleurs.", error.Message);
            Assert.AreEqual(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(Named<TextBlock>(window, "Heading")));
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task The_sample_vault_is_created_in_its_own_folder_and_opened()
    {
        using var test = new TestVaults(AppMode.Installed);
        var opened = new List<AppState>();
        var asked = new List<string>();
        var guides = 0;
        var answer = KyfRev.Application.Outcome.Refused<AppState>("Ce dossier n'est pas vide.");
        var welcome = test.Welcome(opened.Add);
        Assert.IsFalse(welcome.CanTrySample || welcome.CanOpenGuide);
        Assert.IsFalse(welcome.TrySampleCommand.CanExecute(null), "rien à proposer tant que la coquille n'a rien donné");
        welcome.OpenGuideCommand.Execute(null);

        var sample = test.Folder("Exemple");
        welcome.Offers = new WelcomeOffers(sample, root =>
        {
            asked.Add(root);
            return answer;
        }, () => guides++);
        Assert.IsTrue(welcome.CanTrySample && welcome.CanOpenGuide);

        // Refusé : la raison se lit, rien n'est ouvert.
        await welcome.TrySampleCommand.ExecuteAsync(null);
        CollectionAssert.AreEqual(new[] { sample }, asked, "dans son dossier, sans rien demander");
        Assert.AreEqual("Ce dossier n'est pas vide.", welcome.Error);
        Assert.IsEmpty(opened);
        Assert.IsFalse(welcome.IsBusy);

        // Créé : il s'ouvre comme un coffre choisi.
        answer = KyfRev.Application.Outcome.Of(test.App.Vaults.Create(sample));
        await welcome.TrySampleCommand.ExecuteAsync(null);
        Assert.IsFalse(welcome.HasError);
        Assert.AreEqual(sample, opened.Single().Vault);

        // Sans dossier prévu, un dossier vide est demandé ; annulé, rien ne se passe.
        welcome.Offers = new WelcomeOffers(null, root =>
        {
            asked.Add(root);
            return answer;
        }, null);
        Assert.IsFalse(welcome.CanOpenGuide);
        test.Picker.Answers.Enqueue(null);
        await welcome.TrySampleCommand.ExecuteAsync(null);
        Assert.HasCount(2, asked);
        test.Picker.Answers.Enqueue(test.Folder("Ailleurs"));
        await welcome.TrySampleCommand.ExecuteAsync(null);
        Assert.AreEqual(test.Folder("Ailleurs"), asked[^1]);

        welcome.Offers = new WelcomeOffers(sample, _ => answer, () => guides++);
        welcome.OpenGuideCommand.Execute(null);
        Assert.AreEqual(1, guides);
    }

    [STATestMethod]
    public void The_shell_gives_the_welcome_what_it_offers()
    {
        using var test = new TestVaults(AppMode.Installed);
        var shell = new TestShell().Shell;
        var welcome = test.Welcome(_ => { });
        var offers = new WelcomeOffers(test.Folder("Exemple"), _ => KyfRev.Application.Outcome.Refused<AppState>("non"), shell.ShowGuide);

        shell.ShowWelcome(welcome);
        Assert.IsNull(welcome.Offers);
        shell.WelcomeOffers = offers;
        shell.ShowWelcome(welcome);
        Assert.AreSame(offers, welcome.Offers);

        // « Lire le guide » ouvre l'aide sur le guide ; F1 la rouvre sur les raccourcis.
        welcome.OpenGuideCommand.Execute(null);
        Assert.IsTrue(shell.IsHelpOpen && shell.IsGuideShown);
        shell.IsHelpOpen = false;
        shell.ShowHelpCommand.Execute(null);
        Assert.IsTrue(shell.IsHelpOpen);
        Assert.IsFalse(shell.IsGuideShown);
        shell.ShowGuideCommand.Execute(null);
        Assert.IsTrue(shell.IsGuideShown);
    }

    [STATestMethod]
    [DataRow(470, false)]
    [DataRow(760, true)]
    [DataRow(1280, false)]
    [DataRow(2600, true)]
    public void The_welcome_shows_three_entries_and_the_method_at_every_width_in_both_themes(int width, bool dark)
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Installed);
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var welcome = vaults.Welcome(_ => { });
        welcome.Offers = new WelcomeOffers(vaults.Folder("Exemple"), _ => KyfRev.Application.Outcome.Refused<AppState>("non"), () => { });
        var page = new WelcomePage { DataContext = welcome };
        var host = new Border { Width = width, Height = 860, Child = page, Background = (Brush)test.Design.Dictionary["Kyf.Brush.Background"] };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            List<Button> entries = [Named<Button>(page, "CreateButton"), Named<Button>(page, "OpenButton"), Named<Button>(page, "SampleButton")];
            var places = entries.Select(entry => new Rect(entry.TranslatePoint(default, page), entry.RenderSize)).ToList();
            var method = Named<Border>(page, "Method");
            var steps = Named<System.Windows.Controls.Primitives.UniformGrid>(page, "Steps").Children.OfType<FrameworkElement>().Select(step => new Rect(step.TranslatePoint(default, page), step.RenderSize)).ToList();

            // Rien ne dépasse, à aucune largeur : ni les entrées, ni la méthode, ni le lien du guide.
            foreach (var place in places.Concat(steps).Append(new Rect(method.TranslatePoint(default, page), method.RenderSize)))
            {
                Assert.IsTrue(place.Left >= 0 && place.Right <= width + 0.5, $"de {place.Left:0} à {place.Right:0} sur {width}");
            }
            var scroller = Named<ScrollViewer>(page, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth + 0.5, scroller.ExtentWidth);
            if (width >= WelcomePage.SideBySideMinimum)
            {
                Assert.IsTrue(places.All(place => Math.Abs(place.Top - places[0].Top) < 1), "trois entrées côte à côte");
                Assert.IsTrue(places.All(place => Math.Abs(place.Height - places[0].Height) < 1) && places.All(place => Math.Abs(place.Width - places[0].Width) < 1), "de même taille");
                Assert.IsTrue(places[0].Left < places[1].Left && places[1].Left < places[2].Left);
                Assert.IsTrue(steps.All(step => Math.Abs(step.Top - steps[0].Top) < 1), "les trois temps côte à côte");
                Assert.IsLessThanOrEqualTo(1054 + 0.5, places[2].Right - places[0].Left + 14, "la colonne ne s'étire pas sur un écran très large");
            }
            else
            {
                Assert.IsTrue(places[0].Bottom <= places[1].Top + 1 && places[1].Bottom <= places[2].Top + 1, "l'une sous l'autre en fenêtre étroite");
                Assert.IsTrue(steps[0].Bottom <= steps[1].Top + 1 && steps[1].Bottom <= steps[2].Top + 1);
            }

            // L'entrée principale est en couleur, et son texte reste celui qui se lit sur cette couleur.
            Assert.AreSame(page.FindResource("Kyf.Brush.Primary"), entries[0].Background);
            Assert.AreSame(page.FindResource("Kyf.Brush.OnPrimary"), Named<TextBlock>(page, "PlannedVault").Foreground);
            Assert.AreSame(page.FindResource("Kyf.Brush.OnPrimary"), Named<TextBlock>(page, "NewVaultHelp").Foreground);
            Assert.AreEqual(vaults.Planned, Named<TextBlock>(page, "PlannedVault").Text);
            Assert.IsTrue(entries.All(entry => entry.ActualHeight >= 44 && !string.IsNullOrEmpty(AutomationProperties.GetName(entry)) && !string.IsNullOrEmpty(AutomationProperties.GetHelpText(entry))));
            Assert.AreEqual(6, Named<ItemsControl>(page, "StepPills").Items.Count);
            Assert.AreEqual(Visibility.Visible, Named<Button>(page, "GuideButton").Visibility);
            host.Capture($"accueil-{width}-{(dark ? "sombre" : "clair")}");
        }
        finally
        {
            window.Close();
        }
    }

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement =>
        Descendants(root).OfType<T>().Single(element => element.Name == name);

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
