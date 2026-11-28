using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Modules.Schemas;

/// <summary>
/// Cas d'usage du module Schémas réseau. Un schéma ne s'efface pas : retirer son bloc d'une fiche ne le touche pas, et
/// « supprimer » un schéma le range dans une corbeille d'où il se restaure.
/// </summary>
public sealed class SchemaUseCases(VaultHost host)
{
    /// <summary>Les schémas du coffre, par titre, avec les fiches qui les insèrent.</summary>
    [ZeroXCommand("schemas")]
    public Outcome<IReadOnlyList<SchemaSummary>> Schemas() => host.Use(vault => vault.Schemas());

    /// <summary>Les schémas archivés et ceux de la corbeille.</summary>
    public Outcome<IReadOnlyList<SchemaSummary>> Aside() => host.Use(vault => vault.SchemasAside());

    /// <summary>Archive un schéma, le met à la corbeille, ou le remet dans la liste.</summary>
    public Outcome<bool> Move(string id, SchemaPlace place) => host.Use(vault =>
    {
        vault.MoveSchema(id, place);
        return true;
    });

    /// <summary>Lit un schéma.</summary>
    [ZeroXCommand("lire_schema")]
    public Outcome<SchemaDocument> Read(string id) => host.Use(vault => vault.ReadSchema(id));

    /// <summary>Crée un schéma (nouveau, importé) : vérifié, il reçoit un identifiant neuf.</summary>
    [ZeroXCommand("creer_schema")]
    public Outcome<SchemaDocument> Create(string text) => host.Use(vault => vault.CreateSchema(text));

    /// <summary>Enregistre un schéma, s'il est toujours celui qui a été lu ; sinon, rend ce qu'il est devenu.</summary>
    [ZeroXCommand("enregistrer_schema")]
    public Outcome<SaveResult> Save(string id, string text, string fingerprint) => host.Use(vault => vault.SaveSchema(id, text, fingerprint));

    /// <summary>Copie indépendante d'un schéma.</summary>
    [ZeroXCommand("dupliquer_schema")]
    public Outcome<SchemaDocument> Duplicate(string id) => host.Use(vault => vault.DuplicateSchema(id));

    /// <summary>Écrit un export hors du coffre, sans rien écraser ; rend le fichier créé.</summary>
    /// <param name="folder">Le dossier choisi.</param>
    /// <param name="name">Le nom voulu, extension comprise.</param>
    /// <param name="content">Le contenu.</param>
    [ZeroXCommand("exporter_schema")]
    public Outcome<string> Export(string folder, string name, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dot = name.LastIndexOf('.');
        var (stem, extension) = dot < 0 ? (name, string.Empty) : (name[..dot], name[(dot + 1)..]);
        return host.Use(vault => vault.ExportSchema(folder, stem, extension, content));
    }

    /// <summary>Lit un fichier choisi pour l'import : rien n'est écrit dans le coffre.</summary>
    [ZeroXCommand("lire_import_schema")]
    public Outcome<string> ReadImport(string path) => host.Use(vault => vault.ReadSchemaToImport(path));

    /// <summary>Les modèles de schéma de l'utilisateur (« Mes modèles »), par nom.</summary>
    public Outcome<IReadOnlyList<SchemaDocument>> Templates() => host.Use(vault => vault.SchemaTemplates());

    /// <summary>Enregistre un modèle de schéma : vérifié, il reçoit un identifiant neuf.</summary>
    public Outcome<SchemaDocument> CreateTemplate(string text) => host.Use(vault => vault.CreateSchemaTemplate(text));

    /// <summary>Retire un modèle de schéma : son fichier est rangé à part, rien n'est effacé.</summary>
    public Outcome<bool> SetTemplateAside(string id) => host.Use(vault =>
    {
        vault.SetSchemaTemplateAside(id);
        return true;
    });
}
