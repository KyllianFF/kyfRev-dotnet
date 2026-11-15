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

    /// <summary>« Passages et définitions pour Anki ».</summary>
    public static string ExportAnki => Text(nameof(ExportAnki));

    /// <summary>« Un fichier à importer dans Anki ou AnkiDroid, pour réviser en déplacement. Réimporté, il met les cartes à jour sans les doubler. Ce qui est fait dans Anki ne revient pas dans kyfRev. ».</summary>
    public static string ExportAnkiHelp => Text(nameof(ExportAnkiHelp));

    /// <summary>« Échéances pour un agenda (.ics) ».</summary>
    public static string ExportCalendar => Text(nameof(ExportCalendar));

    /// <summary>« Un événement par jour de révision, et un par examen. Les titres des fiches y sont écrits : importé dans un agenda en ligne, ils quittent votre ordinateur. ».</summary>
    public static string ExportCalendarHelp => Text(nameof(ExportCalendarHelp));

    /// <summary>« Coffre complet (.zip) ».</summary>
    public static string ExportVault => Text(nameof(ExportVault));

    /// <summary>« Exporté dans « {0} ». ».</summary>
    public static string ExportedOf(object folder) => Format("Exported", folder);

    /// <summary>« Dans le dossier de votre choix, hors du coffre ; aucun fichier exis… ».</summary>
    public static string ExportsHelp => Text(nameof(ExportsHelp));

    /// <summary>« Ouvrir les applications installées ».</summary>
    public static string OpenInstalledApps => Text(nameof(OpenInstalledApps));

    /// <summary>« kyfRev {0}, l'ancienne version, est inscrit dans le même dossier… ».</summary>
    public static string LegacySharedOf(object version) => Format("LegacyShared", version);

    /// <summary>« kyfRev {0}, l'ancienne version, est encore installé… ».</summary>
    public static string LegacyInstalledOf(object version) => Format("LegacyInstalled", version);

    /// <summary>« Rechercher une mise à jour ».</summary>
    public static string CheckUpdate => Text(nameof(CheckUpdate));

    /// <summary>« Recherche… ».</summary>
    public static string CheckingUpdate => Text(nameof(CheckingUpdate));

    /// <summary>« Une version plus récente se télécharge dans kyfRev… ».</summary>
    public static string UpdateHelp => Text(nameof(UpdateHelp));

    /// <summary>« Cette copie de kyfRev ne vient pas de l'installateur… ».</summary>
    public static string UpdateHelpPortable => Text(nameof(UpdateHelpPortable));

    /// <summary>« Rechercher une mise à jour au démarrage ».</summary>
    public static string CheckAtStartup => Text(nameof(CheckAtStartup));

    /// <summary>« À chaque ouverture, kyfRev regarde si une version plus récente est publiée… ».</summary>
    public static string CheckAtStartupHelp => Text(nameof(CheckAtStartupHelp));

    /// <summary>« Mise à jour disponible ».</summary>
    public static string UpdateOfferTitle => Text(nameof(UpdateOfferTitle));

    /// <summary>« kyfRev {0} est disponible (vous avez la {1}). Elle se télécharge maintenant… ».</summary>
    public static string UpdateOfferOf(object latest, object current) => Format("UpdateOffer", latest, current);

    /// <summary>« Mettre à jour ».</summary>
    public static string UpdateNow => Text(nameof(UpdateNow));

    /// <summary>« Retarder à la prochaine ouverture ».</summary>
    public static string UpdatePostpone => Text(nameof(UpdatePostpone));

    /// <summary>« kyfRev {0} est disponible : elle vous sera proposée de nouveau… ».</summary>
    public static string UpdatePostponedOf(object latest) => Format("UpdatePostponed", latest);

    /// <summary>« Téléchargement de kyfRev {0} ».</summary>
    public static string UpdateDownloadingTitleOf(object latest) => Format("UpdateDownloadingTitle", latest);

    /// <summary>« kyfRev télécharge la nouvelle version, puis vérifie… ».</summary>
    public static string UpdateDownloading => Text(nameof(UpdateDownloading));

    /// <summary>« Téléchargement de kyfRev {0}… ».</summary>
    public static string UpdateDownloadingShortOf(object latest) => Format("UpdateDownloadingShort", latest);

    /// <summary>« {0} Mo sur {1} Mo ».</summary>
    public static string UpdateProgressOf(object received, object total) => Format("UpdateProgress", received, total);

    /// <summary>« Progression du téléchargement ».</summary>
    public static string UpdateProgressLabel => Text(nameof(UpdateProgressLabel));

    /// <summary>« Masquer ».</summary>
    public static string UpdateHide => Text(nameof(UpdateHide));

    /// <summary>« Mise à jour prête ».</summary>
    public static string UpdateReadyTitle => Text(nameof(UpdateReadyTitle));

    /// <summary>« kyfRev {0} est téléchargée et vérifiée. Pour l'installer, kyfRev doit redémarrer… ».</summary>
    public static string UpdateReadyOf(object latest) => Format("UpdateReady", latest);

    /// <summary>« kyfRev est installé pour toutes les personnes de cet ordinateur… ».</summary>
    public static string UpdateAllUsers => Text(nameof(UpdateAllUsers));

    /// <summary>« Redémarrer maintenant ».</summary>
    public static string RestartNow => Text(nameof(RestartNow));

    /// <summary>« Plus tard ».</summary>
    public static string RestartLater => Text(nameof(RestartLater));

    /// <summary>« kyfRev {0} est prête : elle s'installera quand vous fermerez kyfRev. ».</summary>
    public static string UpdateAtExitOf(object latest) => Format("UpdateAtExit", latest);

    /// <summary>« kyfRev est passé à la version {0}. ».</summary>
    public static string UpdateInstalledOf(object version) => Format("UpdateInstalled", version);

    /// <summary>« Mise à jour non installée ».</summary>
    public static string UpdateNotInstalledTitle => Text(nameof(UpdateNotInstalledTitle));

    /// <summary>« La mise à jour vers kyfRev {0} ne s'est pas installée… ».</summary>
    public static string UpdateNotInstalledOf(object latest, object current, object log) => Format("UpdateNotInstalled", latest, current, log);

    /// <summary>« Réessayer ».</summary>
    public static string UpdateRetry => Text(nameof(UpdateRetry));

    /// <summary>« Mise à jour impossible ».</summary>
    public static string UpdateFailedTitle => Text(nameof(UpdateFailedTitle));

    /// <summary>« kyfRev ne peut pas se fermer pendant une conversion… ».</summary>
    public static string UpdateCannotClose => Text(nameof(UpdateCannotClose));

    /// <summary>« Ouvrir la page de cette version ».</summary>
    public static string OpenRelease => Text(nameof(OpenRelease));

    /// <summary>« kyfRev {0} est disponible (vous avez la {1}). ».</summary>
    public static string UpdateAvailableOf(object latest, object current) => Format("UpdateAvailable", latest, current);

    /// <summary>« Vous avez la dernière version ({0}). ».</summary>
    public static string UpToDateOf(object current) => Format("UpToDate", current);

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

    /// <summary>« kyfRev {0}, publiée le {1} ».</summary>
    public static string VersionReleasedOf(object version, object day) => Format("VersionReleased", version, day);

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
