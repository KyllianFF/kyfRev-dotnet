using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using KyfRev.Domain.Calendar;

namespace KyfRev.Domain.Reviews;

/// <summary>
/// Suivi d'un élément révisable : fiche entière, passage annoté ou définition.
/// </summary>
/// <remarks>
/// Un suivi est immuable : chaque opération vérifie toutes ses conditions et calcule toutes ses
/// dates, puis rend un nouveau suivi. Une opération refusée laisse donc le suivi d'origine
/// strictement inchangé. Règles appliquées : un Jn est dû n jours après la révision précédente
/// réellement validée (D2, D7) ; la création vaut J0 (D3) ; les délais de rappel se comptent en
/// mois calendaires (D5) ; l'historique ne fait que grandir et les compteurs se recalculent
/// toujours à partir de lui.
/// </remarks>
public sealed class Tracking
{
    private Tracking(ItemState state, CycleCounters counters, ImmutableArray<TrackingEvent> history)
    {
        State = state;
        Counters = counters;
        History = history;
    }

    /// <summary>État actif (avec son cycle) ou archivé.</summary>
    public ItemState State { get; }

    /// <summary>Cycles commencés, terminés et interrompus.</summary>
    public CycleCounters Counters { get; }

    /// <summary>Historique complet, en ajout seul.</summary>
    public ImmutableArray<TrackingEvent> History { get; }

    /// <summary>Statut de l'élément.</summary>
    public TrackingStatus Status => State is ActiveState ? TrackingStatus.Active : TrackingStatus.Archived;

    /// <summary>Cycle en cours, si l'élément est actif.</summary>
    public Cycle? Cycle => (State as ActiveState)?.Cycle;

    /// <summary>Le cycle en cours s'il se présente à réviser : l'élément est actif, et pas suspendu.</summary>
    public Cycle? DueCycle => Cycle is { SuspendedOn: null, IsNote: false } cycle ? cycle : null;

    /// <summary>L'élément est actif, mais suspendu : il ne se présente plus à réviser.</summary>
    public bool IsSuspended => Cycle is { SuspendedOn: not null };

    /// <summary>L'élément est une simple note, tenue hors des révisions.</summary>
    public bool IsNote => Cycle is { IsNote: true };

    /// <summary>Informations d'archivage, si l'élément est archivé.</summary>
    public ArchiveInfo? Archive => (State as ArchivedState)?.Archive;

    /// <summary>Ouvre le suivi d'une fiche ou d'un passage. Le jour même vaut J0 (D3).</summary>
    public static TrackingResult Start(Moment now, ReviewSequence sequence, StartOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (!TryOpenCycle(1, sequence, now.Day, out var cycle, out var error))
        {
            return TrackingResult.Failure(error);
        }
        return TrackingResult.Success(new Tracking(
            new ActiveState(cycle),
            new CycleCounters(Started: 1, Completed: 0, Interrupted: 0),
            [StartReview(now, cycle, origin)]));
    }

    /// <summary>
    /// Reconstitue un suivi relu depuis le disque, après contrôle d'intégrité (voir
    /// <see cref="Verify"/>).
    /// </summary>
    public static TrackingResult Restore(ItemState state, CycleCounters counters, IEnumerable<TrackingEvent> history)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(history);
        var tracking = new Tracking(state, counters, [.. history]);
        return tracking.Verify() is { } error ? TrackingResult.Failure(error) : TrackingResult.Success(tracking);
    }

    /// <summary>
    /// Valide la révision de l'étape attendue. L'étape suivante est due n jours après la date de
    /// révision (D7) ; valider la dernière étape termine le cycle et archive l'élément.
    /// </summary>
    /// <remarks>
    /// Avec <see cref="ReviewRecall.Forgotten"/>, la révision compte de la même façon (elle a eu
    /// lieu, et la relecture a servi de correction), mais l'étape attendue recule d'un cran au lieu
    /// d'avancer : après un oubli à J7, c'est J3 qui est attendu trois jours plus tard. Elle ne
    /// descend jamais sous J1, et un oubli ne termine jamais un cycle.
    /// </remarks>
    /// <param name="now">Moment présent.</param>
    /// <param name="reviewDate">Date de la révision, pour antidater (« je l'ai révisée hier ») ; aujourd'hui par défaut.</param>
    /// <param name="rules">Règles des rappels ; <see langword="null"/> pour un passage, qui n'a pas de rappels.</param>
    /// <param name="recall">Ce que l'utilisateur dit de sa mémoire.</param>
    /// <param name="load">
    /// Lissage, en option : ce qui tombe déjà un jour donné. Avec lui, une étape longue peut se poser sur un jour voisin moins
    /// chargé (voir <see cref="DueSmoothing"/>) ; sans lui, l'échéance est exacte.
    /// </param>
    /// <param name="notAfter">
    /// La veille d'un examen à venir : l'étape suivante ne tombe pas après elle, tant que la révision la précède. Une étape
    /// qui tomberait après l'examen ne servirait plus à le préparer.
    /// </param>
    public ReviewResult ValidateReview(Moment now, DateOnly? reviewDate, ReminderRules? rules, ReviewRecall recall = ReviewRecall.Remembered, Func<DateOnly, int>? load = null, DateOnly? notAfter = null)
    {
        var day = reviewDate ?? now.Day;
        if (day > now.Day)
        {
            return ReviewResult.Failure(TrackingError.FutureDate(day));
        }
        if (State is not ActiveState { Cycle: var cycle })
        {
            return ReviewResult.Failure(TrackingError.NotActive());
        }
        if (cycle.SuspendedOn is not null)
        {
            return ReviewResult.Failure(TrackingError.Suspended());
        }
        if (day == cycle.LastReview)
        {
            return ReviewResult.Failure(TrackingError.AlreadyReviewed(day));
        }
        if (day < cycle.LastReview)
        {
            return ReviewResult.Failure(TrackingError.BeforeLastReview(day, cycle.LastReview));
        }

        var review = new ReviewEvent(
            now.Instant,
            day,
            cycle.Number,
            cycle.StepLabel,
            cycle.DueDate,
            CivilCalendar.DaysBetween(cycle.DueDate, day),
            Origin: null,
            Forgotten: recall == ReviewRecall.Forgotten);

        if (recall == ReviewRecall.Forgotten)
        {
            var previous = Math.Max(cycle.Step - 1, 1);
            if (!CivilCalendar.TryAddDays(day, cycle.Sequence.IntervalBefore(previous), out var again))
            {
                return ReviewResult.Failure(TrackingError.DateOutOfRange());
            }
            again = Capped(Smoothed(again, cycle.Sequence.IntervalBefore(previous), load), day, notAfter);
            var steppedBack = new Tracking(
                new ActiveState(cycle with { Step = previous, LastReview = day, DueDate = again }),
                Counters,
                History.Add(review));
            return ReviewResult.Success(steppedBack, new NextStepScheduled(cycle.Sequence.Label(previous), again));
        }

        if (cycle.Step >= cycle.Sequence.LastStep)
        {
            if (!TryCreateArchive(day, ArchiveReason.EndOfCycle, rules, include: true, out var archive, out var archiveError))
            {
                return ReviewResult.Failure(archiveError);
            }
            var archived = new Tracking(
                new ArchivedState(archive),
                Counters with { Completed = Counters.Completed + 1 },
                History.AddRange(review, new ArchivingEvent(now.Instant, day, ArchiveReason.EndOfCycle)));
            return ReviewResult.Success(archived, new CycleCompleted(day));
        }

        var next = cycle.Step + 1;
        if (!CivilCalendar.TryAddDays(day, cycle.Sequence.IntervalBefore(next), out var dueDate))
        {
            return ReviewResult.Failure(TrackingError.DateOutOfRange());
        }
        dueDate = Capped(Smoothed(dueDate, cycle.Sequence.IntervalBefore(next), load), day, notAfter);
        var advanced = new Tracking(
            new ActiveState(cycle with { Step = next, LastReview = day, DueDate = dueDate }),
            Counters,
            History.Add(review));
        return ReviewResult.Success(advanced, new NextStepScheduled(cycle.Sequence.Label(next), dueDate));
    }

    /// <summary>Déplace l'échéance de l'étape attendue. Ne compte pas comme une révision.</summary>
    public TrackingResult Postpone(Moment now, DateOnly to)
    {
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        if (cycle.SuspendedOn is not null)
        {
            return TrackingResult.Failure(TrackingError.Suspended());
        }
        if (to <= now.Day)
        {
            return TrackingResult.Failure(TrackingError.PostponeInPast(now.Day));
        }
        if (to == cycle.DueDate)
        {
            return TrackingResult.Failure(TrackingError.PostponeUnchanged(to));
        }
        return TrackingResult.Success(new Tracking(
            new ActiveState(cycle with { DueDate = to }),
            Counters,
            History.Add(new PostponementEvent(now.Instant, cycle.Number, cycle.StepLabel, cycle.DueDate, to))));
    }

    /// <summary>
    /// Suspend un élément actif : il garde son étape et son échéance, mais ne se présente plus à réviser (ni dans la liste
    /// du jour, ni dans la charge, ni dans une séance) tant qu'il n'est pas repris. Rien ne s'ajoute à l'historique : ce
    /// n'est ni une révision ni un report.
    /// </summary>
    public TrackingResult Suspend(Moment now)
    {
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        return cycle.SuspendedOn is not null
            ? TrackingResult.Failure(TrackingError.Suspended())
            : TrackingResult.Success(new Tracking(new ActiveState(cycle with { SuspendedOn = now.Day }), Counters, History));
    }

    /// <summary>
    /// Reprend un élément suspendu, là où il en était : même étape, même échéance. Si elle est passée entre-temps, il
    /// revient parmi les retards.
    /// </summary>
    public TrackingResult Resume()
    {
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        return cycle.SuspendedOn is null
            ? TrackingResult.Failure(TrackingError.NotSuspended())
            : TrackingResult.Success(new Tracking(new ActiveState(cycle with { SuspendedOn = null, IsNote = false }), Counters, History));
    }

    /// <summary>
    /// Fait d'un élément actif une simple note : il sort des révisions, comme un élément suspendu, mais sans attendre
    /// d'être repris. Il garde son étape, son échéance et son historique, auquel rien ne s'ajoute.
    /// </summary>
    public TrackingResult MakeNote(Moment now)
    {
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        return cycle.IsNote
            ? TrackingResult.Failure(TrackingError.AlreadyNote())
            : TrackingResult.Success(new Tracking(new ActiveState(cycle with { SuspendedOn = cycle.SuspendedOn ?? now.Day, IsNote = true }), Counters, History));
    }

    /// <summary>
    /// Met une note en révision. Si elle n'a jamais été révisée, elle repart à J0 aujourd'hui, comme une fiche qu'on
    /// vient d'écrire ; sinon elle reprend là où elle en était, même étape et même échéance.
    /// </summary>
    public TrackingResult PutInReview(Moment now, ReviewSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        if (!cycle.IsNote)
        {
            return TrackingResult.Failure(TrackingError.NotNote());
        }
        // Jamais révisée, et pas commencée aujourd'hui : son J0 date du jour où la note a été écrite, pas de celui où l'on
        // décide de l'apprendre.
        return cycle.LastReview == cycle.StartedOn && cycle.StartedOn != now.Day
            ? Restart(now, sequence)
            : TrackingResult.Success(new Tracking(new ActiveState(cycle with { SuspendedOn = null, IsNote = false }), Counters, History));
    }

    /// <summary>
    /// « Recommencer à J0 » (élément actif) ou « Remettre en révision » (élément archivé). Le jour
    /// même vaut J0 ; l'historique est conservé.
    /// </summary>
    public TrackingResult Restart(Moment now, ReviewSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (!TryOpenCycle(Counters.Started + 1, sequence, now.Day, out var cycle, out var error))
        {
            return TrackingResult.Failure(error);
        }
        return TrackingResult.Success(WithNewCycle(now, cycle, StartOrigin.Restart, History));
    }

    /// <summary>Archivage manuel avant la fin du cycle : le cycle est interrompu.</summary>
    /// <param name="now">Moment présent.</param>
    /// <param name="rules">Règles des rappels ; <see langword="null"/> pour un passage, qui n'a pas de rappels.</param>
    public TrackingResult ArchiveManually(Moment now, ReminderRules? rules)
    {
        if (State is not ActiveState { Cycle: var cycle })
        {
            return TrackingResult.Failure(TrackingError.NotActive());
        }
        var include = rules is { IncludeManualArchiving: true };
        if (!TryCreateArchive(now.Day, ArchiveReason.Manual, rules, include, out var archive, out var error))
        {
            return TrackingResult.Failure(error);
        }
        return TrackingResult.Success(new Tracking(
            new ArchivedState(archive),
            Counters with { Interrupted = Counters.Interrupted + 1 },
            History.AddRange(
                new InterruptionEvent(now.Instant, now.Day, cycle.Number),
                new ArchivingEvent(now.Instant, now.Day, ArchiveReason.Manual))));
    }

    /// <summary>Vrai si la fiche peut être tirée au sort le jour donné.</summary>
    public bool IsEligibleOn(DateOnly day) =>
        Archive is { IncludedInReminders: true, ReminderDrawnOn: null } archive && archive.EligibleOn <= day;

    /// <summary>Enregistre que la fiche a été tirée pour un rappel de consolidation.</summary>
    public TrackingResult MarkDrawn(DateOnly day)
    {
        if (State is not ArchivedState { Archive: var archive })
        {
            return TrackingResult.Failure(TrackingError.NotArchived());
        }
        if (!archive.IncludedInReminders)
        {
            return TrackingResult.Failure(TrackingError.ExcludedFromReminders());
        }
        if (archive.ReminderDrawnOn is not null)
        {
            return TrackingResult.Failure(TrackingError.ReminderAlreadyPending());
        }
        if (day < archive.EligibleOn)
        {
            return TrackingResult.Failure(TrackingError.NotYetEligible(archive.EligibleOn));
        }
        return TrackingResult.Success(new Tracking(
            new ArchivedState(archive with { ReminderDrawnOn = day }),
            Counters,
            History));
    }

    /// <summary>
    /// Enregistre le résultat d'un rappel de consolidation.
    /// <list type="bullet">
    /// <item>« Fiche connue » : la fiche reste archivée et se repose plus longtemps.</item>
    /// <item>« Certains points sont à revoir » : la fiche reste archivée, le repos repart au
    /// premier niveau ; les passages sont gérés par l'appelant.</item>
    /// <item>« Fiche à revoir entièrement » : nouveau cycle, le rappel vaut J0.</item>
    /// </list>
    /// </summary>
    public TrackingResult HandleReminder(Moment now, ReminderAnswer answer, ReminderRules rules, ReviewSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(sequence);
        if (rules.Validate() is { } rulesError)
        {
            return TrackingResult.Failure(rulesError);
        }
        if (State is not ArchivedState { Archive: var archive })
        {
            return TrackingResult.Failure(TrackingError.NotArchived());
        }
        if (archive.ReminderDrawnOn is not DateOnly drawnOn)
        {
            return TrackingResult.Failure(TrackingError.NoPendingReminder());
        }

        var day = now.Day;
        var reminder = new ReminderEvent(now.Instant, day, drawnOn, answer.Outcome);
        switch (answer.Outcome)
        {
            case ReminderOutcome.Known:
                {
                    var level = Math.Min(archive.RestLevel + 1, rules.RestSequenceMonths.Length - 1);
                    return Rest(level, rules, day, archive, reminder);
                }
            case ReminderOutcome.SomePointsToReview:
                return answer.MarkedPassages == 0
                    ? TrackingResult.Failure(TrackingError.NoPassageMarked())
                    : Rest(0, rules, day, archive, reminder);
            default:
                if (!TryOpenCycle(Counters.Started + 1, sequence, day, out var cycle, out var error))
                {
                    return TrackingResult.Failure(error);
                }
                return TrackingResult.Success(WithNewCycle(now, cycle, StartOrigin.Reminder, History.Add(reminder)));
        }
    }

    /// <summary>Inclut la fiche archivée dans les rappels de consolidation, ou l'en exclut.</summary>
    public TrackingResult IncludeInReminders(Moment now, bool include)
    {
        if (State is not ArchivedState { Archive: var archive })
        {
            return TrackingResult.Failure(TrackingError.NotArchived());
        }
        if (archive.IncludedInReminders == include)
        {
            return TrackingResult.Success(this);
        }
        var updated = archive with
        {
            IncludedInReminders = include,
            ReminderDrawnOn = include ? archive.ReminderDrawnOn : null,
        };
        return TrackingResult.Success(new Tracking(
            new ArchivedState(updated),
            Counters,
            History.Add(new ReminderInclusionEvent(now.Instant, include))));
    }

    /// <summary>
    /// Inscrit dans l'historique la modification d'un passage annoté ; ni l'état ni le calendrier
    /// ne changent.
    /// </summary>
    public Tracking RecordModification(Moment now, ModifiedObject changed) =>
        new(State, Counters, History.Add(new ModificationEvent(now.Instant, changed)));

    /// <summary>
    /// Contrôle d'intégrité d'un suivi relu depuis le disque : les compteurs doivent correspondre
    /// à l'historique et à l'état.
    /// </summary>
    /// <returns><see langword="null"/> si le suivi est cohérent, sinon la raison.</returns>
    public TrackingError? Verify()
    {
        if (History.IsDefaultOrEmpty || History[0] is not ReviewEvent { Origin: not null })
        {
            return TrackingError.Inconsistent("l'historique doit commencer par un J0");
        }

        var recount = new CycleCounters(
            Started: History.Count(e => e is ReviewEvent { Origin: not null }),
            Completed: History.Count(e => e is ArchivingEvent { Reason: ArchiveReason.EndOfCycle }),
            Interrupted: History.Count(e => e is InterruptionEvent));
        if (recount != Counters)
        {
            return TrackingError.Inconsistent("les compteurs ne correspondent pas à l'historique");
        }

        var active = Status == TrackingStatus.Active ? 1 : 0;
        if (Counters.Started != Counters.Completed + Counters.Interrupted + active)
        {
            return TrackingError.Inconsistent("nombre de cycles commencés incohérent");
        }

        return State switch
        {
            ActiveState { Cycle: var cycle } when cycle.Number != Counters.Started =>
                TrackingError.Inconsistent("numéro de cycle incohérent"),
            ActiveState { Cycle: var cycle } when cycle.Step == 0 || cycle.Step > cycle.Sequence.LastStep =>
                TrackingError.Inconsistent("étape hors de la séquence"),
            ActiveState { Cycle: var cycle } when cycle.StartedOn > cycle.LastReview || cycle.DueDate <= cycle.LastReview =>
                TrackingError.Inconsistent("dates du cycle incohérentes"),
            ArchivedState { Archive: var archive } when archive.EligibleOn < archive.ArchivedOn =>
                TrackingError.Inconsistent("date d'éligibilité antérieure à l'archivage"),
            _ => null,
        };
    }

    private TrackingResult Rest(int level, ReminderRules rules, DateOnly day, ArchiveInfo archive, ReminderEvent reminder)
    {
        if (!CivilCalendar.TryAddMonths(day, rules.RestSequenceMonths[level], out var eligibleOn))
        {
            return TrackingResult.Failure(TrackingError.DateOutOfRange());
        }
        return TrackingResult.Success(new Tracking(
            new ArchivedState(archive with { RestLevel = level, EligibleOn = eligibleOn, ReminderDrawnOn = null }),
            Counters,
            History.Add(reminder)));
    }

    /// <summary>Applique un nouveau cycle déjà construit (et ses dates déjà calculées).</summary>
    private Tracking WithNewCycle(Moment now, Cycle cycle, StartOrigin origin, ImmutableArray<TrackingEvent> history)
    {
        var counters = Counters;
        TrackingEvent closing;
        if (State is ActiveState { Cycle: var previous })
        {
            closing = new InterruptionEvent(now.Instant, now.Day, previous.Number);
            counters = counters with { Interrupted = counters.Interrupted + 1 };
        }
        else
        {
            closing = new ReactivationEvent(now.Instant, now.Day);
        }
        return new Tracking(
            new ActiveState(cycle),
            counters with { Started = counters.Started + 1 },
            history.AddRange(closing, StartReview(now, cycle, origin)));
    }

    /// <summary>
    /// Cycle dont le J0 est validé le jour donné : J1 est attendu le lendemain avec la séquence par
    /// défaut. Une séquence compte toujours au moins deux étapes : J1 existe.
    /// </summary>
    private static bool TryOpenCycle(
        int number,
        ReviewSequence sequence,
        DateOnly day,
        [NotNullWhen(true)] out Cycle? cycle,
        [NotNullWhen(false)] out TrackingError? error)
    {
        cycle = null;
        if (!CivilCalendar.TryAddDays(day, sequence.IntervalBefore(1), out var dueDate))
        {
            error = TrackingError.DateOutOfRange();
            return false;
        }
        cycle = new Cycle(number, sequence, day, Step: 1, dueDate, LastReview: day);
        error = null;
        return true;
    }

    /// <param name="day">Jour de l'archivage.</param>
    /// <param name="reason">Fin de cycle ou archivage manuel.</param>
    /// <param name="rules">Règles des rappels ; <see langword="null"/> pour un élément sans rappels (passage).</param>
    /// <param name="include">Vrai si l'élément doit recevoir des rappels.</param>
    /// <param name="archive">Informations d'archivage créées.</param>
    /// <param name="error">Raison du refus.</param>
    private static bool TryCreateArchive(
        DateOnly day,
        ArchiveReason reason,
        ReminderRules? rules,
        bool include,
        [NotNullWhen(true)] out ArchiveInfo? archive,
        [NotNullWhen(false)] out TrackingError? error)
    {
        archive = null;
        var eligibleOn = day;
        if (rules is not null)
        {
            if (rules.Validate() is { } rulesError)
            {
                error = rulesError;
                return false;
            }
            if (!CivilCalendar.TryAddMonths(day, rules.MinimumDelayMonths, out eligibleOn))
            {
                error = TrackingError.DateOutOfRange();
                return false;
            }
        }
        archive = new ArchiveInfo(
            day,
            reason,
            IncludedInReminders: include && rules is not null,
            RestLevel: 0,
            eligibleOn,
            ReminderDrawnOn: null);
        error = null;
        return true;
    }

    private static DateOnly Smoothed(DateOnly exact, int interval, Func<DateOnly, int>? load) =>
        load is null ? exact : DueSmoothing.Choose(exact, interval, load);

    /// <summary>Ramène une échéance à la limite, si la révision la précède ; l'échéance reste après la révision.</summary>
    private static DateOnly Capped(DateOnly dueDate, DateOnly reviewed, DateOnly? notAfter) =>
        notAfter is { } limit && limit > reviewed && dueDate > limit ? limit : dueDate;

    private static ReviewEvent StartReview(Moment now, Cycle cycle, StartOrigin origin) =>
        new(now.Instant, now.Day, cycle.Number, cycle.Sequence.Label(0), now.Day, DaysLate: 0, origin);
}
