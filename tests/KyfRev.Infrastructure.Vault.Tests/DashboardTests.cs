using System.Diagnostics;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Tableau de bord sur un grand coffre : le critère de J6 (affiché en moins de 300 ms sur un coffre
/// de 2 000 fiches) laisse l'essentiel du temps à l'affichage ; le calcul doit en prendre peu.
/// La comparaison avec la 0.x est faite par le scénario 13.
/// </summary>
[TestClass]
public sealed class DashboardTests
{
    private const int Sheets = 2000;

    [TestMethod]
    public void The_dashboard_of_2000_sheets_takes_a_fraction_of_the_display_budget()
    {
        using var folder = TemporaryVault.Empty();
        var start = new Moment(new DateOnly(2026, 9, 1), new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));
        using (var vault = VaultSession.Initialize(folder.Root, start))
        {
            for (var n = 0; n < Sheets; n++)
            {
                vault.CreateSheet($"Matière {n % 20}", $"Fiche {n}", $"# Fiche {n}\n\nUn lien vers [la suivante](Fiche {n + 1}.md).\n", start);
            }
        }
        var day = new DateOnly(2026, 9, 20);
        using var reopened = VaultSession.Open(folder.Root, new Moment(day, new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero)));
        reopened.Dashboard(day);

        var clock = Stopwatch.StartNew();
        var dashboard = reopened.Dashboard(day);
        clock.Stop();

        Assert.AreEqual(Sheets, dashboard.ActiveSheets);
        Assert.HasCount(Sheets, dashboard.Overdue);
        Assert.IsLessThan(TimeSpan.FromMilliseconds(150), clock.Elapsed, $"Calcul du tableau de bord : {clock.Elapsed.TotalMilliseconds:0} ms");
    }

    [TestMethod]
    public void Broken_links_follow_the_files_changed_outside_the_application()
    {
        using var folder = TemporaryVault.Empty();
        var now = new Moment(new DateOnly(2026, 9, 1), new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));
        using var vault = VaultSession.Initialize(folder.Root, now);
        var id = vault.CreateSheet("Biologie", "Mitose", "Voir [la méiose](Méiose.md) et [le site](https://exemple.org).\n", now);
        Assert.AreEqual("Méiose.md", vault.BrokenLinks().Single().Target);

        File.WriteAllText(folder.PathOf("fiches/Biologie/Méiose.md"), "La méiose.\n");
        Assert.IsEmpty(vault.BrokenLinks());

        var sheet = folder.PathOf("fiches/Biologie/Mitose.md");
        File.AppendAllText(sheet, "Puis [la cytokinèse](Cytokinèse.md#fin).\n");
        Assert.AreEqual("Cytokinèse.md", vault.BrokenLinks().Single().Target);
        Assert.AreEqual(id, vault.BrokenLinks().Single().SheetId);

        vault.Analyze(now);
        Assert.AreEqual("Cytokinèse.md", vault.BrokenLinks().Single().Target);
    }

    [TestMethod]
    public void A_link_through_a_directory_link_is_checked_path_by_path()
    {
        using var outside = TemporaryVault.Empty();
        Directory.CreateDirectory(outside.PathOf("Externe"));
        File.WriteAllText(outside.PathOf("Externe/Dehors.md"), "Hors du coffre.\n");
        using var folder = TemporaryVault.Empty();
        var now = new Moment(new DateOnly(2026, 9, 1), new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));
        using var vault = VaultSession.Initialize(folder.Root, now);
        vault.CreateSheet("Biologie", "Cellule", "La cellule.\n", now);
        string[] links = [folder.PathOf("fiches/Raccourci"), folder.PathOf("fiches/Sortie")];
        FileLinks.CreateDirectoryLink(links[0], folder.PathOf("fiches/Biologie"));
        FileLinks.CreateDirectoryLink(links[1], outside.PathOf("Externe"));
        try
        {
            vault.CreateSheet("Notes", "Index", "[Cellule](../Raccourci/Cellule.md) et [dehors](../Sortie/Dehors.md).\n", now);

            Assert.AreEqual("../Sortie/Dehors.md", vault.BrokenLinks().Single().Target);
        }
        finally
        {
            // Les liens seuls sont retirés, jamais ce qu'ils désignent.
            foreach (var link in links)
            {
                Directory.Delete(link);
            }
        }
    }
}
