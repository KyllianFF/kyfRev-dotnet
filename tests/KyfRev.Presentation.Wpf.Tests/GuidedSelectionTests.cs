using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KyfRev.Presentation.Wpf.Extensions;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La sélection guidée qu'une extension demande dans une fiche, et une fiche rouverte à un passage.</summary>
[TestClass]
public sealed class GuidedSelectionTests
{
    private const string Body = "# Mitose\n\nLa mitose est une division cellulaire.\n\nElle donne deux cellules filles identiques.\n";

    private static readonly SelectionAsk Question = new("Quiz", "Sélectionnez la question", null, AllowsTyping: true);

    [TestMethod]
    public void The_bar_gives_what_the_user_chooses()
    {
        var bar = new GuidedSelectionBar();
        Assert.IsFalse(bar.IsActive);
        Assert.AreEqual(string.Empty, bar.Prompt);

        var first = bar.StartAsync(Question);
        Assert.IsTrue(bar.IsActive);
        Assert.AreEqual("Quiz : Sélectionnez la question", bar.Prompt);
        Assert.AreEqual("Quiz", bar.Extension);
        Assert.IsTrue(bar.AllowsTyping);
        bar.NeedsSelection();
        Assert.IsTrue(bar.HasHint);
        bar.Take("f_1", "Mitose", 3, 9, "mitose");
        Assert.AreEqual(new GuidedSelection(GuidedSelection.Taken, "mitose", "f_1", "Mitose", 3, 9), first.Result);
        Assert.IsFalse(bar.IsActive);
        Assert.IsFalse(bar.HasHint);

        var typed = bar.StartAsync(Question with { AllowsTyping = false });
        Assert.IsFalse(bar.AllowsTyping);
        Assert.IsFalse(bar.TakeTypedCommand.CanExecute(null));
        bar.Typed = "  Une division  ";
        bar.TakeTypedCommand.Execute(null);
        Assert.AreEqual(new GuidedSelection(GuidedSelection.Typed, "Une division"), typed.Result);

        var skipped = bar.StartAsync(Question);
        bar.SkipCommand.Execute(null);
        Assert.AreEqual(GuidedSelection.Skipped, skipped.Result.Choice);

        var stopped = bar.StartAsync(Question);
        bar.StopSeriesCommand.Execute(null);
        Assert.AreEqual(GuidedSelection.Stopped, stopped.Result.Choice);

        var replaced = bar.StartAsync(Question);
        var next = bar.StartAsync(Question);
        Assert.AreEqual(GuidedSelection.Stopped, replaced.Result.Choice, "Une nouvelle demande termine la précédente.");
        bar.Abandon();
        Assert.AreEqual(GuidedSelection.Stopped, next.Result.Choice);
        Assert.ThrowsExactly<ArgumentNullException>(() => bar.StartAsync(null!));
    }

    [STATestMethod]
    public void An_extension_asks_for_a_text_in_a_sheet_and_gets_it()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var screen = new ExtensionScreen(scene.Test.Shell, scene.Navigation);

        var refused = screen.AskSelectionAsync(Question);
        Assert.AreEqual(ReadingText.GuidedNoSheet, refused.Result.Refusal, "Sans fiche ouverte ni désignée, rien à sélectionner.");

        var asking = screen.AskSelectionAsync(Question with { SheetId = scene.Vault.SheetId });
        var page = (SheetPageViewModel)scene.Test.Shell.Current.Page;
        var view = scene.SheetView();
        UiThread.Until(() => { TestShell.Flush(); return page.Reading is not null && view.SheetText.Document is not null; });
        TestShell.Flush();
        Assert.IsTrue(page.Guided.IsActive);
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)view.FindName("GuidedBar")).Visibility);

        var guidedTake = (Button)view.FindName("GuidedTake");
        guidedTake.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.IsTrue(page.Guided.HasHint, "Rien de sélectionné : la barre le dit.");
        var start = page.Reading!.Map.Text.IndexOf("division", StringComparison.Ordinal);
        Assert.IsTrue(view.SheetText.Select(start, start + "division cellulaire".Length));
        Assert.IsTrue(view.TextMenuItems(null).OfType<MenuItem>().Any(item => (string)item.Header == ReadingText.GuidedTakeFor("Quiz")));
        guidedTake.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        UiThread.Until(() => asking.IsCompleted);
        Assert.AreEqual(new GuidedSelection(GuidedSelection.Taken, "division cellulaire", scene.Vault.SheetId, "Mitose", start, start + 19), asking.Result.Answer);

        var typing = screen.AskSelectionAsync(Question);
        Assert.IsTrue(page.Guided.IsActive, "La fiche ouverte sert sans qu'on la désigne.");
        page.Guided.Typed = "Combien de cellules ?";
        page.Guided.TakeTypedCommand.Execute(null);
        UiThread.Until(() => typing.IsCompleted);
        Assert.AreEqual(new GuidedSelection(GuidedSelection.Typed, "Combien de cellules ?"), typing.Result.Answer);

        var left = screen.AskSelectionAsync(Question);
        scene.Test.Shell.Navigate(scene.Test.Shell.MainNavigation[0]);
        UiThread.Until(() => left.IsCompleted);
        Assert.AreEqual(GuidedSelection.Stopped, left.Result.Answer!.Choice, "Quitter la fiche termine la sélection guidée.");
    }

    [STATestMethod]
    public void A_sheet_opens_at_the_passage_an_extension_gives()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var screen = new ExtensionScreen(scene.Test.Shell, scene.Navigation);
        Assert.AreEqual(ReadingText.GuidedNoSheet, screen.OpenSheet(" ", null, null));

        Assert.IsNull(screen.OpenSheet(scene.Vault.SheetId, 0, 0), "Un passage vide ouvre la fiche sans rien sélectionner.");
        var plain = (SheetPageViewModel)scene.Test.Shell.Current.Page;
        Assert.IsNull(plain.RangeToShow);

        Assert.IsNull(screen.OpenSheet(scene.Vault.SheetId, 10, 20));
        var page = (SheetPageViewModel)scene.Test.Shell.Current.Page;
        var view = scene.SheetView();
        UiThread.Until(() => { TestShell.Flush(); return page.Reading is not null && page.RangeToShow is null; });
        TestShell.Flush();
        var selected = view.SheetText.CurrentSelection();
        Assert.IsNotNull(selected);
        Assert.AreEqual(page.Reading!.Map.Text[10..20].Trim(), page.Reading.Map.Text[selected.Value.Start..selected.Value.End]);
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionScreen(null!, scene.Navigation));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ExtensionScreen(scene.Test.Shell, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => screen.AskSelectionAsync(null!).GetAwaiter().GetResult());
    }

    [STATestMethod]
    public void A_position_of_the_text_becomes_a_selection_and_back()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault(Body, passage: false);
        var viewer = new SheetDocumentViewer();
        Assert.IsFalse(viewer.Select(0, 3), "Sans fiche, rien à sélectionner.");
        viewer.Reading = SheetReading.Of(vault.App.Sheets.Open(vault.SheetId).Value, _ => KyfRev.Application.Outcome.Refused<byte[]>("non"));
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var text = viewer.Reading.Map.Text;
            var start = text.IndexOf("deux cellules", StringComparison.Ordinal);
            Assert.IsTrue(viewer.Select(start, start + 13));
            Assert.AreEqual((start, start + 13), (viewer.CurrentSelection()!.Value.Start, viewer.CurrentSelection()!.Value.End));
            Assert.IsFalse(viewer.Select(text.Length + 5, text.Length + 9), "Hors du texte affiché, rien.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Une fiche d'essai, la coquille et sa navigation, dans une vraie fenêtre.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly ShellWindow _window;

        public Scene()
        {
            Vault = new ReadingVault(Body, passage: false);
            Test = new TestShell();
            Test.Shell.ShowWorkspace();
            Navigation = new ReviewNavigation(Vault.App.Sheets, Vault.App.Annotations, Vault.App.Reviews, Vault.App.Reminders, Vault.App.Vaults, new MemorySessions(), TimeProvider.System, new RecordingLinks(), Vault.App.Editing, FakeEnvironment.Markdown());
            Navigation.Attach(Test.Shell);
            _window = new ShellWindow(Test.Shell) { ShowActivated = false, Left = -10000, Top = -10000, Width = 1300, Height = 800, WindowStartupLocation = WindowStartupLocation.Manual };
            _window.Resources.MergedDictionaries.Add(Test.Design.Dictionary);
            _window.Show();
            TestShell.Flush();
        }

        public ReadingVault Vault { get; }

        public TestShell Test { get; }

        public ReviewNavigation Navigation { get; }

        public SheetPage SheetView()
        {
            TestShell.Flush();
            return Descendants<SheetPage>(_window).Single();
        }

        public void Dispose()
        {
            _window.Close();
            Vault.Dispose();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }
                foreach (var inner in Descendants<T>(child))
                {
                    yield return inner;
                }
            }
        }
    }
}
