using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Links;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>Les rétroliens et l'aperçu d'une fiche liée, repris des essais de la 0.x (<c>tests/retroliens.rs</c>).</summary>
public abstract class BacklinksTests(StorageKind storage)
{
    private static readonly DateOnly Day = new(2026, 10, 1);
    private static readonly Moment Now = new(Day, new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private static readonly Moment Tomorrow = new(Day.AddDays(1), new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void A_sheet_is_led_to_by_those_that_link_to_it()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var mitose = vault.CreateSheet("Bio", "Mitose", "## Phases\n\nLa **prophase** commence.\n", Now);
        // Deux liens vers Mitose, dont un dans du code (ignoré) ; un lien vers l'extérieur.
        var revolution = vault.CreateSheet(
            "Histoire",
            "Révolution",
            "Intro.\n\nVoir [la mitose](<../Bio/Mitose.md>) pour la *division*, et [Wikipédia](https://fr.wikipedia.org).\n\n```\n[pas un lien](../Bio/Mitose.md)\n```\n\nEncore [Mitose](../Bio/Mitose.md).\n",
            Tomorrow);
        vault.CreateSheet(null, "Autre", "Rien à voir.\n", Tomorrow);

        var backlink = vault.Backlinks(mitose, CancellationToken.None).Single();

        Assert.AreEqual((revolution, "Révolution", "Histoire", false, 2), (backlink.SheetId, backlink.Title, backlink.Folder, backlink.Archived, backlink.Links));
        CollectionAssert.AreEqual(
            new[] { new ExcerptPart("Voir ", false), new ExcerptPart("la mitose", true), new ExcerptPart(" pour la division, et Wikipédia.", false) },
            backlink.Excerpt.ToArray());
        Assert.IsEmpty(vault.Backlinks(revolution, CancellationToken.None), "dans l'autre sens, rien");
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.Backlinks("f_inconnue", CancellationToken.None)).Kind);
    }

    [TestMethod]
    public void The_sheets_that_lead_here_are_by_title_and_say_when_they_are_archived()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", Now);
        var zebre = vault.CreateSheet(null, "zèbre", "[c](Cible.md)\n", Now);
        var abeille = vault.CreateSheet(null, "Abeille", "[c](Cible.md#partie) et [c](Cible.md?a=1)\n", Now);
        var racine = vault.CreateSheet("Bio/Cellule", "Noyau", "[c](../../Cible.md)\n", Now);
        Assert.IsTrue(vault.ApplyToTracking(zebre, tracking => tracking.ArchiveManually(Now, vault.Settings.Reminders)).Archive is not null);

        var backlinks = vault.Backlinks(target, CancellationToken.None);

        Assert.AreEqual($"{abeille}|{racine}|{zebre}", string.Join('|', backlinks.Select(backlink => backlink.SheetId)));
        Assert.AreEqual(2, backlinks[0].Links, "une ancre ou des paramètres mènent à la même fiche");
        Assert.AreEqual("Bio/Cellule", backlinks[1].Folder);
        Assert.AreEqual(string.Empty, backlinks[0].Folder);
        Assert.IsTrue(backlinks[2].Archived);
    }

    [TestMethod]
    public void A_sheet_that_only_mentions_the_name_or_links_outside_does_not_lead_here()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet("Bio", "Mitose", "Texte.\n", Now);
        vault.CreateSheet("Bio", "Voisine", "[homonyme](../Mitose.md) [dehors](../../../Mitose.md) [web](https://exemple.org/Mitose.md) `[code](Mitose.md)`\n", Now);

        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None));
    }

    [TestMethod]
    public void The_list_follows_edits_renames_and_moves()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet("Bio", "Mitose", "Texte.\n", Now);
        var source = vault.CreateSheet("Bio", "Source", "Rien encore.\n", Now);
        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None));

        var read = vault.ReadSheet(source);
        Assert.IsInstanceOfType<SheetSaved>(vault.SaveBody(source, "Voir [ça](Mitose.md).\n", read.Fingerprint, 1, Tomorrow));
        Assert.AreEqual("Voir ", vault.Backlinks(target, CancellationToken.None).Single().Excerpt[0].Text);

        vault.ChangeTitle(target, "Division", Tomorrow);
        Assert.AreEqual(source, vault.Backlinks(target, CancellationToken.None).Single().SheetId, "renommée, ses liens la suivent");

        vault.MoveSheet(source, "Histoire", Tomorrow);
        var moved = vault.Backlinks(target, CancellationToken.None).Single();
        Assert.AreEqual("Histoire", moved.Folder);

        read = vault.ReadSheet(source);
        Assert.IsInstanceOfType<SheetSaved>(vault.SaveBody(source, "Plus de lien.\n", read.Fingerprint, 2, Tomorrow));
        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None));
    }

    [TestMethod]
    public void A_changed_file_is_seen_and_one_that_disappeared_is_left_out()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", Now);
        var source = vault.CreateSheet(null, "Source", "[c](Cible.md)\n", Now);
        Assert.HasCount(1, vault.Backlinks(target, CancellationToken.None));

        folder.Write("fiches/Source.md", folder.ReadText("fiches/Source.md").Replace("[c](Cible.md)", "Sans lien, et plus long.", StringComparison.Ordinal));
        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None), "un changement fait hors de l'application se voit");

        folder.Write("fiches/Source.md", folder.ReadText("fiches/Source.md").Replace("Sans lien, et plus long.", "[c](Cible.md)", StringComparison.Ordinal));
        Assert.AreEqual(source, vault.Backlinks(target, CancellationToken.None).Single().SheetId);

        folder.Delete("fiches/Source.md");
        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None));
        vault.Analyze(Now);
        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None), "une fiche manquante ne mène nulle part");
    }

    [TestMethod]
    public void Searching_and_previewing_write_nothing_in_the_vault()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", Now);
        vault.CreateSheet(null, "Source", "[c](Cible.md)\n", Now);
        var before = OnDisk(folder);

        vault.Backlinks(target, CancellationToken.None);
        vault.PreviewAt("fiches/Cible.md", Day);

        CollectionAssert.AreEqual(before, OnDisk(folder));
    }

    [TestMethod]
    public void A_sheet_that_cannot_be_read_is_left_out()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", Now);
        vault.CreateSheet(null, "Source", "[c](Cible.md)\n", Now);
        Assert.HasCount(1, vault.Backlinks(target, CancellationToken.None), "ses liens sont maintenant relevés");

        using var held = folder.Hold("fiches/Source.md");

        Assert.IsEmpty(vault.Backlinks(target, CancellationToken.None));
    }

    [TestMethod]
    public void A_newer_request_stops_the_search()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", Now);
        vault.CreateSheet(null, "Source", "[c](Cible.md)\n", Now);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => vault.Backlinks(target, cancelled.Token));
    }

    [TestMethod]
    public void A_preview_gives_the_start_of_the_sheet_and_its_step()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var mitose = vault.CreateSheet("Bio", "Mitose", "## Phases\n\n- La **prophase** commence.\n- Puis la `métaphase`.\n\n> Voir [Méiose](Meiose.md).\n", Now);

        var preview = vault.PreviewAt("fiches/Bio/Mitose.md", Day)!;

        Assert.AreEqual((mitose, "Mitose", "Bio", false), (preview.Id, preview.Title, preview.Folder, preview.Archived));
        Assert.AreEqual("Phases La prophase commence. Puis la métaphase. Voir Méiose.", preview.Excerpt);
        Assert.AreEqual(("J1", (DateOnly?)Day.AddDays(1)), (preview.Step, preview.DueDate));
        Assert.IsNull(vault.PreviewAt("fiches/Bio/Inconnue.md", Day));
    }

    [TestMethod]
    public void A_long_sheet_is_cut_after_a_whole_word_and_an_archived_one_has_no_step()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var longue = vault.CreateSheet(null, "Longue", string.Concat(Enumerable.Repeat("mot ", 200)), Now);
        vault.ApplyToTracking(longue, tracking => tracking.ArchiveManually(Now, vault.Settings.Reminders));

        var preview = vault.PreviewAt("fiches/Longue.md", Day)!;

        Assert.EndsWith("mot…", preview.Excerpt);
        Assert.IsLessThanOrEqualTo(321, preview.Excerpt.EnumerateRunes().Count());
        Assert.IsTrue(preview.Archived);
        Assert.IsNull(preview.Step);
        Assert.IsNull(preview.DueDate);
    }

    [TestMethod]
    public void A_sheet_whose_file_is_gone_still_has_its_preview_without_text()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        vault.CreateSheet(null, "Partie", "Texte.\n", Now);
        folder.Delete("fiches/Partie.md");
        vault.Analyze(Now);

        var preview = vault.PreviewAt("fiches/Partie.md", Day)!;

        Assert.AreEqual(("Partie", string.Empty), (preview.Title, preview.Excerpt));
    }

    /// <summary>Tous les fichiers posés sur le disque, avec leur date et leur taille ; le verrou, tenu par la session, n'en dit rien.</summary>
    private static List<(string File, DateTime Modified, long Length)> OnDisk(TemporaryVault folder) =>
        [.. Directory.EnumerateFiles(folder.Root, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "verrou")
            .Order(StringComparer.Ordinal)
            .Select(file => (file, File.GetLastWriteTimeUtc(file), new FileInfo(file).Length))];
}

/// <summary>Sur le disque.</summary>
[TestClass]
public sealed class BacklinksOnDiskTests() : BacklinksTests(StorageKind.Disk)
{
    [TestMethod]
    public void Only_the_sheets_that_lead_here_are_read_again()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Disk);
        using var vault = folder.Initialize(new Moment(new DateOnly(2026, 10, 1), new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)));
        var moment = new Moment(new DateOnly(2026, 10, 1), new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var target = vault.CreateSheet(null, "Cible", "Texte.\n", moment);
        vault.CreateSheet(null, "Source", "[c](Cible.md)\n", moment);
        vault.CreateSheet(null, "Sans lien", "Rien.\n", moment);
        Assert.HasCount(1, vault.Backlinks(target, CancellationToken.None), "ce passage relève les liens");

        // Une fiche qui ne mène pas ici peut devenir illisible sans que la recherche la rouvre : ses liens sont déjà relevés.
        var stamp = File.GetLastWriteTimeUtc(folder.PathOf("fiches/Sans lien.md"));
        var length = new FileInfo(folder.PathOf("fiches/Sans lien.md")).Length;
        File.WriteAllBytes(folder.PathOf("fiches/Sans lien.md"), new byte[length]);
        File.SetLastWriteTimeUtc(folder.PathOf("fiches/Sans lien.md"), stamp);

        Assert.HasCount(1, vault.Backlinks(target, CancellationToken.None));
    }
}

/// <summary>Sur un coffre chiffré.</summary>
[TestClass]
public sealed class BacklinksEncryptedTests() : BacklinksTests(StorageKind.Encrypted);

/// <summary>Les extraits, repris des essais de la 0.x (<c>retroliens.rs</c>).</summary>
[TestClass]
public sealed class SheetExcerptsTests
{
    [TestMethod]
    [DataRow("Voir [Mitose](<../Bio/Mitose.md>) et ![img](a.png).", "Voir Mitose et img.")]
    [DataRow("un [crochet seul", "un [crochet seul")]
    [DataRow("[sans fin](destination", "[sans fin](destination")]
    public void Links_give_way_to_their_text(string text, string expected) => Assert.AreEqual(expected, SheetExcerpts.WithoutLinks(text));

    [TestMethod]
    [DataRow("Un texte court", 20, "Un texte court")]
    [DataRow("Un texte bien trop long pour tenir", 12, "Un texte…")]
    [DataRow("Anticonstitutionnellement", 5, "Antic…")]
    [DataRow("😀😀😀 😀😀😀", 4, "😀😀😀…")]
    public void A_text_is_cut_after_a_whole_word(string text, int count, string expected) => Assert.AreEqual(expected, SheetExcerpts.Shorten(text, count));

    [TestMethod]
    public void A_sheet_loses_its_formatting()
    {
        const string body = "\n# Mitose\n\n## Phases\n\n- La **prophase** commence.\n- Puis la `métaphase`.\n\n```\ncode\n```\n\n> Note : voir [Méiose](Meiose.md).\n";

        Assert.AreEqual("Phases La prophase commence. Puis la métaphase. Note : voir Méiose.", SheetExcerpts.PlainText(body, "Mitose"));
    }

    [TestMethod]
    public void Lists_tables_and_rules_are_read_like_the_0x()
    {
        const string body = "1. Premier\n12. Deuxième\n+ Troisième\n* Quatrième\n| a | b |\n---\n~~~\nblabla\n~~~\nA. Pas un numéro\n";

        Assert.AreEqual("Premier Deuxième Troisième Quatrième A. Pas un numéro", SheetExcerpts.PlainText(body, "Titre"));
        Assert.AreEqual("Un Deux", SheetExcerpts.PlainText("Un\r\nDeux\r\n", "Titre"));
    }

    [TestMethod]
    public void The_excerpt_of_a_link_is_the_text_around_it()
    {
        const string text = "Intro.\nVoir aussi [la mitose](<../Bio/Mitose.md>) pour le détail, et *après*.\nFin.";

        Assert.AreEqual("Voir aussi |[la mitose]| pour le détail, et après.", Shown(SheetExcerpts.AroundLink(text, MarkdownLinks.Find(text)[0])));
    }

    [TestMethod]
    public void The_text_around_a_link_is_cut_on_both_sides()
    {
        var before = string.Concat(Enumerable.Repeat("mot ", 40));
        var after = string.Concat(Enumerable.Repeat(" fin", 40));
        var text = $"{before}[lien](a.md){after}";

        var parts = SheetExcerpts.AroundLink(text, MarkdownLinks.Find(text)[0]);

        Assert.HasCount(3, parts);
        Assert.StartsWith("…", parts[0].Text);
        Assert.IsTrue(parts[1].IsLink);
        Assert.EndsWith("…", parts[2].Text);
    }

    [TestMethod]
    public void A_reference_or_a_link_alone_has_nothing_around_it()
    {
        const string reference = "[ref]: Cible.md\n";
        Assert.AreEqual("[ref]: |[Cible.md]", Shown(SheetExcerpts.AroundLink(reference, MarkdownLinks.Find(reference)[0])));

        const string alone = "[seul](Cible.md)";
        Assert.AreEqual("[seul]", Shown(SheetExcerpts.AroundLink(alone, MarkdownLinks.Find(alone)[0])));

        const string spaced = "voir [ceci](  Cible.md) ok";
        Assert.AreEqual("voir [ceci]( |[Cible.md]|) ok", Shown(SheetExcerpts.AroundLink(spaced, MarkdownLinks.Find(spaced)[0])));
    }

    private static string Shown(IEnumerable<ExcerptPart> parts) =>
        string.Join('|', parts.Select(part => part.IsLink ? $"[{part.Text}]" : part.Text));
}
