using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>Comment une clé se dérive d'un mot de passe : l'algorithme et ses réglages, gardés dans l'en-tête du coffre.</summary>
/// <param name="Algorithm"><c>argon2id</c> (par défaut) ou <c>pbkdf2-sha256</c>.</param>
/// <param name="MemoryKib">Mémoire d'Argon2id, en Kio ; 0 pour PBKDF2.</param>
/// <param name="Passes">Passes d'Argon2id, ou itérations de PBKDF2.</param>
/// <param name="Salt">Le sel, aléatoire, propre au coffre.</param>
public sealed record PasswordParameters(string Algorithm, int MemoryKib, int Passes, ImmutableBytes Salt)
{
    /// <summary>Ce qui se lie au chiffrement de la clé : changer un réglage dans l'en-tête fait échouer l'ouverture.</summary>
    internal string Description => string.Create(CultureInfo.InvariantCulture, $"{Algorithm};{MemoryKib};{Passes};{Convert.ToBase64String(Salt.ToArray())}");
}

/// <summary>Des octets qui ne changent plus, comparés par leur contenu.</summary>
public sealed class ImmutableBytes : IEquatable<ImmutableBytes>
{
    private readonly byte[] _bytes;

    /// <summary>Une copie de ces octets.</summary>
    public ImmutableBytes(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    /// <summary>Leur nombre.</summary>
    public int Length => _bytes.Length;

    /// <summary>Une copie, à effacer après usage si elle est secrète.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <inheritdoc/>
    public bool Equals(ImmutableBytes? other) => other is not null && CryptographicOperations.FixedTimeEquals(_bytes, other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ImmutableBytes);

    /// <inheritdoc/>
    public override int GetHashCode() => _bytes.Length;
}

/// <summary>
/// La clé qui enveloppe la clé de données, tirée du mot de passe. Argon2id par défaut (libsodium, par NSec), réglé pour
/// durer de 0,5 à 1 s et jamais en dessous de 64 Mio et 3 passes ; PBKDF2-HMAC-SHA256 à 600 000 itérations au moins, en
/// option. libsodium ne calcule Argon2id que sur un seul fil : la RFC 9106 en compte quatre, les passes compensent.
/// </summary>
public static class PasswordKeys
{
    /// <summary>Argon2id.</summary>
    public const string Argon2id = "argon2id";

    /// <summary>PBKDF2-HMAC-SHA256.</summary>
    public const string Pbkdf2 = "pbkdf2-sha256";

    /// <summary>La taille du sel.</summary>
    public const int SaltBytes = 16;

    /// <summary>La taille d'une clé.</summary>
    public const int KeyBytes = 32;

    /// <summary>Le moins de mémoire accepté pour Argon2id : 64 Mio.</summary>
    public const int MinimumMemoryKib = 64 * 1024;

    /// <summary>Le moins de passes accepté pour Argon2id.</summary>
    public const int MinimumPasses = 3;

    /// <summary>Le moins d'itérations accepté pour PBKDF2.</summary>
    public const int MinimumIterations = 600_000;

    private const int MaximumMemoryKib = 1024 * 1024;
    private const int MaximumPasses = 64;
    private const int MaximumIterations = 10_000_000;

    /// <summary>Argon2id aux réglages minimaux, avec un sel neuf.</summary>
    public static PasswordParameters NewArgon2id() => new(Argon2id, MinimumMemoryKib, MinimumPasses, new ImmutableBytes(RandomNumberGenerator.GetBytes(SaltBytes)));

    /// <summary>PBKDF2 aux réglages minimaux, avec un sel neuf.</summary>
    public static PasswordParameters NewPbkdf2() => new(Pbkdf2, 0, MinimumIterations, new ImmutableBytes(RandomNumberGenerator.GetBytes(SaltBytes)));

    /// <summary>
    /// Argon2id réglé sur ce poste : 64 Mio, et des passes ajoutées tant que la dérivation dure moins que
    /// <paramref name="target"/> (0,5 s par défaut), sans dépasser 1 s.
    /// </summary>
    public static PasswordParameters Calibrated(TimeSpan? target = null)
    {
        var wanted = target ?? TimeSpan.FromMilliseconds(500);
        var parameters = NewArgon2id();
        var watch = Stopwatch.StartNew();
        Clear(Derive("calibrage", parameters));
        var once = watch.Elapsed;
        if (once >= wanted || once <= TimeSpan.Zero)
        {
            return parameters;
        }
        var perPass = once / MinimumPasses;
        var passes = (int)Math.Min(MaximumPasses, Math.Min(Math.Ceiling(wanted / perPass), Math.Floor(TimeSpan.FromSeconds(1) / perPass)));
        return parameters with { Passes = Math.Max(MinimumPasses, passes) };
    }

    /// <summary>Les réglages sont assez forts pour un nouveau coffre, et pas absurdes pour un coffre relu.</summary>
    public static bool IsStrong(PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return IsReadable(parameters) && parameters.Algorithm switch
        {
            Argon2id => parameters.MemoryKib >= MinimumMemoryKib && parameters.Passes >= MinimumPasses,
            _ => parameters.Passes >= MinimumIterations,
        };
    }

    /// <summary>La clé de <paramref name="password"/>, à effacer après usage (<see cref="Clear"/>).</summary>
    /// <exception cref="VaultCryptoException">Les réglages sont illisibles.</exception>
    public static byte[] Derive(string password, PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!IsReadable(parameters))
        {
            throw new VaultCryptoException("Les réglages du mot de passe sont illisibles : le coffre ne s'ouvre pas.");
        }
        var secret = Encoding.UTF8.GetBytes(password);
        var salt = parameters.Salt.ToArray();
        try
        {
            if (parameters.Algorithm == Pbkdf2)
            {
                return Rfc2898DeriveBytes.Pbkdf2(secret, salt, parameters.Passes, HashAlgorithmName.SHA256, KeyBytes);
            }
            var algorithm = PasswordBasedKeyDerivationAlgorithm.Argon2id(new Argon2Parameters
            {
                DegreeOfParallelism = 1,
                MemorySize = parameters.MemoryKib,
                NumberOfPasses = parameters.Passes,
            });
            return algorithm.DeriveBytes(secret, salt, KeyBytes);
        }
        finally
        {
            Clear(secret);
        }
    }

    /// <summary>Efface une clé de la mémoire.</summary>
    public static void Clear(byte[]? key)
    {
        if (key is not null)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static bool IsReadable(PasswordParameters parameters) => parameters.Salt.Length == SaltBytes && parameters.Algorithm switch
    {
        Argon2id => parameters.MemoryKib is > 0 and <= MaximumMemoryKib && parameters.Passes is > 0 and <= MaximumPasses,
        Pbkdf2 => parameters.MemoryKib == 0 && parameters.Passes is > 0 and <= MaximumIterations,
        _ => false,
    };
}
