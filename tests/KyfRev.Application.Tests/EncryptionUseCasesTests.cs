using System.Runtime.Versioning;
using KyfRev.Application.Ports;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Application.Tests;

/// <summary>
/// Le chiffrement du coffre ouvert : changer son mot de passe, lui donner une clé de récupération neuve, retenir sa clé
/// sur ce poste ou l'oublier, sur un vrai coffre chiffré. Sans coffre ouvert, ou sur un coffre en clair, tout est refusé.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class EncryptionUseCasesTests
{
    private const string Password = "un mot de passe d'essai";

    private const string NewPassword = "un nouveau mot de passe";

    private static readonly Lazy<(VaultHeader Header, byte[] Key, string Recovery)> Shared =
        new(() => VaultHeader.Create("v_01M6A2C3D4E5F6G7H8J9K0MNPQ", Password, PasswordKeys.NewPbkdf2()));

    [TestMethod]
    public void Without_an_open_vault_everything_is_refused()
    {
        using var scene = new Scene();
        var encryption = scene.App.Encryption;

        Assert.IsNull(encryption.Vault);
        Assert.IsFalse(encryption.IsRemembered);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.ChangePassword(VaultSecret.Password(Password), NewPassword, NewPassword).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.DraftRecoveryKey(VaultSecret.Password(Password)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.SaveRecoveryKey(new RecoveryKeyDraft(scene.Folder.Vault, "clé", default, default)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.Remember(VaultSecret.Password(Password)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.Forget().Error);

        // Un coffre chiffré qui attend sa clé n'est pas ouvert.
        scene.CreateEncrypted();
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Open(scene.Folder.Vault).Locked);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.DraftRecoveryKey(VaultSecret.Password(Password)).Error);
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.Remember(VaultSecret.Password(Password)).Error);
        Assert.IsFalse(File.Exists(Path.Combine(scene.LocalData, RememberedKeys.FileName)), "rien n'est retenu");
    }

    [TestMethod]
    public void A_plain_vault_has_no_password_to_change()
    {
        using var scene = new Scene();
        var plain = scene.Folder.PathOf("Clair");
        Assert.AreEqual(plain, scene.App.Vaults.Create(plain).Vault);
        var encryption = scene.App.Encryption;

        Assert.AreEqual(plain, encryption.Vault, "le coffre ouvert, même en clair");
        Assert.IsFalse(encryption.IsRemembered);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, encryption.ChangePassword(VaultSecret.Password(Password), NewPassword, NewPassword).Error);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, encryption.DraftRecoveryKey(VaultSecret.Password(Password)).Error);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, encryption.SaveRecoveryKey(new RecoveryKeyDraft(plain, "clé", default, default)).Error);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, encryption.Remember(VaultSecret.Password(Password)).Error);
        Assert.IsTrue(encryption.Forget().Succeeded, "rien à oublier n'est pas un refus");
        Assert.IsFalse(File.Exists(Path.Combine(plain, EncryptedVault.HeaderFile)), "rien n'a été écrit");

        // Assemblée sans chiffrement, l'application refuse de même, sans rien demander aux clés.
        using var folder = new TestFolder();
        using var bare = new TestApplication(FakeTime.AtLocal(2026, 11, 7, 10, 0));
        Assert.AreEqual(folder.Vault, bare.App.Vaults.Create(folder.Vault).Vault);
        Assert.IsFalse(bare.App.Encryption.IsRemembered);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, bare.App.Encryption.ChangePassword(VaultSecret.Password(Password), NewPassword, NewPassword).Error);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, bare.App.Encryption.DraftRecoveryKey(VaultSecret.Password(Password)).Error);
        Assert.AreEqual(EncryptionUseCases.NotEncrypted, bare.App.Encryption.Remember(VaultSecret.Password(Password)).Error);
        Assert.IsTrue(bare.App.Encryption.Forget().Succeeded);
    }

    [TestMethod]
    public void The_password_of_the_open_vault_changes_and_only_the_new_one_opens_it_again()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false).Vault);
        var id = scene.App.Sheets.Create(null, "Mitose", string.Empty).Value;
        var header = Path.Combine(scene.Folder.Vault, EncryptedVault.HeaderFile);
        var before = File.ReadAllBytes(header);
        var encryption = scene.App.Encryption;

        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", encryption.ChangePassword(VaultSecret.Password(Password), NewPassword, NewPassword + " ").Error);
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", encryption.ChangePassword(VaultSecret.Password(Password), "onze carac.", "onze carac.").Error);
        Assert.AreEqual("Mot de passe incorrect.", encryption.ChangePassword(VaultSecret.Password("pas le bon"), NewPassword, NewPassword).Error);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(header), "rien n'a changé");

        // Par la clé de récupération, quand le mot de passe est perdu : le nouveau est réglé sur ce poste.
        Assert.IsTrue(encryption.ChangePassword(VaultSecret.RecoveryKey(Shared.Value.Recovery), NewPassword, NewPassword).Succeeded);

        Assert.AreEqual(scene.Folder.Vault, scene.App.Host.Root, "le coffre reste ouvert");
        Assert.IsTrue(scene.App.Sheets.Create(null, "Méiose", string.Empty).Succeeded, "et se lit et s'écrit comme avant");
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);
        Assert.AreEqual("Mot de passe incorrect.", scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false).OpenError, "l'ancien ne l'ouvre plus");
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(NewPassword), remember: false).Vault);
        Assert.IsTrue(scene.App.Sheets.List().Value.Any(sheet => sheet.Id == id), "ses fiches sont là");

        // La clé de récupération l'ouvre toujours.
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery), remember: false).Vault);
    }

    [TestMethod]
    public void A_new_recovery_key_is_given_and_the_old_one_no_longer_opens_the_vault()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false).Vault);
        var encryption = scene.App.Encryption;

        Assert.AreEqual("Mot de passe incorrect.", encryption.DraftRecoveryKey(VaultSecret.Password("pas le bon")).Error);
        Assert.Contains("faute de frappe", encryption.DraftRecoveryKey(VaultSecret.RecoveryKey("ABCD-EFGH")).Error ?? string.Empty);
        var draft = encryption.DraftRecoveryKey(VaultSecret.Password(Password));

        Assert.IsTrue(draft.Succeeded);
        Assert.HasCount(14, draft.Value.Key.Split('-'), "14 groupes de quatre caractères");
        Assert.AreNotEqual(Shared.Value.Recovery, draft.Value.Key);
        // Préparée pour un autre coffre, elle n'est pas enregistrée dans celui-ci.
        var elsewhere = new RecoveryKeyDraft(scene.Folder.PathOf("Ailleurs"), draft.Value.Key, draft.Value.Header, draft.Value.Replaces);
        Assert.AreEqual(EncryptionUseCases.OtherVault, encryption.SaveRecoveryKey(elsewhere).Error);
        Assert.IsTrue(encryption.SaveRecoveryKey(draft.Value).Succeeded);
        Assert.Contains("a changé", encryption.SaveRecoveryKey(draft.Value).Error ?? string.Empty, "une seule fois : l'en-tête n'est plus celui qu'elle a lu");
        var fresh = draft.Value.Key;
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);
        Assert.Contains("plus la bonne", scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery), remember: false).OpenError ?? string.Empty);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.RecoveryKey(fresh), remember: false).Vault);
    }

    [TestMethod]
    public void The_key_of_the_open_vault_is_remembered_then_forgotten()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false).Vault);
        var encryption = scene.App.Encryption;
        Assert.IsFalse(encryption.IsRemembered);

        Assert.AreEqual("Mot de passe incorrect.", encryption.Remember(VaultSecret.Password("pas le bon")).Error);
        Assert.IsFalse(encryption.IsRemembered);
        Assert.IsTrue(encryption.Remember(VaultSecret.RecoveryKey(Shared.Value.Recovery)).Succeeded);
        Assert.IsTrue(encryption.IsRemembered);

        // Verrouillé, aucun coffre n'est ouvert ; au démarrage suivant, la clé retenue l'ouvre sans rien demander.
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);
        Assert.IsFalse(encryption.IsRemembered);
        using (var next = scene.Restart())
        {
            Assert.AreEqual(scene.Folder.Vault, next.App.Vaults.OpenAtStartup().Vault);
            Assert.IsTrue(next.App.Encryption.IsRemembered);
            Assert.IsTrue(next.App.Encryption.Forget().Succeeded);
            Assert.IsFalse(next.App.Encryption.IsRemembered);
            Assert.IsTrue(next.App.Encryption.Forget().Succeeded, "rien à oublier n'est pas un refus");
        }

        using var last = scene.Restart();
        Assert.AreEqual(scene.Folder.Vault, last.App.Vaults.OpenAtStartup().Locked, "oubliée, elle ne l'ouvre plus");
    }

    [TestMethod]
    public void A_key_that_cannot_be_kept_or_forgotten_says_so()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Vault);
        var encryption = scene.App.Encryption;
        Assert.IsTrue(encryption.IsRemembered);

        // Tenu par un autre programme, le fichier des clés de ce poste ne se lit ni ne s'écrit.
        using (new FileStream(Path.Combine(scene.LocalData, RememberedKeys.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.AreEqual("La clé retenue n'a pas pu être oubliée : le fichier des clés de ce poste ne s'écrit pas.", encryption.Forget().Error);
            Assert.AreEqual("La clé n'a pas pu être retenue sur ce poste.", encryption.Remember(VaultSecret.Password(Password)).Error);
        }
        Assert.IsTrue(encryption.IsRemembered, "elle est restée retenue");
    }

    [TestMethod]
    public void Without_keys_nothing_is_remembered_and_no_password_changes()
    {
        var keys = new NoVaultKeys();
        Assert.IsFalse(keys.IsRemembered("C:\\Coffre"));
        Assert.ThrowsExactly<NotSupportedException>(() => keys.ChangePassword("C:\\Coffre", VaultSecret.Password(Password), NewPassword));
        Assert.ThrowsExactly<NotSupportedException>(() => keys.DraftRecoveryKey("C:\\Coffre", VaultSecret.Password(Password)));
        Assert.ThrowsExactly<NotSupportedException>(() => keys.SaveRecoveryKey(new RecoveryKeyDraft("C:\\Coffre", "clé", default, default)));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecoveryKeyDraft(null!, "clé", default, default));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecoveryKeyDraft("C:\\Coffre", null!, default, default));
    }

    /// <summary>L'application assemblée comme celle du poste : les coffres en clair et chiffrés, et leurs clés.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly FakeTime _time = FakeTime.AtLocal(2026, 11, 7, 10, 0);
        private readonly bool _ownsFolder;

        public Scene()
            : this(new TestFolder(), new VaultLocations(), ownsFolder: true)
        {
        }

        private Scene(TestFolder folder, VaultLocations locations, bool ownsFolder)
        {
            Folder = folder;
            Locations = locations;
            _ownsFolder = ownsFolder;
            var vaults = new EncryptedVaults(
                new VaultStore(),
                storage => new VaultStore(storage: storage, ownsStorage: true),
                new RememberedKeys(LocalData, DiskStorage.Instance),
                DiskStorage.Instance);
            App = new KyfRevApplication(vaults, new BackupFiles(), locations, _time, Logs, "1.0.0-essai", vaults);
        }

        public TestFolder Folder { get; }

        public VaultLocations Locations { get; }

        public ListLogs Logs { get; } = new();

        public KyfRevApplication App { get; }

        public string LocalData => Folder.PathOf("Poste");

        /// <summary>Le même poste, rouvert : les mêmes dossiers, la même mémoire du dernier coffre.</summary>
        public Scene Restart() => new(Folder, Locations, ownsFolder: false);

        /// <summary>Un coffre chiffré vide, à l'emplacement du coffre.</summary>
        public void CreateEncrypted()
        {
            Directory.CreateDirectory(Folder.Vault);
            var now = new Moment(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero));
            using var storage = EncryptedStorage.Create(Folder.Vault, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance);
            using var vault = new VaultStore(storage: storage).Initialize(Folder.Vault, now);
        }

        public void Dispose()
        {
            App.Dispose();
            Logs.Dispose();
            if (_ownsFolder)
            {
                Folder.Dispose();
            }
        }
    }
}
