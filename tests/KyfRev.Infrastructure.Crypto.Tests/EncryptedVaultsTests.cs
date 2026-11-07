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
