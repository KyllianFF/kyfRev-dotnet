using System.Windows;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Rendering;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La pose de masques sur une image : ouverte sur ceux qu'elle a, tracée, enregistrée dans la fiche.</summary>
[TestClass]
public sealed class ImageMaskTests
{
    private const string Body = "Une cellule.\n\n![Cellule](cellule.png)\n\n![Déjà](noyau.png \"kyfrev-masques:10,10,20,20\")\n\n![Légendée](autre.png \"Ma légende\")\n";

    [TestMethod]
    public async Task Masks_are_drawn_then_written_in_the_sheet()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        var saved = 0;
        editor.Saved += (_, _) => saved++;

        await editor.OpenAsync(vault.SheetId, 0, null);

        Assert.IsTrue(editor.IsOpen);
        Assert.IsTrue(editor.CanDraw);
        Assert.IsFalse(editor.HasError);
        Assert.AreEqual("Aucune zone", editor.Summary);
        Assert.IsFalse(editor.UndoLastCommand.CanExecute(null));

        Assert.IsTrue(editor.Add(new ImageMask(0.6, 0.5, -0.4, -0.2)), "tracée de droite à gauche, elle est remise à l'endroit");
        Assert.IsFalse(editor.Add(new ImageMask(0.5, 0.5, 0.001, 0.3)), "trop fine pour être vue");
        Assert.IsTrue(editor.Add(new ImageMask(0.7, 0.7, 0.2, 0.2)));
        Assert.IsTrue(editor.Add(new ImageMask(0, 0, 0.1, 0.1)));
        Assert.AreEqual("3 zones", editor.Summary);
        editor.UndoLastCommand.Execute(null);
        editor.RemoveAt(7);
        Assert.AreEqual("2 zones", editor.Summary);

        await editor.AnswerCommand.ExecuteAsync(true);

        Assert.IsFalse(editor.IsOpen);
        Assert.AreEqual(1, saved);
        Assert.AreEqual("2 zones masquées sur cette image, en révision.", editor.LastSaved);
        StringAssert.Contains(vault.Body(), "![Cellule](cellule.png \"kyfrev-masques:20,30,40,20;70,70,20,20\")", StringComparison.Ordinal);
        StringAssert.Contains(vault.Body(), "![Déjà](noyau.png \"kyfrev-masques:10,10,20,20\")", StringComparison.Ordinal, "les autres images ne bougent pas");
    }

    [TestMethod]
    public async Task An_image_opens_with_its_masks_and_loses_its_title_when_they_are_all_removed()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();

        await editor.OpenAsync(vault.SheetId, 1, null);
        Assert.AreEqual("1 zone", editor.Summary);
        Assert.AreEqual(new ImageMask(0.1, 0.1, 0.2, 0.2), editor.Masks.Single());

        editor.ClearCommand.Execute(null);
        await editor.AnswerCommand.ExecuteAsync(true);

        Assert.AreEqual("Masques retirés : l'image se voit entière en révision.", editor.LastSaved);
        StringAssert.Contains(vault.Body(), "![Déjà](noyau.png)\n", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Closing_writes_nothing_and_an_image_that_cannot_carry_masks_says_so()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();

        await editor.OpenAsync(vault.SheetId, 0, null);
        editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5));
        await editor.AnswerCommand.ExecuteAsync(false);
        Assert.AreEqual(Body, vault.Body(), "fermer n'écrit rien");

        // Une image qui a déjà un titre à elle : on ne le remplace pas.
        await editor.OpenAsync(vault.SheetId, 2, null);
        Assert.IsTrue(editor.IsOpen);
        Assert.IsFalse(editor.CanDraw);
        Assert.AreEqual(ImageMaskText.CannotWrite, editor.Error);
        Assert.IsFalse(editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5)));
        await editor.AnswerCommand.ExecuteAsync(true);
        Assert.AreEqual(Body, vault.Body());

        await editor.OpenAsync(vault.SheetId, 9, null);
        Assert.AreEqual(ImageMaskText.Gone, editor.Error);
        await editor.AnswerCommand.ExecuteAsync(false);
    }

    [TestMethod]
    public async Task A_sheet_changed_meanwhile_is_not_overwritten()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        await editor.OpenAsync(vault.SheetId, 0, null);
        editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5));

        vault.Write(Body + "\nUne ligne écrite ailleurs.\n");
        await editor.AnswerCommand.ExecuteAsync(true);

        Assert.IsTrue(editor.IsOpen, "le refus laisse la fenêtre ouverte");
        Assert.AreEqual(ImageMaskText.Conflict, editor.Error);
        Assert.IsFalse(editor.CanDraw);
        StringAssert.EndsWith(vault.Body(), "Une ligne écrite ailleurs.\n", StringComparison.Ordinal);
        Assert.IsFalse(vault.Body().Contains("kyfrev-masques:10,10,50,50", StringComparison.Ordinal));
    }

    [STATestMethod]
    public void Dragging_adds_a_zone_and_a_click_on_a_zone_removes_it()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        // Avant toute fenêtre : la suite de la lecture ne dépend d'aucun fil d'interface.
        editor.OpenAsync(vault.SheetId, 0, null).GetAwaiter().GetResult();
        var test = new TestShell();
        var dialog = new ImageMaskDialog { DataContext = editor };
        var window = new Window { Content = dialog, Left = -10000, Top = -10000, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var surface = (System.Windows.Controls.Canvas)dialog.FindName("Surface");
            surface.Width = 200;
            surface.Height = 100;
            TestShell.Flush();

            dialog.Finish(new Point(20, 10), new Point(120, 60));
            Assert.AreEqual(new ImageMask(0.1, 0.1, 0.5, 0.5), editor.Masks.Single());
            Assert.AreEqual(1, surface.Children.Count, "la zone gardée est dessinée");

            dialog.Finish(new Point(150, 80), new Point(151, 81));
            Assert.HasCount(1, editor.Masks, "un clic dans le vide ne fait rien");

            dialog.Finish(new Point(60, 30), new Point(61, 30));
            Assert.IsEmpty(editor.Masks, "un clic sur une zone la retire");
            Assert.AreEqual(0, surface.Children.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void While_writing_the_masks_go_back_as_a_title_and_nothing_is_saved_here()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        var picture = new System.Windows.Media.Imaging.WriteableBitmap(4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var titles = new List<string?>();
        var saved = 0;
        editor.Saved += (_, _) => saved++;

        editor.OpenFor(picture, "kyfrev-masques:10,10,20,20", titles.Add);
        Assert.IsTrue(editor.IsOpen);
        Assert.IsTrue(editor.CanDraw);
        Assert.AreEqual(new ImageMask(0.1, 0.1, 0.2, 0.2), editor.Masks.Single());
        Assert.IsTrue(editor.Add(new ImageMask(0.5, 0.5, 0.2, 0.1)));
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.IsFalse(editor.IsOpen);
        Assert.AreEqual("kyfrev-masques:10,10,20,20;50,50,20,10", titles.Single());
        Assert.AreEqual(1, saved);
        Assert.AreEqual(Body, vault.Body(), "la fiche du disque n'est pas touchée : le texte en cours s'enregistre de son côté");

        // Tout retirer rend une image sans titre ; fermer ne rend rien.
        editor.OpenFor(picture, titles[0], titles.Add);
        editor.ClearCommand.Execute(null);
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.IsNull(titles[1]);
        editor.OpenFor(picture, null, titles.Add);
        editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5));
        editor.AnswerCommand.ExecuteAsync(false).GetAwaiter().GetResult();
        Assert.HasCount(2, titles);

        // Une légende n'est pas remplacée, et une image illisible ne se trace pas.
        editor.OpenFor(picture, "Ma légende", titles.Add);
        Assert.IsFalse(editor.CanDraw);
        Assert.AreEqual(ImageMaskText.CannotWrite, editor.Error);
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        editor.OpenFor(null, null, titles.Add);
        Assert.IsFalse(editor.CanDraw);
        Assert.AreEqual(ImageMaskText.Unreadable, editor.Error);
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.HasCount(2, titles);

        // Rouverte en lecture, la fenêtre enregistre de nouveau la fiche elle-même.
        editor.OpenAsync(vault.SheetId, 0, null).GetAwaiter().GetResult();
        editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5));
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.HasCount(2, titles);
        StringAssert.Contains(vault.Body(), "kyfrev-masques:10,10,50,50", StringComparison.Ordinal);
    }

    [STATestMethod]
    public void Cropping_keeps_one_frame_and_the_other_settings_but_drops_masks_that_would_move()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        var picture = new System.Windows.Media.Imaging.WriteableBitmap(4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var titles = new List<string?>();

        editor.OpenFor(picture, "kyfrev-largeur:60 kyfrev-masques:10,10,20,20", titles.Add, crop: true);
        Assert.IsTrue(editor.IsCrop);
        Assert.AreEqual(ImageMaskText.CropTitle, editor.DialogTitle);
        Assert.AreEqual(ImageMaskText.CropHelp, editor.HelpText);
        Assert.IsEmpty(editor.Masks, "le cadre, pas les masques : l'image n'en a pas encore");
        Assert.IsTrue(editor.Add(new ImageMask(0.1, 0.1, 0.5, 0.5)));
        Assert.IsTrue(editor.Add(new ImageMask(0.2, 0.2, 0.6, 0.7)), "un nouveau cadre remplace l'ancien");
        Assert.AreEqual(new ImageMask(0.2, 0.2, 0.6, 0.7), editor.Masks.Single());
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.AreEqual("kyfrev-largeur:60 kyfrev-cadre:20,20,60,70", titles.Single());
        Assert.AreEqual(ImageMaskText.Cropped, editor.LastSaved);

        // Rouverte, la fenêtre montre le cadre ; le garder tel quel ne retire pas les masques posés depuis.
        editor.OpenFor(picture, "kyfrev-cadre:20,20,60,70 kyfrev-masques:5,5,10,10", titles.Add, crop: true);
        Assert.AreEqual(new ImageMask(0.2, 0.2, 0.6, 0.7), editor.Masks.Single());
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.AreEqual("kyfrev-cadre:20,20,60,70 kyfrev-masques:5,5,10,10", titles[1]);

        // Tout retirer rend l'image entière.
        editor.OpenFor(picture, "kyfrev-largeur:60 kyfrev-cadre:20,20,60,70", titles.Add, crop: true);
        editor.ClearCommand.Execute(null);
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.AreEqual("kyfrev-largeur:60", titles[2]);
        Assert.AreEqual(ImageMaskText.Uncropped, editor.LastSaved);

        // Poser des masques ne touche ni à la largeur ni au cadre.
        editor.OpenFor(picture, "kyfrev-largeur:60 kyfrev-cadre:20,20,60,70", titles.Add);
        Assert.IsFalse(editor.IsCrop);
        Assert.AreEqual(ImageMaskText.Title, editor.DialogTitle);
        editor.Add(new ImageMask(0.1, 0.1, 0.2, 0.2));
        editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
        Assert.AreEqual("kyfrev-largeur:60 kyfrev-cadre:20,20,60,70 kyfrev-masques:10,10,20,20", titles[3]);
    }

    [STATestMethod]
    public void Cropping_shows_the_whole_image_under_a_veil_with_handles_to_pull()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        var picture = new System.Windows.Media.Imaging.WriteableBitmap(4, 4, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        var titles = new List<string?>();
        editor.OpenFor(picture, null, titles.Add, crop: true);
        var test = new TestShell();
        var dialog = new ImageMaskDialog { DataContext = editor };
        var window = new Window { Content = dialog, Left = -10000, Top = -10000, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var surface = (System.Windows.Controls.Canvas)dialog.FindName("Surface");
            surface.Width = 200;
            surface.Height = 100;
            TestShell.Flush();

            // Rien n'est encore rogné : le cadre est l'image entière, avec son voile (vide), son contour et ses huit poignées.
            Assert.AreEqual(new ImageMask(0, 0, 1, 1), editor.Crop);
            Assert.AreEqual(13, surface.Children.Count);
            Assert.AreEqual(ImageMaskDialog.Grip.Left | ImageMaskDialog.Grip.Top, dialog.GripAt(new Point(2, 3)));
            Assert.AreEqual(ImageMaskDialog.Grip.Right, dialog.GripAt(new Point(198, 50)));
            Assert.AreEqual(ImageMaskDialog.Grip.Bottom, dialog.GripAt(new Point(100, 97)));
            Assert.AreEqual(ImageMaskDialog.Grip.Move, dialog.GripAt(new Point(100, 50)));

            // L'angle haut gauche tiré vers l'intérieur, puis le bord droit.
            Assert.IsTrue(dialog.DragCrop(ImageMaskDialog.Grip.Left | ImageMaskDialog.Grip.Top, editor.Crop, new Point(0, 0), new Point(40, 20)));
            Assert.AreEqual(new ImageMask(0.2, 0.2, 0.8, 0.8), editor.Crop);
            Assert.IsTrue(dialog.DragCrop(ImageMaskDialog.Grip.Right, editor.Crop, new Point(200, 50), new Point(160, 50)));
            Assert.AreEqual(new ImageMask(0.2, 0.2, 0.6, 0.8), editor.Crop);
            Assert.AreEqual(ImageMaskDialog.Grip.None, dialog.GripAt(new Point(5, 5)), "hors du cadre, un appui ne saisit rien");

            // Le cadre se déplace entier, et s'arrête aux bords ; un bord ne croise pas celui d'en face.
            dialog.DragCrop(ImageMaskDialog.Grip.Move, editor.Crop, new Point(100, 50), new Point(500, 50));
            Assert.AreEqual(new ImageMask(0.4, 0.2, 0.6, 0.8), editor.Crop);
            dialog.DragCrop(ImageMaskDialog.Grip.Left, editor.Crop, new Point(80, 50), new Point(900, 50));
            Assert.AreEqual(0.02, editor.Crop.Width, 0.0001);

            editor.AnswerCommand.ExecuteAsync(true).GetAwaiter().GetResult();
            Assert.AreEqual("kyfrev-cadre:98,20,2,80", titles.Single());
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void The_size_of_an_image_reads_in_centimetres_and_a_typed_size_gives_its_width()
    {
        // 378 pixels de large dans un texte de 756 : 10 cm, la moitié du texte ; deux fois plus haute que large.
        var image = new KyfRev.Presentation.Wpf.Editing.ImageState(1, "/coffre/a.png", null, "a", true, 50, "fil", null, 0, 378, 756, 756);
        Assert.AreEqual("10,0 cm", image.WidthCm);
        Assert.AreEqual("20,0 cm", image.HeightCm);
        Assert.AreEqual(25, image.PercentFor("5", height: false));
        Assert.AreEqual(25, image.PercentFor(" 5,0 cm ", height: false));
        Assert.AreEqual(25, image.PercentFor("10.0", height: true), "la hauteur demandée donne la largeur qui garde les proportions");
        Assert.AreEqual(100, image.PercentFor("80", height: false), "pas plus large que le texte");
        Assert.AreEqual(5, image.PercentFor("0,1", height: false));
        Assert.IsNull(image.PercentFor("large", height: false));
        Assert.IsNull(image.PercentFor("0", height: false));
        Assert.IsNull((image with { Room = 0 }).PercentFor("5", height: false), "sans mesure, rien à calculer");
        Assert.IsTrue(image.IsInline);
        Assert.IsTrue((image with { Align = "centre" }).IsCenter);
    }

    [STATestMethod]
    public void In_reading_an_image_takes_its_share_of_the_text_width_and_its_place()
    {
        var picture = new System.Windows.Media.Imaging.WriteableBitmap(200, 100, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        System.Windows.Controls.Image Shown() => new() { Source = picture, Stretch = System.Windows.Media.Stretch.Uniform };

        var half = new KyfRev.Presentation.Wpf.Reading.ImageHost { Child = Shown(), Percent = 50, Align = ImageAlign.Center };
        half.Measure(new Size(600, double.PositiveInfinity));
        half.Arrange(new Rect(half.DesiredSize));
        Assert.AreEqual(new Size(600, 150), half.DesiredSize, "seule sur sa ligne, à la moitié du texte : 300 de large, donc 150 de haut");
        Assert.AreEqual(new Rect(150, 0, 300, 150), System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)half.Child));

        var right = new KyfRev.Presentation.Wpf.Reading.ImageHost { Child = Shown(), Percent = 25, Align = ImageAlign.Right };
        right.Measure(new Size(400, double.PositiveInfinity));
        right.Arrange(new Rect(right.DesiredSize));
        Assert.AreEqual(new Rect(300, 0, 100, 50), System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)right.Child));

        var inline = new KyfRev.Presentation.Wpf.Reading.ImageHost { Child = Shown(), Percent = 25, Align = ImageAlign.Inline };
        inline.Measure(new Size(400, double.PositiveInfinity));
        Assert.AreEqual(new Size(100, 50), inline.DesiredSize, "dans le fil du texte, elle ne prend que sa largeur");
    }

    [TestMethod]
    public async Task The_keyboard_adds_places_and_resizes_a_zone()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        await editor.OpenAsync(vault.SheetId, 0, null);
        Assert.AreEqual(-1, editor.SelectedIndex);
        Assert.AreEqual(string.Empty, editor.Selection);

        editor.AddZoneCommand.Execute(null);
        Assert.AreEqual(new ImageMask(0.3, 0.35, 0.2, 0.2), editor.Masks.Single());
        Assert.AreEqual(0, editor.SelectedIndex);
        Assert.AreEqual("Zone 1 sur 1 : à 30 % du bord gauche et 35 % du haut, large de 20 %, haute de 20 %.", editor.Selection);

        editor.MoveSelected(0.01, -0.05);
        editor.ResizeSelected(0.1, -0.5);
        Assert.AreEqual(new ImageMask(0.31, 0.3, 0.3, 0.01), editor.Masks.Single(), "elle ne devient pas trop petite pour être vue");
        editor.MoveSelected(2, 2);
        Assert.AreEqual(new ImageMask(0.7, 0.99, 0.3, 0.01), editor.Masks.Single(), "elle s'arrête aux bords");
        editor.ResizeSelected(2, 2);
        Assert.AreEqual(new ImageMask(0.7, 0.99, 0.3, 0.01), editor.Masks.Single(), "elle ne sort pas de l'image");

        editor.AddZoneCommand.Execute(null);
        Assert.AreEqual(1, editor.SelectedIndex, "la zone ajoutée est la zone choisie");
        Assert.AreNotEqual(new ImageMask(0.3, 0.35, 0.2, 0.2), editor.Masks[1], "elle ne recouvre pas exactement la précédente");
        editor.SelectBy(1);
        Assert.AreEqual(0, editor.SelectedIndex, "après la dernière, la première");
        editor.SelectBy(-1);
        Assert.AreEqual(1, editor.SelectedIndex);
        editor.SelectBy(-1);
        editor.RemoveSelected();
        Assert.AreEqual(0, editor.SelectedIndex, "une zone retirée laisse la main à sa voisine");
        Assert.HasCount(1, editor.Masks);
        editor.RemoveSelected();
        Assert.AreEqual(-1, editor.SelectedIndex);
        editor.MoveSelected(0.1, 0.1);
        editor.RemoveSelected();
        Assert.IsEmpty(editor.Masks);

        for (var i = 0; i < ImageMasks.Maximum + 3; i++)
        {
            if (editor.AddZoneCommand.CanExecute(null))
            {
                editor.AddZoneCommand.Execute(null);
            }
        }
        Assert.HasCount(ImageMasks.Maximum, editor.Masks);
        Assert.IsFalse(editor.AddZoneCommand.CanExecute(null));
    }

    [STATestMethod]
    public void Keys_on_the_image_drive_the_chosen_zone()
    {
        using var vault = new MaskVault(Body);
        var editor = vault.Editor();
        editor.OpenAsync(vault.SheetId, 0, null).GetAwaiter().GetResult();
        var test = new TestShell();
        var dialog = new ImageMaskDialog { DataContext = editor };
        var window = new Window { Content = dialog, Left = -10000, Top = -10000, Width = 700, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        try
        {
            TestShell.Flush();
            var surface = (System.Windows.Controls.Canvas)dialog.FindName("Surface");
            surface.Width = 200;
            surface.Height = 100;
            TestShell.Flush();
            Assert.IsTrue(surface.Focusable);
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(surface);
            Assert.AreEqual(ImageMaskText.Surface, peer.GetName(), "le lecteur d'écran voit et nomme l'image");
            Assert.IsTrue(peer.IsKeyboardFocusable());

            Assert.IsTrue(dialog.Press(System.Windows.Input.Key.Left, System.Windows.Input.ModifierKeys.None), "une flèche reste sur l'image, même sans zone");
            Assert.IsFalse(dialog.Press(System.Windows.Input.Key.Tab, System.Windows.Input.ModifierKeys.None), "Tab quitte l'image");
            Assert.IsFalse(dialog.Press(System.Windows.Input.Key.Left, System.Windows.Input.ModifierKeys.Alt));

            Assert.IsTrue(dialog.Press(System.Windows.Input.Key.Insert, System.Windows.Input.ModifierKeys.None));
            Assert.IsTrue(dialog.Press(System.Windows.Input.Key.Insert, System.Windows.Input.ModifierKeys.None));
            Assert.AreEqual(2, surface.Children.Count);
            Assert.AreEqual(4d, ((System.Windows.Shapes.Rectangle)surface.Children[1]).StrokeThickness, "la zone choisie a un contour plus épais");
            Assert.AreEqual(2d, ((System.Windows.Shapes.Rectangle)surface.Children[0]).StrokeThickness);

            dialog.Press(System.Windows.Input.Key.PageDown, System.Windows.Input.ModifierKeys.None);
            Assert.AreEqual(4d, ((System.Windows.Shapes.Rectangle)surface.Children[0]).StrokeThickness);
            dialog.Press(System.Windows.Input.Key.Right, System.Windows.Input.ModifierKeys.None);
            dialog.Press(System.Windows.Input.Key.Down, System.Windows.Input.ModifierKeys.Control);
            dialog.Press(System.Windows.Input.Key.Left, System.Windows.Input.ModifierKeys.Shift);
            dialog.Press(System.Windows.Input.Key.Down, System.Windows.Input.ModifierKeys.Shift | System.Windows.Input.ModifierKeys.Control);
            Assert.AreEqual(new ImageMask(0.31, 0.4, 0.19, 0.25), editor.Masks[0]);
            Assert.AreEqual(62d, System.Windows.Controls.Canvas.GetLeft(surface.Children[0]), 0.001);

            dialog.Press(System.Windows.Input.Key.Delete, System.Windows.Input.ModifierKeys.None);
            Assert.HasCount(1, editor.Masks);
            Assert.AreEqual(1, surface.Children.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Un coffre d'essai avec une fiche dont le corps est donné.</summary>
    private sealed class MaskVault : IDisposable
    {
        private readonly TestVaults _vaults = new(AppMode.Development);

        public MaskVault(string body)
        {
            Assert.IsNotNull(_vaults.App.Vaults.Create(_vaults.PathOf("Coffre")).Vault);
            SheetId = _vaults.App.Sheets.Create(string.Empty, "Cellule", null).Value;
            Write(body);
        }

        public string SheetId { get; }

        public ImageMaskEditorViewModel Editor() => new(_vaults.App.Sheets, _vaults.App.Editing);

        public string Body() => _vaults.App.Sheets.Open(SheetId).Value.Body;

        public void Write(string body)
        {
            var opened = _vaults.App.Sheets.Open(SheetId).Value;
            Assert.IsInstanceOfType<SheetSaved>(_vaults.App.Editing.Save(SheetId, body, opened.Fingerprint, 42).Value);
        }

        public void Dispose() => _vaults.Dispose();
    }
}
