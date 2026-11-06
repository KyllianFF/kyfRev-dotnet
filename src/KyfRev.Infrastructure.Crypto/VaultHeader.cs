using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KyfRev.Infrastructure.Crypto;

/// <summary>
/// L'en-tête d'un coffre chiffré : la clé de données (256 bits, aléatoire) y est enveloppée deux fois, par la clé tirée du
/// mot de passe et par celle de la clé de récupération, chaque enveloppe en AES-256-GCM. Rien de secret n'y est en clair.
/// Les réglages du mot de passe sont liés à son enveloppe : les changer dans le fichier fait échouer l'ouverture. Changer
/// de mot de passe ne fait qu'envelopper de nouveau la même clé : aucun fichier n'est réécrit.
/// </summary>
public sealed class VaultHeader
{
    /// <summary>La version du format.</summary>
    public const int Format = 1;

    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int EnvelopeBytes = NonceBytes + PasswordKeys.KeyBytes + TagBytes;
    private const string PasswordSlot = "mot-de-passe";
    private const string RecoverySlot = "recuperation";

    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true };

    private readonly ImmutableBytes _passwordEnvelope;
    private readonly ImmutableBytes _recoveryEnvelope;

    private VaultHeader(string vaultId, PasswordParameters password, ImmutableBytes passwordEnvelope, ImmutableBytes recoveryEnvelope)
    {
        VaultId = vaultId;
        Password = password;
        _passwordEnvelope = passwordEnvelope;
        _recoveryEnvelope = recoveryEnvelope;
    }

    /// <summary>Le coffre.</summary>
    public string VaultId { get; }

    /// <summary>Comment la clé se tire du mot de passe.</summary>
    public PasswordParameters Password { get; }

    /// <summary>
    /// Crée l'en-tête d'un nouveau coffre chiffré : une clé de données neuve, enveloppée par le mot de passe et par une clé de
    /// récupération neuve, rendue écrite pour être notée. La clé de données est à effacer après usage.
    /// </summary>
    /// <exception cref="ArgumentException">Le mot de passe est vide, ou les réglages sont trop faibles.</exception>
    public static (VaultHeader Header, byte[] DataKey, string RecoveryKey) Create(string vaultId, string password, PasswordParameters parameters) =>
        Create(vaultId, password, parameters, enforcesStrength: true);

    /// <summary>Relit un en-tête.</summary>
    /// <exception cref="VaultCryptoException">Il est illisible.</exception>
    public static VaultHeader Read(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var root = JsonNode.Parse(json) as JsonObject ?? throw new FormatException("objet attendu");
            if (Value<int>(root, "format") != Format)
            {
                throw new VaultCryptoException("Cet en-tête vient d'une version plus récente du coffre chiffré.");
            }
            var password = root["motDePasse"] as JsonObject ?? throw new FormatException("« motDePasse » attendu");
            var recovery = root["recuperation"] as JsonObject ?? throw new FormatException("« recuperation » attendu");
            var parameters = new PasswordParameters(
                Value<string>(password, "derivation"),
                Value<int>(password, "memoireKio"),
                Value<int>(password, "passes"),
                new ImmutableBytes(Convert.FromBase64String(Value<string>(password, "sel"))));
            var passwordEnvelope = new ImmutableBytes(Convert.FromBase64String(Value<string>(password, "enveloppe")));
            var recoveryEnvelope = new ImmutableBytes(Convert.FromBase64String(Value<string>(recovery, "enveloppe")));
            if (passwordEnvelope.Length != EnvelopeBytes || recoveryEnvelope.Length != EnvelopeBytes)
            {
                throw new FormatException("enveloppe de mauvaise taille");
            }
            return new VaultHeader(Value<string>(root, "coffre"), parameters, passwordEnvelope, recoveryEnvelope);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            throw new VaultCryptoException("L'en-tête du coffre chiffré est illisible : le coffre ne s'ouvre pas.", exception);
        }
    }

    /// <summary>Écrit l'en-tête.</summary>
    public byte[] Write()
    {
        var root = new JsonObject
        {
            ["format"] = Format,
            ["coffre"] = VaultId,
            ["contenu"] = "aes-256-gcm",
            ["motDePasse"] = new JsonObject
            {
                ["derivation"] = Password.Algorithm,
                ["memoireKio"] = Password.MemoryKib,
                ["passes"] = Password.Passes,
                ["sel"] = Convert.ToBase64String(Password.Salt.ToArray()),
                ["enveloppe"] = Convert.ToBase64String(_passwordEnvelope.ToArray()),
            },
            ["recuperation"] = new JsonObject { ["enveloppe"] = Convert.ToBase64String(_recoveryEnvelope.ToArray()) },
        };
        return Encoding.UTF8.GetBytes(root.ToJsonString(Written).ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>La clé de données, ouverte par le mot de passe ; à effacer après usage.</summary>
    /// <exception cref="VaultCryptoException">Le mot de passe n'est pas le bon.</exception>
    public byte[] Open(string password)
    {
        var wrapping = PasswordKeys.Derive(password, Password);
        try
        {
            return Unwrap(wrapping, _passwordEnvelope, Associated(PasswordSlot, VaultId, Password))
                ?? throw new VaultCryptoException("Mot de passe incorrect.");
        }
        finally
        {
            PasswordKeys.Clear(wrapping);
        }
    }

    /// <summary>La clé de données, ouverte par la clé de récupération écrite ; à effacer après usage.</summary>
    /// <exception cref="VaultCryptoException">La clé porte une faute de frappe, ou n'est pas celle de ce coffre.</exception>
    public byte[] OpenWithRecovery(string recovery)
    {
        var key = RecoveryKey.Parse(recovery) ?? throw new VaultCryptoException("Cette clé de récupération porte une faute de frappe : relisez-la.");
        var wrapping = RecoveryKey.WrappingKey(key, VaultId);
        try
        {
            return Unwrap(wrapping, _recoveryEnvelope, Associated(RecoverySlot, VaultId, null))
                ?? throw new VaultCryptoException("Cette clé de récupération n'est pas celle de ce coffre, ou plus la bonne.");
        }
        finally
        {
            PasswordKeys.Clear(key);
            PasswordKeys.Clear(wrapping);
        }
    }

    /// <summary>Le même en-tête, la clé de données enveloppée par un nouveau mot de passe.</summary>
    /// <exception cref="ArgumentException">Le mot de passe est vide, ou les réglages sont trop faibles.</exception>
    public VaultHeader WithPassword(ReadOnlySpan<byte> dataKey, string password, PasswordParameters parameters)
    {
        Check(password, parameters, enforcesStrength: true);
        var wrapping = PasswordKeys.Derive(password, parameters);
        try
        {
            return new VaultHeader(VaultId, parameters, Wrap(wrapping, dataKey, Associated(PasswordSlot, VaultId, parameters)), _recoveryEnvelope);
        }
        finally
        {
            PasswordKeys.Clear(wrapping);
        }
    }

    /// <summary>Le même en-tête avec une clé de récupération neuve, rendue écrite ; l'ancienne n'ouvre plus rien.</summary>
    public (VaultHeader Header, string RecoveryKey) WithNewRecovery(ReadOnlySpan<byte> dataKey)
    {
        var (envelope, written) = NewRecovery(VaultId, dataKey);
        return (new VaultHeader(VaultId, Password, _passwordEnvelope, envelope), written);
    }

    /// <summary>Crée un en-tête, avec des réglages faibles permis pour les essais.</summary>
    internal static (VaultHeader Header, byte[] DataKey, string RecoveryKey) Create(string vaultId, string password, PasswordParameters parameters, bool enforcesStrength)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultId);
        Check(password, parameters, enforcesStrength);
        var dataKey = RandomNumberGenerator.GetBytes(PasswordKeys.KeyBytes);
        var wrapping = PasswordKeys.Derive(password, parameters);
        try
        {
            var passwordEnvelope = Wrap(wrapping, dataKey, Associated(PasswordSlot, vaultId, parameters));
            var (recoveryEnvelope, written) = NewRecovery(vaultId, dataKey);
            return (new VaultHeader(vaultId, parameters, passwordEnvelope, recoveryEnvelope), dataKey, written);
        }
        finally
        {
            PasswordKeys.Clear(wrapping);
        }
    }

    private static void Check(string password, PasswordParameters parameters, bool enforcesStrength)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(parameters);
        if (enforcesStrength && !PasswordKeys.IsStrong(parameters))
        {
            throw new ArgumentException("Ces réglages sont trop faibles pour un coffre chiffré.", nameof(parameters));
        }
    }

    private static (ImmutableBytes Envelope, string Written) NewRecovery(string vaultId, ReadOnlySpan<byte> dataKey)
    {
        var key = RecoveryKey.Create();
        var wrapping = RecoveryKey.WrappingKey(key, vaultId);
        try
        {
            return (Wrap(wrapping, dataKey, Associated(RecoverySlot, vaultId, null)), RecoveryKey.Format(key));
        }
        finally
        {
            PasswordKeys.Clear(key);
            PasswordKeys.Clear(wrapping);
        }
    }

    private static ImmutableBytes Wrap(ReadOnlySpan<byte> wrapping, ReadOnlySpan<byte> dataKey, byte[] associated)
    {
        var envelope = new byte[EnvelopeBytes];
        var nonce = envelope.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(wrapping, TagBytes);
        aes.Encrypt(nonce, dataKey, envelope.AsSpan(NonceBytes, PasswordKeys.KeyBytes), envelope.AsSpan(NonceBytes + PasswordKeys.KeyBytes), associated);
        return new ImmutableBytes(envelope);
    }

    private static byte[]? Unwrap(ReadOnlySpan<byte> wrapping, ImmutableBytes envelope, byte[] associated)
    {
        var bytes = envelope.ToArray();
        var dataKey = new byte[PasswordKeys.KeyBytes];
        try
        {
            using var aes = new AesGcm(wrapping, TagBytes);
            aes.Decrypt(bytes.AsSpan(0, NonceBytes), bytes.AsSpan(NonceBytes, PasswordKeys.KeyBytes), bytes.AsSpan(NonceBytes + PasswordKeys.KeyBytes), dataKey, associated);
            return dataKey;
        }
        catch (AuthenticationTagMismatchException)
        {
            PasswordKeys.Clear(dataKey);
            return null;
        }
    }

    /// <summary>Ce qui se lie à une enveloppe : son rôle, le coffre, et pour le mot de passe ses réglages.</summary>
    private static byte[] Associated(string slot, string vaultId, PasswordParameters? parameters) =>
        Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"kyfrev enveloppe v{Format}|{slot}|{vaultId}|{parameters?.Description}"));

    private static T Value<T>(JsonObject parent, string key) =>
        parent[key] is JsonValue value ? value.GetValue<T>() : throw new FormatException($"« {key} » attendu");
}
