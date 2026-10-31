using System.Numerics;

namespace KyfRev.Domain.Randomness;

/// <summary>
/// Générateur pseudo-aléatoire ChaCha8, compatible bit à bit avec <c>ChaCha8Rng::seed_from_u64</c>
/// du crate Rust <c>rand_chacha</c> 0.10 (graine étendue par PCG32 comme dans <c>rand_core</c>
/// 0.10, compteur de bloc et identifiant de flux sur 64 bits, tampon de quatre blocs).
/// </summary>
/// <remarks>
/// Il sert au tirage reproductible des rappels, pas à la cryptographie.
/// </remarks>
internal sealed class ChaCha8Random
{
    private const int Words = 16;
    private const int BufferedBlocks = 4;
    private const int DoubleRounds = 4;
    private const ulong Pcg32Multiplier = 0x5851_F42D_4C95_7F2D;
    private const ulong Pcg32Increment = 0xA176_54E4_6FBE_17F3;

    private readonly uint[] _input = new uint[Words];
    private readonly uint[] _buffer = new uint[Words * BufferedBlocks];
    private ulong _blockCounter;
    private int _index;

    private ChaCha8Random(ReadOnlySpan<uint> key)
    {
        // « expand 32-byte k »
        _input[0] = 0x6170_7865;
        _input[1] = 0x3320_646E;
        _input[2] = 0x7962_2D32;
        _input[3] = 0x6B20_6574;
        key.CopyTo(_input.AsSpan(4, 8));
        _index = _buffer.Length;
    }

    /// <summary>Équivalent de <c>SeedableRng::seed_from_u64</c> : la clé est remplie par PCG32.</summary>
    public static ChaCha8Random FromSeed(ulong seed)
    {
        Span<uint> key = stackalloc uint[8];
        var state = seed;
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = Pcg32(ref state);
        }
        return new ChaCha8Random(key);
    }

    /// <summary>Entier de 64 bits : mot de poids faible puis mot de poids fort du tampon.</summary>
    public ulong NextUInt64()
    {
        if (_index >= _buffer.Length)
        {
            Refill();
            _index = 0;
        }
        var low = _buffer[_index];
        var high = _buffer[_index + 1];
        _index += 2;
        return ((ulong)high << 32) | low;
    }

    /// <summary>Entier uniforme dans <c>[0, n)</c>, sans biais grâce au rejet.</summary>
    public int NextBounded(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        var bound = (ulong)n;
        var remainder = ((ulong.MaxValue % bound) + 1) % bound;
        var limit = ulong.MaxValue - remainder;
        while (true)
        {
            var x = NextUInt64();
            if (x <= limit)
            {
                return (int)(x % bound);
            }
        }
    }

    private static uint Pcg32(ref ulong state)
    {
        state = unchecked((state * Pcg32Multiplier) + Pcg32Increment);
        var xorShifted = (uint)(((state >> 18) ^ state) >> 27);
        var rotation = (int)(state >> 59);
        return BitOperations.RotateRight(xorShifted, rotation);
    }

    private void Refill()
    {
        Span<uint> x = stackalloc uint[Words];
        for (var block = 0; block < BufferedBlocks; block++)
        {
            _input[12] = (uint)_blockCounter;
            _input[13] = (uint)(_blockCounter >> 32);
            _input.CopyTo(x);
            for (var round = 0; round < DoubleRounds; round++)
            {
                QuarterRound(x, 0, 4, 8, 12);
                QuarterRound(x, 1, 5, 9, 13);
                QuarterRound(x, 2, 6, 10, 14);
                QuarterRound(x, 3, 7, 11, 15);
                QuarterRound(x, 0, 5, 10, 15);
                QuarterRound(x, 1, 6, 11, 12);
                QuarterRound(x, 2, 7, 8, 13);
                QuarterRound(x, 3, 4, 9, 14);
            }
            for (var w = 0; w < Words; w++)
            {
                _buffer[(block * Words) + w] = unchecked(x[w] + _input[w]);
            }
            _blockCounter++;
        }
    }

    private static void QuarterRound(Span<uint> x, int a, int b, int c, int d)
    {
        unchecked
        {
            x[a] += x[b];
            x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 16);
            x[c] += x[d];
            x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 12);
            x[a] += x[b];
            x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 8);
            x[c] += x[d];
            x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 7);
        }
    }
}
