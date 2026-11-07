using System.Runtime.Versioning;
using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Domain.Calendar;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Les coffres de ce poste, en clair ou chiffrés. Un coffre en clair s'ouvre sur le disque, comme avant. Un coffre chiffré
/// s'ouvre avec sa clé de données, tirée de son mot de passe ou de sa clé de récupération, sur son stockage chiffré
/// (<see cref="EncryptedStorage"/>), que la session prend et libère en se fermant : sa copie de la clé est alors effacée.
/// La clé peut être retenue sur ce poste, protégée par Windows (<see cref="RememberedKey"/>), jamais dans le coffre.
/// </summary>
/// <param name="plain">Les coffres en clair.</param>
/// <param name="onStorage">
/// Les coffres sur un stockage donné, qu'ils prennent : la racine de composition passe celui du coffre, qui ne connaît pas
/// le chiffrement.
/// </param>
/// <param name="remembered">Les clés retenues sur ce poste.</param>
/// <param name="disk">Le disque où sont posés les coffres chiffrés.</param>
[SupportedOSPlatform("windows")]
public sealed class EncryptedVaults(IVaultStore plain, Func<VaultStorage, IVaultStore> onStorage, RememberedKeys remembered, VaultStorage disk)
    : IVaultStore, IVaultKeys
{
    /// <summary>Ouvre un coffre en clair.</summary>
    public IVault Open(string root, Moment now) => plain.Open(root, now);

    /// <summary>Crée un coffre en clair : un coffre se chiffre ensuite, vers un nouveau dossier.</summary>
    public IVault Initialize(string root, Moment now) => plain.Initialize(root, now);

    /// <inheritdoc/>
    public bool SameLocation(string first, string second) => plain.SameLocation(first, second);

    /// <summary>
    /// Ouvre le coffre chiffré de <paramref name="root"/> : son index est lu avec la clé, puis la session s'ouvre sur son
    /// stockage, qu'elle libère en se fermant. Si elle ne s'ouvre pas, le stockage est libéré tout de suite.
    /// </summary>
    /// <exception cref="VaultCryptoException">La clé n'ouvre pas l'index, ou il est absent ou altéré.</exception>
    public IVault Open(string root, VaultKey key, Moment now)
    {
        ArgumentNullException.ThrowIfNull(key);
        EncryptedStorage? storage = null;
        try
        {
            storage = EncryptedStorage.Open(root, key.Bytes, disk);
            var vault = onStorage(storage).Open(root, now);
            storage = null;
            return vault;
        }
        finally
        {
            storage?.Dispose();
        }
    }

    /// <summary>Vrai si <paramref name="root"/> contient l'en-tête d'un coffre chiffré.</summary>
    public bool IsEncrypted(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return disk.FileExists(Path.Combine(root, EncryptedVault.HeaderFile));
    }

    /// <summary>
    /// La clé retenue pour ce coffre et ce dossier, si Windows la rend dans cette session et si elle ouvre l'index ; une
    /// clé qui ne l'ouvre plus est oubliée. Un en-tête illisible ne rend rien : le mot de passe, demandé, dira pourquoi.
    /// </summary>
    public VaultKey? Remembered(string root)
    {
        VaultHeader header;
        try
        {
            header = EncryptedVault.ReadHeader(root, disk);
        }
        catch (VaultCryptoException)
        {
            return null;
        }
        if (remembered.Find(header.VaultId, root) is not { } kept || RememberedKey.Unprotect(kept, header.VaultId) is not { } bytes)
        {
            return null;
        }
        var key = new VaultKey(bytes);
        try
        {
            EncryptedVault.Open(root, key.Bytes, disk).Dispose();
            return key;
        }
        catch (VaultCryptoException)
        {
            key.Dispose();
            remembered.Forget(root);
            return null;
        }
    }

    /// <summary>La clé de données du coffre, tirée de son mot de passe ou de sa clé de récupération (<see cref="VaultHeader"/>).</summary>
    /// <exception cref="VaultCryptoException">
    /// Mauvais mot de passe, clé de récupération mal recopiée ou d'un autre coffre, en-tête absent ou illisible.
    /// </exception>
    public VaultKey KeyOf(string root, VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var header = EncryptedVault.ReadHeader(root, disk);
        return new VaultKey(secret.IsRecoveryKey ? header.OpenWithRecovery(secret.Text) : header.Open(secret.Text));
    }

    /// <summary>
    /// Retient la clé, protégée par Windows pour la session de l'utilisateur et liée à ce coffre ; faux si l'en-tête ne se
    /// lit plus, si Windows refuse de la protéger, ou si le fichier n'a pas pu être écrit.
    /// </summary>
    public bool Remember(string root, VaultKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            var vaultId = EncryptedVault.ReadHeader(root, disk).VaultId;
            return remembered.Keep(vaultId, root, RememberedKey.Protect(key.Bytes, vaultId));
        }
        catch (Exception exception) when (exception is VaultCryptoException or CryptographicException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Forget(string root) => remembered.Forget(root);
}
