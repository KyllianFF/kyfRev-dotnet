using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Reading;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>Le papier des feuilles, Ctrl+P dans le texte lu, les raccourcis que l'on change et l'ordre du menu.</summary>
[TestClass]
[DoNotParallelize]
public sealed class PaperAndShortcutsTests
{
    [STATestMethod]
    public void Ctrl_P_in_the_text_asks_the_application_and_never_opens_the_windows_dialog()
    {
        var asked = 0;
        var viewer = new SheetDocumentViewer { Width = 400, Height = 300, Reading = Reading("Texte.") };

        // Sans commande, le raccourci ne fait rien : surtout pas ouvrir la fenêtre d'impression de Windows, qui bloquerait ici.
        viewer.Print();
        ApplicationCommands.Print.Execute(null, viewer);

        viewer.PrintCommand = new RelayCommand(() => asked++);
        viewer.Print();
        Assert.AreEqual(1, asked);
    }

    [STATestMethod]
    public void A_paper_is_drawn_under_the_text_and_follows_the_sheet_it_shows()
    {
        var a5 = new PageLayout(PageFormat.A5);
        var paper = new SheetPaper(new PaperChoices(IsOn: true, Endless: PaperStyle.Lines, A5: PaperStyle.LargeSquares), PageLayout.Default, SheetPageLayouts.None.With("f_1", a5));
        var viewer = new SheetDocumentViewer { Width = 420, Height = 400, Paper = paper };
        var host = new PageFitHost { Child = viewer };
        var window = new Window { Content = host, Left = -10000, Top = -10000, Width = 460, Height = 460, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try
        {
            viewer.Reading = Reading(string.Join("\n\n", Enumerable.Range(1, 12).Select(index => $"Paragraphe {index} avec assez de mots pour tenir sa ligne.")));
            viewer.CompleteDocument();
            Settle(viewer);

            // Une vue qui ne suit pas la fiche : la feuille sans fin, et son papier ligné sur toute la longueur du texte.
            Assert.IsNull(viewer.Layout);
            Assert.AreEqual(PaperStyle.Lines, viewer.PaperShown);
            Assert.AreEqual(1, viewer.PagePaper.Count);
            Assert.AreEqual(PaperStyle.Lines, viewer.PagePaper.Ruling);
            Assert.AreEqual(28, viewer.Document!.PagePadding.Left, "le texte ne touche pas le bord du papier");
            Assert.AreEqual(1, host.Scale);

            // Elle suit la fiche : la feuille A5 de cette fiche, son papier à elle, et la vue rétrécie pour tenir dans la largeur.
            viewer.FollowsSheetLayout = true;
            Settle(viewer);
            Assert.AreEqual(a5, viewer.Layout);
            Assert.AreEqual(PaperStyle.LargeSquares, viewer.PagePaper.Ruling);
            Assert.IsGreaterThan(0, viewer.PageCount);
            Assert.AreEqual(viewer.PageCount, viewer.PagePaper.Count);
            Assert.AreEqual(a5.WidthPixels, viewer.PagePaper.SheetWidth);
            Assert.IsLessThan(1, host.Scale, "une feuille plus large que l'aperçu est vue de plus loin");
            Assert.AreEqual(a5.MarginPixels, viewer.Document.PagePadding.Left, 1);

            // Le papier éteint : les feuilles restent, sans réglure ; sur la feuille sans fin, plus rien.
            viewer.Paper = paper with { Choices = paper.Choices with { IsOn = false } };
            Settle(viewer);
            Assert.IsNull(viewer.PagePaper.Ruling);
            Assert.AreEqual(viewer.PageCount, viewer.PagePaper.Count);
            viewer.FollowsSheetLayout = false;
            Settle(viewer);
            Assert.AreEqual(0, viewer.PagePaper.Count);
            Assert.AreEqual(0, viewer.Document.PagePadding.Left);

            // Ce qui s'imprime n'a jamais de papier.
            viewer.Paper = paper;
            viewer.DrawsPaper = false;
            Settle(viewer);
            Assert.IsNull(viewer.PaperShown);
            Assert.AreEqual(0, viewer.PagePaper.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void A_shortcut_reads_as_its_keys_and_an_unreadable_one_keeps_the_original()
    {
        Assert.AreEqual("Ctrl+Alt+Maj+Espace", ShortcutMap.Display(Key.Space, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift));
        Assert.AreEqual("Alt+→", ShortcutMap.Display(Key.Right, ModifierKeys.Alt));
        Assert.AreEqual("Ctrl+←", ShortcutMap.Display(Key.Left, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+↑", ShortcutMap.Display(Key.Up, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+↓", ShortcutMap.Display(Key.Down, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+plus", ShortcutMap.Display(Key.OemPlus, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+moins", ShortcutMap.Display(Key.Subtract, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+Entrée", ShortcutMap.Display(Key.Enter, ModifierKeys.Control));
        Assert.AreEqual("Ctrl+7", ShortcutMap.Display(Key.D7, ModifierKeys.Control));

        // Écrits dans les préférences : Alt se relit ; un mot inconnu, deux touches ou une touche seule laissent l'origine.
        var map = ShortcutMap.Parse("aide=Alt+F2;imprimer=Ctrl+Alt+J;partager=Ctrl+Truc;capture=Ctrl+J+K;recherche=J");
        Assert.AreEqual("Alt+F2", map.DisplayOf("aide"));
        Assert.AreEqual("Ctrl+Alt+J", map.DisplayOf("imprimer"));
        Assert.IsFalse(map.IsChanged("partager") || map.IsChanged("capture") || map.IsChanged("recherche"));
        Assert.AreEqual("aide=Alt+F2;imprimer=Ctrl+Alt+J", map.Written());
    }

    [TestMethod]
    public void The_paper_is_chosen_for_each_kind_of_sheet_and_kept_on_this_computer()
    {
        var store = new MemoryMachinePreferences();
        var machine = new MachineSettings(store);
        var setup = new PageSetupViewModel(null, true, null, () => machine.Current.Paper, paper => machine.Update(preferences => preferences with { Paper = paper }));
        Assert.IsFalse(setup.IsPaperOn);
        Assert.IsTrue(setup.IsPaperPlain);

        setup.SetPaperCommand.Execute("lignes");
        Assert.IsTrue(setup.IsPaperOn && setup.IsPaperLines, "choisir un papier l'allume");
        setup.SetFormatCommand.Execute("A4");
        Assert.IsTrue(setup.IsPaperPlain, "chaque type de feuille a son papier");
        setup.SetPaperCommand.Execute("grands-carreaux");
        setup.TogglePaperCommand.Execute(null);

        Assert.AreEqual(new PaperChoices(IsOn: false, Endless: PaperStyle.Lines, A4: PaperStyle.LargeSquares), store.Stored.Paper);
        Assert.AreEqual(PaperStyle.LargeSquares, SheetPaper.Of(store.Stored).Choices.A4);
        Assert.IsNull(SheetPaper.Of(store.Stored).StyleOf(PageFormat.A4), "éteint, il ne se dessine pas");

        // L'onglet « Fiches » des paramètres règle les mêmes choix, et la mise en page de toutes les fiches.
        var page = new SheetsSettingsViewModel(machine);
        Assert.AreEqual(PaperStyle.Lines, page.PaperEndless.Value);
        page.IsPaperOn = true;
        page.PaperA6 = SheetsSettingsViewModel.Papers.Single(choice => choice.Value == PaperStyle.Dots);
        page.Format = SheetsSettingsViewModel.Formats.Single(choice => choice.Value == PageFormat.A5);
        page.Orientation = SheetsSettingsViewModel.Orientations[1];
        page.Margin = page.Margins[0];
        Assert.AreEqual(new PaperChoices(true, PaperStyle.Lines, PaperStyle.LargeSquares, PaperStyle.Plain, PaperStyle.Dots), store.Stored.Paper);
        Assert.AreEqual(new PageLayout(PageFormat.A5, Landscape: true, MarginCm: PageLayout.NarrowMargin), store.Stored.Page);
        Assert.IsTrue(setup.IsPaperOn, "le bouton de la fiche relit ce qui est réglé ailleurs");
    }

    [TestMethod]
    public void A_shortcut_is_changed_by_typing_it_and_a_taken_one_is_refused()
    {
        try
        {
            var store = new MemoryMachinePreferences();
            var page = new ShortcutsSettingsViewModel(new MachineSettings(store));
            var print = page.Rows.Single(row => row.Id == "imprimer");
            Assert.AreEqual("Ctrl+P", print.Keys);
            Assert.IsFalse(page.Take(Key.I, ModifierKeys.Control), "rien n'attend : la frappe n'est pas prise");

            page.ChangeCommand.Execute(print);
            Assert.IsTrue(print.IsWaiting && page.IsWaiting);
            Assert.IsTrue(page.Take(Key.LeftCtrl, ModifierKeys.Control), "une touche tenue seule : on attend la suite");
            Assert.IsTrue(print.IsWaiting);
            Assert.IsTrue(page.Take(Key.I, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.AreEqual("Ctrl+Maj+I", print.Keys);
            Assert.IsTrue(print.IsChanged);
            Assert.AreEqual("imprimer=Ctrl+Shift+I", store.Stored.Shortcuts);
            Assert.AreEqual((Key.I, ModifierKeys.Control | ModifierKeys.Shift), ShortcutMap.Current.Of("imprimer"));

            // Une touche déjà prise, une touche sans Ctrl ni Alt, un raccourci fixe : refusés, et dits.
            page.ChangeCommand.Execute(print);
            page.Take(Key.K, ModifierKeys.Control);
            StringAssert.Contains(page.Message, "Ctrl+K sert déjà", StringComparison.Ordinal);
            page.ChangeCommand.Execute(print);
            page.Take(Key.J, ModifierKeys.None);
            StringAssert.Contains(page.Message, "ne convient pas", StringComparison.Ordinal);
            page.ChangeCommand.Execute(print);
            page.Take(Key.D0, ModifierKeys.Control);
            StringAssert.Contains(page.Message, "Ctrl+0 sert déjà", StringComparison.Ordinal);
            page.ChangeCommand.Execute(print);
            page.Take(Key.Escape, ModifierKeys.None);
            Assert.IsFalse(page.HasMessage || print.IsWaiting);
            Assert.AreEqual("Ctrl+Maj+I", print.Keys);

            // La touche libérée peut servir à une autre action ; « Rétablir » rend celle d'origine.
            var share = page.Rows.Single(row => row.Id == "partager");
            page.ChangeCommand.Execute(share);
            page.Take(Key.P, ModifierKeys.Control);
            Assert.AreEqual("Ctrl+P", share.Keys);
            page.RestoreCommand.Execute(print);
            Assert.AreEqual("Ctrl+P", print.Keys, "elle reprend sa touche d'origine");
            Assert.AreEqual("Ctrl+Maj+P", share.Keys, "et l'action qui l'avait prise retrouve la sienne");
            page.ChangeCommand.Execute(share);
            page.Take(Key.J, ModifierKeys.Control);
            Assert.AreEqual("partager=Ctrl+J", store.Stored.Shortcuts);
            page.RestoreCommand.Execute(null);
            Assert.AreEqual(string.Empty, store.Stored.Shortcuts);
            Assert.AreEqual("Ctrl+P", print.Keys);

            // Un fichier abîmé ne casse rien : ce qui ne se lit pas garde sa touche.
            var read = ShortcutMap.Parse("imprimer=Ctrl+Shift+I;inconnu=Ctrl+J;ecrire=J;partager=Ctrl+Shift+I;aide=;recherche=Ctrl+K");
            Assert.AreEqual("imprimer=Ctrl+Shift+I", read.Written());
        }
        finally
        {
            ShortcutMap.Use(null);
        }
    }

    [STATestMethod]
    public void A_view_follows_the_chosen_shortcuts()
    {
        try
        {
            var view = new System.Windows.Controls.Border();
            var binding = new KeyBinding { Key = Key.P, Modifiers = ModifierKeys.Control };
            ShortcutMap.SetId(binding, "imprimer");
            var other = new KeyBinding { Key = Key.Escape };
            view.InputBindings.Add(binding);
            view.InputBindings.Add(other);

            ShortcutMap.Use("imprimer=F9");
            ShortcutMap.ApplyTo(view);

            Assert.AreEqual((Key.F9, ModifierKeys.None), (binding.Key, binding.Modifiers));
            Assert.AreEqual(Key.Escape, other.Key, "un raccourci sans nom ne bouge pas");
            Assert.AreEqual("F9", ShortcutMap.Current.DisplayOf("imprimer"));
            ShortcutMap.Use(null);
            ShortcutMap.ApplyTo(view);
            Assert.AreEqual((Key.P, ModifierKeys.Control), (binding.Key, binding.Modifiers));
        }
        finally
        {
            ShortcutMap.Use(null);
        }
    }

    [STATestMethod]
    public void The_menu_takes_the_chosen_order_and_the_help_shows_the_shortcuts_in_force()
    {
        try
        {
            var shell = new TestShell().Shell;
            var ids = shell.MainNavigation.Select(item => item.Id).ToList();
            Assert.IsGreaterThan(2, ids.Count);
            var store = new MemoryMachinePreferences();
            var asked = 0;
            var page = new ShortcutsSettingsViewModel(new MachineSettings(store), () =>
            {
                asked++;
                return shell.MainNavigation;
            });
            Assert.AreEqual(0, asked, "le menu ne se lit pas à la construction : la coquille n'existe pas encore");
            page.OnNavigatedTo();
            CollectionAssert.AreEqual(ids, page.Menu.Select(row => row.Id).ToList());

            page.MoveUpCommand.Execute(page.Menu[0]);
            Assert.AreEqual(string.Empty, store.Stored.MenuOrder, "la première vue ne monte pas plus haut");
            page.MoveDownCommand.Execute(page.Menu[0]);
            page.MoveUpCommand.Execute(page.Menu[1]);
            page.MoveUpCommand.Execute(page.Menu[2]);
            var wanted = new List<string> { ids[0], ids[2], ids[1] };
            wanted.AddRange(ids.Skip(3));
            Assert.AreEqual(string.Join(',', wanted), store.Stored.MenuOrder);

            shell.SetMenuOrder(store.Stored.MenuOrder);
            CollectionAssert.AreEqual(wanted, shell.MainNavigation.Select(item => item.Id).ToList());
            shell.SetMenuOrder("inconnue," + ids[^1]);
            Assert.AreEqual(ids[^1], shell.MainNavigation[0].Id, "une vue inconnue est ignorée, les autres suivent dans leur ordre d'origine");
            CollectionAssert.AreEqual(ids.Take(ids.Count - 1).ToList(), shell.MainNavigation.Skip(1).Select(item => item.Id).ToList());
            page.RestoreOrderCommand.Execute(null);
            shell.SetMenuOrder(store.Stored.MenuOrder);
            CollectionAssert.AreEqual(ids, shell.MainNavigation.Select(item => item.Id).ToList());

            ShortcutMap.Use("aide=F2");
            shell.RefreshShortcuts();
            Assert.AreEqual("F2", shell.Shortcuts.Single(shortcut => shortcut.Description == ShellText.ShortcutHelp).Keys);
        }
        finally
        {
            ShortcutMap.Use(null);
        }
    }

    private static void Settle(SheetDocumentViewer viewer)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            TestShell.Flush();
            viewer.UpdateLayout();
        }
    }

    private static SheetReading Reading(string body) => SheetReading.Of(
        new OpenedSheet("f_1", "Mitose", "fiches/Biologie/Mitose.md", "Biologie", [], body, 1, "sha256:essai", null, DateTimeOffset.UnixEpoch, null!, []),
        _ => Outcome.Refused<byte[]>("non"));
}
