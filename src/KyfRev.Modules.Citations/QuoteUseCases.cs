using KyfRev.Application;
using KyfRev.Application.Model;
using KyfRev.Application.UseCases;

namespace KyfRev.Modules.Citations;

/// <summary>
/// Cas d'usage du module Citations. « Supprimer » range la citation dans
/// <c>citations/.supprimees/</c> : rien n'est jamais effacé.
/// </summary>
public sealed class QuoteUseCases(VaultHost host, TimeProvider time, OnlineQuoteSearch search)
{
    /// <summary>Toutes les citations, celles de la corbeille à part, et les fichiers illisibles.</summary>
    [ZeroXCommand("citations")]
    public Outcome<QuoteList> Quotes() => host.Use(vault => vault.Quotes());

    /// <summary>Ajoute une citation ; seul le texte est obligatoire.</summary>
    [ZeroXCommand("ajouter_citation")]
    public Outcome<Quote> Add(QuoteContent content)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.AddQuote(content, now));
    }

    /// <summary>Modifie la citation <paramref name="path"/> (relatif au coffre).</summary>
    [ZeroXCommand("modifier_citation")]
    public Outcome<Quote> Modify(string path, QuoteContent content)
    {
        var now = Present.Of(time).Now;
        return host.Use(vault => vault.ModifyQuote(path, content, now));
    }

    /// <summary>Range la citation dans la corbeille ; rend son nouveau chemin.</summary>
    [ZeroXCommand("mettre_citation_a_la_corbeille")]
    public Outcome<string> Trash(string path) => host.Use(vault => vault.TrashQuote(path));

    /// <summary>Remet une citation de la corbeille ; rend son nouveau chemin.</summary>
    [ZeroXCommand("restaurer_citation")]
    public Outcome<string> Restore(string path) => host.Use(vault => vault.RestoreQuote(path));

    /// <summary>Efface pour de bon une citation déjà à la corbeille ; cela ne s'annule pas.</summary>
    public Outcome<Done> Purge(string path) => host.Use(vault => vault.PurgeQuote(path));

    /// <summary>
    /// Cherche la citation du jour sur Wikiquote, si besoin ; rend la citation retenue, ou rien.
    /// Un échec n'est jamais un refus : la citation du jour reste celle du coffre.
    /// </summary>
    [ZeroXCommand("chercher_citation_du_jour")]
    public async Task<Outcome<Quote?>> SearchQuoteOfTheDayAsync(CancellationToken cancellationToken) =>
        Outcome.Of(await search.SearchAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>La citation du jour (tableau de bord) ; rien si le coffre n'en a aucune.</summary>
    public Outcome<Quote?> QuoteOfTheDay()
    {
        var day = Present.Of(time).Now.Day;
        return host.Use(vault => DailyQuote.Of(vault, day));
    }
}
