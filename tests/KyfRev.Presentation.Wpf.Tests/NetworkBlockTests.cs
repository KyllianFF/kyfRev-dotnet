using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.Modules.Schemas;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Schemas;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le bloc <c>```reseau</c> dans les fiches : son dessin, son insertion, l'ouverture de son éditeur.</summary>
[TestClass]
[DoNotParallelize]
public sealed class NetworkBlockTests
{
    private const string SchemaId = "s_01JQZ8S7ZK4V3H2N8M6X5W9PQR";
    private const string Reference = "schema: " + SchemaId;
    private const string Text = "{\"format\": 2, \"titre\": \"Réseau\"}";

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly string[] TheSchema = [SchemaId];

    [TestMethod]
    public void A_block_cites_a_schema_or_holds_an_old_one()
    {
        Assert.AreEqual(Reference, NetworkBlock.Reference(SchemaId));
        Assert.AreEqual(SchemaId, NetworkBlock.ReferenceOf($"  {Reference}\n"));
        Assert.IsNull(NetworkBlock.ReferenceOf("schema: pas_un_identifiant"));
        Assert.IsNull(NetworkBlock.ReferenceOf(null));
        Assert.IsTrue(NetworkBlock.IsInline(" {\"version\": 1}"));
        Assert.IsFalse(NetworkBlock.IsInline(Reference));
        Assert.IsFalse(NetworkBlock.IsInline(null));
    }

    [TestMethod]
    public async Task The_text_of_a_block_comes_from_the_vault_or_from_the_block()
    {
        var schemas = new FakeSchemas();
        schemas.Texts[SchemaId] = Text;

        Assert.AreEqual(Text, await NetworkBlock.TextOfAsync(schemas, Reference));
        Assert.AreEqual("{\"version\": 1}", await NetworkBlock.TextOfAsync(schemas, "{\"version\": 1}"));
        Assert.IsNull(await NetworkBlock.TextOfAsync(schemas, "n'importe quoi"));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => NetworkBlock.TextOfAsync(null!, Reference));
    }

    [STATestMethod]
    public void The_view_draws_the_schema_and_a_double_click_opens_its_editor()
    {
        using var scene = new ViewScene();
        scene.Schemas.Texts[SchemaId] = Text;

        var view = scene.Show(Reference);

        Assert.IsTrue(view.ShowsPicture);
        Assert.AreEqual(Text, scene.Engine.Drawn.Single());
        Assert.AreEqual(SchemaText.EditHint, view.ToolTip);
        Assert.AreEqual(Cursors.Hand, view.Cursor);
        Assert.IsTrue(scene.Engine.Colors!.ContainsKey("--texte"), "Aux couleurs de l'application.");

        view.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        Assert.IsEmpty(scene.Schemas.Opened, "Un seul clic ne l'ouvre pas.");

        view.OpenEditor();
        CollectionAssert.AreEqual(TheSchema, scene.Schemas.Opened);
    }

    [STATestMethod]
    public void An_old_schema_in_the_sheet_draws_but_does_not_open()
    {
        using var scene = new ViewScene();

        var view = scene.Show("{\"version\": 1}");

        Assert.IsTrue(view.ShowsPicture);
        Assert.AreEqual(SchemaText.InlineHint, view.ToolTip);
        view.OpenEditor();
        Assert.IsEmpty(scene.Schemas.Opened);
    }

    [STATestMethod]
    [DataRow(null, "ModuleOff")]
    [DataRow("", "EmptyBlock")]
    [DataRow("schema: inconnu", "Unavailable")]
    [DataRow(Reference, "Unavailable")]
    public void Without_a_drawing_the_view_says_why(string? source, string reason)
    {
        using var scene = new ViewScene();
        scene.Schemas.Active = reason != "ModuleOff";

        var view = scene.Show(source);

        Assert.IsFalse(view.ShowsPicture);
        Assert.AreEqual(typeof(SchemaText).GetProperty(reason)!.GetValue(null), view.Notice);
    }

    [STATestMethod]
    public void A_drawing_refused_by_the_engine_is_told()
    {
        using var scene = new ViewScene();
        scene.Schemas.Texts[SchemaId] = Text;
        scene.Engine.Refuses = true;

        var view = scene.Show(Reference);

        Assert.IsFalse(view.ShowsPicture);
        Assert.AreEqual(SchemaText.Unavailable, view.Notice);
    }

    [STATestMethod]
    public void A_sheet_reads_its_network_block_as_a_drawing()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault($"Avant\n\n```reseau\n{Reference}\n```\n", passage: false);
        var engine = new FakeEngine();
        var schemas = new FakeSchemas();
        schemas.Texts[SchemaId] = Text;
        var viewer = new SheetDocumentViewer { Width = 800, Height = 600 };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources[MermaidEngine.ResourceKey] = engine;
        window.Resources[NetworkBlock.ResourceKey] = schemas;
        window.Show();
        TestShell.Flush();
        try
        {
            viewer.Reading = vault.Reading();
            UiThread.Until(() => Blocks(viewer.Document).OfType<BlockUIContainer>().Any(block => block.Child is NetworkView { IsLoaded: true }));
            var view = (NetworkView)Blocks(viewer.Document).OfType<BlockUIContainer>().Select(block => block.Child).Single(child => child is NetworkView);
            UiThread.Until(() => view.ShowsPicture);
            Assert.AreEqual(Text, engine.Drawn.Single());
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public async Task The_module_reads_lists_creates_and_copies_on_the_vault()
    {
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        SchemaUseCases? useCases = new(vaults.App.Host);
        var schemas = new NetworkSchemas(() => useCases, () => null);
        Assert.IsTrue(schemas.IsActive);

        var id = await schemas.CreateAsync("Réseau \"du\" lycée");
        Assert.IsNotNull(id);
        Assert.AreEqual("Réseau \"du\" lycée", (await schemas.ListAsync()).Single().Title);
        Assert.Contains("lycée", (await schemas.ReadAsync(id))!);
        Assert.IsNull(await schemas.ReadAsync("s_inconnu"));
        var copy = await schemas.DuplicateAsync(id);
        Assert.IsNotNull(copy);
        Assert.IsNull(await schemas.DuplicateAsync("s_inconnu"));
        schemas.Open(id);

        vaults.App.Host.Dispose();
        Assert.IsEmpty(await schemas.ListAsync());
        Assert.IsNull(await schemas.CreateAsync("Refusé"));

        useCases = null;
        Assert.IsFalse(schemas.IsActive);
        Assert.IsNull(await schemas.ReadAsync(id));
        Assert.IsEmpty(await schemas.ListAsync());
        Assert.IsNull(await schemas.CreateAsync("x"));
        Assert.IsNull(await schemas.DuplicateAsync(id));
    }

    [STATestMethod]
    public void A_view_replaced_in_the_menu_closes_its_island_and_a_view_kept_keeps_it()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var page = new SchemasPageViewModel(new SchemaUseCases(vaults.App.Host), vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        var island = page.TakeIsland();
        Assert.AreSame(island, page.TakeIsland(), "une seule à la fois");
        var test = new TestShell();
        var entry = CorePages.FromModule(new NavigationEntry(SchemasModule.ModuleId, "Schémas réseau", 45)) with { Page = page };

        test.Shell.SetModuleEntries([entry]);
        test.Shell.SetModuleEntries([entry]);
        Assert.IsFalse(island.IsDisposed, "la même vue, gardée : son îlot reste ouvert");

        test.Shell.SetModuleEntries([]);
        Assert.IsTrue(island.IsDisposed, "la vue remplacée ferme son îlot et son moteur");
        Assert.IsNull(page.Island);
    }

    [EngineTest]
    public void The_window_warms_up_the_island_of_the_open_vault_in_the_background()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var test = new TestShell();
        using var page = new SchemasPageViewModel(new SchemaUseCases(vaults.App.Host), vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        test.Shell.SetModuleEntries([CorePages.FromModule(new NavigationEntry(SchemasModule.ModuleId, "Schémas réseau", 45)) with { Page = page }]);
        var window = new ShellWindow(test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1100, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            window.WarmUp.Delay = TimeSpan.FromMilliseconds(50);
            test.Shell.ShowWorkspace();
            UiThread.Until(() => page.Island is { IsReady: true }, TimeSpan.FromMinutes(2));
            UiThread.Until(() => page.Island!.Parent is null, TimeSpan.FromSeconds(10));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Opening_goes_to_the_view_and_closing_the_editor_comes_back()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var useCases = new SchemaUseCases(vaults.App.Host);
        var page = new SchemasPageViewModel(useCases, vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        var test = new TestShell();
        var shell = test.Shell;
        shell.SetModuleEntries([CorePages.FromModule(new NavigationEntry(SchemasModule.ModuleId, "Schémas réseau", 45)) with { Page = page }]);
        var from = shell.Current;
        var schemas = new NetworkSchemas(() => useCases, () => shell);

        schemas.Open(SchemaId);

        Assert.AreSame(page, shell.Current.Page);
        Assert.AreEqual(SchemaId, page.ToOpen);

        var bridge = page.Bridge();
        var answer = bridge.ReceiveAsync("{\"kind\":\"call\",\"id\":1,\"method\":\"editeurFerme\"}");
        UiThread.Until(() => answer.IsCompleted);

        Assert.AreSame(from, shell.Current, "Fermer l'éditeur revient d'où l'on vient.");

        var again = bridge.ReceiveAsync("{\"kind\":\"call\",\"id\":2,\"method\":\"editeurFerme\"}");
        UiThread.Until(() => again.IsCompleted);
        Assert.AreSame(from, shell.Current, "Sans fiche d'où revenir : il reste là.");

        new NetworkSchemas(() => useCases, () => null).Open(SchemaId);
    }

    [TestMethod]
    public async Task Choosing_a_new_schema_creates_it_and_opens_it()
    {
        var schemas = new FakeSchemas();
        var choice = new NetworkChoiceViewModel(schemas) { NewTitle = "  Réseau  " };

        Assert.AreEqual(SchemaId, await choice.ChooseAsync());

        Assert.IsTrue(choice.OpensEditor);
        Assert.AreEqual("Réseau", schemas.Created.Single());
        Assert.IsFalse(choice.HasError);
    }

    [TestMethod]
    public async Task Choosing_an_existing_schema_cites_it_or_copies_it()
    {
        var schemas = new FakeSchemas();
        schemas.List.Add(new SchemaSummary("s_un", "Un", 0, null, []));
        var choice = new NetworkChoiceViewModel(schemas);
        await choice.LoadAsync();
        Assert.IsTrue(choice.HasExisting);
        choice.Selected = choice.Existing[0];

        Assert.AreEqual("s_un", await choice.ChooseAsync());
        Assert.IsFalse(choice.OpensEditor);

        choice.IsCopy = true;
        Assert.AreEqual("s_copie", await choice.ChooseAsync());
    }

    [TestMethod]
    public async Task Choosing_nothing_or_a_refused_schema_says_so()
    {
        var schemas = new FakeSchemas { Refuses = true };
        var choice = new NetworkChoiceViewModel(schemas);

        Assert.IsNull(await choice.ChooseAsync());
        Assert.AreEqual(SchemaText.NothingChosen, choice.Error);
        Assert.IsTrue(choice.HasError);

        choice.NewTitle = "Réseau";
        Assert.IsNull(await choice.ChooseAsync());
        Assert.AreEqual(SchemaText.Unavailable, choice.Error);
        Assert.IsFalse(choice.OpensEditor);
    }

    [STATestMethod]
    public void The_ribbon_offers_a_schema_only_with_the_module_and_inserts_a_reference()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Avant\n", passage: false);
        var schemas = new FakeSchemas();
        var test = new TestShell();
        var page = vault.Page(schemas: schemas);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page, Width = 1400, Height = 900 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => page.Editor is not null);
            TestShell.Flush();
            var editor = page.Editor!;
            Assert.IsTrue(editor.CanInsertNetwork);

            // Annuler n'écrit rien.
            editor.OpenNetworkChoiceCommand.Execute(null);
            UiThread.Until(() => editor.IsNetworkChoiceOpen);
            TestShell.Flush();
            Assert.IsTrue(sheet.NetworkChoiceDialog.IsOpen);
            sheet.NetworkChoiceDialog.Close(DesignSystem.Controls.DialogChoice.Secondary);
            UiThread.Until(() => !editor.IsNetworkChoiceOpen);
            Assert.DoesNotContain("reseau", sheet.EditorView.Source.Text);

            // Sans rien de choisi : la fenêtre reste, et dit pourquoi.
            editor.OpenNetworkChoiceCommand.Execute(null);
            UiThread.Until(() => editor.IsNetworkChoiceOpen);
            var choice = editor.NetworkChoice!;
            editor.AnswerNetworkChoiceCommand.Execute(true);
            UiThread.Until(() => choice.HasError);
            Assert.IsTrue(editor.IsNetworkChoiceOpen);

            // Un nouveau schéma : le bloc s'écrit, et son éditeur s'ouvre.
            choice.NewTitle = "Réseau";
            editor.AnswerNetworkChoiceCommand.Execute(true);
            UiThread.Until(() => !editor.IsNetworkChoiceOpen);
            UiThread.Until(() => sheet.EditorView.Source.Text.Contains($"```reseau\nschema: {SchemaId}\n```", StringComparison.Ordinal));
            UiThread.Until(() => schemas.Opened.Count == 1);
            CollectionAssert.AreEqual(TheSchema, schemas.Opened);

            // Sans fenêtre ouverte, répondre ne fait rien.
            editor.AnswerNetworkChoiceCommand.Execute(true);
            schemas.Active = false;
            Assert.IsFalse(editor.CanInsertNetwork);
            editor.OpenNetworkChoiceCommand.Execute(null);
            Assert.IsFalse(editor.IsNetworkChoiceOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [EngineTest]
    public void The_hidden_page_really_draws_a_schema()
    {
        using var ui = new UiThread();
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var colors = new Dictionary<string, string>(StringComparer.Ordinal) { ["--texte"] = "#112233" };

        var drawn = engine.DrawNetworkAsync(Text.Replace("\"Réseau\"", "\"Réseau\", \"id\": \"s_x\"", StringComparison.Ordinal), colors);
        UiThread.Until(() => drawn.IsCompleted, TimeSpan.FromMinutes(2));

        Assert.IsNotNull(drawn.Result);
        Assert.IsNotEmpty(drawn.Result.Png.ToArray());

        var refused = engine.DrawNetworkAsync("{ cassé", colors);
        UiThread.Until(() => refused.IsCompleted, TimeSpan.FromSeconds(30));
        Assert.IsNull(refused.Result);
    }

    [EngineTest]
    public void In_the_text_a_block_draws_and_its_editor_opens()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault($"Avant\n\n```reseau\n{Reference}\n```\n", passage: false);
        using var engine = new MermaidEngine(FakeEnvironment.WebData);
        var schemas = new FakeSchemas();
        schemas.Texts[SchemaId] = Text.Replace("\"Réseau\"", "\"Réseau\", \"id\": \"" + SchemaId + "\"", StringComparison.Ordinal);
        var test = new TestShell();
        var page = vault.Page(markdown: false, schemas: schemas);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var sheet = new SheetPage { DataContext = page, Width = 1400, Height = 900 };
        var window = new Window { Content = new Canvas { Children = { sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Resources[MermaidEngine.ResourceKey] = engine;
        window.Resources[NetworkBlock.ResourceKey] = schemas;
        window.Show();
        TestShell.Flush();
        try
        {
            page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => page.Editor is not null);
            TestShell.Flush();
            UiThread.Until(() => sheet.EditorView.Visual?.IsReady == true, TimeSpan.FromMinutes(2));
            var visual = sheet.EditorView.Visual!;
            string Script(string expression)
            {
                var run = visual.RunScriptAsync(expression);
                UiThread.Until(() => run.IsCompleted);
                return run.Result;
            }

            UiThread.Until(() => Script("document.querySelector('.apercu-dessin img') !== null") == "true", TimeSpan.FromMinutes(1));
            Script("[...document.querySelectorAll('.action-dessin')].find((b) => b.textContent === 'Modifier…').click()");
            UiThread.Until(() => schemas.Opened.Count == 1);
            CollectionAssert.AreEqual(TheSchema, schemas.Opened);

            var menu = visual.MenuItems(new MenuEvent(null, 0, false, false, false, false, 7, NetworkBlock.Language, Reference), null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.AreEqual(new[] { EditingText.EditDrawing, EditingText.ShowDrawingCode, EditingText.DeleteNetwork }, menu);
        }
        finally
        {
            window.Close();
        }
    }

    [EngineTest]
    public void From_a_sheet_the_island_opens_on_the_schema_and_a_new_one_reads_well()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        vaults.App.Vaults.Create(vaults.PathOf("Coffre"));
        var useCases = new SchemaUseCases(vaults.App.Host);
        var id = new NetworkSchemas(() => useCases, () => null).CreateAsync("Depuis une fiche");
        UiThread.Until(() => id.IsCompleted);
        using var model = new SchemasPageViewModel(useCases, vaults.Picker, new FakeOpener(), FakeEnvironment.WebData);
        var test = new TestShell();
        var back = 0;
        model.OpenFrom(id.Result!, () => back++);
        var page = new SchemasPage { DataContext = model, Width = 1300, Height = 860 };
        var window = new Window { Content = new Canvas { Children = { page } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            UiThread.Until(() => page.Island?.IsReady == true, TimeSpan.FromMinutes(2));
            string Script(string expression)
            {
                var run = page.Island!.RunScriptAsync(expression);
                UiThread.Until(() => run.IsCompleted);
                return run.Result;
            }

            // L'éditeur s'ouvre sur le schéma vide écrit par l'application : l'éditeur le lit sans erreur.
            UiThread.Until(() => Script("document.querySelector('dialog[open] input') !== null && document.querySelector('dialog[open] input').value === 'Depuis une fiche'") == "true", TimeSpan.FromSeconds(30));
            Assert.IsNull(model.ToOpen);
            Assert.AreEqual("false", Script("document.body.innerText.includes('illisible')"));

            // Le fermer revient à la fiche.
            Script("[...document.querySelectorAll('dialog[open] button')].find((b) => b.innerText.trim() === 'Fermer').click()");
            UiThread.Until(() => back == 1, TimeSpan.FromSeconds(30));
        }
        finally
        {
            window.Close();
        }
    }

    private static List<Block> Blocks(FlowDocument? document) => document is null ? [] : [.. document.Blocks];

    /// <summary>Le module simulé.</summary>
    private sealed class FakeSchemas : INetworkSchemas
    {
        public Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);

        public List<SchemaSummary> List { get; } = [];

        public List<string> Created { get; } = [];

        public List<string> Opened { get; } = [];

        public bool Active { get; set; } = true;

        public bool Refuses { get; init; }

        public bool IsActive => Active;

        public Task<string?> ReadAsync(string id) => Task.FromResult(Texts.GetValueOrDefault(id));

        public Task<IReadOnlyList<SchemaSummary>> ListAsync() => Task.FromResult<IReadOnlyList<SchemaSummary>>(List);

        public Task<string?> CreateAsync(string title)
        {
            Created.Add(title);
            return Task.FromResult(Refuses ? null : SchemaId);
        }

        public Task<string?> DuplicateAsync(string id) => Task.FromResult<string?>("s_copie");

        public void Open(string id) => Opened.Add(id);
    }

    /// <summary>La page cachée simulée.</summary>
    private sealed class FakeEngine : IMermaidEngine
    {
        public List<string> Drawn { get; } = [];

        public IReadOnlyDictionary<string, string>? Colors { get; private set; }

        public bool Refuses { get; set; }

        public Task<MermaidPicture?> DrawAsync(string code, IReadOnlyDictionary<string, string> theme) => Task.FromResult<MermaidPicture?>(null);

        public Task<MermaidPicture?> DrawSvgAsync(ReadOnlyMemory<byte> svg) => Task.FromResult<MermaidPicture?>(null);

        public Task<MermaidPicture?> DrawNetworkAsync(string schema, IReadOnlyDictionary<string, string> colors)
        {
            Drawn.Add(schema);
            Colors = colors;
            return Task.FromResult(Refuses ? null : new MermaidPicture(Png, "data:image/png;base64,", 12, 6));
        }
    }

    /// <summary>La vue d'un bloc, dans une fenêtre.</summary>
    private sealed class ViewScene : IDisposable
    {
        private readonly UiThread _ui = new();
        private readonly Window _window;

        public ViewScene()
        {
            var test = new TestShell();
            _window = new Window { Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            _window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
            _window.Resources[MermaidEngine.ResourceKey] = Engine;
            _window.Resources[NetworkBlock.ResourceKey] = Schemas;
        }

        public FakeSchemas Schemas { get; } = new();

        public FakeEngine Engine { get; } = new();

        public NetworkView Show(string? source)
        {
            var view = new NetworkView { Source = source };
            _window.Content = view;
            _window.Show();
            TestShell.Flush();
            UiThread.Until(() => view.Drawing.IsCompleted);
            TestShell.Flush();
            return view;
        }

        public void Dispose()
        {
            _window.Close();
            _ui.Dispose();
        }
    }
}
