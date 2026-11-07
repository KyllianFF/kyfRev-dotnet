using System.Security.Cryptography;
using System.Text;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// La clé de récupération : 256 bits aléatoires, qui ouvrent le coffre si le mot de passe est oublié. Elle s'écrit en base32
/// de Crockford (sans I, L, O ni U), suivie de quatre caractères de contrôle, en groupes de quatre : 14 groupes à noter. La
/// relecture pardonne les minuscules, les espaces, et prend O pour 0, I et L pour 1 ; une faute de frappe est refusée.
/// </summary>
public static class RecoveryKey
{
    /// <summary>La taille de la clé.</summary>
    public const int KeyBytes = 32;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int DataLength = 52;
    private const int CheckLength = 4;
    private static readonly byte[] Info = "kyfrev recuperation v1"u8.ToArray();

    /// <summary>Une clé neuve, à effacer après usage.</summary>
    public static byte[] Create() => RandomNumberGenerator.GetBytes(KeyBytes);

    /// <summary>La clé écrite pour être notée : <c>K7QF-2MXA-…</c>.</summary>
    public static string Format(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeyBytes)
        {
            throw new ArgumentException("Une clé de récupération fait 32 octets.", nameof(key));
        }
        var text = Encode(key) + Check(key);
        return string.Join('-', Enumerable.Range(0, text.Length / 4).Select(group => text.Substring(group * 4, 4)));
    }

    /// <summary>La clé relue ; rien si le texte n'en est pas une, ou s'il porte une faute de frappe.</summary>
    public static byte[]? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = new StringBuilder();
        foreach (var character in text.ToUpperInvariant())
        {
            switch (character)
            {
                case ' ' or '-' or '\t' or '\r' or '\n':
                    continue;
                case 'O':
                    clean.Append('0');
                    break;
                case 'I' or 'L':
                    clean.Append('1');
                    break;
                default:
                    if (!Alphabet.Contains(character, StringComparison.Ordinal))
                    {
                        return null;
                    }
                    clean.Append(character);
                    break;
            }
        }
        if (clean.Length != DataLength + CheckLength)
        {
            return null;
        }
        var key = Decode(clean.ToString(0, DataLength));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Check(key)), Encoding.ASCII.GetBytes(clean.ToString(DataLength, CheckLength))))
        {
            CryptographicOperations.ZeroMemory(key);
            return null;
        }
        return key;
    }

    /// <summary>La clé qui enveloppe la clé de données, tirée de la clé de récupération et propre à ce coffre.</summary>
    public static byte[] WrappingKey(ReadOnlySpan<byte> recovery, string vaultId)
    {
        ArgumentNullException.ThrowIfNull(vaultId);
        var wrapping = new byte[PasswordKeys.KeyBytes];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, recovery, wrapping, Encoding.UTF8.GetBytes(vaultId), Info);
        return wrapping;
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(DataLength);
        var (buffer, bits) = (0, 0);
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                text.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
        {
            text.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }
        return text.ToString();
    }

    private static byte[] Decode(string text)
    {
        var bytes = new byte[KeyBytes];
        var (buffer, bits, index) = (0, 0, 0);
        foreach (var character in text)
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(character, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8 && index < KeyBytes)
            {
                bytes[index++] = (byte)(buffer >> (bits - 8));
                bits -= 8;
            }
        }
        return bytes;
    }

    /// <summary>Quatre caractères tirés de l'empreinte de la clé : vingt bits contre les fautes de frappe.</summary>
    private static string Check(ReadOnlySpan<byte> key) => Encode(SHA256.HashData(key).AsSpan(0, 3))[..CheckLength];
}
