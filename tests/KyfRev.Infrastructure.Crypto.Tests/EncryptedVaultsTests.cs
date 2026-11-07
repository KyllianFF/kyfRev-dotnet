using System.Runtime.Versioning;
using System.Text;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Infrastructure.Crypto.Tests;

/// <summary>
/// Les coffres d'un poste, en clair ou chiffrés : un coffre chiffré s'ouvre avec la clé tirée de son mot de passe ou de sa
/// clé de récupération, sur un stockage que sa session libère avec elle ; sa clé peut être retenue sur ce poste, et
/// oubliée dès qu'elle n'ouvre plus le coffre.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class EncryptedVaultsTests
{
    private const string Password = "un mot de passe d'essai";

    /// <summary>Exactement douze caractères : le moins permis.</summary>
    private const string NewPassword = "douze carac.";

    private static readonly Moment Now = new(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero));

    /// <summary>Un en-tête, sa clé et sa clé de récupération pour tous ces essais : dériver un vrai réglage prend une fraction de seconde.</summary>
    private static readonly Lazy<(VaultHeader Header, byte[] Key, string Recovery)> Shared =
        new(() => VaultHeader.Create("v_01M6A0QJ6E2T9K8R3B7D1W4XHZ", Password, PasswordKeys.NewPbkdf2()));

    [TestMethod]
    public void A_plain_vault_opens_and_is_created_as_before()
    {
        using var place = new Place();
        var vaults = place.Vaults();

        using (var created = vaults.Initialize(place.Vault, Now))
        {
            Assert.AreEqual(place.Vault, created.Root);
        }
        Assert.IsFalse(vaults.IsEncrypted(place.Vault));
        using var opened = vaults.Open(place.Vault, Now);
        Assert.AreEqual(place.Vault, opened.Root);
        Assert.IsTrue(vaults.SameLocation(place.Vault, place.Vault + Path.DirectorySeparatorChar));
    }

    [TestMethod]
    public void An_encrypted_vault_opens_with_its_password_or_its_recovery_key_and_nothing_else()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();

        Assert.IsTrue(vaults.IsEncrypted(place.Vault));
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray());
        }
        // Une clé de récupération recopiée en minuscules, sans tirets : elle s'ouvre aussi.
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(new string([.. Shared.Value.Recovery.Replace("-", " ", StringComparison.Ordinal).Select(char.ToLowerInvariant)]))))
        {
            CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray());
        }
        Assert.AreEqual("Mot de passe incorrect.", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Vault, VaultSecret.Password("pas le bon"))).Message);
        Assert.Contains("faute de frappe", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey("ABCD-EFGH"))).Message);
        Assert.Contains("introuvable", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Root, VaultSecret.Password(Password))).Message);
    }

    [TestMethod]
    public void The_session_takes_the_encrypted_storage_and_releases_it_when_it_closes()
    {
        using var place = new Place();
        place.CreateEncrypted();
        EncryptedStorage? taken = null;
        var vaults = place.Vaults(storage =>
        {
            taken = (EncryptedStorage)storage;
            return new VaultStore(storage: storage, ownsStorage: true);
        });

        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        using (var vault = vaults.Open(place.Vault, key, Now))
        {
            Assert.AreEqual(place.Vault, vault.Root);
            Assert.IsNotNull(taken);
            Assert.IsNotEmpty(taken.ReadAllBytes(Path.Combine(place.Vault, ".suivi", "coffre.json")), "le coffre se lit par son stockage chiffré");
            Assert.IsFalse(File.Exists(Path.Combine(place.Vault, ".suivi", "coffre.json")), "rien n'est en clair sur le disque");
        }
        // Fermée, la session a libéré son stockage, et la copie de la clé qu'il gardait.
        Assert.ThrowsExactly<ObjectDisposedException>(() => taken!.ReadAllBytes(Path.Combine(place.Vault, ".suivi", "coffre.json")));
    }

    [TestMethod]
    public void A_session_that_does_not_open_releases_the_storage_at_once()
    {
        using var place = new Place();
        place.CreateEncrypted();
        EncryptedStorage? taken = null;
        var vaults = place.Vaults(storage =>
        {
            taken = (EncryptedStorage)storage;
            return new Refusing();
        });

        using var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password));
        Assert.ThrowsExactly<InvalidOperationException>(() => vaults.Open(place.Vault, key, Now));
        Assert.ThrowsExactly<ObjectDisposedException>(() => taken!.ReadAllBytes(Path.Combine(place.Vault, ".suivi", "coffre.json")));

        // Une clé qui n'ouvre pas l'index ne laisse rien ouvert.
        using var wrong = new VaultKey(new byte[PasswordKeys.KeyBytes]);
        Assert.ThrowsExactly<VaultCryptoException>(() => vaults.Open(place.Vault, wrong, Now));
    }

    [TestMethod]
    public void A_remembered_key_opens_the_vault_where_it_was_remembered_and_nowhere_else()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        Assert.IsNull(vaults.Remembered(place.Vault), "rien n'est retenu au départ");

        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        Assert.IsTrue(File.Exists(Path.Combine(place.LocalData, RememberedKeys.FileName)));
        Assert.DoesNotContain(Convert.ToBase64String(Shared.Value.Key), File.ReadAllText(Path.Combine(place.LocalData, RememberedKeys.FileName)), "la clé n'est écrite que protégée par Windows");

        // Un autre poste (un autre dossier de données) ne la connaît pas ; ce poste, si, sous une autre écriture du dossier.
        using (var elsewhere = new Place())
        {
            Assert.IsNull(elsewhere.Vaults().Remembered(place.Vault));
        }
        using (var remembered = place.Vaults().Remembered(place.Vault.ToUpperInvariant() + Path.DirectorySeparatorChar))
        {
            Assert.IsNotNull(remembered);
            CollectionAssert.AreEqual(Shared.Value.Key, remembered.Bytes.ToArray());
            using var vault = vaults.Open(place.Vault, remembered, Now);
        }

        // Une copie du coffre ailleurs a son propre dossier : la clé retenue ici ne s'y applique pas.
        var copy = Path.Combine(place.Root, "Copie");
        CopyFolder(place.Vault, copy);
        Assert.IsNull(vaults.Remembered(copy));

        Assert.IsTrue(vaults.Forget(place.Vault));
        Assert.IsNull(vaults.Remembered(place.Vault));
    }

    [TestMethod]
    public void A_remembered_key_that_no_longer_opens_the_vault_is_forgotten()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }

        // Le coffre a été remplacé par un autre, du même identifiant mais d'une autre clé : la clé retenue n'ouvre plus son index.
        var (other, otherKey, _) = VaultHeader.Create(Shared.Value.Header.VaultId, Password, PasswordKeys.NewPbkdf2());
        Directory.Delete(place.Vault, recursive: true);
        Directory.CreateDirectory(place.Vault);
        EncryptedStorage.Create(place.Vault, other, otherKey, DiskStorage.Instance).Dispose();

        Assert.IsNull(vaults.Remembered(place.Vault));
        Assert.IsNull(new RememberedKeys(place.LocalData, DiskStorage.Instance).Find(other.VaultId, place.Vault), "elle est oubliée");
    }

    [TestMethod]
    public void A_remembered_key_stays_remembered_while_the_index_cannot_be_read()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        var index = Path.Combine(place.Vault, EncryptedVault.IndexFile);
        var saved = File.ReadAllBytes(index);

        // L'index est tenu par un autre programme (ou absent un moment, ou d'une version plus récente) : ce n'est pas la
        // clé qui est refusée. Le refus dit pourquoi, et la clé reste retenue.
        using (new FileStream(index, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Contains("index", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.Remembered(place.Vault)).Message);
        }
        File.Delete(index);
        Assert.ThrowsExactly<VaultCryptoException>(() => vaults.Remembered(place.Vault));
        Assert.IsNotNull(new RememberedKeys(place.LocalData, DiskStorage.Instance).Find(Shared.Value.Header.VaultId, place.Vault));

        File.WriteAllBytes(index, saved);
        using var back = vaults.Remembered(place.Vault);
        Assert.IsNotNull(back, "l'index revenu, la clé retenue l'ouvre de nouveau");
    }

    [TestMethod]
    public void A_remembered_key_is_given_only_if_windows_and_the_header_give_it_back()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        var keys = new RememberedKeys(place.LocalData, DiskStorage.Instance);

        // Une clé protégée ailleurs (ou abîmée) : Windows ne la rend pas.
        Assert.IsTrue(keys.Keep(Shared.Value.Header.VaultId, place.Vault, Encoding.UTF8.GetBytes("pas une clé protégée")));
        Assert.IsNull(vaults.Remembered(place.Vault));

        // Un en-tête illisible : rien n'est rendu, et rien ne se retient.
        File.WriteAllText(Path.Combine(place.Vault, EncryptedVault.HeaderFile), "{");
        Assert.IsNull(vaults.Remembered(place.Vault));
        using var key = new VaultKey([.. Shared.Value.Key]);
        Assert.IsFalse(vaults.Remember(place.Vault, key));
    }

    [TestMethod]
    public void Remembered_keys_are_kept_per_folder_and_forgotten_by_rewriting_the_file()
    {
        using var place = new Place();
        var keys = new RememberedKeys(place.LocalData, DiskStorage.Instance);
        var first = Path.Combine(place.Root, "Premier");
        var second = Path.Combine(place.Root, "Second");

        Assert.IsNull(keys.Find("v_1", first), "sans fichier, rien n'est retenu");
        Assert.IsTrue(keys.Forget(first), "rien à oublier");
        Assert.IsFalse(File.Exists(keys.Path), "et rien n'est écrit pour cela");

        Assert.IsTrue(keys.Keep("v_1", first, [1, 2, 3]));
        Assert.IsTrue(keys.Keep("v_2", second, [4, 5]));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, keys.Find("v_1", first + Path.DirectorySeparatorChar));
        Assert.IsNull(keys.Find("v_2", first), "une clé est celle d'un coffre");

        // Retenue de nouveau pour le même dossier, elle remplace la précédente, quel que soit son coffre.
        Assert.IsTrue(keys.Keep("v_3", first.ToUpperInvariant(), [9]));
        Assert.IsNull(keys.Find("v_1", first));
        CollectionAssert.AreEqual(new byte[] { 9 }, keys.Find("v_3", first));

        Assert.IsTrue(keys.Forget(first));
        Assert.IsNull(keys.Find("v_3", first));
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, keys.Find("v_2", second), "l'autre dossier garde la sienne");
        Assert.IsTrue(File.Exists(keys.Path), "oublier réécrit le fichier, sans l'effacer");
    }

    [TestMethod]
    public void An_unreadable_file_of_remembered_keys_remembers_nothing()
    {
        using var place = new Place();
        var keys = new RememberedKeys(place.LocalData, DiskStorage.Instance);
        var vault = Path.Combine(place.Root, "Coffre");
        Directory.CreateDirectory(place.LocalData);

        foreach (var content in new[]
        {
            "pas du JSON",
            "[]",
            """{"format":2,"cles":[]}""",
            """{"format":"un","cles":[]}""",
            """{"format":1}""",
            """{"format":1,"cles":[{"coffre":"v_1","dossier":"C:\\x","cle":"pas du base64 !"}]}""",
            """{"format":1,"cles":[{"coffre":"v_1"},42]}""",
        })
        {
            File.WriteAllText(keys.Path, content);
            Assert.IsNull(keys.Find("v_1", vault), content);
        }

        // Il se réécrit ensuite, lisible.
        Assert.IsTrue(keys.Keep("v_1", vault, [7]));
        CollectionAssert.AreEqual(new byte[] { 7 }, keys.Find("v_1", vault));
    }

    [TestMethod]
    public void A_file_of_remembered_keys_that_cannot_be_read_now_or_comes_from_a_newer_version_is_never_overwritten()
    {
        using var place = new Place();
        var keys = new RememberedKeys(place.LocalData, DiskStorage.Instance);
        var vault = Path.Combine(place.Root, "Coffre");
        Assert.IsTrue(keys.Keep("v_1", vault, [1]));

        // Tenu par un autre programme : rien ne se lit, rien ne s'écrit, et l'autre clé reste.
        using (new FileStream(keys.Path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.IsNull(keys.Find("v_1", vault));
            Assert.IsFalse(keys.Keep("v_2", Path.Combine(place.Root, "Autre"), [2]));
            Assert.IsFalse(keys.Forget(vault), "oublier n'a pas pu être écrit : on le dit");
        }
        CollectionAssert.AreEqual(new byte[] { 1 }, keys.Find("v_1", vault));

        // Écrit par une version plus récente : il reste tel quel.
        const string newer = """{"format":2,"cles":[]}""";
        File.WriteAllText(keys.Path, newer);
        Assert.IsFalse(keys.Keep("v_1", vault, [1]));
        Assert.IsFalse(keys.Forget(vault));
        Assert.AreEqual(newer, File.ReadAllText(keys.Path));

        // Sans format : abîmé, il se réécrit.
        File.WriteAllText(keys.Path, """{"cles":[]}""");
        Assert.IsTrue(keys.Keep("v_1", vault, [3]));
        CollectionAssert.AreEqual(new byte[] { 3 }, keys.Find("v_1", vault));
    }

    [TestMethod]
    public void Remembered_keys_that_cannot_be_written_say_so()
    {
        using var place = new Place();
        // Un fichier là où devrait être le dossier des données : rien ne s'y écrit.
        Directory.CreateDirectory(place.Root);
        File.WriteAllText(place.LocalData, "occupé");
        var keys = new RememberedKeys(place.LocalData, DiskStorage.Instance);

        Assert.IsFalse(keys.Keep("v_1", place.Vault, [1]));
        Assert.IsNull(keys.Find("v_1", place.Vault));
    }

    [TestMethod]
    public void A_new_password_keeps_the_data_key_and_retires_only_the_old_password()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        var before = FilesBesideTheHeader(place.Vault);

        // Le nouveau mot de passe est réglé sur ce poste : de l'ordre d'une seconde.
        vaults.ChangePassword(place.Vault, VaultSecret.Password(Password), NewPassword);

        var header = EncryptedVault.ReadHeader(place.Vault);
        Assert.AreEqual(Shared.Value.Header.VaultId, header.VaultId);
        Assert.AreEqual(PasswordKeys.Argon2id, header.Password.Algorithm, "réglé sur ce poste, par Argon2id");
        Assert.IsTrue(PasswordKeys.IsStrong(header.Password));
        Assert.AreEqual("Mot de passe incorrect.", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Vault, VaultSecret.Password(Password))).Message, "l'ancien mot de passe n'ouvre plus rien");
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery)))
        {
            CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray(), "la clé de récupération l'ouvre toujours");
        }
        Assert.IsTrue(vaults.IsRemembered(place.Vault));
        using (var remembered = vaults.Remembered(place.Vault))
        {
            Assert.IsNotNull(remembered, "la clé retenue l'ouvre toujours");
            CollectionAssert.AreEqual(Shared.Value.Key, remembered.Bytes.ToArray());
        }
        AssertSameFiles(before, FilesBesideTheHeader(place.Vault), "seul l'en-tête est réécrit");

        // Le nouveau mot de passe donne la même clé de données, et le coffre s'ouvre comme avant.
        using var opened = vaults.KeyOf(place.Vault, VaultSecret.Password(NewPassword));
        CollectionAssert.AreEqual(Shared.Value.Key, opened.Bytes.ToArray());
        using var vault = vaults.Open(place.Vault, opened, Now);
        Assert.AreEqual(place.Vault, vault.Root);
    }

    [TestMethod]
    public void A_wrong_secret_or_a_short_password_changes_nothing()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        var header = Path.Combine(place.Vault, EncryptedVault.HeaderFile);
        var before = File.ReadAllBytes(header);

        Assert.AreEqual("Mot de passe incorrect.", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.ChangePassword(place.Vault, VaultSecret.Password("pas le bon"), NewPassword)).Message);
        Assert.Contains("faute de frappe", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.ChangePassword(place.Vault, VaultSecret.RecoveryKey("ABCD-EFGH"), NewPassword)).Message);
        Assert.AreEqual(
            "Le mot de passe doit faire au moins 12 caractères.",
            Assert.ThrowsExactly<VaultCryptoException>(() => vaults.ChangePassword(place.Vault, VaultSecret.Password(Password), "onze carac.")).Message);
        Assert.AreEqual("Mot de passe incorrect.", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.NewRecoveryKey(place.Vault, VaultSecret.Password("pas le bon"))).Message);
        Assert.Contains("introuvable", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.NewRecoveryKey(place.Root, VaultSecret.Password(Password))).Message);

        CollectionAssert.AreEqual(before, File.ReadAllBytes(header), "l'en-tête n'a pas changé");
        using var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password));
        CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray());
    }

    [TestMethod]
    public void A_header_that_cannot_be_written_changes_nothing_and_says_so()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        var header = Path.Combine(place.Vault, EncryptedVault.HeaderFile);
        var before = File.ReadAllBytes(header);

        // Un autre programme le tient ouvert en lecture : il se lit, mais ne se remplace pas.
        using (new FileStream(header, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Contains("n'a pas pu être écrit", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.NewRecoveryKey(place.Vault, VaultSecret.Password(Password))).Message);
        }

        CollectionAssert.AreEqual(before, File.ReadAllBytes(header));
        Assert.IsEmpty(Directory.GetFiles(place.Vault, AtomicFile.TemporaryPrefix + "*"), "aucun temporaire ne reste");
        using var key = vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery));
        CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray(), "l'ancienne clé de récupération l'ouvre toujours");
    }

    [TestMethod]
    public void A_new_recovery_key_opens_the_vault_and_the_old_one_no_longer_does()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        var before = FilesBesideTheHeader(place.Vault);

        var fresh = vaults.NewRecoveryKey(place.Vault, VaultSecret.Password(Password));

        var groups = fresh.Split('-');
        Assert.HasCount(14, groups);
        Assert.IsTrue(groups.All(group => group.Length == 4), "14 groupes de quatre caractères");
        Assert.AreNotEqual(Shared.Value.Recovery, fresh);
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(fresh)))
        {
            CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray(), "la même clé de données");
        }
        Assert.Contains("plus la bonne", Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery))).Message);
        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            CollectionAssert.AreEqual(Shared.Value.Key, key.Bytes.ToArray(), "le mot de passe l'ouvre toujours");
        }
        using (var remembered = vaults.Remembered(place.Vault))
        {
            Assert.IsNotNull(remembered, "la clé retenue aussi");
        }
        AssertSameFiles(before, FilesBesideTheHeader(place.Vault), "seul l'en-tête est réécrit");

        // La clé de récupération en donne elle-même une autre : elle n'ouvre plus rien ensuite.
        var third = vaults.NewRecoveryKey(place.Vault, VaultSecret.RecoveryKey(fresh));
        Assert.ThrowsExactly<VaultCryptoException>(() => vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(fresh)));
        Assert.ThrowsExactly<VaultCryptoException>(() => vaults.NewRecoveryKey(place.Vault, VaultSecret.RecoveryKey(fresh)));
        using var last = vaults.KeyOf(place.Vault, VaultSecret.RecoveryKey(third));
        CollectionAssert.AreEqual(Shared.Value.Key, last.Bytes.ToArray());
    }

    [TestMethod]
    public void A_key_is_said_remembered_from_remember_to_forget_without_being_tried()
    {
        using var place = new Place();
        place.CreateEncrypted();
        var vaults = place.Vaults();
        Assert.IsFalse(vaults.IsRemembered(place.Vault), "rien n'est retenu au départ");

        using (var key = vaults.KeyOf(place.Vault, VaultSecret.Password(Password)))
        {
            Assert.IsTrue(vaults.Remember(place.Vault, key));
        }
        Assert.IsTrue(vaults.IsRemembered(place.Vault));
        Assert.IsTrue(vaults.IsRemembered(place.Vault.ToUpperInvariant() + Path.DirectorySeparatorChar), "sous une autre écriture du même dossier");

        // Ni une copie du coffre ailleurs, ni un dossier sans en-tête.
        var copy = Path.Combine(place.Root, "Copie");
        CopyFolder(place.Vault, copy);
        Assert.IsFalse(vaults.IsRemembered(copy));
        Assert.IsFalse(vaults.IsRemembered(place.Root));

        // Elle n'est pas essayée sur l'index : sans lui, elle reste dite retenue.
        var index = Path.Combine(place.Vault, EncryptedVault.IndexFile);
        var savedIndex = File.ReadAllBytes(index);
        File.Delete(index);
        Assert.IsTrue(vaults.IsRemembered(place.Vault));
        File.WriteAllBytes(index, savedIndex);

        // Un en-tête illisible ne dit rien retenu.
        var header = Path.Combine(place.Vault, EncryptedVault.HeaderFile);
        var savedHeader = File.ReadAllBytes(header);
        File.WriteAllText(header, "{");
        Assert.IsFalse(vaults.IsRemembered(place.Vault));
        File.WriteAllBytes(header, savedHeader);

        Assert.IsTrue(vaults.Forget(place.Vault));
        Assert.IsFalse(vaults.IsRemembered(place.Vault));

        // Une clé retenue dans ce dossier pour un autre coffre n'est pas la sienne.
        Assert.IsTrue(new RememberedKeys(place.LocalData, DiskStorage.Instance).Keep("v_un_autre_coffre", place.Vault, [1, 2, 3]));
        Assert.IsFalse(vaults.IsRemembered(place.Vault));
    }

    /// <summary>Chaque fichier du coffre, son en-tête excepté, et son contenu.</summary>
    private static Dictionary<string, byte[]> FilesBesideTheHeader(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(file => !string.Equals(Path.GetFileName(file), EncryptedVault.HeaderFile, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(file => Path.GetRelativePath(root, file), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertSameFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual, string message)
    {
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), message);
        foreach (var (path, content) in expected)
        {
            CollectionAssert.AreEqual(content, actual[path], $"{message} : {path}");
        }
    }

    private static void CopyFolder(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    /// <summary>Un dossier jetable : un coffre, et les données du poste.</summary>
    private sealed class Place : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "kyfrev-tests", Guid.NewGuid().ToString("N"));

        public string Vault => Path.Combine(Root, "Coffre");

        public string LocalData => Path.Combine(Root, "Poste");

        public EncryptedVaults Vaults(Func<VaultStorage, IVaultStore>? onStorage = null) => new(
            new VaultStore(),
            onStorage ?? (storage => new VaultStore(storage: storage, ownsStorage: true)),
            new RememberedKeys(LocalData, DiskStorage.Instance),
            DiskStorage.Instance);

        /// <summary>Un coffre chiffré vide, que sa session a créé (son en-tête, son index, ses premiers objets).</summary>
        public void CreateEncrypted()
        {
            Directory.CreateDirectory(Vault);
            using var storage = EncryptedStorage.Create(Vault, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance);
            using var vault = new VaultStore(storage: storage).Initialize(Vault, Now);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    /// <summary>Un coffre qui ne s'ouvre jamais.</summary>
    private sealed class Refusing : IVaultStore
    {
        public IVault Open(string root, Moment now) => throw new InvalidOperationException("refusé");

        public IVault Initialize(string root, Moment now) => throw new InvalidOperationException("refusé");

        public bool SameLocation(string first, string second) => false;
    }
}
