namespace KyfRev.Infrastructure.Vault.Tests.Reference;

/// <summary>
/// SplitMix64, identique au générateur d'entrées de <c>tools/reference-vectors</c> : il permet de
/// régénérer côté C# exactement les cas joués côté Rust.
/// </summary>
internal sealed class SplitMix(ulong seed)
{
    private ulong _state = seed;

    public ulong Next()
    {
        unchecked
        {
            _state += 0x9E37_79B9_7F4A_7C15;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9;
            z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EB;
            return z ^ (z >> 31);
        }
    }

    public int Below(int n) => (int)(Next() % (ulong)n);

    /// <summary>Assemblage de 0 à <paramref name="max"/> - 1 jetons (<c>assembler</c> côté Rust).</summary>
    public string Assemble(string[] tokens, int max)
    {
        var count = Below(max);
        return string.Concat(Enumerable.Range(0, count).Select(_ => tokens[Below(tokens.Length)]));
    }

    /// <summary>Liste de 0 à 6 textes (<c>liste</c> côté Rust).</summary>
    public List<string> List()
    {
        var count = Below(7);
        return [.. Enumerable.Range(0, count).Select(_ => Assemble(CoffreTokens.NameTokens, 5))];
    }

    public string Pick(string[] values) => values[Below(values.Length)];
}
