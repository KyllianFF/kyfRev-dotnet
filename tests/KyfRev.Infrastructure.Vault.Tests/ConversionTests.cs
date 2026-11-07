using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KyfRev.Cli;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Tests.Support;
using KyfRev.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// <c>kyfrev-coffre chiffrer</c> et <c>dechiffrer</c> : toujours vers un nouveau dossier, vérifiés fichier par fichier, la
/// source intacte, et rien d'écrit quand ils refusent.
/// </summary>
[TestClass]
public sealed partial class ConversionTests
{
    private const string Password = "un mot de passe d'essai";

    [TestMethod]
    public void A_vault_encrypted_then_decrypted_comes_back_byte_for_byte()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var before = vault.Snapshot();

        var (encrypted, output) = Run(Lines(Password, Password), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2");

        Assert.AreEqual(VaultCommands.Success, encrypted, output);
        CollectionAssert.AreEqual(before, vault.Snapshot(), "la source n'a pas changé, pas même son verrou");
        Assert.IsTrue(EncryptedVault.IsEncrypted(place.Encrypted));
        var names = Directory.EnumerateFileSystemEntries(place.Encrypted, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(place.Encrypted, path)).ToList();
        Assert.IsTrue(names.All(name => name is EncryptedVault.HeaderFile or EncryptedVault.IndexFile or EncryptedVault.Objects || name.StartsWith(EncryptedVault.Objects + Path.DirectorySeparatorChar, StringComparison.Ordinal)), string.Join(", ", names));
        Assert.AreEqual(Contents(vault.Root).Count, Directory.GetFiles(Path.Combine(place.Encrypted, EncryptedVault.Objects)).Length, "un objet par fichier");
        var recovery = RecoveryLine().Match(output);
        Assert.IsTrue(recovery.Success, output);
        Assert.AreEqual(0, Leftovers(place), "aucun dossier temporaire ne reste");

        var (decrypted, decryptedOutput) = Run(Lines(Password), "dechiffrer", place.Encrypted, place.Decrypted);
        Assert.AreEqual(VaultCommands.Success, decrypted, decryptedOutput);
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted), "chaque fichier, à l'octet près");

        var (recovered, recoveredOutput) = Run(Lines(recovery.Groups[1].Value), "dechiffrer", place.Encrypted, place.Recovered);
        Assert.AreEqual(VaultCommands.Success, recovered, recoveredOutput);
        CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Recovered), "la clé de récupération ouvre aussi le coffre");
    }

    [TestMethod]
    public void A_refused_conversion_writes_nothing()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        var before = vault.Snapshot();

        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password, "un autre mot de passe"), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2").ExitCode, "deux saisies différentes");
        Assert.AreEqual(VaultCommands.Refused, Run(Lines("court", "court"), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2").ExitCode, "un mot de passe trop court");
        Directory.CreateDirectory(place.Encrypted);
        var (existing, output) = Run(Lines(Password, Password), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2");
        Assert.AreEqual(VaultCommands.Refused, existing);
        Assert.Contains("existe déjà", output);
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(place.Encrypted).Any());
        Directory.Delete(place.Encrypted);
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password, Password), "chiffrer", vault.Root, Path.Combine(vault.Root, "dedans"), "--pbkdf2").ExitCode, "jamais dans la source");
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password, Password), "chiffrer", place.Root, place.Encrypted, "--pbkdf2").ExitCode, "pas un coffre");

        // Le coffre ouvert dans kyfRev : l'application tient son verrou.
        using (new FileStream(vault.PathOf(VaultLayout.LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            var (open, openOutput) = Run(Lines(Password, Password), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2");
            Assert.AreEqual(VaultCommands.Refused, open);
            Assert.Contains("ouvert dans kyfRev", openOutput);
        }

        CollectionAssert.AreEqual(before, vault.Snapshot());
        Assert.IsFalse(Directory.Exists(place.Encrypted));
        Assert.AreEqual(0, Leftovers(place));
    }

    [TestMethod]
    public void A_wrong_secret_or_an_altered_vault_decrypts_nothing()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Assert.AreEqual(VaultCommands.Success, Run(Lines(Password, Password), "chiffrer", vault.Root, place.Encrypted, "--pbkdf2").ExitCode);

        var (wrong, output) = Run(Lines("un mauvais mot de passe"), "dechiffrer", place.Encrypted, place.Decrypted);
        Assert.AreEqual(VaultCommands.Refused, wrong);
        Assert.Contains("n'ouvre pas ce coffre", output);
        Assert.AreEqual(VaultCommands.Refused, Run(Lines(Password), "dechiffrer", vault.Root, place.Decrypted).ExitCode, "un coffre en clair ne se déchiffre pas");

        var objet = Directory.EnumerateFiles(Path.Combine(place.Encrypted, EncryptedVault.Objects)).First();
        var bytes = File.ReadAllBytes(objet);
        bytes[^1] ^= 1;
        File.WriteAllBytes(objet, bytes);
        var (altered, alteredOutput) = Run(Lines(Password), "dechiffrer", place.Encrypted, place.Decrypted);
        Assert.AreEqual(VaultCommands.Refused, altered, alteredOutput);
        Assert.IsFalse(Directory.Exists(place.Decrypted), "un seul fichier altéré, et rien n'est écrit");
        Assert.AreEqual(0, Leftovers(place));
    }

    [TestMethod]
    public void A_file_that_does_not_come_back_identical_is_refused_and_nothing_is_written()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();

        // Un objet altéré entre l'écriture et la vérification : le chiffrement ne rend rien.
        var encrypted = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), temporary =>
        {
            var objet = Directory.EnumerateFiles(Path.Combine(temporary, EncryptedVault.Objects)).First();
            var bytes = File.ReadAllBytes(objet);
            bytes[^1] ^= 1;
            File.WriteAllBytes(objet, bytes);
        });
        Assert.IsFalse(encrypted.Succeeded);
        Assert.Contains("rien n'a été écrit", encrypted.Message);
        Assert.IsFalse(Directory.Exists(place.Encrypted));

        // Un fichier en clair altéré entre l'écriture et la vérification : le déchiffrement ne rend rien.
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        var decrypted = VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password, temporary =>
            File.AppendAllText(Path.Combine(temporary, ".suivi", "coffre.json"), " "));
        Assert.IsFalse(decrypted.Succeeded);
        Assert.Contains("ne se relit pas à l'identique", decrypted.Message);
        Assert.IsFalse(Directory.Exists(place.Decrypted));
        Assert.AreEqual(0, Leftovers(place));
    }

    [TestMethod]
    public void An_unreadable_source_or_a_missing_parent_writes_nothing()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();

        // Un fichier de la source tenu par un autre programme : le chiffrement échoue sans rien laisser.
        var sheet = Directory.EnumerateFiles(vault.PathOf(VaultLayout.Sheets), "*.md", SearchOption.AllDirectories).First();
        using (new FileStream(sheet, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2());
            Assert.IsFalse(result.Succeeded);
            Assert.Contains("Le chiffrement a échoué", result.Message);
        }
        Assert.IsFalse(Directory.Exists(place.Encrypted));
        Assert.AreEqual(0, Leftovers(place));

        var missing = Path.Combine(place.Root, "absent", "chiffre");
        Assert.Contains("n'existe pas", VaultConversion.Encrypt(vault.Root, missing, Password, PasswordKeys.NewPbkdf2()).Message);

        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        Directory.CreateDirectory(place.Decrypted);
        Assert.Contains("existe déjà", VaultConversion.Decrypt(place.Encrypted, place.Decrypted, Password).Message);
    }

    [TestMethod]
    public void An_encryption_cut_at_any_write_leaves_the_source_intact_and_nothing_half_written()
    {
        using var vault = SmallVault();
        var before = vault.Snapshot();
        var writes = Writes(place => VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), disk: place.Disk));

        for (var failAt = 1; failAt <= writes; failAt++)
        {
            using var place = new Place();
            AssertCut(place, failAt, before, vault.Snapshot, disk => VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), disk: disk), place.Encrypted);

            var again = AfterCut(place, failAt, () => VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()));
            Assert.IsTrue(again.Succeeded, $"écriture {failAt} : {again.Message}");
            Assert.AreEqual(0, Leftovers(place), $"écriture {failAt}");
            CollectionAssert.AreEqual(Contents(vault.Root), Decrypted(place), $"écriture {failAt}");
        }
    }

    [TestMethod]
    public void A_decryption_cut_at_any_write_leaves_the_source_intact_and_nothing_half_written()
    {
        using var vault = SmallVault();
        using var source = new Place();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, source.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        var before = Snapshot(source.Encrypted);
        var writes = Writes(place => VaultConversion.Decrypt(source.Encrypted, place.Decrypted, Password, disk: place.Disk));

        for (var failAt = 1; failAt <= writes; failAt++)
        {
            using var place = new Place();
            AssertCut(place, failAt, before, () => Snapshot(source.Encrypted), disk => VaultConversion.Decrypt(source.Encrypted, place.Decrypted, Password, disk: disk), place.Decrypted);

            var again = AfterCut(place, failAt, () => VaultConversion.Decrypt(source.Encrypted, place.Decrypted, Password));
            Assert.IsTrue(again.Succeeded, $"écriture {failAt} : {again.Message}");
            Assert.AreEqual(0, Leftovers(place), $"écriture {failAt}");
            CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted), $"écriture {failAt}");
        }
    }

    [TestMethod]
    public void Folders_that_only_look_like_a_leftover_are_left_alone()
    {
        using var vault = SmallVault();
        using var place = new Place();
        // Le dernier a le nom exact, mais pas de marqueur : rien ne dit qu'une conversion l'a laissé.
        string[] kept = [".chiffre.conversion-pas-a-moi", ".chiffre.conversion-abcdefgh.ijk.bak", ".autre.conversion-abcdefgh.ijk", "chiffre.conversion-abcdefgh.ijk", ".chiffre.conversion-bcdefghi.jkl"];
        foreach (var name in kept)
        {
            Directory.CreateDirectory(Path.Combine(place.Root, name, "objets"));
        }

        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);

        CollectionAssert.AreEquivalent(kept.Append("chiffre").ToArray(), Directory.GetDirectories(place.Root).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void A_leftover_beside_the_destination_refuses_and_is_never_touched()
    {
        using var vault = SmallVault();
        var now = new KyfRev.Domain.Calendar.Moment(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 13, 0, 0, TimeSpan.Zero));

        // Laissé par une conversion coupée net, tenu par une conversion en cours, ou ouvert dans kyfRev depuis : la conversion
        // vers la même destination refuse, le nomme, et ne le touche pas.
        foreach (var state in new[] { "coupé", "tenu", "ouvert" })
        {
            using var place = new Place();
            var leftover = Leftover(vault.Root, Path.Combine(place.Root, ".chiffre.conversion-abcdefgh.ijk"));
            using var held = state == "tenu" ? new FileStream(Path.Combine(leftover, VaultConversion.MarkFile), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete) : null;
            if (state == "ouvert")
            {
                VaultSession.Open(leftover, now).Dispose();
            }
            var before = Snapshot(leftover);

            var refused = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2());

            Assert.IsFalse(refused.Succeeded, $"{state} : {refused.Message}");
            Assert.Contains(leftover, refused.Message, $"{state} : le reste est nommé");
            Assert.Contains("à la main", refused.Message, state);
            CollectionAssert.AreEqual(before, Snapshot(leftover), $"{state} : le reste n'est pas touché");
            Assert.IsFalse(Directory.Exists(place.Encrypted), $"{state} : rien n'est écrit");
            Assert.AreEqual(1, Leftovers(place), $"{state} : pas même un dossier temporaire");
        }
    }

    [TestMethod]
    public void A_folder_with_the_name_of_a_leftover_but_no_mark_is_converted_and_kept()
    {
        using var vault = SmallVault();
        using var place = new Place();

        // Le nom seul ne fait pas un reste : sans marqueur, le dossier se convertit et reste tel quel.
        var plain = CopyWithoutLock(vault.Root, Path.Combine(place.Root, ".Coffre.conversion-abcdefgh.ijk"));
        var before = Snapshot(plain);
        var encrypted = VaultConversion.Encrypt(plain, Path.Combine(place.Root, "Coffre"), Password, PasswordKeys.NewPbkdf2());
        Assert.IsTrue(encrypted.Succeeded, encrypted.Message);
        CollectionAssert.AreEqual(before, Snapshot(plain), "la source est intacte");

        var nested = CopyWithoutLock(vault.Root, Path.Combine(place.Root, ".Range.conversion-abcdefgh.ijk", "Coffre"));
        var nestedBefore = Snapshot(nested);
        var converted = VaultConversion.Encrypt(nested, Path.Combine(place.Root, "Range"), Password, PasswordKeys.NewPbkdf2());
        Assert.IsTrue(converted.Succeeded, converted.Message);
        CollectionAssert.AreEqual(nestedBefore, Snapshot(nested));
    }

    [TestMethod]
    public void A_conversion_folder_is_refused_as_a_source_however_it_is_reached()
    {
        using var vault = SmallVault();
        using var place = new Place();

        // Un déchiffrement vers « Coffre », coupé net, a laissé un coffre en clair complet dans son dossier temporaire.
        var leftover = Leftover(vault.Root, Path.Combine(place.Root, ".Coffre.conversion-abcdefgh.ijk"));
        var before = Snapshot(leftover);
        var link = Path.Combine(place.Root, "lien");
        FileLinks.CreateDirectoryLink(link, leftover);
        using var drive = FileLinks.Subst(leftover);

        // Par son chemin, par une jonction, par un lecteur substitué, vers une autre destination : la source porte le marqueur.
        List<string> sources = [link, leftover];
        if (drive is not null)
        {
            sources.Add(drive.Root);
        }
        foreach (var source in sources)
        {
            var refused = VaultConversion.Encrypt(source, Path.Combine(place.Root, "Autre"), Password, PasswordKeys.NewPbkdf2());
            Assert.IsFalse(refused.Succeeded, $"{source} : {refused.Message}");
            Assert.Contains(VaultConversion.MarkFile, refused.Message, source);
            CollectionAssert.AreEqual(before, Snapshot(leftover), $"{source} : le reste est intact");
        }
        Assert.IsFalse(Directory.Exists(Path.Combine(place.Root, "Autre")));

        // Dans l'autre sens : un coffre chiffré dans le dossier temporaire d'un chiffrement.
        var encrypted = Path.Combine(place.Root, ".Clair.conversion-abcdefgh.ijk");
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        File.WriteAllBytes(Path.Combine(encrypted, VaultConversion.MarkFile), []);
        var encryptedBefore = Snapshot(encrypted);
        Assert.Contains(VaultConversion.MarkFile, VaultConversion.Decrypt(encrypted, Path.Combine(place.Root, "Lu"), Password).Message);
        CollectionAssert.AreEqual(encryptedBefore, Snapshot(encrypted));
        if (drive is null)
        {
            Assert.Inconclusive("Aucune lettre de lecteur n'est libre pour subst : seuls le chemin et la jonction sont vérifiés.");
        }
    }

    [TestMethod]
    public void A_source_below_a_conversion_folder_is_never_touched()
    {
        using var vault = SmallVault();
        using var place = new Place();
        var leftover = Path.Combine(place.Root, ".Range.conversion-abcdefgh.ijk");
        var nested = Path.Combine(Leftover(vault.Root, leftover, "Coffre"), "Coffre");
        var before = Snapshot(nested);
        var link = Path.Combine(place.Root, "lien");
        FileLinks.CreateDirectoryLink(link, nested);
        var shortName = FileLinks.ShortPath(leftover);
        using var drive = FileLinks.Subst(nested);

        // Par son chemin, par une jonction vers lui, sous le nom court (8.3) du dossier temporaire, et par un lecteur
        // substitué sur lui.
        List<string> sources = [link, nested];
        List<string> notChecked = [];
        if (!string.Equals(Path.GetFileName(shortName), Path.GetFileName(leftover), StringComparison.Ordinal))
        {
            sources.Add(Path.Combine(shortName, "Coffre"));
        }
        else
        {
            notChecked.Add("le nom court (8.3), que ce volume ne donne pas");
        }
        if (drive is not null)
        {
            sources.Add(drive.Root);
        }
        else
        {
            notChecked.Add("le lecteur substitué, faute de lettre libre");
        }
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];

            // Vers la destination du reste : il est là, la conversion refuse et le nomme.
            var beside = VaultConversion.Encrypt(source, Path.Combine(place.Root, "Range"), Password, PasswordKeys.NewPbkdf2());
            Assert.IsFalse(beside.Succeeded, $"{source} : {beside.Message}");
            Assert.Contains(leftover, beside.Message, $"{source} : le reste est nommé");

            // Ailleurs : une source qui porte le marqueur, ou dont un parent le porte, est refusée. Un lecteur substitué ne
            // mène à aucun parent : il se convertit, mais rien ne s'efface.
            var elsewhere = VaultConversion.Encrypt(source, Path.Combine(place.Root, $"Ailleurs-{i}"), Password, PasswordKeys.NewPbkdf2());
            Assert.AreEqual(source == drive?.Root, elsewhere.Succeeded, $"{source} : {elsewhere.Message}");
            CollectionAssert.AreEqual(before, Snapshot(nested), $"{source} : la source n'est pas touchée");
        }
        if (notChecked.Count > 0)
        {
            Assert.Inconclusive($"Non vérifiés : {string.Join(", ", notChecked)}.");
        }
    }

    [TestMethod]
    public void A_temporary_folder_that_cannot_be_removed_keeps_its_mark_and_says_so()
    {
        using var vault = SmallVault();
        using var source = new Place();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, source.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);

        foreach (var decrypting in new[] { false, true })
        {
            using var place = new Place();
            var destination = decrypting ? place.Decrypted : place.Encrypted;
            ConversionResult Conversion(Action<string>? beforeVerifying = null) => decrypting
                ? VaultConversion.Decrypt(source.Encrypted, destination, Password, beforeVerifying)
                : VaultConversion.Encrypt(vault.Root, destination, Password, PasswordKeys.NewPbkdf2(), beforeVerifying);

            // Un aperçu de l'Explorateur tient un fichier du dossier temporaire sans le laisser effacer : la vérification le
            // relit, puis le renommage final échoue, et le dossier ne s'efface qu'en partie.
            FileStream? held = null;
            ConversionResult refused;
            try
            {
                refused = Conversion(temporary => held = new FileStream(
                    Directory.EnumerateFiles(temporary, "*", SearchOption.AllDirectories).First(file => Path.GetFileName(file) != VaultConversion.MarkFile),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read));
            }
            finally
            {
                held?.Dispose();
            }

            var what = decrypting ? "déchiffrement" : "chiffrement";
            Assert.IsFalse(refused.Succeeded, $"{what} : {refused.Message}");
            var left = Directory.EnumerateDirectories(place.Root).Single(folder => Path.GetFileName(folder).Contains(".conversion-", StringComparison.Ordinal));
            Assert.Contains(left, refused.Message, $"{what} : le dossier est nommé");
            Assert.Contains("Tant qu'il est là, une conversion vers la même destination refuse", refused.Message, what);
            Assert.IsTrue(File.Exists(Path.Combine(left, VaultConversion.MarkFile)), $"{what} : le marqueur reste, et le message dit vrai");
            Assert.IsFalse(Directory.Exists(destination), what);

            var again = AfterCut(place, 0, () => Conversion());
            Assert.IsTrue(again.Succeeded, $"{what} : {again.Message}");
            Assert.AreEqual(0, Leftovers(place), what);
        }
    }

    [TestMethod]
    public void Two_conversions_to_the_same_destination_leave_each_other_alone()
    {
        using var vault = SmallVault();
        using var place = new Place();
        ConversionResult? second = null;
        string? running = null;

        var first = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2(), temporary =>
        {
            running = temporary;
            second = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2());
            Assert.IsTrue(Directory.Exists(temporary), "la seconde ne touche pas au dossier de la première");
        });

        Assert.IsFalse(second!.Succeeded, "la première tient son marqueur : la seconde refuse");
        Assert.Contains(running!, second.Message, "et nomme le dossier de la première");
        Assert.IsTrue(first.Succeeded, first.Message);
        Assert.AreEqual(0, Leftovers(place));
        CollectionAssert.AreEqual(Contents(vault.Root), Decrypted(place));
    }

    [TestMethod]
    public void The_key_of_the_check_is_cleared_even_when_the_check_fails()
    {
        using var place = new Place();
        Directory.CreateDirectory(place.Encrypted);
        var key = RandomNumberGenerator.GetBytes(PasswordKeys.KeyBytes);

        Assert.ThrowsExactly<VaultCryptoException>(() => VaultConversion.Reread(place.Encrypted, key, new(StringComparer.Ordinal), new(StringComparer.Ordinal)));

        Assert.IsTrue(key.All(value => value == 0), "la clé est effacée");
    }

    /// <summary>
    /// Le dossier temporaire qu'une conversion coupée net a laissé : une copie du coffre <paramref name="source"/> (dans
    /// <paramref name="inside"/> s'il est donné), sans verrou, et le marqueur de la conversion, que plus personne ne tient.
    /// </summary>
    private static string Leftover(string source, string folder, string? inside = null)
    {
        CopyWithoutLock(source, inside is null ? folder : Path.Combine(folder, inside));
        File.WriteAllBytes(Path.Combine(folder, VaultConversion.MarkFile), []);
        return folder;
    }

    /// <summary>
    /// La conversion qui suit une coupure, ou un dossier temporaire qui n'a pas pu être retiré : s'il reste un dossier
    /// temporaire marqué, elle refuse, le nomme, et n'y touche pas ; une fois qu'on l'a retiré à la main, elle réussit.
    /// </summary>
    private static ConversionResult AfterCut(Place place, int failAt, Func<ConversionResult> convert)
    {
        var left = Directory.EnumerateDirectories(place.Root).Where(folder => Path.GetFileName(folder).Contains(".conversion-", StringComparison.Ordinal)).ToList();
        if (left.Count == 0)
        {
            return convert();
        }
        var what = $"écriture {failAt}";
        var before = left.Select(Snapshot).ToList();
        var refused = convert();
        Assert.IsFalse(refused.Succeeded, $"{what} : un reste fait refuser ({refused.Message})");
        Assert.Contains(left[0], refused.Message, $"{what} : le reste est nommé");
        for (var i = 0; i < left.Count; i++)
        {
            Assert.IsTrue(File.Exists(Path.Combine(left[i], VaultConversion.MarkFile)), $"{what} : le reste est marqué");
            CollectionAssert.AreEqual(before[i], Snapshot(left[i]), $"{what} : le reste n'est pas touché");
            Directory.Delete(left[i], recursive: true);
        }
        return convert();
    }

    /// <summary>Une copie du coffre <paramref name="source"/>, sans son verrou : ce qu'une conversion écrit.</summary>
    private static string CopyWithoutLock(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative is VaultLayout.LockFile or VaultLayout.LockInfoFile)
            {
                continue;
            }
            var copy = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
        }
        return target;
    }

    /// <summary>
    /// Une conversion dont l'écriture <paramref name="failAt"/> échoue, une fois puis à partir d'elle (une coupure : plus rien
    /// ne s'écrit, pas même le nettoyage) : refusée, la source intacte, pas de destination ; la première ne laisse rien.
    /// </summary>
    private static void AssertCut(Place place, int failAt, SortedDictionary<string, string> before, Func<SortedDictionary<string, string>> source, Func<FailingDisk, ConversionResult> convert, string destination)
    {
        foreach (var cut in new[] { false, true })
        {
            var disk = new FailingDisk { FailAt = failAt, Cut = cut };
            var result = convert(disk);
            var what = $"écriture {failAt}{(cut ? ", coupée net" : string.Empty)}";
            Assert.IsFalse(result.Succeeded, what);
            Assert.IsGreaterThan(0, disk.Failures, what);
            Assert.Contains("Coupure simulée", result.Message, what);
            CollectionAssert.AreEqual(before, source(), $"{what} : la source n'a pas changé");
            Assert.IsFalse(Directory.Exists(destination), $"{what} : pas de destination");
            if (!cut)
            {
                Assert.AreEqual(0, Leftovers(place), $"{what} : aucun dossier temporaire ne reste");
            }
        }
    }

    /// <summary>Le nombre d'écritures d'une conversion qui réussit.</summary>
    private static int Writes(Func<Place, ConversionResult> convert)
    {
        using var place = new Place();
        var result = convert(place);
        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.IsGreaterThan(5, place.Disk.Writes, "chaque fichier, chaque dossier, l'index et le renommage final");
        return place.Disk.Writes;
    }

    /// <summary>Un petit coffre fermé : deux fiches, une image, un dossier vide.</summary>
    private static TemporaryVault SmallVault()
    {
        var vault = TemporaryVault.Empty();
        var now = new KyfRev.Domain.Calendar.Moment(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
        using var session = VaultSession.Initialize(vault.Root, now);
        var id = session.CreateSheet("Biologie", "Mitose", "La prophase.\n", now);
        session.AddResource(id, "cellule.png", [1, 2, 3]);
        session.CreateSheet(null, "Révolution", "1789.\n", now);
        session.CreateFolder("Vide");
        return vault;
    }

    private static SortedDictionary<string, string> Decrypted(Place place)
    {
        var decrypted = Path.Combine(place.Root, "verifie");
        Assert.IsTrue(VaultConversion.Decrypt(place.Encrypted, decrypted, Password).Succeeded);
        var contents = Contents(decrypted);
        Directory.Delete(decrypted, recursive: true);
        return contents;
    }

    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            snapshot[Path.GetRelativePath(root, file)] = $"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))}|{File.GetLastWriteTimeUtc(file).Ticks}";
        }
        return snapshot;
    }

    [TestMethod]
    public void A_hidden_entry_keeps_what_is_typed_and_erased()
    {
        var keys = new Queue<ConsoleKeyInfo>(
        [
            Key('\b', ConsoleKey.Backspace),
            Key('m', ConsoleKey.M),
            Key('o', ConsoleKey.O),
            Key('x', ConsoleKey.X),
            Key('\b', ConsoleKey.Backspace),
            Key('\t', ConsoleKey.Tab),
            Key('t', ConsoleKey.T),
            Key('\r', ConsoleKey.Enter),
        ]);

        Assert.AreEqual("mot", VaultCommands.ReadHidden(keys.Dequeue));
        Assert.IsEmpty(keys);
    }

    private static ConsoleKeyInfo Key(char character, ConsoleKey key) => new(character, key, shift: false, alt: false, control: false);

    /// <summary>Le contenu d'un coffre en clair : l'empreinte de chaque fichier, verrou exclu.</summary>
    private static SortedDictionary<string, string> Contents(string root)
    {
        var contents = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative is not VaultLayout.LockFile and not VaultLayout.LockInfoFile)
            {
                contents[relative] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
            }
        }
        return contents;
    }

    private static int Leftovers(Place place) =>
        Directory.EnumerateDirectories(place.Root).Count(folder => Path.GetFileName(folder).Contains(".conversion-", StringComparison.Ordinal));

    private static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    private static (int ExitCode, string Output) Run(string input, params string[] args)
    {
        using var output = new StringWriter();
        using var reader = new StringReader(input);
        var exitCode = VaultCommands.Build(output, reader).Parse(args).Invoke();
        return (exitCode, output.ToString());
    }

    [GeneratedRegex(@"^    ([0-9A-Z]{4}(?:-[0-9A-Z]{4}){13})\s*$", RegexOptions.Multiline)]
    private static partial Regex RecoveryLine();

    /// <summary>Un dossier jetable où les conversions écrivent leurs destinations, et le disque qui compte leurs écritures.</summary>
    private sealed class Place : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"))).FullName;

        public FailingDisk Disk { get; } = new();

        public string Encrypted => Path.Combine(Root, "chiffre");

        public string Decrypted => Path.Combine(Root, "en-clair");

        public string Recovered => Path.Combine(Root, "recupere");

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                // Une jonction d'abord : Windows refuse de l'effacer dans le même parcours une fois sa cible effacée.
                foreach (var link in new DirectoryInfo(Root).EnumerateDirectories().Where(folder => folder.LinkTarget is not null))
                {
                    link.Delete();
                }
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
