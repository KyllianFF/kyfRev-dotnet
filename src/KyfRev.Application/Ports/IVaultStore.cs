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

/// <summary>
/// Où sont les coffres de ce poste : celui que prévoit le mode portable ou installé, et la
/// mémoire du dernier coffre ouvert, rouvert au démarrage.
/// </summary>
public interface IVaultLocations
{
    /// <summary>Mode de l'application, selon ce qui se trouve à côté de l'exécutable.</summary>
    AppMode Mode { get; }

    /// <summary>
    /// Coffre à ouvrir au démarrage : celui du mode portable ; sinon le dernier ouvert, et à
    /// défaut celui que prévoit le mode installé.
    /// </summary>
    string? VaultToOpen();

    /// <summary>Le coffre prévu (mode portable ou installé), s'il reste à créer.</summary>
    string? VaultToCreate();

    /// <summary>Retient le coffre qui vient d'être ouvert (rien en mode portable, où il est prévu).</summary>
    /// <returns>
    /// Faux si la mémoire n'a pas pu être écrite : le coffre reste ouvert, il ne sera seulement pas
    /// rouvert de lui-même au prochain démarrage.
    /// </returns>
    bool Remember(string root);
}

/// <summary>La séance d'étude en cours, gardée sur ce poste pour être reprise.</summary>
public interface IStudySessionStore
{
    /// <summary>La séance gardée ; rien s'il n'y en a pas, ou si elle est illisible.</summary>
    StudySession? Load();

    /// <summary>Garde la séance ; faux si elle n'a pas pu l'être (elle ne sera alors pas reprise).</summary>
    bool Save(StudySession session);

    /// <summary>Oublie la séance gardée : elle est terminée. Faux si elle n'a pas pu l'être.</summary>
    bool Forget();
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
