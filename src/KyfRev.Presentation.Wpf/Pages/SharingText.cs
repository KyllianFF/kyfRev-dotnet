using System.Globalization;
using System.Resources;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>Textes du partage d'une fiche, des modèles et de l'import d'un dossier, ceux de la 0.x, lus dans les ressources (<c>Resources/SharingText.resx</c>).</summary>
public static class SharingText
{
    private static readonly ResourceManager Resources = new("KyfRev.Presentation.Wpf.Resources.SharingText", typeof(SharingText).Assembly);

    /// <summary>« Copier ».</summary>
    public static string ActionCopy => Text(nameof(ActionCopy));

    /// <summary>« Enregistrer… ».</summary>
    public static string ActionSave => Text(nameof(ActionSave));

    /// <summary>« Annuler ».</summary>
    public static string Cancel => Text(nameof(Cancel));

    /// <summary>« Ce coffre est chiffré, mais pas ce que vous partagez… ».</summary>
    public static string ClearWarning => Text(nameof(ClearWarning));

    /// <summary>« Fermer ».</summary>
    public static string Close => Text(nameof(Close));

    /// <summary>« Le presse-papiers est pris par un autre programme… ».</summary>
    public static string CopyFailed => Text(nameof(CopyFailed));

    /// <summary>« Fiche copiée : collez-la où vous voulez (Ctrl+V). ».</summary>
    public static string Copied => Text(nameof(Copied));

    /// <summary>« Fichier créé : … ».</summary>
    public static string CreatedOf(object path) => Format("Created", path);

    /// <summary>« [Diagramme] ».</summary>
    public static string Diagram => Text(nameof(Diagram));

    /// <summary>« Schéma » : le nom d'un dessin dans la page partagée.</summary>
    public static string DiagramName => Text(nameof(DiagramName));

    /// <summary>« Fiche partagée depuis kyfRev le … ».</summary>
    public static string FooterOf(object day) => Format("Footer", day);

    /// <summary>« Markdown avec images (.zip) ».</summary>
    public static string FormatArchive => Text(nameof(FormatArchive));

    /// <summary>« La fiche et ses images, réunies dans une archive. ».</summary>
    public static string FormatArchiveHelp => Text(nameof(FormatArchiveHelp));

    /// <summary>« Copier dans le presse-papiers ».</summary>
    public static string FormatCopy => Text(nameof(FormatCopy));

    /// <summary>« Pour coller dans un e-mail, Teams, Word… ».</summary>
    public static string FormatCopyHelp => Text(nameof(FormatCopyHelp));

    /// <summary>« Page web (.html) ».</summary>
    public static string FormatHtml => Text(nameof(FormatHtml));

    /// <summary>« Un seul fichier, images, formules et diagrammes compris… ».</summary>
    public static string FormatHtmlHelp => Text(nameof(FormatHtmlHelp));

    /// <summary>« Format ».</summary>
    public static string FormatLegend => Text(nameof(FormatLegend));

    /// <summary>« Markdown (.md) ».</summary>
    public static string FormatMarkdown => Text(nameof(FormatMarkdown));

    /// <summary>« Le texte de la fiche, lisible par Obsidian, Typora, GitHub… ».</summary>
    public static string FormatMarkdownHelp => Text(nameof(FormatMarkdownHelp));

    /// <summary>« PDF (.pdf) ».</summary>
    public static string FormatPdf => Text(nameof(FormatPdf));

    /// <summary>« Un document à envoyer ou à imprimer… ».</summary>
    public static string FormatPdfHelp => Text(nameof(FormatPdfHelp));

    /// <summary>« Texte brut (.txt) ».</summary>
    public static string FormatText => Text(nameof(FormatText));

    /// <summary>« Sans mise en forme ; les formules restent en notation TeX. ».</summary>
    public static string FormatTextHelp => Text(nameof(FormatTextHelp));

    /// <summary>« [Image] ».</summary>
    public static string Image => Text(nameof(Image));

    /// <summary>« [Image : …] ».</summary>
    public static string ImageNamedOf(object name) => Format("ImageNamed", name);

    /// <summary>« Importer un dossier de notes (Obsidian, Markdown…) ».</summary>
    public static string ImportAction => Text(nameof(ImportAction));

    /// <summary>« Import… ».</summary>
    public static string ImportBusy => Text(nameof(ImportBusy));

    /// <summary>« Changer de dossier… ».</summary>
    public static string ImportChange => Text(nameof(ImportChange));

    /// <summary>« Choisir le dossier… ».</summary>
    public static string ImportChoose => Text(nameof(ImportChoose));

    /// <summary>« Importer ».</summary>
    public static string ImportConfirm => Text(nameof(ImportConfirm));

    /// <summary>« Les copier seulement ».</summary>
    public static string ImportCopyOnly => Text(nameof(ImportCopyOnly));

    /// <summary>« Vous choisirez ensuite lesquelles réviser… ».</summary>
    public static string ImportCopyOnlyHelp => Text(nameof(ImportCopyOnlyHelp));

    /// <summary>« Importer des notes existantes ».</summary>
    public static string ImportEmptyAction => Text(nameof(ImportEmptyAction));

    /// <summary>« Importer un dossier de notes… ».</summary>
    public static string ImportFolder => Text(nameof(ImportFolder));

    /// <summary>« Un coffre Obsidian, un dossier de notes Typora ou VS Code… ».</summary>
    public static string ImportHelp => Text(nameof(ImportHelp));

    /// <summary>« Choisissez le dossier de notes à importer ».</summary>
    public static string ImportPick => Text(nameof(ImportPick));

    /// <summary>« Révision des fiches importées ».</summary>
    public static string ImportReview => Text(nameof(ImportReview));

    /// <summary>« Importer un dossier de notes ».</summary>
    public static string ImportTitle => Text(nameof(ImportTitle));

    /// <summary>« Les mettre toutes en révision ».</summary>
    public static string ImportTrack => Text(nameof(ImportTrack));

    /// <summary>« Aujourd'hui vaut J0 pour chacune… ».</summary>
    public static string ImportTrackHelp => Text(nameof(ImportTrackHelp));

    /// <summary>« Dossier « … » importé. ».</summary>
    public static string ImportedOf(object folder) => Format("Imported", folder);

    /// <summary>« Plus d'actions ».</summary>
    public static string MoreActions => Text(nameof(MoreActions));

    /// <summary>« [Schéma réseau] ».</summary>
    public static string Network => Text(nameof(Network));

    /// <summary>« [Schéma réseau : …] ».</summary>
    public static string NetworkNamedOf(object title) => Format("NetworkNamed", title);

    /// <summary>« Le PDF n'a pas pu être préparé… ».</summary>
    public static string PdfFailed => Text(nameof(PdfFailed));

    /// <summary>« Où enregistrer la fiche partagée ? ».</summary>
    public static string PickFolder => Text(nameof(PickFolder));

    /// <summary>« Préparation de la fiche… ».</summary>
    public static string Preparing => Text(nameof(Preparing));

    /// <summary>« Pour les mettre en révision une à une… ».</summary>
    public static string ReportFollow => Text(nameof(ReportFollow));

    /// <summary>« … éléments non repris ».</summary>
    public static string ReportIgnoredManyOf(object count) => Format("ReportIgnoredMany", count);

    /// <summary>« … élément non repris ».</summary>
    public static string ReportIgnoredOneOf(object count) => Format("ReportIgnoredOne", count);

    /// <summary>« … dans « … » ».</summary>
    public static string ReportInOf(object sheets, object folder) => Format("ReportIn", sheets, folder);

    /// <summary>« … liens [[…]] convertis. ».</summary>
    public static string ReportLinksManyOf(object count) => Format("ReportLinksMany", count);

    /// <summary>« … lien [[…]] converti. ».</summary>
    public static string ReportLinksOneOf(object count) => Format("ReportLinksOne", count);

    /// <summary>« … images ou pièces jointes copiées. ».</summary>
    public static string ReportResourcesManyOf(object count) => Format("ReportResourcesMany", count);

    /// <summary>« … image ou pièce jointe copiée. ».</summary>
    public static string ReportResourcesOneOf(object count) => Format("ReportResourcesOne", count);

    /// <summary>« … fiches importées ».</summary>
    public static string ReportSheetsManyOf(object count) => Format("ReportSheetsMany", count);

    /// <summary>« … fiche importée ».</summary>
    public static string ReportSheetsOneOf(object count) => Format("ReportSheetsOne", count);

    /// <summary>« , dont … en révision (J1 demain) ».</summary>
    public static string ReportTrackedOf(object count) => Format("ReportTracked", count);

    /// <summary>« Enregistrer comme modèle… ».</summary>
    public static string SaveAsTemplate => Text(nameof(SaveAsTemplate));

    /// <summary>« Partager… ».</summary>
    public static string Share => Text(nameof(Share));

    /// <summary>« Partager » : le bouton de l'en-tête d'une fiche.</summary>
    public static string ShareShort => Text(nameof(ShareShort));

    /// <summary>« Partager la fiche ».</summary>
    public static string ShareSheet => Text(nameof(ShareSheet));

    /// <summary>« Partager « … » ».</summary>
    public static string ShareTitleOf(object title) => Format("ShareTitle", title);

    /// <summary>« Enregistrer ».</summary>
    public static string TemplateConfirm => Text(nameof(TemplateConfirm));

    /// <summary>« Modèle « … » enregistré dans le dossier « modeles » du coffre… ».</summary>
    public static string TemplateDoneOf(object name) => Format("TemplateDone", name);

    /// <summary>« Nom du modèle ».</summary>
    public static string TemplateLabel => Text(nameof(TemplateLabel));

    /// <summary>« Enregistrer comme modèle ».</summary>
    public static string TemplateTitle => Text(nameof(TemplateTitle));

    private static string Format(string name, params object[] values) => string.Format(CultureInfo.CurrentCulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Texte « {name} » introuvable.");
}
