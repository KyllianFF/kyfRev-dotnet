using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Vault.Analysis;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Ids;

namespace KyfRev.Conversion;

/// <summary>Ce qu'une conversion a fait, ou pourquoi elle a refusé.</summary>
/// <param name="Succeeded">La destination est écrite et vérifiée.</param>
/// <param name="Message">Ce qui s'est passé, à montrer tel quel.</param>
/// <param name="Files">Le nombre de fichiers convertis et vérifiés.</param>
/// <param name="RecoveryKey">La clé de récupération d'un coffre qu'on vient de chiffrer, à montrer une seule fois.</param>
public sealed record ConversionResult(bool Succeeded, string Message, int Files = 0, string? RecoveryKey = null)
{
    /// <summary>Un refus : rien n'a été écrit.</summary>
    /// <param name="message">Pourquoi, à montrer tel quel.</param>
    public static ConversionResult Refused(string message) => new(false, message);

    /// <summary>Le message, jamais la clé de récupération.</summary>
    public override string ToString() => Message;
}

/// <summary>
/// Chiffrer et déchiffrer un coffre, toujours vers un nouveau dossier (plan de refonte, §4.3). La source reste intacte : rien
/// n'y est écrit, pas même le verrou. La conversion s'écrit d'abord dans un dossier temporaire voisin, chaque fichier y est
/// relu et comparé à l'original par son SHA-256, puis le dossier prend son nom en une seule opération : une conversion coupée
/// ne laisse jamais de coffre à moitié écrit. Toute écriture passe par un <see cref="VaultStorage"/> (le disque, ou dans les
/// essais un disque qui échoue à chaque écriture tour à tour), sauf la pose du marqueur, créé et tenu d'un même geste.
/// </summary>
/// <remarks>
/// <para>
/// Une conversion crée son dossier temporaire, vérifie qu'il est vide, puis y pose un marqueur (<see cref="MarkFile"/>),
/// qu'elle tient et retire juste avant de lui donner le nom de la destination. Elle n'efface que ce dossier-là, le sien :
/// refusée, elle le retire. Deux fenêtres restent sans marqueur : avant sa pose (pour un chiffrement, le temps de poser
/// l'en-tête d'un coffre vide), et entre son retrait et le renommage. Coupée net (arrêt brutal, disque perdu), elle peut
/// laisser son dossier, marqué ou non, et rien ne l'efface ensuite : tant qu'un dossier qui porte le nom exact d'un dossier
/// temporaire de la destination est là, une conversion vers elle refuse et le nomme, pour qu'on le retire à la main. Une
/// source qui porte le marqueur, ou dont un dossier parent le porte, est refusée. Une erreur imprévue laisse le dossier en
/// place, marqué, et la conversion suivante le nomme.
/// </para>
/// <para>
/// Un chiffrement se fait d'un coup (<see cref="Encrypt(string, string, string, PasswordParameters, Action{string}, VaultStorage, IProgress{ConversionProgress})"/>),
/// ou en deux temps : <see cref="Prepare"/> tire la clé de données et la clé de récupération sans rien écrire, pour qu'on
/// note celle-ci, puis <see cref="Encrypt(EncryptionPlan, string, IProgress{ConversionProgress}, Action{string}, VaultStorage)"/>
/// écrit le coffre chiffré. Chaque conversion dit où elle en est : les fichiers écrits, puis les fichiers relus.
/// </para>
/// </remarks>
public static class VaultConversion
{
    /// <summary>La longueur minimale d'un mot de passe de coffre.</summary>
    public const int MinimumPasswordLength = VaultSecret.MinimumPasswordLength;

    /// <summary>
    /// Le marqueur d'une conversion, dans son dossier temporaire : elle le crée et le tient ouvert, seule, tant qu'elle y
    /// travaille, et le retire juste avant de donner au dossier le nom de la destination.
    /// </summary>
    internal const string MarkFile = ".kyfrev-conversion";

    /// <summary>Le refus d'un secret qui n'ouvre pas le coffre chiffré, au déchiffrement comme à la vérification.</summary>
    internal const string WrongSecret = "Ce mot de passe, ou cette clé de récupération, n'ouvre pas ce coffre.";

    /// <summary>Le refus d'un chiffrement préparé déjà libéré : sa clé de données est effacée.</summary>
    internal const string Abandoned = "Ce chiffrement préparé a été abandonné et sa clé effacée : préparez-le de nouveau. Rien n'a été écrit.";

    /// <summary>Le plus de noms qu'essaie <see cref="FreeDestination"/> : le nom voulu, puis « (2) » à « (1000) ».</summary>
    private const int MaximumDestinations = 1000;

    /// <summary>Les fichiers du coffre en clair qui ne sont pas du contenu : le verrou de l'application.</summary>
    internal const string OpenInKyfRev = "Ce coffre est ouvert dans kyfRev : fermez-le, puis recommencez.";

    private static readonly string[] Excluded = [VaultLayout.LockFile, VaultLayout.LockInfoFile];

    /// <summary>
    /// Prépare le chiffrement du coffre en clair <paramref name="source"/>, sans rien écrire : la clé de données est tirée au
    /// hasard et enveloppée par le mot de passe (une dérivation) et par une clé de récupération neuve, à noter avant la
    /// conversion (<see cref="Encrypt(EncryptionPlan, string, IProgress{ConversionProgress}, Action{string}, VaultStorage)"/>).
    /// </summary>
    /// <param name="source">Le coffre en clair ; rien n'y est écrit.</param>
    /// <param name="password">Le mot de passe du coffre chiffré, d'au moins <see cref="MinimumPasswordLength"/> caractères.</param>
    /// <param name="parameters">Les réglages de la dérivation.</param>
    /// <returns>
    /// Le chiffrement préparé, à libérer par qui le reçoit ; ou le refus, si ce n'est pas un coffre en clair ou si le mot de
    /// passe est trop court.
    /// </returns>
    /// <exception cref="ArgumentException">Les réglages sont trop faibles.</exception>
    public static EncryptionPreparation Prepare(string source, string password, PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        source = Path.GetFullPath(source);
        return NotPlain(source) is { } notPlain ? EncryptionPreparation.Refused(notPlain) : Planned(source, password, parameters);
    }

    /// <summary>Chiffre le coffre <paramref name="source"/> dans le nouveau dossier <paramref name="destination"/>.</summary>
    /// <param name="source">Le coffre en clair ; il reste intact.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="password">Le mot de passe du coffre chiffré.</param>
    /// <param name="parameters">Les réglages de la dérivation.</param>
    /// <param name="beforeVerifying">Pour les essais : ce qui arrive au dossier temporaire entre l'écriture et la vérification.</param>
    /// <param name="disk">Là où la conversion écrit : le disque par défaut.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    public static ConversionResult Encrypt(string source, string destination, string password, PasswordParameters parameters, Action<string>? beforeVerifying = null, VaultStorage? disk = null, IProgress<ConversionProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (NotPlain(source) is { } notPlain)
        {
            return ConversionResult.Refused(notPlain);
        }
        if (Refusal(source, destination) is { } refusal)
        {
            return refusal;
        }
        var prepared = Planned(source, password, parameters);
        using var plan = prepared.Plan;
        return plan is null ? ConversionResult.Refused(prepared.Refusal) : Encrypt(plan, destination, progress, beforeVerifying, disk);
    }

    /// <summary>
    /// Chiffre le coffre préparé (<see cref="Prepare"/>) dans le nouveau dossier <paramref name="destination"/>. La conversion
    /// travaille sur sa propre copie de la clé de données : refusée ou échouée, elle laisse le chiffrement préparé tel quel,
    /// prêt pour un nouvel essai. Un chiffrement préparé déjà libéré est refusé.
    /// </summary>
    /// <param name="plan">Le chiffrement préparé ; il reste à qui l'a reçu, qui le libère.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    /// <param name="beforeVerifying">Pour les essais : ce qui arrive au dossier temporaire entre l'écriture et la vérification.</param>
    /// <param name="disk">Là où la conversion écrit : le disque par défaut.</param>
    public static ConversionResult Encrypt(EncryptionPlan plan, string destination, IProgress<ConversionProgress>? progress = null, Action<string>? beforeVerifying = null, VaultStorage? disk = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        disk ??= DiskStorage.Instance;
        destination = Path.GetFullPath(destination);
        if (plan.CopyOfDataKey() is not { } dataKey)
        {
            return ConversionResult.Refused(Abandoned);
        }
        try
        {
            // Le coffre a pu changer depuis la préparation : les contrôles se refont au moment d'écrire.
            if (NotPlain(plan.Source) is { } notPlain)
            {
                return ConversionResult.Refused(notPlain);
            }
            if (Refusal(plan.Source, destination) is { } refusal)
            {
                return refusal;
            }
            return Encrypted(plan, dataKey, destination, progress, beforeVerifying, disk);
        }
        finally
        {
            PasswordKeys.Clear(dataKey);
        }
    }

    /// <summary>
    /// Déchiffre le coffre <paramref name="source"/> dans le nouveau dossier <paramref name="destination"/>, avec son mot de passe
    /// ou sa clé de récupération : un coffre au format 1, en clair, que kyfRev ouvre comme avant.
    /// </summary>
    /// <param name="source">Le coffre chiffré ; il reste intact.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="secret">Le mot de passe, ou la clé de récupération.</param>
    /// <param name="beforeVerifying">Pour les essais : ce qui arrive au dossier temporaire entre l'écriture et la vérification.</param>
    /// <param name="disk">Là où la conversion écrit : le disque par défaut.</param>
    /// <param name="progress">Les fichiers écrits, puis relus ; rien : rien n'est dit.</param>
    /// <param name="isRecoveryKey">Ce qu'est le secret, quand l'appelant le sait ; rien : sa forme le dit.</param>
    public static ConversionResult Decrypt(string source, string destination, string secret, Action<string>? beforeVerifying = null, VaultStorage? disk = null, IProgress<ConversionProgress>? progress = null, bool? isRecoveryKey = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        disk ??= DiskStorage.Instance;
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (!EncryptedVault.IsEncrypted(source))
        {
            return ConversionResult.Refused($"« {source} » n'est pas un coffre chiffré (il n'a pas de {EncryptedVault.HeaderFile}).");
        }
        if (Refusal(source, destination) is { } refusal)
        {
            return refusal;
        }
        if (OpenDataKey(source, secret, isRecoveryKey) is not { } dataKey)
        {
            return ConversionResult.Refused(WrongSecret);
        }
        string? temporary = null;
        ConversionMark? mark = null;
        try
        {
            using var held = HoldSource(source);
            using var vault = EncryptedVault.Open(source, dataKey, DiskStorage.Instance);
            temporary = Temporary(destination, disk);
            mark = ConversionMark.Take(temporary);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var folder in vault.Folders)
            {
                disk.CreateDirectory(VaultLayout.Absolute(temporary, folder));
            }
            var files = vault.Files;
            var written = 0;
            foreach (var path in files)
            {
                var content = vault.Read(path);
                var target = VaultLayout.Absolute(temporary, path);
                disk.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!disk.TryCreateNew(target, content))
                {
                    throw new IOException($"« {path} » existe déjà dans le dossier temporaire.");
                }
                expected[path] = SHA256.HashData(content);
                written++;
                progress?.Report(new ConversionProgress(ConversionStep.Writing, written, files.Count));
            }
            beforeVerifying?.Invoke(temporary);
            var verified = 0;
            foreach (var (path, hash) in expected)
            {
                if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(disk.ReadAllBytes(VaultLayout.Absolute(temporary, path)))))
                {
                    return ConversionResult.Refused($"« {path} » ne se relit pas à l'identique : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
                }
                verified++;
                progress?.Report(new ConversionProgress(ConversionStep.Verifying, verified, expected.Count));
            }
            mark.Remove(disk);
            disk.MoveDirectory(temporary, destination);
            return new ConversionResult(true, $"Coffre déchiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. Il est en clair : rangez-le à l'abri.", expected.Count);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            return ConversionResult.Refused($"Le déchiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
        }
        finally
        {
            mark?.Dispose();
            PasswordKeys.Clear(dataKey);
        }
    }

    /// <summary>
    /// Le premier dossier libre pour une conversion dans <paramref name="parent"/> : <paramref name="name"/>, puis
    /// « <paramref name="name"/> (2) », « (3) »… jusqu'à « (1000) ». Libre : ni dossier ni fichier de ce nom. Rien n'est
    /// écrit : la conversion vérifie de nouveau, au moment d'écrire, que sa destination n'existe pas.
    /// </summary>
    /// <param name="parent">Le dossier qui contiendra la conversion.</param>
    /// <param name="name">Le nom voulu.</param>
    /// <exception cref="IOException">Les mille noms sont pris.</exception>
    public static string FreeDestination(string parent, string name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(name);
        for (var number = 1; number <= MaximumDestinations; number++)
        {
            var candidate = Path.Combine(parent, number == 1 ? name : $"{name} ({number})");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new IOException($"« {name} » et ses variantes numérotées jusqu'à « {name} ({MaximumDestinations}) » sont tous pris dans « {parent} ».");
    }

    /// <summary>
    /// La clé de données du coffre chiffré <paramref name="source"/>, ouverte par son mot de passe ou par sa clé de
    /// récupération : un texte qui se lit comme une clé de récupération en est une. Rien si le secret n'ouvre pas le coffre,
    /// ou si son en-tête ne se lit pas. Rien n'est écrit ; la clé rendue est à effacer après usage.
    /// </summary>
    internal static byte[]? OpenDataKey(string source, string secret, bool? isRecoveryKey = null)
    {
        try
        {
            var header = EncryptedVault.ReadHeader(source);
            if (isRecoveryKey is { } known)
            {
                return known ? header.OpenWithRecovery(secret) : header.Open(secret);
            }
            return RecoveryKey.Parse(secret) is { } recovery ? Opened(header, recovery, secret) : header.Open(secret);
        }
        catch (VaultCryptoException)
        {
            return null;
        }
    }

    /// <summary>
    /// Relit le coffre chiffré écrit dans <paramref name="temporary"/> avec la clé que son en-tête vient de rendre, puis
    /// efface cette clé, que la relecture réussisse ou non. Rend ce qui manque ou diffère.
    /// </summary>
    internal static string? Reread(string temporary, byte[] key, SortedDictionary<string, byte[]> expected, SortedSet<string> folders, IProgress<ConversionProgress>? progress = null)
    {
        try
        {
            using var reopened = EncryptedVault.Open(temporary, key, DiskStorage.Instance);
            return Mismatch(expected, folders, reopened, progress);
        }
        finally
        {
            PasswordKeys.Clear(key);
        }
    }

    /// <summary>Ce qui fait refuser un chiffrement : la source n'est pas un coffre en clair.</summary>
    private static string? NotPlain(string source) =>
        File.Exists(VaultLayout.Absolute(source, VaultLayout.SettingsFile))
            ? null
            : $"« {source} » n'est pas un coffre kyfRev en clair (il n'a pas de {VaultLayout.SettingsFile}).";

    /// <summary>
    /// Le chiffrement préparé du coffre en clair <paramref name="source"/>, déjà reconnu : le mot de passe est vérifié, puis
    /// l'en-tête créé. L'original de la clé de données est effacé : seul le chiffrement préparé en garde une copie.
    /// </summary>
    private static EncryptionPreparation Planned(string source, string password, PasswordParameters parameters)
    {
        if (password.Length < MinimumPasswordLength)
        {
            return EncryptionPreparation.Refused($"Le mot de passe doit faire au moins {MinimumPasswordLength} caractères.");
        }
        var vaultId = VaultInspector.Inspect(source).Summary.VaultId ?? "v_" + UlidGenerator.System.Next();
        var (header, dataKey, recovery) = VaultHeader.Create(vaultId, password, parameters);
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
    /// Écrit le coffre chiffré de <paramref name="plan"/> dans un dossier temporaire, le relit, puis lui donne le nom de
    /// <paramref name="destination"/>. Les contrôles sont faits ; <paramref name="dataKey"/> est la copie de la clé que la
    /// conversion efface ensuite.
    /// </summary>
    private static ConversionResult Encrypted(EncryptionPlan plan, byte[] dataKey, string destination, IProgress<ConversionProgress>? progress, Action<string>? beforeVerifying, VaultStorage disk)
    {
        var source = plan.Source;
        string? temporary = null;
        ConversionMark? mark = null;
        try
        {
            using var held = HoldSource(plan.Source);
            temporary = Temporary(destination, disk);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            using (var vault = EncryptedVault.Create(temporary, plan.Header, dataKey, disk))
            {
                // Un coffre chiffré se crée dans un dossier vide : le marqueur vient aussitôt après, avant tout fichier du coffre.
                mark = ConversionMark.Take(temporary);
                foreach (var folder in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                {
                    var relative = Relative(source, folder);
                    vault.AddFolder(relative);
                    folders.Add(relative);
                }
                // Les fichiers sont comptés avant d'être écrits : la progression dit combien il en reste.
                var files = ContentsOf(source);
                var written = 0;
                foreach (var (file, relative) in files)
                {
                    var content = File.ReadAllBytes(file);
                    vault.Write(relative, content);
                    expected[relative] = SHA256.HashData(content);
                    written++;
                    progress?.Report(new ConversionProgress(ConversionStep.Writing, written, files.Count));
                }
                vault.Flush();
            }
            beforeVerifying?.Invoke(temporary);

            // La vérification repart de l'en-tête écrit, avec le mot de passe : une erreur d'enveloppe se verrait ici.
            if (Reread(temporary, EncryptedVault.ReadHeader(temporary).Open(plan.Password), expected, folders, progress) is { } mismatch)
            {
                return ConversionResult.Refused(mismatch + Discard(temporary, destination, disk, mark));
            }
            mark.Remove(disk);
            disk.MoveDirectory(temporary, destination);
            return new ConversionResult(true, $"Coffre chiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. La source n'a pas changé.", expected.Count, plan.RecoveryKey);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            return ConversionResult.Refused($"Le chiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
        }
        finally
        {
            mark?.Dispose();
        }
    }

    /// <summary>
    /// Les fichiers de contenu du coffre en clair <paramref name="source"/>, et leur chemin dans le coffre : ni son verrou, ni
    /// le fichier temporaire d'une écriture atomique.
    /// </summary>
    private static List<(string File, string Relative)> ContentsOf(string source) =>
    [
        .. Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(file => (File: file, Relative: Relative(source, file)))
            .Where(entry => !Excluded.Contains(entry.Relative, StringComparer.Ordinal) && !Path.GetFileName(entry.File).StartsWith(AtomicFile.TemporaryPrefix, StringComparison.Ordinal)),
    ];

    /// <summary>Une clé de récupération, ou un mot de passe qui en a la forme : si elle n'ouvre pas l'en-tête, il reste à l'essayer comme tel.</summary>
    private static byte[] Opened(VaultHeader header, byte[] recovery, string secret)
    {
        PasswordKeys.Clear(recovery);
        try
        {
            return header.OpenWithRecovery(secret);
        }
        catch (VaultCryptoException)
        {
            return header.Open(secret);
        }
    }

    /// <summary>Ce qui interdit la conversion, quel que soit son sens ; un dossier qui ne se lit pas l'interdit aussi.</summary>
    private static ConversionResult? Refusal(string source, string destination)
    {
        try
        {
            return Checks(source, destination);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            return ConversionResult.Refused($"La conversion ne peut pas vérifier « {destination} » et ce qui l'entoure ({exception.Message}) : rien n'a été écrit.");
        }
    }

    /// <summary>Les contrôles de <see cref="Refusal"/>.</summary>
    private static ConversionResult? Checks(string source, string destination)
    {
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            return ConversionResult.Refused($"« {destination} » existe déjà : la conversion se fait toujours vers un nouveau dossier.");
        }
        if (!Directory.Exists(Path.GetDirectoryName(destination)))
        {
            return ConversionResult.Refused($"Le dossier qui doit contenir « {destination} » n'existe pas.");
        }
        if (Inside(destination, source) || Inside(source, destination))
        {
            return ConversionResult.Refused("La destination ne peut être ni dans la source, ni autour d'elle.");
        }
        if (LeftoverOf(destination) is { } leftover)
        {
            var sign = File.Exists(Path.Combine(leftover, MarkFile)) ? $"il porte le marqueur « {MarkFile} »" : "il en porte le nom exact, sans marqueur";
            return ConversionResult.Refused($"« {leftover} » est le dossier temporaire d'une conversion vers « {destination} », en cours ou coupée : {sign}. Rien ne l'efface de lui-même. Si aucune conversion ne tourne, retirez-le à la main (après un déchiffrement, il contient le coffre en clair), puis recommencez.");
        }
        if (MarkedFolder(source) is { } marked)
        {
            // Une conversion coupée net a pu y laisser un coffre complet : on ne convertit pas ce qu'on retirera à la main.
            return ConversionResult.Refused($"« {marked} » est le dossier temporaire d'une conversion, en cours ou coupée : il contient « {MarkFile} », et une conversion n'en part pas. Pour en faire un coffre, attendez qu'aucune conversion ne tourne, retirez ce fichier, puis renommez le dossier.");
        }
        if (IsOpenElsewhere(source))
        {
            return ConversionResult.Refused(OpenInKyfRev);
        }
        return null;
    }

    /// <summary>
    /// Un dossier temporaire d'une conversion vers <paramref name="destination"/>, à côté d'elle : celui d'une conversion en
    /// cours, ou coupée net. Son nom exact suffit, marqueur ou non : une conversion coupée avant la pose du marqueur, ou entre
    /// son retrait et le renommage, n'en laisse pas. Une conversion n'efface jamais celui d'une autre : elle refuse tant qu'il
    /// est là.
    /// </summary>
    private static string? LeftoverOf(string destination) =>
        Directory.EnumerateDirectories(Path.GetDirectoryName(destination)!).FirstOrDefault(folder => IsTemporaryOf(destination, folder));

    /// <summary>
    /// Ce qui fait refuser la conversion plutôt qu'arrêter l'outil : le disque, les droits, le chiffrement, un chemin ou un
    /// argument refusé. Le reste est une erreur imprévue : elle remonte, et laisse le dossier temporaire marqué.
    /// </summary>
    internal static bool IsRefusal(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or VaultCryptoException or CryptographicException or ArgumentException or NotSupportedException;

    /// <summary>
    /// Le dossier temporaire de conversion que <paramref name="source"/> est, ou qui la contient : le premier qui porte le
    /// marqueur, en remontant. Le disque répond, quel que soit le chemin écrit (une jonction, un nom court 8.3, un lecteur
    /// substitué mènent au même dossier) ; le chemin réel, liens résolus, est remonté lui aussi, pour un lien qui mène dans un
    /// sous-dossier.
    /// </summary>
    private static string? MarkedFolder(string source)
    {
        foreach (var path in (string[])[source, DiskStorage.Instance.RealPath(source)])
        {
            for (var folder = path; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
            {
                if (File.Exists(Path.Combine(folder, MarkFile)))
                {
                    return folder;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Tient le verrou du coffre source pendant toute la conversion : le même verrou, sur la même plage, que celui qu'une
    /// session ouvre (<c>.suivi/verrou</c>). Une autre fenêtre de kyfRev, ou une autre conversion, trouve donc le coffre
    /// pris, et rien ne s'y écrit pendant la copie. Rien n'y est écrit non plus : le fichier existe, ou la source n'en a pas,
    /// et rien n'est tenu. Rend de quoi le relâcher.
    /// </summary>
    private static FileStream? HoldSource(string source)
    {
        var lockFile = VaultLayout.Absolute(source, VaultLayout.LockFile);
        if (!File.Exists(lockFile))
        {
            return null;
        }
        var held = new FileStream(lockFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                held.Lock(0, long.MaxValue);
            }
            return held;
        }
        catch (IOException exception)
        {
            held.Dispose();
            throw new IOException(OpenInKyfRev, exception);
        }
    }

    /// <summary>
    /// Le coffre est ouvert dans kyfRev : l'application tient son verrou ouvert. Le vérifier n'écrit rien : on tente seulement
    /// de l'ouvrir pour soi seul.
    /// </summary>
    internal static bool IsOpenElsewhere(string source)
    {
        var lockFile = VaultLayout.Absolute(source, VaultLayout.LockFile);
        if (!File.Exists(lockFile))
        {
            return false;
        }
        try
        {
            using var probe = new FileStream(lockFile, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>Ce qui manque ou diffère entre le coffre relu et la source ; chaque fichier relu fait avancer la progression.</summary>
    private static string? Mismatch(SortedDictionary<string, byte[]> expected, SortedSet<string> folders, EncryptedVault reopened, IProgress<ConversionProgress>? progress)
    {
        if (!expected.Keys.SequenceEqual(reopened.Files, StringComparer.Ordinal) || !folders.SetEquals(reopened.Folders))
        {
            return "Le coffre chiffré ne contient pas exactement les fichiers et les dossiers de la source : rien n'a été écrit.";
        }
        var verified = 0;
        foreach (var (path, hash) in expected)
        {
            if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(reopened.Read(path))))
            {
                return $"« {path} » ne se relit pas à l'identique : rien n'a été écrit.";
            }
            verified++;
            progress?.Report(new ConversionProgress(ConversionStep.Verifying, verified, expected.Count));
        }
        return null;
    }

    /// <summary>
    /// Un dossier temporaire voisin de la destination : <c>.&lt;destination&gt;.conversion-&lt;nom tiré au hasard&gt;</c>. Il
    /// n'est rendu, et la conversion ne le fait sien, qu'une fois créé par elle et trouvé vide : un dossier qui portait déjà ce
    /// nom, ou qu'un autre programme a rempli entre-temps, n'est pas le sien, et elle n'y efface jamais rien.
    /// </summary>
    private static string Temporary(string destination, VaultStorage disk)
    {
        var folder = Path.Combine(Path.GetDirectoryName(destination)!, $"{TemporaryPrefix(destination)}{Path.GetRandomFileName()}");
        if (disk.Exists(folder))
        {
            throw new IOException($"« {folder} » existe déjà : ce n'est pas le dossier temporaire de cette conversion");
        }
        disk.CreateDirectory(folder);
        if (disk.Entries(folder).Count > 0)
        {
            throw new IOException($"« {folder} » n'est pas vide : ce n'est pas le dossier temporaire de cette conversion");
        }
        return folder;
    }

    private static string TemporaryPrefix(string destination) => $".{Path.GetFileName(destination)}.conversion-";

    /// <summary>
    /// Vrai si <paramref name="folder"/> est un dossier temporaire d'une conversion vers <paramref name="destination"/> : son
    /// nom, et lui seul, le désigne, sans tenir compte de la casse (sous Windows, « Coffre » et « coffre » sont le même
    /// dossier). Un dossier qui n'en a que le début n'en est pas un.
    /// </summary>
    private static bool IsTemporaryOf(string destination, string folder)
    {
        var name = Path.GetFileName(folder);
        var prefix = TemporaryPrefix(destination);
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var random = name[prefix.Length..];
        return random.Length == 12 && random[8] == '.' && random.Remove(8, 1).All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>
    /// Le seul point de la conversion qui efface un dossier : son propre dossier temporaire, créé par cette exécution et trouvé
    /// vide (<see cref="Temporary"/> ne le rend qu'alors), reconnu aussi à son nom. Son marqueur part en dernier : tenu, ou
    /// repris, ou reposé s'il manque, pendant que tout le reste s'efface, il n'est relâché qu'ensuite, et effacé avec le
    /// dossier. S'il ne peut être ni repris ni reposé, le dossier est vidé tout de même : il n'appartient qu'à cette
    /// conversion. Le marqueur que la conversion tient encore, s'il y en a un, est relâché dans tous les cas. Rend de quoi dire
    /// ce qui reste, et ce qu'il faut en faire.
    /// </summary>
    private static string Discard(string? temporary, string destination, VaultStorage disk, ConversionMark? mark = null)
    {
        if (temporary is null || !IsTemporaryOf(destination, temporary) || !disk.DirectoryExists(temporary))
        {
            mark?.Dispose();
            return string.Empty;
        }
        try
        {
            mark = mark is { IsHeld: true } ? mark : ConversionMark.TryHold(temporary);
            foreach (var entry in disk.Entries(temporary).Where(entry => !string.Equals(entry.Name, MarkFile, StringComparison.OrdinalIgnoreCase)))
            {
                disk.Purge(entry.FullName);
            }
            mark?.Dispose();
            disk.Purge(temporary);
            return string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            mark?.Dispose();
            // Marqué ou non, son nom suffit : la conversion suivante vers la même destination refuse tant qu'il est là.
            return $" Le dossier temporaire « {temporary} » n'a pas pu être retiré ({exception.Message}) : retirez-le à la main (après un déchiffrement, il contient le coffre en clair). Tant qu'il est là, une conversion vers la même destination refuse.";
        }
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Le marqueur d'une conversion (<see cref="MarkFile"/>), dans son dossier temporaire : créé et tenu d'un même geste, et
    /// seule la conversion le tient. Coupée net, elle le laisse : c'est à cela qu'on reconnaît son reste, qu'aucune
    /// conversion n'efface.
    /// </summary>
    private sealed class ConversionMark : IDisposable
    {
        private readonly string _path;
        private readonly FileStream _stream;

        private ConversionMark(string path, FileStream stream)
        {
            _path = path;
            _stream = stream;
        }

        /// <summary>Vrai tant que le marqueur est tenu.</summary>
        public bool IsHeld { get; private set; } = true;

        /// <summary>Pose le marqueur dans <paramref name="folder"/> et le tient : personne d'autre ne l'ouvre.</summary>
        public static ConversionMark Take(string folder) => Open(folder, FileMode.CreateNew);

        /// <summary>
        /// Tient de nouveau le marqueur du dossier temporaire de cette conversion, ou le repose s'il manque ; rien s'il ne peut
        /// être ni tenu ni reposé.
        /// </summary>
        public static ConversionMark? TryHold(string folder)
        {
            try
            {
                return Open(folder, FileMode.OpenOrCreate);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Retire le marqueur, encore tenu, juste avant que le dossier prenne le nom de la destination.</summary>
        public void Remove(VaultStorage disk)
        {
            disk.Purge(_path);
            Dispose();
        }

        public void Dispose()
        {
            IsHeld = false;
            _stream.Dispose();
        }

        private static ConversionMark Open(string folder, FileMode mode)
        {
            var path = Path.Combine(folder, MarkFile);
            return new ConversionMark(path, new FileStream(path, mode, FileAccess.ReadWrite, FileShare.Delete));
        }
    }

    private static bool Inside(string path, string folder)
    {
        var parent = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
