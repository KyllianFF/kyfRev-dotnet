using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// Un fichier du coffre chiffré : <c>KYF</c>, la version, un nonce de 96 bits neuf à chaque écriture, puis le contenu en
/// AES-256-GCM et son étiquette. Les données associées lient le chiffrement au coffre et à l'identifiant du fichier : un
/// fichier recopié sous un autre nom, ou dans un autre coffre, ne s'ouvre plus.
/// </summary>
public static class SealedFile
{
    /// <summary>La version du format.</summary>
    public const byte Version = 1;

    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private static readonly byte[] Magic = "KYF"u8.ToArray();
    private static readonly int HeaderBytes = Magic.Length + 1 + NonceBytes;

    /// <summary>Ce qu'ajoute le chiffrement à la taille d'un contenu.</summary>
    public static int Overhead => HeaderBytes + TagBytes;

    /// <summary>Chiffre un contenu.</summary>
    /// <param name="key">La clé de données du coffre.</param>
    /// <param name="vaultId">L'identifiant du coffre.</param>
    /// <param name="fileId">L'identifiant logique du fichier.</param>
    /// <param name="plaintext">Le contenu.</param>
    public static byte[] Seal(ReadOnlySpan<byte> key, string vaultId, string fileId, ReadOnlySpan<byte> plaintext)
    {
        var sealedBytes = new byte[HeaderBytes + plaintext.Length + TagBytes];
        Magic.CopyTo(sealedBytes, 0);
        sealedBytes[Magic.Length] = Version;
        var nonce = sealedBytes.AsSpan(Magic.Length + 1, NonceBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, sealedBytes.AsSpan(HeaderBytes, plaintext.Length), sealedBytes.AsSpan(HeaderBytes + plaintext.Length), Associated(vaultId, fileId));
        return sealedBytes;
    }

    /// <summary>Déchiffre un contenu ; rien n'est rendu s'il a été altéré, échangé ou chiffré avec une autre clé.</summary>
    /// <exception cref="VaultCryptoException">Le fichier ne s'ouvre pas avec cette clé, sous ce nom, dans ce coffre.</exception>
    public static byte[] Open(ReadOnlySpan<byte> key, string vaultId, string fileId, ReadOnlySpan<byte> sealedBytes)
    {
        if (sealedBytes.Length < HeaderBytes + TagBytes || !sealedBytes[..Magic.Length].SequenceEqual(Magic))
        {
            throw new VaultCryptoException($"« {fileId} » n'est pas un fichier du coffre chiffré.");
        }
        if (sealedBytes[Magic.Length] != Version)
        {
            throw new VaultCryptoException($"« {fileId} » vient d'une version plus récente du coffre chiffré.");
        }
        var length = sealedBytes.Length - HeaderBytes - TagBytes;
        var plaintext = new byte[length];
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(sealedBytes.Slice(Magic.Length + 1, NonceBytes), sealedBytes.Slice(HeaderBytes, length), sealedBytes[^TagBytes..], plaintext, Associated(vaultId, fileId));
            return plaintext;
        }
        catch (AuthenticationTagMismatchException exception)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new VaultCryptoException($"« {fileId} » ne s'ouvre pas : il a été modifié, déplacé, ou la clé n'est pas la bonne.", exception);
        }
    }

    /// <summary>Les données associées : le format, sa version, le coffre et le fichier, chacun précédé de sa longueur.</summary>
    private static byte[] Associated(string vaultId, string fileId)
    {
        ArgumentNullException.ThrowIfNull(vaultId);
        ArgumentNullException.ThrowIfNull(fileId);
        var vault = Encoding.UTF8.GetBytes(vaultId);
        var file = Encoding.UTF8.GetBytes(fileId);
        var associated = new byte[Magic.Length + 1 + 4 + vault.Length + 4 + file.Length];
        var span = associated.AsSpan();
        Magic.CopyTo(span);
        span[Magic.Length] = Version;
        span = span[(Magic.Length + 1)..];
        BinaryPrimitives.WriteInt32BigEndian(span, vault.Length);
        vault.CopyTo(span[4..]);
        span = span[(4 + vault.Length)..];
        BinaryPrimitives.WriteInt32BigEndian(span, file.Length);
        file.CopyTo(span[4..]);
        return associated;
    }
}
