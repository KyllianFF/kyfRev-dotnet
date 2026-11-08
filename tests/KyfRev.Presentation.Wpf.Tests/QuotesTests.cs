using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using KyfRev.App;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Modules.Citations;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le module Citations dans l'application : sa vue, son activation, son onglet, et ce qu'il apporte ailleurs.</summary>
[TestClass]
public sealed class QuotesTests
{
    private static readonly string[] CoreMenu = ["Aujourd'hui", "Fiches", "Définitions", "Archives", "Statistiques"];

    private static readonly string[] MenuWithQuotes = ["Aujourd'hui", "Fiches", "Définitions", "Archives", "Statistiques", "Citations"];

    private static readonly string[] ByAuthor = ["Albert", "Émile", "Zola", "— Auteur inconnu"];

    private static readonly string[] Chosen = ["art", "science"];

    private static readonly string[] AllTagLabels = ["Toutes les étiquettes", "art", "poésie"];

    private static readonly string[] OpenedAddress = ["https://exemple.org/page"];

    private static readonly string[] WelcomeFirst = ["modules, accueil=True"];

    [TestMethod]
    public void The_view_needs_its_use_cases_and_its_links()
    {
        using var vault = new QuotesVault();
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuotesPageViewModel(null!, new RecordingLinks()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new QuotesPageViewModel(vault.Quotes, null!));
        Assert.AreEqual("Citations", QuotesPageViewModel.Title);
    }

    [TestMethod]
    public async Task A_quote_found_by_the_search_is_shown_alone_until_all_are_asked_for()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Le cœur a ses raisons.", "Pascal");
        vault.Add("Connais-toi toi-même.", "Socrate");
        var page = await vault.LoadedAsync();
        Assert.HasCount(2, page.Rows);
        Assert.IsFalse(page.HasTarget);

        page.ShowOnly(path);

        Assert.IsTrue(page.HasTarget);
        Assert.AreEqual(path, page.Rows.Single().Quote.Path);

        page.ShowAllCommand.Execute(null);

        Assert.IsFalse(page.HasTarget);
        Assert.HasCount(2, page.Rows);
        Assert.ThrowsExactly<ArgumentNullException>(() => page.ShowOnly(null!));
    }

    [TestMethod]
    public async Task Coming_back_to_the_view_shows_every_quote_again()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Le cœur a ses raisons.", "Pascal");
        vault.Add("Connais-toi toi-même.", "Socrate");
        var page = await vault.LoadedAsync();
        page.ShowOnly(path);

        page.OnNavigatedTo();
        await page.ReloadCommand.ExecutionTask!;

        Assert.IsFalse(page.HasTarget);
        Assert.HasCount(2, page.Rows);
    }

    [STATestMethod]
    public void A_quote_opened_from_the_search_goes_to_its_view()
    {
        using var ui = new UiThread();
        using var vault = new QuotesVault();
        var path = vault.Add("Le cœur a ses raisons.", "Pascal");
        vault.Add("Connais-toi toi-même.", "Socrate");
        var test = new TestShell();
        var page = vault.Page();
        var shell = new ShellViewModel(
            CorePages.All().Append(CorePages.FromModule(new NavigationEntry("citations", "Citations", 60)) with { Page = page }),
            test.Appearance,
            test.Gallery);
        shell.ShowWorkspace();

        new SearchHitOpener(new FakeOpener(), shell).Open(new SearchHit(SearchKinds.Quote, path, "Pascal", []));
        UiThread.Until(() => page.ReloadCommand.ExecutionTask is { IsCompleted: true } && page.HasRows);

        Assert.AreSame(page, shell.Current.Page);
        Assert.IsTrue(page.HasTarget);
        Assert.AreEqual(path, page.Rows.Single().Quote.Path);
    }

    [TestMethod]
    public async Task An_empty_vault_says_only_the_text_is_needed()
    {
        using var vault = new QuotesVault();
        var page = vault.Page();
        Assert.IsFalse(page.IsEmpty, "rien n'a encore été lu");

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsEmpty);
        Assert.IsFalse(page.HasQuotes);
        Assert.IsFalse(page.HasRows);
        Assert.IsFalse(page.HasNoMatch);
        Assert.IsFalse(page.HasTags);
        Assert.IsFalse(page.HasTrash);
        Assert.IsFalse(page.HasUnreadable);
        Assert.AreEqual("0 citation", page.Summary);
    }

    [TestMethod]
    public async Task A_closed_vault_says_why()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = new QuotesVault.Module(vaults.App).Page();

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.IsEmpty);
    }

    [TestMethod]
    public async Task The_form_adds_only_with_a_text_and_keeps_what_was_typed()
    {
        using var vault = new QuotesVault();
        var page = vault.Page();
        await page.ReloadCommand.ExecuteAsync(null);

        page.NewQuoteCommand.Execute(null);
        Assert.IsTrue(page.IsFormOpen);
        Assert.AreEqual("Nouvelle citation", page.FormTitle);
        page.Author = "Voltaire";
        page.Text = "   ";
        await page.AnswerFormCommand.ExecuteAsync(true);
        Assert.IsTrue(page.IsFormOpen, "un refus laisse le formulaire ouvert");
        Assert.IsTrue(page.HasFormError);
        Assert.AreEqual("Le texte de la citation est obligatoire.", page.FormError);
        Assert.AreEqual("Voltaire", page.Author);

        page.Text = " Il faut cultiver notre jardin. ";
        page.Date = "1759";
        page.SourceKind = QuotesPageViewModel.SourceKinds[1];
        page.SourceTitle = "Candide";
        page.SourcePage = "12";
        page.SourceAddress = "https://exemple.org/candide";
        page.Context = "La fin du conte.";
        page.TagsText = "philosophie, , conte";
        await page.AnswerFormCommand.ExecuteAsync(true);

        Assert.IsFalse(page.IsFormOpen);
        Assert.IsFalse(page.HasFormError);
        Assert.AreEqual("Citation ajoutée.", page.Message);
        Assert.IsTrue(page.HasMessage);
        Assert.AreEqual("1 citation", page.Summary);
        var row = page.Rows.Single();
        Assert.AreEqual("Il faut cultiver notre jardin.", row.Text);
        Assert.AreEqual("— Voltaire", row.Attribution);
        Assert.AreEqual("Livre : Candide, p. 12 · 1759 · #philosophie · #conte", row.Reference);
        Assert.IsTrue(row.HasContext);
        Assert.IsTrue(row.HasAdded);
        StringAssert.StartsWith(row.Added, "ajoutée le ", StringComparison.Ordinal);
        Assert.AreEqual("Modifier la citation : Il faut cultiver notre jardin.", row.EditName);
        Assert.AreEqual("Supprimer la citation : Il faut cultiver notre jardin.", row.TrashName);
        Assert.AreEqual("Ouvrir la source de la citation : Il faut cultiver notre jardin.", row.SourceName);
    }

    [TestMethod]
    public async Task Cancelling_the_form_adds_nothing()
    {
        using var vault = new QuotesVault();
        var page = vault.Page();
        await page.ReloadCommand.ExecuteAsync(null);
        page.NewQuoteCommand.Execute(null);
        page.Text = "Rien.";

        await page.AnswerFormCommand.ExecuteAsync(false);

        Assert.IsFalse(page.IsFormOpen);
        Assert.IsEmpty(vault.Quotes.Quotes().Value.Quotes);
    }

    [TestMethod]
    public async Task The_form_opens_on_the_quote_and_saves_its_changes()
    {
        using var vault = new QuotesVault();
        vault.Add("Un texte.", "Hugo", tags: ["poésie"], kind: SourceKind.Book, title: "Les Contemplations");
        var page = await vault.LoadedAsync();

        page.Rows[0].Edit.Execute(null);

        Assert.AreEqual("Modifier la citation", page.FormTitle);
        Assert.AreEqual("Un texte.", page.Text);
        Assert.AreEqual("Hugo", page.Author);
        Assert.AreEqual(SourceKind.Book, page.SourceKind.Value);
        Assert.AreEqual("Les Contemplations", page.SourceTitle);
        Assert.AreEqual("poésie", page.TagsText);

        page.Text = "Un autre texte.";
        await page.AnswerFormCommand.ExecuteAsync(true);

        Assert.AreEqual("Citation modifiée.", page.Message);
        Assert.AreEqual("Un autre texte.", page.Rows.Single().Text);
    }

    [TestMethod]
    public async Task A_change_refused_by_the_vault_keeps_the_form_open()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Un texte.");
        var page = await vault.LoadedAsync();
        page.Rows[0].Edit.Execute(null);
        vault.Quotes.Trash(path).Value.ToString();

        await page.AnswerFormCommand.ExecuteAsync(true);

        Assert.IsTrue(page.IsFormOpen);
        Assert.IsTrue(page.HasFormError);
    }

    [TestMethod]
    [DataRow(null, null, null, null, "Source inconnue · date inconnue")]
    [DataRow(SourceKind.Unknown, null, null, null, "Source inconnue · date inconnue")]
    [DataRow(SourceKind.Book, null, null, "1670", "Livre · 1670")]
    [DataRow(null, "Pensées", null, "1670", "Pensées · 1670")]
    [DataRow(SourceKind.Unknown, "Pensées", "5", null, "Pensées, p. 5 · date inconnue")]
    [DataRow(SourceKind.Film, "Metropolis", null, "1927", "Film : Metropolis · 1927")]
    public void The_reference_says_what_is_missing(SourceKind? kind, string? title, string? page, string? date, string expected)
    {
        var content = new QuoteContent("Texte", null, date, new QuoteSource(kind, title, page, null), null, []);

        Assert.AreEqual(expected, QuotesPageViewModel.ReferenceOf(content));
    }

    [TestMethod]
    public async Task The_search_ignores_accents_and_looks_everywhere()
    {
        using var vault = new QuotesVault();
        vault.Add("Être ou ne pas être.", "Shakespeare", tags: ["théâtre"]);
        vault.Add("Je pense, donc je suis.", "Descartes", context: "Discours de la méthode");
        var page = await vault.LoadedAsync();

        page.Search = "etre";
        Assert.AreEqual("Être ou ne pas être.", page.Rows.Single().Text);

        page.Search = "THEATRE";
        Assert.AreEqual("Être ou ne pas être.", page.Rows.Single().Text);

        page.Search = "méthode";
        Assert.AreEqual("Descartes", page.Rows.Single().Quote.Content.Author);

        page.Search = "introuvable";
        Assert.IsEmpty(page.Rows);
        Assert.IsTrue(page.HasNoMatch);
        Assert.IsFalse(page.IsEmpty);
    }

    [TestMethod]
    public async Task The_tags_filter_and_survive_a_reload()
    {
        using var vault = new QuotesVault();
        vault.Add("Un.", tags: ["art", "poésie"]);
        vault.Add("Deux.", tags: ["art"]);
        vault.Add("Trois.");
        var page = await vault.LoadedAsync();

        Assert.IsTrue(page.HasTags);
        CollectionAssert.AreEqual(AllTagLabels, page.Tags.Select(tag => tag.Label).ToArray());
        Assert.HasCount(3, page.Rows);

        page.Tag = page.Tags[2];
        Assert.AreEqual("Un.", page.Rows.Single().Text);

        await page.ReloadCommand.ExecuteAsync(null);
        Assert.AreEqual("poésie", page.Tag!.Value);
        Assert.AreEqual("Un.", page.Rows.Single().Text);

        page.Tag = null;
        Assert.HasCount(3, page.Rows, "une liste vidée ne filtre rien");
    }

    [TestMethod]
    public async Task The_sorts_put_what_is_missing_last()
    {
        using var vault = new QuotesVault();
        vault.Add("A.", "Zola", date: "1885");
        vault.Add("B.", "Émile", date: "IIIe siècle av. J.-C.");
        vault.Add("C.", "Albert");
        vault.Add("D.", date: "1670");
        var page = await vault.LoadedAsync();

        page.Sort = QuotesPageViewModel.Sorts[1];
        CollectionAssert.AreEqual(ByAuthor, page.Rows.Select(row => row.Quote.Content.Author ?? "— Auteur inconnu").ToArray());
        Assert.AreEqual("— Auteur inconnu", page.Rows[3].Attribution);

        page.Sort = QuotesPageViewModel.Sorts[2];
        Assert.AreEqual("C.", page.Rows[3].Text, "sans date, à la fin");
        Assert.IsLessThan(page.Rows.ToList().FindIndex(row => row.Text == "A."), page.Rows.ToList().FindIndex(row => row.Text == "D."));

        page.Sort = QuotesPageViewModel.Sorts[0];
        Assert.HasCount(4, page.Rows);
    }

    [TestMethod]
    public async Task A_deleted_quote_goes_to_the_trash_and_comes_back()
    {
        using var vault = new QuotesVault();
        vault.Add("Un texte assez long pour que le nom de ses boutons soit coupé quelque part.", "Hugo");
        var page = await vault.LoadedAsync();
        StringAssert.EndsWith(page.Rows[0].TrashName, "…", StringComparison.Ordinal);

        await page.AnswerTrashCommand.ExecuteAsync(true);
        Assert.HasCount(1, page.Rows, "rien n'a été demandé");

        page.Rows[0].Trash.Execute(null);
        Assert.IsTrue(page.IsConfirmingTrash);
        await page.AnswerTrashCommand.ExecuteAsync(false);
        Assert.IsFalse(page.IsConfirmingTrash);
        Assert.HasCount(1, page.Rows);

        page.Rows[0].Trash.Execute(null);
        await page.AnswerTrashCommand.ExecuteAsync(true);

        Assert.AreEqual("Citation placée dans la corbeille.", page.Message);
        Assert.IsEmpty(page.Rows);
        Assert.IsTrue(page.IsEmpty);
        Assert.IsTrue(page.HasTrash);
        Assert.AreEqual("Corbeille (1)", page.TrashTitle);
        var trashed = page.Trash.Single();
        StringAssert.EndsWith(trashed.Excerpt, " » — Hugo", StringComparison.Ordinal);
        StringAssert.StartsWith(trashed.RestoreName, "Restaurer : « Un texte", StringComparison.Ordinal);

        await ((IAsyncRelayCommand)trashed.Restore).ExecuteAsync(null);

        Assert.AreEqual("Citation restaurée.", page.Message);
        Assert.HasCount(1, page.Rows);
        Assert.IsFalse(page.HasTrash);
    }

    [TestMethod]
    public async Task A_refusal_says_why_and_coming_back_forgets_it()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Un texte.");
        var page = await vault.LoadedAsync();
        page.Rows[0].Trash.Execute(null);
        vault.Quotes.Trash(path).Value.ToString();

        await page.AnswerTrashCommand.ExecuteAsync(true);

        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasMessage);

        page.OnNavigatedTo();
        Assert.IsFalse(page.HasError);
        await page.ReloadCommand.ExecutionTask!;
    }

    [TestMethod]
    public async Task Only_a_web_address_opens_and_a_page_of_wikiquote_says_so()
    {
        using var vault = new QuotesVault();
        vault.Add("Avec lien.", address: "https://exemple.org/page");
        vault.Add("Sans lien.", address: "dans un carnet");
        var links = new RecordingLinks();
        var page = await vault.LoadedAsync(links);

        var linked = page.Rows.Single(row => row.Text == "Avec lien.");
        Assert.IsTrue(linked.HasSource);
        Assert.AreEqual("Source", linked.SourceLabel);
        linked.OpenSource!.Execute(null);
        CollectionAssert.AreEqual(OpenedAddress, links.Opened);
        Assert.IsFalse(page.Rows.Single(row => row.Text == "Sans lien.").HasSource);

        var online = new Quote("citations/w.md", null, new QuoteContent("T", null, null, QuoteSource.None, null, []), null, null, false, "wikiquote");
        Assert.AreEqual("Wikiquote", new QuoteRow(online, string.Empty, null, null, new RelayCommand(() => { }), new RelayCommand(() => { })).SourceLabel);
    }

    [TestMethod]
    public async Task An_unreadable_file_is_named_and_left_alone()
    {
        using var vault = new QuotesVault();
        vault.Write("citations/Abîmée.md", "---\nauteur: [\n---\nTexte.\n");

        var page = await vault.LoadedAsync();

        Assert.IsTrue(page.HasUnreadable);
        Assert.AreEqual("1 fichier illisible dans « citations » : citations/Abîmée.md. kyfRev ne les modifie pas.", page.Unreadable);

        vault.Write("citations/Autre.md", "---\nauteur: [\n---\nTexte.\n");
        await page.ReloadCommand.ExecuteAsync(null);
        StringAssert.StartsWith(page.Unreadable, "2 fichiers illisibles", StringComparison.Ordinal);
    }

    [STATestMethod]
    public void The_view_shows_its_cards_and_answers_its_dialogs()
    {
        using var ui = new UiThread();
        using var vault = new QuotesVault();
        vault.Add("Un.", "Hugo", tags: ["poésie"], address: "https://exemple.org");
        vault.Add("Deux.");
        vault.Add("Trois.");
        var test = new TestShell();
        var model = vault.Page();
        var page = new QuotesPage { DataContext = model };
        var host = new Border { Width = 1000, Height = 800, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 800, ShowActivated = false };
        window.Show();
        model.OnNavigatedTo();
        try
        {
            UiThread.Until(() => model.HasRows);
            TestShell.Flush();
            Assert.AreEqual("Citations", page.Heading.Text);
            Assert.AreEqual(Visibility.Visible, page.TagChoice.Visibility);
            Assert.AreEqual(Visibility.Collapsed, page.Empty.Visibility);
            Assert.IsGreaterThan(400d, page.CardWidth);
            Assert.IsLessThanOrEqualTo(page.Scroller.ViewportWidth, page.Scroller.ExtentWidth);

            host.Width = 500;
            TestShell.Flush();
            Assert.IsLessThan(500d, page.CardWidth);
            Assert.IsLessThanOrEqualTo(page.Scroller.ViewportWidth, page.Scroller.ExtentWidth);

            model.NewQuoteCommand.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(page.Form.IsOpen);
            page.Form.Close(DialogChoice.Secondary);
            TestShell.Flush();
            Assert.IsFalse(model.IsFormOpen);

            model.Rows[0].Trash.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(page.TrashConfirmation.IsOpen);
            page.TrashConfirmation.Close(DialogChoice.Primary);
            UiThread.Until(() => model.HasTrash);

            page.DataContext = null;
            page.Form.Close(DialogChoice.None);
            page.TrashConfirmation.Close(DialogChoice.None);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_modules_tab_needs_its_settings_and_its_action()
    {
        using var vault = new QuotesVault();
        Assert.ThrowsExactly<ArgumentNullException>(() => new ModulesSettingsViewModel(null!, _ => { }, FakeExtensions.Settings()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ModulesSettingsViewModel(vault.App.Settings, null!, FakeExtensions.Settings()));
    }

    [TestMethod]
    public void The_modules_tab_shows_what_the_vault_says_and_changing_nothing_writes_nothing()
    {
        using var vault = new QuotesVault();
        vault.App.Settings.Update(preferences => preferences with { QuotesActive = true, QuotesOnline = true, QuoteDomains = ["art"] }).Value.ToString();
        var applied = new List<VaultPreferences>();
        var page = new ModulesSettingsViewModel(vault.App.Settings, applied.Add, FakeExtensions.Settings());

        page.OnNavigatedTo();

        Assert.IsTrue(page.QuotesActive);
        Assert.IsTrue(page.QuotesOnline);
        Assert.IsTrue(page.ShowsDomains);
        Assert.IsTrue(page.Domains.Single(domain => domain.Name == "art").IsChosen);
        Assert.IsFalse(page.Domains.Single(domain => domain.Name == "science").IsChosen);
        StringAssert.Contains(page.Domains[0].Pages, "Philosophie", StringComparison.Ordinal);
        Assert.AreEqual("Philosophie", page.Domains[0].Label);
        Assert.IsEmpty(applied, "montrer n'applique rien");
    }

    [TestMethod]
    public void Activating_a_module_loads_it_and_deactivating_deletes_nothing()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Un texte.");
        var applied = new List<VaultPreferences>();
        var page = new ModulesSettingsViewModel(vault.App.Settings, applied.Add, FakeExtensions.Settings());
        page.OnNavigatedTo();

        page.QuotesActive = true;

        Assert.AreEqual("Module Citations activé.", page.Message);
        Assert.IsTrue(page.HasMessage);
        Assert.IsTrue(vault.App.Settings.Get().Value.QuotesActive);
        Assert.IsTrue(applied.Single().QuotesActive);

        page.QuotesActive = false;

        Assert.AreEqual("Module Citations désactivé : rien n'a été supprimé.", page.Message);
        Assert.IsFalse(vault.App.Settings.Get().Value.QuotesActive);
        Assert.AreEqual(path, vault.Quotes.Quotes().Value.Quotes.Single().Path);
    }

    [TestMethod]
    public void The_online_search_and_its_domains_are_kept_at_once()
    {
        using var vault = new QuotesVault();
        var page = new ModulesSettingsViewModel(vault.App.Settings, _ => { }, FakeExtensions.Settings());
        page.OnNavigatedTo();

        page.QuotesOnline = true;
        Assert.IsTrue(vault.App.Settings.Get().Value.QuotesOnline);
        Assert.IsFalse(page.HasMessage);

        foreach (var domain in page.Domains)
        {
            domain.IsChosen = Chosen.Contains(domain.Name);
        }

        CollectionAssert.AreEquivalent(Chosen, vault.App.Settings.Get().Value.QuoteDomains.ToArray());
    }

    [TestMethod]
    public void A_refused_change_says_why_and_shows_what_is_kept()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = new ModulesSettingsViewModel(vaults.App.Settings, _ => { }, FakeExtensions.Settings());

        page.OnNavigatedTo();
        Assert.IsTrue(page.HasError, "aucun coffre ouvert");

        page.QuotesActive = true;
        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasMessage);
    }

    [STATestMethod]
    public void The_modules_tab_hides_the_options_of_an_inactive_module()
    {
        using var ui = new UiThread();
        using var vault = new QuotesVault();
        var test = new TestShell();
        var model = new ModulesSettingsViewModel(vault.App.Settings, _ => { }, FakeExtensions.Settings());
        model.OnNavigatedTo();
        var page = new ModulesSettingsPage { DataContext = model };
        var host = new Border { Width = 640, Height = 700, Child = page };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 800, Height = 800, ShowActivated = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.AreEqual(Visibility.Collapsed, page.QuotesOptions.Visibility);
            Assert.IsTrue(page.SchemasBox.IsEnabled);

            model.QuotesActive = true;
            model.QuotesOnline = true;
            TestShell.Flush();

            Assert.AreEqual(Visibility.Visible, page.QuotesOptions.Visibility);
            Assert.AreEqual(Visibility.Visible, page.DomainsPanel.Visibility);
            Assert.IsLessThanOrEqualTo(page.Scroller.ViewportWidth, page.Scroller.ExtentWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_active_modules_need_their_catalog_and_the_core()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ActiveModules(null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ActiveModules(new ModuleCatalog([]), null!));
    }

    [TestMethod]
    public void A_module_has_services_only_while_it_is_active()
    {
        using var vault = new QuotesVault();
        using var modules = vault.ActiveModules();
        var changes = 0;
        modules.Changed += (_, _) => changes++;
        Assert.IsNull(modules.Find<QuoteUseCases>(), "rien n'est encore chargé");
        Assert.IsEmpty(modules.All<IDashboardWidget>());
        Assert.IsEmpty(modules.Widgets);

        modules.Load(vault.App.Settings.Get().Value);
        Assert.AreEqual(1, changes);
        Assert.IsNull(modules.Find<QuoteUseCases>(), "le module est désactivé par défaut");

        modules.Load(vault.App.Settings.Update(preferences => preferences with { QuotesActive = true }).Value);
        Assert.IsNotNull(modules.Find<QuoteUseCases>());
        Assert.HasCount(1, modules.Widgets);
        Assert.HasCount(1, modules.All<INavigationContributor>());

        modules.Load(null);
        Assert.AreEqual(3, changes);
        Assert.IsNull(modules.Find<QuoteUseCases>(), "aucun coffre ouvert");
    }

    [STATestMethod]
    public void The_application_adds_the_menu_entry_when_the_module_is_activated()
    {
        using var services = KyfRev.App.Composition.Build();
        var shell = services.GetRequiredService<ShellViewModel>();
        var modules = services.GetRequiredService<ActiveModules>();
        CollectionAssert.AreEqual(CoreMenu, shell.MainNavigation.Select(item => item.Label).ToArray());

        modules.Load(Preferences(quotes: true));

        CollectionAssert.AreEqual(MenuWithQuotes, shell.MainNavigation.Select(item => item.Label).ToArray());
        Assert.IsInstanceOfType<QuotesPageViewModel>(shell.MainNavigation[5].Page);

        modules.Load(Preferences(quotes: false));

        CollectionAssert.AreEqual(CoreMenu, shell.MainNavigation.Select(item => item.Label).ToArray());
    }

    [STATestMethod]
    public void A_contributed_entry_without_a_view_of_ours_keeps_its_place()
    {
        var modules = new ActiveModules(new ModuleCatalog([new OtherModule()]), _ => { });
        modules.Load(Preferences(quotes: false));

        var entry = KyfRev.App.Composition.ModuleEntries(modules, new RecordingLinks(), _ => throw new InvalidOperationException()).Single();

        Assert.AreEqual("Autre", entry.Label);
        Assert.IsInstanceOfType<PlaceholderPageViewModel>(entry.Page);
        modules.Dispose();
    }

    [STATestMethod]
    public void The_schemas_module_brings_its_view_and_its_icon()
    {
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var modules = new ActiveModules(new ModuleCatalog([new KyfRev.Modules.Schemas.SchemasModule()]), services => services.AddSingleton(vaults.App.Host));
        modules.Load(Preferences(quotes: false) with { SchemasActive = true });

        var entry = KyfRev.App.Composition.ModuleEntries(modules, new RecordingLinks(), schemas => new SchemasPageViewModel(schemas, vaults.Picker, new FakeOpener(), FakeEnvironment.WebData)).Single();

        Assert.AreEqual("Schémas réseau", entry.Label);
        Assert.AreEqual(KyfRev.DesignSystem.Controls.Icons.Schema, entry.Glyph);
        Assert.IsInstanceOfType<SchemasPageViewModel>(entry.Page);
        modules.Dispose();
    }

    [STATestMethod]
    public void The_menu_follows_the_modules_and_the_open_view_stays_chosen()
    {
        var test = new TestShell();
        var shell = test.Shell;
        var settings = shell.SecondaryNavigation.Single(item => item.Id == CorePages.Settings);
        shell.Navigate(settings);
        var quotes = CorePages.FromModule(new NavigationEntry("citations", "Citations", 60));
        Assert.ThrowsExactly<ArgumentNullException>(() => shell.SetModuleEntries(null!));

        shell.SetModuleEntries([quotes]);

        CollectionAssert.AreEqual(MenuWithQuotes, shell.MainNavigation.Select(item => item.Label).ToArray());
        CollectionAssert.Contains(shell.OverflowNavigation.ToList(), quotes);
        Assert.AreSame(settings, shell.Current);
        Assert.AreSame(settings, shell.SelectedSecondary);
        Assert.IsNull(shell.SelectedMain);
        shell.PaletteQuery = "Citations";
        Assert.IsTrue(shell.PaletteResults.Any(entry => entry.Label.Contains("Citations", StringComparison.Ordinal)), "la recherche de commandes aussi");
        shell.PaletteQuery = string.Empty;

        shell.Navigate(quotes);
        Assert.AreSame(quotes, shell.SelectedMain);

        shell.SetModuleEntries([]);

        Assert.AreSame(shell.Home, shell.Current, "la vue d'un module qui s'en va se ferme");
        CollectionAssert.AreEqual(CoreMenu, shell.MainNavigation.Select(item => item.Label).ToArray());

        shell.SetModuleEntries([quotes]);
        Assert.AreSame(shell.Home, shell.SelectedMain);
    }

    [STATestMethod]
    public void Opening_a_vault_loads_its_modules_first()
    {
        var test = new TestShell();
        var order = new List<string>();
        test.Shell.WorkspaceOpening += (_, _) => order.Add($"modules, accueil={test.Shell.IsWelcome}");
        test.Shell.ShowWelcome(new object());

        test.Shell.ShowWorkspace();

        CollectionAssert.AreEqual(WelcomeFirst, order);
        Assert.IsFalse(test.Shell.IsWelcome);
    }

    [TestMethod]
    public async Task The_dashboard_rereads_its_cards_and_shows_what_the_module_brought()
    {
        using var vault = new QuotesVault();
        var widgets = new List<IDashboardWidget>();
        var looked = 0;
        var dashboard = new DashboardPageViewModel(vault.App.Reminders, vault.App.Sheets, new FakeOpener(), widgets)
        {
            LookForNews = () =>
            {
                looked++;
                widgets.Add(new NewsCard());
                return Task.FromResult(looked == 1);
            },
        };

        await dashboard.RefreshCommand.ExecuteAsync(null);

        Assert.AreEqual("Nouveau", dashboard.Cards.Single().Title, "ce qui vient d'arriver s'affiche sans attendre");

        await dashboard.RefreshCommand.ExecuteAsync(null);

        Assert.HasCount(1, dashboard.Cards, "rien de neuf : la recherche n'a rien changé");

        await dashboard.RefreshCommand.ExecuteAsync(null);

        Assert.HasCount(2, dashboard.Cards, "les encarts sont relus à chaque fois");
        Assert.AreEqual(3, looked);
    }

    [TestMethod]
    public async Task The_vault_health_shows_what_the_active_modules_notice()
    {
        using var vault = new QuotesVault();
        vault.Write("citations/Abîmée.md", "---\nauteur: [\n---\nTexte.\n");
        using var modules = vault.ActiveModules();
        var app = vault.App;
        var page = new HealthPageViewModel(app.Maintenance, app.Backups, app.Sheets, new FakeOpener(), () => string.Empty, _ => { }, modules.HealthChecks);

        await page.RefreshCommand.ExecuteAsync(null);
        Assert.IsTrue(page.IsHealthy);
        Assert.IsEmpty(page.Sections, "le module n'est pas actif");

        modules.Load(app.Settings.Update(preferences => preferences with { QuotesActive = true }).Value);
        await page.RefreshCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsHealthy, "une remarque n'attend aucune décision");
        var section = page.Sections.Single();
        Assert.IsTrue(section.IsInformation);
        Assert.AreEqual("citations/Abîmée.md", section.Rows.Single().Title);
        StringAssert.StartsWith(section.Rows.Single().Detail, "Citation illisible : ", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task The_data_tab_shows_the_deleted_quotes_and_their_export()
    {
        using var vault = new QuotesVault();
        var path = vault.Add("Un texte.");
        vault.Quotes.Trash(path).Value.ToString();
        var app = vault.App;
        var page = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vault.Vaults.Picker, new FakeOpener(), () => { }, () => vault.Quotes);

        await page.ReloadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.ExportsQuotes);
        var row = page.Trash.Single();
        Assert.AreEqual("« Un texte. »", row.Title);
        Assert.AreEqual("Citation · Auteur inconnu", row.Detail);
        Assert.IsFalse(row.HasSheet);

        await ((IAsyncRelayCommand)row.Restore).ExecuteAsync(null);

        Assert.AreEqual("Citation restaurée.", page.Message);
        Assert.IsEmpty(page.Trash);
        Assert.HasCount(1, vault.Quotes.Quotes().Value.Quotes);

        var inactive = new DataSettingsViewModel(app.Vaults, app.Backups, app.Trash, app.Annotations, app.Exports, app.Settings, vault.Vaults.Picker, new FakeOpener(), () => { });
        await inactive.ReloadCommand.ExecuteAsync(null);
        Assert.IsFalse(inactive.ExportsQuotes);
    }

    private static VaultPreferences Preferences(bool quotes) =>
        new([1, 3], false, 2, 30, [2], false, true, quotes, false, [], false);

    private sealed class OtherModule : IKyfRevModule
    {
        public string Id => "autre";

        public string Name => "Autre";

        public bool IsActiveFor(VaultPreferences preferences) => true;

        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<INavigationContributor>(new OtherEntry());
    }

    private sealed class OtherEntry : INavigationContributor
    {
        public NavigationEntry Entry { get; } = new("autre", "Autre", 70);
    }

    private sealed class NewsCard : IDashboardWidget
    {
        public string Id => "nouveau";

        public int Order => 1;

        public DashboardCard? Card() => new("Nouveau", "Texte", null);
    }

    /// <summary>La vue et les cas d'usage du module sur un vrai coffre, sans réseau.</summary>
    private sealed class QuotesVault : IDisposable
    {
        public QuotesVault()
        {
            Vaults = new TestVaults(AppMode.Development);
            Root = Vaults.PathOf("Coffre");
            Vaults.App.Vaults.Create(Root);
            Quotes = new Module(App).Quotes;
        }

        public TestVaults Vaults { get; }

        public string Root { get; }

        public KyfRevApplication App => Vaults.App;

        public QuoteUseCases Quotes { get; }

        public ActiveModules ActiveModules() => new(new ModuleCatalog([new CitationsModule()]), services =>
        {
            services.AddSingleton(App.Host);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IOnlineReader>(new NoReader());
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        });

        public QuotesPageViewModel Page(RecordingLinks? links = null) => new(Quotes, links ?? new RecordingLinks());

        public async Task<QuotesPageViewModel> LoadedAsync(RecordingLinks? links = null)
        {
            var page = Page(links);
            await page.ReloadCommand.ExecuteAsync(null);
            return page;
        }

        public string Add(string text, string? author = null, string? date = null, string[]? tags = null, SourceKind? kind = null, string? title = null, string? address = null, string? context = null) =>
            Quotes.Add(new QuoteContent(text, author, date, new QuoteSource(kind, title, null, address), context, [.. tags ?? []])).Value.Path;

        public void Write(string relative, string content)
        {
            var path = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public void Dispose() => Vaults.Dispose();

        internal sealed class Module(KyfRevApplication app)
        {
            public QuoteUseCases Quotes { get; } = new(app.Host, TimeProvider.System, new OnlineQuoteSearch(app.Host, new NoReader(), TimeProvider.System, NullLogger<OnlineQuoteSearch>.Instance));

            public QuotesPageViewModel Page() => new(Quotes, new RecordingLinks());
        }
    }

    private sealed class NoReader : IOnlineReader
    {
        public Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken) => throw new HttpRequestException("hors ligne");
    }
}
