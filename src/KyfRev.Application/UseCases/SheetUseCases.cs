using KyfRev.Application.Model;
using Microsoft.Extensions.Logging;

namespace KyfRev.Application.UseCases;

/// <summary>
/// Fiches : création (vide ou d'après un modèle), lecture, titre, étiquettes, rangement, adoption
/// d'un fichier, import d'un dossier de notes et partage. Ouvrir une fiche ne valide rien.
/// </summary>
public sealed partial class SheetUseCases(VaultHost host, TimeProvider time, ILogger<SheetUseCases> logger)
{
    /// <summary>Crée une fiche, vide ou d'après le modèle <paramref name="template"/> ; rend son identifiant.</summary>
    /// <param name="folder">Dossier relatif à <c>fiches/</c> ; vide ou rien : la racine.</param>
    /// <param name="title">Titre.</param>
    /// <param name="template">Nom du modèle ; vide ou rien : une fiche vide.</param>
    [ZeroXCommand("creer_fiche")]
    public Outcome<string> Create(string? folder, string title, string? template)
    {
        var present = Present.Of(time);
        var target = string.IsNullOrWhiteSpace(folder) ? null : folder;
        return host.Use(vault =>
        {
            var body = string.IsNullOrWhiteSpace(template) ? string.Empty : vault.TemplateBody(template, title, target ?? string.Empty, present.Now.Day);
            return vault.CreateSheet(target, title, body, present.Now);
        });
    }

    /// <summary>Contenu d'une image d'une fiche, pour l'afficher ; seul le dossier des fiches est lisible.</summary>
    public Outcome<byte[]> ReadResource(string path) => host.Use(vault => vault.ReadResource(path));

    /// <summary>Toutes les fiches, par dossier puis par titre.</summary>
    [ZeroXCommand("liste_des_fiches")]
    public Outcome<IReadOnlyList<VaultSheet>> List() => host.Use(vault => vault.SheetList());

    /// <summary>
    /// Les fiches qui contiennent tous les mots de <paramref name="query"/> (titre, étiquettes et texte), chacune avec un
    /// extrait de son texte. Annulée, la recherche rend ce qu'elle a déjà trouvé : à qui l'a annulée de ne pas s'en servir.
    /// </summary>
    [ZeroXCommand("chercher_dans_les_fiches")]
    public Outcome<IReadOnlyList<SheetHit>> Search(string query, CancellationToken cancellationToken) => host.Use(vault => vault.SearchSheets(query, cancellationToken));

    /// <summary>Ouvre une fiche pour la lire ou la réviser.</summary>
    [ZeroXCommand("ouvrir_fiche")]
    public Outcome<OpenedSheet> Open(string id)
    {
        var day = Present.Of(time).Now.Day;
        return host.Use(vault => vault.OpenSheet(id, day));
    }

    /// <summary>Identifiant de la fiche située à <paramref name="path"/> (lien d'une fiche vers une autre).</summary>
    [ZeroXCommand("id_de_fiche")]
    public Outcome<string?> IdAt(string path) => host.Use(vault => vault.SheetIdAt(path));

    /// <summary>Nouveau titre : l'en-tête est mis à jour et le fichier renommé, liens compris.</summary>
    [ZeroXCommand("changer_titre_fiche")]
    public Outcome<Done> ChangeTitle(string id, string title)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.ChangeTitle(id, title, now));
    }

    /// <summary>Remplace les étiquettes ; rend celles réellement enregistrées (sans « # », sans doublon).</summary>
    [ZeroXCommand("modifier_etiquettes_fiche")]
    public Outcome<IReadOnlyList<string>> SetTags(string id, IReadOnlyList<string> tags)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.SetTags(id, tags, now));
    }

    /// <summary>Range la fiche dans <paramref name="folder"/> (relatif à <c>fiches/</c>, vide : la racine).</summary>
    [ZeroXCommand("deplacer_fiche")]
    public Outcome<Done> Move(string id, string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var now = Present.Of(time).Now;
        var target = folder.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        return host.Use(vault => vault.MoveSheet(id, target, now));
    }

    /// <summary>« Suivre cette fiche » : un fichier <c>.md</c> sans identifiant entre en révision (J0) ; rend son identifiant.</summary>
    [ZeroXCommand("suivre_fichier")]
    public Outcome<string> Track(string path)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.TrackFile(path, now));
    }

    /// <summary>
    /// Importe un dossier de notes Markdown (Obsidian…) dans <c>fiches/&lt;son nom&gt;</c> ;
    /// <paramref name="track"/> : les fiches commencent leur cycle aujourd'hui.
    /// </summary>
    [ZeroXCommand("importer_dossier")]
    public Outcome<ImportReport> Import(string source, bool track)
    {
        ArgumentNullException.ThrowIfNull(source);
        var now = Present.Of(time).Now;
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) is { Length: > 0 } last ? last : "Import";
        var outcome = host.Use(vault => vault.ImportFolder(source, name, track, now));
        if (outcome.Succeeded)
        {
            var report = outcome.Value;
            LogImported(logger, report.Sheets, report.Tracked, report.Resources, report.Ignored.Count);
        }
        return outcome;
    }

    /// <summary>Partage : le fichier créé dans <paramref name="folder"/>, choisi par l'utilisateur hors du coffre ; rend son chemin.</summary>
    [ZeroXCommand("partager_fiche")]
    public Outcome<string> Share(string id, ShareFormat format, string folder, string? content)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return Path.IsPathFullyQualified(folder)
            ? host.Use(vault => vault.ShareSheet(id, format, folder, content))
            : Outcome.Refused<string>("Choisissez un dossier où enregistrer la fiche.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dossier importé : {Sheets} fiche(s), {Tracked} suivie(s), {Resources} ressource(s), {Ignored} élément(s) ignoré(s).")]
    private static partial void LogImported(ILogger logger, int sheets, int tracked, int resources, int ignored);
}
