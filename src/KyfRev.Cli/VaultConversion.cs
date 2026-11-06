using System.Security.Cryptography;
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
/// ne laisse jamais de coffre à moitié écrit.
/// </summary>
internal static class VaultConversion
{
    /// <summary>La longueur minimale d'un mot de passe de coffre.</summary>
    public const int MinimumPasswordLength = 12;

    /// <summary>Les fichiers du coffre en clair qui ne sont pas du contenu : le verrou de l'application.</summary>
    private static readonly string[] Excluded = [VaultLayout.LockFile, VaultLayout.LockInfoFile];

    /// <summary>Chiffre le coffre <paramref name="source"/> dans le nouveau dossier <paramref name="destination"/>.</summary>
    public static ConversionResult Encrypt(string source, string destination, string password, PasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
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
        var temporary = Temporary(destination);
        try
        {
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var folders = new SortedSet<string>(StringComparer.Ordinal);
            using (var vault = EncryptedVault.Create(temporary, header, dataKey))
            {
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

            // La vérification repart de l'en-tête écrit, avec le mot de passe : une erreur d'enveloppe se verrait ici.
            var reopenedKey = EncryptedVault.ReadHeader(temporary).Open(password);
            using (var reopened = EncryptedVault.Open(temporary, reopenedKey))
            {
                PasswordKeys.Clear(reopenedKey);
                if (Mismatch(expected, folders, reopened) is { } mismatch)
                {
                    Discard(temporary);
                    return ConversionResult.Refused(mismatch);
                }
            }
            Directory.Move(temporary, destination);
            return new ConversionResult(true, $"Coffre chiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. La source n'a pas changé.", expected.Count, recovery);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or VaultCryptoException)
        {
            Discard(temporary);
            return ConversionResult.Refused($"Le chiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} ».");
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
    public static ConversionResult Decrypt(string source, string destination, string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
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
        var temporary = Temporary(destination);
        try
        {
            using var vault = EncryptedVault.Open(source, dataKey);
            var expected = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var folder in vault.Folders)
            {
                Directory.CreateDirectory(VaultLayout.Absolute(temporary, folder));
            }
            foreach (var path in vault.Files)
            {
                var content = vault.Read(path);
                var target = VaultLayout.Absolute(temporary, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, content);
                expected[path] = SHA256.HashData(content);
            }
            foreach (var (path, hash) in expected)
            {
                if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(File.ReadAllBytes(VaultLayout.Absolute(temporary, path)))))
                {
                    Discard(temporary);
                    return ConversionResult.Refused($"« {path} » ne se relit pas à l'identique : rien n'a été écrit dans « {destination} ».");
                }
            }
            Directory.Move(temporary, destination);
            return new ConversionResult(true, $"Coffre déchiffré dans « {destination} » : {expected.Count} fichiers, chacun relu et vérifié. Il est en clair : rangez-le à l'abri.", expected.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or VaultCryptoException)
        {
            Discard(temporary);
            return ConversionResult.Refused($"Le déchiffrement a échoué ({exception.Message}) : rien n'a été écrit dans « {destination} ».");
        }
        finally
        {
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
        if (IsOpenElsewhere(source))
        {
            return ConversionResult.Refused("Ce coffre est ouvert dans kyfRev : fermez-le, puis recommencez.");
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

    private static string Temporary(string destination)
    {
        var folder = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.conversion-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Retire un dossier temporaire de conversion, et lui seul : son nom le désigne.</summary>
    private static void Discard(string temporary)
    {
        if (Path.GetFileName(temporary).Contains(".conversion-", StringComparison.Ordinal) && Directory.Exists(temporary))
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static bool Inside(string path, string folder)
    {
        var parent = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
