using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;

namespace KyfRev.Conversion;

/// <summary>
/// Un chiffrement préparé par <see cref="VaultConversion.Prepare"/>, pas encore fait : le coffre en clair à chiffrer, l'en-tête
/// du futur coffre chiffré, la clé de récupération à noter, et ce que la conversion écrira. Il garde sa propre copie de la clé
/// de données, effacée quand il est libéré ; chaque conversion travaille sur une copie de la sienne : refusée ou échouée, elle
/// le laisse prêt pour un nouvel essai.
/// </summary>
public sealed class EncryptionPlan : EncryptionDraft
{
    private readonly Lock _gate = new();
    private readonly byte[] _dataKey;
    private bool _disposed;

    /// <summary>Le chiffrement préparé : il prend une copie de <paramref name="dataKey"/>, que l'appelant efface ensuite.</summary>
    internal EncryptionPlan(string source, VaultHeader header, ReadOnlySpan<byte> dataKey, string password, string recoveryKey)
        : base(source, recoveryKey)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        _dataKey = dataKey.ToArray();
    }

    /// <summary>L'identifiant du futur coffre chiffré : celui du coffre en clair, ou un neuf s'il n'en avait pas.</summary>
    public string VaultId => Header.VaultId;

    /// <summary>
    /// L'en-tête du futur coffre chiffré : la clé de données y est enveloppée par le mot de passe et par la clé de
    /// récupération. Rien de secret n'y est en clair.
    /// </summary>
    public VaultHeader Header { get; }

    /// <summary>Le mot de passe : la vérification rouvre avec lui l'en-tête écrit, pour qu'une erreur d'enveloppe se voie.</summary>
    internal string Password { get; }

    /// <summary>Une copie de la clé de données, à effacer après usage ; rien une fois le chiffrement préparé libéré.</summary>
    internal byte[]? CopyOfDataKey()
    {
        lock (_gate)
        {
            return _disposed ? null : (byte[])_dataKey.Clone();
        }
    }

    /// <summary>Efface la clé de données : le chiffrement préparé ne chiffre plus rien.</summary>
    /// <param name="disposing">Vrai à l'appel de <see cref="EncryptionDraft.Dispose()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            PasswordKeys.Clear(_dataKey);
            _disposed = true;
        }
    }
}

/// <summary>
/// Ce que rend <see cref="VaultConversion.Prepare"/> : le chiffrement préparé (<see cref="Plan"/>), ou pourquoi il ne l'est
/// pas (<see cref="Refusal"/>).
/// </summary>
public sealed class EncryptionPreparation
{
    private EncryptionPreparation(EncryptionPlan? plan, string refusal)
    {
        Plan = plan;
        Refusal = refusal;
    }

    /// <summary>Le chiffrement préparé, à libérer par qui le reçoit ; rien s'il est refusé.</summary>
    public EncryptionPlan? Plan { get; }

    /// <summary>Pourquoi rien n'est préparé, à montrer tel quel ; vide si le chiffrement est préparé.</summary>
    public string Refusal { get; }

    /// <summary>Le chiffrement préparé, qui passe à qui reçoit ce résultat.</summary>
    internal static EncryptionPreparation Ready(EncryptionPlan plan) => new(plan, string.Empty);

    /// <summary>Le refus : rien n'est préparé.</summary>
    internal static EncryptionPreparation Refused(string refusal) => new(null, refusal);
}
