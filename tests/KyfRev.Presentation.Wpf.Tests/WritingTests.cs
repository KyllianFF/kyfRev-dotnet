using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Rendering;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'écriture d'une fiche, dans une vraie fenêtre : le ruban, le champ, le rendu, l'enregistrement et les conflits.</summary>
[TestClass]
public sealed class WritingTests
{
    private const string Body = "# Mitose\n\nUn premier paragraphe.\n";

    /// <summary>Une fiche ouverte en écriture, dans une fenêtre.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly UiThread _ui = new();

        public Scene(string body = Body, double width = 1400, ThemeKind theme = ThemeKind.Light)
        {
            Vault = new ReadingVault(body, passage: false);
            Test = new TestShell();
            Test.Design.Theme = theme;
            Page = Vault.Page();
            Page.LoadCommand.Execute(null);
            UiThread.Until(() => !Page.IsLoading);
            SheetPage = new SheetPage { DataContext = Page, Width = width, Height = 900 };
            Window = new Window { Content = new Canvas { Children = { SheetPage } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            Window.Resources.MergedDictionaries.Add(Test.Design.Dictionary);
            Window.Show();
            TestShell.Flush();
            Page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => Page.Editor is not null);
            TestShell.Flush();
            Editor.SaveDelay = TimeSpan.FromMilliseconds(10);
            Editor.PreviewDelay = TimeSpan.FromMilliseconds(10);
            UiThread.Until(() => Editor.Preview is not null);
            TestShell.Flush();
        }

        public ReadingVault Vault { get; }

        public TestShell Test { get; }

        public SheetPageViewModel Page { get; }

        public SheetPage SheetPage { get; }

        public Window Window { get; }

        public SheetEditorViewModel Editor => Page.Editor!;

        public SheetEditorView View => SheetPage.EditorView;

        public MarkdownEditor Source => View.Source;

        public EditorRibbon Ribbon => SheetPage.Ribbon;

        public string OnDisk => Vault.App.Sheets.Open(Vault.SheetId).Value.Body;

        public void Type(string text)
        {
            Source.TextArea.Selection.ReplaceSelectionWithText(text);
            TestShell.Flush();
        }

        public void Select(int start, int length)
        {
            Source.Select(start, length);
            Source.TextArea.Caret.Offset = start + length;
            TestShell.Flush();
        }

        public void WaitSaved() => UiThread.Until(() => Editor.State == SaveState.Saved && ((IEditorSurface)Source).Text == OnDisk);

        public void Dispose()
        {
            Window.Close();
            Vault.Dispose();
            _ui.Dispose();
        }
    }

    private static void Click(ButtonBase button)
    {
        Assert.IsTrue(button.IsEnabled, button.Name);
        if (button is ToggleButton toggle)
        {
            ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();
        }
        else
        {
            ((IInvokeProvider)new ButtonAutomationPeer((Button)button).GetPattern(PatternInterface.Invoke)).Invoke();
        }
        TestShell.Flush();
    }

    private static void Choose(ContextMenu menu, string header)
    {
        var item = menu.Items.OfType<MenuItem>().First(candidate => (string)candidate.Header == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        TestShell.Flush();
    }

    private static void PressKey(UIElement target, Key key)
    {
        var source = PresentationSource.FromVisual(target)!;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        TestShell.Flush();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    private static string Written(Scene scene) => ((IEditorSurface)scene.Source).Text;

    [STATestMethod]
    public void Writing_shows_the_code_on_the_left_and_its_rendering_on_the_right()
    {
        using var scene = new Scene();

        Assert.AreEqual(Body, Written(scene));
        Assert.IsTrue(scene.Page.IsWriting);
        Assert.IsFalse(scene.Page.IsReading);
        Assert.AreEqual(Visibility.Visible, scene.View.Visibility);
        var code = scene.View.SourcePane.TranslatePoint(default, scene.View);
        var text = scene.View.PreviewPane.TranslatePoint(default, scene.View);
        Assert.IsLessThan(text.X, code.X, "Le code est à gauche.");
        Assert.Contains("Un premier paragraphe.", scene.Editor.Preview!.Map.Text);
        Assert.Contains("Enregistré", scene.SheetPage.SaveStateText.Text);
        Assert.IsTrue(scene.SheetPage.WriteToggle.IsChecked);
        Assert.IsFalse(scene.SheetPage.ReadToggle.IsChecked);
    }

    [STATestMethod]
    public void A_key_stroke_is_saved_after_a_pause_with_one_version_kept_per_session()
    {
        using var scene = new Scene();
        var title = scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Title;
        var versions = scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value.Count;

        scene.Select(Body.Length, 0);
        scene.Type("Une suite.");
        Assert.AreNotEqual(SaveState.Saved, scene.Editor.State);
        scene.WaitSaved();
        scene.Type(" Encore.");
        scene.WaitSaved();

        Assert.AreEqual(Body + "Une suite. Encore.", scene.OnDisk);
        Assert.AreEqual(title, scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Title);
        Assert.HasCount(versions + 1, scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value);
        Assert.IsTrue(scene.Editor.HasChanged);
    }

    [STATestMethod]
    public void Saves_run_one_at_a_time_and_the_last_text_wins()
    {
        using var scene = new Scene();
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);

        scene.Select(0, 0);
        scene.Type("A");
        var first = scene.Editor.SaveAsync();
        scene.Type("B");
        var second = scene.Editor.SaveAsync();
        Assert.AreSame(first, second);

        UiThread.Until(() => first.IsCompleted);
        Assert.AreEqual("AB" + Body, scene.OnDisk);
        Assert.AreEqual(SaveState.Saved, scene.Editor.State);
    }

    [STATestMethod]
    public void Windows_line_endings_stay_on_disk_and_the_field_only_sees_new_lines()
    {
        using var scene = new Scene("# Titre\r\n\r\nTexte\r\n");

        Assert.AreEqual("# Titre\n\nTexte\n", Written(scene));
        scene.Select(0, 0);
        scene.Type("A");
        UiThread.Until(() => scene.Editor.State == SaveState.Saved && scene.OnDisk.StartsWith('A'));

        Assert.AreEqual("A# Titre\r\n\r\nTexte\r\n", scene.OnDisk);
    }

    [STATestMethod]
    public void An_untouched_sheet_is_never_written()
    {
        using var scene = new Scene();
        var before = scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint;

        scene.Page.ToggleWritingCommand.Execute(null);
        UiThread.Until(() => scene.Page.Editor is null);

        Assert.AreEqual(before, scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint);
        Assert.IsTrue(scene.Page.IsReading);
    }

    [STATestMethod]
    public void Going_back_to_reading_saves_then_shows_the_new_text()
    {
        using var scene = new Scene();
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);

        scene.Select(Body.Length, 0);
        scene.Type("Une toute nouvelle phrase.");
        Click(scene.SheetPage.ReadToggle);
        UiThread.Until(() => scene.Page.Editor is null && !scene.Page.IsLoading && scene.Page.Reading!.Map.Text.Contains("nouvelle phrase", StringComparison.Ordinal));

        Assert.AreEqual(Body + "Une toute nouvelle phrase.", scene.OnDisk);
        Assert.IsTrue(scene.SheetPage.ReadToggle.IsChecked);
        Assert.IsFalse(scene.SheetPage.WriteToggle.IsChecked);

        Click(scene.SheetPage.ReadToggle);
        Assert.IsTrue(scene.SheetPage.ReadToggle.IsChecked, "Le mode en cours reste choisi.");
        Assert.IsTrue(scene.Page.IsReading);
        Click(scene.SheetPage.WriteToggle);
        UiThread.Until(() => scene.Page.IsWriting);
        Click(scene.SheetPage.WriteToggle);
        Assert.IsTrue(scene.SheetPage.WriteToggle.IsChecked, "Le mode en cours reste choisi.");
        Assert.IsFalse(scene.SheetPage.ReadToggle.IsChecked);
    }

    [STATestMethod]
    public void The_text_menu_of_reading_offers_to_edit_the_sheet()
    {
        using var scene = new Scene();
        scene.Page.ToggleWritingCommand.Execute(null);
        UiThread.Until(() => scene.Page.Editor is null);

        var item = scene.SheetPage.TextMenuItems(null).OfType<MenuItem>().Single();
        Assert.AreEqual(EditingText.EditSheet, item.Header);
        Assert.AreEqual(EditingText.WriteKey, item.InputGestureText);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        UiThread.Until(() => scene.Page.Editor is not null);
    }

    [STATestMethod]
    public void Back_saves_first_then_leaves()
    {
        using var scene = new Scene();
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);

        scene.Select(0, 0);
        scene.Type("A");
        scene.Page.BackCommand.Execute(null);
        UiThread.Until(() => scene.Vault.Backs == 1);

        Assert.AreEqual("A" + Body, scene.OnDisk);
    }

    [STATestMethod]
    public void Leaving_the_view_or_closing_the_window_saves_the_last_stroke()
    {
        using var scene = new Scene();
        scene.Editor.SaveDelay = TimeSpan.FromHours(1);

        scene.Select(0, 0);
        scene.Type("A");
        ((Canvas)scene.Window.Content).Children.Remove(scene.SheetPage);
        UiThread.Until(() => scene.OnDisk == "A" + Body);

        ((Canvas)scene.Window.Content).Children.Add(scene.SheetPage);
        TestShell.Flush();
        scene.Select(0, 0);
        scene.Type("B");
        scene.Window.Close();
        Assert.AreEqual("BA" + Body, scene.OnDisk);
    }

    [STATestMethod]
    public void The_history_compares_a_version_with_the_text_and_restores_it()
    {
        using var scene = new Scene("Première\n");
        scene.Select(0, 0);
        scene.Type("Ajout. ");
        UiThread.Until(() => scene.OnDisk.StartsWith("Ajout. ", StringComparison.Ordinal));

        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        TestShell.Flush();

        var history = scene.Editor.History!;
        Assert.IsTrue(scene.SheetPage.HistoryDialog.IsOpen);
        Assert.IsTrue(history.HasVersions);
        Assert.IsFalse(history.IsEmpty);
        var kept = history.Entries.Count;
        Assert.IsGreaterThanOrEqualTo(1, kept);
        Assert.AreSame(history.Entries[0], history.Selected);
        Assert.Contains("version", history.Summary);
        Assert.StartsWith("Version du ", history.VersionLabel);
        Assert.AreEqual(DiffKind.Changed, history.Rows![0].Kind);
        Assert.AreEqual("Première", history.Rows[0].Left);
        Assert.AreEqual("Ajout. Première", history.Rows[0].Right);
        Assert.AreSame(history.Rows, scene.SheetPage.HistoryDiff.Rows);

        history.AskRestoreCommand.Execute(null);
        Assert.IsTrue(history.IsConfirming);
        history.CancelRestoreCommand.Execute(null);
        Assert.IsTrue(history.IsNotConfirming);
        Assert.Contains("Ajout.", Written(scene), "Rien n'a changé.");

        history.AskRestoreCommand.Execute(null);
        history.RestoreCommand.Execute(null);
        UiThread.Until(() => !scene.Editor.IsHistoryOpen);

        Assert.AreEqual("Première\n", Written(scene));
        Assert.AreEqual("Première\n", scene.OnDisk);
        Assert.AreEqual(SaveState.Saved, scene.Editor.State);
        var versions = scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value;
        Assert.HasCount(kept + 1, versions);
        Assert.IsTrue(versions.Any(version => scene.Vault.App.Editing.ReadVersion(scene.Vault.SheetId, version.Name).Value.StartsWith("Ajout. ", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void A_sheet_without_a_version_says_so()
    {
        using var scene = new Scene("Première\n");
        var fresh = scene.Vault.App.Sheets.Create("Biologie", "Neuve", null).Value;

        var history = new VersionsViewModel(scene.Vault.App.Editing, fresh, string.Empty, (_, _) => Task.FromResult(false));

        Assert.IsTrue(history.IsEmpty);
        Assert.IsFalse(history.HasVersions);
        Assert.IsNull(history.Selected);
        Assert.IsNull(history.Rows);
        Assert.AreEqual(string.Empty, history.VersionLabel);
    }

    [STATestMethod]
    public void The_history_closes_by_its_button_and_opens_again()
    {
        using var scene = new Scene("Première\n");
        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        TestShell.Flush();
        Assert.IsTrue(scene.SheetPage.HistoryDialog.IsOpen);

        scene.SheetPage.HistoryDialog.Close(KyfRev.DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();
        Assert.IsFalse(scene.Editor.IsHistoryOpen);

        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        TestShell.Flush();
        Assert.IsTrue(scene.SheetPage.HistoryDialog.IsOpen);
    }

    [STATestMethod]
    public void A_version_that_cannot_be_read_is_said()
    {
        using var scene = new Scene("Première\n");
        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        var history = scene.Editor.History!;
        history.Selected = new VersionEntry(new KyfRev.Application.Model.SheetVersion("inconnue", DateTimeOffset.UnixEpoch, 2048));

        Assert.IsTrue(history.HasProblem);
        Assert.IsNull(history.Rows);
        Assert.AreEqual("2 ko", history.Selected.Size);

        history.RestoreCommand.Execute(null);
        Assert.IsTrue(scene.Editor.IsHistoryOpen);
    }

    [STATestMethod]
    public void Restoring_stops_at_a_conflict_and_nothing_is_lost()
    {
        using var scene = new Scene("Première\n");
        scene.Select(0, 0);
        scene.Type("Ajout. ");
        UiThread.Until(() => scene.OnDisk.StartsWith("Ajout. ", StringComparison.Ordinal));
        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        var history = scene.Editor.History!;
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Sur le disque.", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);

        history.RestoreCommand.Execute(null);
        UiThread.Until(() => history.HasProblem);

        Assert.AreEqual(EditingText.VersionNotRestored, history.Problem);
        Assert.IsTrue(history.IsNotConfirming);
        Assert.AreEqual(SaveState.Conflict, scene.Editor.State);
        Assert.AreEqual("Sur le disque.", scene.OnDisk);
        Assert.Contains("Ajout.", Written(scene));
    }

    [STATestMethod]
    public void Keeping_my_version_after_a_refused_restore_writes_it()
    {
        using var scene = new Scene("Première\n");
        scene.Select(0, 0);
        scene.Type("Ajout. ");
        UiThread.Until(() => scene.OnDisk.StartsWith("Ajout. ", StringComparison.Ordinal));
        scene.WaitSaved();
        scene.Editor.OpenHistoryCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsHistoryOpen);
        var history = scene.Editor.History!;

        // Un autre programme change la fiche : la version n'est pas remise, et rien n'attend de s'écrire.
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Sur le disque.\n", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);
        history.AskRestoreCommand.Execute(null);
        history.RestoreCommand.Execute(null);
        UiThread.Until(() => scene.Editor.State == SaveState.Conflict);

        scene.Editor.KeepMineCommand.Execute(null);

        UiThread.Until(() => scene.Editor.State == SaveState.Saved);
        Assert.StartsWith("Ajout. ", scene.OnDisk, "ma version est écrite, et non seulement annoncée");
    }

    [TestMethod]
    public void Changing_the_model_keeps_what_is_written()
    {
        var existing = DiagramGalleryViewModel.ForExisting("smartart cycle", "Mes\nÉléments", 3)!;
        existing.SelectedItem = existing.Items.First(item => item.Id == "pyramide");
        Assert.AreEqual("Mes\nÉléments", existing.Text, "c'est ce que propose Office");

        var fresh = DiagramGalleryViewModel.For(GalleryKind.SmartArt);
        var untouched = fresh.Text;
        fresh.SelectedItem = fresh.Items[^1];
        Assert.AreEqual(fresh.Items[^1].Sample, fresh.Text, "un exemple intact suit le modèle");
        Assert.AreNotEqual(string.Empty, untouched);

        fresh.Text = "À moi";
        fresh.SelectedItem = fresh.Items[0];
        Assert.AreEqual("À moi", fresh.Text);

        fresh.Text = "   ";
        fresh.SelectedItem = fresh.Items[1];
        Assert.AreEqual(fresh.Items[1].Sample, fresh.Text, "un champ vide reprend l'exemple");
    }

    [STATestMethod]
    public void A_conflict_compares_the_disk_with_my_text_and_closes()
    {
        using var scene = new Scene("Commun\nPremière\n");
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Commun\nSur le disque\n", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);
        scene.Select(0, 0);
        scene.Type("Ma ");
        UiThread.Until(() => scene.Editor.State == SaveState.Conflict);

        scene.Editor.CompareConflictCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsComparingConflict);
        TestShell.Flush();

        Assert.IsTrue(scene.SheetPage.ConflictDialog.IsOpen);
        var rows = scene.Editor.ConflictRows!;
        Assert.AreEqual("Commun", rows[0].Left);
        Assert.AreEqual("Ma Commun", rows[0].Right);
        Assert.AreEqual("Sur le disque", rows[1].Left);
        Assert.AreEqual("Première", rows[1].Right);

        scene.SheetPage.ConflictDialog.Close(KyfRev.DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();
        UiThread.Until(() => !scene.Editor.IsComparingConflict);

        scene.Editor.CompareConflictCommand.Execute(null);
        UiThread.Until(() => scene.Editor.IsComparingConflict);
        scene.Editor.KeepMineCommand.Execute(null);
        UiThread.Until(() => scene.Editor.State == SaveState.Saved);
        Assert.IsFalse(scene.Editor.IsComparingConflict);
    }

    [STATestMethod]
    public void A_change_on_disk_is_a_conflict_that_writes_nothing()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Sur le disque.", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);

        scene.Select(0, 0);
        scene.Type("Ma frappe. ");
        UiThread.Until(() => scene.Editor.State == SaveState.Conflict);

        Assert.AreEqual("Sur le disque.", scene.OnDisk);
        Assert.AreEqual(Visibility.Visible, scene.SheetPage.ConflictPanel.Visibility);
        Assert.AreEqual(EditingText.InConflict, scene.Editor.StateLabel);

        scene.Type("Encore. ");
        Assert.AreEqual(SaveState.Conflict, scene.Editor.State, "Écrire ne le tranche pas.");
        scene.Page.StopWritingCommand.Execute(null);
        TestShell.Flush();
        UiThread.Until(() => !scene.Page.StopWritingCommand.IsRunning);
        Assert.IsTrue(scene.Page.IsWriting, "On ne quitte pas sans trancher.");
        scene.Page.BackCommand.Execute(null);
        UiThread.Until(() => !scene.Page.BackCommand.IsRunning);
        Assert.AreEqual(0, scene.Vault.Backs);
    }

    [STATestMethod]
    public void Keeping_my_version_puts_the_disk_one_in_the_history()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Sur le disque.", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);
        scene.Select(0, 0);
        scene.Type("Ma frappe. ");
        UiThread.Until(() => scene.Editor.State == SaveState.Conflict);
        var versions = scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value.Count;

        Click(scene.SheetPage.KeepMineButton);
        scene.WaitSaved();

        Assert.AreEqual("Ma frappe. " + Body, scene.OnDisk);
        Assert.IsGreaterThanOrEqualTo(versions, scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value.Count);
        Assert.AreEqual(Visibility.Collapsed, scene.SheetPage.ConflictPanel.Visibility);
    }

    [STATestMethod]
    public void Reloading_the_disk_keeps_my_text_in_the_history()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Vault.App.Editing.Save(scene.Vault.SheetId, "Sur le disque.", scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value.Fingerprint, 99).Succeeded);
        scene.Select(0, 0);
        scene.Type("Ma frappe. ");
        UiThread.Until(() => scene.Editor.State == SaveState.Conflict);

        Click(scene.SheetPage.ReloadDiskButton);
        UiThread.Until(() => scene.Editor.State == SaveState.Saved);

        Assert.AreEqual("Sur le disque.", Written(scene));
        Assert.AreEqual("Sur le disque.", scene.OnDisk);
        var versions = scene.Vault.App.Editing.Versions(scene.Vault.SheetId).Value;
        Assert.IsTrue(versions.Any(version => scene.Vault.App.Editing.ReadVersion(scene.Vault.SheetId, version.Name).Value.Contains("Ma frappe.", StringComparison.Ordinal)));
    }

    [STATestMethod]
    public void Without_a_conflict_its_answers_do_nothing()
    {
        using var scene = new Scene();

        scene.Editor.KeepMineCommand.Execute(null);
        scene.Editor.ReloadDiskCommand.Execute(null);
        TestShell.Flush();

        Assert.AreEqual(SaveState.Saved, scene.Editor.State);
        Assert.AreEqual(Body, Written(scene));
    }

    [STATestMethod]
    public void A_refused_save_is_shown_and_can_be_retried()
    {
        using var scene = new Scene();
        var sheet = scene.Vault.App.Sheets.Open(scene.Vault.SheetId).Value;
        var editor = new SheetEditorViewModel(sheet with { Id = "inconnue" }, scene.Vault.App.Editing, scene.Vault.App.Sheets, FakeEnvironment.Markdown())
        {
            SaveDelay = TimeSpan.FromMilliseconds(10),
        };
        var surface = new MarkdownEditor();
        editor.Attach(surface, () => null);

        surface.Document.Insert(0, "A");
        UiThread.Until(() => editor.State == SaveState.Failed);

        Assert.IsTrue(editor.HasFailed);
        Assert.IsTrue(editor.HasProblem);
        Assert.AreEqual(EditingText.SaveFailed, editor.StateLabel);
        editor.RetryCommand.Execute(null);
        UiThread.Until(() => editor.State == SaveState.Failed);
        surface.Document.Insert(0, "B");
        Assert.AreEqual(SaveState.Modified, editor.State);
        Assert.AreEqual(EditingText.Modified, editor.StateLabel);
        UiThread.Until(() => editor.State == SaveState.Failed);
        editor.DismissProblemCommand.Execute(null);
        Assert.IsFalse(editor.HasProblem);
        var finished = editor.FinishAsync();
        UiThread.Until(() => finished.IsCompleted);
        Assert.IsFalse(finished.Result);
    }

    [STATestMethod]
    public void The_marks_of_the_ribbon_wrap_the_selection_and_undo_takes_them_back()
    {
        using var scene = new Scene("un mot ici");
        var ribbon = scene.Ribbon;
        scene.Select(3, 3);
        Assert.IsFalse(scene.Editor.UndoCommand.CanExecute(null));

        Click(ribbon.BoldButton);
        Assert.AreEqual("un **mot** ici", Written(scene));
        Assert.AreEqual(5, scene.Source.SelectionStart);
        Assert.AreEqual(3, scene.Source.SelectionLength);

        Click(ribbon.ItalicButton);
        Assert.AreEqual("un ***mot*** ici", Written(scene));
        Click(ribbon.UnderlineButton);
        Assert.AreEqual("un ***<u>mot</u>*** ici", Written(scene));
        Click(ribbon.StrikeButton);
        Assert.AreEqual("un ***<u>~~mot~~</u>*** ici", Written(scene));
        Click(ribbon.SuperscriptButton);
        Assert.AreEqual("un ***<u>~~<sup>mot</sup>~~</u>*** ici", Written(scene));
        Click(ribbon.SubscriptButton);
        Assert.AreEqual("un ***<u>~~<sup><sub>mot</sub></sup>~~</u>*** ici", Written(scene));

        Assert.IsTrue(scene.Editor.UndoCommand.CanExecute(null));
        Click(ribbon.UndoButton);
        Assert.AreEqual("un ***<u>~~<sup>mot</sup>~~</u>*** ici", Written(scene));
        Assert.IsTrue(scene.Editor.RedoCommand.CanExecute(null));
        Click(ribbon.RedoButton);
        Assert.AreEqual("un ***<u>~~<sup><sub>mot</sub></sup>~~</u>*** ici", Written(scene));
    }

    [STATestMethod]
    public void The_style_list_and_case_menus_act_on_the_text()
    {
        using var scene = new Scene("un mot");
        var ribbon = scene.Ribbon;
        scene.Select(3, 0);
        Assert.AreEqual(EditingText.NormalText, scene.Editor.StyleLabel);

        Choose(ribbon.StyleMenu, EditingText.Heading(2));
        Assert.AreEqual("## un mot", Written(scene));
        Assert.AreEqual(EditingText.Heading(2), scene.Editor.StyleLabel);
        Choose(ribbon.StyleMenu, EditingText.NormalText);
        Assert.AreEqual("un mot", Written(scene));
        Assert.AreEqual(EditingText.NormalText, scene.Editor.StyleLabel);

        Click(ribbon.BulletsButton);
        Assert.AreEqual("- un mot", Written(scene));
        Click(ribbon.NumbersButton);
        Assert.AreEqual("1. un mot", Written(scene));
        Click(ribbon.NumbersButton);
        Assert.AreEqual("un mot", Written(scene));

        scene.Select(0, 6);
        Choose(ribbon.CaseMenu, EditingText.CaseUpper);
        Assert.AreEqual("UN MOT", Written(scene));
        Choose(ribbon.CaseMenu, EditingText.CaseInvert);
        Assert.AreEqual("un mot", Written(scene));
        Choose(ribbon.CaseMenu, EditingText.CaseAlternate);
        Assert.AreEqual("Un MoT", Written(scene));
        Choose(ribbon.CaseMenu, EditingText.CaseWords);
        Assert.AreEqual("Un Mot", Written(scene));
        Choose(ribbon.CaseMenu, EditingText.CaseLower);
        Assert.AreEqual("un mot", Written(scene));
        Choose(ribbon.CaseMenu, EditingText.CaseSentence);
        Assert.AreEqual("Un mot", Written(scene));
    }

    [STATestMethod]
    public void The_ribbon_menus_open_under_their_button()
    {
        using var scene = new Scene();
        var ribbon = scene.Ribbon;

        ribbon.CaseButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();

        Assert.IsTrue(ribbon.CaseMenu.IsOpen);
        Assert.AreSame(ribbon.CaseButton, ribbon.CaseMenu.PlacementTarget);
        ribbon.CaseMenu.IsOpen = false;
    }

    [STATestMethod]
    public void The_insert_tab_shows_its_tools_and_the_home_tab_comes_back()
    {
        using var scene = new Scene();
        var ribbon = scene.Ribbon;
        Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);
        Assert.AreEqual(Visibility.Collapsed, ribbon.InsertPanel.Visibility);

        ribbon.InsertTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreEqual(Visibility.Collapsed, ribbon.HomePanel.Visibility);
        Assert.AreEqual(Visibility.Visible, ribbon.InsertPanel.Visibility);
        Assert.IsFalse(ribbon.HomeTab.IsChecked);
        Assert.IsTrue(ribbon.InsertTab.IsChecked);

        ribbon.HomeTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);
        Assert.IsTrue(ribbon.HomeTab.IsChecked);
    }

    [STATestMethod]
    public void The_table_grid_follows_the_keyboard_and_inserts()
    {
        using var scene = new Scene("|");
        var ribbon = scene.Ribbon;
        ribbon.InsertTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();
        scene.Select(1, 0);

        ribbon.TableButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();
        Assert.IsTrue(ribbon.TablePopup.IsOpen);
        var chooser = ribbon.Chooser;
        Assert.AreEqual("3 colonnes × 3 lignes", chooser.Label);
        foreach (var key in new[] { Key.Right, Key.Down, Key.Down, Key.Left, Key.Up, Key.Right })
        {
            PressKey(chooser, key);
        }
        Assert.AreEqual(4, chooser.Rows);
        Assert.AreEqual(4, chooser.Columns);
        PressKey(chooser, Key.A);
        Assert.AreEqual(4, chooser.Rows);
        for (var i = 0; i < 6; i++)
        {
            PressKey(chooser, Key.Up);
            PressKey(chooser, Key.Left);
        }
        Assert.AreEqual("1 colonne × 1 ligne", chooser.Label);
        PressKey(chooser, Key.Right);
        PressKey(chooser, Key.Enter);

        Assert.IsFalse(ribbon.TablePopup.IsOpen);
        Assert.AreEqual("|\n\n| Colonne 1 | Colonne 2 |\n| --- | --- |\n|   |   |\n", Written(scene));
    }

    [STATestMethod]
    public void The_table_grid_follows_the_mouse()
    {
        using var scene = new Scene(string.Empty);
        var ribbon = scene.Ribbon;
        var chooser = ribbon.Chooser;
        ribbon.TablePopup.IsOpen = true;
        TestShell.Flush();
        var cells = Descendants(chooser).OfType<UniformGrid>().Single().Children.OfType<Border>().ToList();
        var cell = cells[(1 * TableChooser.Maximum) + 3];

        cell.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Assert.AreEqual(2, chooser.Rows);
        Assert.AreEqual(4, chooser.Columns);
        cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent, Source = cell });
        cell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        TestShell.Flush();

        Assert.Contains("Colonne 4", Written(scene));
        ribbon.TablePopup.IsOpen = false;
    }

    [STATestMethod]
    public void The_code_menu_creates_a_block_then_changes_its_language()
    {
        using var scene = new Scene(string.Empty);
        var ribbon = scene.Ribbon;

        Choose(ribbon.CodeMenu, "Python");
        Assert.AreEqual("```python\n\n```\n", Written(scene));
        Assert.AreEqual("```python\n".Length, scene.Source.TextArea.Caret.Offset, "Le curseur est dans le bloc.");

        Choose(ribbon.CodeMenu, "C#");
        Assert.AreEqual("```csharp\n\n```\n", Written(scene));
        Choose(ribbon.CodeMenu, EditingText.PlainCode);
        Assert.AreEqual("```\n\n```\n", Written(scene));
    }

    [STATestMethod]
    public void Rule_and_formula_are_written_as_markdown()
    {
        using var scene = new Scene("texte");
        var ribbon = scene.Ribbon;
        ribbon.InsertTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();

        scene.Select(0, 5);
        Click(ribbon.FormulaButton);
        Assert.AreEqual("$texte$", Written(scene));
        scene.Select(7, 0);
        Click(ribbon.RuleButton);
        Assert.AreEqual("$texte$\n\n---\n", Written(scene));
    }

    [STATestMethod]
    public void An_example_of_formula_fills_the_field()
    {
        using var scene = new Scene("texte");

        scene.Editor.UseFormulaExampleCommand.Execute(SheetEditorViewModel.FormulaExamples[1]);

        Assert.AreEqual("\\frac{a}{b}", scene.Editor.FormulaTex);
    }

    [STATestMethod]
    public void The_preview_draws_a_formula_and_shows_what_it_cannot_draw()
    {
        var preview = new FormulaPreview();
        Assert.IsNull(preview.Content);

        preview.Tex = "\\frac{a}{b}";
        Assert.IsInstanceOfType<Viewbox>(preview.Content);

        preview.Tex = "\\nope{y}";
        Assert.AreEqual("$$\\nope{y}$$", ((TextBlock)preview.Content).Text, "ce qui ne se dessine pas reste lisible");

        preview.Tex = "   ";
        Assert.IsNull(preview.Content);
    }

    [STATestMethod]
    public void A_link_is_asked_with_the_selection_as_its_text()
    {
        using var scene = new Scene("voir la page ici");
        var ribbon = scene.Ribbon;
        ribbon.InsertTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();
        scene.Select(5, 7);

        Click(ribbon.LinkButton);
        Assert.IsTrue(scene.Editor.IsLinkOpen);
        Assert.AreEqual("la page", scene.Editor.LinkLabel);

        scene.Editor.LinkAddress = "https://exemple.org";
        scene.Editor.AnswerLinkCommand.Execute(true);
        Assert.IsFalse(scene.Editor.IsLinkOpen);
        Assert.AreEqual("voir [la page](https://exemple.org) ici", Written(scene));

        scene.Editor.StartLinkCommand.Execute(null);
        scene.Editor.LinkLabel = string.Empty;
        scene.Editor.LinkAddress = "https://a.org";
        scene.Editor.AnswerLinkCommand.Execute(true);
        Assert.Contains("[https://a.org](https://a.org)", Written(scene));

        var before = Written(scene);
        scene.Editor.StartLinkCommand.Execute(null);
        scene.Editor.AnswerLinkCommand.Execute(false);
        Assert.AreEqual(before, Written(scene));
        scene.Editor.StartLinkCommand.Execute(null);
        scene.Editor.LinkAddress = "https://b.org";
        scene.SheetPage.LinkDialog.IsOpen = false;
        TestShell.Flush();
        Assert.AreEqual(before, Written(scene));
    }

    private static DataObject Files(params string[] paths) => new(DataFormats.FileDrop, paths);

    private static string TemporaryImage(string name, int size = 16)
    {
        var folder = Directory.CreateTempSubdirectory("kyfrev-image").FullName;
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    [STATestMethod]
    public void Chosen_images_go_next_to_the_sheet_and_their_links_into_the_text()
    {
        using var scene = new Scene("texte\n");
        scene.Select(6, 0);
        var path = TemporaryImage("Schéma [1].png");
        scene.View.PickImages = () => [path];

        scene.Ribbon.ImagesButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        UiThread.Until(() => Written(scene).Contains("![Schéma 1]", StringComparison.Ordinal));

        Assert.IsFalse(scene.Editor.HasProblem);
        Assert.StartsWith("texte\n![Schéma 1](", Written(scene));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

        scene.View.PickImages = () => [];
        var before = Written(scene);
        scene.Ribbon.ImagesButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        TestShell.Flush();
        Assert.AreEqual(before, Written(scene));
    }

    [STATestMethod]
    public void An_image_refused_or_too_large_is_said_and_the_text_stays()
    {
        using var scene = new Scene("texte");
        var wrong = TemporaryImage("notes.txt");
        var huge = TemporaryImage("grande.png");
        using (var stream = new FileStream(huge, FileMode.Open))
        {
            stream.SetLength(SheetEditorViewModel.MaximumImage + 1);
        }

        var refused = scene.Editor.InsertImageFilesAsync([wrong]);
        UiThread.Until(() => refused.IsCompleted);
        Assert.Contains("notes.txt", scene.Editor.Problem!);
        var tooLarge = scene.Editor.InsertImageFilesAsync([huge]);
        UiThread.Until(() => tooLarge.IsCompleted);
        Assert.AreEqual(EditingText.ImageTooLarge("grande.png"), scene.Editor.Problem);
        var missing = scene.Editor.InsertImageFilesAsync([Path.Combine(Path.GetDirectoryName(huge)!, "absente.png")]);
        UiThread.Until(() => missing.IsCompleted);
        Assert.IsTrue(scene.Editor.HasProblem);

        Assert.AreEqual("texte", Written(scene));
        Directory.Delete(Path.GetDirectoryName(wrong)!, recursive: true);
        Directory.Delete(Path.GetDirectoryName(huge)!, recursive: true);
    }

    [STATestMethod]
    public void Pasted_text_keeps_new_lines_and_a_pasted_image_goes_next_to_the_sheet()
    {
        using var scene = new Scene("x");
        scene.Select(1, 0);

        scene.Source.Paste(new DataObject(DataFormats.UnicodeText, "un\r\ndeux\rtrois"));
        Assert.AreEqual("xun\ndeux\ntrois", Written(scene));

        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
        scene.Source.Paste(new DataObject(DataFormats.Bitmap, bitmap));
        UiThread.Until(() => Written(scene).Contains("![image](", StringComparison.Ordinal));

        var path = TemporaryImage("collee.png");
        var files = Files(path, "notes.txt");
        scene.Source.Paste(files);
        UiThread.Until(() => Written(scene).Contains("![collee](", StringComparison.Ordinal));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);

        var before = Written(scene);
        scene.Source.Paste(new DataObject(DataFormats.Html, "<b>rien</b>"));
        Assert.AreEqual(before, Written(scene));
    }

    [STATestMethod]
    public void A_busy_clipboard_is_said_and_nothing_is_pasted()
    {
        using var scene = new Scene("x");

        scene.Editor.Report(EditingText.ClipboardBusy);

        Assert.AreEqual(EditingText.ClipboardBusy, scene.Editor.Problem);
        Assert.IsTrue(scene.SheetPage.DismissButton.Command.CanExecute(null));
        Assert.IsTrue(System.Windows.Input.ApplicationCommands.Paste.CanExecute(null, scene.Source.TextArea), "Vérifier le collage n'ouvre pas le presse-papiers, qu'un autre programme peut tenir.");
        scene.SheetPage.DismissButton.Command.Execute(null);
        Assert.IsFalse(scene.Editor.HasProblem);
    }

    [STATestMethod]
    public void The_gallery_shows_its_models_by_category_and_follows_the_text()
    {
        var gallery = DiagramGalleryViewModel.For(GalleryKind.SmartArt);

        Assert.AreEqual(EditingText.GallerySmartArtTitle, gallery.Title);
        Assert.AreEqual(EditingText.GalleryAll, gallery.Categories[0]);
        Assert.HasCount(17, gallery.Items);
        Assert.IsTrue(gallery.Items.All(item => item.Thumbnail.Shapes.Length > 0));
        Assert.AreEqual(gallery.Items[0], gallery.SelectedItem);
        Assert.AreEqual(gallery.Items[0].Sample, gallery.Text);

        gallery.SelectedCategory = "Cycle";
        Assert.AreEqual("cycle radial", string.Join(' ', gallery.Items.Select(item => item.Id)));
        Assert.AreEqual("cycle", gallery.SelectedItem!.Id, "Le modèle choisi n'est plus montré : le premier le remplace.");
        Assert.AreEqual(gallery.SelectedItem.Sample, gallery.Text);

        var previews = new List<string?>();
        gallery.PropertyChanged += (_, e) => previews.Add(e.PropertyName);
        gallery.Text = "Une\nDeux";
        Assert.Contains(nameof(DiagramGalleryViewModel.Preview), previews);
        Assert.AreEqual("Cycle : Une, Deux", gallery.Preview!.Description);
        Assert.AreEqual(("smartart cycle", "Une\nDeux"), gallery.Source());

        gallery.SelectedCategory = EditingText.GalleryAll;
        Assert.AreEqual("cycle", gallery.SelectedItem!.Id, "Toujours montré : il reste choisi.");
        Assert.AreEqual("Une\nDeux", gallery.Text);
    }

    [STATestMethod]
    [DataRow(GalleryKind.Schema, EditingText.GallerySchemaTitle, 9, EditingText.GalleryOnePerLine)]
    [DataRow(GalleryKind.Chart, EditingText.GalleryChartTitle, 6, EditingText.GalleryOneValuePerLine)]
    public void The_gallery_of_schemas_and_charts_writes_mermaid(GalleryKind kind, string title, int count, string help)
    {
        var gallery = DiagramGalleryViewModel.For(kind);

        Assert.AreEqual(title, gallery.Title);
        Assert.HasCount(count, gallery.Items);
        Assert.AreEqual(help, gallery.InputHelp);
        Assert.IsTrue(gallery.IsMermaid);
        Assert.IsFalse(gallery.IsDrawn);
        Assert.IsNull(gallery.Preview);
        Assert.IsNotNull(gallery.MermaidCode);
        Assert.AreEqual((KyfRev.Rendering.Diagrams.MermaidTemplates.Language, gallery.MermaidCode), gallery.Source());
    }

    [STATestMethod]
    public void The_gallery_opens_on_schemas_from_the_ribbon_and_reads_as_code_without_the_hidden_page()
    {
        using var scene = new Scene("Avant\n");

        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.Schema);
        TestShell.Flush();

        var view = scene.SheetPage.GalleryContent;
        Assert.AreEqual(Visibility.Visible, view.MermaidPreview.IsVisible ? Visibility.Visible : Visibility.Collapsed);
        Assert.IsFalse(view.PreviewView.IsVisible);
        Assert.IsTrue(view.MermaidPreview.ShowsCode);
        Assert.AreEqual(scene.Editor.Gallery!.MermaidCode, view.MermaidPreview.Code);

        scene.Editor.Gallery.ShowsCode = true;
        TestShell.Flush();
        Assert.IsTrue(view.CodeBox.IsVisible);

        scene.Editor.AnswerGalleryCommand.Execute(true);
        Assert.Contains("```mermaid\nflowchart LR", scene.Source.Text);
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void A_check_box_takes_the_text_color_of_the_theme(ThemeKind theme)
    {
        using var scene = new Scene("Avant\n");
        scene.Test.Design.Theme = theme;
        TestShell.Flush();
        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.Schema);
        TestShell.Flush();

        var box = scene.SheetPage.GalleryContent.CodeToggle;
        box.ApplyTemplate();
        var text = (System.Windows.Media.SolidColorBrush)box.Foreground;
        var ground = ThemeColors.For(theme)[ColorRole.Surface];

        Assert.IsGreaterThanOrEqualTo(4.5, new TokenColor(text.Color.A, text.Color.R, text.Color.G, text.Color.B).ContrastWith(ground));
    }

    [STATestMethod]
    public void The_gallery_has_nothing_to_write_without_text()
    {
        var gallery = DiagramGalleryViewModel.For(GalleryKind.SmartArt);

        gallery.Text = " \n ";

        Assert.IsNull(gallery.Source());
        gallery.SelectedItem = null;
        Assert.IsNull(gallery.Source());
        Assert.IsNull(gallery.Preview);
    }

    [STATestMethod]
    [DataRow("smartart cycle", "a\nb", "cycle")]
    [DataRow("smartart  matrice", "a", "matrice")]
    public void The_gallery_opens_on_a_smartart_of_the_sheet(string info, string code, string layout)
    {
        var gallery = DiagramGalleryViewModel.ForExisting(info, code, 12)!;

        Assert.AreEqual(layout, gallery.SelectedItem!.Id);
        Assert.AreEqual(code, gallery.Text);
        Assert.AreEqual(12, gallery.Position);
    }

    [STATestMethod]
    [DataRow("python")]
    [DataRow("smartart")]
    [DataRow("smartart inconnu")]
    [DataRow("mermaid")]
    public void The_gallery_stays_closed_on_anything_else(string info) =>
        Assert.IsNull(DiagramGalleryViewModel.ForExisting(info, "x", 0));

    [STATestMethod]
    public void The_gallery_writes_a_code_block_in_the_markdown()
    {
        using var scene = new Scene("Avant\n");
        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        Assert.IsTrue(scene.Editor.IsGalleryOpen);
        scene.Editor.Gallery!.Text = "Un\nDeux";

        scene.Editor.AnswerGalleryCommand.Execute(false);
        Assert.IsFalse(scene.Editor.IsGalleryOpen);
        Assert.DoesNotContain("smartart", scene.Source.Text);

        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        scene.Editor.Gallery!.Text = "Un\nDeux";
        scene.Editor.AnswerGalleryCommand.Execute(true);
        Assert.Contains("```smartart blocs\nUn\nDeux\n```", scene.Source.Text);
    }

    [STATestMethod]
    public void The_gallery_is_a_list_of_drawings_with_their_names()
    {
        using var scene = new Scene("Avant\n");
        scene.Editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        TestShell.Flush();

        var view = scene.SheetPage.GalleryContent;
        Assert.AreEqual(scene.Editor.Gallery, view.DataContext);
        Assert.IsTrue(scene.SheetPage.GalleryDialog.IsOpen);
        Assert.HasCount(17, view.ItemList.Items);
        Assert.AreEqual(scene.Editor.Gallery!.Categories.Count, view.CategoryList.Items.Count);
        Assert.AreEqual(scene.Editor.Gallery.Preview!.Description, view.PreviewView.Scene!.Description);
    }

    [STATestMethod]
    public void A_dropped_image_lands_where_it_is_dropped()
    {
        using var scene = new Scene("début\n\nfin");
        var path = TemporaryImage("posee.png");

        Assert.IsTrue(scene.Source.DropImages(Files(path), 6));
        UiThread.Until(() => Written(scene).Contains("![posee](", StringComparison.Ordinal));
        Assert.StartsWith("début\n![posee](", Written(scene));
        Assert.IsFalse(scene.Source.DropImages(Files("notes.txt"), 0));
        Assert.IsFalse(scene.Source.DropImages(new DataObject(DataFormats.UnicodeText, "texte"), 0));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [STATestMethod]
    public void Double_brackets_propose_the_other_sheets_and_a_choice_writes_an_ordinary_link()
    {
        using var scene = new Scene("Voir ");
        var app = scene.Vault.App;
        app.Sheets.Create("Biologie", "Méiose", null);
        app.Sheets.Create("Histoire", "Révolution", null);
        scene.Select(5, 0);

        scene.Type("[[me");
        UiThread.Until(() => scene.Editor.HasProposals);
        Assert.HasCount(1, scene.Editor.Proposals);
        Assert.IsTrue(scene.View.ProposalsPopup.IsOpen);
        Assert.AreEqual("Méiose", scene.Editor.ActiveProposal!.Title);

        Assert.IsTrue(scene.Editor.TakeKey(Key.Enter, ModifierKeys.None));
        Assert.AreEqual("Voir [Méiose](M%C3%A9iose.md)".Replace("M%C3%A9iose.md", "Méiose.md", StringComparison.Ordinal), Written(scene));
        Assert.IsFalse(scene.Editor.HasProposals);
        Assert.IsFalse(scene.View.ProposalsPopup.IsOpen);
    }

    [STATestMethod]
    public void The_list_of_sheets_takes_its_keys_and_stays_closed_after_escape()
    {
        using var scene = new Scene("Voir ");
        var app = scene.Vault.App;
        app.Sheets.Create("Biologie", "Méiose", null);
        app.Sheets.Create("Histoire", "Révolution", null);
        scene.Select(5, 0);
        scene.Type("[[");
        UiThread.Until(() => scene.Editor.Proposals.Count == 2);
        var first = scene.Editor.ActiveProposal!;

        Assert.IsTrue(scene.Editor.TakeKey(Key.Down, ModifierKeys.None));
        Assert.AreNotEqual(first, scene.Editor.ActiveProposal);
        Assert.IsTrue(scene.Editor.TakeKey(Key.Up, ModifierKeys.None));
        Assert.AreEqual(first, scene.Editor.ActiveProposal);
        Assert.IsTrue(scene.Editor.TakeKey(Key.Up, ModifierKeys.None));
        Assert.AreNotEqual(first, scene.Editor.ActiveProposal);
        Assert.IsFalse(scene.Editor.TakeKey(Key.A, ModifierKeys.None));

        Assert.IsTrue(scene.Editor.TakeKey(Key.Escape, ModifierKeys.None));
        Assert.IsFalse(scene.Editor.HasProposals);
        scene.Type("m");
        Assert.IsFalse(scene.Editor.HasProposals, "Échap vaut pour cette saisie.");
        Assert.IsFalse(scene.Editor.TakeKey(Key.Escape, ModifierKeys.None));

        scene.Type(" [[");
        Assert.IsTrue(scene.Editor.HasProposals, "Une nouvelle saisie rouvre la liste.");
        scene.Type("zzz");
        Assert.IsFalse(scene.Editor.HasProposals);
        Assert.IsFalse(scene.Editor.TakeKey(Key.Enter, ModifierKeys.None));
        scene.Editor.ChooseProposalCommand.Execute(first);
    }

    [STATestMethod]
    public void A_choice_also_replaces_closing_brackets_and_a_click_chooses()
    {
        using var scene = new Scene("[[]]");
        scene.Vault.App.Sheets.Create("Biologie", "Méiose", null);
        scene.Select(2, 0);
        UiThread.Until(() => scene.Editor.HasProposals);

        var item = (ListBoxItem)scene.View.ProposalsList.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.IsNotNull(item);
        item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
        TestShell.Flush();

        Assert.AreEqual("[Méiose](Méiose.md)", Written(scene));
    }

    [STATestMethod]
    [DataRow("`[[")]
    [DataRow("```\n[[")]
    public void No_list_of_sheets_in_code(string text)
    {
        using var scene = new Scene(string.Empty);
        scene.Vault.App.Sheets.Create("Biologie", "Méiose", null);

        scene.Type(text);
        TestShell.Flush();

        Assert.IsFalse(scene.Editor.HasProposals);
    }

    [STATestMethod]
    public void The_keys_of_the_text_act_on_it()
    {
        using var scene = new Scene("un mot");
        scene.Select(3, 3);

        Assert.IsTrue(scene.Editor.TakeKey(Key.B, ModifierKeys.Control));
        Assert.AreEqual("un **mot**", Written(scene));
        Assert.IsTrue(scene.Editor.TakeKey(Key.I, ModifierKeys.Control));
        Assert.AreEqual("un ***mot***", Written(scene));
        Assert.IsTrue(scene.Editor.TakeKey(Key.U, ModifierKeys.Control));
        Assert.AreEqual("un ***<u>mot</u>***", Written(scene));
        Assert.IsTrue(scene.Editor.TakeKey(Key.F3, ModifierKeys.Shift));
        Assert.AreEqual("un ***<u>MOT</u>***", Written(scene));
        Assert.IsTrue(scene.Editor.TakeKey(Key.S, ModifierKeys.Control));
        scene.WaitSaved();
        Assert.IsFalse(scene.Editor.TakeKey(Key.Q, ModifierKeys.Control));
    }

    [STATestMethod]
    public void Enter_keeps_new_lines_and_the_indent_and_tab_leaves_the_field()
    {
        using var scene = new Scene("  liste");
        scene.Select(7, 0);
        scene.Source.TextArea.Focus();

        PressKey(scene.Source, Key.Enter);
        Assert.AreEqual("  liste\n  ", Written(scene));
        Assert.AreEqual(10, scene.Source.TextArea.Caret.Offset);

        PressKey(scene.Source, Key.Tab);
        Assert.AreEqual("  liste\n  ", Written(scene), "Tab n'écrit rien : ce n'est pas un piège pour le clavier.");
    }

    [STATestMethod]
    public void A_key_taken_by_the_list_does_not_reach_the_field()
    {
        using var scene = new Scene("[[");
        scene.Vault.App.Sheets.Create("Biologie", "Méiose", null);
        scene.Select(2, 0);
        UiThread.Until(() => scene.Editor.HasProposals);

        PressKey(scene.Source, Key.Escape);

        Assert.IsFalse(scene.Editor.HasProposals);
        Assert.AreEqual("[[", Written(scene));
    }

    [STATestMethod]
    public void A_stray_carriage_return_never_reaches_the_disk()
    {
        using var scene = new Scene(string.Empty);

        scene.Source.Document.Insert(0, "a\r\nb\rc");

        Assert.AreEqual("a\nb\nc", Written(scene));
    }

    [STATestMethod]
    public void A_wrong_word_is_underlined_except_in_code_and_under_the_caret()
    {
        using var scene = new Scene("Une ékole ici.\n\n`ékole` et [lien](ékole.md)\n\n```\nékole\n```\n");
        var layer = scene.Source.SpellingLayer;

        UiThread.Until(() => layer.MistakesOf("Une ékole ici.") is not null);
        Assert.HasCount(1, layer.MistakesOf("Une ékole ici.")!);
        UiThread.Until(() => layer.MistakesOf("`ékole` et [lien](ékole.md)") is not null);
        Assert.IsEmpty(layer.MistakesOf("`ékole` et [lien](ékole.md)")!);
        Assert.IsNull(layer.MistakesOf("ékole"), "Le code n'est pas soumis au correcteur.");

        scene.Select(0, 0);
        var image = new RenderTargetBitmap(1400, 900, 96, 96, PixelFormats.Pbgra32);
        image.Render(scene.Source);
        Assert.AreEqual(1400, image.PixelWidth);
    }

    [STATestMethod]
    public void The_menu_of_a_wrong_word_offers_its_replacements()
    {
        using var scene = new Scene("l'ékole ici");

        var items = scene.Source.MenuItemsAt(3);
        var first = (MenuItem)items[0];
        Assert.AreEqual("école", first.Header);
        Assert.AreEqual("écoles", ((MenuItem)items[1]).Header);
        Assert.AreEqual(EditingText.AddToDictionary, ((MenuItem)items[2]).Header);
        Assert.IsInstanceOfType<Separator>(items[3]);
        Assert.AreEqual(EditingText.Paste, ((MenuItem)items[^1]).Header);

        first.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.AreEqual("l'école ici", Written(scene));
        Assert.HasCount(3, scene.Source.MenuItemsAt(3), "Un mot juste n'a que le presse-papiers.");
    }

    [STATestMethod]
    public void A_word_added_to_the_dictionary_is_no_longer_a_mistake()
    {
        using var scene = new Scene("Une ékole ici.");
        var layer = scene.Source.SpellingLayer;
        UiThread.Until(() => layer.MistakesOf("Une ékole ici.") is { Count: 1 });

        var add = scene.Source.MenuItemsAt(5).OfType<MenuItem>().First(item => (string)item.Header == EditingText.AddToDictionary);
        add.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        UiThread.Until(() => scene.Vault.Spelling.Added.Count == 1);
        Assert.AreEqual("ékole", scene.Vault.Spelling.Added[0]);
        UiThread.Until(() => layer.MistakesOf("Une ékole ici.") is { Count: 0 });
    }

    [STATestMethod]
    public void In_code_and_outside_a_word_the_menu_only_offers_the_clipboard()
    {
        using var scene = new Scene("```\nékole\n```\n\n  ");

        Assert.HasCount(3, scene.Source.MenuItemsAt(5));
        Assert.HasCount(3, scene.Source.MenuItemsAt(Written(scene).Length));
    }

    [STATestMethod]
    public void The_rendering_follows_the_writing()
    {
        using var scene = new Scene();

        scene.Select(Body.Length, 0);
        scene.Type("**Nouvelle** phrase.");
        UiThread.Until(() => scene.Editor.Preview!.Map.Text.Contains("Nouvelle phrase.", StringComparison.Ordinal));

        Assert.IsEmpty(scene.Editor.Preview!.Passages);
    }

    [STATestMethod]
    public void Remaking_the_rendering_keeps_the_place()
    {
        var body = string.Join("\n\n", Enumerable.Range(1, 400).Select(i => $"Paragraphe numéro {i}."));
        using var scene = new Scene(body);
        var scroller = Descendants(scene.View.PreviewText).OfType<ScrollViewer>().First();
        UiThread.Until(() => !scene.View.PreviewText.HasPendingWork);
        scroller.ScrollToVerticalOffset(2000);
        TestShell.Flush();
        var before = scroller.VerticalOffset;
        Assert.IsGreaterThan(0, before);

        scene.Select(0, 0);
        scene.Type("A");
        UiThread.Until(() => scene.Editor.Preview!.Map.Text.StartsWith('A') && !scene.View.PreviewText.HasPendingWork);
        UiThread.Until(() => Math.Abs(scroller.VerticalOffset - before) < 1);
    }

    [STATestMethod]
    public void Without_the_room_the_rendering_takes_the_place_of_the_code()
    {
        using var scene = new Scene(width: 700);

        Assert.IsFalse(scene.Editor.PreviewFits);
        Assert.AreEqual(Visibility.Visible, scene.View.SourcePane.Visibility);
        Assert.AreEqual(Visibility.Collapsed, scene.View.PreviewPane.Visibility);
        Assert.IsFalse(scene.Ribbon.PreviewToggle.IsChecked);

        scene.Editor.IsPreviewOn = true;
        TestShell.Flush();
        Assert.AreEqual(Visibility.Collapsed, scene.View.SourcePane.Visibility);
        Assert.AreEqual(Visibility.Visible, scene.View.PreviewPane.Visibility);

        scene.Editor.IsPreviewOn = false;
        TestShell.Flush();
        Assert.AreEqual(Visibility.Visible, scene.View.SourcePane.Visibility);
    }

    [STATestMethod]
    public void With_the_room_the_rendering_can_be_hidden()
    {
        using var scene = new Scene();
        Assert.IsTrue(scene.Ribbon.PreviewToggle.IsChecked);

        scene.Editor.IsPreviewOn = false;
        TestShell.Flush();

        Assert.AreEqual(Visibility.Visible, scene.View.SourcePane.Visibility);
        Assert.AreEqual(Visibility.Collapsed, scene.View.PreviewPane.Visibility);
    }

    [STATestMethod]
    public void An_editor_comes_back_on_its_text_when_attached_again()
    {
        using var scene = new Scene();
        var other = new MarkdownEditor();

        scene.Editor.Attach(other, () => null);
        other.Document.Insert(0, "A");
        Assert.AreEqual(SaveState.Modified, scene.Editor.State);

        scene.Editor.Attach(scene.Source, () => null);
        Assert.AreEqual(Body, Written(scene));
    }

    [STATestMethod]
    public void A_new_page_opening_on_a_missing_sheet_says_why()
    {
        using var scene = new Scene();
        scene.Page.ToggleWritingCommand.Execute(null);
        UiThread.Until(() => scene.Page.Editor is null);
        Assert.IsTrue(scene.Vault.App.Trash.TrashSheet(scene.Vault.SheetId).Succeeded);

        scene.Page.StartWritingCommand.Execute(null);
        UiThread.Until(() => scene.Page.HasProblem);

        Assert.IsTrue(scene.Page.IsReading);
    }

    private static DocumentLineStyle StyleOf(MarkdownEditor source, int number)
    {
        var view = source.TextArea.TextView;
        var visual = view.GetOrConstructVisualLine(source.Document.GetLineByNumber(number));
        return new DocumentLineStyle(visual);
    }

    private sealed class DocumentLineStyle(VisualLine line)
    {
        public Brush? At(int column) => line.Elements.First(element => element.RelativeTextOffset <= column && column < element.RelativeTextOffset + element.DocumentLength).TextRunProperties.ForegroundBrush;

        public FontWeight WeightAt(int column) => line.Elements.First(element => element.RelativeTextOffset <= column && column < element.RelativeTextOffset + element.DocumentLength).TextRunProperties.Typeface.Weight;
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    [DataRow(ThemeKind.HighContrast)]
    public void The_text_and_its_syntax_take_the_colors_of_the_theme(ThemeKind theme)
    {
        using var scene = new Scene("# Titre\n\nTexte **gras** et <u>mot</u> [lien](a.md)\n\n- liste\n\n```\ncode\n```\n", theme: theme);
        var source = scene.Source;
        var text = (SolidColorBrush)source.Foreground;
        var emphasis = source.EmphasisBrush;
        var syntax = source.SyntaxBrush;

        var heading = StyleOf(source, 1);
        Assert.AreEqual(emphasis, heading.At(2));
        Assert.AreEqual(FontWeights.SemiBold, heading.WeightAt(2));
        var plain = StyleOf(source, 3);
        Assert.AreEqual(text.Color, ((SolidColorBrush)plain.At(0)!).Color, "Le texte suit le thème : jamais noir sur sombre.");
        Assert.AreEqual(syntax, plain.At(6));
        Assert.AreEqual(source.TagBrush, plain.At("Texte **gras** et ".Length));
        Assert.AreEqual(emphasis, plain.At("Texte **gras** et <u>mot</u> [".Length));
        Assert.AreEqual(syntax, plain.At("Texte **gras** et <u>mot</u> [lien](".Length));
        Assert.AreEqual(syntax, StyleOf(source, 5).At(0));
        Assert.AreEqual(syntax, StyleOf(source, 8).At(0));
        Assert.AreEqual(text.Color, ((SolidColorBrush)source.TextArea.Caret.CaretBrush).Color);
        Assert.AreEqual(((SolidColorBrush)scene.Window.TryFindResource("Kyf.Brush.Surface")).Color, ((SolidColorBrush)source.Background).Color);
    }

    private static Color Ground(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            var brush = current switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                Control control => control.Background,
                _ => null,
            };
            if (brush is SolidColorBrush { Color.A: 255 } solid)
            {
                return solid.Color;
            }
        }
        return Colors.White;
    }

    private static double Contrast(Color first, Color second)
    {
        static double Luminance(Color color) => new TokenColor(255, color.R, color.G, color.B).RelativeLuminance;
        var (light, dark) = (Math.Max(Luminance(first), Luminance(second)), Math.Min(Luminance(first), Luminance(second)));
        return (light + 0.05) / (dark + 0.05);
    }

    /// <summary>Le texte visible et actif atteint 4,5:1 sur son fond, calculé élément par élément.</summary>
    private static List<string> ContrastFaults(DependencyObject root)
    {
        var faults = new List<string>();
        foreach (var block in Descendants(root).OfType<TextBlock>().Where(block => block is { IsVisible: true, IsEnabled: true } && block.Text.Length > 0))
        {
            var foreground = ((SolidColorBrush)block.Foreground).Color;
            var ratio = Contrast(foreground, Ground(block));
            if (ratio < 4.5)
            {
                faults.Add($"{block.Text} : {ratio:0.00}");
            }
        }
        return faults;
    }

    /// <summary>En contraste élevé, les couleurs sont celles de Windows, qui garantit leurs paires ; sur ce poste, elles ne sont pas celles d'un vrai thème.</summary>
    private static List<string> Faults(ThemeKind theme, DependencyObject root) => theme == ThemeKind.HighContrast ? [] : ContrastFaults(root);

    /// <summary>Rien ne dépasse du ruban : ses groupes passent à la ligne, jamais de défilement horizontal.</summary>
    private static List<string> OverflowFaults(FrameworkElement ribbon)
    {
        var faults = new List<string>();
        foreach (var element in Descendants(ribbon).OfType<FrameworkElement>().Where(element => element.IsVisible && element is Button or TextBlock or ToggleButton))
        {
            var bounds = element.TransformToAncestor(ribbon).TransformBounds(new Rect(element.RenderSize));
            if (bounds.Left < -0.5 || bounds.Right > ribbon.ActualWidth + 0.5)
            {
                faults.Add($"{element.GetType().Name} {element.Name} : {bounds.Right:0} > {ribbon.ActualWidth:0}");
            }
        }
        return faults;
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light, 1400)]
    [DataRow(ThemeKind.Dark, 1400)]
    [DataRow(ThemeKind.HighContrast, 1400)]
    [DataRow(ThemeKind.Light, 420)]
    [DataRow(ThemeKind.Dark, 420)]
    [DataRow(ThemeKind.HighContrast, 420)]
    [DataRow(ThemeKind.Dark, 800)]
    public void The_ribbon_is_readable_and_stays_in_its_frame(ThemeKind theme, int width)
    {
        using var scene = new Scene(width: width, theme: theme);
        var ribbon = scene.Ribbon;

        foreach (var tab in new[] { ribbon.HomeTab, ribbon.InsertTab })
        {
            tab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            var contrast = Faults(theme, ribbon);
            Assert.IsEmpty(contrast, string.Join(", ", contrast));
            var overflow = OverflowFaults(ribbon);
            Assert.IsEmpty(overflow, string.Join(", ", overflow));
        }
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    [DataRow(ThemeKind.HighContrast)]
    public void The_list_of_sheets_and_the_table_grid_are_readable(ThemeKind theme)
    {
        using var scene = new Scene("[[", theme: theme);
        scene.Vault.App.Sheets.Create("Biologie", "Méiose", null);
        scene.Select(2, 0);
        UiThread.Until(() => scene.Editor.HasProposals);
        TestShell.Flush();

        var list = Faults(theme, scene.View.ProposalsList);
        Assert.IsEmpty(list, string.Join(", ", list));
        Assert.IsNotEmpty(Descendants(scene.View.ProposalsList).OfType<TextBlock>().ToList());

        scene.Ribbon.TablePopup.IsOpen = true;
        TestShell.Flush();
        var grid = Faults(theme, scene.Ribbon.Chooser);
        Assert.IsEmpty(grid, string.Join(", ", grid));
        scene.Ribbon.TablePopup.IsOpen = false;
    }

    [TestMethod]
    public void The_table_size_reads_in_french()
    {
        Assert.AreEqual("4 colonnes × 3 lignes", EditingText.TableSize(3, 4));
        Assert.AreEqual("1 colonne × 1 ligne", EditingText.TableSize(1, 1));
    }

    [TestMethod]
    public void Plain_text_comes_first_among_the_code_languages()
    {
        Assert.AreEqual(string.Empty, CodeLanguages.All[0].Id);
        Assert.AreEqual(CodeLanguages.All.Count, CodeLanguages.All.Select(language => language.Id).Distinct().Count());
    }
}
