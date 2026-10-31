using System.Security.Cryptography;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// Écriture atomique : le contenu est écrit dans un fichier temporaire du même dossier, forcé sur
/// le disque, puis mis en place en une seule opération. Après une coupure, le fichier visé est
/// l'ancienne ou la nouvelle version, jamais un fichier tronqué.
/// </summary>
/// <remarks>
/// Les temporaires portent le préfixe <c>.ecriture-</c> et le suffixe <c>.tmp</c>, comme dans la
/// version 0.x : les deux implémentations les ignorent dans leurs analyses et leurs sauvegardes.
/// </remarks>
public static class AtomicFile
{
    /// <summary>Préfixe des fichiers temporaires.</summary>
    public const string TemporaryPrefix = ".ecriture-";

    private const string TemporarySuffix = ".tmp";
    private const int Attempts = 10;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorFileExists = 80;
    private const int ErrorAlreadyExists = 183;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private static readonly Hooks NoHooks = new();

    /// <summary>Remplace le fichier (ou le crée) de façon atomique.</summary>
    public static void Replace(string path, ReadOnlySpan<byte> content) => Write(path, content, overwrite: true, NoHooks);

    /// <summary>Crée le fichier de façon atomique, sans jamais écraser un fichier existant.</summary>
    /// <returns><see langword="false"/> si le fichier existe déjà ; rien n'est alors modifié.</returns>
    public static bool TryCreateNew(string path, ReadOnlySpan<byte> content) =>
        !File.Exists(path) && Write(path, content, overwrite: false, NoHooks);

    /// <summary>Version de <see cref="Replace(string, ReadOnlySpan{byte})"/> avec des points d'appel pour les tests.</summary>
    internal static void Replace(string path, ReadOnlySpan<byte> content, Hooks hooks) =>
        Write(path, content, overwrite: true, hooks);

    /// <summary>Version de <see cref="TryCreateNew(string, ReadOnlySpan{byte})"/> avec des points d'appel pour les tests.</summary>
    internal static bool TryCreateNew(string path, ReadOnlySpan<byte> content, Hooks hooks) =>
        !File.Exists(path) && Write(path, content, overwrite: false, hooks);

    private static bool Write(string path, ReadOnlySpan<byte> content, bool overwrite, Hooks hooks)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new ArgumentException("Chemin sans dossier.", nameof(path));
        var temporary = CreateTemporary(directory, content);
        try
        {
            hooks.BeforeCommit?.Invoke();
            return Commit(temporary, path, overwrite, hooks);
        }
        catch
        {
            // Un temporaire qui n'a pas pu être retiré est ignoré par les analyses et les sauvegardes.
            _ = TryDeleteTemporary(temporary);
            throw;
        }
    }

    private static string CreateTemporary(string directory, ReadOnlySpan<byte> content)
    {
        for (var attempt = 1; ; attempt++)
        {
            var temporary = Path.Combine(directory, TemporaryPrefix + RandomNumberGenerator.GetString(Alphabet, 6) + TemporarySuffix);
            FileStream stream;
            try
            {
                stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (attempt < Attempts && File.Exists(temporary))
            {
                // Nom déjà pris par un autre temporaire : on en tire un autre.
                continue;
            }
            using (stream)
            {
                File.SetAttributes(temporary, FileAttributes.Temporary);
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            return temporary;
        }
    }

    private static bool Commit(string temporary, string target, bool overwrite, Hooks hooks)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!overwrite && File.Exists(target))
                {
                    _ = TryDeleteTemporary(temporary);
                    return false;
                }
                File.Move(temporary, target, overwrite);
                return true;
            }
            catch (IOException io) when (!overwrite && (io.HResult & 0xFFFF) is ErrorFileExists or ErrorAlreadyExists)
            {
                // Créé entre-temps par un autre processus : on n'écrase jamais.
                _ = TryDeleteTemporary(temporary);
                return false;
            }
            catch (Exception exception) when (attempt < Attempts && IsTransient(exception))
            {
                // Antivirus ou indexeur qui garde le fichier ouvert un instant.
                hooks.BeforeRetry?.Invoke();
                Thread.Sleep(20 * attempt);
            }
        }
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        UnauthorizedAccessException => true,
        IOException io => (io.HResult & 0xFFFF) is ErrorAccessDenied or ErrorSharingViolation or ErrorLockViolation,
        _ => false,
    };

    /// <summary>
    /// Points d'appel pour simuler, dans les tests, ce qui arrive entre deux instructions : une
    /// coupure ou un fichier créé ailleurs avant la mise en place, un fichier libéré avant une
    /// nouvelle tentative.
    /// </summary>
    internal sealed record Hooks(Action? BeforeCommit = null, Action? BeforeRetry = null);

    /// <summary>Seule suppression permise ici : un temporaire créé par cette classe.</summary>
    /// <returns><see langword="false"/> si le temporaire n'a pas pu être retiré.</returns>
    private static bool TryDeleteTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
