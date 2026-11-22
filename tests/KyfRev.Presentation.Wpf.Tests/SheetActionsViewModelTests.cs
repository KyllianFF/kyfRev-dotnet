using System.Windows;
using System.Windows.Controls;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// Les actions communes sur une fiche (rangée 23A des maquettes), depuis n'importe quelle vue : leur fenêtre, ce qu'elles font
/// au coffre, ce qu'elles disent, et le menu d'une fiche qui les propose partout dans le même ordre.
/// </summary>
[TestClass]
public sealed class SheetActionsViewModelTests
{
    private static readonly string[] Bio = ["bio"];

    private static readonly string[] Everything =
    [
        EditingText.Read, ReadingText.Revise, EditingText.Write,
        ReviewText.PostponeTomorrow, SheetActionsText.Archive,
        SheetActionsText.Rename, SheetActionsText.Tags, SheetActionsText.Move,
        PrintText.PrintAction,
        SheetsText.MakeNote,
        SheetActionsText.Delete,
    ];

    [TestMethod]
    public async Task A_sheet_is_renamed_tagged_and_moved_through_the_window_and_the_view_is_told()
    {
        using var scene = new Scene();
        var changed = 0;
        scene.Actions.Changed += (_, _) => changed++;

        scene.Actions.Rename(scene.Target);
        await scene.UntilActingAsync();
        Assert.AreEqual(SheetActionsText.RenameTitle, scene.Actions.ActionTitle);
        Assert.AreEqual("Mitose", scene.Actions.ActionInput);
        scene.Actions.ActionInput = "   ";
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.NameRequired, scene.Actions.ActionError, "un nom vide ne passe pas");
        Assert.IsTrue(scene.Actions.IsActing);
        scene.Actions.ActionInput = "La mitose";
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Renamed, scene.Actions.Done);
        Assert.AreEqual("La mitose", scene.Sheet().Title);
        Assert.AreEqual(1, changed);

        scene.Actions.Tags(scene.Target);
        await scene.UntilActingAsync();
        Assert.AreEqual(SheetActionsText.TagsLabel, scene.Actions.ActionInputLabel);
        CollectionAssert.Contains(scene.Actions.TagSuggestions.ToList(), "bio", "les étiquettes du coffre sont proposées");
        scene.Actions.TakeTagCommand.Execute("bio");
        Assert.AreEqual("bio, ", scene.Actions.ActionInput);
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.TagsSaved, scene.Actions.Done);
        CollectionAssert.AreEqual(Bio, scene.Sheet().Tags.ToArray());

        scene.Actions.Move(scene.Target);
        await scene.UntilActingAsync();
        Assert.AreEqual("Biologie", scene.Actions.ActionDestination!.Value, "son dossier d'abord");
        scene.Actions.ActionDestination = scene.Actions.ActionDestinations.Single(choice => choice.Value == "Histoire");
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Moved, scene.Actions.Done);
        Assert.AreEqual("Histoire", scene.Sheet().Folder);
        Assert.AreEqual(3, changed);

        scene.Actions.Move(scene.Target);
        await scene.UntilActingAsync();
        await scene.Actions.AnswerActionCommand.ExecuteAsync(false);
        Assert.IsFalse(scene.Actions.IsActing);
        Assert.AreEqual(3, changed, "annulé : rien ne change");
        scene.Actions.DismissDoneCommand.Execute(null);
        Assert.IsFalse(scene.Actions.HasDone);
    }

    [TestMethod]
    public async Task A_sheet_is_archived_restarted_and_trashed_and_its_page_is_left()
    {
        using var scene = new Scene();
        var gone = new List<string>();
        scene.Actions.SheetGone = gone.Add;

        scene.Actions.Archive(scene.Target);
        await scene.UntilActingAsync();
        Assert.AreEqual(SheetActionsText.ArchiveMessageOf("Mitose"), scene.Actions.ActionMessage);
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Archived, scene.Actions.Done);
        Assert.IsTrue(scene.Sheet().Archived);

        scene.Actions.ToggleReminders(scene.Target with { IsArchived = true, IsExcludedFromReminders = false });
        await scene.UntilDoneAsync(ArchivesText.ExcludedMessageOf("Mitose"));
        scene.Actions.ToggleReminders(scene.Target with { IsArchived = true, IsExcludedFromReminders = true });
        await scene.UntilDoneAsync(ArchivesText.IncludedMessageOf("Mitose"));

        scene.Actions.Restart(scene.Target);
        await scene.UntilActingAsync();
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Restarted, scene.Actions.Done);
        Assert.IsFalse(scene.Sheet().Archived);

        scene.Actions.Trash(scene.Target);
        await scene.UntilActingAsync();
        Assert.IsTrue(scene.Actions.CanErase);
        scene.Actions.ErasesForGood = true;
        Assert.AreEqual(SheetActionsText.EraseConfirm, scene.Actions.ActionConfirm);
        await scene.Actions.AnswerActionCommand.ExecuteAsync(true);
        Assert.AreEqual(SheetActionsText.Erased, scene.Actions.Done);
        CollectionAssert.AreEqual(new[] { scene.Target.Id }, gone, "la fiche ouverte se quitte");

        scene.Actions.Rename(scene.Target);
        await scene.UntilDoneAsync(SheetActionsText.SheetMissing);
        Assert.IsFalse(scene.Actions.IsActing, "une fiche partie : pas de fenêtre, et on le dit");
    }

    [TestMethod]
    public async Task Postponing_and_turning_into_a_note_happen_at_once_and_a_refusal_is_said()
    {
        using var scene = new Scene();

        scene.Actions.PostponeTomorrow(scene.Target);
        await scene.UntilDoneAsync(SheetActionsText.PostponedTomorrow);

        scene.Actions.ToggleNote(scene.Target);
        await scene.UntilDoneAsync(SheetsText.NowNote);
        Assert.IsTrue(scene.Sheet().IsNote);
        scene.Actions.ToggleNote(scene.Target with { IsNote = true });
        await scene.UntilDoneAsync(SheetsText.NowReview);

        scene.Actions.PostponeTomorrow(new SheetTarget("f_inconnue", "Inconnue"));
        await scene.UntilAsync(() => scene.Actions.HasDone && scene.Actions.Done != SheetsText.NowReview);
        Assert.AreNotEqual(SheetActionsText.PostponedTomorrow, scene.Actions.Done, "le refus se dit à la même place");

        scene.Actions.Archive(scene.Target);
        await scene.UntilActingAsync();
        scene.Actions.Dismiss();
        Assert.IsFalse(scene.Actions.IsActing, "quitter la vue referme la fenêtre");
    }

    [TestMethod]
    public void It_opens_prints_and_offers_its_extensions()
    {
        using var scene = new Scene();
        scene.Actions.Read(scene.Target);
        scene.Actions.Revise(scene.Target);
        scene.Actions.Write(scene.Target);
        scene.Actions.Print(scene.Target);
        CollectionAssert.AreEqual(new object[] { scene.Target.Id, ("réviser", scene.Target.Id), ("écrire", scene.Target.Id), scene.Target.Id }, scene.Opener.Opened);
        Assert.IsFalse(scene.Actions.CanShare, "sans cas d'usage du partage");
        scene.Actions.Share(scene.Target);
        scene.Actions.SaveAsTemplate(scene.Target);
        Assert.IsEmpty(scene.Actions.Extensions(scene.Target));

        scene.Actions.ExtensionMenus = (_, context) => [new MenuContribution($"Quiz de {context["titre"]}", () => { })];
        Assert.AreEqual("Quiz de Mitose", scene.Actions.Extensions(scene.Target).Single().Label);
        Assert.ThrowsExactly<ArgumentNullException>(() => scene.Actions.Read(null!));
    }

    [STATestMethod]
    public void The_menu_of_a_sheet_is_the_same_everywhere_and_marks_the_open_mode()
    {
        using var ui = new UiThread();
        using var scene = new Scene();

        CollectionAssert.AreEqual(Everything, Headers(SheetMenu.Items(scene.Target, scene.Actions)));

        var archived = SheetMenu.Items(scene.Target with { IsArchived = true, IsExcludedFromReminders = true }, scene.Actions);
        CollectionAssert.AreEqual(
            new[] { EditingText.Read, ReadingText.Revise, EditingText.Write, SheetActionsText.Restart, ArchivesText.Include, SheetActionsText.Rename, SheetActionsText.Tags, SheetActionsText.Move, PrintText.PrintAction, SheetActionsText.Delete },
            Headers(archived));
        Assert.IsFalse(((MenuItem)archived[1]).IsEnabled);
        Assert.IsFalse(Headers(SheetMenu.Items(scene.Target with { IsArchived = true }, scene.Actions)).Contains(ArchivesText.Include), "sans savoir, pas de réglage des rappels");

        CollectionAssert.AreEqual(
            new[] { EditingText.Read, ReadingText.Revise, EditingText.Write, SheetsText.MakeReview, SheetActionsText.Rename, SheetActionsText.Tags, SheetActionsText.Move, PrintText.PrintAction, SheetActionsText.Delete },
            Headers(SheetMenu.Items(scene.Target with { IsNote = true }, scene.Actions)), "une note n'a ni « Reporter » ni « Archiver »");

        var opened = SheetMenu.Items(scene.Target, scene.Actions, SheetMode.Write);
        Assert.IsTrue(((MenuItem)opened[2]).IsChecked, "le mode en cours est marqué");
        Assert.IsFalse(((MenuItem)opened[0]).IsChecked);
        Assert.ThrowsExactly<ArgumentNullException>(() => SheetMenu.Items(null!, scene.Actions));
        Assert.ThrowsExactly<ArgumentNullException>(() => SheetMenu.Items(scene.Target, null!));

        // Partout, chaque entrée fait ce qu'elle dit, par les actions communes.
        var shared = new SharedSheetMenu(scene.Actions, scene.Opener);
        foreach (var item in SheetMenu.Items(scene.Target, shared).OfType<MenuItem>())
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        Assert.IsTrue(scene.Opener.Opened.Count >= 4, "Lire, Réviser, Écrire, Imprimer");
        var bare = new SharedSheetMenu(null, scene.Opener, read: _ => scene.Opener.Opened.Add("lu"), revise: _ => scene.Opener.Opened.Add("révisé"));
        Assert.IsFalse(bare.CanManage);
        Assert.IsFalse(bare.CanShare);
        CollectionAssert.AreEqual(new[] { EditingText.Read, ReadingText.Revise, EditingText.Write, PrintText.PrintAction }, Headers(SheetMenu.Items(scene.Target, bare)));
        bare.Read(scene.Target);
        bare.Revise(scene.Target);
        foreach (var act in new Action<SheetTarget>[] { bare.PostponeTomorrow, bare.Archive, bare.Restart, bare.ToggleReminders, bare.Rename, bare.Tags, bare.Move, bare.Share, bare.SaveAsTemplate, bare.ToggleNote, bare.Trash })
        {
            act(scene.Target);
        }
        Assert.IsEmpty(bare.Extensions(scene.Target));
        CollectionAssert.IsSubsetOf(new object[] { "lu", "révisé" }, scene.Opener.Opened);
    }

    [STATestMethod]
    public void A_submenu_keeps_the_look_of_the_menu_and_is_off_when_empty()
    {
        using var ui = new UiThread();
        var empty = ActionMenus.Submenu(ReadingText.SheetSubmenu, []);
        Assert.IsFalse(empty.IsEnabled);
        var full = ActionMenus.Submenu(ReadingText.SheetSubmenu, [new MenuItem { Header = "Lire" }]);
        Assert.IsTrue(full.IsEnabled);
        Assert.HasCount(1, full.Items);
        Assert.ThrowsExactly<ArgumentNullException>(() => ActionMenus.Submenu("x", null!));
    }

    private static List<string> Headers(IReadOnlyList<Control> items) => [.. items.OfType<MenuItem>().Select(item => (string)item.Header)];

    /// <summary>Un coffre avec une fiche « Mitose » en Biologie, un dossier Histoire, et une étiquette « bio » ailleurs.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        private readonly TestVaults _vaults;

        public Scene()
        {
            _vaults = new TestVaults(AppMode.Development, _time);
            var app = _vaults.App;
            app.Vaults.Create(_vaults.PathOf("Coffre"));
            var id = app.Sheets.Create("Biologie", "Mitose", null).Value;
            var other = app.Sheets.Create("Biologie", "Méiose", null).Value;
            app.Sheets.SetTags(other, ["bio"]).Value.ToString();
            app.Folders.Create("Histoire").Value.ToString();
            Target = new SheetTarget(id, "Mitose") { Folder = "Biologie" };
            // Deux jours plus tard : la fiche est en retard, on peut la reporter à demain.
            _time.AdvanceDays(2);
            Actions = new SheetActionsViewModel(app.Sheets, app.Folders, app.Reviews, app.Reminders, app.Trash, Opener, _time);
        }

        public FakeOpener Opener { get; } = new();

        public SheetActionsViewModel Actions { get; }

        public SheetTarget Target { get; }

        public VaultSheet Sheet() => _vaults.App.Sheets.List().Value.Single(sheet => sheet.Id == Target.Id);

        public Task UntilActingAsync() => UntilAsync(() => Actions.IsActing);

        /// <summary>Attend le message de l'action, sans délai fixe : on écoute ce que les actions annoncent.</summary>
        public async Task UntilDoneAsync(string done)
        {
            var before = Actions.Done;
            await UntilAsync(() => Actions.Done == done || Actions.Done != before);
            Assert.AreEqual(done, Actions.Done);
        }

        /// <summary>Attend que la condition soit vraie, en écoutant ce que les actions annoncent.</summary>
        public Task UntilAsync(Func<bool> condition)
        {
            // Sans fil d'interface, les actions finissent sur un fil du pool : on écoute avant de regarder, pour qu'un
            // changement tombé entre les deux ne soit pas manqué.
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Check(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (condition())
                {
                    Actions.PropertyChanged -= Check;
                    reached.TrySetResult();
                }
            }
            Actions.PropertyChanged += Check;
            Check(null, new System.ComponentModel.PropertyChangedEventArgs(null));
            return reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Dispose() => _vaults.Dispose();
    }
}
