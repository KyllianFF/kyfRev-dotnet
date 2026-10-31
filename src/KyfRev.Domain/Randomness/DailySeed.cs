using System.Globalization;
using System.Text;

namespace KyfRev.Domain.Randomness;

/// <summary>
/// Graine stable d'un coffre pour un jour donné : rouvrir l'application le même jour redonne le
/// même tirage de rappels et la même citation du jour.
/// </summary>
public static class DailySeed
{
    private const ulong FnvOffsetBasis = 0xCBF2_9CE4_8422_2325;
    private const ulong FnvPrime = 0x0000_0100_0000_01B3;

    /// <summary>FNV-1a 64 bits du texte « identifiant du coffre|AAAA-MM-JJ », en UTF-8.</summary>
    public static ulong For(string vaultId, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(vaultId);
        var text = string.Create(CultureInfo.InvariantCulture, $"{vaultId}|{day:yyyy-MM-dd}");
        var hash = FnvOffsetBasis;
        foreach (var octet in Encoding.UTF8.GetBytes(text))
        {
            hash = unchecked((hash ^ octet) * FnvPrime);
        }
        return hash;
    }
}
