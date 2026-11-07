using KyfRev.Domain.Calendar;
using KyfRev.Infrastructure.Vault.Adapters;
using KyfRev.Infrastructure.Vault.Tests.Support;

namespace KyfRev.Infrastructure.Vault.Tests;

/// <summary>
/// Un coffre chiffré ouvert prend son stockage : il le libère en se fermant, et la clé avec lui. S'il ne s'ouvre pas, ou
/// s'il ne l'a pas pris, le stockage reste à l'appelant.
/// </summary>
[TestClass]
public sealed class OwnedStorageTests
{
    private static readonly Moment Now = new(new DateOnly(2026, 11, 7), new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void A_vault_releases_the_storage_it_took_when_it_closes_and_only_that_one()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using (new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now))
        {
        }
        var settings = Path.Combine(folder.Root, ".suivi", "coffre.json");

        // Le coffre le prend : le libérer encore à la fin ne change rien.
        using var owned = TemporaryVault.OpenEncrypted(folder.Root);
        using (var vault = new VaultStore(storage: owned, ownsStorage: true).Open(folder.Root, Now))
        {
            Assert.IsNotEmpty(owned.ReadAllBytes(settings));
        }
        Assert.ThrowsExactly<ObjectDisposedException>(() => owned.ReadAllBytes(settings), "fermé, le coffre a libéré son stockage");

        using var kept = TemporaryVault.OpenEncrypted(folder.Root);
        using (new VaultStore(storage: kept).Open(folder.Root, Now))
        {
        }
        Assert.IsNotEmpty(kept.ReadAllBytes(settings), "un stockage qui n'a pas été pris reste à l'appelant");
    }

    [TestMethod]
    public void A_vault_that_does_not_open_leaves_the_storage_to_the_caller()
    {
        using var folder = TemporaryVault.Empty(StorageKind.Encrypted);
        using var open = new VaultStore(storage: folder.Storage).Initialize(folder.Root, Now);
        var settings = Path.Combine(folder.Root, ".suivi", "coffre.json");

        using var refused = TemporaryVault.OpenEncrypted(folder.Root);
        var exception = Assert.ThrowsExactly<VaultException>(() => new VaultStore(storage: refused, ownsStorage: true).Open(folder.Root, Now));

        Assert.AreEqual(VaultErrorKind.AlreadyOpen, exception.Kind);
        Assert.IsNotEmpty(refused.ReadAllBytes(settings));
    }
}
