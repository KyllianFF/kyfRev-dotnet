using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Editing;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'apparence d'un tableau en lecture et en révision, et l'onglet Tableau du ruban.</summary>
[TestClass]
public sealed class TableLookTests
{
    private const string Table = "| Notion | Définition | Auteur |\n|---|---|---|\n| Autorité | Obéir sans contrainte | Arendt |\n| Pouvoir | Agir de concert | Arendt |\n| Domination | Commander | Weber |\n| Synthèse | Trois légitimités | Weber |\n";

    private static readonly IReadOnlyDictionary<ColorRole, TokenColor> Light = ThemeColors.For(ThemeKind.Light);

    private static readonly string[] HiddenCells = ["Obéir sans contrainte", "Agir de concert", "Commander", "Trois légitimités"];

    private static readonly string[] SentChanges = ["theme=foret", "total", "traits=contour", "aligner=center", "reviser", "trier", "action=rowAfter"];

    [STATestMethod]
    public void A_table_takes_its_headers_bands_lines_and_theme_from_the_line_above_it()
    {
        var (window, table) = Show("<!-- kyfrev tableau entete=ligne,colonne total bandes=lignes traits=horizontaux theme=foret -->\n" + Table);
        try
        {
            var rows = table.RowGroups.SelectMany(group => group.Rows).ToList();
            Assert.AreEqual(Color(ColorRole.TableForestHeader), Background(rows[0].Cells[1]), "la ligne d'en-tête");
            Assert.AreEqual(Color(ColorRole.TableForestHeaderText), ((SolidColorBrush)rows[0].Cells[1].Foreground).Color);
            Assert.AreEqual(Color(ColorRole.TableForestHeader), Background(rows[2].Cells[0]), "la colonne d'en-tête");
            Assert.IsNull(rows[1].Cells[1].Background, "la première ligne après l'en-tête n'est pas une bande");
            Assert.AreEqual(Color(ColorRole.TableForestBand), Background(rows[2].Cells[1]));
            Assert.IsNull(rows[4].Cells[1].Background, "la dernière ligne qui ressort n'est pas une bande");
            Assert.AreEqual(FontWeights.SemiBold, rows[4].Cells[1].FontWeight);
            Assert.AreEqual(new Thickness(0, 2, 0, 1), rows[4].Cells[1].BorderThickness, "un trait épais au-dessus de la dernière ligne");
            Assert.AreEqual(Color(ColorRole.TableForestHeader), ((SolidColorBrush)rows[4].Cells[1].BorderBrush).Color);
            Assert.AreEqual(new Thickness(0, 1, 0, 1), rows[0].Cells[1].BorderThickness, "traits horizontaux : le haut du tableau, puis sous chaque ligne");
            Assert.AreEqual(3, table.Columns.Count, "trois colonnes sur toute la largeur du texte, le défaut");
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void Without_a_line_a_table_keeps_its_look_and_a_full_width_one_has_no_empty_column()
    {
        var (window, table) = Show(Table);
        var (other, full) = Show("<!-- kyfrev tableau entete=aucune bandes=colonnes traits=aucun largeur=page -->\n" + Table);
        try
        {
            var rows = table.RowGroups.SelectMany(group => group.Rows).ToList();
            Assert.AreEqual(Color(ColorRole.SurfaceAlt), Background(rows[0].Cells[0]));
            Assert.AreEqual(FontWeights.SemiBold, rows[0].Cells[0].FontWeight);
            Assert.AreEqual(new Thickness(1), rows[0].Cells[0].BorderThickness, "tous les traits : la première case trace aussi le haut et la gauche");
            Assert.AreEqual(new Thickness(0, 0, 1, 1), rows[1].Cells[1].BorderThickness);
            Assert.IsNull(rows[2].Cells[1].Background, "aucune bande par défaut");

            var cells = full.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).ToList();
            Assert.HasCount(3, full.Columns);
            Assert.IsTrue(cells.All(cell => cell.BorderThickness == default(Thickness)));
            Assert.AreEqual(FontWeights.Normal, cells[0].FontWeight, "sans en-tête, la première ligne se lit comme les autres");
            Assert.AreEqual(Color(ColorRole.TableSoberBand), Background(cells[1]), "une colonne sur deux");
            Assert.IsNull(cells[0].Background);
        }
        finally
        {
            window.Close();
            other.Close();
        }
    }

    [STATestMethod]
    public void The_outline_and_header_lines_trace_only_what_they_say()
    {
        var (window, outline) = Show("<!-- kyfrev tableau traits=contour largeur=ajustee -->\n" + Table);
        var (other, header) = Show("<!-- kyfrev tableau entete=ligne,colonne traits=entete -->\n" + Table);
        var (third, vertical) = Show("<!-- kyfrev tableau traits=verticaux -->\n" + Table);
        try
        {
            var rows = outline.RowGroups.SelectMany(group => group.Rows).ToList();
            Assert.AreEqual(new Thickness(1, 1, 0, 0), rows[0].Cells[0].BorderThickness);
            Assert.AreEqual(new Thickness(0, 0, 1, 1), rows[^1].Cells[2].BorderThickness);
            Assert.AreEqual(default(Thickness), rows[^1].Cells[^1].BorderThickness, "la case de la colonne élastique n'a aucun trait");
            Assert.IsNull(rows[0].Cells[^1].Background);
            Assert.AreEqual(default(Thickness), rows[2].Cells[1].BorderThickness);

            var lines = header.RowGroups.SelectMany(group => group.Rows).ToList();
            Assert.AreEqual(new Thickness(0, 0, 2, 2), lines[0].Cells[0].BorderThickness);
            Assert.AreEqual(new Thickness(0, 0, 0, 2), lines[0].Cells[1].BorderThickness);
            Assert.AreEqual(new Thickness(0, 0, 2, 0), lines[1].Cells[0].BorderThickness);
            Assert.AreEqual(Color(ColorRole.TextSecondary), ((SolidColorBrush)lines[0].Cells[1].BorderBrush).Color, "sobre : le trait de l'en-tête se voit");

            var columns = vertical.RowGroups.SelectMany(group => group.Rows).ToList();
            Assert.AreEqual(new Thickness(1, 0, 1, 0), columns[1].Cells[0].BorderThickness);
            Assert.AreEqual(new Thickness(0, 0, 1, 0), columns[1].Cells[1].BorderThickness);
        }
        finally
        {
            window.Close();
            other.Close();
            third.Close();
        }
    }

    [STATestMethod]
    public void A_column_hidden_in_review_gives_its_cells_as_holes_but_not_its_header()
    {
        var reading = SheetReading.Of(Sheet("<!-- kyfrev tableau reviser-cache=2 -->\n" + Table), _ => Outcome.Refused<byte[]>("non"));
        var builder = new DocumentBuilder(reading, _ => { }, opensLinks: false, showsHeader: false);
        builder.Build();
        builder.Complete();

        var hidden = builder.ReviewCells.Select(cell => reading.Map.Text[cell.Start..cell.End]).ToArray();

        CollectionAssert.AreEqual(HiddenCells, hidden);
    }

    [TestMethod]
    public void A_hidden_cell_is_a_hole_unless_a_passage_covers_it_and_its_bold_is_not_one_more()
    {
        var holes = TextHoles.Compute([new Highlight("p", 0, 10, false, false)], [(22, 26), (40, 45)], [(5, 8), (20, 30)]);

        Assert.AreEqual("passage:p case:1 gras:1", string.Join(' ', holes.Select(hole => hole.Key)));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextHoles.Compute([], [], null!));
    }

    [STATestMethod]
    public void The_table_tab_shows_only_in_a_table_and_sends_each_choice()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var model = new RibbonModel();
        var ribbon = new EditorRibbon { DataContext = model };
        var window = new Window { Content = ribbon, Left = -10000, Top = -10000, Width = 1400, Height = 400, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, ribbon.TableTab.Visibility);

            model.Tools = Tools(new TableState("ligne", false, "aucune", "tous", "sobre", null, "ajustee", 2, null, true));
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, ribbon.TableTab.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility, "il apparaît sans s'ouvrir");
            ribbon.TableTab.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(Visibility.Visible, ribbon.TablePanel.Visibility);
            Assert.IsTrue(ribbon.HeaderRowBox.IsChecked);
            Assert.IsTrue(ribbon.BandsNoneChoice.IsChecked);
            Assert.IsTrue(ribbon.ThemeSoberChoice.IsChecked);
            Assert.IsTrue(ribbon.WidthFitChoice.IsChecked);
            Assert.IsFalse(ribbon.HiddenInReviewBox.IsChecked);

            foreach (var control in new ButtonBase[] { ribbon.ThemeForestChoice, ribbon.TotalRowBox, ribbon.LinesOutlineChoice, ribbon.ColumnCenterChoice, ribbon.HiddenInReviewBox, ribbon.SortButton, ribbon.RowAfterButton })
            {
                control.Command.Execute(control.CommandParameter);
            }
            CollectionAssert.AreEqual(SentChanges, model.Changes);

            model.Tools = Tools(new TableState("ligne,colonne", true, "lignes", "contour", "foret", 2, "page", 2, "center", false));
            TestShell.Flush();
            Assert.IsTrue(ribbon.HeaderColumnBox.IsChecked);
            Assert.IsTrue(ribbon.TotalRowBox.IsChecked);
            Assert.IsTrue(ribbon.BandsRowsChoice.IsChecked);
            Assert.IsTrue(ribbon.ThemeForestChoice.IsChecked);
            Assert.IsTrue(ribbon.ColumnCenterChoice.IsChecked);
            Assert.IsTrue(ribbon.HiddenInReviewBox.IsChecked);
            Assert.IsTrue(ribbon.WidthPageChoice.IsChecked);
            Assert.IsFalse(ribbon.SortButton.IsEnabled, "une case fusionnée empêche le tri");

            model.Tools = null;
            TestShell.Flush();
            Assert.AreEqual(Visibility.Collapsed, ribbon.TableTab.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility, "hors du tableau, l'Accueil revient");
            Assert.AreEqual(Visibility.Collapsed, ribbon.TablePanel.Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void A_narrow_ribbon_keeps_one_short_bar_and_opens_the_rest_on_demand()
    {
        using var ui = new UiThread();
        var test = new TestShell();
        var model = new RibbonModel();
        var ribbon = new EditorRibbon { DataContext = model };
        var window = new Window { Content = ribbon, Left = -10000, Top = -10000, Width = 520, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            Assert.IsTrue(ribbon.IsCompact);
            Assert.AreEqual(Visibility.Visible, ribbon.CompactBar.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.TabRow.Visibility, "les onglets attendent le volet");
            Assert.AreEqual(Visibility.Collapsed, ribbon.Panels.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactTableButton.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.FormatLabel.Visibility, "la barre courte n'a que des icônes : le nom se lit au survol");
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactTableLabel.Visibility, "le bouton « Tableau » se réduit à son icône");
            Assert.AreEqual(EditingText.FormattingHelp, (string)ribbon.FormatToggle.ToolTip);
            Assert.AreEqual(EditorRibbon.CompactIconSize, ((KyfRev.DesignSystem.Controls.Icon)ribbon.CompactBoldButton.Content).FontSize, "en grand");
            Assert.AreEqual(EditorRibbon.CompactButtonSide, ribbon.CompactBoldButton.MinHeight);
            Assert.HasCount(5, ribbon.CompactStyleMenu.Items);
            Assert.HasCount(3, ribbon.ViewMenu.Items, "historique, Markdown, aperçu ; la page y ajoute le plan et les passages");
            Assert.IsLessThanOrEqualTo(window.ActualWidth, ribbon.ViewButton.TranslatePoint(new Point(ribbon.ViewButton.ActualWidth, 0), window).X, "rien ne passe sous un autre bouton");

            ribbon.FormatToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.IsTrue(ribbon.IsPanelOpen);
            Assert.AreEqual(Visibility.Visible, ribbon.TabRow.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.ViewButtons.Visibility, "l'affichage reste sous « ⋯ »");
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);
            Assert.IsTrue(ribbon.FormatToggle.IsChecked);

            // Dans un tableau : le bouton « Tableau » apparaît, sans ouvrir le volet sur lui.
            model.Tools = Tools(new TableState("ligne", false, "aucune", "tous", "sobre", null, "ajustee", 2, null, true));
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, ribbon.CompactTableButton.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);
            ribbon.CompactTableButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(Visibility.Visible, ribbon.TablePanel.Visibility);
            Assert.IsTrue(ribbon.CompactTableButton.IsChecked);
            Assert.IsFalse(ribbon.FormatToggle.IsChecked);
            ribbon.UpdateLayout();
            Assert.AreEqual(EditorRibbon.CompactPanelHeight, ribbon.Panels.ActualHeight, 0.5, "le volet du tableau défile au lieu de cacher le texte");
            Assert.AreEqual(0, ribbon.Panels.ScrollableWidth, "jamais de défilement horizontal");

            // « Mise en forme » depuis le tableau revient à l'Accueil ; une seconde fois, le volet se replie.
            ribbon.FormatToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);
            ribbon.FormatToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.IsFalse(ribbon.IsPanelOpen);
            Assert.AreEqual(Visibility.Collapsed, ribbon.Panels.Visibility);

            // Volet replié sur le tableau, puis le curseur en sort : l'Accueil reprend sa place à la réouverture.
            ribbon.CompactTableButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            ribbon.CompactTableButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.IsFalse(ribbon.IsPanelOpen);
            Assert.AreEqual(Visibility.Visible, ribbon.TablePanel.Visibility, "replié, le volet garde l'onglet du tableau");
            model.Tools = null;
            TestShell.Flush();
            ribbon.FormatToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.AreEqual(Visibility.Visible, ribbon.HomePanel.Visibility);

            // Ce qui cède la place d'abord, c'est ce qui est aussi dans le volet ; « Mise en forme » garde son libellé le plus longtemps.
            ribbon.Fit(EditorRibbon.NarrowWidth - 1);
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactRedoButton.Visibility);
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactLinkButton.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.CompactBulletsButton.Visibility);
            ribbon.Fit(EditorRibbon.BulletsWidth - 1);
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactBulletsButton.Visibility);
            ribbon.Fit(EditorRibbon.LabelWidth - 1);
            Assert.AreEqual(Visibility.Collapsed, ribbon.FormatLabel.Visibility);
            ribbon.Fit(EditorRibbon.TightWidth);
            Assert.AreEqual(Visibility.Collapsed, ribbon.FormatLabel.Visibility, "à toute largeur de la barre courte");
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactTableLabel.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.CompactLinkButton.Visibility);

            // Assez de place : le ruban entier revient, ses onglets et l'affichage à droite.
            window.Width = 1100;
            window.UpdateLayout();
            TestShell.Flush();
            Assert.IsFalse(ribbon.IsCompact);
            Assert.AreEqual(Visibility.Collapsed, ribbon.CompactBar.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.TabRow.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.ViewButtons.Visibility);
            Assert.AreEqual(Visibility.Visible, ribbon.Panels.Visibility);
            Assert.AreEqual(double.PositiveInfinity, ribbon.Panels.MaxHeight, "le ruban entier ne défile pas");
        }
        finally
        {
            window.Close();
        }
    }

    private static EditorTools Tools(TableState table) => new(false, false, false, false, false, false, 0, null, false, false, Table: table);

    private static System.Windows.Media.Color Color(ColorRole role) => Light[role].ToColor();

    private static System.Windows.Media.Color Background(TableCell cell) => ((SolidColorBrush)cell.Background).Color;

    private static (Window Window, Table Table) Show(string body)
    {
        var test = new TestShell();
        var viewer = new SheetDocumentViewer { Reading = SheetReading.Of(Sheet(body), _ => Outcome.Refused<byte[]>("non")) };
        var window = new Window { Content = viewer, Left = -10000, Top = -10000, Width = 860, Height = 700, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        return (window, Descendants(viewer.Document).OfType<Table>().Single());
    }

    private static OpenedSheet Sheet(string body) => new(
        "f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Ce que le ruban lit d'un éditeur : ses outils, et l'onglet Tableau qui envoie ses choix.</summary>
    private sealed class RibbonModel : System.ComponentModel.INotifyPropertyChanged
    {
        public RibbonModel()
        {
            ChangeTableCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<string>(change => Changes.Add(change!));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public List<string> Changes { get; } = [];

        public System.Windows.Input.ICommand ChangeTableCommand { get; }

        public EditorTools? Tools
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(Tools)));
                PropertyChanged?.Invoke(this, new(nameof(IsInTable)));
            }
        }

        public bool IsInTable => Tools?.Table is not null;
    }
}
