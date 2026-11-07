using System.Windows;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Avant le verrouillage, ce que l'utilisateur écrivait s'enregistre : le commentaire ou la définition d'une saisie ouverte
/// dans une fiche, un dessin ou un lien en cours, une définition modifiée dans la vue « Définitions » ; si ce n'est pas
/// possible, le coffre reste ouvert. Une saisie de formulaire, elle, ne se garde pas : les paramètres oublient ce qu'ils
/// avaient lu et ce qui y était saisi, et le relisent ensuite.
/// </summary>
[TestClass]
public sealed class DraftsTests
{
    private const string Body = "# Mitose\n\n## Partie\n\nUn premier paragraphe avec un mot.\n\nLe second paragraphe, puis la fin.\n";

    private static readonly Anchor Passage = new("La prophase", "", " précède", 0, 11, ["Mitose", "Étapes"], 5, "sha256:aa");

    private static readonly string[] ChosenDomains = ["art", "science"];

    private static readonly int[] TypedSteps = [2, 4];

    [TestMethod]
    public async Task A_comment_being_written_in_a_sheet_is_saved_before_the_lock()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        page.StartAnnotatingCommand.Execute(PositionOf(page, "premier paragraphe"));
        page.EditorComment = "  Pourquoi ?  ";

        Assert.IsTrue(await page.LeaveAsync());

        Assert.IsFalse(page.IsEditing);
        var created = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single();
        Assert.AreEqual("premier paragraphe", created.Anchor.Exact);
        Assert.AreEqual("Pourquoi ?", created.Comment);
    }

    [TestMethod]
    public async Task A_definition_being_changed_in_a_sheet_is_saved_before_the_lock()
    {
        using var vault = new ReadingVault(Body, passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        page.StartEditingCommand.Execute(page.Passages.Single(passage => passage.IsDefinition));
        page.EditorExplanation = "Un autre sens";

        Assert.IsTrue(await page.LeaveAsync());

        Assert.IsFalse(page.IsEditing);
        var definition = vault.App.Sheets.Open(vault.SheetId).Value.Annotations.Single(annotation => annotation.Definition is not null).Definition;
        Assert.AreEqual(new Definition("mot", "Un autre sens"), definition);
    }

    [TestMethod]
    public async Task A_draft_that_cannot_be_saved_keeps_the_vault_open_with_what_blocks()
    {
        using var vault = new ReadingVault(Body, passage: true);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        // Une définition sans terme ne s'enregistre pas : la saisie reste, avec ce qu'elle contient.
        page.StartDefiningCommand.Execute(PositionOf(page, "second"));
        page.EditorTerm = string.Empty;
        page.EditorExplanation = "Ce qui se dit.";
        Assert.IsFalse(await page.LeaveAsync());
        Assert.IsTrue(page.IsEditing);
        Assert.AreEqual(ReadingText.DefinitionIncomplete, page.EditorProblem);
        Assert.AreEqual("Ce qui se dit.", page.EditorExplanation);
        Assert.HasCount(2, vault.App.Sheets.Open(vault.SheetId).Value.Annotations);

        // Un commentaire que le coffre refuse non plus.
        await page.AnswerEditorCommand.ExecuteAsync(false);
        page.StartEditingCommand.Execute(page.Passages.Single(passage => passage.IsPassage));
        page.EditorComment = "Un autre";
        vault.Leave();
        Assert.IsFalse(await page.LeaveAsync());
        Assert.IsTrue(page.IsEditing);
        Assert.IsTrue(page.HasEditorProblem);
        Assert.AreEqual("Un autre", page.EditorComment);
    }

    [TestMethod]
    public async Task An_input_where_nothing_was_written_closes_without_creating_anything()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);

        page.StartAnnotatingCommand.Execute(PositionOf(page, "premier"));
        Assert.IsTrue(await page.LeaveAsync());
        Assert.IsFalse(page.IsEditing);

        page.StartDefiningCommand.Execute(PositionOf(page, "mot"));
        Assert.IsTrue(await page.LeaveAsync(), "le terme proposé n'est pas écrit par l'utilisateur : rien ne retient le coffre");
        Assert.IsFalse(page.IsEditing);
        Assert.IsEmpty(vault.App.Sheets.Open(vault.SheetId).Value.Annotations);
    }

    [TestMethod]
    public async Task An_answer_typed_for_an_extension_is_not_kept()
    {
        using var vault = new ReadingVault(Body, passage: false);
        var page = vault.Page();
        await page.LoadCommand.ExecuteAsync(null);
        var asked = page.Guided.StartAsync(new SelectionAsk("Quiz", "Sélectionnez la question", null, AllowsTyping: true));
        page.Guided.Typed = "Une division";

        Assert.IsTrue(await page.LeaveAsync());

        Assert.IsFalse(page.Guided.IsActive);
        Assert.AreEqual(string.Empty, page.Guided.Typed);
        Assert.AreEqual(GuidedSelection.Stopped, (await asked).Choice);
    }

    [STATestMethod]
    public void A_diagram_or_a_link_being_composed_is_written_into_the_sheet_and_saved()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Mitose\n\nAvant\n", passage: false);
        var test = new TestShell();
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page, Width = 1400, Height = 900 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => page.Editor is not null);
            TestShell.Flush();
            var editor = page.Editor!;
            editor.SaveDelay = TimeSpan.FromMilliseconds(10);
            editor.PreviewDelay = TimeSpan.FromMilliseconds(10);
            UiThread.Until(() => editor.Preview is not null);

            // Un schéma composé dans son éditeur, pas encore inséré.
            editor.OpenGalleryCommand.Execute(GalleryKind.Schema);
            editor.AnswerGalleryCommand.Execute(true);
            Assert.IsTrue(editor.IsDiagramEditorOpen);
            var leaving = page.LeaveAsync();
            UiThread.Until(() => leaving.IsCompleted);

            Assert.IsTrue(leaving.Result);
            Assert.IsFalse(editor.IsDiagramEditorOpen);
            Assert.Contains("```mermaid\n%% kyfrev etapes\n", vault.App.Sheets.Open(vault.SheetId).Value.Body);

            // Un lien dont l'adresse est écrite.
            editor.StartLinkCommand.Execute(null);
            Assert.IsTrue(editor.IsLinkOpen);
            editor.LinkLabel = "le cours";
            editor.LinkAddress = "https://exemple.org/cours";
            leaving = page.LeaveAsync();
            UiThread.Until(() => leaving.IsCompleted);

            Assert.IsTrue(leaving.Result);
            Assert.IsFalse(editor.IsLinkOpen);
            var body = vault.App.Sheets.Open(vault.SheetId).Value.Body;
            Assert.Contains("le cours", body);
            Assert.Contains("https://exemple.org/cours", body);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task A_definition_changed_in_the_glossary_is_saved_before_the_lock()
    {
        using var glossary = new Glossary();
        var page = await glossary.LoadedAsync();
        var mitose = page.Rows.Single(row => row.Term == "Mitose");
        mitose.EditCommand.Execute(null);
        mitose.EditedExplanation = "  Une division de la cellule.  ";
        var noyau = page.Rows.Single(row => row.Term == "Noyau");
        noyau.EditCommand.Execute(null);

        Assert.IsTrue(await page.LeaveAsync());

        Assert.IsTrue(page.Rows.All(row => row.IsShown), "l'une s'est enregistrée, l'autre, inchangée, s'est refermée");
        Assert.AreEqual("Une division de la cellule.", glossary.App.Annotations.Definitions().Value.Single(definition => definition.Term == "Mitose").Explanation);
        Assert.AreEqual("Une division de la cellule.", page.Rows.Single(row => row.Term == "Mitose").Explanation);
        Assert.IsFalse(page.HasError);
    }

    [TestMethod]
    public async Task A_definition_of_the_glossary_that_cannot_be_saved_keeps_the_vault_open()
    {
        using var glossary = new Glossary();
        var page = await glossary.LoadedAsync();
        var mitose = page.Rows.Single(row => row.Term == "Mitose");
        mitose.EditCommand.Execute(null);
        mitose.EditedExplanation = "   ";

        Assert.IsFalse(await page.LeaveAsync());
        Assert.IsTrue(mitose.IsEditing);
        Assert.AreEqual(ReadingText.DefinitionIncomplete, page.Error);

        // Un refus du coffre aussi : le brouillon reste.
        mitose.EditedExplanation = "Une division.";
        glossary.Leave();
        Assert.IsFalse(await page.LeaveAsync());
        Assert.IsTrue(mitose.IsEditing);
        Assert.IsTrue(page.HasError);
        Assert.AreNotEqual(ReadingText.DefinitionIncomplete, page.Error);
        Assert.AreEqual("Une division.", mitose.EditedExplanation);
    }

    [TestMethod]
    public async Task The_revision_settings_forget_what_they_read_and_what_was_typed_then_read_again()
    {
        using var vaults = new TestVaults(AppMode.Development);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        Assert.IsTrue(vaults.App.Settings.Update(preferences => preferences with { MaxPerDay = 7, ActiveRecall = true, IncludeManualArchiving = true }).Succeeded);
        var page = new SettingsPageViewModel(vaults.App.Settings, TimeProvider.System);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.AreEqual("7", page.MaxPerDay);
        page.Steps = "2, 4";
        page.MaxWaitDays = "abc";
        await page.SaveCommand.ExecuteAsync(null);
        Assert.IsTrue(page.HasError);

        page.Forget();

        Assert.AreEqual(string.Empty, page.Steps);
        Assert.AreEqual(string.Empty, page.RestMonths);
        Assert.AreEqual(string.Empty, page.MaxPerDay);
        Assert.AreEqual(string.Empty, page.MaxWaitDays);
        Assert.IsFalse(page.ActiveRecall);
        Assert.IsFalse(page.IncludeManualArchiving);
        Assert.IsFalse(page.HasStepsPreview);
        Assert.IsFalse(page.IsReady);
        Assert.IsFalse(page.IsLoading);
        Assert.IsFalse(page.IsSaved);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.HasChanges);
        Assert.IsFalse(page.SaveCommand.CanExecute(null));

        await page.LoadCommand.ExecuteAsync(null);

        Assert.IsTrue(page.IsReady);
        Assert.AreEqual("1, 3, 7, 14, 30", page.Steps, "ce qui était saisi n'a pas été enregistré");
        Assert.AreEqual("7", page.MaxPerDay);
        Assert.IsTrue(page.ActiveRecall);
        Assert.IsTrue(page.IncludeManualArchiving);
        Assert.IsFalse(page.HasChanges);
    }

    [STATestMethod]
    public void A_read_or_a_save_of_the_revision_settings_started_before_forgetting_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        var page = new SettingsPageViewModel(vaults.App.Settings, TimeProvider.System);

        page.LoadCommand.Execute(null);
        page.Forget();
        UiThread.Until(() => !page.LoadCommand.IsRunning);
        Assert.AreEqual(string.Empty, page.Steps);
        Assert.IsFalse(page.IsReady);
        Assert.IsFalse(page.IsLoading);

        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.LoadCommand.IsRunning);
        page.Steps = "2, 4";
        page.SaveCommand.Execute(null);
        page.Forget();
        UiThread.Until(() => !page.SaveCommand.IsRunning);
        Assert.AreEqual(string.Empty, page.Steps);
        Assert.IsFalse(page.IsSaved);
        Assert.IsFalse(page.IsReady);
        CollectionAssert.AreEqual(TypedSteps, vaults.App.Settings.Get().Value.Steps.ToArray(), "l'enregistrement, lui, a eu lieu");
    }

    [TestMethod]
    public void The_modules_tab_forgets_the_vault_settings_without_saving_anything()
    {
        using var vaults = new TestVaults(AppMode.Development);
        Assert.IsNotNull(vaults.App.Vaults.Create(vaults.PathOf("Coffre")).Vault);
        Assert.IsTrue(vaults.App.Settings.Update(preferences => preferences with { QuotesActive = true, QuotesOnline = true, SchemasActive = true, QuoteDomains = [.. ChosenDomains] }).Succeeded);
        var applied = 0;
        var tab = new ModulesSettingsViewModel(vaults.App.Settings, _ => applied++, FakeExtensions.Settings());
        tab.OnNavigatedTo();
        tab.SchemasActive = false;
        Assert.AreEqual(1, applied);
        Assert.IsTrue(tab.HasMessage);

        tab.Forget();

        Assert.IsFalse(tab.QuotesActive);
        Assert.IsFalse(tab.QuotesOnline);
        Assert.IsFalse(tab.ShowsDomains);
        Assert.IsFalse(tab.SchemasActive);
        Assert.IsTrue(tab.Domains.All(domain => !domain.IsChosen));
        Assert.IsNull(tab.Message);
        Assert.IsFalse(tab.HasMessage);
        Assert.IsNull(tab.Error);
        Assert.AreEqual(1, applied, "oublier n'enregistre rien");
        var stored = vaults.App.Settings.Get().Value;
        Assert.IsTrue(stored.QuotesActive);
        Assert.IsTrue(stored.QuotesOnline);
        CollectionAssert.AreEqual(ChosenDomains, stored.QuoteDomains.ToArray());

        tab.OnNavigatedTo();

        Assert.IsTrue(tab.QuotesActive);
        Assert.IsTrue(tab.QuotesOnline);
        Assert.IsFalse(tab.SchemasActive);
        CollectionAssert.AreEqual(ChosenDomains, tab.Domains.Where(domain => domain.IsChosen).Select(domain => domain.Name).ToArray());
        Assert.AreEqual(1, applied);
    }

    private static TextPosition PositionOf(SheetPageViewModel page, string exact)
    {
        var start = page.Reading!.Map.Text.IndexOf(exact, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, exact);
        return new TextPosition(start, start + exact.Length);
    }

    /// <summary>Un coffre jetable, avec deux définitions dans la fiche « La cellule ».</summary>
    private sealed class Glossary : IDisposable
    {
        private readonly TestVaults _vaults;

        public Glossary()
        {
            _vaults = new TestVaults(AppMode.Development, new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
            Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            var cell = App.Sheets.Create("Sciences/Cellule", "La cellule", null).Value;
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Mitose", "Division de la cellule.")).Succeeded);
            Assert.IsTrue(App.Annotations.CreateDefinition(cell, Passage, new Definition("Noyau", "Il renferme l'ADN.")).Succeeded);
        }

        public KyfRevApplication App => _vaults.App;

        public async Task<DefinitionsPageViewModel> LoadedAsync()
        {
            var page = new DefinitionsPageViewModel(App.Annotations, new FakeOpener());
            await page.LoadCommand.ExecuteAsync(null);
            return page;
        }

        /// <summary>Passe à un autre coffre : ce qui visait celui-ci est ensuite refusé.</summary>
        public void Leave() => Assert.IsNotNull(App.Vaults.Create(_vaults.PathOf("Autre")).Vault);

        public void Dispose() => _vaults.Dispose();
    }
}
