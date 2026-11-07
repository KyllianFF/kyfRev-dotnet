using System.IO;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Presentation.Wpf.Pages;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>La vue « Santé du coffre » quand le coffre se verrouille : elle oublie le rapport, ses messages et le diagnostic, et relit ensuite.</summary>
[TestClass]
public sealed class HealthForgetTests
{
    [TestMethod]
    public async Task Forgetting_drops_the_report_the_messages_and_the_diagnostic_until_the_next_reading()
    {
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);
        await page.AnalyzeCommand.ExecuteAsync(null);
        Loaded(page);
        await ((IAsyncRelayCommand)Untracked(page).Action!).ExecuteAsync(null);
        page.ToggleDiagnosticCommand.Execute(null);
        page.CopyDiagnosticCommand.Execute(null);
        // La vue tient ce qu'elle a lu : la réparation faite et le diagnostic affiché.
        Assert.AreEqual("« Libre.md » est suivie : J1 demain.", page.Message);
        Assert.IsTrue(page.IsDiagnosticShown);
        Assert.AreEqual("Le diagnostic", page.DiagnosticText);
        Assert.IsTrue(page.HasDiagnosticMessage);

        page.Forget();

        Forgotten(page);

        await page.RefreshCommand.ExecuteAsync(null);
        Loaded(page);
    }

    [STATestMethod]
    public void A_reading_or_a_repair_started_before_forgetting_puts_nothing_back()
    {
        using var ui = new UiThread();
        using var vaults = new TestVaults(AppMode.Development);
        var page = Page(vaults);

        // La suite de l'analyse revient au fil de l'interface : elle n'y passe qu'une fois l'oubli fait.
        var reading = page.AnalyzeCommand.ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => reading.IsCompleted);
        Forgotten(page);

        // Une réparation partie avant l'oubli ne dit rien de ce qu'elle a fait, et ne remet pas le rapport.
        var analysis = page.AnalyzeCommand.ExecuteAsync(null);
        UiThread.Until(() => analysis.IsCompleted);
        Loaded(page);
        var repair = ((IAsyncRelayCommand)Untracked(page).Action!).ExecuteAsync(null);
        page.Forget();
        UiThread.Until(() => repair.IsCompleted);
        Forgotten(page);

        page.OnNavigatedTo();
        UiThread.Until(() => page.IsLoaded && !page.IsLoading);
        Loaded(page);
    }

    /// <summary>La page, sur un coffre ouvert où « Mitose » est en double et « Libre.md » n'est pas suivie.</summary>
    private static HealthPageViewModel Page(TestVaults vaults)
    {
        var root = vaults.PathOf("Coffre");
        Assert.IsNotNull(vaults.App.Vaults.Create(root).Vault);
        var mitosis = vaults.App.Sheets.Create(null, "Mitose", "Texte.\n").Value;
        var path = vaults.App.Sheets.Open(mitosis).Value.Path;
        File.Copy(Path.Combine(root, path), Path.Combine(root, "fiches", "Copie.md"));
        File.WriteAllText(Path.Combine(root, "fiches", "Libre.md"), "# Libre\n\nTexte.\n");
        return new HealthPageViewModel(vaults.App.Maintenance, vaults.App.Backups, vaults.App.Sheets, new FakeOpener(), () => "Le diagnostic", _ => { });
    }

    /// <summary>La ligne « Libre.md », à suivre.</summary>
    private static HealthRow Untracked(HealthPageViewModel page) =>
        page.Sections.Single(section => section.Title.StartsWith("Fichiers non suivis", StringComparison.Ordinal)).Rows.Single(row => row.Title == "Libre.md");

    /// <summary>Les titres de toutes les lignes du rapport.</summary>
    private static List<string> Titles(HealthPageViewModel page) => [.. page.Sections.SelectMany(section => section.Rows).Select(row => row.Title)];

    /// <summary>Le rapport lu dans le coffre : « Mitose » y est en double.</summary>
    private static void Loaded(HealthPageViewModel page)
    {
        Assert.IsNull(page.Error);
        Assert.IsTrue(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);
        Assert.IsTrue(page.HasProblems, page.Summary);
        Assert.IsTrue(page.HasSections);
        Assert.Contains("Mitose", Titles(page));
    }

    /// <summary>Rien de ce qui a été lu dans le coffre ne reste : la page est comme à sa construction.</summary>
    private static void Forgotten(HealthPageViewModel page)
    {
        Assert.IsFalse(page.IsLoaded);
        Assert.IsFalse(page.IsLoading);
        Assert.IsTrue(page.IsIdle);
        Assert.IsFalse(page.IsHealthy);
        Assert.IsFalse(page.HasProblems);
        Assert.AreEqual(string.Empty, page.Summary);
        Assert.IsEmpty(page.Sections);
        Assert.IsFalse(page.HasSections);
        Assert.IsNull(page.Error);
        Assert.IsFalse(page.HasError);
        Assert.IsNull(page.Message);
        Assert.IsFalse(page.HasMessage);
        Assert.IsFalse(page.IsDiagnosticShown);
        Assert.AreEqual(string.Empty, page.DiagnosticText);
        Assert.IsNull(page.DiagnosticMessage);
        Assert.IsFalse(page.HasDiagnosticMessage);
    }
}
