namespace KyfRev.Application.Ports;

/// <summary>
/// La conversion d'un coffre, toujours vers un nouveau dossier : chiffrer un coffre en clair, déchiffrer un coffre
/// chiffré. La source reste intacte, rien n'y est écrit, pas même son verrou ; elle doit être fermée. La copie s'écrit dans
/// un dossier temporaire voisin, chaque fichier y est relu et comparé à l'original, puis le dossier prend le nom de la
/// destination d'un seul geste : une conversion coupée ne laisse jamais de coffre à moitié écrit.
/// </summary>
public interface IVaultConversion
{
    /// <summary>
    /// Prépare le chiffrement du coffre en clair <paramref name="source"/>, sans rien écrire : la clé de données est tirée
    /// au hasard et enveloppée par le mot de passe (une dérivation, de l'ordre d'une seconde) et par une clé de
    /// récupération neuve, rendue écrite pour être notée avant la conversion.
    /// </summary>
    /// <param name="source">Le coffre en clair.</param>
    /// <param name="password">Le mot de passe du coffre chiffré, d'au moins <see cref="VaultSecret.MinimumPasswordLength"/> caractères.</param>
    /// <exception cref="OperationException">Ce n'est pas un coffre en clair, ou le mot de passe est trop court : rien n'est préparé.</exception>
    EncryptionDraft PrepareEncryption(string source, string password);

    /// <summary>
    /// Le premier dossier libre pour une conversion dans <paramref name="parent"/> : <paramref name="name"/>, puis
    /// « <paramref name="name"/> (2) », « (3) »… Libre : ni dossier ni fichier de ce nom.
    /// </summary>
    /// <param name="parent">Le dossier qui contiendra la conversion.</param>
    /// <param name="name">Le nom voulu.</param>
    string FreeDestination(string parent, string name);

    /// <summary>Chiffre le coffre préparé dans le nouveau dossier <paramref name="destination"/>.</summary>
    /// <param name="draft">Le chiffrement préparé (<see cref="PrepareEncryption"/>).</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    ConversionReport Encrypt(EncryptionDraft draft, string destination, IProgress<ConversionProgress>? progress = null);

    /// <summary>
    /// Déchiffre le coffre <paramref name="source"/> dans le nouveau dossier <paramref name="destination"/> : un coffre en
    /// clair, que kyfRev ouvre comme avant.
    /// </summary>
    /// <param name="source">Le coffre chiffré.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="secret">Son mot de passe, ou sa clé de récupération.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    ConversionReport Decrypt(string source, string destination, VaultSecret secret, IProgress<ConversionProgress>? progress = null);
}

/// <summary>Sans conversion : l'application assemblée sans elle, les essais. Rien ne se prépare, rien ne se convertit.</summary>
public sealed class NoVaultConversion : IVaultConversion
{
    /// <summary>Le refus de toute conversion.</summary>
    public const string Unavailable = "La conversion d'un coffre n'est pas disponible ici.";

    /// <summary>Jamais : la conversion n'est pas disponible.</summary>
    /// <exception cref="NotSupportedException">Toujours.</exception>
    public EncryptionDraft PrepareEncryption(string source, string password) => throw new NotSupportedException(Unavailable);

    /// <summary><paramref name="name"/> dans <paramref name="parent"/>, tel quel.</summary>
    public string FreeDestination(string parent, string name) => Path.Combine(parent, name);

    /// <summary>Refusé : la conversion n'est pas disponible.</summary>
    public ConversionReport Encrypt(EncryptionDraft draft, string destination, IProgress<ConversionProgress>? progress = null) => ConversionReport.Refused(Unavailable, destination);

    /// <summary>Refusé : la conversion n'est pas disponible.</summary>
    public ConversionReport Decrypt(string source, string destination, VaultSecret secret, IProgress<ConversionProgress>? progress = null) => ConversionReport.Refused(Unavailable, destination);
}

/// <summary>
/// Un chiffrement préparé, pas encore fait (<see cref="IVaultConversion.PrepareEncryption"/>) : le coffre à chiffrer, la
/// clé de récupération à noter, et ce que la conversion écrira. Sa clé de données est effacée quand il est libéré :
/// abandonné, il ne change rien.
/// </summary>
/// <param name="source">Le coffre en clair à chiffrer.</param>
/// <param name="recoveryKey">La clé de récupération du futur coffre chiffré, en 14 groupes de quatre caractères.</param>
public abstract class EncryptionDraft(string source, string recoveryKey) : IDisposable
{
    /// <summary>Le coffre en clair à chiffrer.</summary>
    public string Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>La clé de récupération du futur coffre chiffré, à noter avant la conversion.</summary>
    public string RecoveryKey { get; } = recoveryKey ?? throw new ArgumentNullException(nameof(recoveryKey));

    /// <summary>Efface la clé de données : le chiffrement préparé ne sert plus.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Ce que c'est, jamais la clé.</summary>
    public override string ToString() => "chiffrement préparé";

    /// <summary>Efface ce que le chiffrement préparé garde de secret.</summary>
    /// <param name="disposing">Vrai à l'appel de <see cref="Dispose()"/>.</param>
    protected abstract void Dispose(bool disposing);
}

/// <summary>L'étape d'une conversion.</summary>
public enum ConversionStep
{
    /// <summary>Les fichiers sont écrits, chiffrés ou déchiffrés, dans le dossier temporaire.</summary>
    Writing,

    /// <summary>Chaque fichier écrit est relu et comparé à l'original.</summary>
    Verifying,
}

/// <summary>Où en est une conversion : <paramref name="Done"/> fichiers sur <paramref name="Total"/>, à l'étape <paramref name="Step"/>.</summary>
/// <param name="Step">L'étape.</param>
/// <param name="Done">Les fichiers faits à cette étape.</param>
/// <param name="Total">Les fichiers à faire.</param>
public readonly record struct ConversionProgress(ConversionStep Step, int Done, int Total);

/// <summary>Ce qu'une conversion a fait, ou pourquoi elle a refusé.</summary>
/// <param name="Succeeded">La destination est écrite et vérifiée.</param>
/// <param name="Message">Ce qui s'est passé, à montrer tel quel.</param>
/// <param name="Destination">Le dossier de la conversion.</param>
/// <param name="Files">Le nombre de fichiers convertis et vérifiés.</param>
public sealed record ConversionReport(bool Succeeded, string Message, string Destination, int Files = 0)
{
    /// <summary>Un refus : rien n'a été écrit dans <paramref name="destination"/>.</summary>
    public static ConversionReport Refused(string message, string destination) => new(false, message, destination);
}
