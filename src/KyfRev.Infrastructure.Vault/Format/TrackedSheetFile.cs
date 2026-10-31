using System.Collections.Immutable;
using System.Text.Json.Nodes;
using KyfRev.Domain.Reviews;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary><c>.suivi/fiches/&lt;id&gt;.json</c> : le suivi d'une fiche et ses dernières informations connues.</summary>
/// <param name="Format">Version du format du fichier.</param>
/// <param name="Id">Identifiant de la fiche (<c>f_</c> + ULID), égal au nom du fichier.</param>
/// <param name="Path">Dernier chemin connu du fichier Markdown.</param>
/// <param name="Title">Dernier titre connu : il reste affichable même si le fichier disparaît.</param>
/// <param name="Tags">Dernières étiquettes connues (copie de l'en-tête de la fiche).</param>
/// <param name="Fingerprint">Empreinte du contenu lors du dernier passage de l'application.</param>
/// <param name="ModifiedAt">Date de dernière modification connue.</param>
/// <param name="Tracking">Suivi de la méthode des J.</param>
/// <param name="Unknown">Propriétés inconnues conservées.</param>
public sealed record TrackedSheetFile(
    long Format,
    string Id,
    VaultPath Path,
    string Title,
    ImmutableArray<string> Tags,
    string Fingerprint,
    DateTimeOffset ModifiedAt,
    Tracking Tracking,
    UnknownProperties Unknown)
{
    private static readonly string[] Known = ["format", "id", "chemin", "titre", "etiquettes", "empreinte", "modifieeLe", .. TrackingJson.Keys];

    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible ou le suivi incohérent.</exception>
    public static TrackedSheetFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        return new TrackedSheetFile(
            Fields.Integer(obj, "format", 0, uint.MaxValue),
            Fields.String(obj, "id"),
            Fields.Path(obj, "chemin"),
            Fields.String(obj, "titre"),
            Fields.Strings(Fields.OptionalArray(obj, "etiquettes"), "etiquettes"),
            Fields.String(obj, "empreinte"),
            Fields.Instant(obj, "modifieeLe"),
            TrackingJson.Read(obj),
            UnknownProperties.From(Fields.Unknown(obj, Known)));
    }

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["id"] = Id,
            ["chemin"] = Path.Value,
            ["titre"] = Title,
        };
        if (!Tags.IsEmpty)
        {
            obj["etiquettes"] = Fields.StringArray(Tags);
        }
        obj["empreinte"] = Fingerprint;
        obj["modifieeLe"] = InstantText.Format(ModifiedAt);
        TrackingJson.Write(Tracking, obj);
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}
