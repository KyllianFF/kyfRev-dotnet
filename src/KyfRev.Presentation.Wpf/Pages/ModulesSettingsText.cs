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

    /// <summary>« L'installation d'extensions n'est pas encore disponible dans cette … ».</summary>
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

    /// <summary>« Modules et extensions ».</summary>
    public static string Tab => Text(nameof(Tab));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
