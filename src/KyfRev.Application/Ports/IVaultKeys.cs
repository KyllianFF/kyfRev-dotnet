using System.Security.Cryptography;

namespace KyfRev.Application.Ports;

/// <summary>
/// Les clés des coffres chiffrés : reconnaître un coffre chiffré, tirer sa clé de données de son mot de passe ou de sa
/// clé de récupération, et la retenir sur ce poste (Windows la protège pour la session de l'utilisateur, elle n'est
/// jamais écrite dans le coffre), ou l'oublier ; changer son mot de passe ou sa clé de récupération, sans réécrire aucune
/// fiche.
/// </summary>
public interface IVaultKeys
{
    /// <summary>Vrai si <paramref name="root"/> contient un coffre chiffré : il ne s'ouvre qu'avec sa clé.</summary>
    bool IsEncrypted(string root);

    /// <summary>
    /// La clé que ce poste a retenue pour le coffre de <paramref name="root"/>, vérifiée sur son index ; rien s'il n'en a
    /// retenu aucune, si elle a été retenue dans une autre session de Windows, ou si elle n'ouvre plus ce coffre.
    /// </summary>
    /// <exception cref="OperationException">L'index ne se lit pas : la clé reste retenue, et le refus dit pourquoi.</exception>
    VaultKey? Remembered(string root);

    /// <summary>La clé de données du coffre de <paramref name="root"/>, tirée de son mot de passe ou de sa clé de récupération.</summary>
    /// <exception cref="OperationException">
    /// Mauvais mot de passe, clé de récupération mal recopiée ou d'un autre coffre, en-tête absent ou illisible.
    /// </exception>
    VaultKey KeyOf(string root, VaultSecret secret);

    /// <summary>Retient la clé du coffre de <paramref name="root"/> sur ce poste ; faux si elle n'a pas pu l'être.</summary>
    bool Remember(string root, VaultKey key);

    /// <summary>Oublie la clé retenue pour le coffre de <paramref name="root"/>, s'il y en a une ; faux si cela n'a pas pu être écrit.</summary>
    bool Forget(string root);

    /// <summary>
    /// Vrai si ce poste a retenu une clé pour le coffre de <paramref name="root"/>, dans ce dossier. Elle n'est pas
    /// essayée sur son index : seul <see cref="Remembered"/> dit si elle l'ouvre encore.
    /// </summary>
    bool IsRemembered(string root);

    /// <summary>
    /// Change le mot de passe du coffre chiffré de <paramref name="root"/> : sa clé de données, la même, est enveloppée
    /// par le nouveau mot de passe, et seul son en-tête est réécrit, d'un coup. Aucune fiche n'est réécrite ; la clé de
    /// récupération et la clé retenue sur ce poste l'ouvrent toujours, l'ancien mot de passe plus.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré.</param>
    /// <param name="current">Son mot de passe actuel, ou sa clé de récupération.</param>
    /// <param name="newPassword">Le nouveau mot de passe, d'au moins <see cref="VaultSecret.MinimumPasswordLength"/> caractères.</param>
    /// <exception cref="OperationException">
    /// Le secret actuel n'ouvre pas le coffre, le nouveau mot de passe est trop court, ou l'en-tête ne se lit pas ou ne
    /// s'écrit pas : rien n'a changé.
    /// </exception>
    void ChangePassword(string root, VaultSecret current, string newPassword);

    /// <summary>
    /// Donne au coffre chiffré de <paramref name="root"/> une clé de récupération neuve, rendue écrite (14 groupes de
    /// quatre caractères) pour être notée : l'ancienne ne l'ouvre plus. Seul son en-tête est réécrit, d'un coup ; le mot
    /// de passe et la clé retenue sur ce poste l'ouvrent toujours.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération actuelle.</param>
    /// <exception cref="OperationException">
    /// Le secret n'ouvre pas le coffre, ou l'en-tête ne se lit pas ou ne s'écrit pas : rien n'a changé.
    /// </exception>
    string NewRecoveryKey(string root, VaultSecret secret);
}

/// <summary>Sans chiffrement : aucun coffre n'est chiffré (l'application assemblée sans lui, les essais).</summary>
public sealed class NoVaultKeys : IVaultKeys
{
    /// <summary>Faux : aucun coffre n'est chiffré.</summary>
    public bool IsEncrypted(string root) => false;

    /// <summary>Rien : aucune clé n'est retenue.</summary>
    public VaultKey? Remembered(string root) => null;

    /// <summary>Jamais appelé : aucun coffre n'est chiffré.</summary>
    /// <exception cref="NotSupportedException">Toujours.</exception>
    public VaultKey KeyOf(string root, VaultSecret secret) => throw new NotSupportedException("Aucun coffre chiffré ne s'ouvre ici.");

    /// <summary>Faux : rien n'est retenu.</summary>
    public bool Remember(string root, VaultKey key) => false;

    /// <summary>Vrai : il n'y a rien à oublier.</summary>
    public bool Forget(string root) => true;

    /// <summary>Faux : aucune clé n'est retenue.</summary>
    public bool IsRemembered(string root) => false;

    /// <summary>Jamais appelé : aucun coffre n'est chiffré.</summary>
    /// <exception cref="NotSupportedException">Toujours.</exception>
    public void ChangePassword(string root, VaultSecret current, string newPassword) => throw new NotSupportedException("Aucun coffre chiffré ne s'ouvre ici.");

    /// <summary>Jamais appelé : aucun coffre n'est chiffré.</summary>
    /// <exception cref="NotSupportedException">Toujours.</exception>
    public string NewRecoveryKey(string root, VaultSecret secret) => throw new NotSupportedException("Aucun coffre chiffré ne s'ouvre ici.");
}

/// <summary>Ce qui ouvre un coffre chiffré : son mot de passe, ou sa clé de récupération. Son texte ne s'affiche jamais.</summary>
public sealed class VaultSecret
{
    /// <summary>Le moins de caractères qu'un nouveau mot de passe doit avoir.</summary>
    public const int MinimumPasswordLength = 12;

    private VaultSecret(string text, bool isRecoveryKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
        IsRecoveryKey = isRecoveryKey;
    }

    /// <summary>Le mot de passe, ou la clé de récupération telle qu'elle a été recopiée.</summary>
    public string Text { get; }

    /// <summary>Vrai pour une clé de récupération, faux pour un mot de passe.</summary>
    public bool IsRecoveryKey { get; }

    /// <summary>Le mot de passe du coffre.</summary>
    public static VaultSecret Password(string password) => new(password, isRecoveryKey: false);

    /// <summary>La clé de récupération du coffre, telle qu'elle a été recopiée (tirets, espaces et casse indifférents).</summary>
    public static VaultSecret RecoveryKey(string key) => new(key, isRecoveryKey: true);

    /// <summary>Ce que c'est, jamais ce que c'est : « mot de passe » ou « clé de récupération ».</summary>
    public override string ToString() => IsRecoveryKey ? "clé de récupération" : "mot de passe";
}

/// <summary>La clé de données d'un coffre chiffré, effacée de la mémoire dès qu'elle ne sert plus.</summary>
public sealed class VaultKey : IDisposable
{
    private readonly byte[] _bytes;
    private bool _disposed;

    /// <summary>Prend les octets de la clé : c'est elle qui les effacera.</summary>
    public VaultKey(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
    }

    /// <summary>Les octets de la clé, tant qu'elle n'est pas effacée.</summary>
    /// <exception cref="ObjectDisposedException">La clé est effacée.</exception>
    public ReadOnlySpan<byte> Bytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes;
        }
    }

    /// <summary>Efface la clé.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_bytes);
        _disposed = true;
    }
}
