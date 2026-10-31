using System.Collections.Immutable;
using System.Text.Json.Nodes;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Json;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary>État d'ancrage d'une annotation.</summary>
public enum AnchorState
{
    /// <summary>Position retrouvée sans ambiguïté.</summary>
    Anchored,

    /// <summary>Position incertaine : l'utilisateur doit confirmer.</summary>
    NeedsReview,

    /// <summary>Passage introuvable : signalé, jamais rattaché ailleurs en silence.</summary>
    NotFound,
}

/// <summary><c>.suivi/annotations/&lt;idFiche&gt;.json</c> : les passages annotés et définitions d'une fiche.</summary>
public sealed record AnnotationsFile(long Format, string SheetId, ImmutableArray<StoredAnnotation> Annotations, UnknownProperties Unknown)
{
    private static readonly string[] Known = ["format", "ficheId", "annotations"];

    /// <summary>Lit le fichier ; chaque annotation doit appartenir à la fiche du fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible.</exception>
    public static AnnotationsFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        var sheetId = Fields.String(obj, "ficheId");
        var annotations = Fields.Array(obj, "annotations")
            .Select(item => StoredAnnotation.Read(item as JsonObject ?? throw new VaultFormatException("une annotation doit être un objet")))
            .ToImmutableArray();
        if (annotations.FirstOrDefault(a => a.SheetId != sheetId) is { } stray)
        {
            throw new VaultFormatException($"l'annotation {stray.Id} appartient à la fiche {stray.SheetId}, pas à {sheetId}");
        }
        return new AnnotationsFile(Fields.Integer(obj, "format", 0, uint.MaxValue), sheetId, annotations, UnknownProperties.From(Fields.Unknown(obj, Known)));
    }

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["ficheId"] = SheetId,
            ["annotations"] = new JsonArray([.. Annotations.Select(a => (JsonNode?)a.ToJson())]),
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}

/// <summary>Passage annoté ou définition, avec son ancre et son propre suivi.</summary>
/// <param name="Id">Identifiant (<c>a_</c> + ULID).</param>
/// <param name="SheetId">Fiche d'origine : le rattachement se fait par identifiant, jamais par chemin.</param>
/// <param name="CreatedAt">Création (son J0).</param>
/// <param name="ModifiedAt">Dernière modification.</param>
/// <param name="Comment">Commentaire, absent pour un simple surlignage.</param>
/// <param name="Definition">Terme et explication, pour une définition.</param>
/// <param name="Anchor">Repères qui permettent de retrouver le passage.</param>
/// <param name="AnchorState">État d'ancrage.</param>
/// <param name="TrashedAt">Mise à la corbeille, récupérable et jamais purgée.</param>
/// <param name="Tracking">Suivi de la méthode des J.</param>
/// <param name="Unknown">Propriétés inconnues conservées.</param>
public sealed record StoredAnnotation(
    string Id,
    string SheetId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string? Comment,
    StoredDefinition? Definition,
    StoredAnchor Anchor,
    AnchorState AnchorState,
    DateTimeOffset? TrashedAt,
    Tracking Tracking,
    UnknownProperties Unknown)
{
    private static readonly string[] Known =
        ["id", "ficheId", "creeeLe", "modifieeLe", "commentaire", "definition", "ancre", "etatAncrage", "corbeilleLe", .. TrackingJson.Keys];

    internal static StoredAnnotation Read(JsonObject obj)
    {
        var definition = Fields.OptionalObject(obj, "definition");
        return new StoredAnnotation(
            Fields.String(obj, "id"),
            Fields.String(obj, "ficheId"),
            Fields.Instant(obj, "creeeLe"),
            Fields.Instant(obj, "modifieeLe"),
            Fields.OptionalString(obj, "commentaire"),
            definition is null ? null : new StoredDefinition(Fields.String(definition, "terme"), Fields.String(definition, "explication")),
            StoredAnchor.Read(Fields.Object(obj, "ancre")),
            ReadState(Fields.String(obj, "etatAncrage")),
            Fields.OptionalInstant(obj, "corbeilleLe"),
            TrackingJson.Read(obj),
            UnknownProperties.From(Fields.Unknown(obj, Known)));
    }

    internal JsonObject ToJson()
    {
        var obj = new JsonObject
        {
            ["id"] = Id,
            ["ficheId"] = SheetId,
            ["creeeLe"] = InstantText.Format(CreatedAt),
            ["modifieeLe"] = InstantText.Format(ModifiedAt),
        };
        if (Comment is not null)
        {
            obj["commentaire"] = Comment;
        }
        if (Definition is not null)
        {
            obj["definition"] = new JsonObject { ["terme"] = Definition.Term, ["explication"] = Definition.Explanation };
        }
        obj["ancre"] = Anchor.ToJson();
        obj["etatAncrage"] = AnchorState switch
        {
            AnchorState.Anchored => "ancree",
            AnchorState.NeedsReview => "a_verifier",
            _ => "introuvable",
        };
        if (TrashedAt is { } trashedAt)
        {
            obj["corbeilleLe"] = InstantText.Format(trashedAt);
        }
        TrackingJson.Write(Tracking, obj);
        Fields.AddUnknown(obj, Unknown.Properties);
        return obj;
    }

    private static AnchorState ReadState(string text) => text switch
    {
        "ancree" => AnchorState.Anchored,
        "a_verifier" => AnchorState.NeedsReview,
        "introuvable" => AnchorState.NotFound,
        _ => throw new VaultFormatException($"état d'ancrage « {text} » inconnu"),
    };
}

/// <summary>Terme et explication d'une définition.</summary>
public sealed record StoredDefinition(string Term, string Explanation);

/// <summary>
/// Repères d'un passage (modèle W3C Web Annotation) : citation exacte et contexte, position en
/// unités UTF-16 dans le texte rendu, chemin de titres, ligne approximative du Markdown.
/// </summary>
public sealed record StoredAnchor(
    string Exact,
    string Before,
    string After,
    long Start,
    long End,
    ImmutableArray<string> Section,
    long? SourceLine,
    string SheetFingerprint,
    UnknownProperties Unknown)
{
    private static readonly string[] Known = ["exact", "avant", "apres", "debut", "fin", "section", "ligneSource", "empreinteFiche"];

    internal static StoredAnchor Read(JsonObject obj) => new(
        Fields.String(obj, "exact"),
        Fields.OptionalString(obj, "avant") ?? string.Empty,
        Fields.OptionalString(obj, "apres") ?? string.Empty,
        Fields.Integer(obj, "debut", 0, long.MaxValue),
        Fields.Integer(obj, "fin", 0, long.MaxValue),
        Fields.Strings(Fields.OptionalArray(obj, "section"), "section"),
        obj.TryGetPropertyValue("ligneSource", out var line) && line is not null ? Fields.IntegerValue(line, "ligneSource", 0, uint.MaxValue) : null,
        Fields.String(obj, "empreinteFiche"),
        UnknownProperties.From(Fields.Unknown(obj, Known)));

    internal JsonObject ToJson()
    {
        var obj = new JsonObject
        {
            ["exact"] = Exact,
            ["avant"] = Before,
            ["apres"] = After,
            ["debut"] = Start,
            ["fin"] = End,
            ["section"] = Fields.StringArray(Section),
        };
        if (SourceLine is { } line)
        {
            obj["ligneSource"] = line;
        }
        obj["empreinteFiche"] = SheetFingerprint;
        Fields.AddUnknown(obj, Unknown.Properties);
        return obj;
    }
}
