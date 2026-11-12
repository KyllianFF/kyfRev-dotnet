using System.Collections.Immutable;
using KyfRev.Domain.Reviews;

namespace KyfRev.Application.Model;

/// <summary>Une fiche ou un passage : en révision, ou archivé.</summary>
public enum ReviewStatus
{
    /// <summary>Un cycle est en cours.</summary>
    Active,

    /// <summary>Archivé.</summary>
    Archived,
}

/// <summary>Une étape du cycle en cours (du dernier cycle pour un élément archivé), pour la frise des étapes.</summary>
/// <param name="Label">« J7 ».</param>
/// <param name="DoneOn">Jour où l'étape a été validée ; rien tant qu'elle reste à faire.</param>
/// <param name="DaysLate">Retard de cette validation en jours (négatif : en avance).</param>
public sealed record CycleStep(string Label, DateOnly? DoneOn, int DaysLate);

/// <summary>Où en est une fiche ou un passage dans son calendrier, un jour donné.</summary>
/// <param name="Status">En révision ou archivé.</param>
/// <param name="Steps">Les étapes du cycle, J0 compris, faites ou à faire.</param>
/// <param name="Step">Étape attendue (« J7 »).</param>
/// <param name="DueDate">Échéance de l'étape attendue.</param>
/// <param name="Cycle">Numéro du cycle en cours.</param>
/// <param name="LastReview">Dernière révision validée.</param>
/// <param name="DaysLate">Jours de retard (0 si l'échéance n'est pas dépassée).</param>
/// <param name="ReviewedToday">Une révision a déjà été validée ce jour-là (une seule par jour).</param>
/// <param name="ArchivedOn">Jour de l'archivage.</param>
/// <param name="ReminderPending">Un rappel de consolidation attend une réponse.</param>
/// <param name="IncludedInReminders">Archivé et inclus dans les rappels de consolidation.</param>
/// <param name="EligibleOn">Jour à partir duquel un rappel peut être tiré.</param>
/// <param name="StartedToday">Cycle commencé ce jour-là : c'est ainsi qu'un passage est « marqué » pendant un rappel.</param>
/// <param name="CyclesStarted">Cycles commencés.</param>
/// <param name="CyclesCompleted">Cycles terminés.</param>
public sealed record ReviewState(
    ReviewStatus Status,
    ImmutableArray<CycleStep> Steps,
    string? Step,
    DateOnly? DueDate,
    int? Cycle,
    DateOnly? LastReview,
    int DaysLate,
    bool ReviewedToday,
    DateOnly? ArchivedOn,
    bool ReminderPending,
    bool IncludedInReminders,
    DateOnly? EligibleOn,
    bool StartedToday,
    int CyclesStarted,
    int CyclesCompleted)
{
    /// <summary>Jour où l'élément a été suspendu ; rien s'il ne l'est pas.</summary>
    public DateOnly? SuspendedOn { get; init; }

    /// <summary>L'élément est une simple note, tenue hors des révisions.</summary>
    public bool IsNote { get; init; }

    /// <summary>État de révision de <paramref name="tracking"/> au <paramref name="day"/> donné.</summary>
    public static ReviewState Of(Tracking tracking, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        var state = new ReviewState(
            ReviewStatus.Archived, StepsOf(tracking), null, null, null, null, 0, false, null, false, false, null, false,
            tracking.Counters.Started, tracking.Counters.Completed);
        if (tracking.Cycle is { } cycle)
        {
            state = state with
            {
                Status = ReviewStatus.Active,
                Step = cycle.StepLabel,
                DueDate = cycle.DueDate,
                Cycle = cycle.Number,
                LastReview = cycle.LastReview,
                DaysLate = cycle.DaysLate(day),
                ReviewedToday = cycle.LastReview == day,
                StartedToday = cycle.StartedOn == day,
                SuspendedOn = cycle.SuspendedOn,
                IsNote = cycle.IsNote,
            };
        }
        if (tracking.Archive is { } archive)
        {
            state = state with
            {
                ArchivedOn = archive.ArchivedOn,
                ReminderPending = archive.ReminderDrawnOn is not null,
                IncludedInReminders = archive.IncludedInReminders,
                EligibleOn = archive.EligibleOn,
            };
        }
        return state;
    }

    /// <summary>
    /// Les étapes du cycle en cours, ou du dernier cycle commencé pour un élément archivé, avec le
    /// jour de chaque validation (J0 compris). Sans cycle en cours, seules les étapes validées
    /// sont connues.
    /// </summary>
    private static ImmutableArray<CycleStep> StepsOf(Tracking tracking)
    {
        var reviews = tracking.History.OfType<ReviewEvent>().ToList();
        int number;
        if (tracking.Cycle is { } cycle)
        {
            number = cycle.Number;
        }
        else if (reviews.Count > 0)
        {
            number = reviews.Max(review => review.CycleNumber);
        }
        else
        {
            return [];
        }
        var done = reviews.Where(review => review.CycleNumber == number).ToList();
        if (tracking.Cycle is not { } current)
        {
            // Après un oubli, une étape a été revue deux fois : la frise garde la dernière.
            return [.. done.GroupBy(review => review.StepLabel).Select(group => new CycleStep(group.Key, group.Last().Day, group.Last().DaysLate))];
        }
        // Une étape n'est faite que si elle précède l'étape attendue : après un oubli, celle qui a
        // reculé et les suivantes sont de nouveau à faire, même si l'historique les a déjà vues.
        return
        [
            .. Enumerable.Range(0, current.Sequence.LastStep + 1).Select(step =>
            {
                var label = current.Sequence.Label(step);
                return step < current.Step && done.LastOrDefault(review => review.StepLabel == label) is { } review
                    ? new CycleStep(label, review.Day, review.DaysLate)
                    : new CycleStep(label, null, 0);
            }),
        ];
    }
}

/// <summary>Repères d'un passage (modèle W3C Web Annotation) : citation exacte, contexte et position dans le texte affiché.</summary>
/// <param name="Exact">Texte exact du passage.</param>
/// <param name="Before">Jusqu'à 32 caractères avant lui.</param>
/// <param name="After">Jusqu'à 32 caractères après lui.</param>
/// <param name="Start">Début, en unités UTF-16 du texte affiché.</param>
/// <param name="End">Fin, en unités UTF-16 du texte affiché.</param>
/// <param name="Section">Titres qui mènent au passage.</param>
/// <param name="SourceLine">Ligne du fichier Markdown (repère approximatif).</param>
/// <param name="SheetFingerprint">Empreinte de la fiche au moment de l'ancrage.</param>
public sealed record Anchor(
    string Exact,
    string Before,
    string After,
    long Start,
    long End,
    ImmutableArray<string> Section,
    long? SourceLine,
    string SheetFingerprint);

/// <summary>Une définition : le terme et son explication.</summary>
public sealed record Definition(string Term, string Explanation)
{
    /// <summary>Le terme se marque dans toutes les fiches qui le contiennent, et pas seulement à sa place.</summary>
    public bool Everywhere { get; init; } = true;
}

/// <summary>Une annotation de la fiche ouverte.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Comment">Commentaire.</param>
/// <param name="Definition">Présente si l'annotation est une définition.</param>
/// <param name="Anchor">Repères du passage.</param>
/// <param name="AnchorState">État d'ancrage enregistré.</param>
/// <param name="InTrash">À la corbeille.</param>
/// <param name="Review">Son calendrier.</param>
public sealed record AnnotationView(
    string Id,
    string? Comment,
    Definition? Definition,
    Anchor Anchor,
    AnchorState AnchorState,
    bool InTrash,
    ReviewState Review);

/// <summary>Une définition du coffre, avec ce qu'il faut pour la retrouver.</summary>
/// <param name="Id">L'annotation qui la porte.</param>
/// <param name="SheetId">Sa fiche.</param>
/// <param name="SheetTitle">Le titre de sa fiche.</param>
/// <param name="Folder">Le dossier de sa fiche, relatif à <c>fiches/</c>.</param>
/// <param name="Term">Le terme.</param>
/// <param name="Explanation">L'explication.</param>
/// <param name="Section">Les titres au-dessus du passage défini.</param>
/// <param name="AnchorState">L'état de sa position dans la fiche.</param>
/// <param name="Step">La prochaine étape de révision ; rien si elle n'en a plus.</param>
/// <param name="DueDate">La date de cette étape.</param>
public sealed record VaultDefinition(
    string Id,
    string SheetId,
    string SheetTitle,
    string Folder,
    string Term,
    string Explanation,
    ImmutableArray<string> Section,
    AnchorState AnchorState,
    string? Step,
    DateOnly? DueDate)
{
    /// <summary>Le terme se marque dans toutes les fiches qui le contiennent.</summary>
    public bool Everywhere { get; init; } = true;
}

/// <summary>Une fiche du coffre, active ou archivée.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre.</param>
/// <param name="Path">Chemin dans le coffre (« fiches/Histoire/Révolution.md »).</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c> (« Histoire »), vide à la racine.</param>
/// <param name="Tags">Étiquettes.</param>
/// <param name="Archived">Archivée.</param>
/// <param name="FilePresent">Faux si le fichier est introuvable ou en double.</param>
/// <param name="Step">Prochaine étape d'une fiche active.</param>
/// <param name="DueDate">Son échéance.</param>
public sealed record VaultSheet(
    string Id,
    string Title,
    string Path,
    string Folder,
    ImmutableArray<string> Tags,
    bool Archived,
    bool FilePresent,
    string? Step,
    DateOnly? DueDate)
{
    /// <summary>Une simple note, tenue hors des révisions.</summary>
    public bool IsNote { get; init; }
}

/// <summary>Un morceau de l'extrait d'un rétrolien.</summary>
/// <param name="Text">Son texte.</param>
/// <param name="IsLink">Le texte du lien : il ressort.</param>
public sealed record ExcerptPart(string Text, bool IsLink);

/// <summary>Une fiche qui contient un lien vers la fiche ouverte.</summary>
/// <param name="SheetId">Son identifiant.</param>
/// <param name="Title">Son titre.</param>
/// <param name="Folder">Son dossier relatif à <c>fiches/</c>, vide à la racine.</param>
/// <param name="Archived">Archivée.</param>
/// <param name="Excerpt">Le texte autour de son premier lien, sur une ligne.</param>
/// <param name="Links">Ses liens vers la fiche ouverte.</param>
public sealed record Backlink(
    string SheetId,
    string Title,
    string Folder,
    bool Archived,
    ImmutableArray<ExcerptPart> Excerpt,
    int Links);

/// <summary>Le début d'une fiche, pour l'aperçu au survol d'un lien.</summary>
/// <param name="Id">Son identifiant.</param>
/// <param name="Title">Son titre.</param>
/// <param name="Folder">Son dossier relatif à <c>fiches/</c>, vide à la racine.</param>
/// <param name="Tags">Ses étiquettes.</param>
/// <param name="Archived">Archivée.</param>
/// <param name="Excerpt">Le début du texte, sans mise en forme ; « … » s'il continue.</param>
/// <param name="Step">Étape attendue d'une fiche active.</param>
/// <param name="DueDate">Son échéance.</param>
public sealed record SheetPreview(
    string Id,
    string Title,
    string Folder,
    ImmutableArray<string> Tags,
    bool Archived,
    string Excerpt,
    string? Step,
    DateOnly? DueDate);

/// <summary>Une fiche ouverte pour la lire ou la réviser. Ouvrir ne valide rien.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre.</param>
/// <param name="Path">Chemin dans le coffre (« fiches/Histoire/Révolution.md »).</param>
/// <param name="Folder">Dossier relatif à <c>fiches/</c> (« Histoire »), vide à la racine.</param>
/// <param name="Tags">Étiquettes.</param>
/// <param name="Body">Texte Markdown sans l'en-tête YAML.</param>
/// <param name="FirstBodyLine">Ligne du fichier (à partir de 1) où commence le corps : repère des passages.</param>
/// <param name="Fingerprint">Empreinte du fichier complet : preuve de la version lue.</param>
/// <param name="CreatedOn">Date de création écrite dans l'en-tête.</param>
/// <param name="ModifiedAt">Dernière modification enregistrée.</param>
/// <param name="Review">Son calendrier.</param>
/// <param name="Annotations">Ses passages et définitions, corbeille comprise.</param>
public sealed record OpenedSheet(
    string Id,
    string Title,
    string Path,
    string Folder,
    ImmutableArray<string> Tags,
    string Body,
    int FirstBodyLine,
    string Fingerprint,
    DateOnly? CreatedOn,
    DateTimeOffset ModifiedAt,
    ReviewState Review,
    ImmutableArray<AnnotationView> Annotations);
