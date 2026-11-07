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
/// Ouvrir un coffre chiffré : sans la clé retenue sur ce poste, il attend son mot de passe ou sa clé de récupération, et
/// rien de lui n'est lu ; déverrouillé, il est le coffre ouvert comme un autre ; sa clé peut être retenue, puis oubliée.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class VaultUnlockTests
{
    private const string Password = "un mot de passe d'essai";

    private static readonly Lazy<(VaultHeader Header, byte[] Key, string Recovery)> Shared =
        new(() => VaultHeader.Create("v_01M6A1B2C3D4E5F6G7H8J9K0MN", Password, PasswordKeys.NewPbkdf2()));

    [TestMethod]
    public void Without_its_key_an_encrypted_vault_waits_and_nothing_is_open()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();

        var state = scene.App.Vaults.Open(scene.Folder.Vault);

        Assert.AreEqual(scene.Folder.Vault, state.Locked);
        Assert.IsNull(state.Vault);
        Assert.IsNull(state.OpenError, "attendre sa clé n'est pas une erreur");
        Assert.IsNull(scene.App.Host.Root);
        Assert.IsEmpty(scene.Locations.Remembered, "un coffre qui attend sa clé n'est pas encore retenu");
    }

    [TestMethod]
    public void A_wrong_password_keeps_it_waiting_with_the_reason()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();

        var state = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password("pas le bon"), remember: false);

        Assert.AreEqual(scene.Folder.Vault, state.Locked);
        Assert.AreEqual("Mot de passe incorrect.", state.OpenError);
        Assert.IsNull(scene.App.Host.Root);
    }

    [TestMethod]
    public void Its_password_opens_it_like_any_vault_and_nothing_is_remembered_unless_asked()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();

        var state = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false);

        Assert.AreEqual(scene.Folder.Vault, state.Vault);
        Assert.IsNull(state.Locked);
        Assert.IsNull(state.OpenError);
        Assert.Contains(scene.Folder.Vault, scene.Locations.Remembered, "il sera rouvert au prochain démarrage");
        Assert.IsTrue(scene.App.Sheets.Create(null, "Mitose", string.Empty).Succeeded, "il se lit et s'écrit comme un autre");
        Assert.IsTrue(scene.Backed(), "et sa sauvegarde du jour est faite");
        Assert.IsFalse(File.Exists(Path.Combine(scene.Folder.Vault, "fiches", "Mitose.md")), "rien en clair sur le disque");

        // Déverrouiller de nouveau le coffre ouvert ne fait rien.
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password("peu importe"), remember: false).Vault);

        // Rien n'a été retenu : au prochain démarrage, il attend de nouveau sa clé.
        using var next = scene.Restart();
        Assert.AreEqual(scene.Folder.Vault, next.App.Vaults.OpenAtStartup().Locked);
    }

    [TestMethod]
    public void A_remembered_key_opens_it_without_asking_until_it_is_forgotten()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();

        Assert.IsNull(scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Locked);
        scene.App.Host.Dispose();

        using (var next = scene.Restart())
        {
            var reopened = next.App.Vaults.OpenAtStartup();
            Assert.AreEqual(scene.Folder.Vault, reopened.Vault, "la clé retenue l'ouvre sans rien demander");
            Assert.IsNull(reopened.Locked);

            // Déverrouillé sans la retenir (par sa clé de récupération ici), la clé retenue est oubliée.
            next.App.Host.Dispose();
            Assert.IsNull(next.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.RecoveryKey(Shared.Value.Recovery), remember: false).Locked);
        }

        using var last = scene.Restart();
        Assert.AreEqual(scene.Folder.Vault, last.App.Vaults.OpenAtStartup().Locked);
    }

    [TestMethod]
    public void An_index_that_cannot_be_read_says_why_and_keeps_the_remembered_key()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.IsNull(scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Locked);
        scene.App.Host.Dispose();
        var index = Path.Combine(scene.Folder.Vault, EncryptedVault.IndexFile);

        using (var next = scene.Restart())
        using (new FileStream(index, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var state = next.App.Vaults.OpenAtStartup();
            Assert.AreEqual(scene.Folder.Vault, state.Locked);
            Assert.Contains("index", state.OpenError ?? string.Empty, "le refus dit pourquoi");
        }

        using var last = scene.Restart();
        Assert.AreEqual(scene.Folder.Vault, last.App.Vaults.OpenAtStartup().Vault, "la clé retenue est restée : l'index revenu, le coffre s'ouvre seul");
    }

    [TestMethod]
    public void A_key_that_cannot_be_remembered_does_not_stop_the_vault_opening()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        // Un fichier là où devrait être le dossier des données du poste : rien ne s'y écrit.
        File.WriteAllText(scene.LocalData, "occupé");

        var state = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true);

        Assert.AreEqual(scene.Folder.Vault, state.Vault);
        Assert.IsNull(state.OpenError);
        Assert.IsTrue(scene.Logs.Warnings.Any(warning => warning.Contains("n'a pas pu être retenue", StringComparison.Ordinal)), "le journal le dit");
    }

    [TestMethod]
    public void The_open_vault_stays_open_until_the_encrypted_one_is_unlocked()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        var plain = scene.Folder.PathOf("Clair");
        Assert.AreEqual(plain, scene.App.Vaults.Create(plain).Vault);

        var waiting = scene.App.Vaults.Open(scene.Folder.Vault);
        Assert.AreEqual(scene.Folder.Vault, waiting.Locked);
        Assert.AreEqual(plain, waiting.Vault, "le coffre ouvert le reste");

        var refused = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password("pas le bon"), remember: false);
        Assert.AreEqual(plain, refused.Vault);
        Assert.AreEqual(scene.Folder.Vault, refused.Locked);

        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: false).Vault);
    }

    [TestMethod]
    public void An_encrypted_vault_that_does_not_open_with_its_key_says_why()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        // Son index a disparu : la clé est bonne, mais le coffre ne s'ouvre pas.
        File.Delete(Path.Combine(scene.Folder.Vault, EncryptedVault.IndexFile));

        var state = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true);

        Assert.AreEqual(scene.Folder.Vault, state.Locked);
        Assert.Contains("index", state.OpenError ?? string.Empty);
        Assert.IsNull(scene.App.Host.Root);
        Assert.IsFalse(File.Exists(Path.Combine(scene.LocalData, RememberedKeys.FileName)), "une clé qui n'a rien ouvert n'est pas retenue");
    }

    [TestMethod]
    public void A_remembered_key_that_opens_a_vault_held_elsewhere_reports_it_as_for_any_vault()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        Assert.IsNull(scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Locked);

        // Une seconde application sur le même poste : la clé retenue est bonne, mais le coffre est déjà ouvert.
        using var other = scene.Restart();
        var state = other.App.Vaults.Open(scene.Folder.Vault);

        Assert.IsNull(state.Locked, "ce n'est pas la clé qui manque");
        Assert.Contains("déjà ouvert", state.OpenError ?? string.Empty);
    }

    [TestMethod]
    public void Locking_closes_the_encrypted_vault_and_only_its_password_opens_it_again()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        var opened = scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true);
        Assert.IsTrue(opened.Encrypted, "le coffre ouvert se dit chiffré");
        Assert.IsTrue(scene.App.Vaults.State().Encrypted);

        var locked = scene.App.Vaults.Lock();

        Assert.AreEqual(scene.Folder.Vault, locked.Locked);
        Assert.IsNull(locked.Vault);
        Assert.IsFalse(locked.Encrypted);
        Assert.IsNull(scene.App.Host.Root);
        Assert.IsFalse(scene.App.Sheets.Create(null, "Mitose", string.Empty).Succeeded, "fermé, plus rien ne s'y lit ni ne s'y écrit");
        Assert.IsTrue(scene.Logs.Entries.Any(entry => entry.Message.Contains("verrouillé", StringComparison.Ordinal)));
        // Le verrou est libéré : une autre application l'ouvrirait (avec sa clé).
        using (var other = scene.Restart())
        {
            Assert.AreEqual(scene.Folder.Vault, other.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Vault);
        }

        // Verrouillé ici, la clé retenue ne le rouvre plus : son mot de passe, si, et la clé resservira ensuite.
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Open(scene.Folder.Vault).Locked);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Vault);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);

        // Verrouiller sans coffre, ou un coffre en clair, ne fait rien.
        Assert.IsNull(scene.App.Vaults.Lock().Locked);
        var plain = scene.Folder.PathOf("Clair");
        scene.App.Vaults.Create(plain);
        var state = scene.App.Vaults.Lock();
        Assert.AreEqual(plain, state.Vault);
        Assert.IsNull(state.Locked);
        Assert.IsFalse(state.Encrypted);
    }

    [TestMethod]
    public void Each_locked_vault_stays_locked_until_its_own_key_is_given()
    {
        using var scene = new Scene();
        scene.CreateEncrypted();
        var other = scene.Folder.PathOf("Autre");
        scene.CreateEncrypted(other);
        Assert.IsNull(scene.App.Vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(Password), remember: true).Locked);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Lock().Locked);
        Assert.IsFalse(scene.App.Vaults.IsOpening);

        // Un autre coffre déverrouillé puis verrouillé : le premier reste verrouillé, sa clé retenue ne le rouvre pas.
        Assert.AreEqual(other, scene.App.Vaults.Unlock(other, VaultSecret.Password(Password), remember: false).Vault);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Open(scene.Folder.Vault).Locked);
        Assert.AreEqual(other, scene.App.Vaults.Lock().Locked);
        Assert.AreEqual(scene.Folder.Vault, scene.App.Vaults.Open(scene.Folder.Vault).Locked);
        Assert.AreEqual(other, scene.App.Vaults.Open(other).Locked);
    }

    [TestMethod]
    public void Without_keys_nothing_is_encrypted()
    {
        var keys = new NoVaultKeys();
        Assert.IsFalse(keys.IsEncrypted("C:\\Coffre"));
        Assert.IsNull(keys.Remembered("C:\\Coffre"));
        Assert.ThrowsExactly<NotSupportedException>(() => keys.KeyOf("C:\\Coffre", VaultSecret.Password("x")));
        using var key = new VaultKey([1, 2]);
        Assert.IsFalse(keys.Remember("C:\\Coffre", key));
        Assert.IsTrue(keys.Forget("C:\\Coffre"));

        // Des coffres seulement en clair n'ouvrent pas de coffre chiffré.
        IVaultStore plain = new VaultStore();
        Assert.ThrowsExactly<NotSupportedException>(() => plain.Open("C:\\Coffre", key, new Moment(new DateOnly(2026, 11, 7), DateTimeOffset.UnixEpoch)));
    }

    [TestMethod]
    public void A_secret_never_shows_its_text_and_a_key_is_erased_once_released()
    {
        var password = VaultSecret.Password("mon secret");
        var recovery = VaultSecret.RecoveryKey("ABCD-EFGH");
        Assert.AreEqual("mon secret", password.Text);
        Assert.IsFalse(password.IsRecoveryKey);
        Assert.IsTrue(recovery.IsRecoveryKey);
        Assert.AreEqual("mot de passe", password.ToString());
        Assert.AreEqual("clé de récupération", recovery.ToString());
        Assert.ThrowsExactly<ArgumentException>(() => VaultSecret.Password(string.Empty));
        Assert.ThrowsExactly<ArgumentNullException>(() => VaultSecret.RecoveryKey(null!));

        var bytes = new byte[] { 1, 2, 3 };
        var key = new VaultKey(bytes);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, key.Bytes.ToArray());
        key.Dispose();
        CollectionAssert.AreEqual(new byte[3], bytes, "la clé est effacée");
        Assert.ThrowsExactly<ObjectDisposedException>(() => key.Bytes.ToArray());
        Assert.ThrowsExactly<ArgumentNullException>(() => new VaultKey(null!));
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

        /// <summary>Un coffre chiffré vide, à l'emplacement du coffre ou à <paramref name="root"/>.</summary>
        public void CreateEncrypted(string? root = null)
        {
            root ??= Folder.Vault;
            Directory.CreateDirectory(root);
            var now = new Moment(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero));
            using var storage = EncryptedStorage.Create(root, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance);
            using var vault = new VaultStore(storage: storage).Initialize(root, now);
        }

        public bool Backed() => Directory.Exists(Folder.Backups) && Directory.EnumerateFiles(Folder.Backups, "*.zip").Any();

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
