using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes de l'onglet « Modules et extensions », ceux de la 0.x, lus dans les ressources (<c>Resources/ModulesSettingsText.resx</c>).</summary>
public static class ModulesSettingsText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.ModulesSettingsText", typeof(ModulesSettingsText).Assembly);

    /// <summary>« Art ».</summary>
    public static string DomainArt => Text(nameof(DomainArt));

    /// <summary>« Pages de Wikiquote : Art, Peinture, Musique, Poésie, Littérature ».</summary>
    public static string DomainArtPages => Text(nameof(DomainArtPages));

    /// <summary>« Informatique ».</summary>
    public static string DomainComputing => Text(nameof(DomainComputing));

    /// <summary>« Pages de Wikiquote : Ordinateur, Internet, Technique ».</summary>
    public static string DomainComputingPages => Text(nameof(DomainComputingPages));

    /// <summary>« Philosophie ».</summary>
    public static string DomainPhilosophy => Text(nameof(DomainPhilosophy));

    /// <summary>« Pages de Wikiquote : Philosophie, Sagesse, Connaissance, Liberté ».</summary>
    public static string DomainPhilosophyPages => Text(nameof(DomainPhilosophyPages));

    /// <summary>« Science ».</summary>
    public static string DomainScience => Text(nameof(DomainScience));

    /// <summary>« Pages de Wikiquote : Science, Mathématiques ».</summary>
    public static string DomainSciencePages => Text(nameof(DomainSciencePages));

    /// <summary>« Domaines de la citation du jour ».</summary>
    public static string DomainsTitle => Text(nameof(DomainsTitle));

    /// <summary>« Des blocs et des vues ajoutés par vous ou par d'autres… ».</summary>
    public static string ExtensionsHelp => Text(nameof(ExtensionsHelp));

    /// <summary>« Extensions ».</summary>
    public static string ExtensionsTitle => Text(nameof(ExtensionsTitle));

    /// <summary>« Des fonctions facultatives, pour ce coffre. Désactiver un module le… ».</summary>
    public static string ModulesHelp => Text(nameof(ModulesHelp));

    /// <summary>« Modules ».</summary>
    public static string ModulesTitle => Text(nameof(ModulesTitle));

    /// <summary>« Chercher chaque jour une citation sur Wikiquote ».</summary>
    public static string Online => Text(nameof(Online));

    /// <summary>« Une requête par jour vers fr.wikiquote.org, la seule de kyfRev. Cha… ».</summary>
    public static string OnlineHelp => Text(nameof(OnlineHelp));

    /// <summary>« Module Citations désactivé : rien n'a été supprimé. ».</summary>
    public static string QuotesDisabled => Text(nameof(QuotesDisabled));

    /// <summary>« Module Citations activé. ».</summary>
    public static string QuotesEnabled => Text(nameof(QuotesEnabled));

    /// <summary>« Vos citations, et la citation du jour sur le tableau de bord. ».</summary>
    public static string QuotesHelp => Text(nameof(QuotesHelp));

    /// <summary>« Module Citations ».</summary>
    public static string QuotesModule => Text(nameof(QuotesModule));

    /// <summary>« Éditeur de topologie (équipements, câbles, VLAN, plan d'adressage),… ».</summary>
    public static string SchemasHelp => Text(nameof(SchemasHelp));

    /// <summary>« Module Schémas réseau ».</summary>
    public static string SchemasModule => Text(nameof(SchemasModule));

    /// <summary>« Module Schémas réseau activé. ».</summary>
    public static string SchemasEnabled => Text(nameof(SchemasEnabled));

    /// <summary>« Module Schémas réseau désactivé : ses schémas restent dans le coffre. ».</summary>
    public static string SchemasDisabled => Text(nameof(SchemasDisabled));

    /// <summary>« Détails » : le nom d'un <c>details</c> qui n'a pas de résumé, comme dans un navigateur.</summary>
    public static string ExtensionDetails => Text(nameof(ExtensionDetails));

    /// <summary>« Aucune extension installée. ».</summary>
    public static string ExtensionsNone => Text(nameof(ExtensionsNone));

    /// <summary>« Installer depuis un dossier… ».</summary>
    public static string ExtensionInstallFromFolder => Text(nameof(ExtensionInstallFromFolder));

    /// <summary>« Choisissez le dossier de l'extension (avec son extension.json) ».</summary>
    public static string ExtensionPick => Text(nameof(ExtensionPick));

    /// <summary>« Installer l'extension ? ».</summary>
    public static string ExtensionInstallTitle => Text(nameof(ExtensionInstallTitle));

    /// <summary>« Mettre à jour l'extension ? ».</summary>
    public static string ExtensionUpdateTitle => Text(nameof(ExtensionUpdateTitle));

    /// <summary>« Installer ».</summary>
    public static string ExtensionInstall => Text(nameof(ExtensionInstall));

    /// <summary>« Mettre à jour ».</summary>
    public static string ExtensionUpdate => Text(nameof(ExtensionUpdate));

    /// <summary>« Elle ne demande aucune permission. ».</summary>
    public static string ExtensionNoPermission => Text(nameof(ExtensionNoPermission));

    /// <summary>« Elle ne pourra ni accéder à Internet, ni modifier vos fiches. ».</summary>
    public static string ExtensionLimits => Text(nameof(ExtensionLimits));

    /// <summary>« N'installez que des extensions dont vous connaissez l'auteur. ».</summary>
    public static string ExtensionTrust => Text(nameof(ExtensionTrust));

    /// <summary>« Retirer l'extension ? ».</summary>
    public static string ExtensionRemoveTitle => Text(nameof(ExtensionRemoveTitle));

    /// <summary>« Retirer ».</summary>
    public static string ExtensionRemove => Text(nameof(ExtensionRemove));

    /// <summary>« Retirer… ».</summary>
    public static string ExtensionRemoveLabel => Text(nameof(ExtensionRemoveLabel));

    /// <summary>« {0} · {1} ».</summary>
    public static string ExtensionVersionByOf(string version, string author) => Format("ExtensionVersionBy", version, author);

    /// <summary>« Ajoute : {0} ».</summary>
    public static string ExtensionAddsOf(string adds) => Format("ExtensionAdds", adds);

    /// <summary>« bloc « {0} » ».</summary>
    public static string ExtensionBlockOf(string language) => Format("ExtensionBlock", language);

    /// <summary>« vue « {0} » ».</summary>
    public static string ExtensionViewOf(string title) => Format("ExtensionView", title);

    /// <summary>« {0} {1} ».</summary>
    public static string ExtensionSummaryOf(string name, string version) => Format("ExtensionSummary", name, version);

    /// <summary>« {0} {1}, par {2} ».</summary>
    public static string ExtensionSummaryByOf(string name, string version, string author) => Format("ExtensionSummaryBy", name, version, author);

    /// <summary>« Elle remplace la version {0}, rangée (et non effacée). ».</summary>
    public static string ExtensionReplacesOf(string version) => Format("ExtensionReplaces", version);

    /// <summary>« Extension « {0} » {1} installée et activée. ».</summary>
    public static string ExtensionInstalledOf(string name, string version) => Format("ExtensionInstalled", name, version);

    /// <summary>« Extension « {0} » activée. ».</summary>
    public static string ExtensionEnabledOf(string name) => Format("ExtensionEnabled", name);

    /// <summary>« Extension « {0} » désactivée. ».</summary>
    public static string ExtensionDisabledOf(string name) => Format("ExtensionDisabled", name);

    /// <summary>« Extension « {0} » retirée ; ses données restent dans le coffre. ».</summary>
    public static string ExtensionRemovedOf(string name) => Format("ExtensionRemoved", name);

    /// <summary>« « {0} » disparaît de kyfRev. Son dossier est rangé (et non effacé), ses données restent dans le coffre : la réinstaller les retrouve. ».</summary>
    public static string ExtensionRemoveDetailsOf(string name) => Format("ExtensionRemoveDetails", name);

    /// <summary>« Retirer {0} ».</summary>
    public static string ExtensionRemoveNameOf(string name) => Format("ExtensionRemoveName", name);

    /// <summary>« {0} fichier(s), {1}. ».</summary>
    public static string ExtensionFilesOf(int count, string size) => Format("ExtensionFiles", count, size);

    /// <summary>« Elle ne pourra ni accéder à Internet, ni supprimer de fiche. ».</summary>
    public static string ExtensionLimitsWriting => Text(nameof(ExtensionLimitsWriting));

    /// <summary>« Elle ajoute ».</summary>
    public static string ExtensionAddsTitle => Text(nameof(ExtensionAddsTitle));

    /// <summary>« Elle pourra ».</summary>
    public static string ExtensionRightsTitle => Text(nameof(ExtensionRightsTitle));

    /// <summary>« Décochez une permission pour la retirer : l'extension reçoit alors un refus quand elle s'en sert. ».</summary>
    public static string ExtensionRightsHelp => Text(nameof(ExtensionRightsHelp));

    /// <summary>« Lire toutes vos fiches, sans pouvoir les modifier ».</summary>
    public static string ExtensionRightReadSheets => Text(nameof(ExtensionRightReadSheets));

    /// <summary>« Garder ses propres données dans le coffre ».</summary>
    public static string ExtensionRightStorage => Text(nameof(ExtensionRightStorage));

    /// <summary>« Voir le texte que vous sélectionnez, quand vous cliquez sur son bouton ».</summary>
    public static string ExtensionRightSelection => Text(nameof(ExtensionRightSelection));

    /// <summary>« Créer des fiches et changer leurs étiquettes ».</summary>
    public static string ExtensionRightCreateSheets => Text(nameof(ExtensionRightCreateSheets));

    /// <summary>« Modifier le texte de vos fiches ».</summary>
    public static string ExtensionRightWriteSheets => Text(nameof(ExtensionRightWriteSheets));

    /// <summary>« permission sensible ».</summary>
    public static string ExtensionRiskNotable => Text(nameof(ExtensionRiskNotable));

    /// <summary>« permission à risque ».</summary>
    public static string ExtensionRiskHigh => Text(nameof(ExtensionRiskHigh));

    /// <summary>« Elle pourra modifier le texte de vos fiches. Chaque modification garde la version précédente de la fiche. ».</summary>
    public static string ExtensionHighRisk => Text(nameof(ExtensionHighRisk));

    /// <summary>« Cette version demande de nouvelles permissions, marquées « Nouvelle ». ».</summary>
    public static string ExtensionNewRights => Text(nameof(ExtensionNewRights));

    /// <summary>« Nouvelle ».</summary>
    public static string ExtensionRightNew => Text(nameof(ExtensionRightNew));

    /// <summary>« Retirée par vous : elle le reste. ».</summary>
    public static string ExtensionRightKeptWithdrawn => Text(nameof(ExtensionRightKeptWithdrawn));

    /// <summary>« commande « {0} » dans la recherche ».</summary>
    public static string ExtensionCommandOf(string title) => Format("ExtensionCommand", title);

    /// <summary>« {0} commandes dans la recherche ».</summary>
    public static string ExtensionCommandsOf(int count) => Format("ExtensionCommands", count);

    /// <summary>« « {0} » au clic droit des fiches ».</summary>
    public static string ExtensionSheetMenuOf(string title) => Format("ExtensionSheetMenu", title);

    /// <summary>« « {0} » au clic droit des dossiers ».</summary>
    public static string ExtensionFolderMenuOf(string title) => Format("ExtensionFolderMenu", title);

    /// <summary>« encart « {0} » sur Aujourd'hui ».</summary>
    public static string ExtensionCardOf(string title) => Format("ExtensionCard", title);

    /// <summary>« Permission retirée à « {0} » : « {1} ». Elle reçoit maintenant un refus. ».</summary>
    public static string ExtensionRightWithdrawnOf(string name, string right) => Format("ExtensionRightWithdrawn", name, right);

    /// <summary>« Permission rendue à « {0} » : « {1} ». ».</summary>
    public static string ExtensionRightGrantedOf(string name, string right) => Format("ExtensionRightGranted", name, right);

    /// <summary>« Réactiver l'extension ? ».</summary>
    public static string ExtensionReactivateTitle => Text(nameof(ExtensionReactivateTitle));

    /// <summary>« Réactiver ».</summary>
    public static string ExtensionReactivate => Text(nameof(ExtensionReactivate));

    /// <summary>« Désactivée, elle n'a plus aucune permission ; la réactiver les redemande. ».</summary>
    public static string ExtensionRightsInactive => Text(nameof(ExtensionRightsInactive));

    /// <summary>« Extension « {0} » réactivée, avec les permissions confirmées. ».</summary>
    public static string ExtensionReactivatedOf(string name) => Format("ExtensionReactivated", name);

    /// <summary>« Modules et extensions ».</summary>
    public static string Tab => Text(nameof(Tab));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
