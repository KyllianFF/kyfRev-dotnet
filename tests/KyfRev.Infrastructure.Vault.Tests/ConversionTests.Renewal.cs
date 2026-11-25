using KyfRev.Conversion;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Renouveler toutes les clés (rangée 29B) : une nouvelle clé de données, tout le coffre chiffré de nouveau dans un dossier
/// voisin, relu ; puis l'échange des dossiers. L'ancien secret ouvre encore l'ancien en-tête, mais plus le coffre renouvelé.
/// </summary>
public sealed partial class ConversionTests
{
    private const string RenewedPassword = "un mot de passe tout neuf";

    [TestMethod]
    public void Renewing_writes_the_vault_with_a_new_data_key_that_the_old_secret_never_gives()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        var oldHeader = File.ReadAllBytes(Path.Combine(place.Encrypted, EncryptedVault.HeaderFile));
        var renewedFolder = Path.Combine(place.Root, "renouvele");

        var prepared = VaultConversion.PrepareRenewal(place.Encrypted, Password, isRecoveryKey: false, RenewedPassword, PasswordKeys.NewPbkdf2());
        using var plan = prepared.Plan!;
        var result = VaultConversion.Renew(plan, Password, isRecoveryKey: false, renewedFolder);

        Assert.IsTrue(result.Succeeded, result.Message);
        Assert.AreEqual(plan.RecoveryKey, result.RecoveryKey);
        Assert.AreEqual(EncryptedVault.ReadHeader(place.Encrypted).VaultId, EncryptedVault.ReadHeader(renewedFolder).VaultId, "le même coffre");
        var newKey = EncryptedVault.ReadHeader(renewedFolder).Open(RenewedPassword);
        using (var renewed = EncryptedVault.Open(renewedFolder, newKey, DiskStorage.Instance))
        using (var original = EncryptedVault.Open(place.Encrypted, EncryptedVault.ReadHeader(place.Encrypted).Open(Password), DiskStorage.Instance))
        {
            CollectionAssert.AreEqual(original.Files.ToArray(), renewed.Files.ToArray());
            CollectionAssert.AreEqual(original.Folders.ToArray(), renewed.Folders.ToArray());
            foreach (var file in original.Files)
            {
                CollectionAssert.AreEqual(original.Read(file), renewed.Read(file), file);
            }
        }
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.ReadHeader(renewedFolder).Open(Password), "l'ancien mot de passe n'ouvre pas le coffre renouvelé");

        // Une ancienne copie de l'en-tête et l'ancien mot de passe donnent l'ancienne clé : elle ne lit rien du coffre renouvelé.
        var oldKey = VaultHeader.Read(oldHeader).Open(Password);
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(renewedFolder, oldKey, DiskStorage.Instance));
        Assert.AreEqual(0, Leftovers(place));
    }

    [TestMethod]
    public void After_renewing_the_folders_swap_and_the_vault_keeps_its_name()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        var renewedFolder = VaultConversion.FreeDestination(place.Root, Path.GetFileName(place.Encrypted) + VaultConversion.RenewedSuffix);
        using (var plan = VaultConversion.PrepareRenewal(place.Encrypted, Password, false, Password, PasswordKeys.NewPbkdf2()).Plan!)
        {
            Assert.IsTrue(VaultConversion.Renew(plan, Password, false, renewedFolder).Succeeded);
        }

        var (retired, refusal) = VaultConversion.Swap(place.Encrypted, renewedFolder);

        Assert.IsNull(refusal);
        Assert.AreEqual(Path.Combine(place.Root, "chiffre" + VaultConversion.RetiredSuffix), retired);
        Assert.IsFalse(Directory.Exists(renewedFolder));
        var key = EncryptedVault.ReadHeader(place.Encrypted).Open(Password);
        using (EncryptedVault.Open(place.Encrypted, key, DiskStorage.Instance))
        {
            // Le mot de passe gardé ouvre le coffre renouvelé, à son nom ; l'ancien coffre est à côté.
        }
        Assert.IsTrue(EncryptedVault.IsEncrypted(retired!));
        Assert.ThrowsExactly<VaultCryptoException>(() => EncryptedVault.Open(place.Encrypted, EncryptedVault.ReadHeader(retired!).Open(Password), DiskStorage.Instance));

        // Échange impossible : rien ne change.
        var (none, why) = VaultConversion.Swap(place.Encrypted, Path.Combine(place.Root, "absent"));
        Assert.IsNull(none);
        Assert.Contains("n'a pas pu prendre son nom", why ?? string.Empty);
        Assert.IsTrue(EncryptedVault.IsEncrypted(place.Encrypted), "l'ancien nom est rendu au coffre");
    }

    [TestMethod]
    public void A_renewal_is_refused_without_the_right_secret_an_encrypted_vault_or_a_long_password()
    {
        using var vault = TemporaryVault.CopyOfReference();
        using var place = new Place();
        Assert.Contains("n'est pas un coffre chiffré", VaultConversion.PrepareRenewal(vault.Root, Password, false, RenewedPassword, PasswordKeys.NewPbkdf2()).Refusal);
        Assert.IsTrue(VaultConversion.Encrypt(vault.Root, place.Encrypted, Password, PasswordKeys.NewPbkdf2()).Succeeded);
        Assert.AreEqual(VaultConversion.WrongSecret, VaultConversion.PrepareRenewal(place.Encrypted, "pas le bon mot de passe", false, RenewedPassword, PasswordKeys.NewPbkdf2()).Refusal);
        Assert.Contains("au moins", VaultConversion.PrepareRenewal(place.Encrypted, Password, false, "court", PasswordKeys.NewPbkdf2()).Refusal);

        var plan = VaultConversion.PrepareRenewal(place.Encrypted, Password, false, RenewedPassword, PasswordKeys.NewPbkdf2()).Plan!;
        var target = Path.Combine(place.Root, "renouvele");
        Assert.AreEqual(VaultConversion.WrongSecret, VaultConversion.Renew(plan, "pas le bon mot de passe", false, target).Message);
        plan.Dispose();
        Assert.AreEqual(VaultConversion.Abandoned, VaultConversion.Renew(plan, Password, false, target).Message, "un renouvellement abandonné n'écrit rien");
        Assert.IsFalse(Directory.Exists(target));
    }
}
