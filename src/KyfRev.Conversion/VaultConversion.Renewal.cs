using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Conversion;

/// <summary>
/// Renouveler toutes les clés d'un coffre chiffré (rangée 29B) : une nouvelle clé de données, enveloppée par un mot de passe
/// (l'actuel ou un nouveau) et par une nouvelle clé de récupération, et tout le coffre chiffré de nouveau avec elle, dans un
/// dossier voisin, relu et comparé. Puis les deux dossiers s'échangent : le coffre garde son nom, l'ancien devient
/// « … (ancien, à supprimer) ». Une ancienne copie de l'en-tête (une sauvegarde, l'historique d'un dossier synchronisé)
/// s'ouvre encore avec l'ancien secret, mais ne donne plus la clé du coffre d'aujourd'hui.
/// </summary>
public static partial class VaultConversion
{
    /// <summary>Le suffixe du coffre écrit avec ses nouvelles clés, avant l'échange.</summary>
    public const string RenewedSuffix = " (nouvelles clés)";

    /// <summary>Le suffixe de l'ancien coffre, après l'échange.</summary>
    public const string RetiredSuffix = " (ancien, à supprimer)";

    /// <summary>
    /// Prépare le renouvellement du coffre chiffré <paramref name="source"/> : le secret actuel est vérifié, puis l'en-tête du
    /// coffre renouvelé créé, avec une nouvelle clé de données, le mot de passe <paramref name="password"/> et une nouvelle clé
    /// de récupération. Rien n'est écrit.
    /// </summary>
    /// <returns>Le renouvellement préparé, à libérer par qui le reçoit ; ou le refus.</returns>
    public static EncryptionPreparation PrepareRenewal(string source, string secret, bool? isRecoveryKey, string password, PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(password);
        source = Path.GetFullPath(source);
        if (!EncryptedVault.IsEncrypted(source))
        {
            return EncryptionPreparation.Refused($"« {source} » n'est pas un coffre chiffré (il n'a pas de {EncryptedVault.HeaderFile}).");
        }
        if (OpenDataKey(source, secret, isRecoveryKey) is not { } current)
        {
            return EncryptionPreparation.Refused(WrongSecret);
        }
        PasswordKeys.Clear(current);
        if (password.Length < MinimumPasswordLength)
        {
            return EncryptionPreparation.Refused($"Le mot de passe doit faire au moins {MinimumPasswordLength} caractères.");
        }
        // Le même identifiant de coffre : c'est le même coffre, avec d'autres clés.
        var (header, dataKey, recovery) = VaultHeader.Create(EncryptedVault.ReadHeader(source).VaultId, password, parameters);
        EncryptionPlan? plan = null;
        try
        {
            plan = new EncryptionPlan(source, header, dataKey, password, recovery);
            var prepared = EncryptionPreparation.Ready(plan);
            plan = null;
            return prepared;
        }
        finally
        {
            plan?.Dispose();
            PasswordKeys.Clear(dataKey);
        }
    }

    /// <summary>
    /// Écrit le coffre de <paramref name="plan"/> (préparé par <see cref="PrepareRenewal"/>) avec ses nouvelles clés dans le
    /// nouveau dossier <paramref name="destination"/>, relu et comparé ; la source reste intacte. Elle est lue avec son
    /// secret actuel, <paramref name="secret"/>.
    /// </summary>
    public static ConversionResult Renew(EncryptionPlan plan, string secret, bool? isRecoveryKey, string destination, IProgress<ConversionProgress>? progress = null, Action<string>? beforeVerifying = null, VaultStorage? disk = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(secret);
        disk ??= DiskStorage.Instance;
        destination = Path.GetFullPath(destination);
        if (!EncryptedVault.IsEncrypted(plan.Source))
        {
            return ConversionResult.Refused($"« {plan.Source} » n'est pas un coffre chiffré (il n'a pas de {EncryptedVault.HeaderFile}).");
        }
        if (Refusal(plan.Source, destination) is { } refusal)
        {
            return refusal;
        }
        if (plan.CopyOfDataKey() is not { } newKey)
        {
            return ConversionResult.Refused(Abandoned);
        }
        if (OpenDataKey(plan.Source, secret, isRecoveryKey) is not { } oldKey)
        {
            PasswordKeys.Clear(newKey);
            return ConversionResult.Refused(WrongSecret);
        }
        string? temporary = null;
        ConversionMark? mark = null;
        try
        {
            using var held = HoldSource(plan.Source);
            using var source = EncryptedVault.Open(plan.Source, oldKey, DiskStorage.Instance);
            temporary = Temporary(destination, disk);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            using (var renewed = EncryptedVault.Create(temporary, plan.Header, newKey, disk))
            {
                mark = ConversionMark.Take(temporary);
                foreach (var folder in source.Folders)
                {
                    renewed.AddFolder(folder);
                    folders.Add(folder);
                }
                var files = source.Files;
                var written = 0;
                foreach (var path in files)
                {
                    var content = source.Read(path);
                    renewed.Write(path, content);
                    expected[path] = SHA256.HashData(content);
                    written++;
                    progress?.Report(new ConversionProgress(ConversionStep.Writing, written, files.Count));
                }
                renewed.Flush();
            }
            beforeVerifying?.Invoke(temporary);
            if (Reread(temporary, EncryptedVault.ReadHeader(temporary).Open(plan.Password), expected, folders, progress) is { } mismatch)
            {
                return ConversionResult.Refused(mismatch + Discard(temporary, destination, disk, mark));
            }
            mark.Remove(disk);
            disk.MoveDirectory(temporary, destination);
            return new ConversionResult(true, $"Coffre chiffré avec ses nouvelles clés dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié.", expected.Count, plan.RecoveryKey);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            return ConversionResult.Refused($"Le renouvellement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
        }
        finally
        {
            mark?.Dispose();
            PasswordKeys.Clear(newKey);
            PasswordKeys.Clear(oldKey);
        }
    }

    /// <summary>
    /// Échange les dossiers après un renouvellement : <paramref name="source"/> devient « … (ancien, à supprimer) », puis
    /// <paramref name="renewed"/> prend son nom. Si le second déplacement échoue, le premier est défait.
    /// </summary>
    /// <returns>Le dossier de l'ancien coffre ; ou le refus, rien n'ayant changé.</returns>
    public static (string? Retired, string? Refusal) Swap(string source, string renewed, VaultStorage? disk = null)
    {
        disk ??= DiskStorage.Instance;
        source = Path.GetFullPath(source);
        renewed = Path.GetFullPath(renewed);
        var parent = Path.GetDirectoryName(source) ?? throw new ArgumentException("Le coffre n'a pas de dossier parent.", nameof(source));
        string retired;
        try
        {
            retired = FreeDestination(parent, Path.GetFileName(source) + RetiredSuffix);
            disk.MoveDirectory(source, retired);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"L'ancien coffre n'a pas pu être renommé ({exception.Message}) : le coffre renouvelé reste dans « {renewed} ».");
        }
        try
        {
            disk.MoveDirectory(renewed, source);
            return (retired, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            disk.MoveDirectory(retired, source);
            return (null, $"Le coffre renouvelé n'a pas pu prendre son nom ({exception.Message}) : il reste dans « {renewed} », l'ancien n'a pas changé.");
        }
    }
}
