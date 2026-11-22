using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.DesignSystem;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>
/// La fenêtre des actions communes, posée dans la fenêtre principale, et le clic droit d'un aperçu (rangées 23A et 23B) :
/// ce que l'on y fait arrive aux actions, et le menu de l'aperçu met le texte choisi devant la fiche.
/// </summary>
[TestClass]
public sealed class SheetActionsViewTests
{
    private static readonly string[] Bio = ["bio"];
    private static readonly string[] SheetEntries = ["Lire", "Archiver"];

    [STATestMethod]
    public void The_actions_window_takes_a_suggested_tag_answers_and_its_message_goes_away()
    {
        using var ui = new UiThread();
        var time = new FakeTime(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var vaults = new TestVaults(AppMode.Development, time);
        var app = vaults.App;
        app.Vaults.Create(vaults.PathOf("Coffre"));
        var id = app.Sheets.Create("Biologie", "Mitose", null).Value;
        var other = app.Sheets.Create("Biologie", "Méiose", null).Value;
        app.Sheets.SetTags(other, ["bio"]).Value.ToString();
        var actions = new SheetActionsViewModel(app.Sheets, app.Folders, app.Reviews, app.Reminders, app.Trash, new FakeOpener(), time);
        var test = new TestShell();
        var view = new SheetActionsView { DataContext = actions };
        var window = new Window { Content = view, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            // Les étiquettes : le champ paraît, une étiquette proposée se prend d'un clic, la saisie reprend à sa suite.
            actions.Tags(new SheetTarget(id, "Mitose"));
            UiThread.Until(() => actions.IsActing && actions.HasTagSuggestions);
            TestShell.Flush();
            var input = Named<TextBox>(view, "ActionInputBox");
            Assert.IsTrue(input.IsVisible);
            var suggestion = Descendants(Named<ItemsControl>(view, "TagSuggestions")).OfType<Button>().Single(button => Equals(button.DataContext, "bio"));
            suggestion.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.AreEqual("bio, ", actions.ActionInput);
            Assert.AreEqual(input.Text.Length, input.CaretIndex, "la saisie reprend après l'étiquette prise");

            // Le bouton principal répond aux actions ; ce qui a abouti se dit, puis s'en va d'un clic.
            Named<Dialog>(view, "ActionDialog").Close(DialogChoice.Primary);
            UiThread.Until(() => actions.Done == SheetActionsText.TagsSaved);
            CollectionAssert.AreEqual(Bio, app.Sheets.List().Value.Single(sheet => sheet.Id == id).Tags.ToArray());
            TestShell.Flush();
            Named<Notification>(view, "DoneMessage").Close();
            Assert.IsFalse(actions.HasDone);

            // Renoncer ne change rien.
            actions.Rename(new SheetTarget(id, "Mitose"));
            UiThread.Until(() => actions.IsActing);
            TestShell.Flush();
            Named<Dialog>(view, "ActionDialog").Close(DialogChoice.Secondary);
            UiThread.Until(() => !actions.IsActing);
            Assert.AreEqual("Mitose", app.Sheets.List().Value.Single(sheet => sheet.Id == id).Title);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_preview_menu_puts_the_chosen_text_before_the_sheet_and_shows_only_the_sheet_otherwise()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var run = new Run("La mitose sépare les chromosomes.");
        var viewer = new SheetDocumentViewer { Document = new FlowDocument(new Paragraph(run)), ContextMenu = new ContextMenu() };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 600, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            static IReadOnlyList<Control> Sheet() => [new MenuItem { Header = "Lire" }, new MenuItem { Header = "Archiver" }];

            // Rien de choisi : le menu de la fiche, seul.
            PreviewMenus.Open(Opening(run), Sheet);
            CollectionAssert.AreEqual(SheetEntries, Headers(viewer.ContextMenu.Items));

            // Un texte choisi : Copier, Tout sélectionner, puis « Fiche › » avec le menu de la fiche.
            viewer.Selection.Select(run.ContentStart, run.ContentEnd);
            var items = PreviewMenus.Items(viewer, Sheet());
            Assert.HasCount(4, items);
            Assert.IsInstanceOfType<Separator>(items[2]);
            var submenu = (MenuItem)items[3];
            Assert.AreEqual(ReadingText.SheetSubmenu, submenu.Header);
            CollectionAssert.AreEqual(SheetEntries, Headers(submenu.Items));
            Assert.AreEqual(DesignText.SelectAll, ((MenuItem)items[1]).Header);

            // Hors d'un aperçu, rien ne s'ouvre.
            var outside = new ContextMenu();
            var button = new Button { ContextMenu = outside };
            PreviewMenus.Open(Opening(button), Sheet);
            Assert.IsEmpty(outside.Items);
        }
        finally
        {
            window.Close();
        }
    }

    private static string[] Headers(ItemCollection items) => [.. items.OfType<MenuItem>().Select(item => (string)item.Header)];

    /// <summary>L'ouverture d'un menu sur cet élément, celle que WPF lui enverrait.</summary>
    private static ContextMenuEventArgs Opening(object source)
    {
        var opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null, [source, true], null)!;
        opening.RoutedEvent = ContextMenuService.ContextMenuOpeningEvent;
        return opening;
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
}
