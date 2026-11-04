using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KyfRev.Application.Model;
using KyfRev.Application.Ports;
using KyfRev.Modules.Citations;
using KyfRev.Presentation.Wpf.Shell;

namespace KyfRev.Presentation.Wpf.Pages;

/// <summary>L'ordre des citations.</summary>
public enum QuoteSort
{
    /// <summary>Les plus récemment ajoutées d'abord.</summary>
    Added,

    /// <summary>Par auteur, les citations sans auteur à la fin.</summary>
    Author,

    /// <summary>Par époque, les citations sans date à la fin.</summary>
    Period,
}

/// <summary>Une citation de la liste.</summary>
public sealed class QuoteRow
{
    internal QuoteRow(Quote quote, string reference, string? added, IRelayCommand? openSource, IRelayCommand edit, IRelayCommand trash)
    {
        Quote = quote;
        Reference = reference;
        Added = added;
        OpenSource = openSource;
        Edit = edit;
        Trash = trash;
    }

    /// <summary>La citation.</summary>
    public Quote Quote { get; }

    /// <summary>Son texte.</summary>
    public string Text => Quote.Content.Text;

    /// <summary>« — Lavoisier ».</summary>
    public string Attribution => QuotesText.AttributionOf(Quote.Content.Author ?? QuotesText.UnknownAuthor);

    /// <summary>« Livre : Traité, p. 12 · 1789 · #chimie ».</summary>
    public string Reference { get; }

    /// <summary>Son contexte.</summary>
    public string? Context => Quote.Content.Context;

    /// <summary>Elle a un contexte.</summary>
    public bool HasContext => Context is { Length: > 0 };

    /// <summary>« ajoutée le 3 nov. ».</summary>
    public string? Added { get; }

    /// <summary>Son jour d'ajout est connu.</summary>
    public bool HasAdded => Added is not null;

    /// <summary>Ouvre sa source en ligne ; rien sans adresse web.</summary>
    public IRelayCommand? OpenSource { get; }

    /// <summary>Elle a une source à ouvrir.</summary>
    public bool HasSource => OpenSource is not null;

    /// <summary>« Wikiquote » ou « Source ».</summary>
    public string SourceLabel => Quote.Origin == "wikiquote" ? QuotesText.Wikiquote : QuotesText.Source;

    /// <summary>Ouvre le formulaire.</summary>
    public IRelayCommand Edit { get; }

    /// <summary>Demande à la ranger dans la corbeille.</summary>
    public IRelayCommand Trash { get; }

    /// <summary>Le début de la citation : toutes les lignes ont les mêmes boutons, lui les distingue.</summary>
    private string Beginning => Text.Length <= 40 ? Text : string.Concat(Text.AsSpan(0, 40), "…");

    /// <summary>Le nom du bouton « Source ».</summary>
    public string SourceName => QuotesText.OpenSourceOf(Beginning);

    /// <summary>Le nom du bouton « Modifier ».</summary>
    public string EditName => QuotesText.EditNameOf(Beginning);

    /// <summary>Le nom du bouton « Supprimer ».</summary>
    public string TrashName => QuotesText.TrashNameOf(Beginning);
}

/// <summary>Une citation de la corbeille.</summary>
public sealed class TrashedQuoteRow
{
    internal TrashedQuoteRow(Quote quote, IRelayCommand restore)
    {
        Excerpt = QuotesText.ExcerptOf(quote.Content.Text, quote.Content.Author ?? QuotesText.UnknownAuthor);
        Restore = restore;
    }

    /// <summary>« « Le texte » — Lavoisier ».</summary>
    public string Excerpt { get; }

    /// <summary>La remet dans la liste.</summary>
    public IRelayCommand Restore { get; }

    /// <summary>Le nom du bouton « Restaurer ».</summary>
    public string RestoreName => QuotesText.RestoreNameOf(Excerpt);
}

/// <summary>
/// La vue « Citations », du module du même nom, comme dans la 0.x : consultation, recherche, ajout et
/// modification ; « Supprimer » range dans la corbeille.
/// </summary>
public sealed partial class QuotesPageViewModel : ObservableObject, INavigationAware
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly QuoteUseCases _quotes;
    private readonly IExternalLinks _links;
    private List<QuoteRow> _all = [];
    private Quote? _editing;
    private Quote? _toTrash;

    /// <summary>La vue, sur les cas d'usage du module.</summary>
    public QuotesPageViewModel(QuoteUseCases quotes, IExternalLinks links)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(links);
        _quotes = quotes;
        _links = links;
        Sort = Sorts[0];
        Tags = [AllTags];
        Tag = AllTags;
        SourceKind = SourceKinds[0];
    }

    /// <summary>« Citations ».</summary>
    public static string Title => QuotesText.Title;

    /// <summary>Les ordres proposés.</summary>
    public static IReadOnlyList<SettingChoice<QuoteSort>> Sorts { get; } =
    [
        new(QuoteSort.Added, QuotesText.SortAdded),
        new(QuoteSort.Author, QuotesText.SortAuthor),
        new(QuoteSort.Period, QuotesText.SortPeriod),
    ];

    /// <summary>Les natures de source proposées.</summary>
    public static IReadOnlyList<SettingChoice<SourceKind?>> SourceKinds { get; } =
    [
        new(null, QuotesText.KindNone),
        new(Application.Model.SourceKind.Book, QuotesText.KindBook),
        new(Application.Model.SourceKind.Newspaper, QuotesText.KindNewspaper),
        new(Application.Model.SourceKind.Article, QuotesText.KindArticle),
        new(Application.Model.SourceKind.Speech, QuotesText.KindSpeech),
        new(Application.Model.SourceKind.Website, QuotesText.KindWebsite),
        new(Application.Model.SourceKind.Film, QuotesText.KindFilm),
        new(Application.Model.SourceKind.Song, QuotesText.KindSong),
        new(Application.Model.SourceKind.Broadcast, QuotesText.KindBroadcast),
        new(Application.Model.SourceKind.Other, QuotesText.KindOther),
        new(Application.Model.SourceKind.Unknown, QuotesText.KindUnknown),
    ];

    private static SettingChoice<string> AllTags { get; } = new(string.Empty, QuotesText.AllTags);

    /// <summary>La liste, filtrée et triée.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows), nameof(HasNoMatch))]
    public partial IReadOnlyList<QuoteRow> Rows { get; private set; } = [];

    /// <summary>Au moins une citation à montrer.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Des citations, mais aucune ne correspond à la recherche.</summary>
    public bool HasNoMatch => HasQuotes && Rows.Count == 0;

    /// <summary>La corbeille.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrash), nameof(TrashTitle))]
    public partial IReadOnlyList<TrashedQuoteRow> Trash { get; private set; } = [];

    /// <summary>La corbeille n'est pas vide.</summary>
    public bool HasTrash => Trash.Count > 0;

    /// <summary>« Corbeille (2) ».</summary>
    public string TrashTitle => QuotesText.TrashTitleOf(Trash.Count);

    /// <summary>« 2 fichiers illisibles dans « citations » : … ».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadable))]
    public partial string? Unreadable { get; private set; }

    /// <summary>Des fichiers n'ont pas pu être lus.</summary>
    public bool HasUnreadable => Unreadable is not null;

    /// <summary>Le coffre a au moins une citation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasNoMatch))]
    public partial bool HasQuotes { get; private set; }

    /// <summary>La liste a été lue, et le coffre n'a aucune citation.</summary>
    public bool IsEmpty => IsLoaded && !HasQuotes;

    /// <summary>« 3 citations ».</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    /// <summary>La liste a été lue.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Le texte cherché.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    /// <summary>Les étiquettes proposées.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTags))]
    public partial IReadOnlyList<SettingChoice<string>> Tags { get; private set; }

    /// <summary>Le coffre a des étiquettes à proposer.</summary>
    public bool HasTags => Tags.Count > 1;

    /// <summary>L'étiquette choisie.</summary>
    [ObservableProperty]
    public partial SettingChoice<string>? Tag { get; set; }

    /// <summary>L'ordre choisi.</summary>
    [ObservableProperty]
    public partial SettingChoice<QuoteSort> Sort { get; set; }

    /// <summary>Refus de la dernière opération.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Une opération a été refusée.</summary>
    public bool HasError => Error is not null;

    /// <summary>Résultat de la dernière action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Une action vient d'aboutir.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Le formulaire est ouvert.</summary>
    [ObservableProperty]
    public partial bool IsFormOpen { get; private set; }

    /// <summary>« Nouvelle citation » ou « Modifier la citation ».</summary>
    [ObservableProperty]
    public partial string FormTitle { get; private set; } = QuotesText.NewQuote;

    /// <summary>Refus du formulaire.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFormError))]
    public partial string? FormError { get; private set; }

    /// <summary>Le formulaire a été refusé.</summary>
    public bool HasFormError => FormError is not null;

    /// <summary>Le texte saisi.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>L'auteur saisi.</summary>
    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    /// <summary>La date saisie.</summary>
    [ObservableProperty]
    public partial string Date { get; set; } = string.Empty;

    /// <summary>La nature de la source choisie.</summary>
    [ObservableProperty]
    public partial SettingChoice<SourceKind?> SourceKind { get; set; }

    /// <summary>Le titre de l'œuvre saisi.</summary>
    [ObservableProperty]
    public partial string SourceTitle { get; set; } = string.Empty;

    /// <summary>La page saisie.</summary>
    [ObservableProperty]
    public partial string SourcePage { get; set; } = string.Empty;

    /// <summary>L'adresse web saisie.</summary>
    [ObservableProperty]
    public partial string SourceAddress { get; set; } = string.Empty;

    /// <summary>Le contexte saisi.</summary>
    [ObservableProperty]
    public partial string Context { get; set; } = string.Empty;

    /// <summary>Les étiquettes saisies, séparées par des virgules.</summary>
    [ObservableProperty]
    public partial string TagsText { get; set; } = string.Empty;

    /// <summary>La mise à la corbeille attend sa confirmation.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingTrash { get; private set; }

    /// <inheritdoc/>
    public void OnNavigatedTo()
    {
        Error = null;
        Message = null;
        ReloadCommand.Execute(null);
    }

    /// <summary>« Livre : Traité, p. 12 · 1789 · #chimie » : ce qui manque est dit.</summary>
    internal static string ReferenceOf(QuoteContent content)
    {
        var source = content.Source;
        var kind = source.Kind is { } known and not Application.Model.SourceKind.Unknown ? SourceKinds.First(choice => choice.Value == known).Label : null;
        var title = source.Title;
        var work = (kind, title) switch
        {
            (null, null) => QuotesText.KindUnknownSource,
            (null, _) => title,
            (_, null) => kind,
            _ => QuotesText.KindAndTitleOf(kind, title),
        };
        if (source.Page is { Length: > 0 } page)
        {
            work = QuotesText.WithPageOf(work!, page);
        }
        var parts = new List<string> { work!, content.Date ?? QuotesText.UnknownDate };
        parts.AddRange(content.Tags.Select(tag => "#" + tag));
        return string.Join(" · ", parts);
    }

    private static string Plain(string text) =>
        string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLower(French);

    private static string? Filled(string text) => text.Trim() is { Length: > 0 } value ? value : null;

    /// <summary>Relit les citations.</summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        var outcome = await Task.Run(_quotes.Quotes).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            Error = outcome.Error;
            return;
        }
        var list = outcome.Value;
        _all = [.. list.Quotes.Select(RowOf)];
        HasQuotes = _all.Count > 0;
        Summary = Dates.Plural(_all.Count, QuotesText.CountOneOf, QuotesText.CountManyOf);
        Trash = [.. list.Trash.Select(quote => new TrashedQuoteRow(quote, new AsyncRelayCommand(() => ActAsync(() => _quotes.Restore(quote.Path).Error, QuotesText.Restored))))];
        Unreadable = list.Unreadable.Count == 0
            ? null
            : QuotesText.UnreadableOf(Dates.Plural(list.Unreadable.Count, QuotesText.UnreadableOneOf, QuotesText.UnreadableManyOf), string.Join(", ", list.Unreadable.Select(file => file.Path)));
        var chosen = Tag;
        Tags = [AllTags, .. list.Quotes.SelectMany(quote => quote.Content.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Create(French, ignoreCase: true)).Select(tag => new SettingChoice<string>(tag, tag))];
        Tag = Tags.FirstOrDefault(choice => choice == chosen) ?? AllTags;
        IsLoaded = true;
        Filter();
    }

    private QuoteRow RowOf(Quote quote)
    {
        var address = quote.Content.Source.Address;
        var hasLink = address is not null && (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        return new QuoteRow(
            quote,
            ReferenceOf(quote.Content),
            quote.AddedOn is { } day ? QuotesText.AddedOnOf(day.ToString("d MMM yyyy", French)) : null,
            hasLink ? new RelayCommand(() => _links.Open(address!)) : null,
            new RelayCommand(() => OpenForm(quote)),
            new RelayCommand(() =>
            {
                _toTrash = quote;
                IsConfirmingTrash = true;
            }));
    }

    private void Filter()
    {
        var wanted = Plain(Search.Trim());
        var tag = Tag?.Value ?? string.Empty;
        var kept = _all.Where(row =>
        {
            var content = row.Quote.Content;
            if (tag.Length > 0 && !content.Tags.Contains(tag))
            {
                return false;
            }
            var everything = string.Join(' ', new[] { content.Text, content.Author, content.Date, content.Source.Title, content.Context }.OfType<string>().Concat(content.Tags));
            return wanted.Length == 0 || Plain(everything).Contains(wanted, StringComparison.Ordinal);
        });
        var french = StringComparer.Create(French, ignoreCase: false);
        Rows = Sort.Value switch
        {
            QuoteSort.Author => [.. kept.OrderBy(row => row.Quote.Content.Author is null).ThenBy(row => row.Quote.Content.Author, french)],
            QuoteSort.Period => [.. kept.OrderBy(row => row.Quote.Year ?? int.MaxValue)],
            _ => [.. kept.OrderByDescending(row => row.Quote.AddedOn ?? DateOnly.MinValue)],
        };
    }

    partial void OnSearchChanged(string value) => Filter();

    partial void OnTagChanged(SettingChoice<string>? value) => Filter();

    partial void OnSortChanged(SettingChoice<QuoteSort> value) => Filter();

    /// <summary>« Nouvelle citation ».</summary>
    [RelayCommand]
    private void NewQuote() => OpenForm(null);

    private void OpenForm(Quote? quote)
    {
        _editing = quote;
        FormError = null;
        FormTitle = quote is null ? QuotesText.NewQuote : QuotesText.EditQuote;
        var content = quote?.Content;
        Text = content?.Text ?? string.Empty;
        Author = content?.Author ?? string.Empty;
        Date = content?.Date ?? string.Empty;
        SourceKind = SourceKinds.First(choice => choice.Value == content?.Source.Kind);
        SourceTitle = content?.Source.Title ?? string.Empty;
        SourcePage = content?.Source.Page ?? string.Empty;
        SourceAddress = content?.Source.Address ?? string.Empty;
        Context = content?.Context ?? string.Empty;
        TagsText = content is null ? string.Empty : string.Join(", ", content.Tags);
        IsFormOpen = true;
    }

    /// <summary>La réponse au formulaire : un refus le laisse ouvert, avec ce qui a été saisi.</summary>
    [RelayCommand]
    private async Task AnswerFormAsync(bool confirmed)
    {
        IsFormOpen = false;
        if (!confirmed)
        {
            FormError = null;
            return;
        }
        if (Text.Trim().Length == 0)
        {
            FormError = QuotesText.TextRequired;
            IsFormOpen = true;
            return;
        }
        var content = new QuoteContent(
            Text.Trim(),
            Filled(Author),
            Filled(Date),
            new QuoteSource(SourceKind.Value, Filled(SourceTitle), Filled(SourcePage), Filled(SourceAddress)),
            Filled(Context),
            [.. TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);
        var editing = _editing;
        var outcome = await Task.Run(() => editing is null ? _quotes.Add(content) : _quotes.Modify(editing.Path, content)).ConfigureAwait(true);
        if (!outcome.Succeeded)
        {
            FormError = outcome.Error;
            IsFormOpen = true;
            return;
        }
        FormError = null;
        Error = null;
        Message = editing is null ? QuotesText.Added : QuotesText.Modified;
        await ReloadAsync().ConfigureAwait(true);
    }

    /// <summary>La réponse à la mise à la corbeille.</summary>
    [RelayCommand]
    private async Task AnswerTrashAsync(bool confirmed)
    {
        IsConfirmingTrash = false;
        if (confirmed && _toTrash is { } quote)
        {
            await ActAsync(() => _quotes.Trash(quote.Path).Error, QuotesText.Trashed).ConfigureAwait(true);
        }
    }

    private async Task ActAsync(Func<string?> action, string done)
    {
        Error = null;
        Message = null;
        var refusal = await Task.Run(action).ConfigureAwait(true);
        if (refusal is not null)
        {
            Error = refusal;
            return;
        }
        Message = done;
        await ReloadAsync().ConfigureAwait(true);
    }
}
