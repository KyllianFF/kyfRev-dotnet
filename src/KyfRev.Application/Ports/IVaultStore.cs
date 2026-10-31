using KyfRev.Application.Model;
using KyfRev.Domain.Calendar;

namespace KyfRev.Application.Ports;

/// <summary>Ouverture et création des coffres.</summary>
public interface IVaultStore
{
    /// <summary>Ouvre un coffre existant (verrou, reprise d'une opération interrompue, analyse).</summary>
    IVault Open(string root, Moment now);

    /// <summary>Crée un coffre dans un dossier vide, puis l'ouvre.</summary>
    IVault Initialize(string root, Moment now);

    /// <summary>Vrai si les deux chemins désignent le même dossier (liens résolus).</summary>
    bool SameLocation(string first, string second);
}

/// <summary>Fichiers de sauvegarde, hors de tout coffre.</summary>
public interface IBackupFiles
{
    /// <summary>Sauvegardes reconnues dans <paramref name="folder"/>, de la plus récente à la plus ancienne (rien s'il n'existe pas).</summary>
    IReadOnlyList<BackupEntry> List(string folder);

    /// <summary>Vrai si aucune sauvegarde n'a encore été faite ce jour-là.</summary>
    bool DailyBackupNeeded(string folder, DateOnly day);

    /// <summary>Applique la rotation ; rend les copies retirées.</summary>
    IReadOnlyList<string> ApplyRotation(string folder, RotationPolicy policy);

    /// <summary>Restaure une sauvegarde dans un nouveau dossier de <paramref name="parentFolder"/> ; rend ce dossier.</summary>
    string Restore(string backup, string parentFolder, DateOnly day);

    /// <summary>Une sauvegarde écrite : son nom, l'horodatage lu dans ce nom, sa taille.</summary>
    BackupEntry Describe(string path);

    /// <summary>Horodatage lu dans un nom de sauvegarde ; rien si le nom ne suit pas exactement le modèle.</summary>
    DateTime? TimestampOf(string name);

    /// <summary>Vrai si <paramref name="path"/> est un fichier.</summary>
    bool IsFile(string path);
}

/// <summary>Mémoire des coffres ouverts : le dernier est rouvert au démarrage.</summary>
public interface IRecentVaults
{
    /// <summary>Retient le coffre qui vient d'être ouvert.</summary>
    void Remember(string root);
}

/// <summary>
/// Lecture d'une adresse en ligne, durcie : HTTPS seulement, ni redirection ni cookie, réponse
/// limitée en taille et en durée. Seule l'adresse demandée part ; un échec est une
/// <see cref="OperationException"/>.
/// </summary>
public interface IOnlineReader
{
    /// <summary>Texte de la réponse à <paramref name="address"/>.</summary>
    Task<string> ReadTextAsync(Uri address, CancellationToken cancellationToken);
}
