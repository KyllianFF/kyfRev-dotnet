using System.Runtime.Versioning;
using System.Security.Cryptography;
using KyfRev.Application;
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
    /// clé que l'index refuse est oubliée. Un en-tête illisible ne rend rien : le mot de passe, demandé, dira pourquoi.
    /// </summary>
    /// <exception cref="VaultCryptoException">
    /// L'index ne se lit pas (absent, illisible un moment, d'une version plus récente) : la clé reste retenue.
    /// </exception>
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
        catch (VaultCryptoException exception)
        {
            key.Dispose();
            if (exception.InnerException is not AuthenticationTagMismatchException)
            {
                // Ce n'est pas la clé qui est refusée : elle reste retenue, et la raison se dit.
                throw;
            }
            // L'index la refuse : le coffre a été remplacé, ou ce n'était plus sa clé.
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

    /// <summary>
    /// Vrai si ce poste a retenu une clé pour ce coffre (l'identifiant de son en-tête) et ce dossier, sans la demander à
    /// Windows ni l'essayer sur l'index ; faux si l'en-tête ne se lit pas.
    /// </summary>
    public bool IsRemembered(string root)
    {
        ArgumentNullException.ThrowIfNull(root);
        try
        {
            return remembered.Find(EncryptedVault.ReadHeader(root, disk).VaultId, root) is not null;
        }
        catch (VaultCryptoException)
        {
            return false;
        }
    }

    /// <summary>
    /// Change le mot de passe : l'en-tête, ouvert par le secret actuel, enveloppe la même clé de données par le nouveau
    /// mot de passe, avec des réglages tirés sur ce poste (<see cref="PasswordKeys.Calibrated"/>, de l'ordre d'une
    /// seconde), puis il est réécrit d'un coup, par le disque. Aucun objet n'est réécrit, et la clé de récupération comme
    /// la clé retenue ouvrent toujours le coffre.
    /// </summary>
    /// <exception cref="VaultCryptoException">
    /// Le nouveau mot de passe est trop court, le secret actuel n'est pas le bon, ou l'en-tête ne se lit pas ou ne s'écrit
    /// pas : l'en-tête n'a pas changé.
    /// </exception>
    public void ChangePassword(string root, VaultSecret current, string newPassword)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(newPassword);
        if (newPassword.Length < VaultSecret.MinimumPasswordLength)
        {
            throw new VaultCryptoException($"Le mot de passe doit faire au moins {VaultSecret.MinimumPasswordLength} caractères.");
        }
        Rewrap(root, current, (header, dataKey) => (header.WithPassword(dataKey, newPassword, PasswordKeys.Calibrated()), default(Done)));
    }

    /// <summary>
    /// Donne au coffre une clé de récupération neuve : l'en-tête, ouvert par le secret, enveloppe la même clé de données
    /// par elle, puis il est réécrit d'un coup, par le disque. L'ancienne clé de récupération n'ouvre plus rien ; le mot de
    /// passe et la clé retenue, si.
    /// </summary>
    /// <exception cref="VaultCryptoException">
    /// Le secret n'est pas le bon, ou l'en-tête ne se lit pas ou ne s'écrit pas : l'en-tête n'a pas changé.
    /// </exception>
    public string NewRecoveryKey(string root, VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Rewrap(root, secret, (header, dataKey) => header.WithNewRecovery(dataKey));
    }

    /// <summary>
    /// Ouvre l'en-tête du coffre avec <paramref name="secret"/>, l'enveloppe de nouveau par <paramref name="rewrap"/> et
    /// l'écrit à sa place, de façon atomique : par le disque tel quel, jamais par un stockage chiffré. La clé de données
    /// est effacée ensuite, quoi qu'il arrive.
    /// </summary>
    private T Rewrap<T>(string root, VaultSecret secret, Func<VaultHeader, byte[], (VaultHeader Header, T Result)> rewrap)
    {
        var header = EncryptedVault.ReadHeader(root, disk);
        var dataKey = secret.IsRecoveryKey ? header.OpenWithRecovery(secret.Text) : header.Open(secret.Text);
        try
        {
            var (changed, result) = rewrap(header, dataKey);
            try
            {
                disk.Replace(Path.Combine(root, EncryptedVault.HeaderFile), changed.Write());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new VaultCryptoException("L'en-tête du coffre chiffré n'a pas pu être écrit : rien n'a changé.", exception);
            }
            return result;
        }
        finally
        {
            PasswordKeys.Clear(dataKey);
        }
    }
}
