using KyfRev.Application.Ports;
using KyfRev.Application.UseCases;
using KyfRev.Conversion;

namespace KyfRev.Application.Tests;

/// <summary>
/// Renouveler toutes les clés du coffre ouvert (rangée 29B), par les cas d'usage : ce qui est refusé, puis le coffre
/// chiffré de nouveau, à son nom, l'ancien mis à côté.
/// </summary>
public sealed partial class VaultConversionUseCasesTests
{
    private const string RenewedPassword = "un mot de passe tout neuf";

    private const string WrongSecret = "Ce mot de passe, ou cette clé de récupération, n'ouvre pas ce coffre.";

    [TestMethod]
    public void Renewing_needs_an_open_encrypted_vault_its_secret_and_a_usable_password()
    {
        using var scene = new Scene();
        var vaults = scene.App.Vaults;
        var current = VaultSecret.Password(Password);
        Assert.AreEqual(VaultHost.NoVaultOpen, vaults.PrepareRenewal(current, null, null).Error);

        using (var plain = new Scene())
        {
            plain.OpenPlain();
            Assert.AreEqual(VaultUseCases.NotEncrypted, plain.App.Vaults.PrepareRenewal(current, null, null).Error);
        }

        scene.OpenEncrypted();
        Assert.AreEqual(VaultUseCases.RenewalNeedsPassword, vaults.PrepareRenewal(VaultSecret.RecoveryKey(Shared.Value.Recovery), null, null).Error);
        Assert.AreEqual("Les deux mots de passe ne sont pas les mêmes.", vaults.PrepareRenewal(current, RenewedPassword, "un autre").Error);
        Assert.AreEqual("Le mot de passe doit faire au moins 12 caractères.", vaults.PrepareRenewal(current, "court", "court").Error);
        Assert.AreEqual(WrongSecret, vaults.PrepareRenewal(VaultSecret.Password("pas le bon mot de passe"), null, null).Error);
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.PrepareRenewal(null!, null, null));
        Assert.AreEqual(scene.Folder.Vault, vaults.State().Vault, "rien n'est fermé pour un refus");

        // Sans conversion assemblée : refusé, avec le même message que les autres conversions.
        using var without = new Scene(new NoVaultConversion());
        without.OpenEncrypted();
        Assert.AreEqual(NoVaultConversion.Unavailable, without.App.Vaults.PrepareRenewal(current, null, null).Error);
        IVaultConversion none = new NoVaultConversion();
        Assert.ThrowsExactly<NotSupportedException>(() => none.PrepareRenewal(scene.Folder.Vault, current, Password));
        using var draft = new SomeDraft(scene.Folder.Vault);
        Assert.AreEqual(NoVaultConversion.Unavailable, none.Renew(draft, current, @"C:\Copie").Message);
        Assert.AreEqual(NoVaultConversion.Unavailable, none.Swap(scene.Folder.Vault, @"C:\Copie").Message);
    }

    [TestMethod]
    public void Renewing_writes_the_vault_again_keeps_its_name_and_sets_the_old_one_aside()
    {
        using var scene = new Scene();
        scene.OpenEncrypted();
        var vaults = scene.App.Vaults;
        var current = VaultSecret.Password(Password);

        using var draft = vaults.PrepareRenewal(current, RenewedPassword, RenewedPassword).Value;
        Assert.AreNotEqual(Shared.Value.Recovery, draft.RecoveryKey);
        Assert.AreEqual(VaultUseCases.StillOpen, vaults.Renew(draft, current).Message, "le coffre se ferme d'abord");
        Assert.IsNull(vaults.Close().Vault);

        // Un secret qui n'ouvre plus la source : rien n'est échangé.
        var refused = vaults.Renew(draft, VaultSecret.Password("pas le bon"));
        Assert.IsFalse(refused.Succeeded);
        Assert.AreEqual(WrongSecret, refused.Message);

        var steps = new List<ConversionProgress>();
        var renewed = vaults.Renew(draft, current, new Collecting(steps));
        Assert.IsTrue(renewed.Succeeded, renewed.Message);
        Assert.AreEqual(scene.Folder.Vault + VaultConversion.RetiredSuffix, renewed.Destination, "le rapport donne l'ancien coffre");
        Assert.Contains("à son nom", renewed.Message);
        Assert.IsGreaterThan(0, renewed.Files);
        Assert.IsNotEmpty(steps);
        Assert.IsFalse(Directory.Exists(scene.Folder.Vault + VaultUseCases.RenewedSuffix));

        // Le nouveau mot de passe ouvre le coffre à son nom ; l'ancien, l'ancien coffre seulement.
        Assert.IsNull(vaults.Unlock(scene.Folder.Vault, current, remember: false).Vault);
        Assert.AreEqual(scene.Folder.Vault, vaults.Unlock(scene.Folder.Vault, VaultSecret.Password(RenewedPassword), remember: false).Vault);
        Assert.IsNull(vaults.Close().Vault);
        Assert.AreEqual(renewed.Destination, vaults.Unlock(renewed.Destination!, current, remember: false).Vault);
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Renew(null!, current));
        Assert.ThrowsExactly<ArgumentNullException>(() => vaults.Renew(draft, null!));
    }
}
