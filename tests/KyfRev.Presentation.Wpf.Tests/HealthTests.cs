using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;
using KyfRev.DesignSystem.Controls;
using KyfRev.Presentation.Wpf.Pages;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Santé du coffre » : ses sections et leurs textes, ceux de la 0.x, ses réparations et le diagnostic.</summary>
[TestClass]
public sealed class HealthTests
{
    private static readonly BackupEntry[] Backups = [new("sauvegarde-1.zip", new DateTime(2026, 10, 12, 8, 30, 0), 10)];

    private static readonly string[] AllSections =
    [
        "Fiches manquantes (1)", "Fichiers en double (2)", "Fichiers non suivis (2)", "Liens cassés (1)", "Passages à vérifier (2)",
        "Passages sans fiche (1)", "Fichiers à examiner (2)", "Pour information",
    ];

    [TestMethod]
    public void The_page_needs_everything_it_uses()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var app = vaults.App;
        var opener = new FakeOpener();
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(null!, app.Backups, app.Sheets, opener, () => "", _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(app.Maintenance, null!, app.Sheets, opener, () => "", _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(app.Maintenance, app.Backups, null!, opener, () => "", _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(app.Maintenance, app.Backups, app.Sheets, null!, () => "", _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(app.Maintenance, app.Backups, app.Sheets, opener, null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => new HealthPageViewModel(app.Maintenance, app.Backups, app.Sheets, opener, () => "", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Page(vaults, opener).Show(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Page(vaults, opener).Show(Report(), null!));
    }

    [TestMethod]
    public void A_healthy_vault_says_so()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        Assert.AreEqual("Santé du coffre", HealthPageViewModel.Title);

        page.Show(Report(), []);

        Assert.IsTrue(page.IsHealthy);
        Assert.IsFalse(page.HasProblems);
        Assert.AreEqual("Aucun problème : tout est en ordre.", page.Summary);
        Assert.IsEmpty(page.Sections);
        Assert.IsFalse(page.HasSections);
    }

    [TestMethod]
    public void Problems_are_counted_and_nothing_is_repaired_without_the_person()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        page.Show(Report(problems: 1), []);
        Assert.AreEqual("1 point attend une décision. Rien n'est réparé ni effacé sans vous.", page.Summary);
        Assert.IsTrue(page.HasProblems);

        page.Show(Report(problems: 3), []);
        StringAssert.StartsWith(page.Summary, "3 points attendent une décision.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Each_kind_of_problem_has_its_section_like_in_0x()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        page.Show(Full(), Backups);

        CollectionAssert.AreEqual(AllSections, page.Sections.Select(section => section.Title).ToArray());
        var missing = page.Sections[0].Rows.Single();
        Assert.AreEqual("Mitose", missing.Title);
        Assert.AreEqual("fiches/Mitose.md", missing.Detail);
        Assert.AreEqual("Restaurer", missing.ActionLabel);
        Assert.IsTrue(missing.HasRepair);
        Assert.IsFalse(missing.HasOpening);
        Assert.AreEqual("sauvegarde-1.zip", missing.SelectedBackup?.Name);
        Assert.AreEqual("12 oct. 2026, 08:30", missing.Backups[0].ToString());
        Assert.AreEqual("Restaurer : Mitose", missing.ActionName);
        Assert.AreEqual("Mitose : fiches/Mitose.md", missing.Spoken);
        Assert.AreEqual(missing.Spoken, missing.ToString());
        var duplicate = page.Sections[1].Rows[1];
        Assert.AreEqual("copie/Rome.md", duplicate.Detail);
        Assert.AreEqual("Garder comme original", duplicate.ActionLabel);
        var untracked = page.Sections[2].Rows[0];
        Assert.AreEqual("Nouveau.md", untracked.Title);
        Assert.AreEqual("Suivre cette fiche (J0)", untracked.ActionLabel);
        var lost = page.Sections[2].Rows[1];
        Assert.AreEqual("fiches/Perdu.md · identifiant présent, suivi perdu", lost.Detail);
        Assert.AreEqual("Recréer son suivi (J0)", lost.ActionLabel);
        var link = page.Sections[3].Rows.Single();
        Assert.AreEqual("→ images/absente.png", link.Detail);
        Assert.IsTrue(link.HasOpening);
        Assert.IsFalse(link.HasRepair);
        Assert.AreEqual("« La prophase »", page.Sections[4].Rows[0].Title);
        Assert.AreEqual("Mitose · position à vérifier", page.Sections[4].Rows[0].Detail);
        Assert.AreEqual("Mitose · introuvable", page.Sections[4].Rows[1].Detail);
        Assert.IsNull(page.Sections[5].Rows.Single().Action);
        Assert.AreEqual("illisible", page.Sections[6].Rows[0].Detail);
        Assert.AreEqual("liens à vérifier après un renommage", page.Sections[6].Rows[1].Detail);
        var information = page.Sections[7];
        Assert.IsTrue(information.IsInformation);
        Assert.IsFalse(information.HasHelp);
        Assert.AreEqual("déplacée hors de kyfRev, rattachée automatiquement : fiches/Là.md", information.Rows[0].Detail);
        Assert.AreEqual("modifiée hors de kyfRev depuis la dernière ouverture", information.Rows[1].Detail);
    }

    [TestMethod]
    public void Without_a_backup_a_missing_sheet_says_so()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        page.Show(Full(), []);

        var missing = page.Sections[0].Rows.Single();
        Assert.IsTrue(missing.HasHint);
        Assert.AreEqual("Aucune sauvegarde disponible.", missing.Hint);
        Assert.IsFalse(missing.HasBackups);
        Assert.IsFalse(missing.HasRepair);
        Assert.IsNull(missing.SelectedBackup);
    }

    [TestMethod]
    public void Links_and_passages_open_the_sheet()
    {
        var opener = new FakeOpener();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, opener);
        page.Show(Full(), Backups);

        page.Sections[3].Rows.Single().Action!.Execute(null);
        Assert.AreEqual("f_1", opener.Opened.Single());

        page.Sections[4].Rows[0].Action!.Execute(null);
        Assert.AreEqual(("f_1", "a_1"), opener.Opened[1]);
    }

    [TestMethod]
    public async Task A_refused_repair_says_why_and_keeps_the_report()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(Full(), Backups);
        var sections = page.Sections;

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)page.Sections[1].Rows[0].Action!).ExecuteAsync(null);

        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsTrue(page.HasError);
        Assert.IsFalse(page.HasMessage);
        Assert.AreSame(sections, page.Sections);
        Assert.IsTrue(page.IsIdle);
    }

    [TestMethod]
    public async Task Reading_without_a_vault_says_why()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());

        await page.RefreshCommand.ExecuteAsync(null);
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsFalse(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);

        await page.AnalyzeCommand.ExecuteAsync(null);
        Assert.AreEqual(VaultHost.NoVaultOpen, page.Error);
        Assert.IsFalse(page.IsBusy);
    }

    [TestMethod]
    public async Task Repairs_work_on_a_real_vault()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var root = vaults.PathOf("Coffre");
        Assert.IsNotNull(vaults.App.Vaults.Create(root));
        var app = vaults.App;
        var id = app.Sheets.Create(null, "Rome", "Texte.\n").Value;
        var path = app.Sheets.Open(id).Value.Path;
        File.Copy(Path.Combine(root, path), Path.Combine(root, "fiches", "Copie.md"));
        await File.WriteAllTextAsync(Path.Combine(root, "fiches", "Libre.md"), "# Libre\n\nTexte.\n");
        var page = Page(vaults, new FakeOpener());

        await page.AnalyzeCommand.ExecuteAsync(null);
        Assert.IsTrue(page.HasProblems, page.Summary);
        var track = page.Sections.Single(section => section.Title.StartsWith("Fichiers non suivis", StringComparison.Ordinal)).Rows.Single();
        Assert.AreEqual("Libre.md", track.Title);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)track.Action!).ExecuteAsync(null);
        Assert.AreEqual("« Libre.md » est suivie : J1 demain.", page.Message);
        Assert.IsTrue(page.HasMessage);

        var keep = page.Sections.Single(section => section.Title.StartsWith("Fichiers en double", StringComparison.Ordinal)).Rows[0];
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)keep.Action!).ExecuteAsync(null);
        StringAssert.Contains(page.Message!, "garde le suivi", StringComparison.Ordinal);
        Assert.IsTrue(page.IsHealthy, page.Summary);
        Assert.IsTrue(page.Sections.All(section => section.IsInformation), "plus rien n'attend de décision");
        Assert.IsTrue(page.HasSections);
    }

    [TestMethod]
    public void The_file_name_comes_after_the_last_slash()
    {
        Assert.AreEqual("Mitose.md", HealthPageViewModel.FileName("fiches/Biologie/Mitose.md"));
        Assert.AreEqual("Mitose.md", HealthPageViewModel.FileName("Mitose.md"));
    }

    [TestMethod]
    public void The_diagnostic_is_copied_and_can_be_read_first()
    {
        string? copied = null;
        using var vaults = new TestVaults(AppMode.Development);
        var page = new HealthPageViewModel(vaults.App.Maintenance, vaults.App.Backups, vaults.App.Sheets, new FakeOpener(), () => "Le texte", text => copied = text);

        Assert.AreEqual("Afficher", page.DiagnosticToggle);
        page.ToggleDiagnosticCommand.Execute(null);
        Assert.IsTrue(page.IsDiagnosticShown);
        Assert.AreEqual("Le texte", page.DiagnosticText);
        Assert.AreEqual("Masquer", page.DiagnosticToggle);
        page.ToggleDiagnosticCommand.Execute(null);
        Assert.IsFalse(page.IsDiagnosticShown);

        page.CopyDiagnosticCommand.Execute(null);

        Assert.AreEqual("Le texte", copied);
        Assert.AreEqual("Diagnostic copié : collez-le dans votre message.", page.DiagnosticMessage);
        Assert.IsTrue(page.HasDiagnosticMessage);
        Assert.IsFalse(page.IsDiagnosticShown);
    }

    [TestMethod]
    public void A_refused_copy_shows_the_text_instead()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = new HealthPageViewModel(vaults.App.Maintenance, vaults.App.Backups, vaults.App.Sheets, new FakeOpener(), () => "Le texte", _ => throw new Win32Exception("Presse-papiers occupé"));

        page.CopyDiagnosticCommand.Execute(null);

        Assert.IsTrue(page.IsDiagnosticShown);
        Assert.AreEqual("Le texte", page.DiagnosticText);
        StringAssert.StartsWith(page.DiagnosticMessage!, "La copie a échoué", StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_diagnostic_says_what_a_report_needs()
    {
        var text = DiagnosticReport.Write("1.0.0-alpha.1", "installée", "156.0", vaultOpen: true, new DateTimeOffset(2026, 11, 3, 8, 30, 0, TimeSpan.Zero), "ligne\n");

        StringAssert.StartsWith(text, "kyfRev 1.0.0-alpha.1\nVersion : installée\nSystème : ", StringComparison.Ordinal);
        StringAssert.Contains(text, "WebView2 : 156.0\nCoffre ouvert : oui\nCopié le : 2026-11-03 08:30:00\n", StringComparison.Ordinal);
        StringAssert.EndsWith(text, "--- Fin du journal (300 lignes au plus) ---\nligne\n", StringComparison.Ordinal);
        StringAssert.Contains(DiagnosticReport.Write("1", "portable", "absent", vaultOpen: false, DateTimeOffset.UnixEpoch, string.Empty), "Coffre ouvert : non", StringComparison.Ordinal);
        Assert.ThrowsExactly<ArgumentNullException>(() => DiagnosticReport.Write("1", "portable", "absent", false, DateTimeOffset.UnixEpoch, null!));
    }

    [STATestMethod]
    public void The_page_shows_its_sections_without_horizontal_scrolling()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults, new FakeOpener());
        page.Show(Full(), Backups);
        var test = new TestShell();
        var shell = test.Shell;
        shell.Navigate(new NavigationItem(CorePages.Health, "Santé du coffre", Icons.Health, int.MaxValue, page));
        var window = new ShellWindow(shell) { ShowActivated = true, Left = -10000, Top = -10000, Width = 800, Height = 800 };
        window.Resources.MergedDictionaries.Add(test.Design.Dictionary);
        window.Show();
        TestShell.Flush();
        try
        {
            Assert.AreEqual("Santé du coffre", Named<TextBlock>(window, "Heading").Text);
            var scroller = Named<ScrollViewer>(window, "Scroller");
            Assert.IsLessThanOrEqualTo(scroller.ViewportWidth, scroller.ExtentWidth);
            Assert.AreEqual(Visibility.Collapsed, Named<FrameworkElement>(window, "DiagnosticBox").Visibility);
            page.ToggleDiagnosticCommand.Execute(null);
            TestShell.Flush();
            Assert.AreEqual(Visibility.Visible, Named<FrameworkElement>(window, "DiagnosticBox").Visibility);
        }
        finally
        {
            window.Close();
        }
    }

    private static HealthPageViewModel Page(TestVaults vaults, IReviewOpener opener) =>
        new(vaults.App.Maintenance, vaults.App.Backups, vaults.App.Sheets, opener, () => "Le diagnostic", _ => { });

    private static HealthDetails Report(int problems = 0) => new([], [], [], [], [], [], [], [], [], [], [], problems);

    private static HealthDetails Full() => new(
        [new MovedSheet("Partie", "fiches/Ici.md", "fiches/Là.md")],
        [new ConcernedSheet("f_5", "Changée", "fiches/Changée.md")],
        [new ConcernedSheet("f_2", "Mitose", "fiches/Mitose.md")],
        [new DuplicateDetails("f_3", "Rome", ["fiches/Rome.md", "copie/Rome.md"])],
        ["fiches/Nouveau.md"],
        [new ConcernedSheet("f_4", "Perdu", "fiches/Perdu.md")],
        [new UnreadableFile("fiches/Abîmé.md", "illisible")],
        ["fiches/Renommé.md"],
        [new BrokenLink("f_1", "Mitose", "images/absente.png")],
        [new ConcernedPassage("a_3", "f_9", "Disparue", "La fin", AnchorState.NotFound)],
        [new ConcernedPassage("a_1", "f_1", "Mitose", "La prophase", AnchorState.NeedsReview), new ConcernedPassage("a_2", "f_1", "Mitose", "La métaphase", AnchorState.NotFound)],
        9);

    private static T Named<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        var found = Find<T>(root, name);
        Assert.IsNotNull(found, name);
        return found;
    }

    private static T? Find<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }
            if (Find<T>(child, name) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }
}
