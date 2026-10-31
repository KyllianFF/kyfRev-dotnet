using System.Text;
using KyfRev.Domain.Calendar;
using KyfRev.Domain.Randomness;
using KyfRev.Infrastructure.Vault.Files;
using KyfRev.Infrastructure.Vault.Format;
using KyfRev.Infrastructure.Vault.Json;
using KyfRev.Infrastructure.Vault.Paths;
using KyfRev.Infrastructure.Vault.Quotes;
using KyfRev.Infrastructure.Vault.Sheets;

namespace KyfRev.Infrastructure.Vault;

/// <remarks>
/// Citations : un fichier <c>.md</c> par citation dans <c>citations/</c> (en-tête YAML et texte).
/// « Supprimer » range le fichier dans <c>citations/.supprimees/</c>, d'où il se restaure ; rien
/// n'est jamais effacé ni écrasé. Modifier une citation ne réécrit que les champs de kyfRev.
/// </remarks>
public sealed partial class VaultSession
{
    private static readonly VaultPath QuotesFolder = VaultPath.Parse(VaultLayout.Quotes);
    private static readonly VaultPath QuotesTrash = VaultPath.Parse($"{VaultLayout.Quotes}/.supprimees");
    private static readonly VaultPath OnlineQuotePath = VaultPath.Parse(VaultLayout.OnlineQuoteFile);

    /// <summary>Toutes les citations, celles de la corbeille à part, les plus récentes d'abord.</summary>
    public QuoteList Quotes()
    {
        var quotes = new List<Quote>();
        var trash = new List<Quote>();
        var unreadable = new List<UnreadableFile>();
        ReadQuotes(QuotesFolder, inTrash: false, quotes, unreadable);
        ReadQuotes(QuotesTrash, inTrash: true, trash, unreadable);
        return new QuoteList(Sorted(quotes), Sorted(trash), unreadable);

        static List<Quote> Sorted(List<Quote> list) =>
            [.. list.OrderByDescending(q => q.AddedOn ?? DateOnly.MinValue).ThenBy(q => q.AddedOn is null).ThenBy(q => q.Path, Text.Utf8Order.Instance)];
    }

    /// <summary>Ajoute une citation ; seul le texte est obligatoire.</summary>
    public Quote AddQuote(QuoteContent content, Moment now) => AddQuote(content, now, []);

    /// <summary>Modifie une citation : ses champs kyfRev sont réécrits, les autres conservés ; le fichier garde son nom.</summary>
    public Quote ModifyQuote(string path, QuoteContent content, Moment now)
    {
        QuoteFile.Verify(content);
        var (quote, inTrash) = ExistingQuote(path);
        if (inTrash)
        {
            throw VaultException.QuoteInTrash();
        }
        var absolute = Confine(quote);
        var bytes = ReadAllBytes(absolute);
        var current = TryDecode(bytes, out var text) ? text : throw VaultException.Io(absolute, new InvalidDataException("fichier non encodé en UTF-8"));
        Quote? previous;
        try
        {
            previous = QuoteFile.Read(current, quote.Value, inTrash: false);
        }
        catch (FormatException)
        {
            previous = null;
        }
        var id = previous?.Id ?? $"c_{_ids.Next()}";
        var addedOn = previous?.AddedOn ?? now.Day;
        var foreign = FrontMatter.Split(current).Yaml is { } yaml ? QuoteFile.ForeignLines(yaml) : [];
        var composed = QuoteFile.Compose(id, content, addedOn, foreign);
        ReplaceFile(absolute, Encoding.UTF8.GetBytes(composed));
        return ReadComposedQuote(composed, quote);
    }

    /// <summary>« Supprimer » : la citation va dans <c>citations/.supprimees/</c>.</summary>
    /// <returns>Son nouveau chemin.</returns>
    public string TrashQuote(string path)
    {
        var (quote, inTrash) = ExistingQuote(path);
        return inTrash ? quote.Value : MoveQuote(quote, QuotesTrash).Value;
    }

    /// <summary>Remet une citation de la corbeille.</summary>
    /// <returns>Son nouveau chemin.</returns>
    public string RestoreQuote(string path)
    {
        var (quote, inTrash) = ExistingQuote(path);
        return inTrash ? MoveQuote(quote, QuotesFolder).Value : quote.Value;
    }

    /// <summary>
    /// Retient la citation du jour parmi celles trouvées sur une page en ligne : une citation pas
    /// encore dans le coffre (corbeille comprise), tirée au hasard du jour, enregistrée dans
    /// <c>citations/</c> avec <c>origine: wikiquote</c>. Rien si la page n'apporte rien de nouveau.
    /// </summary>
    public Quote? KeepOnlineQuote(string page, IEnumerable<QuoteContent> found, Moment now)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(found);
        if (OnlineQuoteOf(now.Day) is { } already)
        {
            return already;
        }
        var list = Quotes();
        var known = list.Quotes.Concat(list.Trash).Select(q => QuoteFile.TextFingerprint(q.Content.Text)).ToHashSet(StringComparer.Ordinal);
        var fresh = found.Where(content => IsValid(content) && !known.Contains(QuoteFile.TextFingerprint(content.Text))).ToList();
        if (fresh.Count == 0)
        {
            return null;
        }
        var seed = DailySeed.For(Settings.Id, now.Day);
        var quote = AddQuote(fresh[(int)(seed % (ulong)fresh.Count)], now, ["origine: wikiquote"]);
        var kept = new OnlineQuoteFile(1, now.Day, quote.Path, page, UnknownProperties.None);
        ReplaceFile(Confine(OnlineQuotePath), kept.Write());
        return quote;

        static bool IsValid(QuoteContent content)
        {
            try
            {
                QuoteFile.Verify(content);
                return true;
            }
            catch (VaultException)
            {
                return false;
            }
        }
    }

    private Quote AddQuote(QuoteContent content, Moment now, string[] extraLines)
    {
        QuoteFile.Verify(content);
        CreateDirectory(Confine(QuotesFolder));
        var id = $"c_{_ids.Next()}";
        var bytes = Encoding.UTF8.GetBytes(QuoteFile.Compose(id, content, now.Day, extraLines));
        var name = QuoteFile.FileName(content);
        var stem = name.EndsWith(".md", StringComparison.Ordinal) ? name[..^3] : name;
        for (var number = 1; number < 1000; number++)
        {
            var path = QuotesFolder.Join(number == 1 ? $"{stem}.md" : $"{stem} ({number}).md");
            if (TryCreateFile(Confine(path), bytes))
            {
                return ReadComposedQuote(Encoding.UTF8.GetString(bytes), path);
            }
        }
        throw VaultException.AlreadyExists(QuotesFolder.ToAbsolute(Root));
    }

    /// <summary>La citation trouvée en ligne ce jour-là, si elle est toujours là (hors corbeille).</summary>
    private Quote? OnlineQuoteOf(DateOnly day)
    {
        if (TryReadAllBytes(Confine(OnlineQuotePath)) is not { } bytes)
        {
            return null;
        }
        OnlineQuoteFile kept;
        try
        {
            kept = OnlineQuoteFile.Read(bytes);
        }
        catch (VaultFormatException)
        {
            return null;
        }
        if (kept.Day != day || kept.Path is not { } path)
        {
            return null;
        }
        try
        {
            var (quote, inTrash) = ExistingQuote(path);
            if (inTrash || TryReadText(quote) is not var (_, text))
            {
                return null;
            }
            return QuoteFile.Read(text, quote.Value, inTrash: false);
        }
        catch (Exception exception) when (exception is VaultException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Chemin d'une citation (dans <c>citations/</c> ou sa corbeille), vérifié.</summary>
    private (VaultPath Path, bool InTrash) ExistingQuote(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var path = VaultPath.Create(text);
        var inTrash = path.Parent == QuotesTrash;
        var inPlace = path.Parent == QuotesFolder;
        if (!(inTrash || inPlace) || !path.Name.EndsWith(".md", StringComparison.Ordinal) || !File.Exists(Confine(path)))
        {
            throw VaultException.UnknownQuote(path.Value);
        }
        return (path, inTrash);
    }

    /// <summary>Déplace un fichier de citation sans jamais écraser : « nom (2).md »…</summary>
    private VaultPath MoveQuote(VaultPath quote, VaultPath folder)
    {
        var folderAbsolute = Confine(folder);
        CreateDirectory(folderAbsolute);
        var stem = WithoutMarkdownExtension(quote.Name);
        for (var number = 1; number < 1000; number++)
        {
            var target = folder.Join(number == 1 ? $"{stem}.md" : $"{stem} ({number}).md");
            var absolute = Confine(target);
            if (RealPaths.Exists(absolute))
            {
                continue;
            }
            var source = Confine(quote);
            Io(source, () =>
            {
                File.Move(source, absolute);
                return true;
            });
            return target;
        }
        throw VaultException.AlreadyExists(folderAbsolute);
    }

    private void ReadQuotes(VaultPath folder, bool inTrash, List<Quote> quotes, List<UnreadableFile> unreadable)
    {
        var absolute = Confine(folder);
        if (!Directory.Exists(absolute))
        {
            return;
        }
        foreach (var file in Io(absolute, () => new DirectoryInfo(absolute).EnumerateFiles().ToList()))
        {
            if (!file.Name.EndsWith(".md", StringComparison.Ordinal) || file.LinkTarget is not null)
            {
                continue;
            }
            var path = folder.Join(file.Name);
            string reason;
            if (TryReadAllBytes(file.FullName) is not { } bytes)
            {
                reason = "lecture impossible";
            }
            else if (!TryDecode(bytes, out var text))
            {
                reason = "encodage non UTF-8";
            }
            else
            {
                try
                {
                    quotes.Add(QuoteFile.Read(text, path.Value, inTrash));
                    continue;
                }
                catch (FormatException exception)
                {
                    reason = exception.Message;
                }
            }
            unreadable.Add(new UnreadableFile(path.Value, reason));
        }
    }

    private static Quote ReadComposedQuote(string text, VaultPath path)
    {
        try
        {
            return QuoteFile.Read(text, path.Value, inTrash: false);
        }
        catch (FormatException exception)
        {
            throw VaultException.InvalidQuote(exception.Message);
        }
    }
}
