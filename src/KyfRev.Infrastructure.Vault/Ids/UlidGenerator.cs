using System.Security.Cryptography;

namespace KyfRev.Infrastructure.Vault.Ids;

/// <summary>
/// Identifiants ULID (26 caractères base 32 de Crockford : 48 bits d'horodatage en millisecondes,
/// puis 80 bits aléatoires), comme ceux de la version 0.x. Les identifiants tirés dans la même
/// milliseconde sont croissants : l'ordre des identifiants suit l'ordre de création.
/// </summary>
public sealed class UlidGenerator
{
    private const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly byte[] _random = new byte[10];
    private readonly Queue<string>? _replay;
    private long _lastMilliseconds = -1;

    /// <summary>Générateur réglé sur l'horloge donnée.</summary>
    public UlidGenerator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    private UlidGenerator(IEnumerable<string> replay)
        : this(TimeProvider.System)
    {
        _replay = new Queue<string>(replay);
    }

    /// <summary>Générateur réglé sur l'horloge du système.</summary>
    public static UlidGenerator System { get; } = new(TimeProvider.System);

    /// <summary>
    /// Générateur qui rend, dans l'ordre, des identifiants déjà tirés : sert aux tests qui rejouent
    /// une suite d'opérations de la version 0.x avec ses propres identifiants.
    /// </summary>
    internal static UlidGenerator Replaying(IEnumerable<string> ids) => new(ids);

    /// <summary>Un nouvel identifiant, sans préfixe.</summary>
    public string Next()
    {
        lock (_gate)
        {
            if (_replay is not null)
            {
                return _replay.TryDequeue(out var id) ? id : throw new InvalidOperationException("Plus aucun identifiant à rejouer.");
            }
            var milliseconds = _time.GetUtcNow().ToUnixTimeMilliseconds();
            if (milliseconds > _lastMilliseconds)
            {
                RandomNumberGenerator.Fill(_random);
                _lastMilliseconds = milliseconds;
            }
            else
            {
                Increment();
                milliseconds = _lastMilliseconds;
            }
            return Encode(milliseconds, _random);
        }
    }

    private void Increment()
    {
        for (var i = _random.Length - 1; i >= 0; i--)
        {
            if (++_random[i] != 0)
            {
                return;
            }
        }
        // Les 80 bits ont débordé : la milliseconde suivante est empruntée.
        _lastMilliseconds++;
    }

    private static string Encode(long milliseconds, byte[] random)
    {
        Span<char> text = stackalloc char[26];
        for (var i = 9; i >= 0; i--)
        {
            text[i] = Crockford[(int)(milliseconds & 31)];
            milliseconds >>= 5;
        }
        // 80 bits aléatoires : 16 caractères de 5 bits, du poids fort au poids faible.
        var bits = new UInt128(
            ((ulong)random[0] << 8) | random[1],
            ((ulong)random[2] << 56) | ((ulong)random[3] << 48) | ((ulong)random[4] << 40) | ((ulong)random[5] << 32)
                | ((ulong)random[6] << 24) | ((ulong)random[7] << 16) | ((ulong)random[8] << 8) | random[9]);
        for (var i = 25; i >= 10; i--)
        {
            text[i] = Crockford[(int)(ulong)(bits & 31)];
            bits >>= 5;
        }
        return new string(text);
    }
}
