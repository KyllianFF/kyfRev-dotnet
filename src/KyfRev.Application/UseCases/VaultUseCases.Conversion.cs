using KyfRev.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.UseCases;

/// <summary>
/// La conversion du coffre ouvert : le chiffrer, le déchiffrer, toujours vers un nouveau dossier. Le coffre se ferme pour
/// être converti, pour que rien ne s'y écrive pendant la copie ; la conversion ne rouvre rien d'elle-même : l'écran ouvre
/// ensuite le nouveau coffre, ou rouvre l'ancien.
/// </summary>
public sealed partial class VaultUseCases
{
    /// <summary>Le suffixe du dossier d'un coffre chiffré à côté du coffre en clair : « Coffre chiffré ».</summary>
    public const string EncryptedSuffix = " chiffré";

    /// <summary>Le suffixe du dossier d'un coffre déchiffré : « Coffre (en clair) ».</summary>
    public const string DecryptedSuffix = " (en clair)";

    /// <summary>Message de refus quand le coffre ouvert est déjà chiffré.</summary>
    public const string AlreadyEncrypted = "Ce coffre est déjà chiffré.";

    /// <summary>Message de refus quand un coffre est encore ouvert au moment de le convertir.</summary>
    public const string StillOpen = "Le coffre est encore ouvert : il se ferme avant d'être converti.";

    /// <summary>Le dossier des sauvegardes du coffre <paramref name="root"/> : à côté de lui, et en clair pour un coffre en clair.</summary>
    /// <param name="root">Le dossier du coffre.</param>
    public static string BackupsFolderOf(string root) => BackupFolders.For(root);

    /// <summary>
    /// Prépare le chiffrement du coffre ouvert, en clair, sans rien écrire : la clé de récupération du futur coffre chiffré
    /// est rendue pour être notée avant la conversion. Refusé sans coffre ouvert, pour un coffre déjà chiffré, si les deux
    /// saisies diffèrent ou si le mot de passe est trop court. La dérivation prend de l'ordre d'une seconde.
    /// </summary>
    /// <param name="password">Le mot de passe du futur coffre chiffré.</param>
    /// <param name="confirmation">Le mot de passe, tapé une seconde fois.</param>
    public Outcome<EncryptionDraft> PrepareEncryption(string password, string confirmation)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(confirmation);
        string root;
        lock (_gate)
        {
            if (host.Root is not { } open)
            {
                return Outcome.Refused<EncryptionDraft>(VaultHost.NoVaultOpen);
            }
            if (open == _encrypted)
            {
                return Outcome.Refused<EncryptionDraft>(AlreadyEncrypted);
            }
            root = open;
        }
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            return Outcome.Refused<EncryptionDraft>("Les deux mots de passe ne sont pas les mêmes.");
        }
        if (password.Length < VaultSecret.MinimumPasswordLength)
        {
            return Outcome.Refused<EncryptionDraft>($"Le mot de passe doit faire au moins {VaultSecret.MinimumPasswordLength} caractères.");
        }
        try
        {
            return Outcome.Of(_conversion.PrepareEncryption(root, password));
        }
        catch (Exception exception) when (exception is OperationException or NotSupportedException)
        {
            // Un refus de la conversion, ou une application assemblée sans elle.
            return Outcome.Refused<EncryptionDraft>(exception.Message);
        }
    }

    /// <summary>
    /// Ferme le coffre ouvert, en clair ou chiffré, pour le convertir, une fois finie l'opération en cours. Rien n'est
    /// retenu : un coffre chiffré fermé ainsi n'attend pas sa clé, et le coffre à rouvrir au démarrage reste le même. Rien
    /// ne se ferme pendant une ouverture : l'état le dit, le coffre restant ouvert.
    /// </summary>
    public AppState Close()
    {
        string root;
        lock (_gate)
        {
            if (_opening > 0 || host.Root is not { } open)
            {
                return State();
            }
            root = open;
            _encrypted = null;
            host.Close();
        }
        LogVaultClosed(logger, root);
        return State();
    }

    /// <summary>
    /// Chiffre le coffre préparé, fermé, dans un nouveau dossier voisin : « Coffre chiffré », ou « Coffre chiffré (2) » si
    /// le nom est pris. La source reste intacte. Refusé si un coffre est encore ouvert.
    /// </summary>
    /// <param name="draft">Le chiffrement préparé (<see cref="PrepareEncryption"/>).</param>
    /// <param name="progress">Les fichiers chiffrés, puis relus.</param>
    public ConversionReport Encrypt(EncryptionDraft draft, IProgress<ConversionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Convert(draft.Source, Path.GetDirectoryName(draft.Source), EncryptedSuffix, destination => _conversion.Encrypt(draft, destination, progress));
    }

    /// <summary>
    /// Déchiffre le coffre chiffré <paramref name="source"/>, fermé, dans un nouveau dossier de <paramref name="parent"/> :
    /// « Coffre (en clair) », ou « Coffre (en clair) (2) » si le nom est pris. La source reste intacte. Refusé si un coffre
    /// est encore ouvert.
    /// </summary>
    /// <param name="source">Le coffre chiffré.</param>
    /// <param name="parent">Le dossier qui contiendra le coffre en clair.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération.</param>
    /// <param name="progress">Les fichiers déchiffrés, puis relus.</param>
    public ConversionReport Decrypt(string source, string parent, VaultSecret secret, IProgress<ConversionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(secret);
        return Convert(source, parent, DecryptedSuffix, destination => _conversion.Decrypt(source, destination, secret, progress));
    }

    /// <summary>La conversion de <paramref name="source"/> vers le premier dossier libre de <paramref name="parent"/>.</summary>
    private ConversionReport Convert(string source, string? parent, string suffix, Func<string, ConversionReport> convert)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) + suffix;
        if (parent is null)
        {
            return ConversionReport.Refused($"« {source} » n'a pas de dossier parent où écrire sa conversion.", source);
        }
        if (host.Root is not null)
        {
            return ConversionReport.Refused(StillOpen, Path.Combine(parent, name));
        }
        string destination;
        try
        {
            destination = _conversion.FreeDestination(parent, name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ConversionReport.Refused($"Le dossier « {parent} » ne se lit pas ({exception.Message}) : rien n'a été écrit.", Path.Combine(parent, name));
        }
        var report = convert(destination);
        LogVaultConverted(logger, source, report.Destination, report.Succeeded);
        return report;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Coffre {Root} fermé pour être converti.")]
    private static partial void LogVaultClosed(ILogger logger, string root);

    [LoggerMessage(Level = LogLevel.Information, Message = "Conversion du coffre {Source} vers {Destination} : réussie {Succeeded}.")]
    private static partial void LogVaultConverted(ILogger logger, string source, string destination, bool succeeded);
}
