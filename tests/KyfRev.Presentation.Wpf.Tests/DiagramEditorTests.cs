using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'éditeur de hiérarchie : plusieurs niveaux, et ajouter sous un seul élément.</summary>
[TestClass]
public sealed class DiagramEditorTests
{
    private const string Family = "Grand-mère\n  Enfant 1\n  Enfant 2\n  Enfant 3\n    Petit-enfant 1\n    Petit-enfant 2";

    [TestMethod]
    public void A_grandmother_three_children_and_two_grandchildren_under_the_third_only()
    {
        var editor = DiagramEditorViewModel.For(SmartArt.OrganizationChartId, "Grand-mère", null);

        editor.AddChildCommand.Execute(null);
        editor.Selected!.Text = "Enfant 1";
        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "Enfant 2";
        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "Enfant 3";
        editor.AddChildCommand.Execute(null);
        editor.Selected!.Text = "Petit-enfant 1";
        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "Petit-enfant 2";

        Assert.AreEqual(("smartart organigramme", Family), editor.Source());
        Assert.HasCount(6, editor.Preview!.Shapes.OfType<DiagramRectangle>().ToList());
    }

    [TestMethod]
    public void A_child_goes_after_those_already_there_and_a_sibling_after_what_depends_on_it()
    {
        var editor = Opened();

        editor.Select(editor.Nodes[3]);
        editor.AddChildCommand.Execute(null);
        editor.Selected!.Text = "Dernier";
        Assert.AreEqual(6, editor.Nodes.IndexOf(editor.Selected));
        Assert.AreEqual(2, editor.Selected.Level);

        editor.Select(editor.Nodes[3]);
        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "Enfant 4";
        Assert.AreEqual(7, editor.Nodes.IndexOf(editor.Selected));
        Assert.AreEqual(1, editor.Selected.Level);
    }

    [TestMethod]
    public void The_top_stays_alone()
    {
        var editor = Opened();

        Assert.IsFalse(editor.AddSiblingCommand.CanExecute(null));
        Assert.IsFalse(editor.IndentCommand.CanExecute(null));
        Assert.IsFalse(editor.OutdentCommand.CanExecute(null));
        Assert.IsFalse(editor.MoveUpCommand.CanExecute(null));
        Assert.IsFalse(editor.MoveDownCommand.CanExecute(null));
        Assert.IsFalse(editor.DeleteCommand.CanExecute(null), "Trois enfants : il faudrait trois sommets.");
        Assert.IsTrue(editor.AddChildCommand.CanExecute(null));

        Assert.IsTrue(editor.TakeKey(Key.Enter, ModifierKeys.None));
        Assert.AreEqual(1, editor.Selected!.Level, "Entrée sur le sommet ajoute dessous.");
    }

    [TestMethod]
    public void A_level_moves_with_what_depends_on_it()
    {
        var editor = Opened();
        var third = editor.Nodes[3];
        editor.Select(third);
        Assert.IsFalse(editor.OutdentCommand.CanExecute(null), "Il n'y a qu'un sommet.");

        editor.IndentCommand.Execute(null);

        Assert.AreEqual(2, third.Level);
        Assert.AreEqual(3, editor.Nodes[4].Level);
        Assert.AreEqual(3, editor.Nodes[5].Level);
        Assert.AreEqual(40, third.Indent.Left);

        editor.OutdentCommand.Execute(null);

        Assert.AreEqual(Family, editor.Source()!.Value.Code);

        editor.Select(editor.Nodes[1]);
        Assert.IsFalse(editor.IndentCommand.CanExecute(null), "Rien avant lui à ce niveau.");
    }

    [TestMethod]
    public void Moving_exchanges_whole_branches_at_the_same_level()
    {
        var editor = Opened();
        var third = editor.Nodes[3];
        editor.Select(third);
        Assert.IsFalse(editor.MoveDownCommand.CanExecute(null), "Il est le dernier.");

        editor.MoveUpCommand.Execute(null);

        Assert.AreEqual("Grand-mère\n  Enfant 1\n  Enfant 3\n    Petit-enfant 1\n    Petit-enfant 2\n  Enfant 2", editor.Source()!.Value.Code);

        editor.MoveDownCommand.Execute(null);

        Assert.AreEqual(Family, editor.Source()!.Value.Code);
        Assert.AreSame(third, editor.Selected);

        editor.Select(editor.Nodes[4]);
        Assert.IsFalse(editor.MoveUpCommand.CanExecute(null), "Le premier de ses frères ne passe pas au parent d'avant.");
    }

    [TestMethod]
    public void Removing_an_element_keeps_what_depends_on_it()
    {
        var editor = Opened();
        editor.Select(editor.Nodes[3]);

        editor.DeleteCommand.Execute(null);

        Assert.AreEqual("Grand-mère\n  Enfant 1\n  Enfant 2\n  Petit-enfant 1\n  Petit-enfant 2", editor.Source()!.Value.Code);
        Assert.AreEqual("Enfant 2", editor.Selected!.Text);
    }

    [TestMethod]
    public void The_top_goes_only_if_one_element_can_replace_it()
    {
        var editor = DiagramEditorViewModel.For(SmartArt.OrganizationChartId, "a\n  b\n    c", null);

        editor.DeleteCommand.Execute(null);

        Assert.AreEqual("b\n  c", editor.Source()!.Value.Code);

        editor.Select(editor.Nodes[1]);
        editor.DeleteCommand.Execute(null);
        Assert.AreEqual("b", editor.Source()!.Value.Code);
        Assert.IsFalse(editor.DeleteCommand.CanExecute(null), "Le dernier reste.");
    }

    [TestMethod]
    public void The_keyboard_does_it_all_from_the_list()
    {
        var editor = Opened();
        editor.Select(editor.Nodes[2]);

        Assert.IsTrue(editor.TakeKey(Key.Down, ModifierKeys.None));
        Assert.AreEqual("Enfant 3", editor.Selected!.Text);
        Assert.IsTrue(editor.TakeKey(Key.Up, ModifierKeys.None));
        Assert.AreEqual("Enfant 2", editor.Selected!.Text);
        Assert.IsTrue(editor.TakeKey(Key.Up, ModifierKeys.Alt));
        Assert.AreEqual(1, editor.Nodes.IndexOf(editor.Selected));
        Assert.IsTrue(editor.TakeKey(Key.Down, ModifierKeys.Alt));
        Assert.AreEqual(2, editor.Nodes.IndexOf(editor.Selected));
        Assert.IsTrue(editor.TakeKey(Key.Right, ModifierKeys.Alt));
        Assert.AreEqual(2, editor.Selected.Level);
        Assert.IsTrue(editor.TakeKey(Key.Left, ModifierKeys.Alt));
        Assert.AreEqual(1, editor.Selected.Level);
        Assert.IsTrue(editor.TakeKey(Key.Enter, ModifierKeys.Control));
        Assert.AreEqual(2, editor.Selected.Level);
        Assert.IsTrue(editor.TakeKey(Key.Enter, ModifierKeys.None));
        Assert.AreEqual(2, editor.Selected!.Level);
        Assert.IsTrue(editor.TakeKey(Key.Back, ModifierKeys.None), "Un élément vide s'efface.");
        Assert.IsTrue(editor.TakeKey(Key.Delete, ModifierKeys.None));
        Assert.AreEqual("Enfant 2", editor.Selected!.Text);

        Assert.IsFalse(editor.TakeKey(Key.Back, ModifierKeys.None), "Son texte se modifie, il ne s'efface pas.");
        Assert.IsFalse(editor.TakeKey(Key.A, ModifierKeys.None));
        editor.Select(editor.Nodes[0]);
        Assert.IsTrue(editor.TakeKey(Key.Up, ModifierKeys.None), "Tout en haut, la touche ne fait rien de plus.");
        Assert.AreSame(editor.Nodes[0], editor.Selected);
    }

    [TestMethod]
    public void The_drawing_follows_the_list_and_shows_the_chosen_element()
    {
        var editor = Opened();
        var changes = new List<string?>();
        editor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        editor.Nodes[1].Text = "Aîné";

        CollectionAssert.Contains(changes, nameof(DiagramEditorViewModel.Preview));
        StringAssert.Contains(editor.Preview!.Description, "Aîné", StringComparison.Ordinal);
        Assert.AreEqual(0, editor.HighlightedBox);

        editor.Select(editor.Nodes[3]);
        Assert.AreEqual(3, editor.HighlightedBox);

        editor.Nodes[1].Text = "  ";
        Assert.AreEqual(2, editor.HighlightedBox, "Un élément vide n'a pas de boîte.");
        editor.Select(editor.Nodes[1]);
        Assert.IsNull(editor.HighlightedBox);

        DiagramNode? focused = null;
        editor.NodeToFocus += (_, node) => focused = node;
        editor.SelectBox(2);
        Assert.AreEqual("Enfant 3", focused!.Text);
        focused = null;
        editor.SelectBox(40);
        Assert.IsNull(focused);
    }

    [TestMethod]
    public void The_direction_is_a_choice_kept_in_the_file()
    {
        var editor = Opened();
        Assert.IsTrue(editor.IsDown);

        editor.IsAcross = true;

        Assert.IsFalse(editor.IsDown);
        Assert.AreEqual("smartart horizontal", editor.Source()!.Value.Info);
        StringAssert.StartsWith(editor.Preview!.Description, "Hiérarchie horizontale", StringComparison.Ordinal);

        editor.IsDown = true;
        Assert.AreEqual("smartart organigramme", editor.Source()!.Value.Info);
        Assert.IsTrue(DiagramEditorViewModel.For(SmartArt.HorizontalHierarchyId, "a", null).IsAcross);
    }

    [TestMethod]
    public void Only_a_hierarchy_of_the_sheet_opens_here()
    {
        var editor = DiagramEditorViewModel.ForExisting("smartart  organigramme", Family, 12)!;

        Assert.AreEqual(12, editor.Position);
        Assert.AreEqual(EditingText.DiagramEditorEdit, editor.Title);
        Assert.AreEqual(EditingText.DiagramEditorSave, editor.ConfirmLabel);
        Assert.AreEqual(Family, editor.Source()!.Value.Code);
        Assert.IsNull(DiagramEditorViewModel.ForExisting("smartart cycle", "a", 1));
        Assert.IsNull(DiagramEditorViewModel.ForExisting("mermaid", "a", 1));
        Assert.IsNull(DiagramEditorViewModel.ForExisting(null!, null!, 1));

        var empty = DiagramEditorViewModel.For(SmartArt.OrganizationChartId, " ", null);
        Assert.AreEqual(EditingText.DiagramEditorNew, empty.Title);
        Assert.AreEqual(EditingText.GalleryInsert, empty.ConfirmLabel);
        Assert.HasCount(1, empty.Nodes);
        Assert.IsNull(empty.Source(), "Rien d'écrit : rien à insérer.");
    }

    [TestMethod]
    public void Without_a_chosen_element_no_key_is_taken()
    {
        var editor = Opened();
        typeof(DiagramEditorViewModel).GetProperty(nameof(DiagramEditorViewModel.Selected))!.SetValue(editor, null);

        Assert.IsFalse(editor.TakeKey(Key.Enter, ModifierKeys.None));
        Assert.IsNull(editor.HighlightedBox);
        Assert.IsFalse(editor.AddChildCommand.CanExecute(null));
        Assert.IsFalse(editor.AddSiblingCommand.CanExecute(null));
        Assert.IsFalse(editor.IndentCommand.CanExecute(null));
        Assert.IsFalse(editor.OutdentCommand.CanExecute(null));
        Assert.IsFalse(editor.MoveUpCommand.CanExecute(null));
        Assert.IsFalse(editor.MoveDownCommand.CanExecute(null));
        Assert.IsFalse(editor.DeleteCommand.CanExecute(null));
    }

    [TestMethod]
    public void The_gallery_passes_a_hierarchy_on_to_the_next_step()
    {
        var gallery = DiagramGalleryViewModel.For(GalleryKind.SmartArt);
        gallery.SelectedItem = gallery.Items.Single(item => item.Id == "cycle");
        Assert.IsFalse(gallery.IsHierarchy);
        Assert.IsTrue(gallery.ShowsItems);
        Assert.AreEqual(EditingText.GalleryInsert, gallery.ConfirmLabel);

        gallery.SelectedItem = gallery.Items.Single(item => item.Id == SmartArt.OrganizationChartId);

        Assert.IsTrue(gallery.IsHierarchy);
        Assert.IsFalse(gallery.ShowsItems);
        Assert.AreEqual(EditingText.GalleryContinue, gallery.ConfirmLabel);
        Assert.AreEqual(EditingText.GalleryNextStep, gallery.InputHelp);

        Assert.IsFalse(DiagramGalleryViewModel.For(GalleryKind.Schema).IsHierarchy);
    }

    [STATestMethod]
    public void From_the_gallery_to_the_sheet_through_the_editor()
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var editor = scene.Page.Editor!;
        editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        var gallery = editor.Gallery!;
        gallery.SelectedItem = gallery.Items.Single(item => item.Id == SmartArt.OrganizationChartId);
        TestShell.Flush();
        Assert.AreEqual(EditingText.GalleryContinue, scene.Sheet.GalleryDialog.PrimaryButtonText);

        scene.Sheet.GalleryDialog.Close(DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();

        Assert.IsFalse(editor.IsGalleryOpen);
        Assert.IsTrue(editor.IsDiagramEditorOpen);
        Assert.IsTrue(scene.Sheet.DiagramEditorDialog.IsOpen);
        Assert.AreEqual(EditingText.DiagramEditorNew, scene.Sheet.DiagramEditorDialog.Title);
        Assert.AreEqual("Cellule", editor.DiagramEditor!.Nodes[0].Text, "L'exemple du modèle sert de départ.");
        editor.DiagramEditor.Select(editor.DiagramEditor.Nodes[3]);
        editor.DiagramEditor.AddChildCommand.Execute(null);
        editor.DiagramEditor.Selected!.Text = "Lipides";

        scene.Sheet.DiagramEditorDialog.Close(DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();

        Assert.IsFalse(editor.IsDiagramEditorOpen);
        UiThread.Until(() => scene.Sheet.EditorView.Source.Text.Contains("```smartart organigramme\nCellule\n  Noyau\n  Cytoplasme\n  Membrane\n    Lipides\n```", StringComparison.Ordinal));

        editor.OpenGalleryCommand.Execute(GalleryKind.SmartArt);
        editor.Gallery!.SelectedItem = editor.Gallery.Items.Single(item => item.Id == SmartArt.HorizontalHierarchyId);
        scene.Sheet.GalleryDialog.Close(DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();
        var before = scene.Sheet.EditorView.Source.Text;

        scene.Sheet.DiagramEditorDialog.Close(DesignSystem.Controls.DialogChoice.Secondary);
        TestShell.Flush();

        Assert.IsFalse(editor.IsDiagramEditorOpen);
        Assert.AreEqual(before, scene.Sheet.EditorView.Source.Text, "Annuler n'écrit rien.");
    }

    [STATestMethod]
    public void The_view_shows_the_list_the_drawing_and_answers_clicks_and_keys()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var editor = Opened();
        var view = new DiagramEditorView { DataContext = editor };
        var host = new Border { Width = 980, Height = 560, Child = view };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1000, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            var fields = Fields(view);
            Assert.HasCount(6, fields);
            Assert.AreEqual("Enfant 3", fields[3].Text);
            Assert.IsGreaterThan(Left(fields[0], view), Left(fields[4], view), "Chaque niveau se décale.");

            // Écrire dans un champ change le dessin.
            fields[1].Text = "Aîné";
            TestShell.Flush();
            StringAssert.Contains(view.PreviewView.Scene!.Description, "Aîné", StringComparison.Ordinal);

            // Prendre le focus choisit l'élément.
            fields[3].RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, fields[3]) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            Assert.AreEqual("Enfant 3", editor.Selected!.Text);
            Assert.AreEqual(3, view.PreviewView.HighlightedBox);

            // Une touche va à la liste ; les autres restent au champ.
            var enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fields[3])!, 0, Key.Enter) { RoutedEvent = UIElement.PreviewKeyDownEvent };
            fields[3].RaiseEvent(enter);
            Assert.IsTrue(enter.Handled);
            Assert.HasCount(7, editor.Nodes);
            var letter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(fields[3])!, 0, Key.A) { RoutedEvent = UIElement.PreviewKeyDownEvent };
            fields[3].RaiseEvent(letter);
            Assert.IsFalse(letter.Handled);
            TestShell.Flush();

            // Un clic sur une boîte choisit son élément.
            var scene = view.PreviewView.Scene!;
            var box = scene.Shapes.OfType<DiagramRectangle>().First();
            Assert.AreEqual(0, view.PreviewView.BoxAt(new Point(box.X + 2, box.Y + 2)));
            Assert.IsNull(view.PreviewView.BoxAt(new Point(0, 0)));
            view.PreviewView.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });

            // Le clic droit porte les mêmes actions.
            var menu = view.NodeMenuItems(fields[0]);
            var labels = menu.OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.Contains(labels, EditingText.DiagramChild);
            CollectionAssert.Contains(labels, EditingText.DiagramDelete);
            CollectionAssert.Contains(labels, EditingText.Paste);
            editor.Select(editor.Nodes[0]);
            var top = view.NodeMenuItems(null).OfType<MenuItem>().ToList();
            Assert.IsFalse(top.Single(item => (string)item.Header == EditingText.DiagramDelete).IsEnabled);
            Assert.IsFalse(top.Exists(item => (string)item.Header == EditingText.Paste));
            top.Single(item => (string)item.Header == EditingText.DiagramChild).Command.Execute(null);
            var count = editor.Nodes.Count;
            editor.Select(editor.Nodes[1]);
            view.NodeMenuItems(null).OfType<MenuItem>().Single(item => (string)item.Header == EditingText.DiagramDelete).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual(count - 1, editor.Nodes.Count);

            // La direction se choisit au-dessus du dessin.
            view.AcrossButton.IsChecked = true;
            Assert.IsTrue(editor.IsAcross);
            TestShell.Flush();
            Assert.IsFalse(view.DownButton.IsChecked);

            view.DataContext = null;
            Assert.IsEmpty(view.NodeMenuItems(null));
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_hidden_document_is_built_only_once_shown()
    {
        using var ui = new UiThread();
        using var vault = new ReadingVault("# Titre\n\nUn texte.\n", passage: false);
        var viewer = new SheetDocumentViewer { Visibility = Visibility.Collapsed };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.IsTrue(viewer.IsLoaded);

            viewer.Reading = vault.Reading();

            Assert.IsNull(viewer.Document);

            viewer.Visibility = Visibility.Visible;
            TestShell.Flush();

            Assert.IsNotNull(viewer.Document);
        }
        finally
        {
            window.Close();
        }
    }

    private static DiagramEditorViewModel Opened() => DiagramEditorViewModel.For(SmartArt.OrganizationChartId, Family, null);

    private static double Left(FrameworkElement element, Visual ancestor) => element.TransformToAncestor(ancestor).Transform(default).X;

    private static List<TextBox> Fields(DependencyObject root)
    {
        var found = new List<TextBox>();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox field)
            {
                found.Add(field);
            }
            else
            {
                found.AddRange(Fields(child));
            }
        }
        return found;
    }

    /// <summary>Une fiche en écriture dans le code : l'éditeur écrit dans un texte qu'on peut lire.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly ReadingVault _vault = new("Avant\n", passage: false);
        private readonly Window _window;

        public Scene()
        {
            var test = new TestShell();
            Page = _vault.Page();
            Page.LoadCommand.Execute(null);
            UiThread.Until(() => !Page.IsLoading);
            Sheet = new SheetPage { DataContext = Page, Width = 1400, Height = 900 };
            _window = new Window { Content = new Canvas { Children = { Sheet } }, Left = -10000, Top = -10000, Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            _window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
            _window.Show();
            TestShell.Flush();
            Page.ToggleWritingCommand.Execute(null);
            UiThread.Until(() => Page.Editor is not null);
            TestShell.Flush();
        }

        public SheetPageViewModel Page { get; }

        public SheetPage Sheet { get; }

        public void Dispose()
        {
            _window.Close();
            _vault.Dispose();
        }
    }
}
