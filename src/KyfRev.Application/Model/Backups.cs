namespace KyfRev.Application.Model;

/// <summary>Durées de conservation des sauvegardes : 14 quotidiennes, 8 hebdomadaires, 24 mensuelles.</summary>
public sealed record RotationPolicy(int Days = 14, int Weeks = 8, int Months = 24)
{
    /// <summary>La politique par défaut.</summary>
    public static RotationPolicy Default { get; } = new();
}

/// <summary>Une sauvegarde écrite et vérifiée.</summary>
/// <param name="Path">Fichier <c>.zip</c>.</param>
/// <param name="Timestamp">Heure locale inscrite dans son nom.</param>
/// <param name="FileCount">Fichiers du coffre qu'elle contient.</param>
public sealed record BackupFile(string Path, DateTime Timestamp, int FileCount);

/// <summary>Une sauvegarde du dossier des sauvegardes.</summary>
/// <param name="Name">Nom du fichier (« coffre-2026-10-09T18-30-05.zip »).</param>
/// <param name="Timestamp">Date et heure locales inscrites dans son nom.</param>
/// <param name="Size">Taille en octets.</param>
public sealed record BackupEntry(string Name, DateTime Timestamp, long Size);

/// <summary>Les sauvegardes du coffre ouvert.</summary>
/// <param name="Folder">Dossier des sauvegardes, hors du coffre.</param>
/// <param name="Rotation">La rotation est active.</param>
/// <param name="Backups">De la plus récente à la plus ancienne.</param>
public sealed record BackupList(string Folder, bool Rotation, IReadOnlyList<BackupEntry> Backups);
