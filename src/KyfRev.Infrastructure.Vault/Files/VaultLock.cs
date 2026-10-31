using KyfRev.Infrastructure.Vault.Format;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>
/// Verrou du coffre : <c>.suivi/verrou</c> est verrouillé par le système tant que le coffre est
/// ouvert, ce qui empêche deux instances (de cette version ou de la version 0.x) de l'ouvrir en
/// même temps. Le système le libère même si le processus s'arrête brutalement.
/// </summary>
public sealed class VaultLock : IDisposable
{
    private readonly FileStream _stream;

    private VaultLock(FileStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// Prend le verrou, puis écrit <c>.suivi/verrou-info.json</c> (poste, processus, heure).
    /// </summary>
    /// <param name="root">Racine du coffre.</param>
    /// <param name="now">Instant présent, inscrit dans <c>verrou-info.json</c>.</param>
    /// <param name="holder">Qui tient déjà le coffre ouvert, si le verrou est pris et que c'est connu.</param>
    /// <returns>Le verrou, à libérer à la fermeture du coffre ; <see langword="null"/> s'il est déjà pris.</returns>
    public static VaultLock? TryAcquire(string root, DateTimeOffset now, out LockInfoFile? holder)
    {
        var stream = new FileStream(
            VaultLayout.Absolute(root, VaultLayout.LockFile),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);
        if (!TryLock(stream))
        {
            stream.Dispose();
            holder = ReadHolder(root);
            return null;
        }

        var info = new LockInfoFile(
            Environment.GetEnvironmentVariable("COMPUTERNAME") ?? "cet ordinateur",
            Environment.ProcessId,
            now,
            UnknownProperties.None);
        AtomicFile.Replace(VaultLayout.Absolute(root, VaultLayout.LockInfoFile), info.Write());
        holder = null;
        return new VaultLock(stream);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            _stream.Unlock(0, long.MaxValue);
        }
        _stream.Dispose();
    }

    /// <summary>Même plage que <c>File::try_lock</c> de Rust : les deux verrous s'excluent.</summary>
    private static bool TryLock(FileStream stream)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            try
            {
                stream.Lock(0, long.MaxValue);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
        throw new PlatformNotSupportedException("Le verrou du coffre n'est disponible que sous Windows et Linux.");
    }

    private static LockInfoFile? ReadHolder(string root)
    {
        try
        {
            return LockInfoFile.Read(File.ReadAllBytes(VaultLayout.Absolute(root, VaultLayout.LockInfoFile)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Json.VaultFormatException)
        {
            return null;
        }
    }
}
