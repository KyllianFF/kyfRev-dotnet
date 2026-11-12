using KyfRev.Application.Model;
using KyfRev.Application.Modules;
using KyfRev.DesignSystem.Controls;
using KyfRev.DesignSystem.Tokens;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La coquille, sans fenêtre : menu, recherche de commandes, aide, mode concentration, zoom.</summary>
[TestClass]
public sealed class ShellViewModelTests
{
    private static readonly string[] MainLabels = ["Aujourd'hui", "Carnet", "Définitions", "Archives", "Statistiques", "Citations", "Quiz"];
    private static readonly string[] SecondaryLabels = ["Santé du coffre", "Paramètres"];

    [STATestMethod]
    public void The_menu_follows_the_order_of_version_0x_with_modules_in_place()
    {
        var test = new TestShell();
        var shell = new ShellViewModel(
            CorePages.All().Append(CorePages.FromModule(new NavigationEntry("citations", "Citations", 60))).Append(CorePages.FromModule(new NavigationEntry("quiz", "Quiz", 70))),
            test.Appearance,
            test.Gallery);

        CollectionAssert.AreEqual(MainLabels, shell.MainNavigation.Select(item => item.Label).ToList());
        CollectionAssert.AreEqual(SecondaryLabels, shell.SecondaryNavigation.Select(item => item.Label).ToList());
        Assert.AreEqual(Icons.Quote, shell.MainNavigation[5].Glyph);
        Assert.AreEqual(Icons.Puzzle, shell.MainNavigation[6].Glyph);
        Assert.AreSame(shell.MainNavigation[0], shell.Current);
        Assert.AreSame(shell.Current, shell.SelectedMain);
        Assert.AreEqual("Aujourd'hui", shell.Current.ToString());
        Assert.IsInstanceOfType<PlaceholderPageViewModel>(shell.Current.Page);
        Assert.ThrowsExactly<ArgumentNullException>(() => CorePages.FromModule(null!));
    }

    [STATestMethod]
    public async Task A_quick_note_is_taken_from_any_view_once_a_vault_is_open()
    {
        var shell = new TestShell().Shell;
        var notes = new List<string>();
        string? refusal = null;
        shell.Capture = text =>
        {
            notes.Add(text);
            return refusal is null ? KyfRev.Application.Outcome.Of("f_boite") : KyfRev.Application.Outcome.Refused<string>(refusal);
        };

        Assert.IsFalse(shell.CanCapture, "sans coffre ouvert, il n'y a nulle part où ranger une note");
        Assert.IsFalse(shell.OpenCapture());
        shell.RequestCaptureCommand.Execute(null);
        Assert.IsFalse(shell.IsCaptureOpen);

        shell.ShowWorkspace();
        shell.ShowHelp();
        Assert.IsTrue(shell.OpenCapture());
        Assert.IsTrue(shell.IsCaptureOpen);
        Assert.IsFalse(shell.IsHelpOpen, "une seule fenêtre à la fois");

        shell.CaptureText = "Revoir la mitose";
        await shell.AnswerCaptureCommand.ExecuteAsync(false);
        Assert.IsFalse(shell.IsCaptureOpen);
        Assert.IsEmpty(notes, "fermer ne note rien");

        shell.OpenCapture();
        Assert.AreEqual(string.Empty, shell.CaptureText, "chaque capture repart d'une note vide");
        shell.CaptureText = "Revoir la mitose";
        refusal = "il n'y a rien à noter.";
        await shell.AnswerCaptureCommand.ExecuteAsync(true);
        Assert.IsTrue(shell.IsCaptureOpen, "un refus laisse la fenêtre ouverte");
        Assert.AreEqual(refusal, shell.CaptureError);
        Assert.AreEqual("Revoir la mitose", shell.CaptureText, "et le texte tapé reste");

        refusal = null;
        var shown = new RefreshProbe();
        shell.Navigate(new NavigationItem("carnet-essai", "Carnet", KyfRev.DesignSystem.Controls.Icons.Document, int.MaxValue, shown));
        var before = shown.Refreshed;
        shell.OpenCapture();
        shell.CaptureText = "Revoir la mitose";
        await shell.AnswerCaptureCommand.ExecuteAsync(true);
        Assert.IsFalse(shell.IsCaptureOpen);
        Assert.IsFalse(shell.HasCaptureError);
        Assert.AreEqual("Revoir la mitose", notes[^1]);
        Assert.AreEqual(before + 1, shown.Refreshed, "la vue affichée montre la note sans qu'on la quitte");

        shell.OpenPaletteCommand.Execute(null);
        shell.PaletteQuery = "capture";
        Assert.AreEqual("Capture rapide", shell.PaletteResults.Single().Label);
        Assert.IsTrue(shell.Shortcuts.Any(shortcut => shortcut.Keys == "Ctrl+Maj+N"));
    }

    [TestMethod]
    public void The_menu_keeps_its_width_in_every_view_and_only_the_window_narrows_it()
    {
        var test = new TestShell();
        var shell = new ShellViewModel(CorePages.All(), test.Appearance, test.Gallery) { WindowWidth = 1600 };
        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout);

        shell.Navigate(shell.MainNavigation.Single(item => item.Id == CorePages.Sheets));
        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout, "le Carnet garde le menu entier");
        shell.Navigate(new NavigationItem(CorePages.Sheet, "Une fiche", KyfRev.DesignSystem.Controls.Icons.Document, int.MaxValue, new object()));
        Assert.AreEqual(NavigationLayout.Sidebar, shell.Layout);

        shell.WindowWidth = 1000;
        Assert.AreEqual(NavigationLayout.Rail, shell.Layout);
        Assert.IsTrue(shell.CanToggleMenu);

        // En fenêtre étroite, les onglets du bas : pas de menu latéral à masquer.
        shell.WindowWidth = 500;
        Assert.AreEqual(NavigationLayout.Tabs, shell.Layout);
        Assert.IsFalse(shell.CanToggleMenu);
    }

    [STATestMethod]
    public void Navigating_selects_the_entry_in_its_part_of_the_menu()
    {
        var shell = new TestShell().Shell;
        var settings = shell.SecondaryNavigation[1];

        shell.Navigate(settings);
        Assert.AreSame(settings, shell.Current);
        Assert.AreSame(settings, shell.SelectedSecondary);
        Assert.IsNull(shell.SelectedMain);

        shell.NavigateToPosition("3");
        Assert.AreEqual("Définitions", shell.Current.Label);
        Assert.IsNull(shell.SelectedSecondary);

        shell.Navigate(shell.Gallery);
        Assert.AreSame(shell.Gallery, shell.Current);
        Assert.IsNull(shell.SelectedMain);
        Assert.IsInstanceOfType<GalleryPageViewModel>(shell.Current.Page);

        foreach (var position in new[] { "0", "9", "x", "-1" })
        {
            shell.NavigateToPosition(position);
            Assert.AreSame(shell.Gallery, shell.Current, position);
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => shell.Navigate(null!));
    }

    [STATestMethod]
    public void The_commands_of_the_extensions_join_the_search()
    {
        var shell = new TestShell().Shell;
        var ran = 0;
        Assert.ThrowsExactly<ArgumentNullException>(() => shell.SetExtensionCommands(null!));

        shell.SetExtensionCommands([new PaletteEntry("Quiz : Nouveau quiz", null, Icons.Puzzle, () => ran++)]);
        shell.OpenPalette();
        shell.PaletteQuery = "nouveau quiz";
        Assert.AreEqual("Quiz : Nouveau quiz", shell.PaletteResults.Single().Label);
        shell.RunSelected();
        Assert.AreEqual(1, ran);

        shell.SetModuleEntries([]);
        shell.OpenPalette();
        shell.PaletteQuery = "nouveau quiz";
        Assert.HasCount(1, shell.PaletteResults, "Refaire le menu garde les commandes des extensions.");

        shell.SetExtensionCommands([]);
        Assert.IsEmpty(shell.PaletteResults);
    }

    [STATestMethod]
    public void The_search_finds_commands_without_case_or_accents()
    {
        var shell = new TestShell().Shell;
        shell.OpenPalette();
        Assert.IsTrue(shell.IsPaletteOpen);
        Assert.IsGreaterThan(10, shell.PaletteResults.Count);
        Assert.AreSame(shell.PaletteResults[0], shell.SelectedResult);

        shell.PaletteQuery = "PARAMETRES";
        Assert.AreEqual("Aller à : Paramètres", shell.PaletteResults.Single().Label);
        shell.PaletteQuery = "aller def";
        Assert.AreEqual("Aller à : Définitions", shell.PaletteResults.Single().Label);
        Assert.AreEqual("Ctrl+3", shell.PaletteResults.Single().Detail);
        shell.RunSelected();
        Assert.IsFalse(shell.IsPaletteOpen);
        Assert.AreEqual("Définitions", shell.Current.Label);

        shell.OpenPalette();
        Assert.AreEqual(string.Empty, shell.PaletteQuery);
        shell.PaletteQuery = "zzz";
        Assert.IsTrue(shell.HasNoResult);
        Assert.IsNull(shell.SelectedResult);
        shell.MoveSelection("1");
        shell.RunSelected();
        Assert.IsTrue(shell.IsPaletteOpen);
        shell.ClosePalette();
        Assert.IsFalse(shell.IsPaletteOpen);
        Assert.AreEqual("evenement ete", ShellViewModel.Fold("Événement Été"));
    }

    [STATestMethod]
    public void The_highlight_moves_in_a_loop()
    {
        var shell = new TestShell().Shell;
        shell.OpenPalette();
        shell.PaletteQuery = "interface";
        var results = shell.PaletteResults;
        Assert.HasCount(2, results);

        shell.MoveSelection("-1");
        Assert.AreSame(results[1], shell.SelectedResult);
        shell.MoveSelection("1");
        Assert.AreSame(results[0], shell.SelectedResult);
        shell.MoveSelection("1");
        Assert.AreSame(results[1], shell.SelectedResult);
        shell.MoveSelection("1");
        Assert.AreSame(results[0], shell.SelectedResult);
        shell.MoveSelection("1");
        shell.MoveSelection("x");
        Assert.AreSame(results[1], shell.SelectedResult);
        shell.SelectedResult = null;
        shell.MoveSelection("1");
        Assert.AreSame(results[0], shell.SelectedResult);
    }

    [STATestMethod]
    public void Every_command_of_the_search_does_what_it_says()
    {
        var test = new TestShell();
        var shell = test.Shell;
        string Run(string query)
        {
            shell.OpenPalette();
            shell.PaletteQuery = query;
            var label = shell.SelectedResult!.Label;
            shell.RunSelected();
            return label;
        }

        Assert.AreEqual("Mode concentration", Run("concentration"));
        Assert.IsTrue(shell.IsFocusMode);
        Assert.AreEqual("Agrandir l'interface", Run("agrandir"));
        Assert.AreEqual(1.1, shell.Zoom, 1e-9);
        Assert.AreEqual("Réduire l'interface", Run("reduire"));
        Assert.AreEqual(1.0, shell.Zoom, 1e-9);
        Run("reduire");
        Assert.AreEqual("Revenir à la taille normale", Run("normale"));
        Assert.AreEqual(1.0, shell.Zoom, 1e-9);
        Assert.AreEqual("Raccourcis clavier", Run("raccourcis"));
        Assert.IsTrue(shell.IsHelpOpen);
        Run("galerie");
        Assert.AreSame(shell.Gallery, shell.Current);
        Run("sante");
        Assert.AreEqual("Santé du coffre", shell.Current.Label);
        Assert.AreEqual("Thème : sombre", Run("theme sombre"));
        Assert.AreEqual(ThemePreference.Dark, test.Appearance.Preference);
        Assert.AreEqual(ThemeKind.Dark, test.Appearance.Theme);
        Run("theme clair");
        Assert.AreEqual(ThemeKind.Light, test.Appearance.Theme);
        Run("windows");
        Assert.AreEqual(ThemePreference.System, test.Appearance.Preference);
    }

    [STATestMethod]
    public void Zoom_stays_between_80_and_200_percent()
    {
        var shell = new TestShell().Shell;
        Assert.AreEqual("Zoom : 100 %", shell.ZoomLabel);
        for (var i = 0; i < 20; i++)
        {
            shell.ZoomIn();
        }
        Assert.AreEqual(ShellViewModel.MaximumZoom, shell.Zoom);
        Assert.AreEqual("Zoom : 200 %", shell.ZoomLabel);
        for (var i = 0; i < 20; i++)
        {
            shell.ZoomOut();
        }
        Assert.AreEqual(ShellViewModel.MinimumZoom, shell.Zoom);
        shell.ResetZoom();
        Assert.AreEqual(1.0, shell.Zoom);
    }

    [STATestMethod]
    public void Help_and_search_never_overlap()
    {
        var shell = new TestShell().Shell;
        Assert.HasCount(17, shell.Shortcuts);
        Assert.AreEqual("Ctrl+Maj+L", shell.Shortcuts[^1].Keys);
        Assert.AreEqual("F11 | Ctrl+Entrée | Entrée, Espace | H | Ctrl+E | Ctrl+P | Ctrl+Maj+P | Ctrl+Maj+N | Ctrl+→", string.Join(" | ", shell.Shortcuts.Skip(3).Take(9).Select(shortcut => shortcut.Keys)));
        shell.OpenPalette();
        shell.ShowHelp();
        Assert.IsTrue(shell.IsHelpOpen);
        Assert.IsFalse(shell.IsPaletteOpen);
        shell.OpenPalette();
        Assert.IsFalse(shell.IsHelpOpen);
        shell.ToggleFocusMode();
        shell.ToggleFocusMode();
        Assert.IsFalse(shell.IsFocusMode);
    }

    [STATestMethod]
    public void A_shell_without_menu_entries_opens_the_gallery()
    {
        var test = new TestShell();
        var shell = new ShellViewModel([], test.Appearance, test.Gallery);
        Assert.AreSame(shell.Gallery, shell.Current);
        Assert.IsNull(shell.SelectedMain);

        var onlySecondary = new ShellViewModel(CorePages.All().Where(item => item.IsSecondary), test.Appearance, test.Gallery);
        Assert.AreEqual("Santé du coffre", onlySecondary.Current.Label);
        Assert.ThrowsExactly<ArgumentNullException>(() => new ShellViewModel(null!, test.Appearance, test.Gallery));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ShellViewModel([], null!, test.Gallery));
    }

    /// <summary>Une vue qui compte ses relectures.</summary>
    private sealed class RefreshProbe : INavigationAware
    {
        public int Refreshed { get; private set; }

        public void OnNavigatedTo() => Refreshed++;
    }
}
