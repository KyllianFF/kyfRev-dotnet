using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Rendering.Diagrams;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'éditeur des dessins : plusieurs niveaux et ajouter sous un seul élément, une simple liste, un tableau de valeurs.</summary>
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
    public void A_block_of_the_sheet_opens_here_unless_it_was_written_by_hand()
    {
        var editor = DiagramEditorViewModel.ForExisting("smartart  organigramme", Family, 12)!;

        Assert.AreEqual(12, editor.Position);
        Assert.AreEqual(EditingText.DiagramEditorEditOf(editor.SelectedModel.Name), editor.Title);
        Assert.AreEqual(EditingText.DiagramEditorSave, editor.ConfirmLabel);
        Assert.AreEqual(Family, editor.Source()!.Value.Code);
        Assert.AreEqual("cycle", DiagramEditorViewModel.ForExisting("smartart cycle", "a", 1)!.SelectedModel.Id);
        Assert.IsNull(DiagramEditorViewModel.ForExisting("smartart inconnu", "a", 1));
        Assert.IsNull(DiagramEditorViewModel.ForExisting("mermaid", "flowchart LR\n    A --> B", 1));
        Assert.IsNull(DiagramEditorViewModel.ForExisting(null!, null!, 1));

        var marked = MermaidTemplates.Source("etapes", "a\nb")!.Value;
        Assert.AreEqual("etapes", DiagramEditorViewModel.ForExisting(marked.Info, marked.Code, 1)!.SelectedModel.Id);
        Assert.IsNull(DiagramEditorViewModel.ForExisting(marked.Info, marked.Code + "\n    n1 --> n0", 1), "Retouché : il reste dans son code.");

        var empty = DiagramEditorViewModel.For(SmartArt.OrganizationChartId, " ", null);
        Assert.AreEqual(EditingText.DiagramEditorNewOf(empty.SelectedModel.Name), empty.Title);
        Assert.AreEqual(EditingText.GalleryInsert, empty.ConfirmLabel);
        Assert.HasCount(1, empty.Nodes);
        Assert.IsNull(empty.Source(), "Rien d'écrit : rien à insérer.");
    }

    [TestMethod]
    public void A_flat_model_is_a_simple_list()
    {
        var editor = DiagramEditorViewModel.For("cycle", "Un\nDeux\nTrois", null);

        Assert.AreEqual(GalleryKind.SmartArt, editor.Kind);
        Assert.IsFalse(editor.ShowsLevels);
        Assert.IsFalse(editor.ShowsDirection);
        Assert.IsFalse(editor.ShowsValues);
        Assert.AreEqual(EditingText.DiagramAddItem, editor.AddLabel);
        Assert.AreEqual(EditingText.DiagramListKeys, editor.KeysHelp);
        Assert.IsFalse(editor.AddChildCommand.CanExecute(null));
        Assert.IsFalse(editor.IndentCommand.CanExecute(null));
        Assert.IsFalse(editor.OutdentCommand.CanExecute(null));
        Assert.IsTrue(editor.AddSiblingCommand.CanExecute(null));
        Assert.IsTrue(editor.DeleteCommand.CanExecute(null), "Une liste n'a pas de sommet à garder.");
        Assert.IsNull(editor.HighlightedBox);
        Assert.IsNull(editor.MermaidCode);
        Assert.IsNotNull(editor.Preview);

        Assert.IsTrue(editor.TakeKey(Key.Enter, ModifierKeys.None));
        editor.Selected!.Text = "Après un";
        Assert.AreEqual(("smartart cycle", "Un\nAprès un\nDeux\nTrois"), editor.Source());

        DiagramNode? focused = null;
        editor.NodeToFocus += (_, node) => focused = node;
        editor.SelectBox(0);
        Assert.IsNull(focused, "Un dessin à plat ne donne pas une boîte par élément.");

        Assert.IsFalse(editor.TakeKey(Key.Enter, ModifierKeys.Control) && editor.Nodes.Any(node => node.Level > 0), "Pas d'enfant dans une liste.");
    }

    [TestMethod]
    public void A_schema_writes_its_model_in_the_block_and_shows_only_its_code()
    {
        var editor = DiagramEditorViewModel.For("etapes", "Crise\nBastille", null);

        Assert.AreEqual(GalleryKind.Schema, editor.Kind);
        Assert.IsTrue(editor.IsMermaid);
        Assert.IsFalse(editor.IsDrawn);
        Assert.IsNull(editor.Preview);
        Assert.AreEqual(MermaidTemplates.Code("etapes", "Crise\nBastille"), editor.MermaidCode);
        Assert.AreEqual(MermaidTemplates.Source("etapes", "Crise\nBastille"), editor.Source());
        Assert.IsNull(editor.LimitWarning);
        Assert.IsFalse(editor.HasValueWarning);

        var changes = new List<string?>();
        editor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        editor.ShowsCode = true;
        CollectionAssert.Contains(changes, nameof(DiagramEditorViewModel.MermaidCode));
        changes.Clear();
        editor.Nodes[1].Text = "Valmy";
        CollectionAssert.Contains(changes, nameof(DiagramEditorViewModel.MermaidCode));
        StringAssert.Contains(editor.MermaidCode, "Valmy", StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_mind_map_has_levels_and_opens_again_with_them()
    {
        var editor = DiagramEditorViewModel.For("carte", "Centre\n  Branche\n    Détail\n  Autre", null);

        Assert.IsTrue(editor.ShowsLevels);
        Assert.IsFalse(editor.ShowsDirection);
        Assert.AreEqual(EditingText.DiagramAddSibling, editor.AddLabel);
        Assert.AreEqual(EditingText.DiagramKeysNoClick, editor.KeysHelp);
        Assert.IsNull(editor.HighlightedBox, "Le dessin est celui de Mermaid.");
        editor.Select(editor.Nodes[2]);
        editor.AddChildCommand.Execute(null);
        editor.Selected!.Text = "Encore";

        var source = editor.Source()!.Value;
        StringAssert.Contains(source.Code, "\n%%     Détail\n%%       Encore\n", StringComparison.Ordinal);

        var again = DiagramEditorViewModel.ForExisting(source.Info, source.Code, 4)!;
        Assert.AreEqual("0 1 2 3 1", string.Join(' ', again.Nodes.Select(node => node.Level)));
        Assert.AreEqual("Encore", again.Nodes[3].Text);
    }

    [TestMethod]
    public void A_chart_is_a_table_of_values()
    {
        var editor = DiagramEditorViewModel.For("secteurs", "Un : 1,5\nDeux : 3", null);

        Assert.AreEqual(GalleryKind.Chart, editor.Kind);
        Assert.IsTrue(editor.ShowsValues);
        Assert.IsFalse(editor.ShowsSecondValue);
        Assert.AreEqual(EditingText.DiagramAddRow, editor.AddLabel);
        Assert.AreEqual("Un", editor.Nodes[0].Text);
        Assert.AreEqual("1,5", editor.Nodes[0].Value);
        Assert.IsFalse(editor.HasValueWarning);

        editor.Nodes[1].Value = "beaucoup";
        Assert.IsTrue(editor.HasValueWarning, "Elle comptera pour 1 : autant le dire.");
        StringAssert.Contains(editor.MermaidCode, "\"Deux\" : 1", StringComparison.Ordinal);

        editor.Nodes[1].Value = "4";
        editor.Nodes[1].Text = "Deux: et demi";
        Assert.IsFalse(editor.HasValueWarning);
        var source = editor.Source()!.Value;
        StringAssert.Contains(source.Code, "%% Deux  et demi : 4\n", StringComparison.Ordinal);
        var again = DiagramEditorViewModel.ForExisting(source.Info, source.Code, 1)!;
        Assert.AreEqual("4", again.Nodes[1].Value);

        var bare = DiagramEditorViewModel.For("secteurs", "Sans valeur", null);
        Assert.AreEqual(string.Empty, bare.Nodes[0].Value);
        Assert.IsTrue(bare.HasValueWarning);
    }

    [TestMethod]
    public void A_chart_with_two_values_keeps_the_second_one()
    {
        var editor = DiagramEditorViewModel.For("mixte", "Jan : 21 : 18", null);

        Assert.IsTrue(editor.ShowsSecondValue);
        Assert.AreEqual("18", editor.Nodes[0].SecondValue);
        Assert.AreEqual("Jan : 21 : 18", MermaidTemplates.ReadBack(editor.Source()!.Value.Code)!.Value.Text);

        editor.Nodes[0].SecondValue = "aucune";
        Assert.IsTrue(editor.HasValueWarning);

        editor.SelectedModel = editor.Models.Single(model => model.Id == "colonnes");
        Assert.IsFalse(editor.ShowsSecondValue);
        Assert.IsFalse(editor.HasValueWarning, "La seconde valeur ne se dessine plus.");
        Assert.AreEqual("Jan : 21", MermaidTemplates.ReadBack(editor.Source()!.Value.Code)!.Value.Text);
    }

    [TestMethod]
    public void Changing_the_model_adapts_the_levels_without_losing_anything()
    {
        var editor = DiagramEditorViewModel.For("cycle", "Un\nDeux\nTrois", null);

        editor.SelectedModel = editor.Models.Single(model => model.Id == SmartArt.OrganizationChartId);

        Assert.AreEqual(("smartart organigramme", "Un\n  Deux\n  Trois"), editor.Source());
        Assert.IsTrue(editor.ShowsDirection);
        Assert.AreEqual(EditingText.DiagramKeys, editor.KeysHelp);
        Assert.IsNotNull(editor.HighlightedBox);

        editor.SelectedModel = editor.Models.Single(model => model.Id == "cycle");

        Assert.AreEqual(("smartart cycle", "Un\nDeux\nTrois"), editor.Source());

        var hierarchy = Opened();
        hierarchy.IsAcross = true;
        Assert.AreEqual(Family, hierarchy.Source()!.Value.Code, "D'une hiérarchie à l'autre, les niveaux restent.");

        var alone = DiagramEditorViewModel.For("cycle", "Seul", null);
        alone.SelectedModel = alone.Models.Single(model => model.Id == SmartArt.OrganizationChartId);
        Assert.AreEqual("Seul", alone.Source()!.Value.Code);
    }

    [TestMethod]
    public void Too_many_elements_are_said_and_kept()
    {
        var maximum = SmartArt.Layouts.Single(layout => layout.Id == "cycle").Maximum;
        var editor = DiagramEditorViewModel.For("cycle", string.Join('\n', Enumerable.Range(0, maximum).Select(i => $"Élément {i}")), null);
        Assert.IsFalse(editor.HasLimitWarning);
        Assert.IsNull(editor.LimitWarning);

        editor.AddSiblingCommand.Execute(null);
        editor.Selected!.Text = "De trop";

        Assert.IsTrue(editor.HasLimitWarning);
        Assert.AreEqual(EditingText.DiagramLimitOf(maximum), editor.LimitWarning);
        StringAssert.Contains(editor.Source()!.Value.Code, "De trop", StringComparison.Ordinal);
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

    [STATestMethod]
    [DataRow(GalleryKind.SmartArt, "cycle")]
    [DataRow(GalleryKind.Schema, "carte")]
    [DataRow(GalleryKind.Chart, "secteurs")]
    public void Every_model_of_the_gallery_goes_on_to_the_editor(GalleryKind kind, string model)
    {
        using var ui = new UiThread();
        using var scene = new Scene();
        var editor = scene.Page.Editor!;
        editor.OpenGalleryCommand.Execute(kind);
        editor.Gallery!.SelectedItem = editor.Gallery.Items.Single(item => item.Id == model);
        TestShell.Flush();
        Assert.AreEqual(EditingText.GalleryContinue, scene.Sheet.GalleryDialog.PrimaryButtonText);

        scene.Sheet.GalleryDialog.Close(DesignSystem.Controls.DialogChoice.Primary);
        TestShell.Flush();

        Assert.IsFalse(editor.IsGalleryOpen);
        Assert.IsTrue(editor.IsDiagramEditorOpen);
        Assert.AreEqual(model, editor.DiagramEditor!.SelectedModel.Id);
        Assert.AreEqual(kind, editor.DiagramEditor.Kind);
        Assert.HasCount(DiagramGalleryViewModel.For(kind).Items.Count, editor.DiagramEditor.Models);
        Assert.IsNotNull(editor.DiagramEditor.Source(), "L'exemple du modèle sert de départ.");
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
        Assert.AreEqual(EditingText.DiagramEditorNewOf(editor.DiagramEditor!.SelectedModel.Name), scene.Sheet.DiagramEditorDialog.Title);
        Assert.AreEqual("Cellule", editor.DiagramEditor.Nodes[0].Text, "L'exemple du modèle sert de départ.");
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
    public void The_view_shows_only_what_the_model_needs()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var view = new DiagramEditorView();
        var host = new Border { Width = 980, Height = 560, Child = view };
        host.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        var canvas = new Canvas();
        canvas.Children.Add(host);
        var window = new Window { Content = canvas, Left = -10000, Top = -10000, Width = 1000, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            // Une simple liste : ni niveaux ni direction, et le dessin ne choisit rien.
            var list = DiagramEditorViewModel.For("cycle", "Un\nDeux", null);
            view.DataContext = list;
            TestShell.Flush();
            Assert.HasCount(2, Fields(view));
            Assert.IsFalse(view.AddChildButton.IsVisible);
            Assert.IsFalse(view.IndentButton.IsVisible);
            Assert.IsFalse(view.DownButton.IsVisible);
            Assert.IsTrue(view.PreviewView.IsVisible);
            Assert.IsFalse(view.MermaidPreview.IsVisible);
            view.PreviewView.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            Assert.AreEqual("Un", list.Selected!.Text);
            var menu = view.NodeMenuItems(null).OfType<MenuItem>().Select(item => (string)item.Header).ToList();
            CollectionAssert.Contains(menu, EditingText.DiagramAddItem);
            CollectionAssert.DoesNotContain(menu, EditingText.DiagramChild);
            CollectionAssert.DoesNotContain(menu, EditingText.DiagramIndent);

            // Changer de modèle ici change ce qui se voit.
            view.ModelChoice.SelectedItem = list.Models.Single(model => model.Id == SmartArt.OrganizationChartId);
            TestShell.Flush();
            Assert.IsTrue(view.AddChildButton.IsVisible);
            Assert.IsTrue(view.DownButton.IsVisible);

            // Un graphique : une valeur par ligne, et le code sous l'aperçu.
            var chart = DiagramEditorViewModel.For("secteurs", "Un : 1\nDeux : 2", null);
            view.DataContext = chart;
            TestShell.Flush();
            Assert.HasCount(4, Fields(view));
            Assert.IsTrue(view.MermaidPreview.IsVisible);
            Assert.IsFalse(view.PreviewView.IsVisible);
            Assert.IsTrue(view.CodeToggle.IsVisible);
            Assert.IsFalse(view.CodeBox.IsVisible);
            Assert.IsFalse(view.ValueWarningMessage.IsOpen);
            chart.Nodes[0].Value = "?";
            TestShell.Flush();
            Assert.IsTrue(view.ValueWarningMessage.IsOpen);

            // Deux valeurs : une colonne de plus.
            view.DataContext = DiagramEditorViewModel.For("mixte", "Un : 1 : 2", null);
            TestShell.Flush();
            Assert.HasCount(3, Fields(view));

            // Une limite dépassée se dit.
            var maximum = SmartArt.Layouts.Single(layout => layout.Id == "cycle").Maximum;
            view.DataContext = DiagramEditorViewModel.For("cycle", string.Join('\n', Enumerable.Range(0, maximum + 1).Select(i => $"Élément {i}")), null);
            TestShell.Flush();
            Assert.IsTrue(view.LimitWarningMessage.IsOpen);
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
                if (field.IsVisible)
                {
                    found.Add(field);
                }
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
