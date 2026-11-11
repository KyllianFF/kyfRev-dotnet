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
