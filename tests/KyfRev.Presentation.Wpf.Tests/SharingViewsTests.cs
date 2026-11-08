using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Là où le partage, les modèles et l'import se trouvent : les menus, les boutons, les fenêtres, à toutes les largeurs.</summary>
[TestClass]
public sealed class SharingViewsTests
{
    private static readonly string[] SharingEntries = [SharingText.Share, SharingText.SaveAsTemplate];

    [STATestMethod]
    public void The_right_click_on_a_sheet_offers_to_share_it_and_to_make_a_template_of_it()
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene();
        var window = scene.Show(1100, 800);
        try
        {
            var row = scene.Page.Rows.OfType<SheetRow>().Single();
            var items = scene.View.RowMenuItems(row);
            var headers = items.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.IsSubsetOf(SharingEntries, headers);
            Assert.AreEqual(headers.IndexOf(SheetActionsText.Move) + 1, headers.IndexOf(SharingText.Share), "après « Déplacer », comme dans la 0.x");
            Assert.IsInstanceOfType<MenuItem>(items[0], "les actions rapides restent en tête");

            Click(items, SharingText.Share);
            TestShell.Flush();
            Assert.IsTrue(Named<Dialog>(window, "ShareDialog").IsOpen);
            Assert.AreEqual("Partager « Mitose »", Named<Dialog>(window, "ShareDialog").Title);
            Named<Dialog>(window, "ShareDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !scene.Sharing.IsSharing);

            Click(items, SharingText.SaveAsTemplate);
            TestShell.Flush();
            var template = Named<Dialog>(window, "TemplateDialog");
            Assert.IsTrue(template.IsOpen);
            Assert.AreEqual("Mitose", Named<TextBox>(window, "TemplateNameBox").Text);
            template.Close(DialogChoice.Primary);
            UiThread.Until(() => scene.Page.HasDone);
            Assert.AreEqual(SharingText.TemplateDoneOf("Mitose"), scene.Page.Done);
            Assert.IsFalse(template.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_import_is_in_the_header_the_free_space_and_the_empty_list()
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene();
        var window = scene.Show(1100, 800);
        try
        {
            var button = Named<Button>(window, "ImportButton");
            Assert.IsTrue(button.IsVisible);
            Assert.AreEqual(SharingText.ImportTitle, AutomationProperties.GetName(button));
            Assert.AreEqual(SharingText.ImportAction, button.ToolTip);
            button.Command.Execute(null);
            TestShell.Flush();
            Assert.IsTrue(Named<Dialog>(window, "ImportDialog").IsOpen);
            Named<Dialog>(window, "ImportDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !scene.Import.IsOpen);

            var items = scene.View.RowMenuItems(null);
            Assert.IsInstanceOfType<Separator>(items[2], "après les actions rapides");
            Click(items, SharingText.ImportFolder);
            TestShell.Flush();
            Assert.IsTrue(scene.Import.IsOpen);
            scene.Import.AnswerCommand.Execute(false);

            scene.Page.Show([]);
            TestShell.Flush();
            Assert.IsTrue(Named<Button>(window, "EmptyImportButton").IsVisible, "« Importer des notes existantes »");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_import_dialog_runs_from_the_folder_to_the_report()
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene();
        var source = scene.Vaults.Folder("Notes");
        File.WriteAllText(Path.Combine(source, "Note.md"), "Une note.\n");
        File.WriteAllBytes(Path.Combine(source, "Abimée.md"), [(byte)'a', 0xFF, 0xFE]);
        var window = scene.Show(1100, 800);
        try
        {
            scene.Import.Request();
            TestShell.Flush();
            var dialog = Named<Dialog>(window, "ImportDialog");
            Assert.IsFalse(dialog.IsPrimaryEnabled, "il faut un dossier");
            Assert.IsTrue(Named<RadioButton>(window, "CopyOnlyChoice").IsChecked);
            Assert.IsFalse(Named<RadioButton>(window, "TrackChoice").IsChecked);

            scene.Vaults.Picker.Answers.Enqueue(source);
            Named<Button>(window, "ChooseButton").Command.Execute(null);
            TestShell.Flush();
            Assert.AreEqual("Notes", Named<TextBlock>(window, "SourceName").Text);
            Assert.IsTrue(dialog.IsPrimaryEnabled);
            Named<RadioButton>(window, "TrackChoice").IsChecked = true;
            Assert.IsTrue(scene.Import.Tracks);

            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => scene.Import.HasReport);
            TestShell.Flush();
            Assert.IsTrue(dialog.IsOpen, "le bilan s'y lit");
            Assert.IsTrue(Named<StackPanel>(window, "ReportPanel").IsVisible);
            Assert.IsFalse(Named<StackPanel>(window, "ChoicePanel").IsVisible);
            Assert.AreEqual(string.Empty, dialog.SecondaryButtonText);
            var toggle = Named<ToggleButton>(window, "IgnoredToggle");
            Assert.IsFalse(Named<ItemsControl>(window, "IgnoredList").IsVisible);
            toggle.IsChecked = true;
            TestShell.Flush();
            Assert.IsTrue(Named<ItemsControl>(window, "IgnoredList").IsVisible);

            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => !scene.Import.IsOpen);
            Assert.IsFalse(dialog.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_share_window_reports_the_file_and_warns_on_an_encrypted_vault()
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene(encrypted: true);
        var window = scene.Show(1100, 800);
        try
        {
            scene.Sharing.RequestShare(scene.SheetId, "Mitose");
            TestShell.Flush();
            var dialog = Named<Dialog>(window, "ShareDialog");
            Assert.IsTrue(Named<Notification>(window, "ClearWarning").IsOpen, "un fichier partagé est en clair");
            Assert.AreEqual(SharingText.Cancel, dialog.SecondaryButtonText);
            var formats = Descendants(Named<ItemsControl>(window, "Formats")).OfType<RadioButton>().ToList();
            Assert.HasCount(6, formats);
            Assert.IsTrue(formats[0].IsChecked);
            formats[2].IsChecked = true;
            TestShell.Flush();
            Assert.IsFalse(formats[0].IsChecked, "un seul format à la fois");
            Assert.AreEqual(ShareKind.Markdown, scene.Sharing.Selected!.Kind);

            var outside = scene.Vaults.Folder("Dehors");
            scene.Vaults.Picker.Answers.Enqueue(outside);
            dialog.Close(DialogChoice.Primary);
            UiThread.Until(() => scene.Sharing.HasMessage);
            TestShell.Flush();
            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual(SharingText.Close, dialog.SecondaryButtonText);
            Assert.IsTrue(Named<Notification>(window, "ShareMessage").IsOpen);
            StringAssert.Contains(Named<Notification>(window, "ShareMessage").Message, "Mitose.md", StringComparison.Ordinal);

            dialog.Close(DialogChoice.Secondary);
            UiThread.Until(() => !scene.Sharing.IsSharing);
            Assert.IsFalse(dialog.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    [DataRow(ThemeKind.Light)]
    [DataRow(ThemeKind.Dark)]
    public void At_the_smallest_window_the_dialogs_fit_scroll_only_downward_and_stay_readable(ThemeKind theme)
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene(encrypted: true);
        var source = scene.Vaults.Folder("Notes au nom assez long pour ne jamais tenir sur une seule ligne de la fenêtre étroite");
        var window = scene.Show(470, 540, theme);
        try
        {
            scene.Sharing.RequestShare(scene.SheetId, "Mitose");
            TestShell.Flush();
            AssertFits(window, Named<Dialog>(window, "ShareDialog"), theme);
            Named<Dialog>(window, "ShareDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !scene.Sharing.IsSharing);

            scene.Sharing.RequestSaveAsTemplate(scene.SheetId, "Mitose", _ => { });
            TestShell.Flush();
            AssertFits(window, Named<Dialog>(window, "TemplateDialog"), theme);
            Named<Dialog>(window, "TemplateDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !scene.Sharing.IsNamingTemplate);

            scene.Import.Request();
            scene.Vaults.Picker.Answers.Enqueue(source);
            scene.Import.ChooseCommand.Execute(null);
            TestShell.Flush();
            AssertFits(window, Named<Dialog>(window, "ImportDialog"), theme);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Leaving_the_page_closes_what_it_had_open()
    {
        using var ui = new UiThread();
        using var scene = new SheetsScene();
        var window = scene.Show(1100, 800);
        try
        {
            scene.Sharing.RequestShare(scene.SheetId, "Mitose");
            scene.Import.Request();
            TestShell.Flush();
            var shell = (ShellViewModel)window.DataContext;
            shell.Navigate(shell.MainNavigation[0]);
            TestShell.Flush();
            Assert.IsFalse(scene.Sharing.IsSharing);
            Assert.IsFalse(scene.Import.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void The_dialogs_do_nothing_once_their_view_model_is_gone()
    {
        using var ui = new UiThread();
        var sharing = new SheetSharingDialogs();
        sharing.ShareDialog.Close(DialogChoice.Primary);
        sharing.TemplateDialog.Close(DialogChoice.Primary);
        new FolderImportDialog().ImportDialog.Close(DialogChoice.Primary);
        Assert.IsNull(sharing.DataContext);
    }

    [STATestMethod]
    public void The_reading_view_offers_them_in_its_text_menu_and_its_header()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        using var vault = new ReadingVault("Un paragraphe avec un mot.\n", passage: true);
        var sharing = new SheetSharingViewModel(vault.App.Sheets, vault.App.Templates, vault.App.Vaults, scene.Vaults.Picker, new KyfRev.Presentation.Wpf.Reading.SharedSheetPictures(null), scene.Printer, TimeProvider.System);
        var page = vault.Page(sharing: sharing);
        Assert.IsFalse(page.CanShare, "pas avant que la fiche soit lue");
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var test = new TestShell();
        var window = Show(test, page, 1400, 900);
        try
        {
            Assert.IsTrue(page.CanShare);
            var view = Descendants(window).OfType<SheetPage>().Single();
            var button = Named<Button>(window, "ShareButton");
            Assert.IsTrue(button.IsVisible, "toujours à portée, comme dans la 0.x");
            Assert.AreEqual(SharingText.ShareSheet, AutomationProperties.GetName(button));
            button.Command.Execute(null);
            UiThread.Until(() => sharing.IsSharing);
            TestShell.Flush();
            Assert.IsTrue(Named<Dialog>(window, "ShareDialog").IsOpen);
            Named<Dialog>(window, "ShareDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !sharing.IsSharing);

            var items = view.TextMenuItems(null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.AreEqual(SharingEntries, items.TakeLast(2).ToList(), "à la fin du menu du texte, comme dans la 0.x");
            view.SheetText.Selection.Select(view.SheetText.Document.ContentStart, view.SheetText.Document.ContentEnd);
            CollectionAssert.AreEqual(SharingEntries, view.TextMenuItems(null).OfType<MenuItem>().Select(item => (string)item.Header).TakeLast(2).ToList(), "sélection comprise");
            view.SheetText.Selection.Select(view.SheetText.Document.ContentStart, view.SheetText.Document.ContentStart);

            Click(view.TextMenuItems(null), SharingText.SaveAsTemplate);
            TestShell.Flush();
            Assert.IsTrue(sharing.IsNamingTemplate);
            Assert.AreEqual("Mitose", sharing.TemplateName);
            Named<Dialog>(window, "TemplateDialog").Close(DialogChoice.Primary);
            UiThread.Until(() => page.HasDone);
            TestShell.Flush();
            Assert.IsTrue(Named<Notification>(window, "DoneMessage").IsOpen);
            StringAssert.Contains(page.Done, "Mitose", StringComparison.Ordinal);

            Named<Notification>(window, "DoneMessage").SetCurrentValue(Notification.IsOpenProperty, false);
            sharing.RequestSaveAsTemplate(page.SheetId, "Encore", _ => { });
            sharing.AnswerTemplateCommand.Execute(false);
            page.RequestSaveAsTemplateCommand.Execute(null);
            Named<Dialog>(window, "TemplateDialog").Close(DialogChoice.Primary);
            UiThread.Until(() => !sharing.IsNamingTemplate);
            UiThread.Until(() => Named<Notification>(window, "DoneMessage").IsOpen);

            Click(view.TextMenuItems(null), SharingText.Share);
            UiThread.Until(() => sharing.IsSharing);
            sharing.Dismiss();
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void In_a_narrow_window_sharing_moves_under_the_more_button()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        using var vault = new ReadingVault("Un paragraphe sans titre.\n", passage: false);
        var sharing = new SheetSharingViewModel(vault.App.Sheets, vault.App.Templates, vault.App.Vaults, scene.Vaults.Picker, new KyfRev.Presentation.Wpf.Reading.SharedSheetPictures(null), scene.Printer, TimeProvider.System);
        var page = vault.Page(sharing: sharing);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(new TestShell(), page, 470, 540);
        try
        {
            Assert.IsTrue(page.IsCompactHeader);
            Assert.IsFalse(Named<Button>(window, "ShareButton").IsVisible);
            var more = Named<Button>(window, "HeaderMoreButton");
            Assert.IsTrue(more.IsVisible, "même sans plan ni passage");
            Assert.AreEqual(SharingText.MoreActions, AutomationProperties.GetName(more));
            var back = Named<Button>(window, "BackButton");
            Assert.AreEqual(back.TranslatePoint(new Point(0, back.ActualHeight / 2), window).Y, more.TranslatePoint(new Point(0, more.ActualHeight / 2), window).Y, 6, "sur la ligne du retour");

            more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            TestShell.Flush();
            var items = more.ContextMenu.Items.OfType<MenuItem>().Where(item => item.IsVisible).Select(item => (string)item.Header).ToList();
            CollectionAssert.AreEqual(SharingEntries, items);
            more.ContextMenu.IsOpen = false;

            var ribbon = Descendants(window).OfType<SheetPage>().Single().Ribbon;
            var share = ribbon.ViewMenu.Items.OfType<MenuItem>().Single(item => (Equals(item.Header, SharingText.Share)));
            Assert.AreEqual(Visibility.Visible, share.Visibility, "à l'écriture, il est dans le « ⋯ » du ruban");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void While_writing_what_was_just_typed_leaves_with_the_sheet_and_the_page_is_covered()
    {
        using var ui = new UiThread();
        using var scene = new SharingScene();
        using var vault = new ReadingVault("Un paragraphe.\n", passage: false);
        var sharing = new SheetSharingViewModel(vault.App.Sheets, vault.App.Templates, vault.App.Vaults, scene.Vaults.Picker, new KyfRev.Presentation.Wpf.Reading.SharedSheetPictures(null), scene.Printer, TimeProvider.System);
        var page = vault.Page(markdown: true, sharing: sharing);
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);
        var window = Show(new TestShell(), page, 1400, 900);
        try
        {
            var writing = page.StartWritingCommand.ExecuteAsync(null);
            UiThread.Until(() => writing.IsCompleted && page.IsWriting);
            TestShell.Flush();
            var view = Descendants(window).OfType<SheetPage>().Single();
            view.EditorView.Source.TextArea.Selection.ReplaceSelectionWithText("Écrit à l'instant. ");
            TestShell.Flush();

            var asked = page.RequestShareCommand.ExecuteAsync(null);
            UiThread.Until(() => asked.IsCompleted);
            TestShell.Flush();

            Assert.IsTrue(sharing.IsSharing);
            Assert.IsTrue(view.EditorView.PageCovered, "le texte mis en forme a sa propre fenêtre");
            StringAssert.Contains(vault.App.Sheets.Open(page.SheetId).Value.Body, "Écrit à l'instant.", StringComparison.Ordinal);

            sharing.AnswerShareCommand.Execute(false);
            TestShell.Flush();
            Assert.IsFalse(view.EditorView.PageCovered);
            view.DataContext = null;
            sharing.RequestShare(page.SheetId, "Mitose");
            sharing.Dismiss();
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Without_sharing_the_reading_view_offers_nothing_more()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("Un paragraphe.\n", passage: false);
        var page = vault.Page();
        page.LoadCommand.Execute(null);
        UiThread.Until(() => !page.IsLoading);

        Assert.IsFalse(page.CanShare);
        page.RequestSaveAsTemplateCommand.Execute(null);
        var asked = page.RequestShareCommand.ExecuteAsync(null);
        UiThread.Until(() => asked.IsCompleted);
        Assert.AreEqual(ReadingText.PanelsMenu, page.PanelsMenuName);
        Assert.IsFalse(page.HasDone);
    }

    private static void AssertFits(Window window, Dialog dialog, ThemeKind theme)
    {
        var scroller = (ScrollViewer)dialog.Template.FindName("PART_Content", dialog);
        Assert.AreEqual(0, scroller.ScrollableWidth, 0.5, "aucun défilement horizontal");
        var card = (FrameworkElement)dialog.Template.FindName("Card", dialog);
        var top = card.TranslatePoint(new Point(0, 0), window);
        var content = ((FrameworkElement)window.Content).ActualHeight;
        Assert.IsGreaterThanOrEqualTo(0, top.Y, "la carte reste dans la fenêtre");
        Assert.IsLessThanOrEqualTo(content, top.Y + card.ActualHeight, "la carte reste dans la fenêtre");
        Assert.IsLessThanOrEqualTo(window.ActualWidth, top.X + card.ActualWidth, "la carte reste dans la fenêtre");
        var primary = (Button)dialog.Template.FindName(Dialog.PrimaryButtonPart, dialog);
        var bottom = primary.TranslatePoint(new Point(0, primary.ActualHeight), window).Y;
        Assert.IsLessThanOrEqualTo(content, bottom, "les boutons restent visibles");

        var ground = ThemeColors.For(theme)[ColorRole.Surface];
        foreach (var text in Descendants(dialog).OfType<TextBlock>().Where(block => block.IsVisible && block.Text.Length > 0 && !Inside<Button>(block) && !Inside<Notification>(block)))
        {
            var ink = ((SolidColorBrush)text.Foreground).Color;
            Assert.IsGreaterThanOrEqualTo(4.5, new TokenColor(ink.A, ink.R, ink.G, ink.B).ContrastWith(ground), $"« {text.Text} » en thème {theme}");
        }
    }

    private static bool Inside<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T)
            {
                return true;
            }
        }
        return false;
    }

    private static void Click(IEnumerable<Control> items, string header) =>
        items.OfType<MenuItem>().Single(item => Equals(item.Header, header)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static ShellWindow Show(TestShell test, object page, double width, double height)
    {
        var shell = test.Shell;
        shell.Navigate(new NavigationItem("fiche", "Fiche", Icons.Document, int.MaxValue, page));
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.Manual };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return window;
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

    /// <summary>La vue « Fiches » sur un coffre d'une fiche, avec le partage et l'import.</summary>
    private sealed class SheetsScene : IDisposable
    {
        private readonly FakePrinter _printer = new();

        public SheetsScene(bool encrypted = false)
        {
            Vaults = new TestVaults(AppMode.Development);
            var root = Vaults.PathOf("Coffre");
            if (encrypted)
            {
                Vaults.CreateEncryptedVault(root);
                Assert.AreEqual(root, App.Vaults.Unlock(root, VaultSecret.Password(TestVaults.Password), remember: false).Vault);
            }
            else
            {
                Assert.AreEqual(root, App.Vaults.Create(root).Vault);
            }
            SheetId = App.Sheets.Create(string.Empty, "Mitose", null).Value;
            Sharing = new SheetSharingViewModel(App.Sheets, App.Templates, App.Vaults, Vaults.Picker, new KyfRev.Presentation.Wpf.Reading.SharedSheetPictures(null), _printer, TimeProvider.System);
            Import = new FolderImportViewModel(App.Sheets, Vaults.Picker);
            Page = new SheetsPageViewModel(App.Sheets, App.Folders, App.Templates, new FakeOpener(), TimeProvider.System, App.Reviews, App.Trash, Sharing, Import);
        }

        public TestVaults Vaults { get; }

        public KyfRev.Application.UseCases.KyfRevApplication App => Vaults.App;

        public string SheetId { get; }

        public SheetSharingViewModel Sharing { get; }

        public FolderImportViewModel Import { get; }

        public SheetsPageViewModel Page { get; }

        public SheetsPage View { get; private set; } = null!;

        public ShellWindow Show(double width, double height, ThemeKind theme = ThemeKind.Light)
        {
            var test = new TestShell { Design = { Theme = theme } };
            var window = SharingViewsTests.Show(test, Page, width, height);
            Page.RefreshCommand.Execute(null);
            UiThread.Until(() => Page.Rows.Count > 0);
            TestShell.Flush();
            View = Descendants(window).OfType<SheetsPage>().Single();
            return window;
        }

        public void Dispose() => Vaults.Dispose();
    }
}
