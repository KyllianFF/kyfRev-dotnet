using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KyfRev.Cli;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Tests.Support;

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

    /// <summary>Un dossier jetable où les conversions écrivent leurs destinations.</summary>
    private sealed class Place : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"))).FullName;

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
