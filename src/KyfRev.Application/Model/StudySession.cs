using System.Collections.Immutable;

namespace KyfRev.Application.Model;

/// <summary>Un élément d'une séance d'étude : une fiche, ou un passage de sa fiche.</summary>
/// <param name="Kind">Fiche ou passage.</param>
/// <param name="Id">Identifiant de la fiche ou de l'annotation.</param>
/// <param name="SheetId">Fiche (celle du passage).</param>
public sealed record SessionItem(ItemKind Kind, string Id, string SheetId);

/// <summary>
/// Une séance d'étude en cours, gardée sur ce poste pour être reprise si elle est interrompue
/// (vue quittée, application fermée) : le coffre et le jour où elle a commencé, ses éléments, où
/// on en est, ce qui a été fait, et le temps déjà passé.
/// </summary>
/// <param name="Vault">Dossier du coffre.</param>
/// <param name="Day">Jour de la séance : une séance d'un autre jour n'est pas reprise.</param>
/// <param name="Items">Les éléments, dans l'ordre de la séance.</param>
/// <param name="Position">Rang de l'élément en cours (à partir de 0).</param>
/// <param name="Done">Éléments révisés (validés, recommencés ou archivés).</param>
/// <param name="Postponed">Éléments reportés.</param>
/// <param name="Skipped">Éléments passés sans rien faire.</param>
/// <param name="Elapsed">Temps déjà passé dans la séance.</param>
/// <param name="ActiveRecall">Rappel actif : les passages restent cachés jusqu'à la demande.</param>
public sealed record StudySession(
    string Vault,
    DateOnly Day,
    ImmutableArray<SessionItem> Items,
    int Position,
    int Done,
    int Postponed,
    int Skipped,
    TimeSpan Elapsed,
    bool ActiveRecall)
{
    /// <summary>Éléments qui restent, l'élément en cours compris.</summary>
    public int Remaining => Math.Max(0, Items.Length - Position);
}
