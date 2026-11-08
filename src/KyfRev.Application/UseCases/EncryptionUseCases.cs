using KyfRev.Application.Ports;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Le chiffrement du coffre ouvert : changer son mot de passe, lui donner une clé de récupération neuve, retenir sa clé
/// sur ce poste ou l'oublier. Seul l'en-tête du coffre est réécrit : aucune fiche ne l'est. Chaque cas d'usage porte sur
/// le coffre ouvert, et refuse sans lui (<see cref="VaultHost.NoVaultOpen"/>) ou s'il est en clair. La dérivation d'un
/// mot de passe prend de l'ordre d'une seconde : l'écran les appelle hors de son fil.
/// </summary>
/// <param name="host">Le coffre ouvert.</param>
/// <param name="keys">Les clés des coffres chiffrés.</param>
public sealed class EncryptionUseCases(VaultHost host, IVaultKeys keys)
{
    /// <summary>Message de refus quand le coffre ouvert est en clair.</summary>
    public const string NotEncrypted = "Le coffre ouvert n'est pas chiffré.";

    /// <summary>Message de refus quand la clé préparée est celle d'un autre coffre que l'ouvert.</summary>
    public const string OtherVault = "Cette clé de récupération a été préparée pour un autre coffre : elle n'a pas été enregistrée.";

    /// <summary>Le dossier du coffre ouvert ; rien sans coffre ouvert.</summary>
    public string? Vault => host.Root;

    /// <summary>Vrai si ce poste a retenu une clé pour le coffre ouvert ; faux sans coffre ouvert.</summary>
    public bool IsRemembered => host.Root is { } root && keys.IsRemembered(root);

    /// <summary>
    /// Change le mot de passe du coffre ouvert : la même clé de données est enveloppée par le nouveau mot de passe, et seul
    /// l'en-tête est réécrit. La clé de récupération et la clé retenue sur ce poste l'ouvrent toujours, l'ancien mot de passe
    /// plus. Refusé si les deux saisies diffèrent, si le mot de passe est trop court ou si le secret actuel n'est pas le bon.
    /// </summary>
    /// <param name="current">Le mot de passe actuel, ou la clé de récupération.</param>
    /// <param name="newPassword">Le nouveau mot de passe, d'au moins <see cref="VaultSecret.MinimumPasswordLength"/> caractères.</param>
    /// <param name="confirmation">Le nouveau mot de passe, tapé une seconde fois.</param>
    public Outcome<Done> ChangePassword(VaultSecret current, string newPassword, string confirmation)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(newPassword);
        ArgumentNullException.ThrowIfNull(confirmation);
        return OnEncrypted(root =>
        {
            if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            {
                return Outcome.Refused<Done>("Les deux mots de passe ne sont pas les mêmes.");
            }
            if (newPassword.Length < VaultSecret.MinimumPasswordLength)
            {
                return Outcome.Refused<Done>($"Le mot de passe doit faire au moins {VaultSecret.MinimumPasswordLength} caractères.");
            }
            keys.ChangePassword(root, current, newPassword);
            return Outcome.Done();
        });
    }

    /// <summary>
    /// Prépare une clé de récupération neuve pour le coffre ouvert, rendue écrite (14 groupes de quatre caractères) pour
    /// être notée, sans rien écrire : tant qu'elle n'est pas enregistrée (<see cref="SaveRecoveryKey"/>), l'ancienne reste
    /// la seule qui ouvre le coffre.
    /// </summary>
    /// <param name="secret">Le mot de passe, ou la clé de récupération actuelle.</param>
    public Outcome<RecoveryKeyDraft> DraftRecoveryKey(VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return OnEncrypted(root => Outcome.Of(keys.DraftRecoveryKey(root, secret)));
    }

    /// <summary>
    /// Enregistre la clé de récupération préparée, une fois notée : l'ancienne n'ouvre plus le coffre ; le mot de passe et
    /// la clé retenue sur ce poste, si. Refusé si le coffre ouvert n'est plus le sien (un verrouillage, un autre coffre) ou
    /// si son en-tête a changé depuis : rien ne change alors.
    /// </summary>
    /// <param name="draft">La clé préparée.</param>
    public Outcome<Done> SaveRecoveryKey(RecoveryKeyDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return OnEncrypted(root =>
        {
            if (!string.Equals(root, draft.Root, StringComparison.OrdinalIgnoreCase))
            {
                return Outcome.Refused<Done>(OtherVault);
            }
            keys.SaveRecoveryKey(draft);
            return Outcome.Done();
        });
    }

    /// <summary>
    /// Vérifie que <paramref name="secret"/> ouvre le coffre ouvert, sans rien écrire ni retenir : avant de le fermer pour le
    /// déchiffrer, par exemple. La clé tirée du secret est effacée aussitôt.
    /// </summary>
    /// <param name="secret">Le mot de passe, ou la clé de récupération.</param>
    public Outcome<Done> Check(VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return OnEncrypted(root =>
        {
            using var key = keys.KeyOf(root, secret);
            return Outcome.Done();
        });
    }

    /// <summary>
    /// Retient la clé du coffre ouvert sur ce poste, protégée par Windows pour la session de l'utilisateur : elle l'ouvrira
    /// au démarrage sans rien demander. La clé tirée du secret est effacée aussitôt retenue.
    /// </summary>
    /// <param name="secret">Le mot de passe, ou la clé de récupération.</param>
    public Outcome<Done> Remember(VaultSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return OnEncrypted(root =>
        {
            using var key = keys.KeyOf(root, secret);
            return keys.Remember(root, key)
                ? Outcome.Done()
                : Outcome.Refused<Done>("La clé n'a pas pu être retenue sur ce poste.");
        });
    }

    /// <summary>
    /// Oublie la clé retenue sur ce poste pour le coffre ouvert : au démarrage suivant, il demandera son mot de passe. Rien
    /// à oublier n'est pas un refus.
    /// </summary>
    public Outcome<Done> Forget()
    {
        if (host.Root is not { } root)
        {
            return Outcome.Refused<Done>(VaultHost.NoVaultOpen);
        }
        return keys.Forget(root)
            ? Outcome.Done()
            : Outcome.Refused<Done>("La clé retenue n'a pas pu être oubliée : le fichier des clés de ce poste ne s'écrit pas.");
    }

    /// <summary>
    /// Exécute <paramref name="action"/> sur le dossier du coffre ouvert, s'il est chiffré. Un refus des clés devient un
    /// <see cref="Outcome{T}"/> refusé, avec son message. Le coffre n'est pas tenu pendant ce temps : la dérivation ne
    /// bloque pas les autres opérations.
    /// </summary>
    private Outcome<T> OnEncrypted<T>(Func<string, Outcome<T>> action)
    {
        if (host.Root is not { } root)
        {
            return Outcome.Refused<T>(VaultHost.NoVaultOpen);
        }
        if (!keys.IsEncrypted(root))
        {
            return Outcome.Refused<T>(NotEncrypted);
        }
        try
        {
            return action(root);
        }
        catch (OperationException exception)
        {
            return Outcome.Refused<T>(exception.Message);
        }
    }
}
