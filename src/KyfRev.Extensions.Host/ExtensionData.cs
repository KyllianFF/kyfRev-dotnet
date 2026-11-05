using System.Globalization;
using System.Text.Json;
using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Extensions.Host;

/// <summary>Les données des extensions dans le coffre ouvert (<c>.suivi/extensions/&lt;id&gt;.json</c>). Elles restent quand une extension est retirée.</summary>
/// <param name="host">Le coffre ouvert.</param>
public sealed class ExtensionDataUseCases(VaultHost host)
{
    /// <summary>Les données d'une extension, en JSON ; rien si elle n'en a pas.</summary>
    /// <param name="id">Son identifiant.</param>
    [ZeroXCommand("donnees_extension")]
    public Outcome<string?> Read(string id) => host.Use(vault => vault.ExtensionData(id));

    /// <summary>Remplace les données d'une extension : du JSON valide, 1 Mo au plus.</summary>
    /// <param name="id">Son identifiant.</param>
    /// <param name="json">Ses données.</param>
    [ZeroXCommand("enregistrer_donnees_extension")]
    public Outcome<Done> Save(string id, string json) => host.Use(vault => vault.SaveExtensionData(id, json));

    /// <summary>Le journal de ce qu'une extension a écrit dans le coffre, du plus récent au plus ancien.</summary>
    /// <param name="id">Son identifiant.</param>
    public Outcome<IReadOnlyList<ExtensionJournalEntry>> Journal(string id) => host.Use(vault =>
    {
        var lines = (vault.ExtensionJournal(id) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var entries = new List<ExtensionJournalEntry>(lines.Length);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            entries.Add(Entry(lines[index]) ?? ExtensionJournalEntry.UnreadableLine(lines[index]));
        }
        return (IReadOnlyList<ExtensionJournalEntry>)entries;
    });

    /// <summary>Une ligne du journal ; rien si elle n'en est pas une (abîmée à la main, par exemple).</summary>
    private static ExtensionJournalEntry? Entry(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<ExtensionJournalEntry>(line) is { Action: not null, Sheet: not null, Title: not null } entry ? entry : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Note une écriture au journal d'une extension.</summary>
    /// <param name="id">Son identifiant.</param>
    /// <param name="entry">L'écriture.</param>
    public Outcome<Done> Note(string id, ExtensionJournalEntry entry) => host.Use(vault => vault.AppendExtensionJournal(id, JsonSerializer.Serialize(entry)));
}

/// <summary>
/// Le coffre ouvert, tel que les extensions le voient : ses fiches présentes, et leurs propres données. Elles peuvent créer des
/// fiches, changer leurs étiquettes et remplacer leur texte, jamais en supprimer : le texte remplacé va dans les versions de la
/// fiche, et chaque écriture au journal de l'extension.
/// </summary>
/// <param name="sheets">Les fiches.</param>
/// <param name="editing">L'enregistrement des fiches, avec leurs versions.</param>
/// <param name="data">Les données et le journal des extensions.</param>
/// <param name="time">L'heure du journal.</param>
public sealed class VaultExtensionData(SheetUseCases sheets, EditingUseCases editing, ExtensionDataUseCases data, TimeProvider time) : IExtensionData
{
    // Chaque écriture a sa propre session d'enregistrement : le texte qu'elle remplace va toujours dans les versions. Celles de
    // l'éditeur restent sous 2^48 ; celles-ci partent de 2^63.
    private long _writes;

    /// <inheritdoc/>
    public Task<Outcome<IReadOnlyList<ExtensionSheet>>> SheetsAsync() => Task.Run(() =>
    {
        var list = sheets.List();
        return list.Succeeded
            ? Outcome.Of<IReadOnlyList<ExtensionSheet>>([.. list.Value.Where(sheet => sheet.FilePresent).Select(sheet => new ExtensionSheet(sheet.Id, sheet.Title, sheet.Folder, sheet.Tags, sheet.Archived))])
            : Outcome.Refused<IReadOnlyList<ExtensionSheet>>(list.Error!);
    });

    /// <inheritdoc/>
    public Task<Outcome<ExtensionSheetText>> SheetAsync(string id) => Task.Run(() =>
    {
        var sheet = sheets.Open(id);
        return sheet.Succeeded
            ? Outcome.Of(new ExtensionSheetText(sheet.Value.Id, sheet.Value.Title, sheet.Value.Body))
            : Outcome.Refused<ExtensionSheetText>(sheet.Error!);
    });

    /// <inheritdoc/>
    public Task<Outcome<string?>> StoredAsync(string extension) => Task.Run(() => data.Read(extension));

    /// <inheritdoc/>
    public Task<Outcome<Done>> StoreAsync(string extension, string json) => Task.Run(() => data.Save(extension, json));

    /// <inheritdoc/>
    public Task<Outcome<string>> CreateSheetAsync(string extension, ExtensionNewSheet sheet) => Task.Run(() =>
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var created = sheets.Create(sheet.Folder, sheet.Title, null);
        if (!created.Succeeded)
        {
            return created;
        }
        var id = created.Value;
        if (!string.IsNullOrEmpty(sheet.Text) && Replace(id, sheet.Text) is { } refused)
        {
            return Outcome.Refused<string>(refused);
        }
        if (sheet.Tags.Count > 0 && sheets.SetTags(id, sheet.Tags) is { Succeeded: false } untagged)
        {
            return Outcome.Refused<string>(untagged.Error!);
        }
        return Noted(extension, ExtensionJournalEntry.Creation, id, sheet.Title, sheet.Folder is { Length: > 0 } folder ? folder : null, id);
    });

    /// <inheritdoc/>
    public Task<Outcome<IReadOnlyList<string>>> SetTagsAsync(string extension, string id, IReadOnlyList<string> tags) => Task.Run(() =>
    {
        var sheet = sheets.Open(id);
        if (!sheet.Succeeded)
        {
            return Outcome.Refused<IReadOnlyList<string>>(sheet.Error!);
        }
        var saved = sheets.SetTags(id, tags);
        return saved.Succeeded
            ? Noted(extension, ExtensionJournalEntry.Tagging, id, sheet.Value.Title, string.Join(", ", saved.Value), saved.Value)
            : saved;
    });

    /// <inheritdoc/>
    public Task<Outcome<Done>> WriteSheetAsync(string extension, string id, string text) => Task.Run(() =>
    {
        var sheet = sheets.Open(id);
        if (!sheet.Succeeded)
        {
            return Outcome.Refused<Done>(sheet.Error!);
        }
        if (Replace(id, text) is { } refused)
        {
            return Outcome.Refused<Done>(refused);
        }
        var detail = string.Create(CultureInfo.InvariantCulture, $"{sheet.Value.Body.Length} → {text.Length} caractères");
        return Noted(extension, ExtensionJournalEntry.Writing, id, sheet.Value.Title, detail, default(Done));
    });

    /// <inheritdoc/>
    public Task<Outcome<IReadOnlyList<ExtensionJournalEntry>>> JournalAsync(string extension) => Task.Run(() => data.Journal(extension));

    /// <summary>Remplace le texte d'une fiche telle qu'elle est sur le disque, le précédent gardé en version ; la raison d'un refus.</summary>
    private string? Replace(string id, string text)
    {
        var sheet = sheets.Open(id);
        if (!sheet.Succeeded)
        {
            return sheet.Error;
        }
        var session = (1UL << 63) | (ulong)Interlocked.Increment(ref _writes);
        var saved = editing.Save(id, text, sheet.Value.Fingerprint, session);
        return !saved.Succeeded ? saved.Error
            : saved.Value is SaveConflict ? "la fiche a changé pendant l'écriture : réessayez"
            : null;
    }

    /// <summary>Note l'écriture au journal, puis rend <paramref name="value"/> : une écriture faite et non notée serait un refus.</summary>
    private Outcome<T> Noted<T>(string extension, string action, string id, string title, string? detail, T value)
    {
        var noted = data.Note(extension, new ExtensionJournalEntry(time.GetUtcNow(), action, id, title, detail));
        return noted.Succeeded ? Outcome.Of(value) : Outcome.Refused<T>($"écrit, mais pas noté au journal : {noted.Error}");
    }
}
