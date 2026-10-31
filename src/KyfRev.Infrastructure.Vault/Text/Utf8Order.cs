using System.Text;

namespace KyfRev.Infrastructure.Vault.Text;

/// <summary>
/// Ordre des octets UTF-8, c'est-à-dire des points de code : celui des chaînes en Rust, donc de la
/// version 0.x (listes, clés inconnues, chemins). L'ordre ordinal de .NET, sur les unités UTF-16,
/// en diffère au-delà de U+E000.
/// </summary>
internal sealed class Utf8Order : IComparer<string>
{
    public static readonly Utf8Order Instance = new();

    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? string.Empty));
}
