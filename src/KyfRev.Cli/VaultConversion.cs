using System.Security.Cryptography;
using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Crypto;
using KyfRev.Infrastructure.Vault;
using KyfRev.Infrastructure.Vault.Analysis;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Ids;

namespace KyfRev.Cli;

/// <summary>Ce qu'une conversion a fait, ou pourquoi elle a refusé.</summary>
/// <param name="Succeeded">La destination est écrite et vérifiée.</param>
/// <param name="Message">Ce qui s'est passé, pour la console.</param>
/// <param name="Files">Le nombre de fichiers convertis et vérifiés.</param>
/// <param name="RecoveryKey">La clé de récupération d'un coffre qu'on vient de chiffrer, à montrer une seule fois.</param>
internal sealed record ConversionResult(bool Succeeded, string Message, int Files = 0, string? RecoveryKey = null)
{
    public static ConversionResult Refused(string message) => new(false, message);
}

/// <summary>
/// Chiffrer et déchiffrer un coffre, toujours vers un nouveau dossier (plan de refonte, §4.3). La source reste intacte : rien
/// n'y est écrit, pas même le verrou. La conversion s'écrit d'abord dans un dossier temporaire voisin, chaque fichier y est
/// relu et comparé à l'original par son SHA-256, puis le dossier prend son nom en une seule opération : une conversion coupée
/// ne laisse jamais de coffre à moitié écrit. Toute écriture passe par un <see cref="VaultStorage"/> (le disque, ou dans les
/// essais un disque qui échoue à chaque écriture tour à tour), sauf la pose du marqueur, créé et tenu d'un même geste.
/// </summary>
/// <remarks>
/// Tant qu'elle travaille, une conversion tient dans son dossier temporaire un marqueur (<see cref="MarkFile"/>), qu'elle
/// retire juste avant de lui donner le nom de la destination. Elle n'efface que ce dossier-là, créé pendant cette exécution :
/// refusée, elle le retire. Coupée net (arrêt brutal, disque perdu), elle peut le laisser, et rien ne l'efface ensuite : tant
/// qu'il est là, une conversion vers la même destination refuse et le nomme, pour qu'on le retire à la main. Une source qui
/// porte le marqueur, ou dont un dossier parent le porte, est refusée.
/// </remarks>
internal static class VaultConversion
{
    /// <summary>La longueur minimale d'un mot de passe de coffre.</summary>
    public const int MinimumPasswordLength = 12;

    /// <summary>
    /// Le marqueur d'une conversion, dans son dossier temporaire : elle le crée et le tient ouvert, seule, tant qu'elle y
    /// travaille, et le retire juste avant de donner au dossier le nom de la destination.
    /// </summary>
    internal const string MarkFile = ".kyfrev-conversion";

    /// <summary>Les fichiers du coffre en clair qui ne sont pas du contenu : le verrou de l'application.</summary>
    private static readonly string[] Excluded = [VaultLayout.LockFile, VaultLayout.LockInfoFile];

    /// <summary>Chiffre le coffre <paramref name="source"/> dans le nouveau dossier <paramref name="destination"/>.</summary>
    /// <param name="source">Le coffre en clair ; il reste intact.</param>
    /// <param name="destination">Un nouveau dossier.</param>
    /// <param name="password">Le mot de passe du coffre chiffré.</param>
    /// <param name="parameters">Les réglages de la dérivation.</param>
    /// <param name="beforeVerifying">Pour les essais : ce qui arrive au dossier temporaire entre l'écriture et la vérification.</param>
    /// <param name="disk">Là où la conversion écrit : le disque par défaut.</param>
    public static ConversionResult Encrypt(string source, string destination, string password, PasswordParameters parameters, Action<string>? beforeVerifying = null, VaultStorage? disk = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        disk ??= DiskStorage.Instance;
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (!File.Exists(VaultLayout.Absolute(source, VaultLayout.SettingsFile)))
        {
            return ConversionResult.Refused($"« {source} » n'est pas un coffre kyfRev en clair (il n'a pas de {VaultLayout.SettingsFile}).");
        }
        if (Refusal(source, destination) is { } refusal)
        {
            return refusal;
        }
        if (password.Length < MinimumPasswordLength)
        {
            return ConversionResult.Refused($"Le mot de passe doit faire au moins {MinimumPasswordLength} caractères.");
        }
        var vaultId = VaultInspector.Inspect(source).Summary.VaultId ?? "v_" + UlidGenerator.System.Next();
        var (header, dataKey, recovery) = VaultHeader.Create(vaultId, password, parameters);
        string? temporary = null;
        ConversionMark? mark = null;
        try
        {
            temporary = Temporary(destination, disk);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            using (var vault = EncryptedVault.Create(temporary, header, dataKey, disk))
            {
                // Un coffre chiffré se crée dans un dossier vide : le marqueur vient aussitôt après, avant tout fichier du coffre.
                mark = ConversionMark.Take(temporary);
                foreach (var folder in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                {
                    var relative = Relative(source, folder);
                    vault.AddFolder(relative);
                    folders.Add(relative);
                }
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var relative = Relative(source, file);
                    if (Excluded.Contains(relative, StringComparer.Ordinal) || Path.GetFileName(file).StartsWith(AtomicFile.TemporaryPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var content = File.ReadAllBytes(file);
                    vault.Write(relative, content);
                    expected[relative] = SHA256.HashData(content);
                }
                vault.Flush();
            }
            beforeVerifying?.Invoke(temporary);

            // La vérification repart de l'en-tête écrit, avec le mot de passe : une erreur d'enveloppe se verrait ici.
            if (Reread(temporary, EncryptedVault.ReadHeader(temporary).Open(password), expected, folders) is { } mismatch)
            {
                return ConversionResult.Refused(mismatch + Discard(temporary, destination, disk, mark));
            }
            mark.Remove(disk);
            disk.MoveDirectory(temporary, destination);
            return new ConversionResult(true, $"Coffre chiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. La source n'a pas changé.", expected.Count, recovery);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or VaultCryptoException)
        {
            return ConversionResult.Refused($"Le chiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
        }
        finally
        {
            mark?.Dispose();
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
    public static ConversionResult Decrypt(string source, string destination, string secret, Action<string>? beforeVerifying = null, VaultStorage? disk = null)
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
        byte[] dataKey;
        try
        {
            var header = EncryptedVault.ReadHeader(source);
            dataKey = RecoveryKey.Parse(secret) is { } recovery ? Opened(header, recovery, secret) : header.Open(secret);
        }
        catch (VaultCryptoException)
        {
            return ConversionResult.Refused("Ce mot de passe, ou cette clé de récupération, n'ouvre pas ce coffre.");
        }
        string? temporary = null;
        ConversionMark? mark = null;
        try
        {
            using var vault = EncryptedVault.Open(source, dataKey, DiskStorage.Instance);
            temporary = Temporary(destination, disk);
            mark = ConversionMark.Take(temporary);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var folder in vault.Folders)
            {
                disk.CreateDirectory(VaultLayout.Absolute(temporary, folder));
            }
            foreach (var path in vault.Files)
            {
                var content = vault.Read(path);
                var target = VaultLayout.Absolute(temporary, path);
                disk.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!disk.TryCreateNew(target, content))
                {
                    throw new IOException($"« {path} » existe déjà dans le dossier temporaire.");
                }
                expected[path] = SHA256.HashData(content);
            }
            beforeVerifying?.Invoke(temporary);
            foreach (var (path, hash) in expected)
            {
                if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(disk.ReadAllBytes(VaultLayout.Absolute(temporary, path)))))
                {
                    return ConversionResult.Refused($"« {path} » ne se relit pas à l'identique : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
                }
            }
            mark.Remove(disk);
            disk.MoveDirectory(temporary, destination);
            return new ConversionResult(true, $"Coffre déchiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. Il est en clair : rangez-le à l'abri.", expected.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or VaultCryptoException)
        {
            return ConversionResult.Refused($"Le déchiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} »." + Discard(temporary, destination, disk, mark));
        }
        finally
        {
            mark?.Dispose();
            PasswordKeys.Clear(dataKey);
        }
    }

    private static byte[] Opened(VaultHeader header, byte[] recovery, string secret)
    {
        PasswordKeys.Clear(recovery);
        return header.OpenWithRecovery(secret);
    }

    /// <summary>Ce qui interdit la conversion, quel que soit son sens.</summary>
    private static ConversionResult? Refusal(string source, string destination)
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
            return ConversionResult.Refused($"« {leftover} » est le dossier temporaire d'une conversion vers « {destination} », en cours ou coupée : il porte le marqueur « {MarkFile} ». Rien ne l'efface de lui-même. Si aucune conversion ne tourne, retirez-le à la main (après un déchiffrement, il contient le coffre en clair), puis recommencez.");
        }
        if (MarkedFolder(source) is { } marked)
        {
            // Une conversion coupée net a pu y laisser un coffre complet : on ne convertit pas ce qu'on retirera à la main.
            return ConversionResult.Refused($"« {marked} » est le dossier temporaire d'une conversion, en cours ou coupée : il contient « {MarkFile} », et une conversion n'en part pas. Pour en faire un coffre, attendez qu'aucune conversion ne tourne, retirez ce fichier, puis renommez le dossier.");
        }
        if (IsOpenElsewhere(source))
        {
            return ConversionResult.Refused("Ce coffre est ouvert dans kyfRev : fermez-le, puis recommencez.");
        }
        return null;
    }

    /// <summary>
    /// Un dossier temporaire marqué d'une conversion vers <paramref name="destination"/>, à côté d'elle : celui d'une
    /// conversion en cours, ou coupée net. Une conversion n'efface jamais celui d'une autre : elle refuse tant qu'il est là.
    /// </summary>
    private static string? LeftoverOf(string destination) =>
        Directory.EnumerateDirectories(Path.GetDirectoryName(destination)!)
            .FirstOrDefault(folder => IsTemporaryOf(destination, folder) && File.Exists(Path.Combine(folder, MarkFile)));

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
    /// Le coffre est ouvert dans kyfRev : l'application tient son verrou ouvert. Le vérifier n'écrit rien : on tente seulement
    /// de l'ouvrir pour soi seul.
    /// </summary>
    private static bool IsOpenElsewhere(string source)
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

    /// <summary>
    /// Relit le coffre chiffré écrit dans <paramref name="temporary"/> avec la clé que son en-tête vient de rendre, puis
    /// efface cette clé, que la relecture réussisse ou non. Rend ce qui manque ou diffère.
    /// </summary>
    internal static string? Reread(string temporary, byte[] key, SortedDictionary<string, byte[]> expected, SortedSet<string> folders)
    {
        try
        {
            using var reopened = EncryptedVault.Open(temporary, key, DiskStorage.Instance);
            return Mismatch(expected, folders, reopened);
        }
        finally
        {
            PasswordKeys.Clear(key);
        }
    }

    /// <summary>Ce qui manque ou diffère entre le coffre relu et la source.</summary>
    private static string? Mismatch(SortedDictionary<string, byte[]> expected, SortedSet<string> folders, EncryptedVault reopened)
    {
        if (!expected.Keys.SequenceEqual(reopened.Files, StringComparer.Ordinal) || !folders.SetEquals(reopened.Folders))
        {
            return "Le coffre chiffré ne contient pas exactement les fichiers et les dossiers de la source : rien n'a été écrit.";
        }
        foreach (var (path, hash) in expected)
        {
            if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(reopened.Read(path))))
            {
                return $"« {path} » ne se relit pas à l'identique : rien n'a été écrit.";
            }
        }
        return null;
    }

    /// <summary>Un dossier temporaire voisin de la destination : <c>.&lt;destination&gt;.conversion-&lt;nom tiré au hasard&gt;</c>.</summary>
    private static string Temporary(string destination, VaultStorage disk)
    {
        var folder = Path.Combine(Path.GetDirectoryName(destination)!, $"{TemporaryPrefix(destination)}{Path.GetRandomFileName()}");
        disk.CreateDirectory(folder);
        return folder;
    }

    private static string TemporaryPrefix(string destination) => $".{Path.GetFileName(destination)}.conversion-";

    /// <summary>
    /// Vrai si <paramref name="folder"/> est un dossier temporaire d'une conversion vers <paramref name="destination"/> : son
    /// nom, et lui seul, le désigne. Un dossier qui n'en a que le début n'en est pas un.
    /// </summary>
    private static bool IsTemporaryOf(string destination, string folder)
    {
        var name = Path.GetFileName(folder);
        var prefix = TemporaryPrefix(destination);
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        var random = name[prefix.Length..];
        return random.Length == 12 && random[8] == '.' && random.Remove(8, 1).All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c));
    }

    /// <summary>
    /// Le seul point de la conversion qui efface un dossier : son propre dossier temporaire, créé pendant cette exécution et
    /// marqué tout du long, reconnu aussi à son nom. Son marqueur part en dernier : tenu, ou repris, ou reposé s'il manque,
    /// pendant que tout le reste s'efface, il n'est relâché qu'ensuite, et effacé avec le dossier. S'il ne peut être ni repris
    /// ni reposé, le dossier est vidé tout de même : il n'appartient qu'à cette conversion. Le marqueur que la conversion tient
    /// encore, s'il y en a un, est relâché dans tous les cas. Rend de quoi dire ce qui reste, et ce qu'il faut en faire.
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
            var left = $" Le dossier temporaire « {temporary} » n'a pas pu être retiré ({exception.Message}) : retirez-le à la main (après un déchiffrement, il contient le coffre en clair).";
            return File.Exists(Path.Combine(temporary, MarkFile))
                ? left + " Tant qu'il est là, une conversion vers la même destination refuse."
                : left + " Plus rien ne le désigne comme dossier temporaire.";
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
