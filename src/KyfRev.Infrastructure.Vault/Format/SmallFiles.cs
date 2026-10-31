using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;

namespace KyfRev.Infrastructure.Vault.Format;

/// <summary><c>.suivi/rappels.json</c> : un tirage de rappels par jour préparé, jamais refait.</summary>
public sealed record DrawJournalFile(long Format, ImmutableArray<RecordedDraw> Draws, UnknownProperties Unknown)
{
    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible.</exception>
    public static DrawJournalFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        var draws = (Fields.OptionalArray(obj, "tirages") ?? [])
            .Select(item => item as JsonObject ?? throw new VaultFormatException("un tirage doit être un objet"))
            .Select(draw => new RecordedDraw(
                Fields.Day(draw, "jour"),
                Fields.Strings(Fields.Array(draw, "selection"), "selection"),
                Fields.Integer(draw, "prioritairesNonServis", 0, uint.MaxValue),
                UnknownProperties.From(Fields.Unknown(draw, "jour", "selection", "prioritairesNonServis"))))
            .ToImmutableArray();
        return new DrawJournalFile(Fields.Integer(obj, "format", 0, uint.MaxValue), draws, UnknownProperties.From(Fields.Unknown(obj, "format", "tirages")));
    }

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["tirages"] = new JsonArray([.. Draws.Select(draw =>
            {
                var item = new JsonObject
                {
                    ["jour"] = Fields.DayText(draw.Day),
                    ["selection"] = Fields.StringArray(draw.Selection),
                    ["prioritairesNonServis"] = draw.UnservedPriority,
                };
                Fields.AddUnknown(item, draw.Unknown.Properties);
                return (JsonNode?)item;
            })]),
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}

/// <summary>Tirage enregistré d'un jour.</summary>
public sealed record RecordedDraw(DateOnly Day, ImmutableArray<string> Selection, long UnservedPriority, UnknownProperties Unknown);

/// <summary><c>.suivi/citation-en-ligne.json</c> : la citation trouvée en ligne pour un jour.</summary>
public sealed record OnlineQuoteFile(long Format, DateOnly? Day, string? Path, string? Page, UnknownProperties Unknown)
{
    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible.</exception>
    public static OnlineQuoteFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        return new OnlineQuoteFile(
            Fields.Integer(obj, "format", 0, uint.MaxValue),
            Fields.OptionalDay(obj, "jour"),
            Fields.OptionalString(obj, "chemin"),
            Fields.OptionalString(obj, "page"),
            UnknownProperties.From(Fields.Unknown(obj, "format", "jour", "chemin", "page")));
    }

    /// <summary>Contenu du fichier ; une valeur absente s'écrit <c>null</c>, comme dans la version 0.x.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["jour"] = Day is { } day ? Fields.DayText(day) : null,
            ["chemin"] = Path,
            ["page"] = Page,
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}

/// <summary><c>.suivi/verrou-info.json</c> : qui tient le coffre ouvert (pour le message d'erreur).</summary>
public sealed record LockInfoFile(string Machine, long Process, DateTimeOffset Since, UnknownProperties Unknown)
{
    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible.</exception>
    public static LockInfoFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        return new LockInfoFile(
            Fields.String(obj, "machine"),
            Fields.Integer(obj, "processus", 0, uint.MaxValue),
            Fields.Instant(obj, "depuis"),
            UnknownProperties.From(Fields.Unknown(obj, "machine", "processus", "depuis")));
    }

    /// <summary>Contenu du fichier : JSON indenté <b>sans</b> saut de ligne final, comme dans la version 0.x.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["machine"] = Machine,
            ["processus"] = Process,
            ["depuis"] = InstantText.Format(Since),
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SerdeJson.Pretty(obj));
    }
}

/// <summary>État d'un journal d'opération.</summary>
public enum OperationState
{
    /// <summary>Opération commencée : à rejouer à l'ouverture.</summary>
    InProgress,

    /// <summary>Opération terminée ; le journal n'est jamais effacé.</summary>
    Done,
}

/// <summary>Déplacement prévu par une opération.</summary>
public sealed record PlannedMove(VaultPath From, VaultPath To);

/// <summary>Réécriture prévue par une opération, appliquée seulement si l'empreinte n'a pas changé.</summary>
public sealed record PlannedWrite(VaultPath Path, string FingerprintBefore, string Content);

/// <summary><c>.suivi/operation.json</c> : opération sur plusieurs fichiers (renommer, déplacer, mettre à la corbeille).</summary>
public sealed record OperationJournalFile(
    long Format,
    OperationState State,
    string Description,
    ImmutableArray<PlannedMove> Moves,
    ImmutableArray<PlannedWrite> Writes,
    UnknownProperties Unknown)
{
    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible : il est signalé et laissé en place.</exception>
    public static OperationJournalFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        var state = Fields.String(obj, "etat") switch
        {
            "en_cours" => OperationState.InProgress,
            "terminee" => OperationState.Done,
            var other => throw new VaultFormatException($"état d'opération « {other} » inconnu"),
        };
        var moves = Fields.Array(obj, "deplacements")
            .Select(item => item as JsonObject ?? throw new VaultFormatException("un déplacement doit être un objet"))
            .Select(move => new PlannedMove(Fields.Path(move, "de"), Fields.Path(move, "vers")))
            .ToImmutableArray();
        var writes = Fields.Array(obj, "ecritures")
            .Select(item => item as JsonObject ?? throw new VaultFormatException("une écriture doit être un objet"))
            .Select(write => new PlannedWrite(Fields.Path(write, "chemin"), Fields.String(write, "empreinteAvant"), Fields.String(write, "contenu")))
            .ToImmutableArray();
        return new OperationJournalFile(
            Fields.Integer(obj, "format", 0, uint.MaxValue),
            state,
            Fields.String(obj, "description"),
            moves,
            writes,
            UnknownProperties.From(Fields.Unknown(obj, "format", "etat", "description", "deplacements", "ecritures")));
    }

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["etat"] = State == OperationState.InProgress ? "en_cours" : "terminee",
            ["description"] = Description,
            ["deplacements"] = new JsonArray([.. Moves.Select(move => (JsonNode?)new JsonObject { ["de"] = move.From.Value, ["vers"] = move.To.Value })]),
            ["ecritures"] = new JsonArray([.. Writes.Select(write => (JsonNode?)new JsonObject
            {
                ["chemin"] = write.Path.Value,
                ["empreinteAvant"] = write.FingerprintBefore,
                ["contenu"] = write.Content,
            })]),
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}

/// <summary><c>fiches/.supprimees/&lt;id&gt;/corbeille.json</c> : d'où vient une fiche mise à la corbeille.</summary>
public sealed record TrashInfoFile(long Format, string Id, string Title, VaultPath OriginalPath, DateTimeOffset TrashedAt, UnknownProperties Unknown)
{
    /// <summary>Lit le fichier.</summary>
    /// <exception cref="VaultFormatException">Le fichier est illisible.</exception>
    public static TrashInfoFile Read(ReadOnlySpan<byte> json)
    {
        var obj = Fields.ParseObject(json);
        return new TrashInfoFile(
            Fields.Integer(obj, "format", 0, uint.MaxValue),
            Fields.String(obj, "id"),
            Fields.String(obj, "titre"),
            Fields.Path(obj, "chemin"),
            Fields.Instant(obj, "miseALaCorbeilleLe"),
            UnknownProperties.From(Fields.Unknown(obj, "format", "id", "titre", "chemin", "miseALaCorbeilleLe")));
    }

    /// <summary>Contenu du fichier, octet pour octet comme la version 0.x l'écrirait.</summary>
    public byte[] Write()
    {
        var obj = new JsonObject
        {
            ["format"] = Format,
            ["id"] = Id,
            ["titre"] = Title,
            ["chemin"] = OriginalPath.Value,
            ["miseALaCorbeilleLe"] = InstantText.Format(TrashedAt),
        };
        Fields.AddUnknown(obj, Unknown.Properties);
        return SerdeJson.PrettyFile(obj);
    }
}
