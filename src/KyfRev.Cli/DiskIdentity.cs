using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace KyfRev.Cli;

/// <summary>
/// Ce qu'est un dossier sur le disque, quel que soit le chemin écrit qui y mène : le numéro de série de son volume et son
/// identifiant de fichier (<c>GetFileInformationByHandle</c>). Un lecteur substitué, un partage, une jonction ou un nom
/// court 8.3 mènent au même dossier, donc à la même identité.
/// </summary>
/// <param name="Volume">Le numéro de série du volume.</param>
/// <param name="File">L'identifiant du dossier sur ce volume.</param>
internal readonly partial record struct DiskIdentity(uint Volume, ulong File)
{
    /// <summary><c>FILE_READ_ATTRIBUTES</c> : lire les attributs, sans rien ouvrir d'autre.</summary>
    private const uint ReadAttributes = 0x80;

    /// <summary><c>FILE_FLAG_BACKUP_SEMANTICS</c> : ce qui permet d'ouvrir un dossier.</summary>
    private const uint BackupSemantics = 0x02000000;

    /// <summary>L'identité du dossier <paramref name="folder"/> ; rien s'il ne s'ouvre pas, ou hors de Windows.</summary>
    public static DiskIdentity? Of(string folder)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        using var handle = CreateFileW(folder, ReadAttributes, FileShare.ReadWrite | FileShare.Delete, 0, FileMode.Open, BackupSemantics, 0);
        return !handle.IsInvalid && GetFileInformationByHandle(handle, out var information)
            ? new DiskIdentity(information.VolumeSerialNumber, ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow)
            : null;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, FileShare share, nint security, FileMode mode, uint flags, nint template);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    /// <summary><c>BY_HANDLE_FILE_INFORMATION</c> : chaque date en deux mots de 32 bits.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public uint CreationLow;
        public uint CreationHigh;
        public uint AccessLow;
        public uint AccessHigh;
        public uint WriteLow;
        public uint WriteHigh;
        public uint VolumeSerialNumber;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
