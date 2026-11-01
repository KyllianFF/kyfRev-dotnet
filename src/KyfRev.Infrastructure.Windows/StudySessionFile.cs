using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// La séance d'étude en cours, gardée dans <c>seance.json</c>, parmi les données propres à ce poste
/// (<see cref="VaultLocations.LocalData"/>), jamais dans le coffre. Une séance terminée est oubliée
/// en réécrivant le fichier vide. Un fichier absent, illisible ou mal formé ne reprend rien.
/// </summary>
public sealed class StudySessionFile : IStudySessionStore
{
    private const int Format = 1;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _file;

    /// <summary>La séance gardée dans le dossier <paramref name="folder"/>.</summary>
    public StudySessionFile(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        _file = Path.Combine(folder, "seance.json");
    }

    /// <inheritdoc/>
    public StudySession? Load()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(_file));
            return Read(document.RootElement);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Save(StudySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", Format);
            writer.WriteString("coffre", session.Vault);
            writer.WriteString("jour", session.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteStartArray("elements");
            foreach (var item in session.Items)
            {
                writer.WriteStartObject();
                writer.WriteString("genre", item.Kind == ItemKind.Sheet ? "fiche" : "passage");
                writer.WriteString("id", item.Id);
                writer.WriteString("fiche", item.SheetId);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteNumber("position", session.Position);
            writer.WriteNumber("faits", session.Done);
            writer.WriteNumber("reportes", session.Postponed);
            writer.WriteNumber("passes", session.Skipped);
            writer.WriteNumber("dureeSecondes", Math.Round(session.Elapsed.TotalSeconds, 3));
            writer.WriteBoolean("rappelActif", session.ActiveRecall);
            writer.WriteEndObject();
        }
        return Write(buffer.WrittenSpan.ToArray());
    }

    /// <inheritdoc/>
    public bool Forget() => Write("{}"u8.ToArray());

    private bool Write(byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllBytes(_file, bytes);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>La séance décrite par <paramref name="root"/> ; rien si un champ manque ou n'a pas le bon type.</summary>
    private static StudySession? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("format", out var format) || format.GetInt32() != Format
            || !root.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var items = ImmutableArray.CreateBuilder<SessionItem>();
        foreach (var element in elements.EnumerateArray())
        {
            var kind = Text(element, "genre") switch
            {
                "fiche" => ItemKind.Sheet,
                "passage" => ItemKind.Passage,
                _ => (ItemKind?)null,
            };
            if (kind is null)
            {
                return null;
            }
            items.Add(new SessionItem(kind.Value, Text(element, "id"), Text(element, "fiche")));
        }
        return new StudySession(
            Text(root, "coffre"),
            DateOnly.ParseExact(Text(root, "jour"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            items.ToImmutable(),
            root.GetProperty("position").GetInt32(),
            root.GetProperty("faits").GetInt32(),
            root.GetProperty("reportes").GetInt32(),
            root.GetProperty("passes").GetInt32(),
            TimeSpan.FromSeconds(root.GetProperty("dureeSecondes").GetDouble()),
            root.GetProperty("rappelActif").GetBoolean());
    }

    /// <summary>Le texte de la propriété <paramref name="name"/> ; une valeur nulle rend le fichier invalide.</summary>
    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new FormatException($"« {name} » est vide.");
}
