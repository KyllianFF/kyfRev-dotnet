using KyfRev.Application.Ports;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Tous les cas d'usage, regroupés par domaine, autour d'un même coffre ouvert. La racine de
/// composition (l'application, les tests) fournit les ports : le coffre, les sauvegardes, les
/// emplacements des coffres, l'horloge et le journal.
/// </summary>
public sealed class KyfRevApplication : IDisposable
{
    private readonly VaultHost _host = new();

    /// <summary>Assemble les cas d'usage.</summary>
    /// <param name="store">Ouverture et création des coffres.</param>
    /// <param name="backups">Fichiers de sauvegarde.</param>
    /// <param name="locations">Coffre prévu et mémoire du dernier coffre ouvert.</param>
    /// <param name="time">Horloge : seule source de la date du jour.</param>
    /// <param name="logging">Journal de diagnostic.</param>
    /// <param name="version">Version de l'application.</param>
    public KyfRevApplication(IVaultStore store, IBackupFiles backups, IVaultLocations locations, TimeProvider time, ILoggerFactory logging, string version)
    {
        ArgumentNullException.ThrowIfNull(logging);
        Vaults = new VaultUseCases(_host, store, backups, locations, time, logging.CreateLogger<VaultUseCases>(), version);
        Sheets = new SheetUseCases(_host, time, logging.CreateLogger<SheetUseCases>());
        Editing = new EditingUseCases(_host, time);
        Folders = new FolderUseCases(_host, time);
        Trash = new TrashUseCases(_host, time);
        Reviews = new ReviewUseCases(_host, time);
        Reminders = new ReminderUseCases(_host, time);
        Annotations = new AnnotationUseCases(_host, time);
        Exams = new ExamUseCases(_host, time);
        Settings = new SettingsUseCases(_host);
        Templates = new TemplateUseCases(_host);
        Maintenance = new MaintenanceUseCases(_host, backups, time);
        Backups = new BackupUseCases(_host, backups, time);
        Exports = new ExportUseCases(_host, time);
    }

    /// <summary>Le coffre ouvert, que les modules partagent avec les cas d'usage du cœur.</summary>
    public VaultHost Host => _host;

    /// <summary>Ouverture et création du coffre.</summary>
    public VaultUseCases Vaults { get; }

    /// <summary>Fiches.</summary>
    public SheetUseCases Sheets { get; }

    /// <summary>Édition et versions.</summary>
    public EditingUseCases Editing { get; }

    /// <summary>Dossiers.</summary>
    public FolderUseCases Folders { get; }

    /// <summary>Corbeille.</summary>
    public TrashUseCases Trash { get; }

    /// <summary>Méthode des J.</summary>
    public ReviewUseCases Reviews { get; }

    /// <summary>Rappels de consolidation.</summary>
    public ReminderUseCases Reminders { get; }

    /// <summary>Passages annotés et définitions.</summary>
    public AnnotationUseCases Annotations { get; }

    /// <summary>Examens.</summary>
    public ExamUseCases Exams { get; }

    /// <summary>Réglages.</summary>
    public SettingsUseCases Settings { get; }

    /// <summary>Modèles de fiche.</summary>
    public TemplateUseCases Templates { get; }

    /// <summary>Santé du coffre et réparations.</summary>
    public MaintenanceUseCases Maintenance { get; }

    /// <summary>Sauvegardes.</summary>
    public BackupUseCases Backups { get; }

    /// <summary>Exports.</summary>
    public ExportUseCases Exports { get; }

    /// <summary>Ferme le coffre ouvert.</summary>
    public void Dispose() => _host.Dispose();
}
