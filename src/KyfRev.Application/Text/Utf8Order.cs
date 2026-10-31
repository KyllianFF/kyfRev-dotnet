using System.Text;

namespace KyfRev.Application.Text;

/// <summary>
/// Ordre des octets UTF-8, c'est-à-dire des points de code : celui des chaînes en Rust, donc de la
/// version 0.x (listes, clés inconnues, chemins). L'ordre ordinal de .NET, sur les unités UTF-16,
/// en diffère au-delà de U+E000.
/// </summary>
public sealed class Utf8Order : IComparer<string>
{
    /// <summary>L'ordre, sans état : une seule instance suffit.</summary>
    public static readonly Utf8Order Instance = new();

    /// <summary>Compare les octets UTF-8 des deux chaînes (rien vaut une chaîne vide).</summary>
    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? string.Empty));
}
