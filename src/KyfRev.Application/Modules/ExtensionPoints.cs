using System.Collections.Immutable;
using KyfRev.Application.Model;

namespace KyfRev.Application.Modules;

/// <summary>Une entrée du menu de navigation.</summary>
/// <param name="Id">Identifiant de la vue ouverte.</param>
/// <param name="Label">Libellé affiché.</param>
/// <param name="Order">Place dans le menu, les plus petites d'abord.</param>
public sealed record NavigationEntry(string Id, string Label, int Order);

/// <summary>Un encart du tableau de bord, prêt à afficher.</summary>
/// <param name="Title">Titre de l'encart.</param>
/// <param name="Text">Texte principal.</param>
/// <param name="Detail">Précision, sous le texte.</param>
public sealed record DashboardCard(string Title, string Text, string? Detail);

/// <summary>Une remarque de « Santé du coffre » apportée par un module.</summary>
/// <param name="Path">Fichier concerné, relatif au coffre.</param>
/// <param name="Message">Ce qui ne va pas, prêt à afficher.</param>
public sealed record HealthNotice(string Path, string Message);

/// <summary>Les natures des éléments que la recherche globale sait ouvrir.</summary>
public static class SearchKinds
{
    /// <summary>Une fiche.</summary>
    public const string Sheet = "fiche";

    /// <summary>Un passage annoté.</summary>
    public const string Passage = "passage";

    /// <summary>Une définition.</summary>
    public const string Definition = "definition";

    /// <summary>Une citation.</summary>
    public const string Quote = "citation";
}

/// <summary>Un résultat de la recherche globale.</summary>
/// <param name="Kind">Nature de l'élément trouvé (<see cref="SearchKinds"/>).</param>
/// <param name="Id">Ce qui le désigne (identifiant ou chemin).</param>
/// <param name="Title">Titre affiché : celui de la fiche, le terme, le passage, l'auteur ; vide si on ne le connaît pas.</param>
/// <param name="Excerpt">Le texte autour des mots trouvés ; vide quand le titre les contient tous.</param>
public sealed record SearchHit(string Kind, string Id, string Title, ImmutableArray<ExcerptPiece> Excerpt)
{
    /// <summary>La fiche d'un passage ou d'une définition.</summary>
    public string? SheetId { get; init; }

    /// <summary>Où il se trouve : le dossier d'une fiche, le titre de la fiche d'un passage.</summary>
    public string? Detail { get; init; }

    /// <summary>Ce qui se lit à la place de l'extrait quand il est vide.</summary>
    public string? Text { get; init; }

    /// <summary>Une fiche archivée.</summary>
    public bool IsArchived { get; init; }
}

/// <summary>Point d'extension : une entrée du menu de navigation (la coquille, J5, les assemble).</summary>
public interface INavigationContributor
{
    /// <summary>L'entrée apportée.</summary>
    NavigationEntry Entry { get; }
}

/// <summary>Point d'extension : un encart du tableau de bord (J6).</summary>
public interface IDashboardWidget
{
    /// <summary>Identifiant stable de l'encart.</summary>
    string Id { get; }

    /// <summary>Place sur le tableau de bord, les plus petites d'abord.</summary>
    int Order { get; }

    /// <summary>Contenu du jour ; rien si l'encart n'a rien à montrer.</summary>
    DashboardCard? Card();
}

/// <summary>Point d'extension : des remarques pour « Santé du coffre ».</summary>
public interface IVaultHealthCheck
{
    /// <summary>Remarques sur le coffre ouvert ; aucune si tout va bien.</summary>
    IReadOnlyList<HealthNotice> Notices();
}

/// <summary>Point d'extension : des résultats pour la recherche globale (J9).</summary>
public interface ISearchProvider
{
    /// <summary>Éléments qui contiennent tous les mots de <paramref name="query"/> ; annulée, la recherche rend ce qu'elle a déjà trouvé.</summary>
    Outcome<IReadOnlyList<SearchHit>> Search(string query, CancellationToken cancellationToken);
}

/// <summary>Point d'extension : un format d'export de plus (J9).</summary>
public interface IExportFormat
{
    /// <summary>Identifiant stable du format.</summary>
    string Id { get; }

    /// <summary>Libellé affiché.</summary>
    string Label { get; }

    /// <summary>Écrit l'export dans <paramref name="folder"/>, hors du coffre ; rend les fichiers créés.</summary>
    Outcome<IReadOnlyList<string>> Export(string folder);
}

/// <summary>Point d'extension : un langage de bloc de code dessiné au lieu d'être affiché (J7, J10).</summary>
public interface IBlockRenderer
{
    /// <summary>Langage du bloc (« reseau » pour <c>```reseau</c>).</summary>
    string Language { get; }
}
