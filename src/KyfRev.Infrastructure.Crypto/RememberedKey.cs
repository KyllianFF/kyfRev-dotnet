using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// « Se souvenir sur cet ordinateur » : la clé de données protégée par Windows (DPAPI) pour la session de l'utilisateur. Elle
/// reste sur ce poste, jamais dans le coffre ; un autre compte, ou le disque ailleurs, ne peut pas s'en servir. L'entropie
/// propre à kyfRev et au coffre empêche une autre application de la même session de la lire par erreur.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RememberedKey
{
    /// <summary>La clé de données protégée pour cette session Windows.</summary>
    public static byte[] Protect(ReadOnlySpan<byte> dataKey, string vaultId)
    {
        var key = dataKey.ToArray();
        try
        {
            return ProtectedData.Protect(key, Entropy(vaultId), DataProtectionScope.CurrentUser);
        }
        finally
        {
            PasswordKeys.Clear(key);
        }
    }

    /// <summary>La clé de données retenue ; rien si elle vient d'une autre session, d'un autre coffre, ou si elle est abîmée.</summary>
    public static byte[]? Unprotect(byte[] remembered, string vaultId)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        try
        {
            var key = ProtectedData.Unprotect(remembered, Entropy(vaultId), DataProtectionScope.CurrentUser);
            if (key.Length == PasswordKeys.KeyBytes)
            {
                return key;
            }
            PasswordKeys.Clear(key);
            return null;
        }
        catch (CryptographicException)
        {
            // Une clé retenue illisible se redemande : le mot de passe ouvre toujours le coffre.
            return null;
        }
    }

    private static byte[] Entropy(string vaultId)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultId);
        return SHA256.HashData(Encoding.UTF8.GetBytes($"kyfRev coffre retenu v1|{vaultId}"));
    }
}
