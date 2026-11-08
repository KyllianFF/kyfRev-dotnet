using System.Collections.Immutable;
using System.Security.Cryptography;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// La recherche dans le coffre, comme <c>crates/coffre/tests/recherche.rs</c> de la 0.x : en clair comme chiffré, elle se
/// lit par le stockage du coffre, et n'écrit rien.
/// </summary>
public abstract class VaultSearchTests(StorageKind storage)
{
    private const string Mitosis = "Division d'une cellule en deux cellules filles identiques. Le cœur du processus est la réplication de l'ADN.\n";

    private static readonly Moment Now = new(new DateOnly(2026, 10, 12), new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void The_text_is_searched_without_case_or_accents()
    {
        using var sample = new Sample(storage);

        var found = sample.Sheets("CLERGE");

        Assert.AreEqual(sample.Revolution, found.Single().Id);
        Assert.AreEqual(
            "… Régime repose sur trois ordres : le [clergé], la noblesse et le tiers état. La Dette atteint la moitié des dépenses.",
            Read(found[0].Excerpt));

        // Tous les mots doivent y être, où qu'ils soient (titre ou texte).
        Assert.AreEqual(sample.Revolution, sample.Sheets("révolution dette").Single().Id);
        Assert.IsEmpty(sample.Sheets("révolution cellule"));

        // Ligature et apostrophe typographique.
        Assert.AreEqual(sample.Mitosis, sample.Sheets("coeur adn").Single().Id);
        Assert.AreEqual(sample.State, sample.Sheets("l'etat").Single().Id);
    }

    [TestMethod]
    public void A_hit_says_where_the_sheet_is_and_if_it_is_archived()
    {
        using var sample = new Sample(storage);

        var hit = sample.Sheets("clerge").Single();
        Assert.AreEqual("Révolution française", hit.Title);
        Assert.AreEqual("Histoire", hit.Folder);
        Assert.IsFalse(hit.Archived);

        Assert.AreEqual(string.Empty, sample.Sheets("souverainete").Single().Folder, "à la racine");
    }

    [TestMethod]
    public void Without_a_word_every_sheet_comes_by_its_identifier()
    {
        using var sample = new Sample(storage);

        var found = sample.Sheets("  ").Select(hit => hit.Id).ToList();

        Assert.HasCount(3, found);
        CollectionAssert.AreEqual(found.Order(StringComparer.Ordinal).ToList(), found);
    }

    [TestMethod]
    public void The_title_and_the_tags_need_no_excerpt()
    {
        using var sample = new Sample(storage);

        var byTitle = sample.Sheets("mitose").Single();
        Assert.AreEqual(sample.Mitosis, byTitle.Id);
        Assert.IsTrue(byTitle.Excerpt.IsEmpty);
        Assert.AreEqual(sample.Mitosis, sample.Sheets("examen").Single().Id);

        // Un mot du titre et un mot du texte : l'extrait montre celui du texte, avec tous les mots repérés.
        Assert.AreEqual(
            "… d'une cellule en deux cellules [filles] identiques. Le cœur du processus est la réplication de l'ADN.",
            Read(sample.Sheets("mitose filles")[0].Excerpt));
    }

    [TestMethod]
    public void The_technical_header_is_not_searched()
    {
        using var sample = new Sample(storage);

        Assert.IsEmpty(sample.Sheets("creee"), "« creee: 2026-10-12 » est dans l'en-tête");
        Assert.IsEmpty(sample.Sheets(sample.Mitosis), "l'identifiant aussi");
    }

    [TestMethod]
    public void A_missing_sheet_is_searched_by_its_title()
    {
        using var sample = new Sample(storage);
        sample.Folder.Delete("fiches/Bio/Mitose.md");
        sample.Vault.Analyze(Now);

        Assert.AreEqual(sample.Mitosis, sample.Sheets("mitose").Single().Id);
        Assert.IsEmpty(sample.Sheets("cellules"));
    }

    [TestMethod]
    public void A_sheet_that_cannot_be_decoded_is_searched_by_its_title()
    {
        using var sample = new Sample(storage);
        sample.Folder.WriteBytes("fiches/Bio/Mitose.md", [0xFF, 0xFE, 0xFD]);

        Assert.AreEqual(sample.Mitosis, sample.Sheets("mitose").Single().Id);
        Assert.IsEmpty(sample.Sheets("cellules"));
    }

    [TestMethod]
    public void An_excerpt_of_a_long_text_is_cut_between_two_words()
    {
        const string before = "Un deux trois quatre cinq six sept huit neuf dix onze douze treize ";
        const string after = " quatorze quinze seize dix-sept dix-huit dix-neuf vingt vingt-et-un vingt-deux vingt-trois vingt-quatre vingt-cinq vingt-six vingt-sept vingt-huit vingt-neuf trente";
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        vault.CreateSheet(null, "Nombres", $"{before}MILIEU{after}\n", Now);

        var excerpt = Read(vault.SearchSheets("milieu", CancellationToken.None)[0].Excerpt);

        Assert.StartsWith("… ", excerpt);
        Assert.Contains(" [MILIEU] quatorze", excerpt);
        Assert.EndsWith("…", excerpt);
        // Aucun mot coupé : chaque mot de l'extrait existe dans le texte.
        var text = $"{before}MILIEU{after}".Split(' ');
        foreach (var word in excerpt.TrimStart('…', ' ').TrimEnd('…').Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.Contains(word.Trim('[', ']'), text, $"« {word} » coupé dans : {excerpt}");
        }
    }

    [TestMethod]
    public void Passages_and_definitions_are_found_and_the_trash_is_left_out()
    {
        using var sample = new Sample(storage);
        var (passage, definition) = sample.Annotate();

        var found = sample.Annotations("CLERGE");

        // Le passage (par son commentaire), puis la définition (par son explication) ; pas celui de la corbeille.
        CollectionAssert.AreEqual(new[] { passage, definition }, found.Select(hit => hit.Id).ToList());
        Assert.IsNull(found[0].Term);
        Assert.AreEqual("[clergé] en tête", Read(found[0].Excerpt));
        Assert.AreEqual("Tiers état", found[1].Term);
        Assert.AreEqual("Ceux qui ne sont ni du [clergé] ni de la noblesse. tiers état", Read(found[1].Excerpt));
        Assert.AreEqual(sample.Revolution, found[0].SheetId);
        Assert.AreEqual("Révolution française", found[0].SheetTitle);
        Assert.AreEqual("trois ordres", found[0].Passage);

        // Le terme suffit : pas d'extrait, le passage reste affichable.
        var byTerm = sample.Annotations("tiers etat").Single(hit => hit.Id == definition);
        Assert.IsTrue(byTerm.Excerpt.IsEmpty);
        Assert.AreEqual("tiers état", byTerm.Passage);

        Assert.IsEmpty(sample.Annotations("   "), "rien sans mot");
    }

    [TestMethod]
    public void Annotations_come_by_sheet_then_passages_first_then_by_term()
    {
        using var sample = new Sample(storage);
        var (revolutionPassage, _) = sample.Annotate();
        var mitosisPassage = sample.Vault.CreateAnnotation(sample.Mitosis, Anchor("noblesse d'une cellule"), null, Now);
        var zeta = sample.Vault.CreateDefinition(sample.Revolution, Anchor("noblesse"), new StoredDefinition("Zêta", "noblesse"), Now);
        var alpha = sample.Vault.CreateDefinition(sample.Revolution, Anchor("noblesse"), new StoredDefinition("Alpha", "noblesse"), Now);
        var other = sample.Vault.CreateAnnotation(sample.Revolution, Anchor("la noblesse"), null, Now);
        sample.Vault.ModifyComment(revolutionPassage, "noblesse", Now);

        var ids = sample.Annotations("noblesse").Select(hit => hit.Id).ToList();

        Assert.AreEqual(mitosisPassage, ids[0], "« mitose » avant « révolution »");
        var passages = ids.Skip(1).Take(2).ToList();
        CollectionAssert.Contains(passages, revolutionPassage);
        CollectionAssert.Contains(passages, other);
        Assert.AreEqual(alpha, ids[3]);
        Assert.AreEqual(zeta, ids[^1]);
    }

    [TestMethod]
    public void An_annotation_whose_sheet_is_gone_says_so()
    {
        using var sample = new Sample(storage);
        sample.Annotate();
        sample.Folder.Delete("fiches/Histoire/Révolution française.md");
        sample.Folder.Delete($"{VaultLayout.SheetTracking}/{sample.Revolution}.json");
        sample.Vault.Analyze(Now);

        Assert.AreEqual("Fiche introuvable", sample.Annotations("clerge")[0].SheetTitle);
    }

    [TestMethod]
    public void Quotes_are_found_by_author_work_and_tags_or_by_their_text()
    {
        using var sample = new Sample(storage);
        var pascal = sample.Vault.AddQuote(Pascal(), Now);

        // L'auteur et l'œuvre suffisent : pas d'extrait.
        var byAuthor = sample.Quotes("pascal pensees").Single();
        Assert.AreEqual(pascal.Path, byAuthor.Path);
        Assert.AreEqual("Blaise Pascal", byAuthor.Author);
        Assert.IsTrue(byAuthor.Excerpt.IsEmpty);
        Assert.AreEqual("Le cœur a ses raisons que la raison ne connaît point.", byAuthor.Text);

        Assert.AreEqual(pascal.Path, sample.Quotes("philosophie").Single().Path);
        Assert.AreEqual("Le [cœur] a ses [raisons] que la raison ne connaît point. Port-Royal", Read(sample.Quotes("coeur raisons")[0].Excerpt));
        StringAssert.Contains(Read(sample.Quotes("port-royal")[0].Excerpt), "[Port-Royal]", StringComparison.Ordinal);
        Assert.IsEmpty(sample.Quotes("   "), "rien sans mot");
    }

    [TestMethod]
    public void Quotes_come_by_author_with_those_without_first_then_by_text()
    {
        using var sample = new Sample(storage);
        var none = sample.Vault.AddQuote(Quote("raison sans auteur", null), Now);
        var second = sample.Vault.AddQuote(Quote("raison zêta", "Pascal"), Now);
        var first = sample.Vault.AddQuote(Quote("raison alpha", "pascal"), Now);
        var last = sample.Vault.AddQuote(Quote("raison", "Voltaire"), Now);

        CollectionAssert.AreEqual(new[] { none.Path, first.Path, second.Path, last.Path }, sample.Quotes("raison").Select(hit => hit.Path).ToList());
    }

    [TestMethod]
    public void A_quote_in_the_trash_is_left_out()
    {
        using var sample = new Sample(storage);
        sample.Vault.TrashQuote(sample.Vault.AddQuote(Pascal(), Now).Path);

        Assert.IsEmpty(sample.Quotes("pascal"));
    }

    [TestMethod]
    public void A_cancelled_search_stops_and_gives_what_it_has()
    {
        using var sample = new Sample(storage);
        sample.Annotate();
        sample.Vault.AddQuote(Pascal(), Now);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.IsEmpty(sample.Vault.SearchSheets("clerge", cancellation.Token));
        Assert.IsEmpty(sample.Vault.SearchAnnotations("clerge", cancellation.Token));
        Assert.IsEmpty(sample.Vault.SearchQuotes("pascal", cancellation.Token));
    }

    [TestMethod]
    public void A_search_writes_nothing()
    {
        using var sample = new Sample(storage);
        sample.Annotate();
        sample.Vault.AddQuote(Pascal(), Now);
        var before = OnDisk(sample.Folder);

        Assert.IsNotEmpty(sample.Sheets("clerge"));
        Assert.IsNotEmpty(sample.Annotations("clerge"));
        Assert.IsNotEmpty(sample.Quotes("pascal"));

        CollectionAssert.AreEqual(before, OnDisk(sample.Folder), "ni index, ni cache, ni journal");
    }

    [TestMethod]
    public void The_port_reaches_the_session()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var id = vault.CreateSheet(null, "Mitose", "La prophase précède.\n", Now);
        var passage = vault.CreateAnnotation(id, new Anchor("La prophase", string.Empty, " précède", 0, 11, [], 5, "sha256:aa"), "à revoir", Now);
        vault.AddQuote(Pascal(), Now);

        Assert.AreEqual("[prophase] précède.", Read(vault.SearchSheets("prophase", CancellationToken.None).Single().Excerpt)[3..]);
        Assert.AreEqual(passage, vault.SearchAnnotations("revoir", CancellationToken.None).Single().Id);
        Assert.AreEqual("Blaise Pascal", vault.SearchQuotes("pensees", CancellationToken.None).Single().Author);

        vault.ArchiveSheet(id, Now);
        Assert.IsTrue(vault.SearchSheets("prophase", CancellationToken.None).Single().Archived, "une fiche archivée reste trouvée");
    }

    /// <summary>Chaque fichier sur le disque, son contenu et sa date ; le verrou, que la session tient ouvert, n'est pas lu.</summary>
    private static List<string> OnDisk(TemporaryVault folder) =>
    [
        .. Directory.EnumerateFiles(folder.Root, "*", SearchOption.AllDirectories)
            .Where(file => file != folder.PathOf(VaultLayout.LockFile) && file != folder.PathOf(VaultLayout.LockInfoFile))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{file}|{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))}|{File.GetLastWriteTimeUtc(file).Ticks}"),
    ];

    private static string Read(ImmutableArray<ExcerptPiece> excerpt) =>
        string.Concat(excerpt.Select(piece => piece.Found ? $"[{piece.Text}]" : piece.Text));

    private static StoredAnchor Anchor(string exact) =>
        new(exact, string.Empty, string.Empty, 0, exact.Length, [], null, "sha256:…", UnknownProperties.None);

    private static QuoteContent Pascal() => new(
        "Le cœur a ses raisons que la raison ne connaît point.",
        "Blaise Pascal",
        null,
        new QuoteSource(null, "Pensées", null, null),
        "Port-Royal",
        ["philosophie"]);

    private static QuoteContent Quote(string text, string? author) => new(text, author, null, QuoteSource.None, null, []);

    /// <summary>Trois fiches : « Révolution française », « Mitose » (étiquette « examen ») et « L’État moderne ».</summary>
    private sealed class Sample : IDisposable
    {
        public Sample(StorageKind storage)
        {
            Folder = TemporaryVault.Empty(storage);
            Vault = Folder.Initialize(Now);
            Revolution = Vault.CreateSheet(
                "Histoire",
                "Révolution française",
                "## Causes sociales\n\nLa société d'Ancien Régime repose sur trois ordres : le clergé, la noblesse et le tiers état.\n\nLa **Dette** atteint la moitié des dépenses.\n",
                Now);
            Mitosis = Vault.CreateSheet("Bio", "Mitose", VaultSearchTests.Mitosis, Now);
            State = Vault.CreateSheet(null, "L’État moderne", "Souveraineté, territoire, population.\n", Now);
            Vault.SetTags(Mitosis, ["examen"], Now);
        }

        public TemporaryVault Folder { get; }

        public VaultSession Vault { get; }

        public string Revolution { get; }

        public string Mitosis { get; }

        public string State { get; }

        public IReadOnlyList<SheetHit> Sheets(string query) => Vault.SearchSheets(query, CancellationToken.None);

        public IReadOnlyList<AnnotationHit> Annotations(string query) => Vault.SearchAnnotations(query, CancellationToken.None);

        public IReadOnlyList<QuoteHit> Quotes(string query) => Vault.SearchQuotes(query, CancellationToken.None);

        /// <summary>Un passage commenté et une définition dans « Révolution », un passage mis à la corbeille dans « Mitose ».</summary>
        public (string Passage, string Definition) Annotate()
        {
            var passage = Vault.CreateAnnotation(Revolution, Anchor("trois ordres"), "clergé en tête", Now);
            var definition = Vault.CreateDefinition(Revolution, Anchor("tiers état"), new StoredDefinition("Tiers état", "Ceux qui ne sont ni du clergé ni de la noblesse."), Now);
            Vault.TrashAnnotation(Vault.CreateAnnotation(Mitosis, Anchor("deux cellules"), "clergé ?", Now), Now);
            return (passage, definition);
        }

        public void Dispose()
        {
            Vault.Dispose();
            Folder.Dispose();
        }
    }
}

/// <summary>Sur le disque tel quel.</summary>
[TestClass]
public sealed class VaultSearchOnDiskTests() : VaultSearchTests(StorageKind.Disk)
{
    private static readonly Moment At = new(new DateOnly(2026, 10, 12), new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void A_sheet_another_program_holds_open_for_writing_is_still_searched()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = folder.Initialize(At);
        vault.CreateSheet("Bio", "Mitose", "La prophase.\n", At);

        using var writer = new FileStream(folder.PathOf("fiches/Bio/Mitose.md"), FileMode.Open, FileAccess.Write, FileShare.Read);

        Assert.HasCount(1, vault.SearchSheets("prophase", CancellationToken.None), "la 0.x la trouvait aussi");
    }

    [TestMethod]
    public void A_sheet_another_program_locks_entirely_is_searched_by_its_title_only()
    {
        using var folder = TemporaryVault.Empty();
        using var vault = folder.Initialize(At);
        vault.CreateSheet("Bio", "Mitose", "La prophase.\n", At);

        using var locked = new FileStream(folder.PathOf("fiches/Bio/Mitose.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.HasCount(1, vault.SearchSheets("mitose", CancellationToken.None));
        Assert.IsEmpty(vault.SearchSheets("prophase", CancellationToken.None), "ce qui ne se lit pas n'est pas cherché, sans erreur");
    }

    [TestMethod]
    public void A_sheet_that_leads_out_of_the_vault_is_searched_by_its_title_only()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        using var vault = folder.Initialize(At);
        vault.CreateSheet("Bio", "Mitose", "La prophase.\n", At);
        File.WriteAllText(outside.PathOf("Mitose.md"), File.ReadAllText(folder.PathOf("fiches/Bio/Mitose.md")));
        Directory.Delete(folder.PathOf("fiches/Bio"), recursive: true);
        FileLinks.CreateDirectoryLink(folder.PathOf("fiches/Bio"), outside.Root);
        try
        {
            Assert.HasCount(1, vault.SearchSheets("mitose", CancellationToken.None));
            Assert.IsEmpty(vault.SearchSheets("prophase", CancellationToken.None), "ce qui est hors du coffre n'est pas lu");
        }
        finally
        {
            Directory.Delete(folder.PathOf("fiches/Bio"));
        }
    }
}

/// <summary>Sur un coffre chiffré.</summary>
[TestClass]
public sealed class VaultSearchEncryptedTests() : VaultSearchTests(StorageKind.Encrypted);
