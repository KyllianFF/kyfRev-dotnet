using System.Security.Cryptography;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// Empreinte d'un fichier : « sha256: » suivi de 64 chiffres hexadécimaux minuscules, calculée
/// sur les octets bruts (BOM et fins de ligne compris), jamais sur un texte réencodé.
/// </summary>
public static class Fingerprint
{
    /// <summary>Préfixe commun à toutes les empreintes.</summary>
    public const string Prefix = "sha256:";

    /// <summary>Empreinte des octets donnés.</summary>
    public static string Of(ReadOnlySpan<byte> content) => Prefix + Convert.ToHexStringLower(SHA256.HashData(content));
}
