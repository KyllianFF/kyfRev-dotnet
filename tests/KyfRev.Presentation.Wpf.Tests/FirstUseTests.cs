using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La première utilisation : la carte « Premiers pas » de la vue « Aujourd'hui », dont les pas se cochent seuls, et ce que
/// l'ordinateur en retient.
/// </summary>
[TestClass]
public sealed class FirstUseTests
{
    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    [TestMethod]
    public async Task The_first_steps_tick_themselves_as_the_vault_fills()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var test = new TestVaults(AppMode.Development, time);
        var app = test.App;
        Assert.IsNotNull(app.Vaults.Create(test.PathOf("Coffre")).Vault);
        var guides = 0;
        var memory = new FirstUse();
        var steps = new FirstSteps(memory, () => guides++);
        var page = new DashboardPageViewModel(app.Reminders, app.Sheets, new FakeOpener()) { FirstSteps = steps };

        // Un coffre neuf : la carte tient la place de l'état vide, le premier pas est le prochain.
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreSame(steps, page.Rows.Single(row => row is FirstSteps));
        Assert.IsFalse(page.Rows.Any(row => row is EmptyDashboardRow));
        Assert.AreEqual("0 sur 3", steps.Progress);
        Assert.AreEqual("Premiers pas, 0 sur 3", steps.ToString());
        Assert.IsTrue(steps.Steps[0] is { IsCurrent: true, IsDone: false, OffersNewSheet: true, Number: "1" });
        Assert.IsTrue(steps.Steps.Skip(1).All(step => step is { IsCurrent: false, IsDone: false, OffersNewSheet: false }));
        StringAssert.StartsWith(steps.Steps[0].Spoken, "Écrire votre première fiche. ", StringComparison.Ordinal);

        // « Nouvelle fiche » ouvre la fenêtre de la vue ; « Lire le guide » ouvre le guide.
        steps.StartSheetCommand.Execute(null);
        Assert.IsTrue(page.IsCreatingSheet);
        await page.AnswerNewSheetCommand.ExecuteAsync(false);
        Assert.IsTrue(steps.CanOpenGuide);
        steps.OpenGuideCommand.Execute(null);
        Assert.AreEqual(1, guides);

        // Une fiche écrite : le premier pas est fait. Elle n'est due que demain, la carte reste là.
        var sheet = app.Sheets.Create("Biologie", "Mitose", null).Value;
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));
        Assert.AreEqual("1 sur 3", steps.Progress);
        Assert.IsTrue(steps.Steps[0] is { IsDone: true, IsCurrent: false, OffersNewSheet: false });
        Assert.AreEqual("Écrire votre première fiche : fait", steps.Steps[0].ToString());
        Assert.IsTrue(steps.Steps[1].IsCurrent);

        // Le lendemain, la fiche est due : la liste reprend sa place.
        time.AdvanceDays(1);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(page.Rows.Contains(steps), "une révision attend : la liste, pas la carte");
        Assert.IsTrue(memory.Has(FirstUse.SheetWritten));
        Assert.IsFalse(memory.Has(FirstUse.SheetReviewed));

        // Validée : le deuxième pas est fait, et la carte revient avec le troisième.
        Assert.IsTrue(app.Reviews.Validate(ItemKind.Sheet, sheet).Succeeded);
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));
        Assert.AreEqual("2 sur 3", steps.Progress);
        Assert.IsTrue(steps.Steps[2].IsCurrent);

        // Un passage surligné : les trois pas sont faits, la carte ne revient plus.
        app.Annotations.Create(sheet, Passage, null).Value.ToString();
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(memory.Has(FirstUse.PassageMarked));
        Assert.AreEqual("3 sur 3", steps.Progress);
        Assert.IsFalse(page.Rows.Contains(steps));
        time.AdvanceDays(40);
        Assert.IsTrue(app.Reviews.Validate(ItemKind.Passage, app.Sheets.Open(sheet).Value.Annotations.Single().Id).Succeeded);
        Assert.IsFalse(new FirstSteps(memory).Follow(app.Reminders.Dashboard().Value), "retenu : une autre séance ne la remontre pas");
    }

    [TestMethod]
    public async Task The_first_steps_can_be_hidden_and_stay_out_of_a_vault_that_is_no_longer_new()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var test = new TestVaults(AppMode.Development, time);
        var app = test.App;
        Assert.IsNotNull(app.Vaults.Create(test.PathOf("Coffre")).Vault);
        var memory = new FirstUse();
        var steps = new FirstSteps(memory);
        Assert.IsFalse(steps.CanOpenGuide);
        steps.OpenGuideCommand.Execute(null);
        steps.StartSheetCommand.Execute(null);
        var page = new DashboardPageViewModel(app.Reminders, app.Sheets, new FakeOpener()) { FirstSteps = steps };
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.Rows.Contains(steps));

        // « Masquer les premiers pas » : l'état vide revient aussitôt, et pour de bon.
        steps.HideCommand.Execute(null);
        Assert.IsFalse(page.Rows.Contains(steps));
        Assert.IsTrue(page.Rows.Any(row => row is EmptyDashboardRow));
        Assert.IsTrue(memory.Has(FirstUse.StepsHidden));
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(page.Rows.Contains(steps));

        // Un coffre qui a déjà plusieurs fiches ne débute plus : même sans rien masquer, la carte ne s'y montre pas.
        var fresh = new FirstSteps(new FirstUse());
        Assert.IsTrue(fresh.Follow(app.Reminders.Dashboard().Value));
        for (var index = 0; index <= FirstSteps.YoungVault; index++)
        {
            app.Sheets.Create(null, $"Fiche {index}", null).Value.ToString();
        }
        Assert.IsFalse(fresh.Follow(app.Reminders.Dashboard().Value));
        Assert.AreEqual("1 sur 3", fresh.Progress, "ce qu'il montre est coché quand même");
        Assert.ThrowsExactly<ArgumentNullException>(() => new FirstSteps(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => fresh.Follow(null!));
    }

    [TestMethod]
    public void What_the_first_use_has_shown_is_kept_with_the_machine_preferences()
    {
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var memory = new FirstUse(machine);
        var changes = 0;
        memory.Changed += (_, _) => changes++;

        Assert.IsFalse(memory.Has(FirstUse.StepsHidden));
        memory.Remember(FirstUse.StepsHidden);
        memory.Remember(FirstUse.ReviewTipRead);
        memory.Remember(FirstUse.StepsHidden);

        Assert.AreEqual(2, changes, "une seule fois chacun");
        Assert.AreEqual("aide-revision;pas-masques", store.Stored.FirstUse);
        Assert.IsTrue(new FirstUse(new MachineSettings(store)).Has(FirstUse.ReviewTipRead), "relu au démarrage suivant");
        Assert.IsFalse(memory.Has(FirstUse.NoTips));

        // Sans préférences (un essai, un poste où elles ne s'écrivent pas) : le temps de la séance.
        var session = new FirstUse();
        session.Remember(FirstUse.WritingTipRead);
        Assert.IsTrue(session.Has(FirstUse.WritingTipRead));
        Assert.IsFalse(new FirstUse().Has(FirstUse.WritingTipRead));
    }

    private static readonly string[] LinkNames = ["FirstStepsGuide", "FirstStepsSample", "FirstStepsHide"];

    [STATestMethod]
    [DataRow(470, true)]
    [DataRow(900, false)]
    [DataRow(1700, true)]
    public void The_first_steps_card_holds_at_every_width_in_both_themes(int width, bool dark)
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        vaults.App.Sheets.Create("Biologie", "Mitose", null).Value.ToString();
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var asked = 0;
        var steps = new FirstSteps(new FirstUse(), () => { }) { OpenSample = () => Task.FromResult<string?>(++asked == 1 ? "Ce dossier n'est pas vide." : null) };
        var model = new DashboardPageViewModel(vaults.App.Reminders, vaults.App.Sheets, new FakeOpener()) { FirstSteps = steps };
        var page = new DashboardPage { DataContext = model };
        var host = new Border { Width = width, Height = 900, Child = page, Background = (Brush)test.Design.Dictionary["Kyf.Brush.Background"] };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            model.OnNavigatedTo();
            UiThread.Until(() => model.Rows.Contains(steps));
            TestShell.Flush();
            var card = Descendants(page).OfType<Border>().Single(border => border.Name == "FirstStepsCard");
            var place = new Rect(card.TranslatePoint(default, page), card.RenderSize);
            Assert.IsTrue(place.Left >= 0 && place.Right <= width + 0.5, $"la carte tient dans la fenêtre : de {place.Left:0} à {place.Right:0} sur {width}");
            Assert.AreEqual("Premiers pas, 1 sur 3", AutomationProperties.GetName(card));
            var texts = Descendants(card).OfType<TextBlock>().Where(text => text.IsVisible).ToList();
            Assert.IsTrue(texts.All(text => text.TranslatePoint(new Point(text.ActualWidth, 0), page).X <= width + 0.5), "aucun texte ne dépasse");
            Assert.IsTrue(texts.Any(text => text.Text == "1 sur 3"));
            var links = Descendants(card).OfType<Button>().Where(button => button.IsVisible).ToList();
            Assert.HasCount(3, links, "le premier pas est fait : « Nouvelle fiche » n'y est plus, restent le guide, le coffre d'exemple et « Masquer »");
            Assert.IsTrue(links.All(link => link.ActualHeight >= 44));
            CollectionAssert.AreEqual(LinkNames, links.Select(link => link.Name).ToList(), "dans l'ordre de la maquette 14C");

            // Le coffre d'exemple refusé : la raison se lit dans la carte ; ouvert, elle s'efface.
            var problem = Descendants(card).OfType<TextBlock>().Single(text => text.Name == "FirstStepsSampleProblem");
            Assert.IsFalse(problem.IsVisible);
            links[1].Command.Execute(null);
            UiThread.Until(() => steps.SampleProblem is not null);
            TestShell.Flush();
            Assert.IsTrue(problem.IsVisible);
            Assert.AreEqual("Ce dossier n'est pas vide.", problem.Text);
            // Le pas fait est coché, le suivant est désigné.
            var dots = Descendants(card).OfType<Border>().Where(border => border.Name == "Dot").ToList();
            Assert.HasCount(3, dots);
            Assert.AreSame(page.FindResource("Kyf.Brush.Primary"), dots[0].Background);
            Assert.AreSame(page.FindResource("Kyf.Brush.NavigationActiveBackground"), dots[1].Background);
            Assert.IsNull(dots[2].Background);
            host.Capture($"premiers-pas-{width}-{(dark ? "sombre" : "clair")}");
            steps.OpenSampleVaultCommand.Execute(null);
            UiThread.Until(() => steps.SampleProblem is null);
            TestShell.Flush();
            Assert.IsFalse(problem.IsVisible);
            Assert.AreEqual(2, asked);

            // Un groupe replié : son chevron tourne d'un quart au lieu de changer de glyphe (rangée 19).
            var group = model.Rows.OfType<DashboardGroup>().First(candidate => candidate.Count > 0);
            Icon Chevron() => Descendants(page).OfType<Icon>().Single(icon => icon.Name == "Chevron" && ReferenceEquals(icon.DataContext, group));
            Assert.AreEqual(0, ((RotateTransform)Chevron().RenderTransform).Angle);
            group.ToggleCommand.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(Icons.ChevronDown, Chevron().Text);
            Assert.AreEqual(Transitions.FoldedAngle, ((RotateTransform)Chevron().RenderTransform).Angle);
            host.Capture($"aujourdhui-groupe-replie-{width}-{(dark ? "sombre" : "clair")}");
            var without = new FirstSteps(new FirstUse());
            Assert.IsFalse(without.CanOpenSample, "sans emplacement prévu, pas de lien");
            without.OpenSampleVaultCommand.Execute(null);
            Assert.IsNull(without.SampleProblem);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_guide_shipped_with_the_application_is_cut_into_its_parts()
    {
        var guide = new GuideViewModel();

        // Les parties sont les titres de niveau 2 du guide du dépôt, précédés de sa présentation.
        var written = System.IO.File.ReadAllLines(System.IO.Path.Combine(RepositoryRoot(), "docs", "guide-utilisateur.md"))
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal)).Select(line => line[3..].Trim()).ToList();
        CollectionAssert.AreEqual(written.Prepend(GuideText.Introduction).ToList(), guide.Sections.Select(section => section.Title).ToList());
        Assert.IsGreaterThan(8, guide.Sections.Count);
        Assert.AreSame(guide.Sections[0], guide.Selected, "la présentation d'abord");
        Assert.AreEqual(GuideText.Introduction, guide.Selected!.ToString());

        // Chaque partie se lit comme une fiche, sans rien qui manque.
        foreach (var section in guide.Sections)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(section.Reading.Map.Text), section.Title);
            var prepared = section.Reading;
            Assert.AreSame(prepared, guide.Sections.Single(other => other.Title == section.Title).Reading, "préparée une fois");
        }
        StringAssert.Contains(guide.Sections.Single(section => section.Title == "Réviser").Reading.Map.Text, "J0", StringComparison.Ordinal);

        // « Voir comment faire » mène à une partie par son titre ; un titre inconnu ne change rien.
        var changes = 0;
        guide.PropertyChanged += (_, _) => changes++;
        guide.Show("réviser");
        Assert.AreEqual("Réviser", guide.Selected!.Title);
        guide.Show("Inconnue");
        Assert.AreEqual("Réviser", guide.Selected.Title);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public void The_guide_keeps_the_words_of_a_link_that_leads_nowhere_here()
    {
        var sections = GuideViewModel.Split("# Guide\n\nBonjour.\n\n## Protéger\n\nVoir [le modèle de menace](chiffrement.md#le-modele) et [le site](https://exemple.org).\n\n## Vide\n\n## Fin\n\nAu revoir.\r\n");

        CollectionAssert.AreEqual(new[] { GuideText.Introduction, "Protéger", "Fin" }, sections.Select(section => section.Title).ToArray(), "une partie sans texte n'est pas montrée");
        Assert.IsEmpty(GuideViewModel.Split(string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => GuideViewModel.Split(null!));

        // Un guide absent du programme se dit, plutôt que de ne rien montrer.
        var missing = new GuideViewModel(() => GuideText.Missing);
        Assert.AreEqual("Guide", missing.Selected!.Title);
        Assert.IsNull(new GuideViewModel(() => string.Empty).Selected);
    }

    [STATestMethod]
    [DataRow(1280, 800, false)]
    [DataRow(470, 540, true)]
    public void The_help_shows_the_shortcuts_or_the_guide_and_holds_in_the_window(int width, int height, bool dark)
    {
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var pane = (GuideView)window.FindName("GuidePane");
            var shortcuts = (ItemsControl)window.FindName("ShortcutList");
            var guideTab = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("GuideTab");
            var shortcutsTab = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("ShortcutsTab");

            // F1 : les raccourcis.
            test.Shell.ShowHelpCommand.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(shortcuts.IsVisible);
            Assert.IsFalse(pane.IsVisible);
            Assert.IsTrue(shortcutsTab.IsChecked == true && guideTab.IsChecked == false);

            // L'onglet « Guide » : le plan et le texte de la première partie, à la place des raccourcis.
            guideTab.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(test.Shell.IsGuideShown);
            Assert.IsTrue(pane.IsVisible);
            Assert.IsFalse(shortcuts.IsVisible);
            Assert.IsFalse(shortcutsTab.IsChecked == true);
            var plan = (ListBox)pane.FindName("Plan");
            var choice = (ComboBox)pane.FindName("PlanChoice");
            Assert.AreEqual(pane.ActualWidth >= GuideView.SidePlanMinimum, plan.IsVisible, "le plan à gauche tant que la place le permet");
            Assert.AreNotEqual(plan.IsVisible, choice.IsVisible, "sinon dans une liste déroulante, jamais les deux");
            Assert.AreEqual(test.Shell.Guide.Sections.Count, plan.Items.Count);
            Assert.AreEqual(GuideText.Plan, AutomationProperties.GetName(plan));

            // Il tient dans la fenêtre, à cette taille comme à la plus petite.
            var place = new Rect(pane.TranslatePoint(default, window), pane.RenderSize);
            Assert.IsTrue(place.Left >= 0 && place.Right <= window.ActualWidth && place.Top >= 0 && place.Bottom <= window.ActualHeight, $"{place} dans {window.ActualWidth:0} × {window.ActualHeight:0}");
            Assert.IsTrue(guideTab.ActualHeight >= 44 && shortcutsTab.ActualHeight >= 44);

            // Une partie choisie dans le plan : son texte remplace l'autre.
            test.Shell.Guide.Show("Réviser");
            TestShell.Flush();
            Assert.AreSame(test.Shell.Guide.Selected, plan.SelectedItem);
            Assert.AreSame(test.Shell.Guide.Selected!.Reading, ((Reading.SheetDocumentViewer)pane.FindName("GuideText")).Reading);
            ((FrameworkElement)window.Content).Capture($"aide-guide-{width}-{(dark ? "sombre" : "clair")}");

            // L'onglet « Raccourcis » y revient.
            shortcutsTab.IsChecked = true;
            TestShell.Flush();
            Assert.IsFalse(test.Shell.IsGuideShown);
            Assert.IsTrue(shortcuts.IsVisible);
        }
        finally
        {
            window.Close();
        }
        Assert.AreEqual((800d, 440d), ShellWindow.GuideSize(new Size(2560, 1440)));
        Assert.AreEqual((374d, 240d), ShellWindow.GuideSize(new Size(470, 540)));
        Assert.AreEqual((240d, 160d), ShellWindow.GuideSize(new Size(200, 200)), "jamais moins que de quoi lire");
    }

    [STATestMethod]
    [DataRow(470, true)]
    [DataRow(1280, false)]
    public void A_first_time_tip_shows_once_and_all_of_them_can_be_turned_off(int width, bool dark)
    {
        using var ui = new UiThread();
        var test = new TestShell();
        test.Design.Theme = dark ? DesignSystem.Tokens.ThemeKind.Dark : DesignSystem.Tokens.ThemeKind.Light;
        var memory = new FirstUse();
        var review = new FirstTip { Mark = FirstUse.ReviewTipRead, Title = FirstTipText.ReviewTitle, Text = FirstTipText.ReviewText, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Right };
        var writing = new FirstTip { Mark = FirstUse.WritingTipRead, Title = FirstTipText.WritingTitle, Text = FirstTipText.WritingText };
        var unmarked = new FirstTip { Title = "Sans marque" };
        var panel = new StackPanel { Children = { review, writing, unmarked } };
        var host = new Border { Width = width, Padding = new Thickness(16), Child = panel, Background = (Brush)test.Design.Dictionary["Kyf.Brush.Background"] };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            // Sans rien pour retenir ce qui a été lu (une vue seule, un essai) : aucune aide ne se montre.
            Assert.IsFalse(review.IsVisible || writing.IsVisible || unmarked.IsVisible);

            // La fenêtre transmet la mémoire à ce qu'elle contient : les aides pas encore lues se montrent.
            FirstTip.SetMemory(host, memory);
            TestShell.Flush();
            Assert.AreSame(memory, FirstTip.GetMemory(review));
            Assert.IsTrue(review.IsVisible && writing.IsVisible);
            Assert.IsFalse(unmarked.IsVisible, "une aide sans marque ne saurait pas se faire oublier : elle ne se montre pas");
            var bubble = (Border)review.FindName("Bubble");
            Assert.AreEqual(FirstTipText.ReviewTitle, AutomationProperties.GetName(bubble));
            Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(bubble));
            Assert.AreEqual(FirstTipText.ReviewText, ((TextBlock)review.FindName("BodyText")).Text);
            var place = new Rect(review.TranslatePoint(default, host), review.RenderSize);
            Assert.IsTrue(place.Left >= 0 && place.Right <= width + 0.5 && place.Width <= 420.5, $"de {place.Left:0} à {place.Right:0} sur {width}");
            var understood = (Button)review.FindName("UnderstoodButton");
            var noMore = (Button)writing.FindName("NoMoreButton");
            Assert.IsTrue(understood.ActualHeight >= 44 && noMore.ActualHeight >= 44);
            host.Capture($"aide-contexte-{width}-{(dark ? "sombre" : "clair")}");

            // Pas encore le moment : elle attend.
            review.IsOffered = false;
            Assert.IsFalse(review.IsVisible);
            review.IsOffered = true;
            Assert.IsTrue(review.IsVisible);

            // « Compris » : celle-là ne revient plus, l'autre reste.
            understood.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.IsTrue(memory.Has(FirstUse.ReviewTipRead));
            Assert.IsFalse(review.IsVisible);
            Assert.IsTrue(writing.IsVisible);

            // « Ne plus montrer d'aide » : plus aucune, sans que les autres soient marquées comme lues.
            noMore.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.IsTrue(memory.Has(FirstUse.NoTips));
            Assert.IsFalse(memory.Has(FirstUse.WritingTipRead));
            Assert.IsFalse(writing.IsVisible);

            // Retirée de l'écran, elle ne suit plus la mémoire ; remise sous une autre, elle la suit.
            panel.Children.Remove(writing);
            FirstTip.SetMemory(host, new FirstUse());
            panel.Children.Add(writing);
            TestShell.Flush();
            Assert.IsTrue(writing.IsVisible && review.IsVisible);
            Assert.ThrowsExactly<ArgumentNullException>(() => FirstTip.GetMemory(null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => FirstTip.SetMemory(null!, memory));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(470, "Light")]
    [DataRow(1280, "Dark")]
    [DataRow(2560, "HighContrast")]
    public void The_first_review_tip_is_a_bubble_pointing_at_validate_at_every_width(int width, string theme)
    {
        using var ui = new UiThread();
        var test = new TestShell();
        test.Design.Theme = Enum.Parse<DesignSystem.Tokens.ThemeKind>(theme);
        var bar = new ReviewBar { DataContext = new ValidatingBar(), VerticalAlignment = VerticalAlignment.Bottom };
        var host = new Grid { Width = width, Height = 360, Children = { bar } };
        host.SetResourceReference(Panel.BackgroundProperty, "Kyf.Brush.Background");
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var window = new Window { Content = new Canvas { Children = { host } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            TestShell.Flush();
            var tip = (FirstTip)bar.FindName("ReviewTip");
            Assert.IsFalse(tip.IsVisible, "sans mémoire, aucune aide");
            FirstTip.SetMemory(host, new FirstUse());
            TestShell.Flush();
            Assert.IsTrue(tip.IsVisible);

            var validate = (Button)bar.FindName("ValidateButton");
            var pointer = (FrameworkElement)tip.FindName("Pointer");
            var bubble = (FrameworkElement)tip.FindName("Bubble");
            var band = (FrameworkElement)bar.FindName("Bar");
            Assert.AreEqual(Visibility.Visible, pointer.Visibility, "la bulle a sa pointe");
            var aim = validate.TranslatePoint(new Point(validate.ActualWidth / 2, 0), host).X;
            var tipOf = pointer.TranslatePoint(new Point(pointer.ActualWidth / 2, pointer.ActualHeight), host);
            Assert.AreEqual(aim, tipOf.X, 1.5, $"à {width} pixels, la pointe désigne « Valider »");
            var bandTop = band.TranslatePoint(default, host).Y;
            Assert.IsLessThanOrEqualTo(bandTop, tipOf.Y, "la pointe s'arrête au-dessus de la bande");
            Assert.IsGreaterThan(bandTop - 12, tipOf.Y, "tout près d'elle");
            var place = new Rect(bubble.TranslatePoint(default, host), bubble.RenderSize);
            Assert.IsTrue(place.Left >= ReviewBar.TipMargin - 0.5 && place.Right <= width - ReviewBar.TipMargin + 0.5 && place.Width <= ReviewBar.TipWidest + 0.5,
                $"la bulle tient dans la bande : de {place.Left:0} à {place.Right:0} sur {width}");
            // Une ombre de menu, comme ce qui flotte ; en contraste élevé, aucune ombre : le trait de la bulle suffit.
            Assert.AreSame(bubble.TryFindResource("Kyf.Elevation.Flyout"), ((Border)tip.FindName("BubbleShadow")).Effect);
            Assert.IsTrue(((Button)tip.FindName("UnderstoodButton")).ActualHeight >= 44);
            host.Capture($"bulle-revision-{width}-{theme}");

            // Sans place donnée, pas de pointe ; une place hors de la bulle reste sur la bulle.
            tip.PointerOffset = double.NaN;
            Assert.AreEqual(Visibility.Collapsed, pointer.Visibility);
            tip.PointerOffset = -50;
            Assert.AreEqual(12, ((FrameworkElement)pointer).Margin.Right);
            tip.PointerOffset = 5000;
            Assert.IsLessThan(tip.ActualWidth, ((FrameworkElement)pointer).Margin.Right);
            bar.PlaceTip();

            // « Compris » : elle part, et ne suit plus la bande.
            ((Button)tip.FindName("UnderstoodButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            TestShell.Flush();
            Assert.IsFalse(tip.IsVisible);
            bar.PlaceTip();
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_review_bar_carries_the_tip_of_the_first_review_and_the_window_hands_it_the_memory()
    {
        var bar = new ReviewBar();
        var tip = (FirstTip)bar.FindName("ReviewTip");
        Assert.AreEqual(FirstUse.ReviewTipRead, tip.Mark);
        Assert.AreEqual(FirstTipText.ReviewTitle, tip.Title);
        Assert.IsNotNull(System.Windows.Data.BindingOperations.GetBinding(tip, FirstTip.IsOfferedProperty), "seulement quand « Valider » est proposé");

        var test = new TestShell();
        var memory = new FirstUse();
        test.Shell.FirstUse = memory;
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.AreSame(memory, FirstTip.GetMemory((DependencyObject)window.FindName("Root")));
            Assert.AreSame(memory, FirstTip.GetMemory((DependencyObject)window.FindName("HelpDialog")), "transmise à ce que la fenêtre contient");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_empty_views_offer_what_to_do_next()
    {
        using var test = new TestVaults(AppMode.Development);
        Assert.IsNotNull(test.App.Vaults.Create(test.PathOf("Coffre")).Vault);

        // Définitions : « Voir comment faire » ouvre le guide là où il parle des passages et des définitions.
        var bare = new DefinitionsPageViewModel(test.App.Annotations, new FakeOpener());
        Assert.IsFalse(bare.CanShowHow);
        bare.ShowHowCommand.Execute(null);
        var asked = new List<string>();
        var definitions = new DefinitionsPageViewModel(test.App.Annotations, new FakeOpener()) { OpenGuide = asked.Add };
        Assert.IsTrue(definitions.CanShowHow);
        definitions.ShowHowCommand.Execute(null);
        CollectionAssert.AreEqual(new[] { DefinitionsPageViewModel.HowSection }, asked);
        Assert.IsTrue(new GuideViewModel().Sections.Any(section => section.Title == DefinitionsPageViewModel.HowSection), "cette partie existe dans le guide livré");
        var how = (Button)new DefinitionsPage().FindName("EmptyHowButton");
        Assert.AreEqual("Voir comment faire", how.Content);

        // Carnet : « Nouvelle fiche », avant « Importer ».
        var carnet = new SheetsPage();
        var newSheet = (Button)carnet.FindName("EmptyNewSheetButton");
        Assert.AreEqual(SheetsText.NewSheet, newSheet.Content);
        Assert.AreEqual("RequestCreateCommand", System.Windows.Data.BindingOperations.GetBinding(newSheet, System.Windows.Controls.Primitives.ButtonBase.CommandProperty)!.Path.Path);
        Assert.IsNotNull(carnet.FindName("EmptyImportButton"));

        // À propos : le guide s'ouvre de là aussi, quand la coquille le propose.
        var about = new AboutSettingsViewModel(test.App.Vaults);
        Assert.IsFalse(about.CanOpenGuide);
        about.OpenGuideNowCommand.Execute(null);
        var guides = 0;
        about.OpenGuide = () => guides++;
        Assert.IsTrue(about.CanOpenGuide);
        about.OpenGuideNowCommand.Execute(null);
        Assert.AreEqual(1, guides);
        Assert.AreEqual(WelcomeText.ReadGuide, ((Button)new AboutSettingsPage().FindName("GuideButton")).Content);
    }

    private static string RepositoryRoot()
    {
        var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(System.IO.Path.Combine(folder.FullName, "KyfRev.slnx")))
        {
            folder = folder.Parent;
        }
        Assert.IsNotNull(folder, "le dépôt, depuis le dossier des essais");
        return folder.FullName;
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

/// <summary>Ce que la bande de révision lit, sans cas d'usage : « Valider » est proposé, rien d'autre ne se montre.</summary>
public sealed record ValidatingBar(
    bool CanValidate = true,
    bool ShowsActiveActions = true,
    bool? ValidateShown = true,
    bool? ForgotShown = true,
    bool ShowsCycle = true,
    bool ShowsSteps = false,
    string ValidateLabel = "Valider J1",
    string StatusTitle = "J1 à valider",
    string StatusDetail = "due aujourd'hui",
    bool IsNote = false,
    bool IsReminder = false,
    bool IsMarking = false,
    bool IsChoosingDay = false,
    bool HasPassages = false,
    bool HasNoPassage = false,
    bool HasError = false,
    bool ShowsResume = false,
    bool ShowsArchivedActions = false,
    bool ShowsReminderChoices = false,
    bool? ShowsNextButton = false,
    bool? ShowsDoneText = false,
    bool ShowsToast = false,
    bool IsSheet = true);
