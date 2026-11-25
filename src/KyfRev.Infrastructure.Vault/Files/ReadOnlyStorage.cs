using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Vault.Files;

/// <summary>Un coffre ouvert en lecture seule refuse toute écriture ; le message le dit tel quel.</summary>
public sealed class ReadOnlyVaultException : IOException
{
    /// <summary>Le refus, avec son message.</summary>
    public ReadOnlyVaultException()
        : base("Ce coffre est ouvert en lecture seule : rien ne s'y écrit. Passez en écriture pour le modifier.")
    {
    }

    /// <summary>Le refus, avec un autre message.</summary>
    public ReadOnlyVaultException(string message)
        : base(message)
    {
    }

    /// <summary>Le refus, avec un autre message et sa cause.</summary>
    public ReadOnlyVaultException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Un stockage qui lit comme celui qu'il enveloppe et refuse toute écriture : celui d'un coffre ouvert en lecture seule,
/// quand un autre poste l'a ouvert. Rien ne s'y écrit, pas même un dossier.
/// </summary>
/// <param name="inner">Le stockage du coffre.</param>
public sealed class ReadOnlyStorage(VaultStorage inner) : VaultStorage
{
    /// <inheritdoc/>
    public override string? Root => inner.Root;

    /// <inheritdoc/>
    public override VaultStorage Raw => ReferenceEquals(inner.Raw, inner) ? this : new ReadOnlyStorage(inner.Raw);

    /// <inheritdoc/>
    public override bool FileExists(string path) => inner.FileExists(path);

    /// <inheritdoc/>
    public override bool DirectoryExists(string path) => inner.DirectoryExists(path);

    /// <inheritdoc/>
    public override byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);

    /// <inheritdoc/>
    public override byte[] ReadShared(string path) => inner.ReadShared(path);

    /// <inheritdoc/>
    public override string ReadAllText(string path) => inner.ReadAllText(path);

    /// <inheritdoc/>
    public override long LengthOf(string path) => inner.LengthOf(path);

    /// <inheritdoc/>
    public override DateTime LastWriteTimeUtc(string path) => inner.LastWriteTimeUtc(path);

    /// <inheritdoc/>
    public override bool TryCreateNew(string path, ReadOnlySpan<byte> content) => throw new ReadOnlyVaultException();

    /// <inheritdoc/>
    public override void Replace(string path, ReadOnlySpan<byte> content) => throw new ReadOnlyVaultException();

    /// <inheritdoc/>
    public override void CreateDirectory(string path)
    {
        if (!inner.DirectoryExists(path))
        {
            throw new ReadOnlyVaultException();
        }
    }

    /// <inheritdoc/>
    public override void MoveFile(string source, string destination) => throw new ReadOnlyVaultException();

    /// <inheritdoc/>
    public override void MoveDirectory(string source, string destination) => throw new ReadOnlyVaultException();

    /// <inheritdoc/>
    public override void Purge(string path) => throw new ReadOnlyVaultException();

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> Entries(string folder) => inner.Entries(folder);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Files(string folder, string pattern) => inner.Files(folder, pattern);

    /// <inheritdoc/>
    public override IReadOnlyList<StorageEntry> AllEntries(string folder) => inner.AllEntries(folder);

    /// <inheritdoc/>
    public override string RealPath(string path) => inner.RealPath(path);

    /// <inheritdoc/>
    public override string RealPathBelow(string realBase, string relative) => inner.RealPathBelow(realBase, relative);

    /// <inheritdoc/>
    public override bool SameEntry(string first, string second) => inner.SameEntry(first, second);

    /// <inheritdoc/>
    public override IReadOnlyList<StoredFile> FilesOfBackup(IReadOnlyList<StoredFile> entries, string backup) => inner.FilesOfBackup(entries, backup);

    /// <inheritdoc/>
    public override IEnumerable<string> FilesToBackUp(IEnumerable<string> files) => inner.FilesToBackUp(files);

    /// <inheritdoc/>
    public override void Reload() => inner.Reload();
}
