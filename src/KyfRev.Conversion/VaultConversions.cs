using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;

namespace KyfRev.Conversion;

/// <summary>
/// La conversion des coffres pour l'application (<see cref="IVaultConversion"/>), par <see cref="VaultConversion"/> : chiffrer
/// un coffre en clair en deux temps (préparer, puis écrire), déchiffrer un coffre chiffré, toujours vers un nouveau dossier.
/// </summary>
/// <param name="parameters">
/// Les réglages de la dérivation du mot de passe ; chaque préparation tire son propre sel. Rien : Argon2id, calibré sur ce
/// poste à chaque préparation.
/// </param>
public sealed class VaultConversions(PasswordParameters? parameters = null) : IVaultConversion
{
    /// <summary>Prépare le chiffrement du coffre en clair <paramref name="source"/> (<see cref="VaultConversion.Prepare"/>).</summary>
    /// <exception cref="VaultCryptoException">Ce n'est pas un coffre en clair, il ne se lit pas, ou le mot de passe est trop court : rien n'est préparé.</exception>
    public EncryptionDraft PrepareEncryption(string source, string password)
    {
        EncryptionPreparation prepared;
        try
        {
            prepared = VaultConversion.Prepare(source, password, Parameters());
        }
        catch (Exception exception) when (exception is not VaultCryptoException && VaultConversion.IsRefusal(exception))
        {
            // Le coffre ne se lit plus (un disque retiré, un dossier interdit) : un refus, pas une panne de l'application.
            throw new VaultCryptoException($"Le coffre ne se lit pas ({exception.Message}) : rien n'a été préparé.", exception);
        }
        return prepared.Plan ?? throw new VaultCryptoException(prepared.Refusal);
    }

    /// <inheritdoc/>
    public string FreeDestination(string parent, string name) => VaultConversion.FreeDestination(parent, name);

    /// <summary>Chiffre le coffre préparé dans le nouveau dossier <paramref name="destination"/>.</summary>
    /// <param name="draft">Le chiffrement préparé par <see cref="PrepareEncryption"/>.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    /// <exception cref="ArgumentException">Le chiffrement n'a pas été préparé par cette conversion.</exception>
    public ConversionReport Encrypt(EncryptionDraft draft, string destination, IProgress<ConversionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft is not EncryptionPlan plan)
        {
            throw new ArgumentException("Ce chiffrement n'a pas été préparé par cette conversion.", nameof(draft));
        }
        return Report(VaultConversion.Encrypt(plan, destination, progress), destination);
    }

    /// <inheritdoc/>
    public ConversionReport Decrypt(string source, string destination, VaultSecret secret, IProgress<ConversionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Report(VaultConversion.Decrypt(source, destination, secret.Text, progress: progress, isRecoveryKey: secret.IsRecoveryKey), destination);
    }

    private static ConversionReport Report(ConversionResult result, string destination) =>
        new(result.Succeeded, result.Message, destination, result.Files);

    /// <summary>Les réglages d'une préparation : ceux donnés, avec un sel neuf ; à défaut, ceux que ce poste calibre.</summary>
    private PasswordParameters Parameters() =>
        parameters is null
            ? PasswordKeys.Calibrated()
            : parameters with { Salt = new ImmutableBytes(RandomNumberGenerator.GetBytes(PasswordKeys.SaltBytes)) };
}
