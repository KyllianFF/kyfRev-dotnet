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

            var again = VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2());
            Assert.IsTrue(again.Succeeded, $"écriture {failAt} : {again.Message}");
            Assert.AreEqual(0, Leftovers(place), "la conversion suivante retire ce qu'une coupure a laissé");
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

            var again = VaultConversion.Decrypt(source.Encrypted, place.Decrypted, Password);
            Assert.IsTrue(again.Succeeded, $"écriture {failAt} : {again.Message}");
            Assert.AreEqual(0, Leftovers(place), "la conversion suivante retire ce qu'une coupure a laissé");
            CollectionAssert.AreEqual(Contents(vault.Root), Contents(place.Decrypted), $"écriture {failAt}");
        }
    }

    [TestMethod]
    public void Only_a_conversion_folder_is_ever_removed()
    {
        using var vault = SmallVault();
        using var place = new Place();
        string[] kept = [".chiffre.conversion-pas-a-moi", ".chiffre.conversion-abcdefgh.ijk.bak", ".autre.conversion-abcdefgh.ijk", "chiffre.conversion-abcdefgh.ijk"];
        foreach (var name in kept)
        {
            Directory.CreateDirectory(Path.Combine(place.Root, name));
        }
        Directory.CreateDirectory(Path.Combine(place.Root, ".chiffre.conversion-abcdefgh.ijk", "objets"));

        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);

        CollectionAssert.AreEquivalent(kept.Append("chiffre").ToArray(), Directory.GetDirectories(place.Root).Select(Path.GetFileName).ToArray());
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
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
