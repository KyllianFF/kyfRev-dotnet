using KyfRev.Application.Ports;
using KyfRev.Infrastructure.Vault.Files;

namespace KyfRev.Tests.Support;

/// <summary>
/// Le disque, qui échoue à la demande : l'écriture numéro <see cref="FailAt"/> lève une <see cref="IOException"/>, et avec
/// <see cref="Cut"/> toutes les suivantes aussi, comme après une coupure. Chaque appel qui peut écrire compte pour une
/// écriture : création, remplacement, dossier, déplacement, effacement. Les lectures passent toujours.
/// </summary>
/// <remarks>Partagé par les projets d'essais du coffre et du chiffrement (fichier lié).</remarks>
internal sealed class FailingDisk : VaultStorage
{
    private readonly VaultStorage _disk = DiskStorage.Instance;

    /// <summary>Le nombre d'écritures tentées.</summary>
    public int Writes { get; private set; }

    /// <summary>L'écriture qui échoue, à partir de 1 ; rien : aucune.</summary>
    public int? FailAt { get; set; }

    /// <summary>Vrai : toutes les écritures échouent à partir de <see cref="FailAt"/>, rien ne s'écrit plus.</summary>
    public bool Cut { get; set; }

    /// <summary>Les écritures échouées.</summary>
    public int Failures { get; private set; }

    public override bool FileExists(string path) => _disk.FileExists(path);

    public override bool DirectoryExists(string path) => _disk.DirectoryExists(path);

    public override byte[] ReadAllBytes(string path) => _disk.ReadAllBytes(path);

    public override byte[] ReadShared(string path) => _disk.ReadShared(path);

    public override string ReadAllText(string path) => _disk.ReadAllText(path);

    public override long LengthOf(string path) => _disk.LengthOf(path);

    public override DateTime LastWriteTimeUtc(string path) => _disk.LastWriteTimeUtc(path);

    public override bool TryCreateNew(string path, ReadOnlySpan<byte> content)
    {
        Write(path);
        return _disk.TryCreateNew(path, content);
    }

    public override void Replace(string path, ReadOnlySpan<byte> content)
    {
        Write(path);
        _disk.Replace(path, content);
    }

    public override void CreateDirectory(string path)
    {
        Write(path);
        _disk.CreateDirectory(path);
    }

    public override void MoveFile(string source, string destination)
    {
        Write(destination);
        _disk.MoveFile(source, destination);
    }

    public override void MoveDirectory(string source, string destination)
    {
        Write(destination);
        _disk.MoveDirectory(source, destination);
    }

    public override void Purge(string path)
    {
        Write(path);
        _disk.Purge(path);
    }

    public override IReadOnlyList<StorageEntry> Entries(string folder) => _disk.Entries(folder);

    public override IReadOnlyList<string> Files(string folder, string pattern) => _disk.Files(folder, pattern);

    public override IReadOnlyList<StorageEntry> AllEntries(string folder) => _disk.AllEntries(folder);

    public override string RealPath(string path) => _disk.RealPath(path);

    public override string RealPathBelow(string realBase, string relative) => _disk.RealPathBelow(realBase, relative);

    public override bool SameEntry(string first, string second) => _disk.SameEntry(first, second);

    private void Write(string path)
    {
        Writes++;
        if (FailAt is { } failAt && (Writes == failAt || (Cut && Writes > failAt)))
        {
            Failures++;
            throw new IOException($"Coupure simulée à l'écriture {Writes} : « {path} ».");
        }
    }
}
