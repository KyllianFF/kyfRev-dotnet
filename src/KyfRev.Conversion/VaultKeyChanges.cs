using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Conversion;

/// <summary>
/// Changer les clés d'un coffre chiffré fermé : seul son en-tête est réécrit, d'un seul geste ; ses fichiers et sa clé de
/// données ne changent pas. Le coffre reste tenu tout du long, comme pendant une conversion : l'application ne l'ouvre pas
/// pendant ce temps.
/// </summary>
public static class VaultKeyChanges
{
    /// <summary>Le dossier donné n'est pas un coffre chiffré.</summary>
    internal const string NotEncrypted = "Ce dossier n'est pas un coffre chiffré : rien n'a été écrit.";

    /// <summary>La nouvelle clé n'a pas été confirmée.</summary>
    internal const string NotNoted = "La nouvelle clé n'a pas été enregistrée : l'ancienne reste valable.";

    /// <summary>
    /// Change le mot de passe du coffre chiffré <paramref name="root"/>. La clé de récupération et la clé retenue sur un poste
    /// restent valables ; une sauvegarde faite avant garde l'ancien mot de passe.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré, fermé.</param>
    /// <param name="secret">Son mot de passe actuel, ou sa clé de récupération.</param>
    /// <param name="newPassword">Le nouveau mot de passe, d'au moins <see cref="VaultConversion.MinimumPasswordLength"/> caractères.</param>
    /// <param name="parameters">Les réglages de la dérivation.</param>
    /// <returns>Ce qui s'est fait, à montrer tel quel ; sur un refus, l'en-tête n'a pas changé.</returns>
    public static ConversionResult ChangePassword(string root, string secret, string newPassword, PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(newPassword);
        ArgumentNullException.ThrowIfNull(parameters);
        return newPassword.Length < VaultConversion.MinimumPasswordLength
            ? ConversionResult.Refused($"Le mot de passe doit faire au moins {VaultConversion.MinimumPasswordLength} caractères : rien n'a été écrit.")
            : Rewrite(root, secret, (header, dataKey) => (
                header.WithPassword(dataKey, newPassword, parameters),
                new ConversionResult(true, "Mot de passe changé. La clé de récupération reste valable ; les sauvegardes faites avant gardent l'ancien mot de passe.")));
    }

    /// <summary>
    /// Remplace la clé de récupération du coffre chiffré <paramref name="root"/>, mais seulement si <paramref name="noted"/>,
    /// qui reçoit la nouvelle clé, confirme qu'elle est rangée : une clé que personne n'a notée ne remplace jamais l'ancienne.
    /// </summary>
    /// <param name="root">Le dossier du coffre chiffré, fermé.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération actuelle.</param>
    /// <param name="noted">Montre la nouvelle clé ; vrai si elle est rangée.</param>
    /// <returns>Ce qui s'est fait, à montrer tel quel ; sur un refus, l'en-tête n'a pas changé.</returns>
    public static ConversionResult NewRecoveryKey(string root, string secret, Func<string, bool> noted)
    {
        ArgumentNullException.ThrowIfNull(noted);
        return Rewrite(root, secret, (header, dataKey) =>
        {
            var (changed, key) = header.WithNewRecovery(dataKey);
            return noted(key)
                ? (changed, new ConversionResult(true, "Nouvelle clé de récupération enregistrée : l'ancienne n'ouvre plus ce coffre, mais les sauvegardes faites avant s'ouvrent encore avec elle."))
                : (null, ConversionResult.Refused(NotNoted));
        });
    }

    private static ConversionResult Rewrite(string root, string secret, Func<VaultHeader, byte[], (VaultHeader? Header, ConversionResult Result)> change)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(secret);
        root = Path.GetFullPath(root);
        if (!EncryptedVault.IsEncrypted(root))
        {
            return ConversionResult.Refused(NotEncrypted);
        }
        byte[]? dataKey = null;
        try
        {
            if (VaultConversion.IsOpenElsewhere(root))
            {
                return ConversionResult.Refused(VaultConversion.OpenInKyfRev);
            }
            using var held = VaultConversion.HoldSource(root);
            var header = EncryptedVault.ReadHeader(root);
            dataKey = VaultConversion.OpenDataKey(root, secret);
            if (dataKey is null)
            {
                return ConversionResult.Refused(VaultConversion.WrongSecret);
            }
            var (changed, result) = change(header, dataKey);
            if (changed is not null)
            {
                DiskStorage.Instance.Replace(Path.Combine(root, EncryptedVault.HeaderFile), changed.Write());
            }
            return result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or VaultCryptoException)
        {
            return ConversionResult.Refused($"{exception.Message} L'en-tête n'a pas changé.");
        }
        finally
        {
            if (dataKey is not null)
            {
                PasswordKeys.Clear(dataKey);
            }
        }
    }
}
