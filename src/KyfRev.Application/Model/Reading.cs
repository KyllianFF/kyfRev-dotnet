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
        var labels = tracking.Cycle is { } current
            ? Enumerable.Range(0, current.Sequence.LastStep + 1).Select(current.Sequence.Label)
            : done.Select(review => review.StepLabel);
        return
        [
            .. labels.Select(label => done.FirstOrDefault(review => review.StepLabel == label) is { } review
                ? new CycleStep(label, review.Day, review.DaysLate)
                : new CycleStep(label, null, 0)),
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
public sealed record Definition(string Term, string Explanation);

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
