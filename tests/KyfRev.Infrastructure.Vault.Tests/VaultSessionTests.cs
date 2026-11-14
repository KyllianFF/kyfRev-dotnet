using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Ids;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Défenses de la session du coffre que les scénarios comparés à la 0.x n'exercent pas, jouées sur le disque tel quel
/// (<see cref="VaultSessionOnDiskTests"/>) puis sur un coffre chiffré (<see cref="VaultSessionEncryptedTests"/>).
/// </summary>
/// <param name="storage">Où le coffre est posé.</param>
public abstract class VaultSessionTests(StorageKind storage)
{
    protected static readonly Moment Now = new(new DateOnly(2026, 9, 1), new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void A_resource_of_a_sheet_is_read_and_nothing_else_is()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Fiche", "Corps", Now);
        var link = vault.AddResource(id, "carte.png", [1, 2, 3]);
        var path = $"fiches/{link}";

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, vault.ReadResource(path));
        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource(".suivi/coffre.json"));
        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource("fiches/Fiche.md"));
        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource("fiches/FICHE.MD"));
        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource("../hors.png"));
        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource("fiches/absente.png"));
    }

    [TestMethod]
    public void A_resource_too_big_to_show_is_refused()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        folder.WriteZeros("fiches/grande.png", VaultSession.MaxResourceBytes + 1);

        Assert.ThrowsExactly<VaultException>(() => vault.ReadResource("fiches/grande.png"));
    }

    [TestMethod]
    public void A_new_vault_needs_an_empty_folder()
    {
        using var folder = TemporaryVault.Empty(storage);
        folder.Write("déjà là.txt", "x");
        var error = Assert.ThrowsExactly<VaultException>(() => folder.Initialize(Now));
        Assert.AreEqual(VaultErrorKind.FolderNotEmpty, error.Kind);
    }

    [TestMethod]
    public void A_folder_without_settings_is_not_a_vault()
    {
        using var folder = TemporaryVault.Empty(storage);
        Assert.AreEqual(VaultErrorKind.NotAVault, Assert.ThrowsExactly<VaultException>(() => folder.Open(Now)).Kind);
    }

    [TestMethod]
    public void A_vault_whose_copy_has_not_finished_says_so_and_nothing_is_written()
    {
        // Un dossier synchronisé reçu à moitié : .suivi et son verrou sont là, pas coffre.json.
        using var folder = TemporaryVault.Empty(storage);
        var tracking = Path.Combine(folder.Root, ".suivi");
        Directory.CreateDirectory(tracking);
        File.WriteAllBytes(Path.Combine(tracking, "verrou"), []);

        var error = Assert.ThrowsExactly<VaultException>(() => folder.Open(Now));

        Assert.AreEqual(VaultErrorKind.NotAVault, error.Kind);
        StringAssert.Contains(error.Message, "la copie de ce coffre n'est pas finie", StringComparison.Ordinal);
        StringAssert.Contains(error.Message, "dossier synchronisé", StringComparison.Ordinal);
        Assert.AreEqual("verrou", Path.GetFileName(Directory.GetFiles(tracking).Single()), "rien d'écrit, pas même le verrou-info");
    }

    [TestMethod]
    public void A_vault_opened_twice_names_who_holds_it()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var first = folder.Initialize(Now);

        var error = Assert.ThrowsExactly<VaultException>(() => folder.Open(Now));
        Assert.AreEqual(VaultErrorKind.AlreadyOpen, error.Kind);
        Assert.Contains("depuis 2026-09-01T12:00:00Z", error.Message);

        // Le verrou reste sur le disque, même pour un coffre chiffré.
        File.WriteAllText(folder.PathOf(".suivi/verrou-info.json"), "illisible");
        Assert.Contains("par une autre fenêtre", Assert.ThrowsExactly<VaultException>(() => folder.Open(Now)).Message);
    }

    [TestMethod]
    public void Unreadable_settings_prevent_opening_and_release_the_lock()
    {
        using var folder = TemporaryVault.Empty(storage);
        folder.Initialize(Now).Dispose();
        var original = folder.ReadBytes(".suivi/coffre.json");
        folder.Write(".suivi/coffre.json", "{ cassé");

        Assert.AreEqual(VaultErrorKind.UnreadableSettings, Assert.ThrowsExactly<VaultException>(() => folder.Open(Now)).Kind);

        folder.WriteBytes(".suivi/coffre.json", original);
        using var vault = folder.Open(Now);
        Assert.IsEmpty(vault.Sheets);
    }

    [TestMethod]
    public void A_resource_over_50_MB_is_refused()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Images", "", Now);
        var error = Assert.ThrowsExactly<VaultException>(() => vault.AddResource(id, "énorme.png", new byte[VaultSession.MaxResourceBytes + 1]));
        Assert.AreEqual(VaultErrorKind.ResourceRefused, error.Kind);
    }

    [TestMethod]
    public void A_failed_write_changes_nothing()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Verrouillée", "", Now);
        var before = vault.Sheet(id).Sheet.Tracking;

        using (folder.Hold($".suivi/fiches/{id}.json"))
        {
            var error = Assert.ThrowsExactly<VaultException>(() => vault.ApplyToTracking(id, tracking => tracking.Postpone(Now, new DateOnly(2026, 9, 5))));
            Assert.AreEqual(VaultErrorKind.Io, error.Kind);
        }
        Assert.AreSame(before, vault.Sheet(id).Sheet.Tracking);
    }

    [TestMethod]
    public void A_sheet_rewritten_in_another_encoding_is_not_modified()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Encodage", "", Now);
        folder.WriteBytes("fiches/Encodage.md", [0x65, 0xE9, 0x0A]);
        Assert.AreEqual(VaultErrorKind.NotUtf8, Assert.ThrowsExactly<VaultException>(() => vault.ReadSheet(id)).Kind);
    }

    [TestMethod]
    public void Only_well_named_files_count_as_versions()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Versions", "", Now);
        vault.KeepAsVersion(id, "Brouillon.\n", Now);
        folder.Write($".suivi/versions/{id}/notes.txt", "?");
        folder.Write($".suivi/versions/{id}/20260901T120000Z-1234.md", "numéro trop long");
        folder.CreateDirectory($".suivi/versions/{id}/20260902T120000Z.md");

        var versions = vault.Versions(id);
        Assert.AreEqual("20260902T120000Z.md|20260901T120000Z.md", string.Join('|', versions.Select(v => v.Name)));
        Assert.AreEqual(0, versions[0].Size);
        Assert.IsEmpty(vault.Versions(vault.CreateSheet(null, "Sans version", "", Now)));
    }

    [TestMethod]
    public void The_trash_tolerates_missing_or_damaged_files()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var kept = vault.CreateSheet("Cours", "Gardée", "", Now);
        var damaged = vault.CreateSheet("Cours", "Abîmée", "", Now);
        var noted = vault.CreateSheet("Cours", "Annotée", "", Now);
        vault.TrashSheet(kept, Now);
        vault.TrashSheet(damaged, Now);
        vault.TrashSheet(noted, Now);
        folder.Delete($"fiches/.supprimees/{kept}/corbeille.json");
        folder.Write($".suivi/corbeille/{damaged}.json", "{ cassé");
        folder.Write($".suivi/corbeille/{noted}.annotations.json", "{ \"annotations\": [1, 2] }");
        folder.Write($".suivi/corbeille/{kept}.annotations.json", "pas du JSON");
        folder.Write(".suivi/corbeille/autre.json", "{}");

        var trashed = vault.TrashedSheets();

        Assert.AreEqual($"{kept}:0|{noted}:2", string.Join('|', trashed.Select(t => $"{t.Id}:{t.Annotations}").Order(StringComparer.Ordinal)));
        Assert.AreEqual("Cours", trashed.Single(t => t.Id == kept).Folder, "sans fiche d'information, le suivi fait foi");
        Assert.AreEqual(VaultErrorKind.UnreadableTracking, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheet(damaged, Now)).Kind);
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheet("f_court", Now)).Kind);
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheet("f_01M69JFV2X5VM53EY3DR1BY61M", Now)).Kind);

        vault.RestoreSheet(kept, Now);
        Assert.AreEqual("fiches/Cours/Gardée.md", vault.Sheet(kept).Sheet.Path.Value);
    }

    [TestMethod]
    public void Only_what_is_in_the_trash_is_erased_and_it_leaves_no_file_behind()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet("Cours", "Effacée", "Le corps.", Now);
        var other = vault.CreateSheet("Cours", "Gardée", "Le corps.", Now);
        var kept = vault.CreateAnnotation(other, new Format.StoredAnchor("Le", string.Empty, " corps.", 0, 2, [], null, "sha256:x", Format.UnknownProperties.None), null, Now);
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.PurgeSheet(id)).Kind, "pas à la corbeille");
        Assert.AreEqual(VaultErrorKind.UnknownAnnotation, Assert.ThrowsExactly<VaultException>(() => vault.PurgeAnnotation(kept)).Kind, "pas à la corbeille");
        Assert.IsTrue(folder.FileExists("fiches/Cours/Effacée.md"));

        vault.TrashSheet(id, Now);
        Assert.IsTrue(folder.DirectoryExists($"fiches/.supprimees/{id}"));
        vault.PurgeSheet(id);

        Assert.IsFalse(folder.DirectoryExists($"fiches/.supprimees/{id}"));
        Assert.IsFalse(folder.FileExists($".suivi/corbeille/{id}.json"));
        Assert.IsFalse(folder.FileExists($".suivi/corbeille/{id}.annotations.json"));
        Assert.IsEmpty(vault.TrashedSheets());
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.PurgeSheet(id)).Kind, "deux fois");
        Assert.AreEqual(VaultErrorKind.UnknownSheet, Assert.ThrowsExactly<VaultException>(() => vault.RestoreSheet(id, Now)).Kind);

        vault.TrashAnnotation(kept, Now);
        vault.PurgeAnnotation(kept);
        Assert.IsEmpty(vault.TrashedAnnotations());
        Assert.IsEmpty(vault.Annotations(other));
        Assert.AreEqual(VaultErrorKind.UnknownAnnotation, Assert.ThrowsExactly<VaultException>(() => vault.RestoreAnnotation(kept, Now)).Kind);
    }

    [TestMethod]
    public void A_folder_is_erased_whole_with_its_subfolders()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        vault.CreateSheet("Cours/Vieux", "Une", "", Now);
        vault.CreateSheet("Cours/Vieux/Plus", "Deux", "", Now);
        var kept = vault.CreateSheet("Cours", "Gardée", "", Now);
        vault.CreateFolder("Cours/Vieux/Vide");

        Assert.AreEqual(2, vault.PurgeFolder("Cours/Vieux", Now));

        Assert.IsFalse(folder.DirectoryExists("fiches/Cours/Vieux"));
        Assert.IsEmpty(vault.TrashedSheets());
        Assert.IsEmpty(folder.Directories("fiches/.supprimees/dossiers"), "le rangement est vide");
        Assert.HasCount(1, folder.Directories("fiches/.supprimees"), "il ne reste que lui");
        Assert.AreEqual("fiches/Cours/Gardée.md", vault.Sheet(kept).Sheet.Path.Value);
    }

    [TestMethod]
    public void A_trash_move_blocked_by_an_existing_file_is_reported()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        var id = vault.CreateSheet(null, "Bloquée", "", Now);
        folder.Write($".suivi/corbeille/{id}.json", "{}");
        Assert.AreEqual(VaultErrorKind.AlreadyExists, Assert.ThrowsExactly<VaultException>(() => vault.TrashSheet(id, Now)).Kind);
    }

    [TestMethod]
    public void The_health_report_counts_what_awaits_a_decision()
    {
        using var folder = TemporaryVault.Empty(storage);
        using var vault = folder.Initialize(Now);
        folder.Write("fiches/Libre.md", "Sans en-tête.\n");
        folder.Write("fiches/Illisible.md", "---\nid: [cassé\n---\n");
        vault.Analyze(Now);
        Assert.AreEqual(2, vault.Health.Problems);
    }
}

/// <summary>La session sur le disque tel quel, et ce que seul un disque connaît : les liens, les identifiants.</summary>
[TestClass]
public sealed class VaultSessionOnDiskTests() : VaultSessionTests(StorageKind.Disk)
{
    [TestMethod]
    public void A_link_leading_outside_the_vault_is_never_followed()
    {
        using var folder = TemporaryVault.Empty();
        using var outside = TemporaryVault.Empty();
        outside.Write("Secret.md", "---\nid: f_01M69JFV2X5VM53EY3DR1BY61M\n---\n");
        using var vault = VaultSession.Initialize(folder.Root, Now);
        var link = folder.PathOf("fiches/Dehors");
        FileLinks.CreateDirectoryLink(link, outside.Root);
        try
        {
            Assert.AreEqual(VaultErrorKind.OutsideVault, Assert.ThrowsExactly<VaultException>(() => vault.CreateSheet("Dehors", "Fuite", "", Now)).Kind);
            vault.Analyze(Now);
            Assert.IsEmpty(vault.Health.WithoutTracking, "les fichiers derrière un lien ne sont pas analysés");
            Assert.IsFalse(File.Exists(outside.PathOf("Fuite.md")));
        }
        finally
        {
            // Le lien seul est retiré, jamais ce qu'il désigne.
            Directory.Delete(link);
        }
    }

    [TestMethod]
    public void Identifiers_are_increasing_crockford_ulids()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        var ids = new UlidGenerator(clock);
        var first = ids.Next();
        var second = ids.Next();
        clock.Now = clock.Now.AddMilliseconds(1);
        var third = ids.Next();

        foreach (var id in new[] { first, second, third })
        {
            Assert.AreEqual(26, id.Length);
            Assert.IsTrue(id.All(c => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(c, StringComparison.Ordinal)), id);
        }
        Assert.AreEqual(first[..10], second[..10], "même milliseconde");
        Assert.IsLessThan(0, string.CompareOrdinal(first, second));
        Assert.IsLessThan(0, string.CompareOrdinal(second, third));
        Assert.AreEqual("01M", first[..3], "horodatage d'octobre 2026");

        var replay = UlidGenerator.Replaying(["A"]);
        Assert.AreEqual("A", replay.Next());
        Assert.ThrowsExactly<InvalidOperationException>(() => replay.Next());
    }

    [TestMethod]
    public void Vault_errors_always_carry_a_message()
    {
        Assert.AreEqual("Accès au coffre impossible.", new VaultException().Message);
        Assert.AreEqual(VaultErrorKind.Io, new VaultException("x").Kind);
        Assert.AreEqual(VaultErrorKind.Io, new VaultException("x", new IOException()).Kind);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

/// <summary>La session sur un coffre chiffré : les mêmes défenses, sans rien en clair sur le disque.</summary>
[TestClass]
public sealed class VaultSessionEncryptedTests() : VaultSessionTests(StorageKind.Encrypted);
