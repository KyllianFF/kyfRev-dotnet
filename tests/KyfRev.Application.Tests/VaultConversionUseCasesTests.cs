using System.Runtime.Versioning;
using KyfRev.Application.Ports;
using KyfRev.Application.Tests.Support;
using KyfRev.Application.UseCases;
using KyfRev.Conversion;
using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Application.Tests;

/// <summary>
/// La conversion du coffre ouvert, sur de vrais coffres : préparer le chiffrement sans rien écrire, fermer le coffre sans
/// rien retenir, chiffrer et déchiffrer vers un nouveau dossier voisin, et ce qui est refusé.
/// </summary>
[TestClass]
[SupportedOSPlatform("windows")]
public sealed class VaultConversionUseCasesTests
{
    private const string Password = "un mot de passe d'essai";

    private static readonly Lazy<(VaultHeader Header, byte[] Key, string Recovery)> Shared =
        new(() => VaultHeader.Create("v_01M6A2C3D4E5F6G7H8J9K0MNPQ", Password, PasswordKeys.NewPbkdf2()));

    [TestMethod]
    public void Preparing_needs_an_open_plain_vault_and_a_confirmed_long_enough_password()
    {
        using var scene = new Scene();
        var vaults = scene.App.Vaults;
        Assert.AreEqual(VaultHost.NoVaultOpen, vaults.PrepareEncryption(Password, Password).Error);

        scene.OpenPlain();
        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", vaults.PrepareEncryption(Password, "un autre mot de passe").Error);
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", vaults.PrepareEncryption("trop court", "trop court").Error);

        using var draft = vaults.PrepareEncryption(Password, Password).Value;
        Assert.AreEqual(scene.Folder.Vault, draft.Source);
        Assert.HasCount(14, draft.RecoveryKey.Split('-'), "14 groupes de quatre caractères");
        Assert.AreEqual("chiffrement préparé", draft.ToString(), "la clé ne se lit pas dans un journal");
        Assert.AreEqual(scene.Folder.Vault, vaults.State().Vault, "préparer ne ferme rien");
        Assert.IsFalse(Directory.Exists(scene.Encrypted), "préparer n'écrit rien");
    }

    [TestMethod]
    public void An_encrypted_vault_is_not_encrypted_again()
    {
        using var scene = new Scene();
        scene.OpenEncrypted();

        Assert.AreEqual(VaultUseCases.AlreadyEncrypted, scene.App.Vaults.PrepareEncryption(Password, Password).Error);
    }

    [TestMethod]
    public void Closing_holds_nothing_back_and_a_remembered_key_still_opens_the_vault()
    {
        using var scene = new Scene();
        var vaults = scene.App.Vaults;
        Assert.IsNull(vaults.Close().Vault, "sans coffre ouvert, rien ne se ferme");

        scene.OpenEncrypted(remember: true);
        Assert.IsTrue(vaults.State().Encrypted);
        var closed = vaults.Close();

        Assert.IsNull(closed.Vault);
        Assert.IsNull(closed.Locked, "fermé, il n'attend pas sa clé");
        Assert.IsFalse(closed.Encrypted);
        Assert.IsTrue(scene.Logs.Entries.Any(entry => entry.Message.Contains("fermé pour être converti", StringComparison.Ordinal)));
        Assert.AreEqual(scene.Folder.Vault, vaults.Open(scene.Folder.Vault).Vault, "à la différence d'un verrouillage, sa clé retenue le rouvre");
    }

    [TestMethod]
    public void A_vault_encrypts_then_decrypts_into_new_sibling_folders()
    {
        using var scene = new Scene();
        var vaults = scene.App.Vaults;
        scene.OpenPlain();
        var sheet = scene.App.Sheets.Create(null, "La Révolution française", "Les trois ordres de la société.").Value;
        using var draft = vaults.PrepareEncryption(Password, Password).Value;

        // Le coffre doit être fermé : sinon rien ne s'écrit.
        var refused = vaults.Encrypt(draft);
        Assert.IsFalse(refused.Succeeded);
        Assert.AreEqual(VaultUseCases.StillOpen, refused.Message);
        Assert.AreEqual(scene.Encrypted, refused.Destination);
        Assert.IsFalse(Directory.Exists(scene.Encrypted));

        Assert.IsNull(vaults.Close().Vault);
        var steps = new List<ConversionProgress>();
        var encrypted = vaults.Encrypt(draft, new Collecting(steps));
        Assert.IsTrue(encrypted.Succeeded, encrypted.Message);
        Assert.AreEqual(scene.Encrypted, encrypted.Destination);
        Assert.IsGreaterThan(0, encrypted.Files);
        Assert.AreEqual(new ConversionProgress(ConversionStep.Writing, encrypted.Files, encrypted.Files), steps.Last(step => step.Step == ConversionStep.Writing));
        Assert.AreEqual(new ConversionProgress(ConversionStep.Verifying, encrypted.Files, encrypted.Files), steps[^1]);
        Assert.IsTrue(scene.Logs.Entries.Any(entry => entry.Message.Contains("Conversion du coffre", StringComparison.Ordinal)));

        // Le nom est pris : le suivant ; et le chiffrement préparé sert encore.
        var again = vaults.Encrypt(draft);
        Assert.IsTrue(again.Succeeded, again.Message);
        Assert.AreEqual(scene.Encrypted + " (2)", again.Destination);

        // Le coffre chiffré s'ouvre avec le mot de passe, puis se déchiffre à côté, fermé.
        Assert.AreEqual(scene.Encrypted, vaults.Unlock(scene.Encrypted, VaultSecret.Password(Password), remember: false).Vault);
        Assert.AreEqual("La Révolution française", scene.App.Sheets.Open(sheet).Value.Title);
        var parent = scene.Folder.PathOf("Ailleurs");
        Directory.CreateDirectory(parent);
        var stillOpen = vaults.Decrypt(scene.Encrypted, parent, VaultSecret.RecoveryKey(draft.RecoveryKey));
        Assert.AreEqual(VaultUseCases.StillOpen, stillOpen.Message);
        Assert.IsNull(vaults.Close().Vault);
        var decrypted = vaults.Decrypt(scene.Encrypted, parent, VaultSecret.RecoveryKey(draft.RecoveryKey));
        Assert.IsTrue(decrypted.Succeeded, decrypted.Message);
        Assert.AreEqual(Path.Combine(parent, Path.GetFileName(scene.Encrypted) + VaultUseCases.DecryptedSuffix), decrypted.Destination);

        Assert.AreEqual(decrypted.Destination, vaults.Open(decrypted.Destination).Vault);
        Assert.IsFalse(vaults.State().Encrypted);
        Assert.AreEqual("La Révolution française", scene.App.Sheets.Open(sheet).Value.Title);
    }

    [TestMethod]
    public void What_cannot_be_converted_says_why()
    {
        using var scene = new Scene(new UnreadableParents());
        var vaults = scene.App.Vaults;
        using var draft = new SomeDraft(@"C:\");

        // Un coffre à la racine d'un disque n'a pas de dossier voisin.
        var noParent = vaults.Encrypt(draft);
        Assert.IsFalse(noParent.Succeeded);
        Assert.Contains("n'a pas de dossier parent", noParent.Message);

        // Le dossier qui doit la contenir ne se lit pas : rien n'est écrit, et la raison est dite.
        using var other = new SomeDraft(scene.Folder.Vault);
        var unreadable = vaults.Encrypt(other);
        Assert.IsFalse(unreadable.Succeeded);
        Assert.Contains("ne se lit pas", unreadable.Message);
        Assert.Contains("illisible", unreadable.Message);

        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Encrypt(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Decrypt(null!, "C:\\", VaultSecret.Password(Password)));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Decrypt("C:\\Coffre", null!, VaultSecret.Password(Password)));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Decrypt("C:\\Coffre", "C:\\", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.PrepareEncryption(null!, Password));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.PrepareEncryption(Password, null!));
    }

    [TestMethod]
    public void Without_a_conversion_nothing_is_prepared_nor_converted()
    {
        using var scene = new Scene(new NoVaultConversion());
        scene.OpenPlain();

        Assert.AreEqual(NoVaultConversion.Unavailable, scene.App.Vaults.PrepareEncryption(Password, Password).Error);

        var none = new NoVaultConversion();
        Assert.ThrowsExactly<NotSupportedException>(() => none.PrepareEncryption(@"C:\Coffre", Password));
        Assert.AreEqual(Path.Combine(@"C:\", "Coffre"), none.FreeDestination(@"C:\", "Coffre"));
        using var draft = new SomeDraft(@"C:\Coffre");
        var encrypted = none.Encrypt(draft, @"C:\Copie");
        Assert.IsFalse(encrypted.Succeeded);
        Assert.AreEqual(NoVaultConversion.Unavailable, encrypted.Message);
        Assert.AreEqual(@"C:\Copie", encrypted.Destination);
        Assert.AreEqual(NoVaultConversion.Unavailable, none.Decrypt(@"C:\Coffre", @"C:\Copie", VaultSecret.Password(Password)).Message);
        Assert.ThrowsExactly<ArgumentNullException>(() => new SomeDraft(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SomeDraft(@"C:\Coffre", null!));
    }

    [TestMethod]
    public void The_secret_of_the_open_vault_is_checked_without_writing_anything()
    {
        using var scene = new Scene();
        var encryption = scene.App.Encryption;
        Assert.AreEqual(VaultHost.NoVaultOpen, encryption.Check(VaultSecret.Password(Password)).Error);

        scene.OpenEncrypted();
        var header = Path.Combine(scene.Folder.Vault, EncryptedVault.HeaderFile);
        var before = File.ReadAllBytes(header);

        Assert.AreEqual("Mot de passe incorrect.", encryption.Check(VaultSecret.Password("pas le bon")).Error);
        Assert.IsTrue(encryption.Check(VaultSecret.Password(Password)).Succeeded);
        Assert.IsTrue(encryption.Check(VaultSecret.RecoveryKey(Shared.Value.Recovery)).Succeeded);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(header));
        Assert.IsFalse(encryption.IsRemembered, "vérifier ne retient rien");
        Assert.ThrowsExactly<ArgumentNullException>(() => encryption.Check(null!));
    }

    /// <summary>Une progression qui note chaque étape, dans l'ordre et sans attendre le fil de l'interface.</summary>
    private sealed class Collecting(List<ConversionProgress> steps) : IProgress<ConversionProgress>
    {
        public void Report(ConversionProgress value) => steps.Add(value);
    }

    /// <summary>Un chiffrement préparé qui ne garde rien.</summary>
    private sealed class SomeDraft(string source, string key = "clé") : EncryptionDraft(source, key)
    {
        protected override void Dispose(bool disposing)
        {
        }
    }

    /// <summary>Une conversion dont le dossier parent ne se lit pas.</summary>
    private sealed class UnreadableParents : IVaultConversion
    {
        public EncryptionDraft PrepareEncryption(string source, string password) => throw new NotSupportedException();

        public string FreeDestination(string parent, string name) => throw new IOException("illisible");

        public ConversionReport Encrypt(EncryptionDraft draft, string destination, IProgress<ConversionProgress>? progress = null) => throw new NotSupportedException();

        public ConversionReport Decrypt(string source, string destination, VaultSecret secret, IProgress<ConversionProgress>? progress = null) => throw new NotSupportedException();
    }

    /// <summary>L'application assemblée comme sur le poste, avec la conversion (PBKDF2, pour que l'essai reste rapide).</summary>
    private sealed class Scene : IDisposable
    {
        private readonly FakeTime _time = FakeTime.AtLocal(2026, 11, 7, 10, 0);

        public Scene(IVaultConversion? conversion = null)
        {
            var vaults = new EncryptedVaults(
                new VaultStore(),
                storage => new VaultStore(storage: storage, ownsStorage: true),
                new RememberedKeys(Folder.PathOf("Poste"), DiskStorage.Instance),
                DiskStorage.Instance);
            App = new KyfRevApplication(vaults, new BackupFiles(), new VaultLocations(), _time, Logs, "1.0.0-essai", vaults, conversion ?? new VaultConversions(PasswordKeys.NewPbkdf2()));
        }

        public TestFolder Folder { get; } = new();

        public ListLogs Logs { get; } = new();

        public KyfRevApplication App { get; }

        /// <summary>Le dossier d'un coffre chiffré à côté du coffre.</summary>
        public string Encrypted => Folder.Vault + VaultUseCases.EncryptedSuffix;

        public void OpenPlain() => Assert.AreEqual(Folder.Vault, App.Vaults.Create(Folder.Vault).Vault);

        public void OpenEncrypted(bool remember = false)
        {
            Directory.CreateDirectory(Folder.Vault);
            var now = new Moment(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero));
            using (var storage = EncryptedStorage.Create(Folder.Vault, Shared.Value.Header, Shared.Value.Key, DiskStorage.Instance))
            using (new VaultStore(storage: storage).Initialize(Folder.Vault, now))
            {
            }
            Assert.AreEqual(Folder.Vault, App.Vaults.Unlock(Folder.Vault, VaultSecret.Password(Password), remember).Vault);
        }

        public void Dispose()
        {
            App.Dispose();
            Logs.Dispose();
            Folder.Dispose();
        }
    }
}
