using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes des onglets de la vue « Paramètres », ceux de la 0.x, lus dans les ressources (<c>Resources/SettingsHubText.resx</c>).</summary>
public static class SettingsHubText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SettingsHubText", typeof(SettingsHubText).Assembly);

    /// <summary>« À propos ».</summary>
    public static string About => Text(nameof(About));

    /// <summary>« {0} annotations ».</summary>
    public static string AnnotationManyOf(object count) => Format("AnnotationMany", count);

    /// <summary>« {0} annotation ».</summary>
    public static string AnnotationOneOf(object count) => Format("AnnotationOne", count);

    /// <summary>« Sauvegarde faite. ».</summary>
    public static string BackupDone => Text(nameof(BackupDone));

    /// <summary>« Une sauvegarde .zip quotidienne, vérifiée, rangée hors du coffre da… ».</summary>
    public static string BackupHelpOf(object folder) => Format("BackupHelp", folder);

    /// <summary>« Sauvegarder maintenant ».</summary>
    public static string BackupNow => Text(nameof(BackupNow));

    /// <summary>« Sauvegarde restaurée dans « {0} ». Le coffre actuel n'a pas été mod… ».</summary>
    public static string BackupRestoredOf(object folder) => Format("BackupRestored", folder);

    /// <summary>« Sauvegardes ».</summary>
    public static string BackupsTitle => Text(nameof(BackupsTitle));

    /// <summary>« {0} octets ».</summary>
    public static string BytesOf(object size) => Format("Bytes", size);

    /// <summary>« Créer un nouveau coffre… ».</summary>
    public static string CreateVault => Text(nameof(CreateVault));

    /// <summary>« Données ».</summary>
    public static string Data => Text(nameof(Data));

    /// <summary>« Supprimer définitivement ».</summary>
    public static string Erase => Text(nameof(Erase));

    /// <summary>« {0} sera effacé du coffre. Cela ne s'annule pas… ».</summary>
    public static string EraseConfirmOf(object title) => Format("EraseConfirm", title);

    /// <summary>« Supprimer définitivement ? ».</summary>
    public static string EraseConfirmTitle => Text(nameof(EraseConfirmTitle));

    /// <summary>« Supprimer définitivement : {0} ».</summary>
    public static string EraseItemOf(object title) => Format("EraseItem", title);

    /// <summary>« {0} : supprimé définitivement. ».</summary>
    public static string ErasedOf(object title) => Format("Erased", title);

    /// <summary>« Annotations (Markdown) ».</summary>
    public static string ExportAnnotations => Text(nameof(ExportAnnotations));

    /// <summary>« Citations (Markdown et CSV) ».</summary>
    public static string ExportQuotes => Text(nameof(ExportQuotes));

    /// <summary>« Suivi des révisions (CSV) ».</summary>
    public static string ExportTracking => Text(nameof(ExportTracking));

    /// <summary>« Coffre complet (.zip) ».</summary>
    public static string ExportVault => Text(nameof(ExportVault));

    /// <summary>« Exporté dans « {0} ». ».</summary>
    public static string ExportedOf(object folder) => Format("Exported", folder);

    /// <summary>« Dans le dossier de votre choix, hors du coffre ; aucun fichier exis… ».</summary>
    public static string ExportsHelp => Text(nameof(ExportsHelp));

    /// <summary>« Exports ».</summary>
    public static string ExportsTitle => Text(nameof(ExportsTitle));

    /// <summary>« {0} Ko ».</summary>
    public static string KilobytesOf(object size) => Format("Kilobytes", size);

    /// <summary>« Licence ».</summary>
    public static string LicenseLabel => Text(nameof(LicenseLabel));

    /// <summary>« MIT. Les licences de kyfRev et des bibliothèques reprises sont dans… ».</summary>
    public static string LicenseText => Text(nameof(LicenseText));

    /// <summary>« {0} Mo ».</summary>
    public static string MegabytesOf(object size) => Format("Megabytes", size);

    /// <summary>« développement ».</summary>
    public static string ModeDevelopment => Text(nameof(ModeDevelopment));

    /// <summary>« installé (le programme dans son dossier, vos données dans votre pro… ».</summary>
    public static string ModeInstalled => Text(nameof(ModeInstalled));

    /// <summary>« Mode ».</summary>
    public static string ModeLabel => Text(nameof(ModeLabel));

    /// <summary>« portable (tout reste dans le dossier MesRévisions) ».</summary>
    public static string ModePortable => Text(nameof(ModePortable));

    /// <summary>« Aucune sauvegarde pour l'instant. ».</summary>
    public static string NoBackup => Text(nameof(NoBackup));

    /// <summary>« Aucun élément supprimé. ».</summary>
    public static string NoTrash => Text(nameof(NoTrash));

    /// <summary>« Ouvrir ce coffre ».</summary>
    public static string OpenRestored => Text(nameof(OpenRestored));

    /// <summary>« Ouvrir la fiche de : {0} ».</summary>
    public static string OpenSheetOf(object title) => Format("OpenSheet", title);

    /// <summary>« Ouvrir un autre coffre… ».</summary>
    public static string OpenVault => Text(nameof(OpenVault));

    /// <summary>« Passage restauré. ».</summary>
    public static string PassageRestored => Text(nameof(PassageRestored));

    /// <summary>« Choisissez le dossier de l'export ».</summary>
    public static string PickExport => Text(nameof(PickExport));

    /// <summary>« Choisissez un dossier vide pour le nouveau coffre ».</summary>
    public static string PickNewVault => Text(nameof(PickNewVault));

    /// <summary>« Choisissez le dossier du coffre ».</summary>
    public static string PickVault => Text(nameof(PickVault));

    /// <summary>« « {0} » ».</summary>
    public static string QuotedOf(object text) => Format("Quoted", text);

    /// <summary>« Restaurer ».</summary>
    public static string Restore => Text(nameof(Restore));

    /// <summary>« Restaurer la sauvegarde du {0} ».</summary>
    public static string RestoreBackupOf(object moment) => Format("RestoreBackup", moment);

    /// <summary>« Restaurer… ».</summary>
    public static string RestoreBackupLabel => Text(nameof(RestoreBackupLabel));

    /// <summary>« La sauvegarde du {0} sera extraite dans un nouveau dossier, à côté … ».</summary>
    public static string RestoreConfirmOf(object moment) => Format("RestoreConfirm", moment);

    /// <summary>« Restaurer cette sauvegarde ? ».</summary>
    public static string RestoreConfirmTitle => Text(nameof(RestoreConfirmTitle));

    /// <summary>« Restaurer : {0} ».</summary>
    public static string RestoreItemOf(object title) => Format("RestoreItem", title);

    /// <summary>« « {0} » est restaurée. ».</summary>
    public static string RestoredOf(object title) => Format("Restored", title);

    /// <summary>« Révisions ».</summary>
    public static string Revisions => Text(nameof(Revisions));

    /// <summary>« Rotation : garder 14 quotidiennes, 8 hebdomadaires, 24 mensuelles ».</summary>
    public static string Rotation => Text(nameof(Rotation));

    /// <summary>« Fiche ».</summary>
    public static string SheetKind => Text(nameof(SheetKind));

    /// <summary>« Thèmes des paramètres ».</summary>
    public static string TabsName => Text(nameof(TabsName));

    /// <summary>« Les fiches, passages et définitions supprimés restent ici, restaura… ».</summary>
    public static string TrashHelp => Text(nameof(TrashHelp));

    /// <summary>« Éléments supprimés ».</summary>
    public static string TrashTitle => Text(nameof(TrashTitle));

    /// <summary>« supprimée le {0} ».</summary>
    public static string TrashedOnOf(object day) => Format("TrashedOn", day);

    /// <summary>« Le coffre actuel est simplement fermé, sans rien modifier. kyfRev r… ».</summary>
    public static string VaultHelpInstalled => Text(nameof(VaultHelpInstalled));

    /// <summary>« Ce coffre est déjà ouvert. ».</summary>
    public static string VaultAlreadyOpen => Text(nameof(VaultAlreadyOpen));

    /// <summary>« Le coffre actuel est simplement fermé, sans rien modifier. En versi… ».</summary>
    public static string VaultHelpPortable => Text(nameof(VaultHelpPortable));

    /// <summary>« Ouvert : {0} ».</summary>
    public static string VaultOpenOf(object folder) => Format("VaultOpen", folder);

    /// <summary>« Coffre ».</summary>
    public static string VaultTitle => Text(nameof(VaultTitle));

    /// <summary>« kyfRev {0} ».</summary>
    public static string VersionOf(object version) => Format("Version", version);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
